using System.Linq;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using UnityEditor;

public static class LiveClimateStackTiming
{
    [MenuItem("Tools/HoneyComb/Live climate full tick timing")]
    public static void Run()
    {
        VerifyContinuation();
        const int count=100;
        var world=new GameWorld(new WeatherSimulator(42,WeatherAssetLoader.LoadBaseline()));
        try
        {
            for(int i=0;i<world.Inventory.Capacity;i++)world.Inventory.GetSlot(i).Take();
            int height=world.Apiary.Grid.Height, id=10000;
            while(world.Apiary.Grid.UnlockedWidth*height<count)
                if(!world.Apiary.Grid.TryUnlockNextColumn())throw new Exception("Grid expansion failed");
            for(int h=0;h<count;h++)
            {
                int x=h/height,y=h%height;
                if(h>0)
                {
                    var hive=new HiveData(h+1,x,y);
                    if(!world.Apiary.AddHive(hive,new ItemData(id++,"brood",ItemKind.BroodBox,hive)))throw new Exception("Hive insertion failed");
                }
                var root=world.Apiary.GetItemSlot(x,y);
                Populate(root,ref id);
            }
            world.PrepareThermal(ThermalMaterials.Load());
            world.Thermal.PrepareExchange();
            var ex=world.Thermal.Exchange;
            if(ex.CellCount!=count*10368||ex.FaceCount!=count*29468)throw new Exception("Fixture size mismatch: "+ex.CellCount+"/"+ex.FaceCount);
            var scheduler=GameTimeRuntime.CreateWorldScheduler(world);
            string folder=Directory.GetParent(UnityEngine.Application.dataPath).FullName;
            using(var w=new StreamWriter(Path.Combine(folder,"LiveClimateFullTick.csv")))
            {
                w.AutoFlush=true;
                w.WriteLine("minute,hives,cells,faces,total_ms");
                for(int minute=1;minute<=13;minute++)
                {
                    var watch=Stopwatch.StartNew();
                    scheduler.RunToAsync(minute,()=>System.Threading.Tasks.Task.CompletedTask,
                        ()=>System.Threading.Tasks.Task.CompletedTask,CancellationToken.None).GetAwaiter().GetResult();
                    watch.Stop();
                    if(world.CurrentGameMinute!=minute || world.Weather.Minute!=minute || scheduler.Completed!=1 || world.Weather.Nectar==null)
                        throw new Exception("Minute participants diverged");
                    w.WriteLine(FormattableString.Invariant($"{minute},{count},{ex.CellCount},{ex.FaceCount},{watch.Elapsed.TotalMilliseconds:F4}"));
                }
            }
            var scratch=Path.Combine(Path.GetTempPath(),"HoneyCombClimateTest-"+Guid.NewGuid().ToString("N"));
            var store=new WorldSaveStore(scratch);
            double captureMs=0,storeMs=0;int bytes=0;
            try
            {
                scheduler.CheckpointAsync=()=>{
                    var timer=Stopwatch.StartNew();
                    var data=WorldSaveCodec.Encode(new WorldSaveSnapshot{World=world,TargetMinute=world.CurrentGameMinute,UtcTicks=DateTime.UtcNow.Ticks});
                    captureMs=timer.Elapsed.TotalMilliseconds;bytes=data.Length;timer.Restart();
                    store.Save(data);storeMs=timer.Elapsed.TotalMilliseconds;
                    return System.Threading.Tasks.Task.CompletedTask;
                };
                var full=Stopwatch.StartNew();
                scheduler.RunToAsync(14,()=>System.Threading.Tasks.Task.CompletedTask,()=>System.Threading.Tasks.Task.CompletedTask,CancellationToken.None).GetAwaiter().GetResult();
                full.Stop();
                File.WriteAllText(Path.Combine(folder,"LiveClimateCheckpoint.csv"),"minute,total_ms,capture_ms,write_and_validation_ms,bytes\n"+
                    FormattableString.Invariant($"14,{full.Elapsed.TotalMilliseconds:F4},{captureMs:F4},{storeMs:F4},{bytes}\n"));
                var restored=store.Load(out var recovered).World;
                if(recovered||restored.CurrentGameMinute!=14||restored.Weather.Minute!=14)throw new Exception("Checkpoint restore failed");
            }
            finally
            {
                foreach(var name in new[]{"world-a.save","world-b.save","world-a.save.tmp","world-b.save.tmp"})
                    File.Delete(Path.Combine(scratch,name));
                if(Directory.Exists(scratch))Directory.Delete(scratch);
            }
            UnityEngine.Debug.Log("Live climate benchmark passed: "+UnityEngine.SystemInfo.processorType);
        }
        finally{world.Thermal?.Dispose();}
    }
    static void VerifyContinuation()
    {
        var baseline=WeatherAssetLoader.LoadBaseline();
        var world=new GameWorld(new WeatherSimulator(42,baseline));
        GameWorld restored=null;
        try
        {
            var root=world.Apiary.GetItemSlot(0,0);int id=10000;Populate(root,ref id);
            var materials=ThermalMaterials.Load();
            world.PrepareThermal(materials);
            var scheduler=GameTimeRuntime.CreateWorldScheduler(world);
            void Tick(SimulationScheduler s,int minute)=>s.RunToAsync(minute,()=>System.Threading.Tasks.Task.CompletedTask,
                ()=>System.Threading.Tasks.Task.CompletedTask,CancellationToken.None).GetAwaiter().GetResult();
            Tick(scheduler,1);
            FrameSurface Surface(GameWorld w)
            {
                var brood=ThermalWorld.Items(w).First(i=>i.Content is HiveData);
                return brood.InteriorSlots.GetSlot(0).Item.GetContent<FrameData>().Front;
            }
            float water=Surface(world).GetWaterMassMg(0,0);
            if(!(water<100&&water>0))throw new Exception("No nectar evaporation in live frame: "+water);
            var bytes=WorldSaveCodec.Encode(new WorldSaveSnapshot{World=world,TargetMinute=1,UtcTicks=DateTime.UtcNow.Ticks});
            restored=WorldSaveCodec.Decode(bytes).World;
            if(Surface(restored).GetWaterMassMg(0,0)!=water)throw new Exception("Saved frame water changed");
            restored.Weather.BindBaseline(baseline);restored.PrepareThermal(materials);
            var resumed=GameTimeRuntime.CreateWorldScheduler(restored);
            Tick(scheduler,2);Tick(resumed,2);
            if(Math.Abs(Surface(restored).GetWaterMassMg(0,0)-Surface(world).GetWaterMassMg(0,0))>.001)
                throw new Exception("Nectar continuation diverged");
            var a=restored.Thermal.Exchange;var b=world.Thermal.Exchange;
            for(int i=0;i<a.CellCount;i++){
                var x=a.GetCell(i);var y=b.GetCell(i);
                if(Math.Abs(x.EnergyJ/x.HeatCapacityJPerK-y.EnergyJ/y.HeatCapacityJPerK)>.001)
                    throw new Exception("Restored temperature diverged");
            }
            var broodBox=ThermalWorld.Items(restored).First(i=>i.Content is HiveData);
            var removed=broodBox.InteriorSlots.GetSlot(0).Take();
            if(!restored.Hand.Slot.TryInsertValue(removed))throw new Exception("Frame move failed");
            Tick(resumed,3);
            if(restored.CurrentGameMinute!=3)throw new Exception("Topology change failed");
            UnityEngine.Debug.Log("Live climate evaporation, save continuation and frame move passed");
        }
        finally{world.Thermal?.Dispose();restored?.Thermal?.Dispose();}
    }
    static void Populate(ItemSlot root,ref int id)
    {
        var box=root.Item;
            for(int level=0;level<3;level++)
            {
                if(level>0)
                {
                    box=new ItemData(id++,"super",ItemKind.Super);
                    var upper=new ItemSlot();
                    if(!upper.TryInsert(box)||!root.TryCombineFrom(upper))throw new Exception("Cannot stack super");
                }
                for(int slot=0;slot<10;slot++)
                {
                    var data=new FrameData();
                    var frame=new ItemData(id++,"frame",ItemKind.Frame,data);
                    data.Front.SetAllConstruction(100);
                    data.Back.SetAllConstruction(100);
                    if(!data.Front.TryAddNectar(0,0,0,40,100)||!box.InteriorSlots.GetSlot(slot).TryInsert(frame))
                        throw new Exception("Cannot populate frame "+level+"/"+slot);
                }
            }
            var lid=new ItemSlot();
            if(!lid.TryInsert(new ItemData(id++,"lid",ItemKind.Lid))||!root.TryCombineFrom(lid))throw new Exception("Cannot fit lid");
    }
}



