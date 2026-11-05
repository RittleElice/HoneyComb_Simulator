using System;
using System.Collections.Generic;

public static class HiveSpatialGridTests
{
    private static int checks;
    private static void Check(bool value, string message)
    { checks++; if (!value) throw new Exception("Spatial grid: " + message); }
    private static ItemData Item(ItemKind kind, object content = null) => new ItemData(0, kind.ToString(), kind, content);

#if UNITY_EDITOR
    [UnityEditor.MenuItem("Tools/HoneyComb/검증/Run Hive Spatial Grid Tests")]
#endif
    public static void RunAll()
    {
        checks = 0;
        Geometry(ItemKind.BroodBox, 4158, 263, 11535);
        Geometry(ItemKind.Super, 3234, 238, 8869);
        OccupancyAndEvents();
        Stack();
        LidLayer();
        EquipmentOwnership();
        ItemAssemblyTests.RunAll();
        string message = "HiveSpatialGrid tests passed: " + checks + " checks.";
#if UNITY_EDITOR
        UnityEngine.Debug.Log(message);
#else
        Console.WriteLine(message);
#endif
    }

    private static void Geometry(ItemKind kind, int count, double height, int edges)
    {
        var box = Item(kind); var grid = box.SpatialGrid; var layout = grid.Layout;
        Check(ReferenceEquals(grid, box.SpatialGrid), "stable lazy grid");
        Check(grid.CellCount == count && Math.Abs(layout.Z[layout.SizeZ] - height) < 1e-8, "dimensions");
        double volume = 0; int air = 0;
        for (int i = 0; i < count; i++)
        {
            layout.Coordinates(i, out int x, out int y, out int z);
            Check(layout.Index(x,y,z) == i, "index round trip");
            Check(layout.VolumeMm3(i) > 0, "positive volume"); volume += layout.VolumeMm3(i);
            if (grid.GetCell(i).Material == HiveMaterial.Air) air++;
            for (int d = 0; d < 6; d++)
            {
                int n = grid.Neighbour(i,d);
                if (n >= 0) Check(grid.Neighbour(n,d ^ 1) == i, "symmetric neighbour");
            }
        }
        Check(Math.Abs(volume - 496 * 390 * height) < .001, "total volume");
        Check(air == (kind == ItemKind.BroodBox ? 2920 : 2520), "empty interior air");
        int actual = 0; var unique = new HashSet<long>();
        foreach (var edge in grid.Edges())
        {
            actual++;
            Check(unique.Add((long)edge.A * count + edge.B), "unique face");
            Check(edge.AreaMm2 > 0 && edge.DistanceMm > 0 && edge.B > edge.A, "face geometry");
        }
        Check(actual == edges && actual == layout.EdgeCount, "edge total");
        Check(grid.Neighbour(0,0) == -1 && grid.Neighbour(0,2) == -1 && grid.Neighbour(0,4) == -1, "outer boundaries");
        bool threw = false;
        try { layout.Index(HiveGridLayout.SizeX,0,0); } catch (ArgumentOutOfRangeException) { threw = true; }
        Check(threw, "invalid index");
    }

    private static void OccupancyAndEvents()
    {
        var box = Item(ItemKind.BroodBox); var grid = box.SpatialGrid;
        var frameData = new FrameData(); var frame = Item(ItemKind.Frame, frameData);
        int events = 0; grid.Changed += _ => events++;
        Check(box.InteriorSlots.GetSlot(0).TryInsert(frame), "frame insertion");
        int comb = 0, wood = 0;
        for (int z = 3; z < 3 + HiveGridLayout.FrameSizeZ; z++)
        for (int y = 1; y <= 5; y++)
        for (int x = 1; x < HiveGridLayout.SizeX - 1; x++)
        {
            var cell = grid.GetCell(x,y,z);
            if (cell.Material == HiveMaterial.Comb) comb++;
            if (cell.Material == HiveMaterial.Wood) wood++;
            if (cell.Material != HiveMaterial.Air) Check(ReferenceEquals(cell.Frame,frameData), "frame reference");
        }
        Check(comb == 30 && wood == 40, "frame interior and perimeter");
        Check(grid.GetCell(3,1,5).Material == HiveMaterial.Air && grid.GetCell(3,4,5).Material == HiveMaterial.Air, "front/back air");
        Check(box.InteriorSlots.GetSlot(0).TryMoveTo(box.InteriorSlots.GetSlot(9)), "move");
        Check(grid.GetCell(3,3,5).Material == HiveMaterial.Air && ReferenceEquals(grid.GetCell(3,39,5).Frame,frameData), "move refresh");
        var super = Item(ItemKind.Super);
        var upperGrid = super.SpatialGrid;
        Check(box.InteriorSlots.GetSlot(9).TryMoveTo(super.InteriorSlots.GetSlot(0)), "cross box move");
        Check(grid.GetCell(3,39,5).Material == HiveMaterial.Air && ReferenceEquals(upperGrid.GetCell(3,3,3).Frame,frameData), "both grids refreshed");
        Check(super.InteriorSlots.GetSlot(0).TrySwap(box.InteriorSlots.GetSlot(1)), "swap");
        Check(ReferenceEquals(grid.GetCell(3,7,5).Frame,frameData), "swap refresh");
        box.InteriorSlots.GetSlot(1).Take();
        Check(grid.GetCell(3,7,5).Material == HiveMaterial.Air, "removal refresh");
        Check(box.InteriorSlots.GetSlot(0).TryInsert(Item(ItemKind.DividerBoard)), "divider accepted");
        Check(grid.GetCell(3,3,5).Material == HiveMaterial.Wood && grid.GetCell(3,2,5).Material == HiveMaterial.Wood, "5mm profile");
        Check(box.InteriorSlots.GetSlot(1).TryInsert(Item(ItemKind.InsulationBoard)), "insulation accepted");
        Check(grid.GetCell(3,6,5).Material == HiveMaterial.Insulation && grid.GetCell(3,7,5).Material == HiveMaterial.Insulation, "25mm profile");
        var custom = new HiveOccupancyProfile(1,1,HiveMaterial.Insulation);
        Check(box.InteriorSlots.GetSlot(2).TryInsert(Item(ItemKind.DividerBoard,custom)), "custom profile");
        Check(grid.GetCell(3,10,5).Material == HiveMaterial.Insulation && grid.GetCell(3,11,5).Material == HiveMaterial.Air, "custom occupancy");
        int previous = events;
        Check(!box.InteriorSlots.GetSlot(0).TryInsert(Item(ItemKind.Frame)), "rejected insert");
        Check(events == previous, "failed operation no update");
        var queen = Item(ItemKind.QueenExcluder);
        Check(box.TopSlot.TryInsert(queen), "excluder insertion");
        int faces = 0, openFaces = 0, blockedFaces = 0;
        foreach (var edge in grid.Edges())
            if (edge.HasQueenExcluder)
            {
                faces++;
                bool air=grid.GetCell(edge.A).Material==HiveMaterial.Air && grid.GetCell(edge.B).Material==HiveMaterial.Air;
                Check(edge.AllowsAir==air && edge.AllowsWorkers==air && !edge.AllowsQueen &&
                    ReferenceEquals(edge.BoundaryEquipment,queen),"excluder permeability follows adjacent materials");
                if(air)openFaces++;else blockedFaces++;
            }
        Check(openFaces>0 && blockedFaces>0,"excluder spans air and installed equipment");
        Check(faces == 360 && events == previous + 1, "excluder faces and event");
        box.TopSlot.Take();
        foreach (var edge in grid.Edges()) Check(!edge.HasQueenExcluder, "excluder removal");
        Check(grid.Revision == events, "revision");
        Check(Item(ItemKind.Frame).SpatialGrid == null, "non-box no grid");
    }

    private static void Stack()
    {
        var lower = new ItemSlot(); var upper = new ItemSlot(); var target = new ItemSlot();
        var brood = Item(ItemKind.BroodBox); var super = Item(ItemKind.Super);
        lower.TryInsert(brood); upper.TryInsert(super);
        var observer = new HiveSpatialAssembly(lower);
        int notifications = 0; observer.Changed += () => notifications++;
        Check(lower.TryCombineFrom(upper), "combine");
        Check(observer.Boxes.Count == 2 && observer.HasOpenTop, "stack view");
        int faces = 0;
        foreach (var face in observer.Interfaces()) { faces++; Check(face.AreaMm2 > 0 && face.DistanceMm == 9, "stack face"); }
        Check(faces == 462, "stack face total");
        upper.TryInsert(Item(ItemKind.Lid)); lower.TryCombineFrom(upper);
        Check(!observer.HasOpenTop && observer.Lid.Kind == ItemKind.Lid, "lid boundary");
        Check(lower.TrySplitTo(1,target), "split");
        Check(observer.Boxes.Count == 1 && observer.HasOpenTop && ReferenceEquals(observer.Boxes[0],brood.SpatialGrid), "split preserves grid");
        int previous = notifications; super.InteriorSlots.TryAdd(Item(ItemKind.Frame));
        Check(notifications == previous, "detached box unsubscribed");
        observer.Dispose(); brood.InteriorSlots.TryAdd(Item(ItemKind.Frame)); lower.TakeValue();
        Check(notifications == previous, "disposed observer unsubscribed");
    }

    private static void EquipmentOwnership()
    {
        var frameData = new FrameData();
        var frame = Item(ItemKind.Frame, frameData);
        var own = frame.EquipmentGrid;
        Check(own.SizeX == 7 && own.SizeY == 2 && own.SizeZ == 5 && own.CellCount == 70, "detached frame geometry");
        Check(own.X[own.SizeX] == 440 && own.Y[2] == 25 && Math.Abs(own.Z[own.SizeZ] - 220) < 1e-8, "frame physical dimensions");
        Check(ReferenceEquals(own,frame.EquipmentGrid), "stable equipment grid");
        var brood = Item(ItemKind.BroodBox); var super = Item(ItemKind.Super);
        var space = brood.SpatialGrid; var otherSpace = super.SpatialGrid;
        Check(brood.EquipmentGrid.CellCount == 1238 && super.EquipmentGrid.CellCount == 714, "boxes own only shell");
        Check(brood.EquipmentGrid.GetCell(3,3,5) == null, "box does not own interior air");
        Check(ReferenceEquals(space.GetCell(0,0,0).SourceCell,brood.EquipmentGrid.GetCell(0,0,0)), "shell placed by reference");
        for (int slot = 0; slot < 10; slot++)
        {
            Check(brood.InteriorSlots.GetSlot(slot).TryInsert(frame), "place detached frame");
            double volume = 0;
            for (int z = 0; z < HiveGridLayout.FrameSizeZ; z++)
            for (int y = 0; y < 2; y++)
            for (int x = 0; x < HiveGridLayout.FrameSizeX; x++)
            {
                var local = own.GetCell(x,y,z);
                var placed = space.GetCell(x + 2,slot * 4 + y + 2,z + 3);
                Check(ReferenceEquals(local,placed.SourceCell) && ReferenceEquals(local.Frame,frameData), "exact local cell reference");
                Check(local.VolumeMm3 == space.Layout.VolumeMm3(space.Layout.Index(x + 2,slot * 4 + y + 2,z + 3)), "placed cell volume");
                volume += local.VolumeMm3;
            }
            Check(Math.Abs(volume - 440 * 25 * 220) < .001, "frame solid volume");
            Check(brood.InteriorSlots.GetSlot(slot).TryMoveTo(super.InteriorSlots.GetSlot(slot)), "move referenced frame");
            Check(space.GetCell(3,slot * 4 + 3,5).SourceCell == null, "old space freed");
            Check(ReferenceEquals(otherSpace.GetCell(3,slot * 4 + 3,3).SourceCell,own.GetCell(1,1,2)), "same cell after cross box movement");
            Check(ReferenceEquals(super.InteriorSlots.GetSlot(slot).Take(),frame), "detach same item");
            Check(ReferenceEquals(frame.EquipmentGrid,own), "detach retains geometry");
        }
        Check(ReferenceEquals(own.Neighbour(0,0,0,1),own.GetCell(1,0,0)) && own.Neighbour(0,0,0,0) == null, "equipment adjacency");
        Check(!ReferenceEquals(Item(ItemKind.Frame).EquipmentGrid.GetCell(0,0,0),own.GetCell(0,0,0)), "different equipment has distinct cells");
        foreach (var kind in new[] { ItemKind.DividerBoard, ItemKind.InsulationBoard })
        {
            var panel = Item(kind); var local = panel.EquipmentGrid;
            Check(local.CellCount == 70, "panel own cells");
            brood.InteriorSlots.GetSlot(0).TryInsert(panel);
            Check(ReferenceEquals(space.GetCell(2,1 + local.SlotFirstY,3).SourceCell,local.GetCell(0,0,0)), "panel placement reference");
            brood.InteriorSlots.GetSlot(0).Take();
            Check(ReferenceEquals(panel.EquipmentGrid,local), "detached panel retained");
        }
        var queen = Item(ItemKind.QueenExcluder); var boundary = queen.EquipmentGrid;
        Check(boundary.CellCount == 0 && boundary.FaceCount == 360 && boundary.SizeZ == 0, "queen zero-thickness face grid");
        brood.TopSlot.TryInsert(queen);
        foreach (var edge in space.Edges())
        {
            if (!edge.HasQueenExcluder) continue;
            space.Layout.Coordinates(edge.A,out int x,out int y,out int z);
            Check(ReferenceEquals(edge.SourceFace,boundary.GetFace(x - 1,y - 1)), "queen face reference");
            Check(edge.AreaMm2 == edge.SourceFace.AreaMm2, "queen area unchanged");
        }
        Check(brood.TopSlot.TryMoveTo(super.TopSlot), "queen move");
        Check(ReferenceEquals(queen.EquipmentGrid,boundary) && space.TopBoundaryEquipment == null, "queen geometry retained");
        var lid = Item(ItemKind.Lid);
        Check(lid.EquipmentGrid.CellCount == 462 && ReferenceEquals(lid.SpatialGrid.GetCell(3,3,0).SourceCell,lid.EquipmentGrid.GetCell(3,3,0)), "lid owns and places cells");
        Check(Item(ItemKind.Generic).EquipmentGrid == null, "unsupported item no geometry");
    }

    private static void LidLayer()
    {
        var lid = Item(ItemKind.Lid); var grid = lid.SpatialGrid;
        Check(ReferenceEquals(grid,lid.SpatialGrid), "stable lid grid");
        Check(grid.CellCount == 462 && grid.Layout.SizeZ == 1 && grid.Layout.Z[1] == 20, "single 20mm lid layer");
        Check(grid.Layout.IsLid && grid.Layout.SlotBottom == -1 && !grid.Layout.HasFloor, "lid has no interior slots or floor");
        Check(lid.InteriorSlots == null && lid.TopSlot == null && grid.TopBoundaryEquipment == null, "lid no equipment slots");
        double volume = 0;
        for (int i = 0; i < grid.CellCount; i++)
        {
            Check(grid.GetCell(i).Material == HiveMaterial.Wood && ReferenceEquals(grid.GetCell(i).Equipment,lid), "lid wood ownership");
            Check(grid.Neighbour(i,4) == -1 && grid.Neighbour(i,5) == -1, "single layer Z boundaries");
            volume += grid.Layout.VolumeMm3(i);
        }
        Check(volume == 496 * 390 * 20, "lid volume");
        int count = 0;
        foreach (var edge in grid.Edges()) { count++; Check(!edge.AllowsAir && !edge.HasQueenExcluder, "solid lid edges"); }
        Check(count == 871 && count == grid.Layout.EdgeCount, "lid XY edge count");
        var lower = new ItemSlot(); var upper = new ItemSlot(); var detached = new ItemSlot();
        var brood = Item(ItemKind.BroodBox);
        lower.TryInsert(brood); upper.TryInsert(lid);
        using (var observer = new HiveSpatialAssembly(lower))
        {
            Check(lower.TryCombineFrom(upper), "attach lid");
            Check(observer.Boxes.Count == 1 && ReferenceEquals(observer.LidGrid,grid) && !observer.HasOpenTop, "lid separate from box list");
            count = 0;
            foreach (var face in observer.Interfaces())
            {
                count++;
                Check(ReferenceEquals(face.Upper,grid) && face.DistanceMm == 15, "lid contact distance");
                Check(face.UpperCell >= 0 && face.UpperCell < grid.CellCount, "lid contact index");
            }
            Check(count == 462 && brood.SpatialGrid.CellCount + grid.CellCount == 4620, "brood and lid totals");
            Check(lower.TrySplitTo(1,detached) && observer.LidGrid == null && observer.HasOpenTop, "remove lid");
            count = 0; foreach (var face in observer.Interfaces()) count++;
            Check(count == 0 && ReferenceEquals(detached.Item.SpatialGrid,grid), "detach preserves lid removes faces");
            upper.TryInsert(Item(ItemKind.Super)); lower.TryCombineFrom(upper); lower.TryCombineFrom(detached);
            count = 0; foreach (var face in observer.Interfaces()) count++;
            Check(count == 924 && observer.Boxes.Count == 2 && ReferenceEquals(observer.LidGrid,grid), "lid above super");
        }
        using (var standalone = new HiveSpatialAssembly(new ItemSlot()))
            Check(standalone.LidGrid == null, "empty assembly");
        var lidSlot = new ItemSlot(); lidSlot.TryInsert(Item(ItemKind.Lid));
        using (var standalone = new HiveSpatialAssembly(lidSlot))
        {
            count = 0; foreach (var face in standalone.Interfaces()) count++;
            Check(standalone.LidGrid.CellCount == 462 && count == 0, "standalone lid");
        }
    }
}
