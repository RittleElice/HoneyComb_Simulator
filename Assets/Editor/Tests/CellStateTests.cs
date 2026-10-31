using System;
public static class CellStateTests
{
    private static int checks;
    private static void Check(bool value, string message)
    { checks++; if (!value) throw new Exception("Cell state: " + message); }
#if UNITY_EDITOR
    [UnityEditor.MenuItem("Tools/HoneyComb/Run Cell State Tests")]
#endif
    public static void RunAll()
    {
        checks = 0;
        var s = new FrameSurface(84,40);
        Check(!s.TrySetCapped(0,0,true), "unfinished cannot be capped");
        Check(!s.TrySetStoredContent(0,0,CellContentKind.Honey,0.5f), "unfinished cannot store");
        Check(s.Revision == 0, "rejections preserve revision");
        s.TrySetConstruction(0,0,100);
        foreach (var kind in new[] {CellContentKind.Honey,CellContentKind.Pollen,CellContentKind.Nectar})
        {
            Check(s.TrySetStoredContent(0,0,kind,0.35f),"assign amount");
            Check(s.GetCell(0,0).FillRatio == 0.35f,"stored amount");
            Check(s.TrySetCapped(0,0,true),"cap nonempty");
            int revision = s.Revision;
            s.TrySetContent(0,0,kind); s.TrySetStoredContent(0,0,kind,0.35f);
            s.TrySetConstruction(0,0,100); s.TrySetCapped(0,0,true);
            Check(s.Revision == revision && s.GetCell(0,0).IsCapped,"same state preserves cap and revision");
            foreach (float invalid in new[] {-1f,1.01f,float.NaN,float.PositiveInfinity,float.NegativeInfinity})
            {
                Check(!s.TrySetFillRatio(0,0,invalid),"invalid ratio rejected");
                Check(!s.TrySetStoredContent(0,0,CellContentKind.Nectar,invalid),"atomic invalid assignment rejected");
                Check(s.Revision == revision && s.GetCell(0,0).Content == (kind == CellContentKind.Honey ? CellContentKind.Nectar : kind) && s.GetCell(0,0).FillRatio == 0.35f && s.GetCell(0,0).IsCapped,"no partial write");
            }
            s.TrySetFillRatio(0,0,0);
            Check(s.GetCell(0,0).Content == CellContentKind.Empty && !s.GetCell(0,0).IsCapped,"drained cell clears cap");
        }
        foreach (var kind in new[] {CellContentKind.Egg,CellContentKind.Larva,CellContentKind.Pupa})
        {
            Check(!s.TrySetContent(0,0,kind),"legacy brood requires explicit BeeId migration");
            Check(s.GetCell(0,0).FillRatio == 0 && !s.TrySetFillRatio(0,0,0.5f),"brood does not store fill or development");
        }
        s.TrySetStoredContent(0,0,CellContentKind.Honey,0.7f); s.TrySetCapped(0,0,true);
        s.TrySetType(0,0,CombCellType.Drone);
        Check(s.GetCell(0,0).FillRatio == 0.7f && s.GetCell(0,0).IsCapped,"type preserves stored state");
        s.TrySetContent(0,0,CellContentKind.Pollen);
        Check(s.GetCell(0,0).FillRatio == 1 && !s.GetCell(0,0).IsCapped,"legacy replacement defaults full uncapped");
        s.TrySetCapped(0,0,true); s.SetAllConstruction(50);
        Check(s.GetCell(0,0).Content == CellContentKind.Empty && s.GetCell(0,0).FillRatio == 0 && !s.GetCell(0,0).IsCapped,"construction clears all content state");
        int rev = s.Revision; s.SetAllConstruction(50);
        Check(s.Revision == rev,"bulk no-op does not dirty");
        Check(!s.TrySetContent(0,0,(CellContentKind)999),"invalid enum rejected");
        Check(!s.TrySetFillRatio(-1,0,0.5f) && !s.TrySetCapped(84,0,true) && !s.TrySetStoredContent(0,40,CellContentKind.Honey,0.5f),"bounds rejected");
        Check(new CellData(100,CellContentKind.Honey).FillRatio == 1,"legacy constructor defaults full");
        Check(new CellData(79,CellContentKind.Honey).FillRatio == 0,"unfinished constructor empty");
        var f = new FrameData(); var item = new ItemData(987,"Frame",ItemKind.Frame,f);
        f.Front.TrySetConstruction(4,9,100); f.Front.TrySetStoredContent(4,9,CellContentKind.Honey,0.6f); f.Front.TrySetCapped(4,9,true);
        var box = new ItemData(988,"Box",ItemKind.BroodBox);
        Check(box.InteriorSlots.GetSlot(0).TryInsert(item),"insert");
        Check(ReferenceEquals(box.InteriorSlots.GetSlot(0).Take(),item),"remove same frame");
        f.Flip();
        Check(f.Back.GetCell(4,9).FillRatio == 0 && !f.Back.GetCell(4,9).IsCapped,"independent sides");
        Check(f.Front.GetCell(4,9).FillRatio == 0.6f && f.Front.GetCell(4,9).IsCapped,"movement and flip preserve state");
#if UNITY_EDITOR
        UnityEngine.Debug.Log("Cell state tests passed: " + checks);
#else
        Console.WriteLine("Cell state tests passed: " + checks);
#endif
    }
}
