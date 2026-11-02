using System;
using System.Collections.Generic;
public static class CombGeometryTests
{
    static int checks;
    static void Check(bool ok, string message) { checks++; if (!ok) throw new Exception(message); }
#if UNITY_EDITOR
    [UnityEditor.MenuItem("Tools/HoneyComb/검증/Run Comb Geometry Tests")]
#endif
    public static void RunAll()
    {
        checks = 0;
        var f = new FrameData(); var item = new ItemData(501, "Frame", ItemKind.Frame, f);
        Check(f.Width == 85 && f.Height == 39 && f.TotalCellCount == 6630, "fixed dimensions");
        var g = new CombGeometry(); var ids = new HashSet<CombCellId>();
        var counts = new int[15];
        for (int r = 0; r < 39; r++) for (int c = 0; c < 85; c++)
        {
            g.Center(c, r, out double x, out double z);
            Check(g.TryFind(x,z,out int pc,out int pr) && pc == c && pr == r, "centre hit round trip");
            Check(x - g.StepX / 2 >= -1e-8 && x + g.StepX / 2 <= 420 + 1e-8 && z - g.Radius >= 0 && z + g.Radius <= 200, "bounds");
            Check(ids.Add(new CombCellId(item,FrameSide.Front,c,r)) && ids.Add(new CombCellId(item,FrameSide.Back,c,r)), "unique IDs");
            var mapped = CombSpatialMapper.GetLogicalCell(item,c,r,0);
            int rx = mapped.Index % item.EquipmentGrid.SizeX - 1;
            int rz = mapped.Index / (item.EquipmentGrid.SizeX * item.EquipmentGrid.SizeY) - 1;
            counts[rx + 5 * rz]++;
            Check(CombSpatialMapper.GetLogicalCell(item,c,r,0).Material == HiveMaterial.Comb, "logical comb material");
            if (r < 38)
            {
                g.Center(c,r+1,out double nx,out double nz);
                Check(Math.Abs(Math.Sqrt((nx-x)*(nx-x)+(nz-z)*(nz-z))-g.StepX)<1e-8,"dense neighbour pitch");
            }
        }
        foreach (var count in counts) Check(count == 221,"17 by 13 cells per region");
        Check(Math.Abs(g.Width-420)<1e-8 && Math.Abs(g.Height-167.33028854408101)<1e-5,"physical extent");
        Check(!g.TryFind(210,0,out _,out _),"margin is not a cell");
        f.Front.TrySetConstruction(3,4,100); f.Front.TrySetContent(3,4,CellContentKind.Honey);
        f.Front.TrySetType(3,4,CombCellType.Drone);
        Check(f.Front.GetCell(3,4).Content == CellContentKind.Nectar && f.Back.GetCell(3,4).Content == CellContentKind.Empty,"type preserves contents and side independence");
        var cell = CombSpatialMapper.GetLogicalCell(item,3,4,0);
        var box = new ItemData(502,"Box",ItemKind.BroodBox);
        box.InteriorSlots.GetSlot(0).TryInsert(item);
        var removed = box.InteriorSlots.GetSlot(0).Take();
        Check(ReferenceEquals(item,removed) && ReferenceEquals(cell,CombSpatialMapper.GetLogicalCell(item,3,4,0)),"movement preserves owned cell");
        f.Flip(); Check(f.FacingSide == FrameSide.Back,"flip");
        f.Front.TrySetConstruction(3,4,50);
        Check(f.Front.GetCell(3,4).Content == CellContentKind.Empty && f.Front.GetCell(3,4).Type == CombCellType.Drone,"construction rules preserved");
        Check(!f.Front.TrySetContent(3,4,CellContentKind.Egg),"incomplete cell rejects contents");
        Check(new FrameData(78,43).TotalCellCount == 6708,"legacy explicit dimensions retained");
#if UNITY_EDITOR
        UnityEngine.Debug.Log("Comb tests passed: " + checks);
#else
        Console.WriteLine("Comb tests passed: " + checks);
#endif
    }
}
