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
    // Each slot owns compact columns. Offsets delimit one cell for homogeneous empty
    // columns, or four cells for occupied columns and material transitions.
    private sealed class SlotBand
    {
        public readonly int[] Offsets;
        public readonly HiveGridCell[] Cells;
        public SlotBand(int[] offsets, HiveGridCell[] cells) { Offsets = offsets; Cells = cells; }
    }
    private readonly SlotBand[] slots;
    public int ActiveCellCount { get; private set; }
    public ItemData Box { get; }
    public HiveGridLayout Layout { get; }
    // Stable physical address space used by saved equipment and the grid inspector.
    public int CellCount => Layout.CellCount;
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
        ActiveCellCount = Layout.CellCount;
        if (Layout.IsLid) return;
        slots = new SlotBand[HiveGridLayout.SlotCount];
        for (int i = 0; i < HiveGridLayout.SlotCount; i++)
        {
            RefreshSlot(i);
            box.InteriorSlots.GetSlot(i).OnChanged += OnSlotChanged;
        }
        box.TopSlot.OnChanged += OnTopChanged;
    }

    private bool IsSlotY(int y) => slots != null && y >= 1 &&
        y < 1 + HiveGridLayout.SlotCount * HiveGridLayout.SlotDepthCells;

    internal bool IsGroupedColumn(int x, int y, int z)
    {
        if (!IsSlotY(y)) return false;
        var band = slots[(y - 1) / HiveGridLayout.SlotDepthCells];
        int column = x + HiveGridLayout.SizeX * z;
        return band.Offsets[column + 1] - band.Offsets[column] == 1;
    }

    // The active cell's first physical address and its Y span are stable until this slot changes.
    public (int firstIndex, int depthCells) ActiveCellAt(int index)
    {
        Layout.Coordinates(index, out int x, out int y, out int z);
        if (!IsGroupedColumn(x,y,z)) return (index,1);
        int layer = (y - 1) % HiveGridLayout.SlotDepthCells;
        return (index - layer * HiveGridLayout.SizeX,HiveGridLayout.SlotDepthCells);
    }

    public IEnumerable<(int firstIndex, int depthCells)> ActiveCells()
    {
        for (int z = 0; z < Layout.SizeZ; z++)
        for (int y = 0; y < HiveGridLayout.SizeY; y++)
        for (int x = 0; x < HiveGridLayout.SizeX; x++)
        {
            bool grouped = IsGroupedColumn(x,y,z);
            if (grouped && (y - 1) % HiveGridLayout.SlotDepthCells != 0) continue;
            yield return (Layout.Index(x,y,z),grouped ? HiveGridLayout.SlotDepthCells : 1);
        }
    }

    public HiveGridCell GetCell(int x, int y, int z) => GetCell(Layout.Index(x,y,z));
    public HiveGridCell GetCell(int index)
    {
        Layout.Coordinates(index, out int x, out int y, out int z);
        if (!IsSlotY(y)) return new HiveGridCell(Box.EquipmentGrid.GetCell(x,y,z));
        var band = slots[(y - 1) / HiveGridLayout.SlotDepthCells];
        int column = x + HiveGridLayout.SizeX * z;
        int start = band.Offsets[column];
        if (band.Offsets[column + 1] - start != 1)
            return band.Cells[start + (y - 1) % HiveGridLayout.SlotDepthCells];
        // A grouped wood cell still exposes the requested equipment-local source address.
        var representative = band.Cells[start];
        return representative.SourceCell == null ? representative : new HiveGridCell(Box.EquipmentGrid.GetCell(x,y,z));
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
        for (int i = 0; i < HiveGridLayout.SlotCount; i++)
            if (ReferenceEquals(slot, Box.InteriorSlots.GetSlot(i))) { RefreshSlot(i); break; }
        Notify();
    }
    private void OnTopChanged(ItemSlot slot) => Notify();
    private void Notify() { Revision++; Changed?.Invoke(this); }

    private void RefreshSlot(int slot)
    {
        var item = Box.InteriorSlots.GetSlot(slot).Item;
        var local = item?.EquipmentGrid;
        bool empty = Box.InteriorSlots.GetSlot(slot).IsEmpty;
        int depth = HiveGridLayout.SlotDepthCells;
        int firstY = 1 + slot * depth;
        int columns = HiveGridLayout.SizeX * Layout.SizeZ;
        var offsets = new int[columns + 1];
        var active = new List<HiveGridCell>(item == null ? columns : columns * depth);
        var members = new HiveGridCell[depth];
        for (int z = 0; z < Layout.SizeZ; z++)
        for (int x = 0; x < HiveGridLayout.SizeX; x++)
        {
            int frameZ = z - Layout.SlotBottom;
            bool interior = x > 0 && x < HiveGridLayout.SizeX - 1 &&
                frameZ >= 0 && frameZ < HiveGridLayout.FrameSizeZ;
            bool grouped = empty;
            for (int layer = 0; layer < depth; layer++)
            {
                var source = interior ? null : Box.EquipmentGrid.GetCell(x, firstY + layer, z);
                if (interior && local != null && x >= 2 && x < 2 + local.SizeX &&
                    layer >= local.SlotFirstY && layer < local.SlotFirstY + local.SizeY)
                    source = local.GetCell(x - 2,layer - local.SlotFirstY,frameZ);
                var cell = new HiveGridCell(source);
                members[layer] = cell;
                grouped &= cell.Material == members[0].Material &&
                    (cell.Material == HiveMaterial.Wood || cell.Material == HiveMaterial.Air) &&
                    (source == null || (ReferenceEquals(source.Equipment,Box) &&
                        source.Index == Layout.Index(x,firstY + layer,z)));
            }
            if (grouped) active.Add(members[0]);
            else active.AddRange(members);
            offsets[x + HiveGridLayout.SizeX * z + 1] = active.Count;
        }
        var previous = slots[slot];
        slots[slot] = new SlotBand(offsets,active.ToArray());
        ActiveCellCount += active.Count - (previous?.Cells.Length ?? columns * depth);
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
        return x < 0 || x >= HiveGridLayout.SizeX || y < 0 || y >= HiveGridLayout.SizeY || z < 0 || z >= Layout.SizeZ ? -1 : Layout.Index(x, y, z);
    }

    // Enumerates each geometric face once. Solid faces remain neighbours, not air-flow paths.
    public IEnumerable<HiveGridEdge> Edges()
    {
        for (int i = 0; i < CellCount; i++)
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
                EquipmentSpatialFace boundary = axis == 2 && z == Layout.SizeZ - 2 && x > 0 && x < HiveGridLayout.SizeX - 1 && y > 0 && y < HiveGridLayout.SizeY - 1 ?
                    TopBoundaryEquipment?.EquipmentGrid.GetFace(x - 1,y - 1) : null;
                yield return new HiveGridEdge(i, n, axis == 0 ? dy * dz : axis == 1 ? dx * dz : dx * dy, (width + next) * 0.5, boundary,
                    GetCell(i).Material == HiveMaterial.Air && GetCell(n).Material == HiveMaterial.Air);
            }
        }
    }
}
