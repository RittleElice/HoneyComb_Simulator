using System;using System.IO;using System.Linq;using System.Reflection;using System.Collections;using System.Threading.Tasks;using UnityEngine;
public static class BeeLoadForageTests {
 static readonly BindingFlags Flags=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
 static object Call(BeeCycleRuntime r,string name,params object[] args)=>typeof(BeeCycleRuntime).GetMethod(name,Flags).Invoke(r,args);
 static void Check(bool ok,string label){if(!ok)throw new Exception("Load forage: "+label);}
 static void Near(double a,double b,string label)=>Check(Math.Abs(a-b)<1e-7,label+" "+a+" / "+b);
 static void Tick(BeeCycleRuntime r,CycleBee b){((IList)typeof(BeeCycleRuntime).GetField("forageRequests",Flags).GetValue(r)).Clear();b.tickActivity=0;Call(r,"Run",b);((Task)Call(r,"ResolveForageRequestsAsync",new object[]{null})).GetAwaiter().GetResult();}
 public static void Run(){
 BeeRepresentativeTests.Run();
 var w=new GameWorld(new WeatherSimulator(WeatherSimulator.DefaultSeed,WeatherAssetLoader.LoadBaseline(),null,123456789UL));
 try{
 w.BeeCycle.SeedDemo(new BeeCycleSettings{demoWorkers=0,demoEggs=0,demoLarvae=0,demoPupae=0});var r=w.BeeCycle;var b=r.Bees[0];w.BeeLocations.TryGet(b.id,out var pos);b.queen=false;b.task=CycleTask.ForageNectar;b.returnPoint=new CyclePoint(pos);b.forageSource=0;b.outside=true;b.outsideRemaining=999;b.cargo=new CycleCargo();w.BeeLocations.Remove(b.id);
 var cfg=r.Settings;foreach(double shortage in new[]{0.0,0.25,0.5,1.0})Near(r.ForageShortageReturnProbability(20,20*(1-shortage)),shortage*0.2,"shortage return curve");Near(r.ForageShortageReturnProbability(0,0),0,"zero demand");cfg.formulas.First(x=>x.name=="forageShortageReturnChance").expression="0";cfg.interruptPerElapsedMinute=1;cfg.formulas.First(x=>x.name=="forageReturnChance").expression="0";r.ApplySettings(cfg);foreach(var s in w.Weather.Nectar.Sources)s.sugar=s.water=s.pollen=0;var source=w.Weather.Nectar.Sources[0];source.sugar=1000;source.water=2000;
 Tick(r,b);Near(b.cargo.Sugar,1.875*11.05,"per minute physical collection");Near(source.sugar,1000-b.cargo.Sugar,"actual source deduction");Check(b.outside&&b.outsideRemaining==999,"timer and generic interruption bypassed");
 var saved=WorldSaveCodec.Encode(new WorldSaveSnapshot{World=w,UtcTicks=1,TargetMinute=w.CurrentGameMinute});WorldSaveCodec.Validate(saved);var restored=WorldSaveCodec.Decode(saved).World;try{Near(restored.BeeCycle.Bees[0].cargo.Sugar,b.cargo.Sugar,"in-flight cargo persists");Check(restored.BeeCycle.Bees[0].outside&&restored.BeeCycle.Settings.forageLoadBased==1,"mode and in-flight state persist");}finally{restored.Thermal?.Dispose();}
 cfg.formulas.First(x=>x.name=="forageReturnChance").expression="pow(clamp(ratio,0,1),forageReturnExponent)";r.ApplySettings(cfg);
 foreach(double fill in new[]{0.0,0.25,0.5,0.75,1.0}){b.cargo=new CycleCargo();b.cargo.Add(0,cfg.carryCapacity*11.05*fill);Near(r.ForageReturnProbability(b),fill*fill,"return curve");Near((double)Call(r,"ForageChoiceWeight",b,CycleTask.ForageNectar,100.0),100*(1-fill)*(1-fill),"departure weight");Near((double)Call(r,"ForageChoiceWeight",b,CycleTask.Nurse,100.0),100,"unrelated desire unchanged");}
 Tick(r,b);Check(!b.outside&&b.task==CycleTask.None,"full load guaranteed return");
 cfg.formulas.First(x=>x.name=="forageReturnChance").expression="0";r.ApplySettings(cfg);b.task=CycleTask.ForageNectar;b.outside=true;b.returnPoint=new CyclePoint(pos);b.forageSource=0;b.cargo=new CycleCargo();w.BeeLocations.Remove(b.id);source.sugar=0;w.Weather.Nectar.Sources[1].sugar=100;
 Tick(r,b);Check(b.outside&&b.forageSource==1&&b.cargo.Sugar>0,"switch depleted source and continue collection");
 foreach(var s in w.Weather.Nectar.Sources)s.sugar=0;double held=b.cargo.Sugar;Tick(r,b);Check(b.outside&&b.forageSource==-1,"no alternative waits when chance zero");Near(b.cargo.Sugar,held,"waiting retains cargo");
 var waitingBytes=WorldSaveCodec.Encode(new WorldSaveSnapshot{World=w,UtcTicks=1,TargetMinute=w.CurrentGameMinute});WorldSaveCodec.Validate(waitingBytes);var waiting=WorldSaveCodec.Decode(waitingBytes).World;try{Check(waiting.BeeCycle.Bees[0].outside&&waiting.BeeCycle.Bees[0].forageSource==-1,"waiting search state persists");Near(waiting.BeeCycle.Settings.forageShortageReturnRate,.2,"shortage parameter persists");}finally{waiting.Thermal?.Dispose();}
 cfg.formulas.First(x=>x.name=="forageShortageReturnChance").expression="1";r.ApplySettings(cfg);Tick(r,b);Check(!b.outside,"certain shortage return");Near(b.cargo.Sugar,held,"return retains cargo");
 cfg.formulas.First(x=>x.name=="forageShortageReturnChance").expression="0";r.ApplySettings(cfg);
 b.task=CycleTask.ForagePollen;b.outside=true;b.forageSource=0;b.returnPoint=new CyclePoint(pos);w.BeeLocations.Remove(b.id);b.cargo=new CycleCargo();source.pollen=100;Tick(r,b);Near(b.cargo.pollen,1.875*11.05,"independent pollen collection");Near(b.cargo.Sugar,0,"pollen does not create sugar");
 Debug.Log("LOAD FORAGE TESTS PASS; SHORTAGE RETURN TESTS PASS");
 }finally{w.Thermal?.Dispose();}
 }
}
