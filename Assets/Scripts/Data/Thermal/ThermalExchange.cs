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
        public readonly int A, B;
        public readonly double AreaM2, HalfAM, HalfBM;
        public Face(int a, int b, double area, double halfA, double halfB)
        { A = a; B = b; AreaM2 = area; HalfAM = halfA; HalfBM = halfB; }
    }
    public readonly struct Boundary
    {
        public readonly int Cell, Direction; // -/+ X, -/+ Y, -/+ Z = 0..5
        public readonly double AreaM2, HalfM;
        public Boundary(int cell, int direction, double area, double half)
        { Cell=cell; Direction=direction; AreaM2=area; HalfM=half; }
    }
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
    private int uploadedBuild = -1;
    private static readonly HashSet<ThermalExchange> activeBuffers = new HashSet<ThermalExchange>();
    public int BufferAllocations { get; private set; }
    public int CoefficientUploads { get; private set; }
    public bool HasBuffers => nativeOffsets.IsCreated;
    public long BufferBytes => HasBuffers ? 4L * (offsets.Length + neighbours.Length) + 8L * (coefficients.Length + 4L * cells.Length) : 0;
    // Releases only derived working storage. Authoritative equipment energy is untouched.
    // A subsequent Step may lazily recreate the buffers (e.g. returning to a loaded world).
    public void Dispose()
    {
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
    public ThermalExchange(Cell[] source, Face[] connections, double? maximumFractionPerMinute = 0.1, Boundary[] exteriorBoundaries = null, double exteriorFilmCoefficientWPerM2K = 5)
    {
        if (maximumFractionPerMinute.HasValue && (!Finite(maximumFractionPerMinute.Value) || maximumFractionPerMinute.Value <= 0 || maximumFractionPerMinute.Value > 0.5))
            throw new ArgumentOutOfRangeException(nameof(maximumFractionPerMinute));
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
    private string Detail(int i, string reason)
    {
        var c = cells[i]; double capacity = c.Owner.HeatCapacityBuffer[c.Index];
        return reason + "; " + c.Label + "; C=" + capacity.ToString("R") + " J/K; sumG=" + sums[i].ToString("R") +
            " W/K; dtMax=" + (sums[i] > 0 ? capacity/sums[i] : double.PositiveInfinity).ToString("R") + " s";
    }
    public void RefreshCoefficients()
    {
        bool changed = CoefficientBuilds == 0;
        for(int i=0;i<owners.Length;i++) changed |= revisions[i] != owners[i].CacheRevision;
        if(DiagnosticsEnabled)
            for(int i=0;i<cells.Length;i++)
            {
                var c=cells[i];
                if(c.Owner.Diagnostic!=null || c.Owner.IsDirty(c.Index) || !c.Owner.IsInitialized(c.Index))
                    throw new InvalidOperationException(Detail(i,"Cache not ready"));
            }
        if (!changed) return;
        Array.Clear(sums,0,sums.Length); var cursor = (int[])offsets.Clone();
        foreach (var f in faces)
        {
            var a = cells[f.A]; var b = cells[f.B];
            double g = Conductance(f.AreaM2,f.HalfAM,a.Owner.EffectiveConductivityWPerMK[a.Index],f.HalfBM,b.Owner.EffectiveConductivityWPerMK[b.Index]);
            int ia = cursor[f.A]++, ib = cursor[f.B]++;
            neighbours[ia] = f.B; neighbours[ib] = f.A;
            coefficients[ia] = coefficients[ib] = g; // One calculation, identical bits in both directions.
            sums[f.A] += g; sums[f.B] += g;
        }
        Array.Clear(rawExterior,0,rawExterior.Length);
        foreach (var b in boundaries)
        {
            var c=cells[b.Cell]; double k=c.Owner.EffectiveConductivityWPerMK[c.Index];
            double g=exteriorFilmCoefficient == 0 ? 0 : b.AreaM2/(b.HalfM/k+1/exteriorFilmCoefficient);
            if (!Finite(g) || g < 0) throw new InvalidOperationException("Invalid exterior conductance");
            rawExterior[b.Cell]+=g; sums[b.Cell]+=g;
        }
        // Budget internal AND external exchange together for a stable minute update.
        // Budget every cell over all its neighbours, not independently per face.
        // A face uses the smaller endpoint scale in BOTH directions, conserving energy.
        RawMinimumStepSeconds = double.PositiveInfinity;
        var scales = new double[cells.Length];
        for (int i = 0; i < cells.Length; i++)
        {
            var c = cells[i]; double capacity = c.Owner.HeatCapacityBuffer[c.Index];
            if (!Positive(capacity) || !Finite(sums[i])) throw new InvalidOperationException(Detail(i,"Invalid C or sumG"));
            double limit = sums[i] > 0 ? capacity / sums[i] : double.PositiveInfinity;
            RawMinimumStepSeconds = Math.Min(RawMinimumStepSeconds, limit);
            scales[i] = maximumFractionPerMinute.HasValue ? Math.Min(1, maximumFractionPerMinute.Value * (limit / 60d)) : 1;
        }
        if (maximumFractionPerMinute.HasValue)
        {
            Array.Clear(sums, 0, sums.Length); cursor = (int[])offsets.Clone();
            foreach (var f in faces)
            {
                int ia = cursor[f.A]++, ib = cursor[f.B]++;
                double g = coefficients[ia] * Math.Min(scales[f.A], scales[f.B]);
                coefficients[ia] = coefficients[ib] = g;
                sums[f.A] += g; sums[f.B] += g;
            }
        }
        for (int i=0;i<cells.Length;i++)
        {
            exterior[i]=rawExterior[i]*scales[i];
            if (maximumFractionPerMinute.HasValue) sums[i]+=exterior[i];
        }
        MinimumStepSeconds = double.PositiveInfinity; LimitingCell = -1;
        for (int i = 0; i < cells.Length; i++)
        {
            var c = cells[i]; double capacity = c.Owner.HeatCapacityBuffer[c.Index];
            cachedCapacities[i] = c.Owner.HeatCapacityBuffer[c.Index];
            if (!Positive(capacity) || !Finite(sums[i])) throw new InvalidOperationException(Detail(i,"Invalid C or sumG"));
            double limit = sums[i] > 0 ? capacity/sums[i] : double.PositiveInfinity;
            if (limit < MinimumStepSeconds) { MinimumStepSeconds = limit; LimitingCell = i; }
        }
        for(int i=0;i<owners.Length;i++) revisions[i]=owners[i].CacheRevision;
        CoefficientBuilds++;
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
            Next[i] = Energy[i] + Seconds * watts;
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
                var energyBuffer = e; var temperatureBuffer = t;
                for (int i = 0; i < cells.Length; i++)
                {
                    var c = cells[i];
                    double energy = c.Owner.EnergyBuffer[c.Index];
                    energyBuffer[i] = energy;
                    temperatureBuffer[i] = energy / cachedCapacities[i];
                }
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
                for (int i = 0; i < cells.Length; i++)
                    cells[i].Owner.CommitExchangeEnergy(cells[i].Index,(float)next[i]);
            }
        }
        catch (Exception ex) { Diagnostic = ex.Message; throw; }
        finally { LastTickMilliseconds = total?.Elapsed.TotalMilliseconds ?? 0; }
    }
    private static double Add(double sum, double value, ref double correction)
    { double y=value-correction, result=sum+y; correction=(result-sum)-y; return result; }
}
