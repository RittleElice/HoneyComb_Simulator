using System;
using System.Collections.Generic;
using System.Diagnostics;
using Unity.Collections;
using Unity.Jobs;

// Closed, orthogonal finite volumes. No exterior flux, advection, or invented contact resistance.
public sealed class ThermalExchange
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
    private readonly Cell[] cells;
    private readonly Face[] faces;
    private readonly int[] offsets, neighbours;
    private readonly double[] coefficients, sums;
    private readonly int[] revisions;
    public int CellCount => cells.Length;
    public int FaceCount => faces.Length;
    public int CoefficientBuilds { get; private set; }
    public double MinimumStepSeconds { get; private set; }
    public int LimitingCell { get; private set; }
    public double DoubleResidualJ { get; private set; }
    public double FloatResidualJ { get; private set; }
    public double RoundingAbsoluteJ { get; private set; }
    public double LastJobMilliseconds { get; private set; }
    public double LastTickMilliseconds { get; private set; }
    public string Diagnostic { get; private set; }
    public Cell GetCell(int i) => cells[i];
    public double SumConductance(int i) => sums[i];
    public ThermalExchange(Cell[] source, Face[] connections)
    {
        cells = (Cell[])source.Clone(); faces = (Face[])connections.Clone();
        offsets = new int[cells.Length + 1]; revisions = new int[cells.Length];
        var unique = new HashSet<(EquipmentThermalState, int)>();
        for (int i = 0; i < cells.Length; i++)
        {
            if (cells[i].Owner == null || cells[i].Index < 0 || cells[i].Index >= cells[i].Owner.Count || !unique.Add((cells[i].Owner, cells[i].Index)))
                throw new InvalidOperationException("Duplicate or invalid thermal owner: " + cells[i].Label);
            revisions[i] = -1;
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
        var c = cells[i]; double capacity = c.Owner.HeatCapacityJPerK[c.Index];
        return reason + "; " + c.Label + "; C=" + capacity.ToString("R") + " J/K; sumG=" + sums[i].ToString("R") +
            " W/K; dtMax=" + (sums[i] > 0 ? capacity/sums[i] : double.PositiveInfinity).ToString("R") + " s";
    }
    public void RefreshCoefficients()
    {
        bool changed = CoefficientBuilds == 0;
        for (int i = 0; i < cells.Length; i++)
        {
            var c = cells[i];
            if (c.Owner.Diagnostic != null || c.Owner.IsDirty(c.Index) || !c.Owner.IsInitialized(c.Index)) throw new InvalidOperationException(Detail(i,"Cache not ready"));
            changed |= revisions[i] != c.Owner.CacheRevision;
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
        MinimumStepSeconds = double.PositiveInfinity; LimitingCell = -1;
        for (int i = 0; i < cells.Length; i++)
        {
            var c = cells[i]; double capacity = c.Owner.HeatCapacityJPerK[c.Index];
            if (!Positive(capacity) || !Finite(sums[i])) throw new InvalidOperationException(Detail(i,"Invalid C or sumG"));
            double limit = sums[i] > 0 ? capacity/sums[i] : double.PositiveInfinity;
            if (limit < MinimumStepSeconds) { MinimumStepSeconds = limit; LimitingCell = i; }
            revisions[i] = c.Owner.CacheRevision;
        }
        CoefficientBuilds++;
    }
    public void ValidateStep(double seconds)
    {
        RefreshCoefficients();
        if (!Positive(seconds)) throw new ArgumentOutOfRangeException(nameof(seconds));
        if (seconds > MinimumStepSeconds) throw new InvalidOperationException(Detail(LimitingCell,"Explicit step rejected: dt="+seconds+" s"));
    }
    private struct Gather : IJobParallelFor
    {
        [ReadOnly] public NativeArray<int> Offsets, Neighbours;
        [ReadOnly] public NativeArray<double> G, T, Energy;
        [WriteOnly] public NativeArray<double> Next;
        public double Seconds;
        public void Execute(int i)
        {
            double watts = 0;
            for (int p = Offsets[i]; p < Offsets[i+1]; p++) watts += G[p] * (T[Neighbours[p]] - T[i]);
            Next[i] = Energy[i] + Seconds * watts;
        }
    }
    // No owner is written before EVERY candidate and both conservation checks pass.
    public void Step(double seconds = 60)
    {
        var total = Stopwatch.StartNew(); Diagnostic = null;
        try
        {
            ValidateStep(seconds);
            using (var o = new NativeArray<int>(offsets,Allocator.TempJob))
            using (var n = new NativeArray<int>(neighbours,Allocator.TempJob))
            using (var g = new NativeArray<double>(coefficients,Allocator.TempJob))
            using (var t = new NativeArray<double>(cells.Length,Allocator.TempJob))
            using (var e = new NativeArray<double>(cells.Length,Allocator.TempJob))
            using (var next = new NativeArray<double>(cells.Length,Allocator.TempJob))
            {
                double absoluteEnergy = 0;
                for (int i = 0; i < cells.Length; i++)
                {
                    var c = cells[i]; e[i] = c.Owner.EnergyJ[c.Index]; t[i] = e[i] / c.Owner.HeatCapacityJPerK[c.Index];
                    if (!Finite(t[i]) || t[i] < -273.15) throw new InvalidOperationException(Detail(i,"Invalid old temperature"));
                    absoluteEnergy += Math.Abs(e[i]);
                }
                var jobTime = Stopwatch.StartNew();
                new Gather {Offsets=o,Neighbours=n,G=g,T=t,Energy=e,Next=next,Seconds=seconds}.Schedule(cells.Length,64).Complete();
                LastJobMilliseconds = jobTime.Elapsed.TotalMilliseconds;
                var staged = new float[cells.Length];
                DoubleResidualJ = FloatResidualJ = RoundingAbsoluteJ = 0;
                double residualCorrection = 0, floatCorrection = 0;
                for (int i = 0; i < cells.Length; i++)
                {
                    var c = cells[i]; double capacity = c.Owner.HeatCapacityJPerK[c.Index];
                    double value = next[i], temp = value/capacity, min = t[i], max = t[i];
                    for (int p = offsets[i]; p < offsets[i+1]; p++) { min = Math.Min(min,t[neighbours[p]]); max = Math.Max(max,t[neighbours[p]]); }
                    double tolerance = 1e-10 * Math.Max(1,Math.Max(Math.Abs(min),Math.Abs(max)));
                    staged[i] = (float)value;
                    if (!Finite(value) || !ThermalMath.Finite(staged[i]) || temp < -273.15 || temp < min-tolerance || temp > max+tolerance)
                        throw new InvalidOperationException(Detail(i,"Non-finite or divergent candidate"));
                    DoubleResidualJ = Add(DoubleResidualJ,value-e[i],ref residualCorrection);
                    FloatResidualJ = Add(FloatResidualJ,(double)staged[i]-e[i],ref floatCorrection);
                    RoundingAbsoluteJ += Math.Abs((double)staged[i]-value);
                }
                double doubleTolerance = 1e-10 * Math.Max(1,absoluteEnergy);
                if (Math.Abs(DoubleResidualJ) > doubleTolerance || Math.Abs(FloatResidualJ) > doubleTolerance + RoundingAbsoluteJ)
                    throw new InvalidOperationException("Closed-system conservation failed: double="+DoubleResidualJ+", float="+FloatResidualJ);
                for (int i = 0; i < cells.Length; i++) cells[i].Owner.RestoreEnergy(cells[i].Index,staged[i]);
            }
        }
        catch (Exception ex) { Diagnostic = ex.Message; throw; }
        finally { LastTickMilliseconds = total.Elapsed.TotalMilliseconds; }
    }
    private static double Add(double sum, double value, ref double correction)
    { double y=value-correction, result=sum+y; correction=(result-sum)-y; return result; }
}
