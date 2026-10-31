using System;

public static class CombThermalTests
{
    private static int checks;
    private static void Check(bool value, string message) { checks++; if (!value) throw new Exception("Comb thermal: " + message); }
    private static void Near(double value, double expected, string message) => Check(Math.Abs(value - expected) < 1e-6, message + ": " + value + " != " + expected);
#if UNITY_EDITOR
    [UnityEditor.MenuItem("Tools/HoneyComb/Run Comb Thermal Tests")]
#endif
    public static void RunAll()
    {
        checks = 0;
        var previous = CombBalance.Snapshot();
        try
        {
            Check(CombBalance.TryApply(new CombBalanceParameters(), out _), "defaults valid");
            Near(CombBalance.NectarVolumeMl(160,100),.2,"additive component volumes");
            Near(CombBalance.NectarVolumeMl(0,100),.1,"water density");
            Near(CombBalance.NectarVolumeMl(160,0),.1,"effective sugar density");
            Near(CombBalance.NectarVolumeMl(0,0),0,"empty volume");
            var badDensity=CombBalance.Snapshot(); badDensity.SugarEffectiveDensityGPerMl=0;
            Check(!CombBalance.TryApply(badDensity,out _),"zero sugar density rejected");
            var f = new FrameData(); var frame = new ItemData(1,"frame",ItemKind.Frame,f);
            var front = CombSpatialMapper.GetContentCell(frame,FrameSide.Front,0,0);
            var back = CombSpatialMapper.GetContentCell(frame,FrameSide.Back,0,0);
            var centre = frame.EquipmentGrid.GetCell(1,1,1);
            var s = f.Front;
            double baseHeat = front.HeatCapacityJPerK;
            Near(baseHeat, 1.47, "empty comb effective density and mm3 to m3");
            Near(centre.ContentHeatCapacityJPerK,0,"centre has no duplicate contents");
            s.TrySetConstruction(0,0,80);
            Check(s.TryAddNectar(0,0,0,0,100),"store water");
            Near(front.ContentHeatCapacityJPerK,.418,"mg to kg conversion");
            Near(front.HeatCapacityJPerK,baseHeat+.418,"contents added to substrate");
            Near(back.ContentHeatCapacityJPerK,0,"back independent");
            Check(s.TryAddNectar(0,0,1,100,0),"mix sugar");
            Near(front.ContentHeatCapacityJPerK,.543,"same-kind addition invalidates mass cache");
            Check(s.TryAddNectar(0,0,2,10,0),"second sugar source");
            Near(front.ContentHeatCapacityJPerK,.5555,"all forage contributions counted");
            s.TrySetCappingProgress(0,0,100); s.TrySetConstruction(0,0,51);
            Near(front.ContentHeatCapacityJPerK,.5555,"cap and 51 construction preserve heat mass");
            s.TrySetConstruction(0,0,50); Near(front.ContentHeatCapacityJPerK,0,"collapse invalidates cache");
            s.TrySetConstruction(0,0,80); Check(s.TryAddPollen(0,0,100),"pollen insert");
            Near(front.ContentHeatCapacityJPerK,.18,"pollen specific heat");
            s.TryClearContent(0,0); Check(s.TrySetBrood(0,0,789),"brood insert");
            Near(front.ContentHeatCapacityJPerK,.3,"brood proxy");
            s.TryClearContent(0,0); s.TrySetStoredContent(0,0,CellContentKind.Honey,.5f);
            Near(front.ContentHeatCapacityJPerK,0,"legacy fill not assigned fake mass");
            Check(front.ContentThermalMass.UnquantifiedCellCount == 1,"unknown mass disclosed");
            s.TryClearContent(0,0); Check(front.ContentThermalMass.UnquantifiedCellCount == 0,"unknown count clears");
            Check(s.TryAddNectar(0,0,0,0,300),"water near capacity");
            int revision=s.Revision; long bytes=s.AllocatedDataBytes;
            Check(!s.TryAddNectar(0,0,0,0,1) && s.Revision==revision && s.GetWaterMassMg(0,0)==300 && s.AllocatedDataBytes==bytes,"overflow is atomic");
            Check(s.GetCapacityFillRatio(0,0) <= 1 && s.GetCapacityFillRatio(0,0) > .99,"physical fill ratio");
            var settings=CombBalance.Snapshot(); settings.CellLiquidCapacityMl=.1f; settings.WaterSpecificHeatJPerKgK=2000;
            Check(CombBalance.TryApply(settings,out _),"live tuning");
            Near(front.ContentHeatCapacityJPerK,.6,"tuning updates cached mass heat immediately");
            Check(s.GetCapacityFillRatio(0,0)>2 && s.GetWaterMassMg(0,0)==300,"capacity lowering preserves stored mass");
            Check(!s.TryAddNectar(0,0,0,0,1),"overfull blocks new material");
            settings.CellLiquidCapacityMl=.5f; Check(CombBalance.TryApply(settings,out _),"increase capacity");
            Check(s.TryAddNectar(0,0,0,0,1),"increase re-enables deposit");
            settings.WaterSpecificHeatJPerKgK=9999; Near(front.ContentHeatCapacityJPerK,.602,"applied config is defensive copy");
            settings.CellPollenCapacityMg=-1; int version=CombBalance.Revision;
            Check(!CombBalance.TryApply(settings,out _) && CombBalance.Revision==version,"invalid settings atomic");
            settings=CombBalance.Snapshot(); settings.SugarSpecificHeatJPerKgK=float.NaN;
            Check(!CombBalance.TryApply(settings,out _),"NaN setting rejected");
            settings=CombBalance.Snapshot(); settings.WaterDensityGPerMl=0;
            Check(!CombBalance.TryApply(settings,out _),"zero water density rejected");
            CombBalance.TryApply(new CombBalanceParameters(),out _);
            s.TryClearContent(0,0); Check(s.TryAddPollen(0,0,200) && !s.TryAddPollen(0,0,.1f),"pollen capacity");
            s.TryClearContent(0,0); s.TryAddNectar(0,0,0,171.2f,42.8f); s.TryAddNectar(0,0,1,171.2f,42.8f);
            Check(s.GetCapacityFillRatio(0,0)>.99 && s.GetCapacityFillRatio(0,0)<=1,"80 percent nectar capacity");
            Check(!s.TryAddNectar(0,0,2,1,0),"mixed-source capacity shared");
            var box = new ItemData(2,"box",ItemKind.BroodBox); var grid=box.SpatialGrid;
            Check(box.InteriorSlots.GetSlot(0).TryInsert(frame),"place frame");
            var local=frame.EquipmentGrid;
            int gridIndex=-1;
            for (int i=0;i<grid.CellCount;i++) if (ReferenceEquals(grid.GetCell(i).SourceCell,front)) { gridIndex=i; break; }
            Check(gridIndex>=0,"composed source identity");
            Near(grid.GetHeatCapacityJPerK(gridIndex),front.HeatCapacityJPerK,"composed reads current source heat");
            s.TryClearContent(0,0); Near(grid.GetHeatCapacityJPerK(gridIndex),baseHeat,"clear reaches composed grid without relocation");
            s.TryAddNectar(0,0,0,0,100); f.Back.TrySetConstruction(0,0,80); f.Back.TryAddPollen(0,0,100);
            f.Flip(); Near(front.ContentHeatCapacityJPerK,.418,"display flip does not swap physical data sides");
            Near(back.ContentHeatCapacityJPerK,.18,"back mapped once");
            double total=0;
            for(int z=0;z<local.SizeZ;z++) for(int y=0;y<local.SizeY;y++) for(int x=0;x<local.SizeX;x++) total+=local.GetCell(x,y,z)?.ContentHeatCapacityJPerK ?? 0;
            Near(total,.598,"all local regions conserve contents heat exactly once");
            box.InteriorSlots.GetSlot(0).Take();
            Near(grid.GetHeatCapacityJPerK(gridIndex),CombBalance.MaterialHeatCapacityJPerK(HiveMaterial.Air,grid.Layout.VolumeMm3(gridIndex)),"removed frame replaced by air heat capacity");
            Near(front.ContentHeatCapacityJPerK,.418,"detached frame retains thermal contents");
            Check(box.InteriorSlots.GetSlot(2).TryInsert(frame),"reinsert different slot");
            var wood=local.GetCell(0,0,0); Near(wood.ContentHeatCapacityJPerK,0,"wood has no comb contents");
            Near(wood.HeatCapacityJPerK,wood.VolumeMm3*1e-9*500*1600,"wood baseline units");
            Check(s.TrySetType(0,0,CombCellType.Drone),"legacy tag retained");
            Check(!s.TryAddNectar(0,0,0,0,201),"drone tag gets no extra capacity");
            // Every cell in each 7x5 group lands once in its own side/region.
            var dense=new FrameData(); var denseItem=new ItemData(3,"dense",ItemKind.Frame,dense);
            foreach(var side in new[]{FrameSide.Front,FrameSide.Back})
            {
                var surface=dense.GetSurface(side); surface.SetAllConstruction(100);
                for(int y=0;y<40;y++) for(int x=0;x<84;x++) Check(surface.TryAddNectar(x,y,0,0,1),"populate all cells");
                for(int z=0;z<8;z++) for(int x=0;x<12;x++)
                    Near(CombSpatialMapper.GetContentCell(denseItem,side,x*7,z*5).ContentHeatCapacityJPerK,35*.00418,"region conserves 35 cells");
            }
            long cacheBytes=dense.Front.AllocatedDataBytes;
            for(int k=0;k<100;k++) dense.Front.GetThermalRegion(0,0);
            Check(dense.Front.AllocatedDataBytes==cacheBytes,"queries reuse cache");
        }
        finally { CombBalance.TryApply(previous,out _); }
#if UNITY_EDITOR
        UnityEngine.Debug.Log("Comb thermal tests passed: " + checks);
#else
        Console.WriteLine("Comb thermal tests passed: " + checks);
#endif
    }
}
