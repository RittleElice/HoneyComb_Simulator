using System;
using System.Linq;
using System.Threading;

public static class PersistentThermalTests
{
    private static void Check(bool value,string message) { if(!value) throw new Exception("Persistent thermal: "+message); }
    public static void Run(ThermalMaterials materials)
    {
        var world=new GameWorld(new WeatherSimulator(42,WeatherAssetLoader.LoadBaseline()));
        var box=world.Apiary.GetItemSlot(0,0).Item;
        var frame=new ItemData(9980,"persistent frame",ItemKind.Frame,new FrameData());
        frame.GetContent<FrameData>().Front.SetAllConstruction(100);
        Check(box.InteriorSlots.GetSlot(0).TryInsert(frame),"insert frame");
        world.PrepareThermal(materials);world.Thermal.PrepareExchange();
        var exchange=world.Thermal.Exchange;
        Check(!exchange.HasBuffers,"lazy allocation");
        for(int i=0;i<exchange.CellCount;i++)
        {
            var c=exchange.GetCell(i);c.Owner.RestoreEnergy(c.Index,c.Owner.HeatCapacityJPerK[c.Index]*(5+i%41));
        }
        exchange.Step(60,18);
        var before=Enumerable.Range(0,exchange.CellCount).Select(i=>exchange.GetCell(i).Owner.EnergyJ[exchange.GetCell(i).Index]).ToArray();
        exchange.Step(60,18);
        var expected=Enumerable.Range(0,exchange.CellCount).Select(i=>exchange.GetCell(i).Owner.EnergyJ[exchange.GetCell(i).Index]).ToArray();
        Check(exchange.BufferAllocations==1 && exchange.CoefficientUploads==1,"steady steps reuse all buffers and coefficients");
        long bytes=exchange.BufferBytes;Check(bytes>0,"memory accounted");
        exchange.Dispose();exchange.Dispose();Check(!exchange.HasBuffers && exchange.BufferBytes==0,"idempotent release");
        for(int i=0;i<exchange.CellCount;i++){var c=exchange.GetCell(i);c.Owner.RestoreEnergy(c.Index,before[i]);}
        exchange.Step(60,18);
        for(int i=0;i<exchange.CellCount;i++) Check(exchange.GetCell(i).Owner.EnergyJ[exchange.GetCell(i).Index]==expected[i],"reused vs freshly allocated bit exact");
        Check(exchange.BufferAllocations==2 && exchange.BufferBytes==bytes,"recreate after release");
        int uploads=exchange.CoefficientUploads;
        Check(frame.GetContent<FrameData>().Front.TryAddNectar(0,0,1,100,100),"contents update");
        world.Thermal.Refresh();exchange.Step(60,18);
        Check(exchange.CoefficientUploads==uploads+1 && exchange.BufferAllocations==2,"changed contents upload coefficients without reallocating");
        var snap=new WorldSaveSnapshot {World=world,UtcTicks=DateTime.UtcNow.Ticks,TargetMinute=world.CurrentGameMinute};
        var encoded=WorldSaveCodec.Encode(snap);exchange.Dispose();
        Check(encoded.SequenceEqual(WorldSaveCodec.Encode(snap)),"native buffers absent from saves");
        exchange.Step(60,18);
        var slot=new ItemSlot();slot.TryInsert(new ItemData(9981,"super",ItemKind.Super));
        Check(world.Apiary.GetItemSlot(0,0).TryCombineFrom(slot),"stack super");
        slot.TryInsert(new ItemData(9982,"lid",ItemKind.Lid));Check(world.Apiary.GetItemSlot(0,0).TryCombineFrom(slot),"stack lid");
        var grids=HiveSpatialGridDebugWindow.PlacementGrids(world.Apiary.GetItemSlot(0,0).Value);
        Check(grids.Count==3 && grids[0].Box==box && grids[1].Box.Kind==ItemKind.Super && grids[2].Box.Kind==ItemKind.Lid,"all placement parts in stack order");
        Check(Enumerable.Range(0,grids[0].CellCount).Any(i=>grids[0].GetCell(i).Equipment==frame),"inserted equipment present in displayed grid");
        world.Thermal.Refresh();world.Thermal.PrepareExchange();
        Check(!exchange.HasBuffers && !ReferenceEquals(exchange,world.Thermal.Exchange),"topology replacement frees old buffers");
        world.Thermal.CalculateMinuteAsync(1,CancellationToken.None).GetAwaiter().GetResult();
        foreach(var grid in grids)
        {
            var monitor=new HiveTemperatureMonitor();
            Check(monitor.Capture(grid,1,18,out var reason),"every part captures: "+reason);
            Check(monitor.Temperatures.Length==grid.CellCount,"complete monitor data");
        }
        world.Thermal.Exchange.Dispose();
        Check(HiveSpatialGridDebugWindow.PlacementGrids(null).Count==0,"empty placement");
        Check(HiveSpatialGridDebugWindow.PlacementGrids(box).Count==1,"single placement");
        UnityEngine.Debug.Log("PERSISTENT THERMAL + ALL PLACEMENT PARTS PASS");
    }
}
