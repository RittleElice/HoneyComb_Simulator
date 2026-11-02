using System;
using System.Collections.Generic;
using System.Diagnostics;
using Unity.Collections;
using Unity.Burst;
using Unity.Jobs;

// Orthogonal volumes with optional fixed-temperature exterior reservoirs; no advection.
public sealed class ThermalExchange : IDisposable
{
    public readonly struct Cell
    {
        public readonly EquipmentThermalState Owner;
        public readonly int Index;
        public readonly string Label;
        public Cell(EquipmentThermalState owner, int index, string label) { Owner = owner; Index = index; Label = label; }
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
    private readonly ThermalPairCoefficients materialPairs;
    private readonly Boundary[] boundaries;
    private readonly double exteriorFilmCoefficient;
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
    private readonly double? maximumFractionPerMinute;
    private NativeArray<int> nativeOffsets, nativeNeighbours;
    private NativeArray<double> nativeG, nativeExterior, nativeT, nativeEnergy, nativeNext;
    internal ThermalTopology.Binding TopologyBinding;
    private readonly Dictionary<EquipmentThermalState,int[]> ownerIndices = new Dictionary<EquipmentThermalState,int[]>();
    private readonly int[] rowFaces, faceSlotA, faceSlotB, boundaryOffsets, rowBoundaries;
    private readonly double[] rawFaces, rawBoundaries, rawTotals, scales;
    private readonly WorkSet changedCells, rawWork, scaleWork, effectiveWork, finalWork;
    private readonly MinimumTree rawLimits, effectiveLimits;
    public int LastChangedCells { get; private set; }
    public int LastRawFacesUpdated { get; private set; }
    public int LastEffectiveFacesUpdated { get; private set; }
    private int uploadedBuild = -1;
    private static readonly HashSet<ThermalExchange> activeBuffers = new HashSet<ThermalExchange>();
    public int BufferAllocations { get; private set; }
    public int CoefficientUploads { get; private set; }
    public bool HasBuffers => nativeOffsets.IsCreated;
    public long BufferBytes => HasBuffers ? 4L * (offsets.Length + neighbours.Length) + 8L * (coefficients.Length + 4L * cells.Length) : 0;
    // Between completed minutes nativeEnergy holds the last committed float energy.
    // Equipment reads and writes its active rows through its stable owner-local mapping.
    private bool energyBound;
    internal float ReadEnergy(int row) => (float)nativeEnergy[row];
    internal void SetEnergy(int row, float value) => nativeEnergy[row] = value;
    private void ReleaseEnergy()
    {
        if (!energyBound) return;
        for (int i = 0; i < cells.Length; i++)
            cells[i].Owner.CommitExchangeEnergy(cells[i].Index, ReadEnergy(i));
        foreach (var owner in owners) owner.BindEnergy(null, null);
        energyBound = false;
    }
    public void Dispose()
    {
        ReleaseEnergy();
        if (nativeOffsets.IsCreated) nativeOffsets.Dispose();
        if (nativeNeighbours.IsCreated) nativeNeighbours.Dispose();
        if (nativeG.IsCreated) nativeG.Dispose();
        if (nativeExterior.IsCreated) nativeExterior.Dispose();
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
    private void PrepareBuffers()
    {
        if (!HasBuffers)
        {
            try
            {
                nativeOffsets = new NativeArray<int>(offsets, Allocator.Persistent);
                nativeNeighbours = new NativeArray<int>(neighbours, Allocator.Persistent);
                nativeG = new NativeArray<double>(coefficients.Length, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
                nativeExterior = new NativeArray<double>(cells.Length, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
                nativeT = new NativeArray<double>(cells.Length, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
                nativeEnergy = new NativeArray<double>(cells.Length, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
                nativeNext = new NativeArray<double>(cells.Length, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
                activeBuffers.Add(this);
                BufferAllocations++;
            }
            catch { Dispose(); throw; }
        }
        if (uploadedBuild != CoefficientBuilds)
        {
            nativeG.CopyFrom(coefficients);
            nativeExterior.CopyFrom(exterior);
            uploadedBuild = CoefficientBuilds;
            CoefficientUploads++;
        }
    }
    public double? MaximumFractionPerMinute => maximumFractionPerMinute;
    public double RawMinimumStepSeconds { get; private set; }
    public int CellCount => cells.Length;
    public int FaceCount => faces.Length;
    public int MaximumDegree { get; private set; }
    public int CoefficientBuilds { get; private set; }
    public double MinimumStepSeconds { get; private set; }
    public int LimitingCell { get; private set; }
    public double LastJobMilliseconds { get; private set; }
    public double LastTickMilliseconds { get; private set; }
    public string Diagnostic { get; private set; }
    public Cell GetCell(int i) => cells[i];
    public Face GetFace(int i) => faces[i];
    public double SumConductance(int i) => sums[i];
    // null is retained only for physical-model diagnostics and regression comparisons.
    public ThermalExchange(Cell[] source, Face[] connections, double? maximumFractionPerMinute = 0.1, Boundary[] exteriorBoundaries = null, double exteriorFilmCoefficientWPerM2K = 5, ThermalPairCoefficients materialPairs = null)
    {
        if (maximumFractionPerMinute.HasValue && (!Finite(maximumFractionPerMinute.Value) || maximumFractionPerMinute.Value <= 0 || maximumFractionPerMinute.Value > 0.5))
            throw new ArgumentOutOfRangeException(nameof(maximumFractionPerMinute));
        this.materialPairs=materialPairs;
        this.maximumFractionPerMinute = maximumFractionPerMinute;
        cells = (Cell[])source.Clone(); faces = (Face[])connections.Clone();
        if (!Finite(exteriorFilmCoefficientWPerM2K) || exteriorFilmCoefficientWPerM2K < 0)
            throw new ArgumentOutOfRangeException(nameof(exteriorFilmCoefficientWPerM2K));
        exteriorFilmCoefficient = exteriorFilmCoefficientWPerM2K;
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
                !Positive(f.AreaM2) || !Positive(f.HalfAM) || !Positive(f.HalfBM) || !pairs.Add((Math.Min(f.A,f.B),Math.Max(f.A,f.B))))
                throw new InvalidOperationException("Invalid/duplicate thermal face");
            offsets[f.A+1]++; offsets[f.B+1]++;
        }
        for (int i = 1; i < offsets.Length; i++) offsets[i] += offsets[i-1];
        for (int i = 0; i < cells.Length; i++) MaximumDegree = Math.Max(MaximumDegree, offsets[i+1]-offsets[i]);
        neighbours = new int[2*faces.Length]; coefficients = new double[neighbours.Length]; sums = new double[cells.Length];
        rowFaces = new int[neighbours.Length]; faceSlotA = new int[faces.Length]; faceSlotB = new int[faces.Length];
        rawFaces = new double[faces.Length]; rawBoundaries = new double[boundaries.Length];
        rawTotals = new double[cells.Length]; scales = new double[cells.Length];
        changedCells = new WorkSet(cells.Length); rawWork = new WorkSet(faces.Length); scaleWork = new WorkSet(cells.Length);
        effectiveWork = new WorkSet(faces.Length); finalWork = new WorkSet(cells.Length);
        rawLimits = new MinimumTree(cells.Length); effectiveLimits = new MinimumTree(cells.Length);
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
        for(int i=0;i<cells.Length;i++)ownerIndices[cells[i].Owner][cells[i].Index]=i;
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
    private double FaceConductance(Face f,Cell a,Cell b) => materialPairs==null
        ? Conductance(f.AreaM2,f.HalfAM,a.Owner.Conductivity(a.Index,f.Axis),f.HalfBM,b.Owner.Conductivity(b.Index,f.Axis))
        : materialPairs.Conductance(a.Owner.ExchangeMaterial(a.Index),b.Owner.ExchangeMaterial(b.Index),f.AreaM2,f.HalfAM,a.Owner.Conductivity(a.Index,f.Axis),f.HalfBM,b.Owner.Conductivity(b.Index,f.Axis));
    private double BoundaryConductance(Boundary b,Cell c,double k)=>materialPairs==null
        ? (exteriorFilmCoefficient==0?0:b.AreaM2/(b.HalfM/k+1/exteriorFilmCoefficient))
        : materialPairs.Exterior(c.Owner.ExchangeMaterial(c.Index),b.AreaM2,b.HalfM,k);
    private string Detail(int i, string reason)
    {
        var c = cells[i]; double capacity = c.Owner.HeatCapacityBuffer[c.Index];
        return reason + "; " + c.Label + "; C=" + capacity.ToString("R") + " J/K; sumG=" + sums[i].ToString("R") +
            " W/K; dtMax=" + (sums[i] > 0 ? capacity/sums[i] : double.PositiveInfinity).ToString("R") + " s";
    }
    private sealed class WorkSet
    {
        private readonly bool[] marked;
        public readonly List<int> Values = new List<int>();
        public WorkSet(int count) { marked=new bool[count]; }
        public void Add(int i) {if(!marked[i]){marked[i]=true;Values.Add(i);}}
        public void Clear() {foreach(int i in Values)marked[i]=false;Values.Clear();}
    }
    // Preserve exact minimum/tie behaviour without a whole-world scan after each local edit.
    private sealed class MinimumTree
    {
        private readonly double[] values;
        private readonly int start;
        public MinimumTree(int count) {start=1;while(start<count)start*=2;values=new double[start*2];for(int i=0;i<values.Length;i++)values[i]=double.PositiveInfinity;}
        public void Set(int index,double value) {int p=start+index;values[p]=value;while((p/=2)>0)values[p]=Math.Min(values[p*2],values[p*2+1]);}
        public void SetInitial(int index,double value) => values[start+index]=value;
        public void Build() {for(int p=start-1;p>0;p--)values[p]=Math.Min(values[p*2],values[p*2+1]);}
        public double Value => values[1];
        public int Index { get {if(double.IsPositiveInfinity(Value))return -1;int p=1;while(p<start)p=values[p*2]<=values[p*2+1]?p*2:p*2+1;return p-start;} }
    }
    internal bool CanRebind(EquipmentThermalState owner) => ownerIndices.ContainsKey(owner);
    internal void RebindCells(List<KeyValuePair<int,Cell>> replacements)
    {
        if (replacements.Count > 0) ReleaseEnergy();
        // Clear all old mappings before assigning: a move may exchange two owners.
        foreach(var pair in replacements){var old=cells[pair.Key];ownerIndices[old.Owner][old.Index]=-1;}
        foreach(var pair in replacements){cells[pair.Key]=pair.Value;ownerIndices[pair.Value.Owner][pair.Value.Index]=pair.Key;changedCells.Add(pair.Key);}
    }
    private void InitializeCoefficients()
    {
        Array.Clear(sums,0,sums.Length);Array.Clear(rawExterior,0,rawExterior.Length);
        // Initial construction is a linear full pass, not an all-cell dirty queue.
        // This avoids retaining world-sized work lists just for later small edits.
        for(int id=0;id<faces.Length;id++) {
            var f=faces[id];var a=cells[f.A];var b=cells[f.B];
            double g=FaceConductance(f,a,b);
            rawFaces[id]=g;sums[f.A]+=g;sums[f.B]+=g;
        }
        for(int id=0;id<boundaries.Length;id++) {
            var b=boundaries[id];var c=cells[b.Cell];double k=c.Owner.Conductivity(c.Index,b.Direction/2);
            double g=BoundaryConductance(b,c,k);
            if(!Finite(g)||g<0)throw new InvalidOperationException("Invalid exterior conductance");
            rawBoundaries[id]=g;rawExterior[b.Cell]+=g;sums[b.Cell]+=g;
        }
        for(int i=0;i<cells.Length;i++) {
            var c=cells[i];double capacity=c.Owner.HeatCapacityBuffer[c.Index];cachedCapacities[i]=c.Owner.HeatCapacityBuffer[c.Index];rawTotals[i]=sums[i];
            if(!Positive(capacity)||!Finite(sums[i]))throw new InvalidOperationException(Detail(i,"Invalid C or sumG"));
            double limit=sums[i]>0?capacity/sums[i]:double.PositiveInfinity;rawLimits.SetInitial(i,limit);
            scales[i]=maximumFractionPerMinute.HasValue?Math.Min(1,maximumFractionPerMinute.Value*(limit/60d)):1;
        }
        if(maximumFractionPerMinute.HasValue)Array.Clear(sums,0,sums.Length);
        for(int id=0;id<faces.Length;id++) {
            var f=faces[id];double g=rawFaces[id];if(maximumFractionPerMinute.HasValue){g*=Math.Min(scales[f.A],scales[f.B]);sums[f.A]+=g;sums[f.B]+=g;}
            coefficients[faceSlotA[id]]=coefficients[faceSlotB[id]]=g;
        }
        for(int i=0;i<cells.Length;i++) {
            exterior[i]=rawExterior[i]*scales[i];if(maximumFractionPerMinute.HasValue)sums[i]+=exterior[i];
            double capacity=cachedCapacities[i];if(!Positive(capacity)||!Finite(sums[i]))throw new InvalidOperationException(Detail(i,"Invalid C or sumG"));
            effectiveLimits.SetInitial(i,sums[i]>0?capacity/sums[i]:double.PositiveInfinity);
        }
        rawLimits.Build();effectiveLimits.Build();RawMinimumStepSeconds=rawLimits.Value;MinimumStepSeconds=effectiveLimits.Value;LimitingCell=effectiveLimits.Index;
        for(int k=0;k<owners.Length;k++)revisions[k]=owners[k].CacheRevision;
        LastChangedCells=cells.Length;LastRawFacesUpdated=LastEffectiveFacesUpdated=faces.Length;CoefficientBuilds++;
    }
    public void RefreshCoefficients()
    {
        if(CoefficientBuilds==0) {
            if(DiagnosticsEnabled)for(int i=0;i<cells.Length;i++){var c=cells[i];if(c.Owner.Diagnostic!=null||c.Owner.IsDirty(c.Index)||!c.Owner.IsInitialized(c.Index))throw new InvalidOperationException(Detail(i,"Cache is not ready"));}
            InitializeCoefficients(); return;
        }
        LastChangedCells=LastRawFacesUpdated=LastEffectiveFacesUpdated=0;
        for(int k=0;k<owners.Length;k++) {
            var owner=owners[k];
            if(revisions[k]==owner.CacheRevision)continue;
            var indices=ownerIndices[owner];
            for(int j=0;j<indices.Length;j++)if(indices[j]>=0 && owner.CellCacheRevision(j)>revisions[k])changedCells.Add(indices[j]);
        }
        if(DiagnosticsEnabled)for(int i=0;i<cells.Length;i++) {
            var c=cells[i];if(c.Owner.Diagnostic!=null || c.Owner.IsDirty(c.Index) || !c.Owner.IsInitialized(c.Index))throw new InvalidOperationException(Detail(i,"Cache is not ready"));
        }
        for(int k=0;k<owners.Length;k++)revisions[k]=owners[k].CacheRevision;
        if(changedCells.Values.Count==0)return;
        LastChangedCells=changedCells.Values.Count;
        bool bulk=LastChangedCells>cells.Length/3;
        foreach(int i in changedCells.Values) {
            scaleWork.Add(i);
            for(int p=offsets[i];p<offsets[i+1];p++)rawWork.Add(rowFaces[p]);
            for(int p=boundaryOffsets[i];p<boundaryOffsets[i+1];p++) {
                int id=rowBoundaries[p];var boundary=boundaries[id];var cell=cells[i];
                double k=cell.Owner.Conductivity(cell.Index,boundary.Direction/2);
                double g=BoundaryConductance(boundary,cell,k);
                if(!Finite(g)||g<0)throw new InvalidOperationException("Invalid exterior conductance");rawBoundaries[id]=g;
            }
        }
        foreach(int id in rawWork.Values) {
            var f=faces[id];var a=cells[f.A];var b=cells[f.B];
            rawFaces[id]=FaceConductance(f,a,b);
            scaleWork.Add(f.A);scaleWork.Add(f.B);effectiveWork.Add(id);
        }
        LastRawFacesUpdated=rawWork.Values.Count;
        // Re-sum, never subtract deltas: same face order and rounding as the full rebuild.
        foreach(int i in scaleWork.Values) {
            double sum=0,ext=0;
            for(int p=offsets[i];p<offsets[i+1];p++)sum+=rawFaces[rowFaces[p]];
            for(int p=boundaryOffsets[i];p<boundaryOffsets[i+1];p++){double g=rawBoundaries[rowBoundaries[p]];ext+=g;sum+=g;}
            rawExterior[i]=ext;rawTotals[i]=sum;var c=cells[i];double capacity=c.Owner.HeatCapacityBuffer[c.Index];
            cachedCapacities[i]=c.Owner.HeatCapacityBuffer[c.Index];
            if(!Positive(capacity)||!Finite(sum))throw new InvalidOperationException(Detail(i,"Invalid C or sumG"));
            double limit=sum>0?capacity/sum:double.PositiveInfinity;if(bulk)rawLimits.SetInitial(i,limit);else rawLimits.Set(i,limit);
            double scale=maximumFractionPerMinute.HasValue?Math.Min(1,maximumFractionPerMinute.Value*(limit/60d)):1;
            if(scale!=scales[i])for(int p=offsets[i];p<offsets[i+1];p++)effectiveWork.Add(rowFaces[p]);
            scales[i]=scale;exterior[i]=ext*scale;finalWork.Add(i);
        }
        foreach(int id in effectiveWork.Values) {
            var f=faces[id];double g=rawFaces[id];if(maximumFractionPerMinute.HasValue)g*=Math.Min(scales[f.A],scales[f.B]);
            coefficients[faceSlotA[id]]=coefficients[faceSlotB[id]]=g;finalWork.Add(f.A);finalWork.Add(f.B);
        }
        LastEffectiveFacesUpdated=effectiveWork.Values.Count;
        foreach(int i in finalWork.Values) {
            double sum=0;for(int p=offsets[i];p<offsets[i+1];p++)sum+=coefficients[p];
            sum=maximumFractionPerMinute.HasValue?sum+exterior[i]:rawTotals[i];sums[i]=sum;
            double capacity=cachedCapacities[i];if(!Positive(capacity)||!Finite(sum))throw new InvalidOperationException(Detail(i,"Invalid C or sumG"));
            double limit=sum>0?capacity/sum:double.PositiveInfinity;
            if(bulk)effectiveLimits.SetInitial(i,limit);else effectiveLimits.Set(i,limit);
        }
        if(bulk){rawLimits.Build();effectiveLimits.Build();}
        RawMinimumStepSeconds=rawLimits.Value;MinimumStepSeconds=effectiveLimits.Value;LimitingCell=effectiveLimits.Index;
        // Keep existing Native storage; only touched connections and exterior rows are uploaded.
        bool canPatch=HasBuffers && uploadedBuild==CoefficientBuilds;
        CoefficientBuilds++;
        if(canPatch) {
            foreach(int id in effectiveWork.Values){nativeG[faceSlotA[id]]=coefficients[faceSlotA[id]];nativeG[faceSlotB[id]]=coefficients[faceSlotB[id]];}
            foreach(int i in scaleWork.Values)nativeExterior[i]=exterior[i];
            uploadedBuild=CoefficientBuilds;CoefficientUploads++;
        }
        changedCells.Clear();rawWork.Clear();scaleWork.Clear();effectiveWork.Clear();finalWork.Clear();
    }
    public void ValidateStep(double seconds)
    {
        RefreshCoefficients();
        if (!Positive(seconds)) throw new ArgumentOutOfRangeException(nameof(seconds));
        if (seconds > MinimumStepSeconds) throw new InvalidOperationException(Detail(LimitingCell,"Explicit step rejected: dt="+seconds+" s"));
    }
    [BurstCompile(FloatMode = FloatMode.Strict, FloatPrecision = FloatPrecision.Standard, CompileSynchronously = true)]
    private struct Gather : IJobParallelFor
    {
        [ReadOnly] public NativeArray<int> Offsets, Neighbours;
        [ReadOnly] public NativeArray<double> G, T, Energy, Exterior;
        [WriteOnly] public NativeArray<double> Next;
        public double Seconds, OutsideC;
        public void Execute(int i)
        {
            double watts = Exterior[i] * (OutsideC - T[i]);
            for (int p = Offsets[i]; p < Offsets[i+1]; p++) watts += G[p] * (T[Neighbours[p]] - T[i]);
            Next[i] = (float)(Energy[i] + Seconds * watts);
        }
    }
    // Results are committed directly after the Job; result-range and energy-balance audits live in editor tests.
    public void Step(double seconds = 60, double? outsideC = null)
    {
        RefreshCoefficients();
        StepPrepared(seconds,outsideC);
    }
    // ThermalWorld has already refreshed state, topology and coefficients.
    internal void StepPrepared(double seconds, double? outsideC)
    {
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
            PrepareBuffers();
            var o=nativeOffsets; var n=nativeNeighbours; var g=nativeG; var ext=nativeExterior;
            var t=nativeT; var e=nativeEnergy; var next=nativeNext;
            {
                if (!energyBound)
                {
                    // Another solver (for example a diagnostic one) can share these owners.
                    foreach (var owner in owners) owner.ActiveExchange?.ReleaseEnergy();
                    for (int i = 0; i < cells.Length; i++)
                        e[i] = cells[i].Owner.ReadEnergy(cells[i].Index);
                }
                for (int i = 0; i < cells.Length; i++)
                    t[i] = e[i] / cachedCapacities[i];
                if(DiagnosticsEnabled)
                {
                    double externalEnergy=0,externalCorrection=0;
                    for(int i=0;i<cells.Length;i++)
                    {
                        if(!Finite(t[i]) || t[i]<-273.15) throw new InvalidOperationException(Detail(i,"Invalid old temperature"));
                        externalEnergy=Add(externalEnergy,seconds*exterior[i]*(ambient-t[i]),ref externalCorrection);
                    }
                    ExternalEnergyJ=externalEnergy;
                }
                var jobTime = measure ? Stopwatch.StartNew() : null;
                new Gather {Offsets=o,Neighbours=n,G=g,T=t,Energy=e,Next=next,Seconds=seconds,Exterior=ext,OutsideC=ambient}.Schedule(cells.Length,64).Complete();
                LastJobMilliseconds = jobTime?.Elapsed.TotalMilliseconds ?? 0;
                // Publish only after the complete job succeeds.
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
