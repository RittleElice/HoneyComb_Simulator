using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEditor;
using System.Threading.Tasks;
using Contact = HiveClimateKernels.Contact;
using Link = HiveClimateKernels.Face;

using Model = HiveClimate.Model;
public static class MoisturePressureExperiment
{
    [MenuItem("Tools/HoneyComb/Moisture pressure benchmark")]
    public static void Run() => RunFixture(false);

    [MenuItem("Tools/HoneyComb/Moisture substep comparison")]
    public static void Compare() => RunFixture(true);
    public static void VerifyCorrection() { Compare(); Run(); }

    static void CompareCells(ThermalExchange exchange, Dictionary<EquipmentThermalState,ItemData> owners,
        ThermalMaterials materials, string directory, bool warmGradient=false)
    {
        var steps = new[] {1, 6, 60};
        var models = steps.Select(_ => new Model(exchange, owners, materials,warmGradient)).ToArray();
        if (models.Any(m => !m.SameInitialState(models[0]))) throw new Exception("Initial state mismatch");
        var initial = models[0].Audit();
        using (var writer = new StreamWriter(Path.Combine(directory, warmGradient?"PyramidWarm.csv":"PyramidCold.csv")))
        {
            writer.WriteLine("minute,row,label,steps,temp_C,RH_percent");
            for (int minute = 0; minute <= 60; minute++)
            {
                for (int k = 0; k < models.Length; k++)
                {
                    var model = models[k];
                    if (minute > 0) model.Minute(steps[k]);
                    var totals = model.Audit();
                    if (Math.Abs(totals.m-initial.m)>1e-10 || Math.Abs(totals.e-initial.e)>1e-6)
                        throw new Exception("Conservation failure at minute "+minute);
                    for (int row = 0; row < exchange.CellCount; row++)
                    {
                        var label = exchange.GetCell(row).Label.Replace("\"", "\"\"");
                        writer.WriteLine(FormattableString.Invariant($"{minute},{row},\"{label}\",{steps[k]},{model.Temperature(row):R},{model.RelativeHumidity(row):R}"));
                    }
                }
            }
        }
    }

    [MenuItem("Tools/HoneyComb/Three storey 100 hive phase timings")]
    public static void ProfileStacks() => ProfileStacks(true);
    public static void VerifyOptimizations() { Compare(); ProfileStacks(false); ProfileStacks(true); }
    static void ProfileStacks(bool parallel)
    {
        var world=new GameWorld(new WeatherSimulator(42,WeatherAssetLoader.LoadBaseline()));
        try
        {
            for(int i=0;i<world.Inventory.Capacity;i++)world.Inventory.GetSlot(i).Take();
            var root=world.Apiary.GetItemSlot(0,0);
            var box=root.Item;
            int id=10000;
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
            var materials=ThermalMaterials.Load();
            world.PrepareThermal(materials);world.Thermal.PrepareExchange();
            var ex=world.Thermal.Exchange;
            var owners=ThermalWorld.Items(world).Where(x=>x.EquipmentGrid!=null).Distinct().ToDictionary(x=>x.ThermalState);
            var models=new Model[100];
            for(int i=0;i<models.Length;i++)models[i]=new Model(ex,owners,materials);
            var initial=models[0].Audit();
            var path=Path.Combine(Directory.GetParent(UnityEngine.Application.dataPath).FullName,parallel?"MoistureStackOptimizedParallel.csv":"MoistureStackOptimizedSerial.csv");
            using(var w=new StreamWriter(path))
            {
                w.WriteLine(parallel?"sample,hives,cells_per_hive,faces_per_hive,total_ms":"sample,hives,cells_per_hive,faces_per_hive,state_and_evaporation_ms,pressure_ms,moisture_exchange_ms,heat_exchange_ms,commit_and_refresh_ms,total_ms");
                for(int sample=-2;sample<=10;sample++)
                {
                    var ticks=new long[5];
                    var wall=Stopwatch.StartNew();
                    if(parallel)
                        AdvanceBatch(models,1);
                    else
                        foreach(var m in models)m.ProfileMinute(ticks);
                    wall.Stop();
                    if(sample<=0)continue;
                    w.WriteLine(FormattableString.Invariant($"{sample},100,{ex.CellCount},{ex.FaceCount},")+
                        (parallel?"":string.Join(",",ticks.Select(t=>(1000d*t/Stopwatch.Frequency).ToString("F4",System.Globalization.CultureInfo.InvariantCulture)))+",")+
                        FormattableString.Invariant($"{wall.Elapsed.TotalMilliseconds:F4}"));
                    w.Flush();
                }
            }
            foreach(var m in models)
            {
                if(!m.SameInitialState(models[0]))throw new Exception("Independent hive results diverged");
                var end=m.Audit();
                if(Math.Abs(end.m-initial.m)>1e-9||Math.Abs(end.e-initial.e)>1e-5)throw new Exception("Conservation check failed");
            }
            UnityEngine.Debug.Log("Stack profiling passed: "+UnityEngine.SystemInfo.processorType);
        }
        finally{world.Thermal.Dispose();}
    }

    static void AdvanceBatch(Model[] models,int steps)
    {
        if(models.Length==1){models[0].Minute(steps);return;}
        Parallel.For(0,models.Length,i=>models[i].Minute(steps));
    }

    static void RunFixture(bool compare)
    {
        var world=new GameWorld(new WeatherSimulator(42,WeatherAssetLoader.LoadBaseline()));
        try{
        var materials=ThermalMaterials.Load();
        for(int slot=0;slot<world.Inventory.Capacity;slot++)world.Inventory.GetSlot(slot).Take();
        var box=world.Apiary.GetItemSlot(0,0).Item;
        var frame=new ItemData(99901,"wet frame",ItemKind.Frame,new FrameData());
        var surface=frame.GetContent<FrameData>().Front;surface.SetAllConstruction(100);
        if(!surface.TryAddNectar(0,0,0,40,100)||!box.InteriorSlots.GetSlot(0).TryInsert(frame))throw new Exception("Fixture insertion failed");
        world.PrepareThermal(materials);world.Thermal.PrepareExchange();
        var ex=world.Thermal.Exchange;
        var owners=ThermalWorld.Items(world).Where(x=>x.EquipmentGrid!=null).Distinct().ToDictionary(x=>x.ThermalState);
        var path=Path.Combine(Directory.GetParent(UnityEngine.Application.dataPath).FullName,"MoistureOptimizedTiming.csv");
        if(compare){CompareCells(ex,owners,materials,Path.GetDirectoryName(path));CompareCells(ex,owners,materials,Path.GetDirectoryName(path),true);return;}
        using(var w=new StreamWriter(path)){
        w.AutoFlush=true;w.WriteLine("hives,steps,cells_per_fixture,faces_per_fixture,minute_ms,managed_bytes,mass_error_kg,energy_error_J");
        var warmup=new Model(ex,owners,materials);warmup.Minute(1);warmup.Audit();
        foreach(int count in new[]{1,10,100})foreach(int steps in new[]{1,6,60})
        {
            long before=GC.GetTotalMemory(true);
            var models=new Model[count];
            for(int i=0;i<count;i++)models[i]=new Model(ex,owners,materials);
            foreach(var model in models)model.Minute(steps); // warm geometry, coefficients and JIT
            long bytes=GC.GetTotalMemory(true)-before;
            var initial=models[0].Audit();var timer=Stopwatch.StartNew();
            AdvanceBatch(models,steps);
            timer.Stop();double dm=0,de=0;
            foreach(var model in models){var end=model.Audit();dm=Math.Max(dm,Math.Abs(end.m-initial.m));de=Math.Max(de,Math.Abs(end.e-initial.e));}
            if(dm>1e-10||de>1e-6*Math.Max(1,Math.Abs(initial.e)))throw new Exception("Conservation failure");
            w.WriteLine(FormattableString.Invariant($"{count},{steps},{ex.CellCount},{ex.FaceCount},{timer.Elapsed.TotalMilliseconds:F3},{bytes},{dm:E4},{de:E4}"));
            models=null;
        }}
        }finally{world.Thermal.Dispose();}
    }
}










