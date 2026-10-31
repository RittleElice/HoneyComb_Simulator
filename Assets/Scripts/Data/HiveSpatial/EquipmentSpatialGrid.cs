using System;
using System.Collections.Generic;

// Persistent, equipment-owned cell identity. Placement never recreates these objects.
public sealed class EquipmentSpatialCell
{
    public ItemData Equipment { get; }
    public int Index { get; }
    public HiveMaterial Material { get; }
    public double VolumeMm3 { get; }
    public FrameData Frame => Equipment.GetContent<FrameData>();
    public CombRegionThermalMass ContentThermalMass => Equipment.EquipmentGrid.GetContentThermalMass(Index);
    public double ContentHeatCapacityJPerK => ContentThermalMass.HeatCapacityJPerK;
    public double HeatCapacityJPerK => CombBalance.MaterialHeatCapacityJPerK(Material, VolumeMm3) + ContentHeatCapacityJPerK;
    internal EquipmentSpatialCell(ItemData equipment, int index, HiveMaterial material, double volume)
    { Equipment = equipment; Index = index; Material = material; VolumeMm3 = volume; }
}

public sealed class EquipmentSpatialFace
{
    public ItemData Equipment { get; }
    public int Index { get; }
    public double AreaMm2 { get; }
    public bool AllowsAir => true;
    public bool AllowsWorkers => true;
    public bool AllowsQueen => false;
    internal EquipmentSpatialFace(ItemData equipment, int index, double area)
    { Equipment = equipment; Index = index; AreaMm2 = area; }
}

// Local geometry only. Empty box volume is null, not equipment-owned air.
// Queen excluders own a zero-thickness 16 x 50 face grid, not artificial solid cells.
public sealed class EquipmentSpatialGrid
{
    private readonly EquipmentSpatialCell[] cells;
    private readonly EquipmentSpatialFace[] faces;
    public ItemData Equipment { get; }
    public IReadOnlyList<double> X { get; }
    public IReadOnlyList<double> Y { get; }
    public IReadOnlyList<double> Z { get; }
    public int SizeX => X.Count - 1;
    public int SizeY => Y.Count - 1;
    public int SizeZ => Z.Count - 1;
    public int CellCount { get; private set; }
    public int FaceCount => faces.Length;
    // Offset within a slot's five Y subdivisions, for panel placement.
    public int SlotFirstY { get; }

    public static bool Supports(ItemKind kind) => kind == ItemKind.Frame || kind == ItemKind.DividerBoard ||
        kind == ItemKind.InsulationBoard || kind == ItemKind.QueenExcluder || kind == ItemKind.BroodBox ||
        kind == ItemKind.Super || kind == ItemKind.Lid;

    internal EquipmentSpatialGrid(ItemData item)
    {
        Equipment = item;
        bool frame = item.Kind == ItemKind.Frame;
        bool panel = item.Kind == ItemKind.DividerBoard || item.Kind == ItemKind.InsulationBoard;
        bool boundary = item.Kind == ItemKind.QueenExcluder;
        HiveOccupancyProfile profile = null;
        HiveGridLayout layout = null;
        if (frame || panel)
        {
            profile = panel ? item.GetContent<HiveOccupancyProfile>() ??
                (item.Kind == ItemKind.DividerBoard ? HiveOccupancyProfile.Divider : HiveOccupancyProfile.Insulation) : null;
            SlotFirstY = frame ? 1 : profile.FirstY;
            X = Slice(HiveGridLayout.Brood.X, 2, 14);
            Y = Slice(HiveGridLayout.Brood.Y, 1 + SlotFirstY, frame ? 3 : profile.CountY);
            Z = Slice(HiveGridLayout.Brood.Z, 3, 10);
        }
        else if (boundary)
        {
            X = Slice(HiveGridLayout.Brood.X, 1, 16);
            Y = Slice(HiveGridLayout.Brood.Y, 1, 50);
            Z = Array.AsReadOnly(new double[] { 0 });
        }
        else
        {
            layout = item.Kind == ItemKind.BroodBox ? HiveGridLayout.Brood :
                item.Kind == ItemKind.Super ? HiveGridLayout.Super : HiveGridLayout.Lid;
            X = layout.X; Y = layout.Y; Z = layout.Z;
        }
        cells = new EquipmentSpatialCell[SizeX * SizeY * SizeZ];
        faces = new EquipmentSpatialFace[boundary ? SizeX * SizeY : 0];
        for (int y = 0; y < SizeY; y++)
        for (int x = 0; x < SizeX; x++)
        {
            double area = (X[x + 1] - X[x]) * (Y[y + 1] - Y[y]);
            if (boundary) faces[x + SizeX * y] = new EquipmentSpatialFace(item, x + SizeX * y, area);
            for (int z = 0; z < SizeZ; z++)
            {
                bool occupied = frame || panel || layout.IsLid || x == 0 || x == SizeX - 1 ||
                    y == 0 || y == SizeY - 1 || (layout.HasFloor && z == 0);
                if (!occupied) continue;
                HiveMaterial material = panel ? profile.Material : frame && x > 0 && x < SizeX - 1 && z > 0 && z < SizeZ - 1 ? HiveMaterial.Comb : HiveMaterial.Wood;
                int index = Index(x,y,z);
                cells[index] = new EquipmentSpatialCell(item, index, material, area * (Z[z + 1] - Z[z]));
                CellCount++;
            }
        }
    }

    // Front/back are stable data sides, not the current display FacingSide.
    // Logical 7x5 groups: front -> local Y=0; back -> local Y=2; centre Y=1 has no contents.
    public CombRegionThermalMass GetContentThermalMass(int index)
    {
        if (index < 0 || index >= cells.Length) throw new ArgumentOutOfRangeException(nameof(index));
        if (Equipment.Kind != ItemKind.Frame || cells[index]?.Material != HiveMaterial.Comb) return default;
        var frame = Equipment.GetContent<FrameData>();
        if (frame == null) return default;
        int x = index % SizeX, y = index / SizeX % SizeY, z = index / (SizeX * SizeY);
        if (y == 1) return default;
        return frame.GetSurface(y == 0 ? FrameSide.Front : FrameSide.Back).GetThermalRegion(x - 1, z - 1);
    }

    private static IReadOnlyList<double> Slice(IReadOnlyList<double> source, int start, int count)
    {
        var values = new double[count + 1];
        for (int i = 0; i <= count; i++) values[i] = source[start + i] - source[start];
        return Array.AsReadOnly(values);
    }
    public int Index(int x, int y, int z)
    {
        if (x < 0 || x >= SizeX || y < 0 || y >= SizeY || z < 0 || z >= SizeZ)
            throw new ArgumentOutOfRangeException("Equipment coordinate");
        return x + SizeX * (y + SizeY * z);
    }
    public EquipmentSpatialCell GetCell(int x, int y, int z) => cells[Index(x,y,z)];
    // Only owned solids are neighbours here. Null means air/empty space or exterior.
    public EquipmentSpatialCell Neighbour(int x, int y, int z, int direction)
    {
        Index(x,y,z);
        switch (direction)
        {
            case 0: x--; break; case 1: x++; break;
            case 2: y--; break; case 3: y++; break;
            case 4: z--; break; case 5: z++; break;
            default: throw new ArgumentOutOfRangeException(nameof(direction));
        }
        return x < 0 || x >= SizeX || y < 0 || y >= SizeY || z < 0 || z >= SizeZ ? null : GetCell(x,y,z);
    }
    public EquipmentSpatialFace GetFace(int x, int y)
    {
        if (faces.Length == 0 || x < 0 || x >= SizeX || y < 0 || y >= SizeY)
            throw new ArgumentOutOfRangeException("Equipment face coordinate");
        return faces[x + SizeX * y];
    }
}
