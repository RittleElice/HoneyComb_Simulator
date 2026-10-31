using System;
using System.Collections.Generic;

public static class EquipmentCellDataTests
{
    private static int checks;
    private static void Check(bool condition, string message)
    { checks++; if (!condition) throw new Exception("Equipment/cell data: " + message); }
#if UNITY_EDITOR
    [UnityEditor.MenuItem("Tools/HoneyComb/Run Equipment Cell Data Tests")]
#endif
    public static void RunAll()
    {
        checks = 0;
        foreach (ItemKind kind in Enum.GetValues(typeof(ItemKind)))
        {
            var item = new ItemData(1, "test", kind);
            Check(item.Aging == 0 && item.TrySetAging(0.6f) && item.Aging == 0.6f, "aging on all equipment");
            foreach (float value in new[] { -1f, 1.001f, float.NaN, float.PositiveInfinity, float.NegativeInfinity })
                Check(!item.TrySetAging(value) && item.Aging == 0.6f, "atomic invalid aging");
            Check(item.AgingAffectsCombDecay == (kind == ItemKind.Frame), "comb aging hook");
            Check(item.AgingAffectsInsulation == (kind == ItemKind.BroodBox || kind == ItemKind.Super || kind == ItemKind.Lid), "insulation aging hook");
        }
        var f = new FrameData(); var s = f.Front;
        Check(f.TotalCellCount == 6720 && s.AllocatedDataBytes == 3360 * 8, "fixed default compact arrays");
        Check(!s.TryAddNectar(0,0,0,10,20) && !s.TrySetBrood(0,0,123), "unfinished cannot hold material");
        s.TrySetConstruction(0,0,79); Check(!s.GetCell(0,0).IsComplete, "79 not complete");
        s.TrySetConstruction(0,0,80); Check(s.GetCell(0,0).IsComplete, "80 completes");
        Check(s.TryAddNectar(0,0,10,12,20) && s.TryAddNectar(0,0,20,18,30) && s.TryAddNectar(0,0,10,2,3), "mixed sources");
        Check(s.GetSugarMassMg(0,0,10) == 14 && s.GetTotalSugarMassMg(0,0) == 32 && s.GetWaterMassMg(0,0) == 53, "mass conservation");
        var sugars = new List<KeyValuePair<int,float>>(); s.CopyNectarSugars(0,0,sugars);
        Check(sugars.Count == 2, "merged duplicate source");
        Check(!s.TryAddPollen(0,0,5) && !s.TrySetBrood(0,0,3), "one content per cell");
        Check(!s.TrySetFillRatio(0,0,0.5f) && !s.TrySetStoredContent(0,0,CellContentKind.Pollen,0.5f), "legacy ratio cannot replace masses");
        int revision = s.Revision;
        foreach (float bad in new[] { -1f, float.NaN, float.PositiveInfinity, float.NegativeInfinity })
            Check(!s.TryAddNectar(0,0,10,bad,0) && !s.TryAddNectar(0,0,10,0,bad), "invalid mass rejected");
        Check(s.Revision == revision && s.GetTotalSugarMassMg(0,0) == 32, "failed write atomic");
        Check(s.TrySetCappingProgress(0,0,99) && !s.GetCell(0,0).IsCapped, "partial capping");
        revision = s.Revision;
        Check(!s.TrySetCappingProgress(0,0,101) && s.Revision == revision, "invalid cap atomic");
        Check(s.TrySetCappingProgress(0,0,100) && s.GetCell(0,0).IsCapped, "complete cap derived");
        s.TrySetConstruction(0,0,51); Check(s.GetCell(0,0).IsComplete && s.GetTotalSugarMassMg(0,0) == 32, "51 retains complete/material");
        s.TrySetType(0,0,CombCellType.Drone);
        var losses = new List<CellContentLoss>();
        s.ContentLost += (surface, loss) => {
            Check(surface.GetCell(loss.Column,loss.Row).Content == CellContentKind.Empty, "loss event sees committed state");
            losses.Add(loss);
        };
        s.TrySetConstruction(0,0,50);
        Check(losses.Count == 1 && losses[0].SugarMg == 32 && losses[0].WaterMg == 53 && losses[0].Reason == CellRemovalReason.ConstructionCollapse, "collapse loss accounting");
        Check(!s.HasQuantifiedContent(0,0) && !s.GetCell(0,0).IsComplete && s.GetCell(0,0).CappingProgress == 0 && s.GetCell(0,0).Type == CombCellType.Drone, "collapse clears only content");
        s.TrySetConstruction(0,0,79); Check(!s.GetCell(0,0).IsComplete, "79 remains incomplete after collapse");
        s.TrySetConstruction(0,0,80);
        Check(s.TrySetBrood(0,0,long.MaxValue) && s.GetBeeId(0,0) == long.MaxValue, "64 bit bee reference");
        Check(!s.TrySetBrood(0,0,456) && s.TrySetBrood(0,0,long.MaxValue), "cannot silently replace bee");
        s.TryClearContent(0,0); Check(losses[1].BeeId == long.MaxValue, "bee removal is reported, not simulated");
        Check(s.TryAddPollen(0,0,2.5f) && s.TryAddPollen(0,0,1.5f) && s.GetPollenMassMg(0,0) == 4, "pollen mass");
        s.SetAllConstruction(50); Check(losses[2].PollenMg == 4, "bulk loss accounting");
        int count = losses.Count; s.SetAllConstruction(50); Check(losses.Count == count, "no duplicate losses");
        s.TrySetConstruction(0,0,100);
        long bytes = s.AllocatedDataBytes;
        for (int i = 0; i < 1000; i++) { s.TryAddNectar(0,0,i,1,1); s.TryClearContent(0,0); }
        Check(s.AllocatedDataBytes == bytes, "sugar slots reused without growth");
        s.TryAddNectar(0,0,0,0,1); revision = s.Revision;
        Check(!s.TryAddNectar(0,0,0,float.MaxValue,2) && s.Revision == revision && s.GetWaterMassMg(0,0) == 1, "overflow cannot partially add water");
        s.TryClearContent(0,0); s.TryAddNectar(0,0,0,0,1);
        Check(s.HasQuantifiedContent(0,0) && s.GetTotalSugarMassMg(0,0) == 0, "water-only nectar");
        var frame = new ItemData(123,"frame",ItemKind.Frame,f); frame.TrySetAging(0.7f);
        var box = new ItemData(124,"box",ItemKind.BroodBox); var grid = box.SpatialGrid;
        var local = frame.EquipmentGrid;
        Check(box.InteriorSlots.GetSlot(0).TryInsert(frame), "insert frame");
        Check(ReferenceEquals(CombSpatialMapper.GetLogicalCell(frame,0,0,0).Frame,f), "spatial link");
        Check(ReferenceEquals(box.InteriorSlots.GetSlot(0).Take(),frame), "remove same frame");
        f.Flip(); Check(frame.Aging == 0.7f && s.GetWaterMassMg(0,0) == 1 && f.Back.GetWaterMassMg(0,0) == 0 && ReferenceEquals(local,frame.EquipmentGrid), "move and flip preserve identity/state");
        s.TryClearContent(0,0); s.TrySetStoredContent(0,0,CellContentKind.Honey,0.3f);
        Check(s.GetCell(0,0).Content == CellContentKind.Nectar && !s.HasQuantifiedContent(0,0) && !s.TryAddNectar(0,0,1,1,1), "legacy display data never invents mass");
        Check(!s.TrySetContent(0,0,CellContentKind.Egg), "stage requires migration decision");
#if UNITY_EDITOR
        UnityEngine.Debug.Log("Equipment/cell tests passed: " + checks);
#else
        Console.WriteLine("Equipment/cell tests passed: " + checks);
        long start = GC.GetAllocatedBytesForCurrentThread();
        var large = new FrameSurface(15000000,1);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - start;
        Check(large.AllocatedDataBytes == 120000000, "15 million base bytes");
        Console.WriteLine("15,000,000 empty cells: allocated " + allocated + " bytes; payload " + large.AllocatedDataBytes);
        GC.KeepAlive(large);
#endif
    }
}
