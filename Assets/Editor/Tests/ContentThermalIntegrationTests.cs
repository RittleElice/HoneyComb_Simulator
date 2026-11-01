using System;
using System.Linq;

public static class ContentThermalIntegrationTests
{
    private static void Check(bool ok,string text) { if(!ok) throw new Exception("Contents thermal: "+text); }
    private static void Near(double a,double b,string text) => Check(Math.Abs(a-b)<=2e-5*Math.Max(1,Math.Abs(b)),text+": "+a+" vs "+b);
    public static void Run(ThermalMaterials materials)
    {
        var original=CombBalance.Snapshot();
        try
        {
            CombBalance.TryApply(new CombBalanceParameters(),out _);
            var f=new FrameData();var item=new ItemData(9901,"contents frame",ItemKind.Frame,f);var state=item.ThermalState;
            f.Front.SetAllConstruction(100);f.Back.SetAllConstruction(100);state.Refresh(materials,20);
            var g=item.EquipmentGrid;int front=g.Index(1,0,1),back=g.Index(1,2,1),middle=g.Index(1,1,1);
            double baseC=state.HeatCapacityJPerK[front];float oldEnergy=state.EnergyJ[front];
            Check(f.Front.TryAddNectar(0,0,1,100,100),"nectar insert");state.Refresh(materials,20);
            Near(state.ContentHeatCapacityJPerK[front],.543,"water plus sugar capacity");
            Near(state.HeatCapacityJPerK[front],baseC+.543,"solver capacity includes contents");
            Check(state.EnergyJ[front]==oldEnergy,"existing stored energy preserved");
            Near(state.TemperatureC(front),oldEnergy/(baseC+.543),"temperature uses new capacity");
            Near(state.ContentHeatCapacityJPerK[back],0,"front/back independent");Near(state.ContentHeatCapacityJPerK[middle],0,"no central duplication");
            Check(f.Front.TryAddNectar(0,0,2,10,0),"second sugar source");state.Refresh(materials,20);Near(state.ContentHeatCapacityJPerK[front],.5555,"same-kind mass change invalidates");
            var cells=new[]{new ThermalExchange.Cell(state,front,"filled"),new ThermalExchange.Cell(state,middle,"wax")};
            var solver=new ThermalExchange(cells,new[]{new ThermalExchange.Face(0,1,1e-8,.01,.01)});
            state.RestoreEnergy(front,state.HeatCapacityJPerK[front]*10);state.RestoreEnergy(middle,state.HeatCapacityJPerK[middle]*30);
            solver.RefreshCoefficients();int builds=solver.CoefficientBuilds;
            double ca=state.HeatCapacityJPerK[front],cb=state.HeatCapacityJPerK[middle],ea=state.EnergyJ[front],eb=state.EnergyJ[middle];
            double q=60*solver.SumConductance(0)*(eb/cb-ea/ca);solver.Step();Near(state.EnergyJ[front],ea+q,"exchange uses contents capacity");
            f.Front.TryClearContent(0,0);state.Refresh(materials,20);solver.RefreshCoefficients();
            Near(state.HeatCapacityJPerK[front],baseC,"clear restores empty capacity");Check(solver.CoefficientBuilds==builds+1,"contents invalidate exchange cache");
            f.Back.TryAddPollen(0,0,100);f.Front.TrySetBrood(0,0,123);state.Refresh(materials,20);
            Near(state.ContentHeatCapacityJPerK[front],.3,"brood proxy");Near(state.ContentHeatCapacityJPerK[back],.18,"pollen mass");
            var settings=CombBalance.Snapshot();settings.BroodHeatCapacityJPerK=.6f;settings.PollenSpecificHeatJPerKgK=2000;CombBalance.TryApply(settings,out _);state.Refresh(materials,20);
            Near(state.ContentHeatCapacityJPerK[front],.6,"live brood tuning");Near(state.ContentHeatCapacityJPerK[back],.2,"live pollen tuning");
            f.Front.TrySetConstruction(0,0,50);state.Refresh(materials,20);Near(state.ContentHeatCapacityJPerK[front],0,"collapse removes contents");
            var box=new ItemData(9902,"box",ItemKind.BroodBox);Check(box.InteriorSlots.GetSlot(0).TryInsert(item),"place frame");
            var view=new HiveThermalView(box.SpatialGrid);int mapped=Enumerable.Range(0,box.SpatialGrid.CellCount).First(i=>ReferenceEquals(box.SpatialGrid.GetCell(i).SourceCell,g.GetCell(1,2,1)));
            Near(view.HeatCapacityJPerK(mapped),state.HeatCapacityJPerK[back],"assembled view reads owner capacity");
            box.InteriorSlots.GetSlot(0).Take();state.Refresh(materials,20);Near(state.ContentHeatCapacityJPerK[back],.2,"detached retains contents");
            f.Flip();state.Refresh(materials,20);Near(state.ContentHeatCapacityJPerK[back],.2,"flip retains physical face");
            var dense=new FrameData();var denseItem=new ItemData(9903,"dense",ItemKind.Frame,dense);
            foreach(var side in new[]{FrameSide.Front,FrameSide.Back})
            {
                var surf=dense.GetSurface(side);surf.SetAllConstruction(100);
                for(int y=0;y<40;y++)for(int x=0;x<84;x++)Check(surf.TryAddNectar(x,y,0,0,1),"dense insert");
            }
            denseItem.ThermalState.Refresh(materials,20);
            Near(denseItem.ThermalState.ContentHeatCapacityJPerK.Sum(v=>(double)v),6720*.00418,"all cells counted once");
            var world=new GameWorld(new WeatherSimulator(42,WeatherAssetLoader.LoadBaseline()));
            Check(world.Hand.Slot.TryInsert(denseItem),"put dense frame in saved world");world.PrepareThermal(materials);
            var snap=new WorldSaveSnapshot{World=world,UtcTicks=DateTime.UtcNow.Ticks,TargetMinute=world.CurrentGameMinute};
            var bytes=WorldSaveCodec.Encode(snap);var loaded=WorldSaveCodec.Decode(bytes);loaded.World.PrepareThermal(materials);
            Check(bytes.SequenceEqual(WorldSaveCodec.Encode(loaded)),"content and energy exact save roundtrip");
            Near(loaded.World.Hand.Item.ThermalState.ContentHeatCapacityJPerK.Sum(v=>(double)v),6720*.00418,"loaded contents rebuild capacity");
        }
        finally {CombBalance.TryApply(original,out _);}
        UnityEngine.Debug.Log("CONTENT THERMAL INTEGRATION PASS");
    }
}
