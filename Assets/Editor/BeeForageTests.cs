using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using UnityEditor;
using UnityEngine;
public static class BeeForageTests
{
    public static void RunAll(){Run();BeeCycleTests.Run();}
    static int checks;
    static BeeGridPosition testPosition;
    static void Check(bool ok,string label){checks++;if(!ok)throw new Exception(label);}
    static void Near(double a,double b,string label)=>Check(Math.Abs(a-b)<1e-8,label+": "+a+" / "+b);
    public static void FiniteFixture(GameWorld w){
        for(int i=0;i<8;i++){
            var p=w.Weather.Nectar.Settings.Sources[i];p.BaseBloomRate=p.BaseWiltingRate=p.NectarProductionFactor=p.WaterProductionFactor=p.NectarEvaporationRate=p.PollenRainLossCoefficient=p.PollenProductionFactor=0;p.NectarStorageFactor=1e12;
            var s=w.Weather.Nectar.Sources[i];s.bloom=1;s.unopened=0;s.sugar=s.water=s.pollen=i==0?1e6:0;
        }
    }
    static GameWorld World(){
        var w=new GameWorld(new WeatherSimulator(WeatherSimulator.DefaultSeed,WeatherAssetLoader.LoadBaseline(),null,123456789UL));w.BeeCycle.SeedDemo();
        w.BeeLocations.TryGet(w.BeeCycle.Bees[0].id,out testPosition);
        foreach(var b in w.BeeCycle.Bees)w.BeeLocations.Remove(b.id);
        w.BeeCycle.Bees.Clear();return w;
    }
    static CycleBee Returner(GameWorld w,int source,bool pollen,double demand,bool interrupted=false){
        // Use the ordinary creation path and a valid return location from the topology.
        var pos=testPosition;
        var b=w.BeeCycle.Add(pos,pos.EquipmentId);b.task=pollen?CycleTask.ForagePollen:CycleTask.ForageNectar;b.returnPoint=new CyclePoint(pos);b.forageSource=source;b.outside=true;b.outsideRemaining=1;b.returning=interrupted;b.interruptAfterReturn=interrupted;
        b.cargo.pollen=w.BeeCycle.Settings.carryCapacity-demand;w.BeeLocations.Remove(b.id);return b;
    }
    static void Tick(GameWorld w){w.BeeCycle.CalculateMinuteAsync(w.BeeCycle.LastMinute+1,CancellationToken.None).GetAwaiter().GetResult();}
    static void Quiet(GameWorld w){var s=w.BeeCycle.Settings;s.interruptPerElapsedMinute=0;s.commandMaximum=0;s.feedWeight=s.restWeight=s.recoverWeight=s.storeWeight=0;s.basalSugar=s.basalEnergy=0;w.BeeCycle.ApplySettings(s);}
    static byte[] Save(GameWorld w)=>WorldSaveCodec.Encode(new WorldSaveSnapshot{World=w,UtcTicks=1,TargetMinute=w.CurrentGameMinute});
    [MenuItem("Tools/HoneyComb/검증/Run Finite Forage Tests")]
    public static void Run(){
        checks=0;
        var w=World();try{
            Quiet(w);var n=w.Weather.Nectar;var stock=n.Sources[0];stock.sugar=10;stock.water=30;stock.pollen=12;
            var taken=NectarHarvest.Withdraw(n,0,2,6);Near(taken.sugar,2,"raw sugar");Near(taken.water,6,"water ratio");Near(taken.pollen,6,"raw pollen");
            bool rejected=false;try{NectarHarvest.Withdraw(n,0,double.NaN,1);}catch(ArgumentOutOfRangeException){rejected=true;}Check(rejected,"reject NaN");Near(stock.sugar,8,"invalid unchanged");
            taken=NectarHarvest.Withdraw(n,0,100,100);Near(taken.sugar,8,"limited sugar");Near(taken.water,24,"limited water");Near(taken.pollen,6,"limited pollen");
            stock.sugar=12;stock.water=24;stock.pollen=8;
            var a=Returner(w,0,false,5);var b=Returner(w,0,false,15);var c=Returner(w,0,true,5);var d=Returner(w,0,true,15);
            Tick(w);Near(a.lastAcquired,3,"5:15 sugar share A");Near(b.lastAcquired,9,"5:15 sugar share B");Near(c.lastAcquired,2,"pollen share A");Near(d.lastAcquired,6,"pollen share B");Near(stock.sugar,0,"depleted sugar");Near(stock.water,0,"depleted water");Near(stock.pollen,0,"depleted pollen");Near(w.BeeCycle.Counters.discardedWater,24,"discard counter");Near(w.BeeCycle.Counters.externalSugar,12,"sugar conservation");Near(w.BeeCycle.Counters.externalPollen,8,"pollen conservation");Check(!a.outside&&a.forageSource==-1,"returned state");
        }finally{w.Thermal?.Dispose();}
        w=World();try{
            Quiet(w);var n=w.Weather.Nectar;n.Sources[1].sugar=30;n.Sources[1].water=60;
            var a=Returner(w,0,false,15);var b=Returner(w,1,false,15,true);Tick(w);
            Near(a.lastAcquired,0,"selected source depleted: no reroute");Near(n.Sources[1].sugar,30,"interrupt no withdrawal");Check(w.BeeCycle.Counters.resourceFailed==1&&w.BeeCycle.Counters.interrupted==1,"return outcomes");
            foreach(var bee in w.BeeCycle.Bees)w.BeeLocations.Remove(bee.id);w.BeeCycle.Bees.Clear();
            n.Sources[0].sugar=1;n.Sources[1].sugar=3;
            var select=typeof(BeeCycleRuntime).GetMethod("SelectForageSource",BindingFlags.NonPublic|BindingFlags.Instance);int second=0;
            for(int i=0;i<4000;i++){int selected=(int)select.Invoke(w.BeeCycle,new object[]{false});Check(selected==0||selected==1,"positive source only");if(selected==1)second++;}
            Check(second>2850&&second<3150,"stock weighted selection: "+second);
            n.Sources[0].sugar=n.Sources[1].sugar=0;Check((int)select.Invoke(w.BeeCycle,new object[]{false})==-1,"no stock choice");
            for(int i=0;i<8;i++){n.Sources[i].pollen=1;Check((int)select.Invoke(w.BeeCycle,new object[]{true})==i,"all eight pollen sources");n.Sources[i].pollen=0;}
        }finally{w.Thermal?.Dispose();}
        w=World();try{
            Quiet(w);var b=w.BeeCycle.Add(testPosition,testPosition.EquipmentId);b.task=CycleTask.ForageNectar;b.target=new CyclePoint(testPosition);b.hasTarget=true;
            Tick(w);Check(!b.outside&&w.BeeCycle.Counters.resourceFailed==1,"empty stock prevents departure");
            var stock=w.Weather.Nectar.Sources[7];stock.sugar=20;stock.water=40;
            b.task=CycleTask.ForageNectar;b.target=new CyclePoint(testPosition);b.hasTarget=true;Tick(w);
            Check(b.outside&&b.forageSource==7,"departure chooses source");Near(stock.sugar,20,"departure does not reserve stock");
            for(int i=0;i<7;i++)Tick(w);Check(b.outside,"full trip duration");Near(stock.sugar,20,"in flight stock untouched");
            Tick(w);Check(!b.outside,"trip completes");Near(b.lastAcquired,15,"per-trip cap");Near(stock.sugar,5,"completion deducts");Near(stock.water,10,"completion proportional water");
        }finally{w.Thermal?.Dispose();}
        w=World();try{
            Quiet(w);var a=Returner(w,0,false,5);a.homeBox=1;
            var hive=new HiveData(9000,1,0);var box=new ItemData(9000,"Second hive",ItemKind.BroodBox,hive);Check(w.Apiary.AddHive(hive,box),"second hive inserted");
            var frame=new ItemData(9001,"Second frame",ItemKind.Frame,new FrameData());Check(box.InteriorSlots.GetSlot(2).TryInsert(frame),"second frame inserted");
            testPosition=BeeGridPosition.FromSupport(frame.Id,2,0,1,BeeFace.MinusY);
            var b=Returner(w,0,false,15);b.homeBox=box.Id;w.Weather.Nectar.Sources[0].sugar=12;
            Tick(w);Near(a.lastAcquired,3,"two hive share A");Near(b.lastAcquired,9,"two hive share B");Check(w.BeeLocations.TryGet(b.id,out var returned)&&returned.EquipmentId==frame.Id,"return to second hive");
        }finally{w.Thermal?.Dispose();}
        w=World();try{
            Quiet(w);FiniteFixture(w);var b=Returner(w,0,false,15);b.outsideRemaining=3;
            var restored=WorldSaveCodec.Decode(Save(w)).World;try{
                restored.Weather.BindBaseline(WeatherAssetLoader.LoadBaseline());restored.PrepareThermal(ThermalMaterials.Load());
                foreach(var world in new[]{w,restored}){
                    var scheduler=new SimulationScheduler(world.Clock);scheduler.Register(world.Weather);scheduler.Register(world.BeeCycle);
                    scheduler.RunToAsync(4,()=>System.Threading.Tasks.Task.CompletedTask,()=>System.Threading.Tasks.Task.CompletedTask,CancellationToken.None).GetAwaiter().GetResult();
                }
                Near(w.Weather.Nectar.Sources[0].sugar,restored.Weather.Nectar.Sources[0].sugar,"resume stock exact");
                var originalSave=Save(w);var restoredSave=Save(restored);Check(originalSave.Skip(originalSave.Length-w.BeeCycle.SnapshotSize()).SequenceEqual(restoredSave.Skip(restoredSave.Length-restored.BeeCycle.SnapshotSize())),"in-flight resume bee bytes exact");
                Near(w.BeeCycle.Counters.externalSugar,15,"saved trip collected once");
            }finally{restored.Thermal?.Dispose();}
        }finally{w.Thermal?.Dispose();}
        var root=Path.GetFullPath(Path.Combine(Application.dataPath,"Editor/ForageFixtures"));
        w=WorldSaveCodec.Decode(File.ReadAllBytes(Path.Combine(root,"legacy19.bytes"))).World;try{
            w.Weather.BindBaseline(WeatherAssetLoader.LoadBaseline());w.PrepareThermal(ThermalMaterials.Load());
            Check(w.BeeCycle.Bees.Count==49,"v19 count");Check(w.BeeCycle.Bees.Take(2).All(b=>b.outside&&b.outsideRemaining==4&&b.forageSource==-1),"v19 in-flight migration");
            w.BeeCycle.Bees[0].forageSource=6;var bytes=Save(w);var restored=WorldSaveCodec.Decode(bytes).World;try{restored.Weather.BindBaseline(WeatherAssetLoader.LoadBaseline());restored.PrepareThermal(ThermalMaterials.Load());Check(Save(restored).SequenceEqual(bytes),"v20 exact roundtrip");Check(restored.BeeCycle.Bees[0].forageSource==6,"source persisted");}finally{restored.Thermal?.Dispose();}
        }finally{w.Thermal?.Dispose();}
        var weather=new WeatherSimulator(WeatherSimulator.DefaultSeed,WeatherAssetLoader.LoadBaseline(),null,123456789UL);for(int i=1;i<=120;i++)weather.Advance(i);
        using(var stream=new MemoryStream()){using(var writer=new BinaryWriter(stream,System.Text.Encoding.UTF8,true))weather.WriteSnapshot(writer);Check(stream.ToArray().SequenceEqual(File.ReadAllBytes(Path.Combine(root,"weather-before.bytes"))),"no-harvest weather bit exact 120 ticks");}
        Debug.Log("FINITE FORAGE PASS "+checks+" checks");
    }
}

