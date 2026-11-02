using System;
using System.Collections.Generic;

// Coordinates are millimetres; Z increases from the bottom towards the open top.
public sealed class HiveGridLayout
{
    public const int CombColumns = 5, CombRows = 3, FrameSizeX = CombColumns + 2, FrameSizeZ = CombRows + 2;
    private static readonly double[] SlotLayersMm = { 5, 12.5, 12.5, 5 };
    public static readonly double SlotDepthMm = ArraySum(SlotLayersMm);
    private static double ArraySum(double[] widths) { double sum=0; foreach(double width in widths) sum+=width; return sum; }
    public static double SlotLayerEnergyShare(int layer) => SlotLayersMm[layer] / SlotDepthMm;
    public const int SizeX = CombColumns + 6, SizeY = 42, SlotCount = 10, SlotDepthCells = 4;
    public static readonly HiveGridLayout Brood = new HiveGridLayout(true);
    public static readonly HiveGridLayout Super = new HiveGridLayout(false);
    public static readonly HiveGridLayout Lid = new HiveGridLayout();
    public IReadOnlyList<double> X { get; }
    public IReadOnlyList<double> Y { get; }
    public IReadOnlyList<double> Z { get; }
    public bool HasFloor { get; }
    public bool IsLid { get; }
    public int SizeZ => Z.Count - 1;
    public int CellCount => SizeX * SizeY * SizeZ;
    public int SlotBottom => IsLid ? -1 : HasFloor ? 3 : 1;
    // Unreduced entrance: X=0..20, Y=20..370, Z=15..25 mm.
    // The forty Y subdivisions through the X=0 wall are air, not equipment-owned wood.
    public bool IsEntranceCell(int x, int y, int z) => HasFloor && x == 0 && z == 1 && y > 0 && y < SizeY - 1;
    public int EdgeCount => (SizeX - 1) * SizeY * SizeZ + SizeX * (SizeY - 1) * SizeZ + SizeX * SizeY * (SizeZ - 1);

    // A single 20 mm layer with exactly the same XY partition as the boxes.
    private HiveGridLayout()
    {
        IsLid = true;
        X = Brood.X; Y = Brood.Y;
        Z = Array.AsReadOnly(new double[] { 0, 20 });
    }

    private HiveGridLayout(bool brood)
    {
        HasFloor = brood;
        var x = new List<double> { 20, 8, 10 };
        for (int i = 0; i < CombColumns; i++) x.Add(420.0 / CombColumns);
        x.AddRange(new double[] { 10, 8, 20 });
        var y = new List<double> { 20 };
        for (int i = 0; i < SlotCount; i++) y.AddRange(SlotLayersMm);
        y.Add(20);
        var z = new List<double>();
        if (brood) z.AddRange(new double[] { 15, 10 });
        z.AddRange(new double[] { 8, 10 });
        for (int i = 0; i < CombRows; i++) z.Add(200.0 / CombRows);
        z.AddRange(new double[] { 10, 5, 5 });
        X = Boundaries(x); Y = Boundaries(y); Z = Boundaries(z);
        if (X[SizeX] != 496 || Y[SizeY] != 390 || Math.Abs(Z[SizeZ] - (brood ? 263 : 238)) > 1e-9)
            throw new InvalidOperationException("Hive dimension totals disagree with the specification.");
    }

    private static IReadOnlyList<double> Boundaries(List<double> widths)
    {
        var bounds = new double[widths.Count + 1];
        for (int i = 0; i < widths.Count; i++) bounds[i + 1] = bounds[i] + widths[i];
        return Array.AsReadOnly(bounds);
    }

    public int Index(int x, int y, int z)
    {
        if (x < 0 || x >= SizeX || y < 0 || y >= SizeY || z < 0 || z >= SizeZ)
            throw new ArgumentOutOfRangeException("Grid coordinate");
        return x + SizeX * (y + SizeY * z);
    }

    public void Coordinates(int index, out int x, out int y, out int z)
    {
        if (index < 0 || index >= CellCount) throw new ArgumentOutOfRangeException(nameof(index));
        x = index % SizeX; y = index / SizeX % SizeY; z = index / (SizeX * SizeY);
    }

    public double VolumeMm3(int index)
    {
        Coordinates(index, out int x, out int y, out int z);
        return (X[x + 1] - X[x]) * (Y[y + 1] - Y[y]) * (Z[z + 1] - Z[z]);
    }
}
