using System;
using System.Collections.Generic;
using System.Diagnostics;
using Unity.Collections;
using Unity.Burst;
using Unity.Jobs;

// Six centre-to-face pyramids per cell, pairwise exponential exchange, then instantaneous remixing.
// Cached J/K transfer factors divided by their timestep reuse the existing Burst energy kernel.
public sealed class ThermalExchange : IDisposable
{
    public readonly struct Cell
    {
        public readonly EquipmentThermalState Owner;
        public readonly int Index;
        public readonly string Label;
        public readonly bool IsSlotAir;
        public Cell(EquipmentThermalState owner, int index, string label, bool isSlotAir = false)
        { Owner = owner; Index = index; Label = label; IsSlotAir = isSlotAir; }
        public int MemberCount => IsSlotAir ? HiveGridLayout.SlotDepthCells : 1;
        public int MemberIndex(int layer) => Index + layer * HiveGridLayout.SizeX;
        public float HeatCapacityJPerK => Sum(Owner.HeatCapacityJPerK);
        public float EnergyJ => Sum(Owner.EnergyJ);
        private float Sum(IReadOnlyList<float> values)
        {
            if (!IsSlotAir) return values[Index];
            double total = 0;
            for (int k = 0; k < MemberCount; k++) total += values[MemberIndex(k)];
            return (float)total;
        }
        // All four members are the same air material and share the X/Z area.
        internal float MemberEnergy(float total, int member)
        {
            if (!IsSlotAir) return total;
            int layer = (member - Index) / HiveGridLayout.SizeX;
            return (float)(total * HiveGridLayout.SlotLayerEnergyShare(layer));
        }
    }
    public readonly struct Face
    {
        public readonly int A, B, Axis;
        public readonly double AreaM2, HalfAM, HalfBM;
        public Face(int a, int b, double area, double halfA, double halfB, int axis = 0)
        { A = a; B = b; AreaM2 = area; HalfAM = halfA; HalfBM = halfB; Axis = axis; }
    }
    public readonly struct Boundary
    {
        public readonly int Cell, Direction; // -/+ X, -/+ Y, -/+ Z = 0..5
        public readonly double AreaM2, HalfM;
        public Boundary(int cell, int direction, double area, double half)
        { Cell=cell; Direction=direction; AreaM2=area; HalfM=half; }
    }
    private readonly Boundary[] boundaries;
    private readonly ThermalTransferCoefficients transfers;
    private readonly double[] exterior, rawExterior;
    public int BoundaryCount => boundaries.Length;
    public Boundary GetBoundary(int i) => boundaries[i];
    public double ExteriorConductance(int i) => exterior[i];
    public double RawExteriorConductance(int i) => rawExterior[i];
    public double ExternalEnergyJ { get; private set; } // Diagnostic mode only; positive means energy entering the model. Zero when disabled.
    private readonly Cell[] cells;
    private readonly Face[] faces;
    private readonly int[] offsets, neighbours;
    private readonly double[] coefficients, sums;
    // Exact float capacities in solver-cell order; refreshed with the coefficient revision.
    private readonly float[] cachedCapacities;
    public long CapacityCacheBytes => 4L * cachedCapacities.Length;
    private readonly int[] revisions;
    private readonly EquipmentThermalState[] owners;
    // Explicit tooling opt-ins. Normal minute updates perform neither audits nor timing.
    public bool DiagnosticsEnabled { get; set; }
    public bool MeasurePerformance { get; set; }
    private const int RowStride = HiveGridLayout.SizeX;
    private const int PlaneStride = RowStride * HiveGridLayout.SizeY;
    private NativeArray<int> nativeOffsets, nativeNeighbours;
    private NativeArray<byte> nativeFixedRows;
    private NativeArray<double> nativeG, nativeExterior, nativeT, nativeEnergy, nativeNext, nativeNextT;
    private NativeArray<float> nativeCapacity;
    internal ThermalTopology.Binding TopologyBinding;
    private readonly Dictionary<EquipmentThermalState,int[]> ownerIndices = new Dictionary<EquipmentThermalState,int[]>();
    private readonly int[] rowFaces, faceSlotA, faceSlotB, boundaryOffsets, rowBoundaries;
    private readonly double[] rawFaces, rawBoundaries;
    private double coefficientSeconds=60;
    private readonly WorkSet changedCells, rawWork, finalWork;
    public int LastChangedCells { get; private set; }
    public int LastRawFacesUpdated { get; private set; }
    public int LastEffectiveFacesUpdated { get; private set; }
    private int uploadedBuild = -1;
    private static readonly HashSet<ThermalExchange> activeBuffers = new HashSet<ThermalExchange>();
    public int BufferAllocations { get; private set; }
    public int CoefficientUploads { get; private set; }
    public bool HasBuffers => nativeOffsets.IsCreated;
    public long BufferBytes => HasBuffers ? 4L * (offsets.Length + neighbours.Length) + 8L * (coefficients.Length + 5L * cells.Length) + 5L * cells.Length : 0;
    // Between completed minutes nativeEnergy holds the last committed float energy.
    // Equipment reads and writes its active rows through its stable owner-local mapping.
    private bool energyBound;
    internal float ReadEnergy(int row, int member) => cells[row].MemberEnergy((float)nativeEnergy[row], member);
    internal void SetEnergy(int row, int member, float value)
    {
        if (cells[row].IsSlotAir)
        {
            // A per-cell external write is applied to its physical owner before re-mixing.
            ReleaseEnergy();
            cells[row].Owner.CommitExchangeEnergy(member, value);
            return;
        }
        nativeEnergy[row] = value;
        nativeT[row] = (double)value / nativeCapacity[row];
    }
    private void ReleaseEnergy()
    {
        if (!energyBound) return;
        for (int i = 0; i < cells.Length; i++)
        {
            var cell = cells[i];
            for (int k = 0; k < cell.MemberCount; k++)
            {
                int member = cell.MemberIndex(k);
                cell.Owner.CommitExchangeEnergy(member, ReadEnergy(i, member));
            }
        }
        foreach (var owner in owners) owner.BindEnergy(null, null);
        energyBound = false;
    }
    public void Dispose()
    {
        ReleaseEnergy();
        if (nativeFixedRows.IsCreated) nativeFixedRows.Dispose();
        if (nativeOffsets.IsCreated) nativeOffsets.Dispose();
        if (nativeNeighbours.IsCreated) nativeNeighbours.Dispose();
        if (nativeG.IsCreated) nativeG.Dispose();
        if (nativeExterior.IsCreated) nativeExterior.Dispose();
        if (nativeCapacity.IsCreated) nativeCapacity.Dispose();
        if (nativeNextT.IsCreated) nativeNextT.Dispose();
        if (nativeT.IsCreated) nativeT.Dispose();
        if (nativeEnergy.IsCreated) nativeEnergy.Dispose();
        if (nativeNext.IsCreated) nativeNext.Dispose();
        uploadedBuild = -1;
        activeBuffers.Remove(this);
    }
    public static void ReleaseAllBuffers()
    {
        foreach (var exchange in new List<ThermalExchange>(activeBuffers)) exchange.Dispose();
    }
    [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetBuffers()
    {
        ReleaseAllBuffers();
        UnityEngine.Application.quitting -= ReleaseAllBuffers;
        UnityEngine.Application.quitting += ReleaseAllBuffers;
    }
    // Compare the actual solver rows, not the nominal geometry: merged slots and
    // stack boundaries can change the offsets. Keep the original sum order.
    private bool HasFixedNeighbours(int row)
    {
        int p = offsets[row];
        return offsets[row + 1] - p == 6
            && neighbours[p]     == row - PlaneStride
            && neighbours[p + 1] == row - RowStride
            && neighbours[p + 2] == row - 1
            && neighbours[p + 3] == row + 1
            && neighbours[p + 4] == row + RowStride
            && neighbours[p + 5] == row + PlaneStride;
    }

    private void PrepareBuffers()
    {
        if (!HasBuffers)
        {
            try
            {
                nativeFixedRows = new NativeArray<byte>(cells.Length, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
                for (int i = 0; i < cells.Length; i++)
                    nativeFixedRows[i] = (byte)(HasFixedNeighbours(i) ? 1 : 0);
                nativeOffsets = new NativeArray<int>(offsets, Allocator.Persistent);
                nativeNeighbours = new NativeArray<int>(neighbours, Allocator.Persistent);
                nativeG = new NativeArray<double>(coefficients.Length, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
                nativeExterior = new NativeArray<double>(cells.Length, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
                nativeT = new NativeArray<double>(cells.Length, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
                nativeNextT = new NativeArray<double>(cells.Length, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
                nativeCapacity = new NativeArray<float>(cells.Length, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
                nativeEnergy = new NativeArray<double>(cells.Length, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
                nativeNext = new NativeArray<double>(cells.Length, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
                activeBuffers.Add(this);
                BufferAllocations++;
            }
            catch { Dispose(); throw; }
        }
        if (uploadedBuild != CoefficientBuilds)
        {
            nativeCapacity.CopyFrom(cachedCapacities);
            nativeG.CopyFrom(coefficients);
            nativeExterior.CopyFrom(exterior);
            uploadedBuild = CoefficientBuilds;
            CoefficientUploads++;
        }
    }
    public int CellCount => cells.Length;
    public int FaceCount => faces.Length;
    public int MaximumDegree { get; private set; }
    public int CoefficientBuilds { get; private set; }
    public double LastJobMilliseconds { get; private set; }
    public double LastTickMilliseconds { get; private set; }
    public string Diagnostic { get; private set; }
    public Cell GetCell(int i) => cells[i];
    public Face GetFace(int i) => faces[i];
    public double SumConductance(int i) => sums[i];
    public ThermalExchange(Cell[] source, Face[] connections, Boundary[] exteriorBoundaries = null, ThermalTransferCoefficients transfers = null)
    {
        cells = (Cell[])source.Clone(); faces = (Face[])connections.Clone();
        this.transfers=transfers??ThermalTransferCoefficients.Default;
        boundaries = exteriorBoundaries == null ? Array.Empty<Boundary>() : (Boundary[])exteriorBoundaries.Clone();
        var boundaryKeys = new HashSet<(int,int)>();
        foreach (var b in boundaries)
            if (b.Cell < 0 || b.Cell >= cells.Length || b.Direction < 0 || b.Direction > 5 || !Positive(b.AreaM2) || !Positive(b.HalfM) || !boundaryKeys.Add((b.Cell,b.Direction)))
                throw new ArgumentException("Invalid/duplicate exterior boundary");
        exterior = new double[cells.Length]; rawExterior = new double[cells.Length];
        cachedCapacities = new float[cells.Length];
        offsets = new int[cells.Length + 1];
        var ownerSet = new HashSet<EquipmentThermalState>();
        var ownerList = new List<EquipmentThermalState>();
        foreach(var cell in cells) if(ownerSet.Add(cell.Owner)) ownerList.Add(cell.Owner);
        owners = ownerList.ToArray(); revisions = new int[owners.Length];
        for(int i=0;i<revisions.Length;i++) revisions[i]=-1;
        var unique = new HashSet<(EquipmentThermalState, int)>();
        for (int i = 0; i < cells.Length; i++)
        {
            if (cells[i].Owner == null || cells[i].Index < 0 || cells[i].Index >= cells[i].Owner.Count || !unique.Add((cells[i].Owner, cells[i].Index)))
                throw new InvalidOperationException("Duplicate or invalid thermal owner: " + cells[i].Label);
        }
        var pairs = new HashSet<(int, int)>();
        foreach (var f in faces)
        {
            if (f.A < 0 || f.B < 0 || f.A >= cells.Length || f.B >= cells.Length || f.A == f.B ||
                f.Axis<0 || f.Axis>2 || !Positive(f.AreaM2) || !Positive(f.HalfAM) || !Positive(f.HalfBM) || !pairs.Add((Math.Min(f.A,f.B),Math.Max(f.A,f.B))))
                throw new InvalidOperationException("Invalid/duplicate thermal face");
            offsets[f.A+1]++; offsets[f.B+1]++;
        }
        for (int i = 1; i < offsets.Length; i++) offsets[i] += offsets[i-1];
        for (int i = 0; i < cells.Length; i++) MaximumDegree = Math.Max(MaximumDegree, offsets[i+1]-offsets[i]);
        neighbours = new int[2*faces.Length]; coefficients = new double[neighbours.Length]; sums = new double[cells.Length];
        rowFaces = new int[neighbours.Length]; faceSlotA = new int[faces.Length]; faceSlotB = new int[faces.Length];
        rawFaces = new double[faces.Length]; rawBoundaries = new double[boundaries.Length];
        changedCells = new WorkSet(cells.Length); rawWork = new WorkSet(faces.Length);
        finalWork = new WorkSet(cells.Length);
        var cursor = (int[])offsets.Clone();
        for (int f=0;f<faces.Length;f++) {
            var edge=faces[f]; int a=cursor[edge.A]++, b=cursor[edge.B]++;
            faceSlotA[f]=a; faceSlotB[f]=b; rowFaces[a]=rowFaces[b]=f;
            neighbours[a]=edge.B; neighbours[b]=edge.A;
        }
        boundaryOffsets=new int[cells.Length+1];
        foreach(var b in boundaries) boundaryOffsets[b.Cell+1]++;
        for(int i=1;i<boundaryOffsets.Length;i++)boundaryOffsets[i]+=boundaryOffsets[i-1];
        rowBoundaries=new int[boundaries.Length];cursor=(int[])boundaryOffsets.Clone();
        for(int b=0;b<boundaries.Length;b++)rowBoundaries[cursor[boundaries[b].Cell]++]=b;
        foreach(var owner in owners) {var indices=new int[owner.Count];for(int j=0;j<indices.Length;j++)indices[j]=-1;ownerIndices.Add(owner,indices);}
        for (int i = 0; i < cells.Length; i++)
            for (int k = 0; k < cells[i].MemberCount; k++)
                ownerIndices[cells[i].Owner][cells[i].MemberIndex(k)] = i;
    }
    public static bool Finite(double v) => !double.IsNaN(v) && !double.IsInfinity(v);
    private static bool Positive(double v) => Finite(v) && v > 0;
    public static double Conductance(double area, double halfA, double kA, double halfB, double kB)
    {
        if (!Positive(area) || !Positive(halfA) || !Positive(halfB) || !Positive(kA) || !Positive(kB)) throw new InvalidOperationException("Invalid face resistance");
        double g = area / (halfA / kA + halfB / kB);
        if (!Positive(g)) throw new InvalidOperationException("Non-finite face conductance");
        return g;
    }
    public double FacePyramidCapacity(int row,int axis,double contactArea)
    {
        var c=cells[row];double fraction=contactArea/c.Owner.FaceAreaM2(c.Index,axis,c.IsSlotAir);
        if(!Positive(fraction)||fraction>1+1e-6)throw new InvalidOperationException("Contact exceeds pyramid face: "+c.Label);
        return c.HeatCapacityJPerK*fraction/6;
    }
    // Stable evaluation of 1-exp(-x), including very weak connections.
    private static double Relaxation(double x) => x<1e-5 ? x*(1-x*(.5-x*(1d/6-x/24))) : 1-Math.Exp(-x);
    private double FaceConductance(Face f)
    {
        var a=cells[f.A];var b=cells[f.B];
        double g=f.AreaM2*transfers.Between(a.Owner.ExchangeMaterial(a.Index),b.Owner.ExchangeMaterial(b.Index));
        double ca=FacePyramidCapacity(f.A,f.Axis,f.AreaM2),cb=FacePyramidCapacity(f.B,f.Axis,f.AreaM2),reduced=ca*cb/(ca+cb);
        return reduced*Relaxation(coefficientSeconds*g/reduced)/coefficientSeconds;
    }
    private double BoundaryConductance(Boundary b,out double raw)
    {
        var c=cells[b.Cell];double capacity=FacePyramidCapacity(b.Cell,b.Direction/2,b.AreaM2);
        // Fixed-temperature weather reservoir; independently tuned material-to-exterior coefficients.
        raw=b.AreaM2*transfers.Outside(c.Owner.ExchangeMaterial(c.Index));
        return capacity*Relaxation(coefficientSeconds*raw/capacity)/coefficientSeconds;
    }
    private string Detail(int i,string reason)=>reason+"; "+cells[i].Label;
    private sealed class WorkSet
    {
        private readonly bool[] marked;
        public readonly List<int> Values = new List<int>();
        public WorkSet(int count) { marked=new bool[count]; }
        public void Add(int i) {if(!marked[i]){marked[i]=true;Values.Add(i);}}
        public void Clear() {foreach(int i in Values)marked[i]=false;Values.Clear();}
    }
    internal bool CanRebind(EquipmentThermalState owner) => ownerIndices.ContainsKey(owner);
    internal void RebindCells(List<KeyValuePair<int,Cell>> replacements)
    {
        if (replacements.Count > 0) ReleaseEnergy();
        // Clear all old mappings before assigning: a move may exchange two owners.
        foreach(var pair in replacements){var old=cells[pair.Key];ownerIndices[old.Owner][old.Index]=-1;}
        foreach(var pair in replacements){cells[pair.Key]=pair.Value;ownerIndices[pair.Value.Owner][pair.Value.Index]=pair.Key;changedCells.Add(pair.Key);}
    }
    private void UpdateCell(int i)
    {
        cachedCapacities[i]=cells[i].HeatCapacityJPerK;
        if(!Positive(cachedCapacities[i]))throw new InvalidOperationException(Detail(i,"Invalid capacity"));
        double sum=0,raw=0;
        for(int p=boundaryOffsets[i];p<boundaryOffsets[i+1];p++){int id=rowBoundaries[p];sum+=BoundaryConductance(boundaries[id],out double g);rawBoundaries[id]=g;raw+=g;}
        exterior[i]=sum;rawExterior[i]=raw;
    }
    private void UpdateFace(int id)
    {
        double g=FaceConductance(faces[id]);rawFaces[id]=g;
        coefficients[faceSlotA[id]]=coefficients[faceSlotB[id]]=g;
    }
    private void SumCell(int i)
    {
        double sum=exterior[i];for(int p=offsets[i];p<offsets[i+1];p++)sum+=coefficients[p];sums[i]=sum;
        if(!Finite(sum)||coefficientSeconds*sum>cachedCapacities[i]*(1+1e-6))throw new InvalidOperationException(Detail(i,"Overlapping pyramid capacity"));
    }
    private void InitializeCoefficients()
    {
        for(int i=0;i<cells.Length;i++)UpdateCell(i);
        for(int f=0;f<faces.Length;f++)UpdateFace(f);
        for(int i=0;i<cells.Length;i++)SumCell(i);
        for(int k=0;k<owners.Length;k++)revisions[k]=owners[k].CacheRevision;
        LastChangedCells=cells.Length;LastRawFacesUpdated=LastEffectiveFacesUpdated=faces.Length;CoefficientBuilds++;
        changedCells.Clear();rawWork.Clear();finalWork.Clear();
    }
    public void RefreshCoefficients()=>RefreshCoefficients(coefficientSeconds);
    private void RefreshCoefficients(double seconds)
    {
        if(!Positive(seconds))throw new ArgumentOutOfRangeException(nameof(seconds));
        if(CoefficientBuilds==0||seconds!=coefficientSeconds){coefficientSeconds=seconds;InitializeCoefficients();return;}
        for(int k=0;k<owners.Length;k++){
            var owner=owners[k];if(revisions[k]==owner.CacheRevision)continue;
            var indices=ownerIndices[owner];
            for(int j=0;j<indices.Length;j++)if(indices[j]>=0&&owner.CellCacheRevision(j)>revisions[k])changedCells.Add(indices[j]);
        }
        for(int k=0;k<owners.Length;k++)revisions[k]=owners[k].CacheRevision;
        LastChangedCells=changedCells.Values.Count;LastRawFacesUpdated=LastEffectiveFacesUpdated=0;
        if(changedCells.Values.Count==0)return;
        foreach(int i in changedCells.Values){UpdateCell(i);finalWork.Add(i);for(int p=offsets[i];p<offsets[i+1];p++)rawWork.Add(rowFaces[p]);}
        foreach(int id in rawWork.Values){UpdateFace(id);finalWork.Add(faces[id].A);finalWork.Add(faces[id].B);}
        foreach(int i in finalWork.Values)SumCell(i);
        LastRawFacesUpdated=LastEffectiveFacesUpdated=rawWork.Values.Count;
        bool patch=HasBuffers&&uploadedBuild==CoefficientBuilds;CoefficientBuilds++;
        if(patch){
            foreach(int id in rawWork.Values){nativeG[faceSlotA[id]]=coefficients[faceSlotA[id]];nativeG[faceSlotB[id]]=coefficients[faceSlotB[id]];}
            foreach(int i in changedCells.Values){nativeExterior[i]=exterior[i];nativeCapacity[i]=cachedCapacities[i];if(energyBound)nativeT[i]=nativeEnergy[i]/cachedCapacities[i];}
            uploadedBuild=CoefficientBuilds;CoefficientUploads++;
        }
        changedCells.Clear();rawWork.Clear();finalWork.Clear();
    }
    public void ValidateStep(double seconds)=>RefreshCoefficients(seconds);
    [BurstCompile(FloatMode = FloatMode.Strict, FloatPrecision = FloatPrecision.Standard, CompileSynchronously = true)]
    private struct ConvertTemperatures : IJobParallelFor
    {
        [ReadOnly] public NativeArray<double> Energy;
        [ReadOnly] public NativeArray<float> Capacity;
        [WriteOnly] public NativeArray<double> Temperature;
        public void Execute(int i) => Temperature[i] = Energy[i] / Capacity[i];
    }
    [BurstCompile(FloatMode = FloatMode.Strict, FloatPrecision = FloatPrecision.Standard, CompileSynchronously = true)]
    private struct Gather : IJobParallelFor
    {
        [ReadOnly] public NativeArray<int> Offsets, Neighbours;
        [ReadOnly] public NativeArray<byte> FixedRows;
        [ReadOnly] public NativeArray<double> G, T, Energy, Exterior;
        [ReadOnly] public NativeArray<float> Capacity;
        [WriteOnly] public NativeArray<double> Next, NextT;
        public double Seconds, OutsideC;
        public void Execute(int i)
        {
            double watts = Exterior[i] * (OutsideC - T[i]);
            int start = Offsets[i];
            double temperature = T[i];
            if (FixedRows[i] != 0)
            {
                watts += G[start]     * (T[i - PlaneStride] - temperature);
                watts += G[start + 1] * (T[i - RowStride] - temperature);
                watts += G[start + 2] * (T[i - 1] - temperature);
                watts += G[start + 3] * (T[i + 1] - temperature);
                watts += G[start + 4] * (T[i + RowStride] - temperature);
                watts += G[start + 5] * (T[i + PlaneStride] - temperature);
            }
            else
            {
                for (int p = start; p < Offsets[i + 1]; p++)
                    watts += G[p] * (T[Neighbours[p]] - temperature);
            }
            double committedEnergy = (float)(Energy[i] + Seconds * watts);
            Next[i] = committedEnergy;
            NextT[i] = committedEnergy / Capacity[i];
        }
    }
    // Results are committed directly after the Job; result-range and energy-balance audits live in editor tests.
    public void Step(double seconds = 60, double? outsideC = null)
    {
        RefreshCoefficients(seconds);
        StepPrepared(seconds,outsideC);
    }
    // ThermalWorld has already refreshed state, topology and coefficients.
    internal void StepPrepared(double seconds, double? outsideC)
    {
        if(seconds!=coefficientSeconds)RefreshCoefficients(seconds);
        bool measure=MeasurePerformance || DiagnosticsEnabled;
        var total = measure ? Stopwatch.StartNew() : null; Diagnostic = null;
        try
        {
            ExternalEnergyJ = 0;
            if(DiagnosticsEnabled)
            {
                if (boundaries.Length > 0 && !outsideC.HasValue) throw new ArgumentException("Exterior temperature is required");
                if (outsideC.HasValue && (!Finite(outsideC.Value) || outsideC.Value < -273.15)) throw new ArgumentOutOfRangeException(nameof(outsideC));
                ValidateStep(seconds);
            }
            double ambient=outsideC ?? 0;
            bool rebuildTemperatures = !energyBound || uploadedBuild != CoefficientBuilds;
            PrepareBuffers();
            var o=nativeOffsets; var n=nativeNeighbours; var g=nativeG; var ext=nativeExterior;
            var t=nativeT; var e=nativeEnergy; var next=nativeNext;
            {
                if (!energyBound)
                {
                    // Another solver (for example a diagnostic one) can share these owners.
                    foreach (var owner in owners) owner.ActiveExchange?.ReleaseEnergy();
                    for (int i = 0; i < cells.Length; i++)
                        e[i] = cells[i].EnergyJ;
                }
                JobHandle temperaturesReady = default;
                if (rebuildTemperatures)
                    temperaturesReady = new ConvertTemperatures { Energy=e, Capacity=nativeCapacity, Temperature=t }
                        .Schedule(cells.Length, 64);
                if(DiagnosticsEnabled)
                {
                    temperaturesReady.Complete();
                    double externalEnergy=0,externalCorrection=0;
                    for(int i=0;i<cells.Length;i++)
                    {
                        if(!Finite(t[i]) || t[i]<-273.15) throw new InvalidOperationException(Detail(i,"Invalid old temperature"));
                        externalEnergy=Add(externalEnergy,seconds*exterior[i]*(ambient-t[i]),ref externalCorrection);
                    }
                    ExternalEnergyJ=externalEnergy;
                }
                var jobTime = measure ? Stopwatch.StartNew() : null;
                new Gather {Offsets=o,Neighbours=n,FixedRows=nativeFixedRows,G=g,T=t,Energy=e,Next=next,NextT=nativeNextT,Capacity=nativeCapacity,Seconds=seconds,Exterior=ext,OutsideC=ambient}.Schedule(cells.Length,64,temperaturesReady).Complete();
                LastJobMilliseconds = jobTime?.Elapsed.TotalMilliseconds ?? 0;
                // Publish only after the complete job succeeds.
                nativeT = nativeNextT;
                nativeNextT = t;
                nativeEnergy = next;
                nativeNext = e;
                if (!energyBound)
                {
                    foreach (var owner in owners) owner.BindEnergy(this, ownerIndices[owner]);
                    energyBound = true;
                }
            }
        }
        catch (Exception ex) { Diagnostic = ex.Message; throw; }
        finally { LastTickMilliseconds = total?.Elapsed.TotalMilliseconds ?? 0; }
    }
    private static double Add(double sum, double value, ref double correction)
    { double y=value-correction, result=sum+y; correction=(result-sum)-y; return result; }
}
