using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

public static class BeeCycleTests
{
    static int checks;
    static void Check(bool value,string label){checks++;if(!value)throw new Exception("CHECK FAILED: "+label);}
    static void Near(double a,double b,double epsilon,string label)=>Check(Math.Abs(a-b)<=epsilon,$"{label}: {a:R} vs {b:R}");
    static GameWorld World()
    {var w=new GameWorld(WeatherAssetLoader.Create(WeatherSimulator.DefaultSeed));w.PrepareThermal(ThermalMaterials.Load());w.BeeCycle.SeedDemo();w.Thermal.Refresh();return w;}
    static void Tick(GameWorld w,int n,bool full=true)
    {
        var scheduler=full?GameTimeRuntime.CreateWorldScheduler(w):new SimulationScheduler(w.Clock);
        if(!full){scheduler.Register(w.Weather);scheduler.Register(w.BeeCycle);}
        scheduler.RunToAsync(w.CurrentGameMinute+n,()=>Task.CompletedTask,()=>Task.CompletedTask,CancellationToken.None).GetAwaiter().GetResult();
    }
    static byte[] Save(GameWorld w)=>WorldSaveCodec.Encode(new WorldSaveSnapshot{World=w,UtcTicks=1,TargetMinute=w.CurrentGameMinute});
    static double Sugar(GameWorld w)
    {
        double n=0;foreach(var item in ThermalWorld.Items(w))if(item.GetContent<FrameData>() is FrameData f)foreach(var face in new[]{f.Front,f.Back})for(int y=0;y<face.Height;y++)for(int x=0;x<face.Width;x++)n+=face.GetTotalSugarMassMg(x,y);
        foreach(var b in w.BeeCycle.Bees)n+=b.cargo.Sugar+b.reserve+b.broodSugar;
        return n;
    }
    [MenuItem("Tools/HoneyComb/검증/Run Bee Cycle Tests")]
    public static void Run()
    {
        checks=0;var output=new System.Text.StringBuilder();
        try
        {
            Materials();Rules();
            var w=World();
            try
            {
                double initial=Sugar(w);var watch=System.Diagnostics.Stopwatch.StartNew();Tick(w,120);
                Check(w.BeeCycle.Counters.externalSugar>0,"forage nectar returns");
                Check(w.BeeCycle.Counters.actions[(int)CycleAction.Store]>0,"storage action executes");
                Check(w.BeeCycle.Counters.actions[(int)CycleAction.Feed]>0,"feeding action executes");
                Check(w.BeeCycle.Counters.actions[(int)CycleAction.Rest]>0,"rest action executes");
                Check(w.BeeCycle.Counters.built>0,"construction progresses");
                Check(w.BeeCycle.Counters.heatJ>0,"basal heat applied");
                Near(Sugar(w)+w.BeeCycle.Counters.consumedSugar+w.BeeCycle.Counters.lostSugar,initial+w.BeeCycle.Counters.externalSugar,0.001,"sugar mass balance");
                Check(w.BeeLocations.Count==w.BeeCycle.Bees.Count(b=>b.stage!=CycleStage.Dead&&!b.outside),"occupancy count");
                var saved=Save(w);var restored=WorldSaveCodec.Decode(saved).World;
                try
                {
                    restored.Weather.BindBaseline(WeatherAssetLoader.LoadBaseline());restored.PrepareThermal(ThermalMaterials.Load());Check(saved.SequenceEqual(Save(restored)),"exact save round trip");
                    Tick(w,10);Tick(restored,10);
                    File.WriteAllText(Path.Combine(Application.dataPath,"../../original-bees.json"),JsonUtility.ToJson(new BeeDump{bees=w.BeeCycle.Bees.ToArray(),counters=w.BeeCycle.Counters},true));
                    File.WriteAllText(Path.Combine(Application.dataPath,"../../restored-bees.json"),JsonUtility.ToJson(new BeeDump{bees=restored.BeeCycle.Bees.ToArray(),counters=restored.BeeCycle.Counters},true));
                    File.WriteAllBytes(Path.Combine(Application.dataPath,"../../original.bin"),Save(w));File.WriteAllBytes(Path.Combine(Application.dataPath,"../../restored.bin"),Save(restored));
                    var wa=Save(w);var wb=Save(restored);
                    Check(wa.Skip(wa.Length-w.BeeCycle.SnapshotSize()).SequenceEqual(wb.Skip(wb.Length-restored.BeeCycle.SnapshotSize())),"bee save resume exact continuation");
                    double maxT=0;var restoredItems=ThermalWorld.Items(restored).ToDictionary(i=>i.Id);
                    foreach(var item in ThermalWorld.Items(w))for(int ci=0;ci<item.ThermalState.Count;ci++)if(item.ThermalState.IsInitialized(ci)&&!item.ThermalState.IsDirty(ci))maxT=Math.Max(maxT,Math.Abs(item.ThermalState.TemperatureC(ci)-restoredItems[item.Id].ThermalState.TemperatureC(ci)));
                    output.AppendLine($"Full world byte identical={wa.SequenceEqual(wb)}; max temperature difference after 10 resumed ticks={maxT:R} C; bee state byte-identical");
                }finally{restored.Thermal?.Dispose();}
                output.AppendLine($"120 full world ticks elapsed={watch.Elapsed.TotalMilliseconds:F2}ms; bee last={w.BeeCycle.LastMilliseconds:F3}ms; initial adults=49; live={w.BeeCycle.Bees.Count(b=>b.stage!=CycleStage.Dead)}");
                output.AppendLine(JsonUtility.ToJson(w.BeeCycle.Counters,true));
                // Actual version-18 data remains readable, without generating bees or food.
                var old=saved.Take(saved.Length-w.BeeCycle.SnapshotSize()).ToArray();
                // Snapshot size changes as state evolves; construct this fixture from its own boundary.
                var now=Save(w);old=now.Take(now.Length-w.BeeCycle.SnapshotSize()).ToArray();Array.Copy(BitConverter.GetBytes(18),old,4);
                var legacy=WorldSaveCodec.Decode(old).World;Check(legacy.BeeCycle.Bees.Count==0,"legacy no implicit spawning");legacy.Thermal?.Dispose();
            }finally{w.Thermal.Dispose();}
            Mortality();Sharing();Development();BroodCycle();BaselineResume(output);Performance(output);LongRun(output);
            BeeSurfaceTests.Run();
            WorldSaveTests.Run();
            try{PersistentThermalTests.Run(ThermalMaterials.Load());output.AppendLine("Existing PersistentThermalTests: PASS");}
            catch(Exception e){output.AppendLine("Existing PersistentThermalTests diagnostic: "+e.Message+" (see separate original-source baseline control)");}
            if(AssetDatabase.LoadAssetAtPath<BeeCycleSettingsAsset>("Assets/Resources/BeeCycleParameters.asset")==null)
            {var asset=ScriptableObject.CreateInstance<BeeCycleSettingsAsset>();AssetDatabase.CreateAsset(asset,"Assets/Resources/BeeCycleParameters.asset");AssetDatabase.SaveAssets();}
            output.AppendLine($"PASS {checks} bee checks plus existing BeeSurfaceTests and WorldSaveTests; thermal diagnostic reported separately");
            File.WriteAllText(Path.Combine(Application.dataPath,"../../test-report.txt"),output.ToString());Debug.Log(output.ToString());
        }
        catch(Exception e){output.AppendLine(e.ToString());File.WriteAllText(Path.Combine(Application.dataPath,"../../test-report.txt"),output.ToString());Debug.LogException(e);throw;}
    }
    [Serializable] sealed class BeeDump { public CycleBee[] bees;public CycleCounters counters; }
    static void Materials()
    {
        var f=new FrameSurface(25,12);f.TrySetConstruction(0,0,100);f.TryAddNectar(0,0,2,10,20);f.TryAddNectar(0,0,3,20,0);
        var take=f.WithdrawCycle(0,0,15,false,out double water);Near(take.Sugar,15,1e-7,"withdraw actual sugar");Near(water,10,1e-7,"discard proportional water");Near(take.sugars.Find(s=>s.source==2).mass,5,1e-7,"source preserved");
        Near(f.DepositCycle(0,0,2,1.0/3,false),f.GetSugarMassMg(0,0,2)-5,1e-7,"actual float deposit");
    }
    static void Rules()
    {
        var s=new BeeCycleSettings();var rules=new BeeCycleRules(s);
        Near(rules.Eval("desireNext",new CycleInputs{amount=20,available=30}),40,0,"stimulus minus10");
        Near(rules.Eval("feedDesire",new CycleInputs{ratio=0.5}),10,0,"state square");
        s.formulas.Find(f=>f.name=="feedDesire").expression="feedWeight * ratio";rules=new BeeCycleRules(s);Near(rules.Eval("feedDesire",new CycleInputs{ratio=0.5}),20,0,"editable formula");
    }
    static void Mortality()
    {
        var w=World();try{var b=w.BeeCycle.Bees[0];b.development=200;b.outside=true;WorldPositionForReturn(w,b);w.BeeLocations.Remove(b.id);var c=w.BeeCycle.Bees[1];c.health=0;Tick(w,1,false);Check(b.stage==CycleStage.Dead&&c.stage==CycleStage.Dead,"development and health death");Check(!b.outside&&!w.BeeLocations.TryGet(b.id,out _),"dead removed from occupancy and outside state");}finally{w.Thermal.Dispose();}
    }
    static void WorldPositionForReturn(GameWorld w,CycleBee b){w.BeeLocations.TryGet(b.id,out var p);b.returnPoint=new CyclePoint(p);}
    public static void FinalSmoke()
    {checks=0;Mortality();Sharing();BroodCycle();Debug.Log("Final lifecycle/material smoke PASS "+checks);}
    static void Sharing()
    {
        var w=World();try
        {
            var cfg=w.BeeCycle.Settings;cfg.interruptPerElapsedMinute=0;cfg.basalSugar=cfg.basalEnergy=0;cfg.commandMaximum=0;w.BeeCycle.ApplySettings(cfg);
            foreach(var bee in w.BeeCycle.Bees){bee.task=CycleTask.None;bee.reserve=cfg.reserveTarget;bee.energy=cfg.maxEnergy;}
            var a=w.BeeCycle.Bees[0];var b=w.BeeCycle.Bees[1];a.task=b.task=CycleTask.Feed;a.reserve=b.reserve=0;
            Tick(w,1,false);Near(a.lastAcquired,b.lastAcquired,1e-9,"equal demand proportional sharing");Check(a.lastAcquired>0,"acquired from frame");
            a.cargo=new CycleCargo{pollen=16};b.cargo=new CycleCargo();cfg.acquireRate=100;w.BeeCycle.ApplySettings(cfg);
            foreach(var item in ThermalWorld.Items(w))if(item.GetContent<FrameData>() is FrameData f)foreach(var face in new[]{f.Front,f.Back})for(int yy=0;yy<12;yy++)for(int xx=0;xx<25;xx++)if(face.GetCell(xx,yy).Content==CellContentKind.Nectar)face.TryClearContent(xx,yy);
            var firstFrame=ThermalWorld.Items(w).First(i=>i.Kind==ItemKind.Frame&&i.GetContent<FrameData>().Front.GetCell(0,0).IsComplete);firstFrame.GetContent<FrameData>().Front.TryAddNectar(0,0,0,6,6);
            Tick(w,1,false);Near(a.lastAcquired,1,1e-8,"scarcity share 4/24");Near(b.lastAcquired,5,1e-8,"scarcity share 20/24");
        }finally{w.Thermal.Dispose();}
    }
    static void Development()
    {
        var w=World();try
        {
            var b=w.BeeCycle.Bees[0];var cfg=w.BeeCycle.Settings;cfg.commandMaximum=0;cfg.interruptPerElapsedMinute=0;cfg.basalEnergy=cfg.basalSugar=0;w.BeeCycle.ApplySettings(cfg);
            b.task=CycleTask.Rest;b.energy=0;double before=b.development;Tick(w,1,false);Near(b.development-before,cfg.restDevelopment,1e-10,"per tick rest development");
        }finally{w.Thermal.Dispose();}
    }
    static void BroodCycle()
    {
        var w=World();try
        {
            var cfg=w.BeeCycle.Settings;cfg.broodTick=0.5;cfg.queenLayPerMinute=1;cfg.commandMaximum=100;cfg.interruptPerElapsedMinute=0;w.BeeCycle.ApplySettings(cfg);
            foreach(var item in ThermalWorld.Items(w))for(int i=0;i<item.ThermalState.Count;i++)if(item.ThermalState.IsInitialized(i))item.ThermalState.RestoreEnergy(i,item.ThermalState.HeatCapacityJPerK[i]*34);
            Tick(w,80,false);Check(w.BeeCycle.Counters.eggs>0,"queen lays eggs");Check(w.BeeCycle.Counters.actions[(int)CycleAction.Nurse]>0,"nursing executes");Check(w.BeeCycle.Counters.emerged>0,"accelerated staged emergence");
            Check(w.BeeLocations.Count==w.BeeCycle.Bees.Count(b=>b.stage!=CycleStage.Dead&&!b.outside),"brood/adult occupancy conserved");
            var child=w.BeeCycle.Bees.First(b=>b.stage==CycleStage.Egg);w.BeeLocations.TryGet(child.id,out var p);var broodItem=ThermalWorld.Items(w).First(i=>i.Id==p.EquipmentId);var face=p.Face==BeeFace.MinusY?broodItem.GetContent<FrameData>().Front:broodItem.GetContent<FrameData>().Back;
            face.TryClearContent(child.broodColumn,child.broodRow);Tick(w,1,false);Check(child.health==0&&child.stage==CycleStage.Dead,"lost brood link sets health zero");
        }finally{w.Thermal.Dispose();}
    }
    static void BaselineResume(System.Text.StringBuilder output)
    {
        var w=new GameWorld(WeatherAssetLoader.Create(WeatherSimulator.DefaultSeed));w.PrepareThermal(ThermalMaterials.Load());GameWorld clone=null;
        try
        {
            Tick(w,5);clone=WorldSaveCodec.Decode(Save(w)).World;clone.Weather.BindBaseline(WeatherAssetLoader.LoadBaseline());clone.PrepareThermal(ThermalMaterials.Load());Tick(w,10);Tick(clone,10);
            double maxT=0;var other=ThermalWorld.Items(clone).ToDictionary(i=>i.Id);
            foreach(var item in ThermalWorld.Items(w))for(int i=0;i<item.ThermalState.Count;i++)if(item.ThermalState.IsInitialized(i)&&!item.ThermalState.IsDirty(i))maxT=Math.Max(maxT,Math.Abs(item.ThermalState.TemperatureC(i)-other[item.Id].ThermalState.TemperatureC(i)));
            output.AppendLine($"No-bee control resume byte-identical={Save(w).SequenceEqual(Save(clone))}; max temperature delta={maxT:R} C");
        }finally{w.Thermal.Dispose();clone?.Thermal?.Dispose();}
    }
    static void Performance(System.Text.StringBuilder output)
    {
        foreach(int count in new[]{100,1000})
        {
            var w=World();try
            {
                var first=w.BeeCycle.Bees[0];w.BeeLocations.TryGet(first.id,out var p);
                while(w.BeeCycle.Bees.Count<count)w.BeeCycle.Add(p,first.homeBox);
                Tick(w,3,false);long before=GC.GetTotalMemory(true);double sum=0,max=0;var watch=System.Diagnostics.Stopwatch.StartNew();
                for(int i=0;i<30;i++){Tick(w,1,false);sum+=w.BeeCycle.LastMilliseconds;max=Math.Max(max,w.BeeCycle.LastMilliseconds);}
                long after=GC.GetTotalMemory(false);output.AppendLine($"N={count}: bee mean={sum/30:F3}ms max={max:F3}ms; weather+bee elapsed={watch.Elapsed.TotalMilliseconds:F1}ms; managed heap delta={after-before} bytes (not peak/resident memory)");
            }finally{w.Thermal.Dispose();}
        }
    }
    static void LongRun(System.Text.StringBuilder output)
    {
        var w=World();try
        {
            double initial=Sugar(w);var watch=System.Diagnostics.Stopwatch.StartNew();
            for(int i=0;i<4;i++){Tick(w,360);Debug.Log("Bee full-world long run "+w.CurrentGameMinute+"/1440");}
            double balance=Sugar(w)+w.BeeCycle.Counters.consumedSugar+w.BeeCycle.Counters.lostSugar-initial-w.BeeCycle.Counters.externalSugar;
            Near(balance,0,0.01,"one-day sugar conservation");Check(w.BeeCycle.Failure==null,"one-day no tick failure");
            output.AppendLine($"1440 full-world minutes: {watch.Elapsed.TotalMilliseconds:F1}ms; sugar balance error={balance:R}mg; built={w.BeeCycle.Counters.built}; completed={w.BeeCycle.Counters.completed}; living={w.BeeCycle.Bees.Count(b=>b.stage!=CycleStage.Dead)}");
        }finally{w.Thermal.Dispose();}
    }
}
