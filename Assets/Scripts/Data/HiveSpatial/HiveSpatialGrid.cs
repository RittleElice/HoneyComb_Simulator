using System;
using System.Collections.Generic;

public readonly struct HiveGridCell
{
    public EquipmentSpatialCell SourceCell { get; }
    public HiveMaterial Material => SourceCell?.Material ?? HiveMaterial.Air;
    public ItemData Equipment => SourceCell?.Equipment;
    public FrameData Frame => Equipment == null ? null : Equipment.GetContent<FrameData>();
    public HiveGridCell(EquipmentSpatialCell sourceCell) { SourceCell = sourceCell; }
}

public readonly struct HiveGridEdge
{
    public int A { get; }
    public int B { get; }
    public double AreaMm2 { get; }
    public double DistanceMm { get; }
    public EquipmentSpatialFace SourceFace { get; }
    public ItemData BoundaryEquipment => SourceFace?.Equipment;
    public bool HasQueenExcluder => BoundaryEquipment != null;
    // Qualitative permeability only: physical transport coefficients are deliberately unspecified.
    public bool AllowsAir { get; }
    public bool AllowsWorkers => AllowsAir && (SourceFace?.AllowsWorkers ?? true);
    public bool AllowsQueen => AllowsAir && (SourceFace?.AllowsQueen ?? true);
    public HiveGridEdge(int a, int b, double area, double distance, EquipmentSpatialFace boundary, bool air)
    { A = a; B = b; AreaMm2 = area; DistanceMm = distance; SourceFace = boundary; AllowsAir = air && (boundary?.AllowsAir ?? true); }
}

// Composed placement view. Solid cells reference equipment-owned local cells.
public sealed class HiveSpatialGrid
{
    private readonly HiveGridCell[] cells;
    public ItemData Box { get; }
    public HiveGridLayout Layout { get; }
    public int CellCount => cells.Length;
    public int Revision { get; private set; }
    public event Action<HiveSpatialGrid> Changed;
    public ItemData TopBoundaryEquipment => Box.TopSlot?.Item;

    internal HiveSpatialGrid(ItemData box)
    {
        Box = box ?? throw new ArgumentNullException(nameof(box));
        if (box.Kind != ItemKind.BroodBox && box.Kind != ItemKind.Super && box.Kind != ItemKind.Lid)
            throw new ArgumentException("Only hive boxes and lids have a spatial grid.");
        Layout = box.Kind == ItemKind.Lid ? HiveGridLayout.Lid :
            box.Kind == ItemKind.BroodBox ? HiveGridLayout.Brood : HiveGridLayout.Super;
        cells = new HiveGridCell[Layout.CellCount];
        for (int z = 0; z < Layout.SizeZ; z++)
        for (int y = 0; y < 52; y++)
        for (int x = 0; x < 18; x++)
        {
            cells[Layout.Index(x, y, z)] = new HiveGridCell(box.EquipmentGrid.GetCell(x,y,z));
        }
        if (Layout.IsLid) return;
        for (int i = 0; i < 10; i++)
        {
            RefreshSlot(i);
            box.InteriorSlots.GetSlot(i).OnChanged += OnSlotChanged;
        }
        box.TopSlot.OnChanged += OnTopChanged;
    }

    public HiveGridCell GetCell(int x, int y, int z) => cells[Layout.Index(x, y, z)];
    public HiveGridCell GetCell(int index)
    {
        if (index < 0 || index >= cells.Length) throw new ArgumentOutOfRangeException(nameof(index));
        return cells[index];
    }

    // Read current contents/settings. Placement Revision alone is NOT a thermal cache key.
    public double GetHeatCapacityJPerK(int x, int y, int z) => GetHeatCapacityJPerK(Layout.Index(x,y,z));
    public double GetHeatCapacityJPerK(int index)
    {
        var source = GetCell(index).SourceCell;
        return source != null ? source.HeatCapacityJPerK :
            CombBalance.MaterialHeatCapacityJPerK(HiveMaterial.Air, Layout.VolumeMm3(index));
    }

    private void OnSlotChanged(ItemSlot slot)
    {
        for (int i = 0; i < 10; i++)
            if (ReferenceEquals(slot, Box.InteriorSlots.GetSlot(i))) { RefreshSlot(i); break; }
        Notify();
    }
    private void OnTopChanged(ItemSlot slot) => Notify();
    private void Notify() { Revision++; Changed?.Invoke(this); }

    private void RefreshSlot(int slot)
    {
        ItemData item = Box.InteriorSlots.GetSlot(slot).Item;
        EquipmentSpatialGrid local = item?.EquipmentGrid;
        for (int z = 0; z < 10; z++)
        for (int y = 0; y < 5; y++)
        for (int x = 0; x < 16; x++)
        {
            EquipmentSpatialCell source = local != null && x >= 1 && x < 1 + local.SizeX &&
                y >= local.SlotFirstY && y < local.SlotFirstY + local.SizeY ?
                local.GetCell(x - 1, y - local.SlotFirstY, z) : null;
            cells[Layout.Index(x + 1, 1 + slot * 5 + y, Layout.SlotBottom + z)] =
                new HiveGridCell(source);
        }
    }

    // Direction order: -X,+X,-Y,+Y,-Z,+Z. Exterior neighbours return -1.
    public int Neighbour(int index, int direction)
    {
        Layout.Coordinates(index, out int x, out int y, out int z);
        switch (direction)
        {
            case 0: x--; break; case 1: x++; break;
            case 2: y--; break; case 3: y++; break;
            case 4: z--; break; case 5: z++; break;
            default: throw new ArgumentOutOfRangeException(nameof(direction));
        }
        return x < 0 || x >= 18 || y < 0 || y >= 52 || z < 0 || z >= Layout.SizeZ ? -1 : Layout.Index(x, y, z);
    }

    // Enumerates each geometric face once. Solid faces remain neighbours, not air-flow paths.
    public IEnumerable<HiveGridEdge> Edges()
    {
        for (int i = 0; i < cells.Length; i++)
        {
            Layout.Coordinates(i, out int x, out int y, out int z);
            double dx = Layout.X[x + 1] - Layout.X[x], dy = Layout.Y[y + 1] - Layout.Y[y], dz = Layout.Z[z + 1] - Layout.Z[z];
            for (int axis = 0; axis < 3; axis++)
            {
                int n = Neighbour(i, axis * 2 + 1);
                if (n < 0) continue;
                double width = axis == 0 ? dx : axis == 1 ? dy : dz;
                double next = axis == 0 ? Layout.X[x + 2] - Layout.X[x + 1] :
                    axis == 1 ? Layout.Y[y + 2] - Layout.Y[y + 1] : Layout.Z[z + 2] - Layout.Z[z + 1];
                EquipmentSpatialFace boundary = axis == 2 && z == Layout.SizeZ - 2 && x > 0 && x < 17 && y > 0 && y < 51 ?
                    TopBoundaryEquipment?.EquipmentGrid.GetFace(x - 1,y - 1) : null;
                yield return new HiveGridEdge(i, n, axis == 0 ? dy * dz : axis == 1 ? dx * dz : dx * dy, (width + next) * 0.5, boundary,
                    cells[i].Material == HiveMaterial.Air && cells[n].Material == HiveMaterial.Air);
            }
        }
    }
}
