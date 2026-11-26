using System;using System.IO;using System.Linq;using System.Reflection;using System.Collections.Generic;using UnityEngine;
public static class BeeRelocationTests {
 static readonly BindingFlags F=BindingFlags.Instance|BindingFlags.Static|BindingFlags.NonPublic;
 static object Call(BeeCycleRuntime r,string name,params object[] args)=>typeof(BeeCycleRuntime).GetMethod(name,F).Invoke(r,args);
 static void Check(bool value,string label){if(!value)throw new Exception("Relocation: "+label);}
 public static void RunAndExit(){try{Run();UnityEditor.EditorApplication.Exit(0);}catch(Exception e){Debug.LogException(e);UnityEditor.EditorApplication.Exit(1);}}
 public static void Run(){
  BeeRepresentativeTests.Run();
  var w=new GameWorld(new WeatherSimulator(WeatherSimulator.DefaultSeed,WeatherAssetLoader.LoadBaseline(),null,123456789UL));w.BeeCycle.SeedDemo();
  try{
   w.Weather.BindBaseline(WeatherAssetLoader.LoadBaseline());w.PrepareThermal(ThermalMaterials.Load());var r=w.BeeCycle;Call(r,"RefreshRegions");
   Check(r.Settings.relocateEnabled==1&&r.Settings.formulas.Any(x=>x.name=="relocateDestination"),"new game relocation defaults and formulas");
   var entrances=(Dictionary<int,BeeGridPosition>)typeof(BeeCycleRuntime).GetField("entrances",F).GetValue(r);var p=entrances.Values.First();
   var b=r.Bees.First(x=>x.stage==CycleStage.Adult&&!x.queen);foreach(var x in r.Bees.Where(x=>x.stage==CycleStage.Adult&&!x.queen)){x.outside=false;x.task=CycleTask.Rest;x.energy=80;w.BeeLocations.Set(w,x.id,p);}
   Call(r,"BeginRelocationTick");double densityWeight=r.RelocationWeight(b);Check(densityWeight>0,"crowding produces desire");
   foreach(var task in new[]{CycleTask.Nurse,CycleTask.Heat,CycleTask.Build,CycleTask.Store,CycleTask.Feed,CycleTask.ForageNectar,CycleTask.Lay}){b.task=task;Check(r.RelocationWeight(b)==0,"busy task excluded "+task);}
   b.task=CycleTask.Rest;b.outside=true;Check(r.RelocationWeight(b)==0,"outside excluded");b.outside=false;b.energy=0;Check(r.RelocationWeight(b)==0,"exhausted bee rests first");b.energy=80;
   Check((bool)Call(r,"StartRelocation",b),"select reachable better destination");Check(b.task==CycleTask.Relocate&&!b.target.Grid.Equals(p),"new task and different destination");Call(r,"TrackRelocationBee",b);
   var bytes=WorldSaveCodec.Encode(new WorldSaveSnapshot{World=w,UtcTicks=1,TargetMinute=w.CurrentGameMinute});WorldSaveCodec.Validate(bytes);var loaded=WorldSaveCodec.Decode(bytes).World;
   try{Check(loaded.BeeCycle.Bees.First(x=>x.id==b.id).task==CycleTask.Relocate,"v10 active task save/load");Check(loaded.BeeCycle.Settings.relocateHotStart==38,"settings persist");}finally{loaded.Thermal?.Dispose();}
   var counts=(Dictionary<(EquipmentThermalState,int),double>)typeof(BeeCycleRuntime).GetField("relocationOccupancy",F).GetValue(r);
   var beforeReservation=counts.Values.Sum();Call(r,"TrackRelocationBee",b);Check(Math.Abs(beforeReservation-counts.Values.Sum())<1e-8,"reservation is not counted twice");
   Check(Math.Abs(beforeReservation-r.Bees.Where(x=>x.stage==CycleStage.Adult&&!x.outside).Sum(x=>r.RepresentedBees(x))-r.RepresentedBees(b))<1e-6,"target reservation adds exactly one represented bee");
   Call(r,"End",b,CycleEnd.Interrupted);b.task=CycleTask.Rest;Call(r,"TrackRelocationBee",b);
   var noTarget=r.Settings;noTarget.relocateMinImprovement=1000;r.ApplySettings(noTarget);Check(!(bool)Call(r,"StartRelocation",b)&&b.task==CycleTask.Rest,"no better destination preserves rest");noTarget.relocateMinImprovement=0.1;r.ApplySettings(noTarget);
   var cfg=r.Settings;cfg.relocateCrowdingWeight=0;r.ApplySettings(cfg);
   var air=Call(r,"Air",p);var owner=(EquipmentThermalState)typeof(BeeCycleRuntime).GetMethod("Owner",F).Invoke(null,new[]{air});int index=(int)typeof(BeeCycleRuntime).GetMethod("OwnerIndex",F).Invoke(null,new[]{air});
   owner.RestoreEnergy(index,owner.HeatCapacityJPerK[index]*37);Check(r.RelocationWeight(b)==0,"hot start threshold");
   owner.RestoreEnergy(index,owner.HeatCapacityJPerK[index]*39);Check(r.RelocationWeight(b)>0,"hot desire");
   owner.RestoreEnergy(index,owner.HeatCapacityJPerK[index]*17);Check(r.RelocationWeight(b)>0,"cold desire");
   cfg.relocateEnabled=0;r.ApplySettings(cfg);uint rng=r.RandomState;Call(r,"TryRelocateFromRest",b);Check(r.RandomState==rng&&r.RelocationWeight(b)==0,"disabled path consumes no RNG");
   cfg.relocateDensityStop=cfg.relocateDensityStart;bool rejected=false;try{cfg.Validate();}catch(ArgumentException){rejected=true;}Check(rejected,"invalid hysteresis rejected");
  }finally{w.Thermal?.Dispose();}
  Debug.Log("BEE RELOCATION TESTS PASS");
 }
}
