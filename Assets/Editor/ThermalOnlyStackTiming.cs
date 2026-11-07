using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using UnityEditor;

public static class ThermalOnlyStackTiming
{
    [MenuItem("Tools/HoneyComb/Three storey thermal only timing")]
    public static void Run()
    {
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
            ex.MeasurePerformance=true;
            string folder=Directory.GetParent(UnityEngine.Application.dataPath).FullName;
            using(var w=new StreamWriter(Path.Combine(folder,"ThermalOnlyThreeStoreyTiming.csv")))
            {
                w.WriteLine("minute,hives,cells,internal_faces,external_faces,prepare_heaters_and_call_ms,solver_overhead_ms,exchange_job_ms,total_ms");
                for(int minute=1;minute<=13;minute++)
                {
                    var watch=Stopwatch.StartNew();
                    world.Thermal.CalculateLegacyThermalBenchmark(minute,CancellationToken.None).GetAwaiter().GetResult();
                    watch.Stop();
                    double total=watch.Elapsed.TotalMilliseconds;
                    if(minute>3)w.WriteLine(FormattableString.Invariant($"{minute},{count},{ex.CellCount},{ex.FaceCount},{ex.BoundaryCount},{total-ex.LastTickMilliseconds:F6},{ex.LastTickMilliseconds-ex.LastJobMilliseconds:F6},{ex.LastJobMilliseconds:F6},{total:F6}"));
                    world.Weather.Advance(minute);
                }
            }
            UnityEngine.Debug.Log("Thermal baseline completed "+UnityEngine.SystemInfo.processorType);
        }
        finally{world.Thermal?.Dispose();}
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

