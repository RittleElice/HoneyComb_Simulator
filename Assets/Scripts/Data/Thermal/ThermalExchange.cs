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
        public readonly bool IsSlotGroup;
        public bool IsSlotAir => IsSlotGroup && Owner.ExchangeMaterial(Index) == HiveMaterial.Air;
        internal sealed class StackMembers
        {
            internal readonly (EquipmentThermalState owner,int index,double volume)[] Parts;
            internal readonly double Volume;
            internal readonly bool CrossesStack;
            internal readonly double[] FaceAreas = new double[3];
            internal StackMembers(List<(EquipmentThermalState owner,int index,double volume)> parts)
            {
                Parts = parts.ToArray();
                foreach(var part in Parts)
                {
                    Volume += part.volume;
                    if(!ReferenceEquals(part.owner,Parts[0].owner))CrossesStack=true;
                }
            }
        }
        internal readonly StackMembers Stack;
        public bool IsMergedAir => Stack != null;
        public bool IsStackAir => Stack != null && Stack.CrossesStack;
        public Cell(EquipmentThermalState owner, int index, string label, bool isSlotAir = false)
        { Owner = owner; Index = index; Label = label; IsSlotGroup = isSlotAir; Stack=null; }
        internal Cell(StackMembers stack, string label) : this(stack.Parts[0].owner, stack.Parts[0].index, label)
        { Stack=stack; }
        public int MemberCount => Stack?.Parts.Length ?? (IsSlotGroup ? HiveGridLayout.SlotDepthCells : 1);
        public EquipmentThermalState MemberOwner(int layer) => Stack == null ? Owner : Stack.Parts[layer].owner;
        public int MemberIndex(int layer) => Stack == null ? Index + layer * HiveGridLayout.SizeX : Stack.Parts[layer].index;
        public float HeatCapacityJPerK => Sum(false);
        public float EnergyJ => Sum(true);
        private float Sum(bool energy)
        {
            if(!IsSlotGroup && Stack==null)return energy?Owner.EnergyJ[Index]:Owner.HeatCapacityJPerK[Index];
            float total=0;
            for(int k=0;k<MemberCount;k++){
                var owner=MemberOwner(k);int index=MemberIndex(k);
                total+=energy?owner.EnergyJ[index]:owner.HeatCapacityJPerK[index];
            }
            return total;
        }
        internal float MemberEnergy(float total, EquipmentThermalState owner,int member)
        {
            if(Stack!=null){
                foreach(var part in Stack.Parts)if(ReferenceEquals(part.owner,owner) && part.index==member)
                    return (float)(total*part.volume/Stack.Volume);
                throw new InvalidOperationException("Unknown stack member");
            }
            if(!IsSlotGroup)return total;
            int layer=(member-Index)/HiveGridLayout.SizeX;
            return (float)(total*HiveGridLayout.SlotLayerEnergyShare(layer));
        }
    }
    public readonly struct Face
    {
        public readonly int A, B, Axis;
        public readonly float AreaM2, HalfAM, HalfBM;
        public Face(int a, int b, double area, double halfA, double halfB, int axis = 0)
        { A = a; B = b; AreaM2 = (float)area; HalfAM = (float)halfA; HalfBM = (float)halfB; Axis = axis; }
    }
    public readonly struct Boundary
    {
        public readonly int Cell, Direction; // -/+ X, -/+ Y, -/+ Z = 0..5
        public readonly float AreaM2, HalfM;
        public Boundary(int cell, int direction, double area, double half)
        { Cell=cell; Direction=direction; AreaM2=(float)area; HalfM=(float)half; }
    }
    private readonly Boundary[] boundaries;
    private readonly ThermalTransferCoefficients transfers;
    private readonly float[] exterior, rawExterior;
    public int BoundaryCount => boundaries.Length;
    public Boundary GetBoundary(int i) => boundaries[i];
    public double ExteriorConductance(int i) => exterior[i];
    public double RawExteriorConductance(int i) => rawExterior[i];
    public double ExternalEnergyJ { get; private set; } // Diagnostic mode only; positive means energy entering the model. Zero when disabled.
    private readonly Cell[] cells;
    private readonly Face[] faces;
    private readonly int[] offsets, neighbours;
    private readonly float[] coefficients, sums;
    // Exact float capacities in solver-cell order; refreshed with the coefficient revision.
    private readonly float[] cachedCapacities;
    public long CapacityCacheBytes => 4L * cachedCapacities.Length;
    private readonly int[] revisions;
    private readonly Dictionary<EquipmentThermalState, int> ownerOrdinals = new Dictionary<EquipmentThermalState, int>();
    private readonly SortedSet<int> pendingOwners = new SortedSet<int>();
    private bool watchingOwners;
    private void OwnerCacheChanged(EquipmentThermalState owner) => pendingOwners.Add(ownerOrdinals[owner]);
    private void WatchOwners()
    {
        if (watchingOwners) return;
        for (int k = 0; k < owners.Length; k++)
        {
            owners[k].CacheChanged += OwnerCacheChanged;
            pendingOwners.Add(k); // Also catches edits while buffers were released.
        }
        watchingOwners = true;
    }
    private readonly EquipmentThermalState[] owners;
    // Explicit tooling opt-ins. Normal minute updates perform neither audits nor timing.
    public bool DiagnosticsEnabled { get; set; }
    public bool MeasurePerformance { get; set; }
    private const int RowStride = HiveGridLayout.SizeX;
    private const int PlaneStride = RowStride * HiveGridLayout.SizeY;
    private NativeArray<int> nativeOffsets, nativeNeighbours;
    private NativeArray<byte> nativeFixedRows;
    private NativeArray<float> nativeG, nativeExterior, nativeT, nativeEnergy, nativeNext, nativeNextT;
    private NativeArray<float> nativeCapacity;
    internal ThermalTopology.Binding TopologyBinding;
    private readonly Dictionary<EquipmentThermalState,int[]> ownerIndices = new Dictionary<EquipmentThermalState,int[]>();
    private readonly int[] rowFaces, faceSlotA, faceSlotB, boundaryOffsets, rowBoundaries;
    private readonly float[] rawFaces, rawBoundaries;
    private float coefficientSeconds=60;
    private readonly WorkSet changedCells, rawWork, finalWork;
    public int LastChangedCells { get; private set; }
    public int LastRawFacesUpdated { get; private set; }
    public int LastEffectiveFacesUpdated { get; private set; }
    private int uploadedBuild = -1;
    private static readonly HashSet<ThermalExchange> activeBuffers = new HashSet<ThermalExchange>();
    public int BufferAllocations { get; private set; }
    public int CoefficientUploads { get; private set; }
    public bool HasBuffers => nativeOffsets.IsCreated;
    public long BufferBytes => HasBuffers ? 4L * (offsets.Length + neighbours.Length) + 4L * (coefficients.Length + 5L * cells.Length) + 5L * cells.Length : 0;
    // Between completed minutes nativeEnergy holds the last committed float energy.
    // Equipment reads and writes its active rows through its stable owner-local mapping.
    private bool energyBound;
    internal float ReadEnergy(int row, EquipmentThermalState owner,int member) => cells[row].MemberEnergy(nativeEnergy[row], owner,member);
    internal void SetEnergy(int row, EquipmentThermalState owner,int member, float value)
    {
        if (cells[row].IsSlotGroup || cells[row].IsMergedAir)
        {
            // A per-cell external write is applied to its physical owner before re-mixing.
            ReleaseEnergy();
            owner.CommitExchangeEnergy(member, value);
            return;
        }
        nativeEnergy[row] = value;
        nativeT[row] = value / nativeCapacity[row];
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
                var owner=cell.MemberOwner(k);
                owner.CommitExchangeEnergy(member, ReadEnergy(i,owner,member));
            }
        }
        foreach (var owner in owners) owner.BindEnergy(null, null);
        energyBound = false;
    }
    public void Dispose()
    {
        ReleaseEnergy();
        if (watchingOwners)
            foreach (var owner in owners) owner.CacheChanged -= OwnerCacheChanged;
        watchingOwners = false;
        pendingOwners.Clear();
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
                nativeG = new NativeArray<float>(coefficients.Length, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
                nativeExterior = new NativeArray<float>(cells.Length, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
                nativeT = new NativeArray<float>(cells.Length, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
                nativeNextT = new NativeArray<float>(cells.Length, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
                nativeCapacity = new NativeArray<float>(cells.Length, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
                nativeEnergy = new NativeArray<float>(cells.Length, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
                nativeNext = new NativeArray<float>(cells.Length, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
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
        exterior = new float[cells.Length]; rawExterior = new float[cells.Length];
        cachedCapacities = new float[cells.Length];
        offsets = new int[cells.Length + 1];
        var ownerSet = new HashSet<EquipmentThermalState>();
        var ownerList = new List<EquipmentThermalState>();
        foreach(var cell in cells) for(int k=0;k<cell.MemberCount;k++){var owner=cell.MemberOwner(k);if(ownerSet.Add(owner))ownerList.Add(owner);}
        owners = ownerList.ToArray(); revisions = new int[owners.Length];
        for(int i=0;i<revisions.Length;i++) { revisions[i]=-1; ownerOrdinals.Add(owners[i], i); }
        var unique = new HashSet<(EquipmentThermalState, int)>();
        for (int i = 0; i < cells.Length; i++)
        {
            if (cells[i].Owner == null || cells[i].Index < 0 || cells[i].Index >= cells[i].Owner.Count || !unique.Add((cells[i].Owner, cells[i].Index)))
                throw new InvalidOperationException("Duplicate or invalid thermal owner: " + cells[i].Label);
        }
        var pairs = new HashSet<(int, int)>();
        var mergedFaces = new HashSet<(int, int, int)>();
        foreach (var f in faces)
        {
            if (f.A < 0 || f.B < 0 || f.A >= cells.Length || f.B >= cells.Length || f.A == f.B ||
                f.Axis<0 || f.Axis>2 || !Positive(f.AreaM2) || !Positive(f.HalfAM) || !Positive(f.HalfBM))
                throw new InvalidOperationException("Invalid thermal face");
            // A shared volume can meet one neighbour on multiple sides. MergeAir has
            // already summed patches with the same endpoints and positive direction.
            bool distinct = cells[f.A].IsMergedAir || cells[f.B].IsMergedAir
                ? mergedFaces.Add((f.A,f.B,f.Axis))
                : pairs.Add((Math.Min(f.A,f.B),Math.Max(f.A,f.B)));
            if (!distinct)
                throw new InvalidOperationException("Invalid/duplicate thermal face");
            offsets[f.A+1]++; offsets[f.B+1]++;
        }
        for (int i = 1; i < offsets.Length; i++) offsets[i] += offsets[i-1];
        for (int i = 0; i < cells.Length; i++) MaximumDegree = Math.Max(MaximumDegree, offsets[i+1]-offsets[i]);
        neighbours = new int[2*faces.Length]; coefficients = new float[neighbours.Length]; sums = new float[cells.Length];
        rowFaces = new int[neighbours.Length]; faceSlotA = new int[faces.Length]; faceSlotB = new int[faces.Length];
        rawFaces = new float[faces.Length]; rawBoundaries = new float[boundaries.Length];
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
                ownerIndices[cells[i].MemberOwner(k)][cells[i].MemberIndex(k)] = i;
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
    public float FacePyramidCapacity(int row,int axis,float contactArea)
    {
        var c=cells[row];float fraction=contactArea/(float)(c.Stack!=null ? c.Stack.FaceAreas[axis] : c.Owner.FaceAreaM2(c.Index,axis,c.IsSlotGroup));
        if(!Positive(fraction)||fraction>1+1e-6)throw new InvalidOperationException("Contact exceeds pyramid face: "+c.Label);
        return c.HeatCapacityJPerK*fraction/6;
    }
    // Stable evaluation of 1-exp(-x), including very weak connections.
    private static float Relaxation(float x) => x < 0.01f ? x * (1f - x * (0.5f - x * (1f / 6f - x / 24f))) : 1f - MathF.Exp(-x);
    private float FaceConductance(Face f)
    {
        var a=cells[f.A];var b=cells[f.B];
        float g=f.AreaM2*(float)transfers.Between(a.Owner.ExchangeMaterial(a.Index),b.Owner.ExchangeMaterial(b.Index));
        float ca=FacePyramidCapacity(f.A,f.Axis,f.AreaM2),cb=FacePyramidCapacity(f.B,f.Axis,f.AreaM2),reduced=ca*cb/(ca+cb);
        return reduced*Relaxation(coefficientSeconds*g/reduced)/coefficientSeconds;
    }
    private float BoundaryConductance(Boundary b,out float raw)
    {
        var c=cells[b.Cell];float capacity=FacePyramidCapacity(b.Cell,b.Direction/2,b.AreaM2);
        // Fixed-temperature weather reservoir; independently tuned material-to-exterior coefficients.
        raw=b.AreaM2*(float)transfers.Outside(c.Owner.ExchangeMaterial(c.Index));
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
        float sum=0,raw=0;
        for(int p=boundaryOffsets[i];p<boundaryOffsets[i+1];p++){int id=rowBoundaries[p];sum+=BoundaryConductance(boundaries[id],out float g);rawBoundaries[id]=g;raw+=g;}
        exterior[i]=sum;rawExterior[i]=raw;
    }
    private void UpdateFace(int id)
    {
        float g=FaceConductance(faces[id]);rawFaces[id]=g;
        coefficients[faceSlotA[id]]=coefficients[faceSlotB[id]]=g;
    }
    private void SumCell(int i)
    {
        float sum=exterior[i];for(int p=offsets[i];p<offsets[i+1];p++)sum+=coefficients[p];sums[i]=sum;
        if(!Finite(sum)||coefficientSeconds*sum>cachedCapacities[i]*(1+1e-6))throw new InvalidOperationException(Detail(i,"Overlapping pyramid capacity"));
    }
    private void InitializeCoefficients()
    {
        for(int i=0;i<cells.Length;i++)UpdateCell(i);
        for(int f=0;f<faces.Length;f++)UpdateFace(f);
        for(int i=0;i<cells.Length;i++)SumCell(i);
        for(int k=0;k<owners.Length;k++)revisions[k]=owners[k].CacheRevision;
        pendingOwners.Clear();
        LastChangedCells=cells.Length;LastRawFacesUpdated=LastEffectiveFacesUpdated=faces.Length;CoefficientBuilds++;
        changedCells.Clear();rawWork.Clear();finalWork.Clear();
    }
    public void RefreshCoefficients()=>RefreshCoefficients(coefficientSeconds);
    private void RefreshCoefficients(double seconds)
    {
        if(!Positive((float)seconds))throw new ArgumentOutOfRangeException(nameof(seconds));
        WatchOwners();
        if(CoefficientBuilds==0||(float)seconds!=coefficientSeconds){coefficientSeconds=(float)seconds;InitializeCoefficients();return;}
        foreach(int k in pendingOwners){
            var owner=owners[k];if(revisions[k]==owner.CacheRevision)continue;
            var indices=ownerIndices[owner];
            for(int j=0;j<indices.Length;j++)if(indices[j]>=0&&owner.CellCacheRevision(j)>revisions[k])changedCells.Add(indices[j]);
        }
        foreach(int k in pendingOwners)revisions[k]=owners[k].CacheRevision;
        pendingOwners.Clear();
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
        [ReadOnly] public NativeArray<float> Energy;
        [ReadOnly] public NativeArray<float> Capacity;
        [WriteOnly] public NativeArray<float> Temperature;
        public void Execute(int i) => Temperature[i] = Energy[i] / Capacity[i];
    }
    [BurstCompile(FloatMode = FloatMode.Strict, FloatPrecision = FloatPrecision.Standard, CompileSynchronously = true)]
    private struct Gather : IJobParallelFor
    {
        [ReadOnly] public NativeArray<int> Offsets, Neighbours;
        [ReadOnly] public NativeArray<byte> FixedRows;
        [ReadOnly] public NativeArray<float> G, T, Energy, Exterior;
        [ReadOnly] public NativeArray<float> Capacity;
        [WriteOnly] public NativeArray<float> Next, NextT;
        public float Seconds, OutsideC;
        public void Execute(int i)
        {
            float watts = Exterior[i] * (OutsideC - T[i]);
            int start = Offsets[i];
            float temperature = T[i];
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
            float committedEnergy = Energy[i] + Seconds * watts;
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
        if((float)seconds!=coefficientSeconds)RefreshCoefficients(seconds);
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
                        externalEnergy=Add(externalEnergy,seconds*nativeExterior[i]*(ambient-t[i]),ref externalCorrection);
                    }
                    ExternalEnergyJ=externalEnergy;
                }
                var jobTime = measure ? Stopwatch.StartNew() : null;
                new Gather {Offsets=o,Neighbours=n,FixedRows=nativeFixedRows,G=g,T=t,Energy=e,Next=next,NextT=nativeNextT,Capacity=nativeCapacity,Seconds=(float)seconds,Exterior=ext,OutsideC=(float)ambient}.Schedule(cells.Length,64,temperaturesReady).Complete();
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
