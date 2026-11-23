using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using UnityEngine;
public static class BeeCargoSplitTests {
 static void Near(double a,double b,string label){if(Math.Abs(a-b)>1e-7)throw new Exception(label+": "+a+" vs "+b);}
 static object Call(BeeCycleRuntime r,string method,params object[] args)=>typeof(BeeCycleRuntime).GetMethod(method,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(r,args);
 static void Resolve(BeeCycleRuntime r)=>((Task)Call(r,"ResolveRequestsAsync",new object[]{null})).GetAwaiter().GetResult();
 static GameWorld World(){var w=new GameWorld(new WeatherSimulator(WeatherSimulator.DefaultSeed,WeatherAssetLoader.LoadBaseline(),null,123456789UL));w.BeeCycle.SeedDemo(new BeeCycleSettings{demoConstruction=70,demoSugar=300,demoWater=300,demoPollen=100,demoEggs=0,demoLarvae=0,demoPupae=0});return w;}
 public static void Run(){
  foreach(bool pollen in new[]{false,true}){
   var w=World();try{
    var r=w.BeeCycle;var b=r.Bees[0];w.BeeLocations.TryGet(b.id,out var p);b.cargo=new CycleCargo();
    if(pollen)b.cargo.Add(0,20);else{b.cargo.Add(0,5);b.cargo.pollen=15;}
    Near(r.CargoFree(b,pollen),15,"independent free capacity");Near(r.CargoRatio(b),1,"full other resource storage ratio");
    Call(r,"Acquire",b,p,pollen);Resolve(r);Near(pollen?b.cargo.pollen:b.cargo.Sugar,pollen?5:10,"acquire despite other resource full");
    Near(pollen?b.cargo.Sugar:b.cargo.pollen,pollen?20:15,"other cargo retained");
   }finally{w.Thermal.Dispose();}
  }
  {
   var w=World();try{var r=w.BeeCycle;var b=r.Bees[0];w.BeeLocations.TryGet(b.id,out var p);b.cargo=new CycleCargo();b.cargo.Add(0,20);b.task=CycleTask.Clear;b.target=new CyclePoint(p);b.hasTarget=true;
    var cfg=r.Settings;cfg.interruptPerElapsedMinute=0;r.ApplySettings(cfg);Call(r,"Run",b);Resolve(r);Near(b.cargo.Sugar,20,"clear preserves full sugar");Near(b.cargo.pollen,5,"clear uses independent pollen room");
   }finally{w.Thermal.Dispose();}
  }
  var path=Path.Combine(Application.dataPath,"Editor/ForageFixtures/legacy20-cargo.bytes");var restored=WorldSaveCodec.Decode(File.ReadAllBytes(path)).World;try{
   Near(restored.BeeCycle.Settings.carryCapacity,20,"legacy sugar cap");Near(restored.BeeCycle.Settings.pollenCarryCapacity,15,"legacy pollen cap default");Near(restored.BeeCycle.Bees[0].cargo.pollen,20,"legacy excess cargo preserved");Near(restored.BeeCycle.CargoFree(restored.BeeCycle.Bees[0],true),0,"legacy excess no acquisition");Near(restored.BeeCycle.Bees[1].cargo.Sugar,5,"legacy mixed sugar preserved");Near(restored.BeeCycle.Bees[1].cargo.pollen,15,"legacy mixed pollen preserved");
   restored.Weather.BindBaseline(WeatherAssetLoader.LoadBaseline());restored.PrepareThermal(ThermalMaterials.Load());
   var data=WorldSaveCodec.Encode(new WorldSaveSnapshot{World=restored,UtcTicks=1,TargetMinute=0});var again=WorldSaveCodec.Decode(data).World;try{again.Weather.BindBaseline(WeatherAssetLoader.LoadBaseline());again.PrepareThermal(ThermalMaterials.Load());var round=WorldSaveCodec.Encode(new WorldSaveSnapshot{World=again,UtcTicks=1,TargetMinute=0});if(!data.SequenceEqual(round))throw new Exception("split capacity save roundtrip mismatch");}finally{again.Thermal?.Dispose();}
  }finally{restored.Thermal?.Dispose();}
  Debug.Log("SPLIT CARGO PASS");BeeForageTests.Run();BeeCycleTests.Run();
 }
}
