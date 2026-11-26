using System;using System.IO;using System.Linq;using System.Reflection;using System.Collections.Generic;using UnityEngine;
public static class BeeRepresentativeThermalTests {
 static readonly BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
 static void Check(bool value,string label){if(!value)throw new Exception(label);}
 static void Near(double a,double b,string label){Check(Math.Abs(a-b)<1e-7*Math.Max(1,Math.Abs(b)),label+": "+a+" != "+b);}
 static object Call(object value,string name,params object[] args)=>value.GetType().GetMethod(name,Flags).Invoke(value,args);
 public static void Run(){
  var source=Resources.Load<ThermalMaterialAsset>("Thermal/Air");var transfer=Resources.Load<PyramidTransferSettings>("Thermal/PyramidTransferSettings");
  double multiplier=transfer.AirConductivityMultiplier;float physical=source.ConductivityWPerMK;var material=ThermalMaterials.Load();
  Near(material[ThermalMaterialKind.Air].Conductivity,physical*multiplier,"separate effective conductivity correction");
  Near(material[ThermalMaterialKind.Air].SpecificHeat,source.SpecificHeatJPerKgK,"air heat capacity unchanged");
  try{transfer.AirConductivityMultiplier=1;ThermalMaterials.NotifyAssetsChanged();Near(material.RefreshLiveSnapshot()[ThermalMaterialKind.Air].Conductivity,physical,"live multiplier edit restores physical conduction");}
  finally{transfer.AirConductivityMultiplier=multiplier;ThermalMaterials.NotifyAssetsChanged();}
  Check(source.ConductivityWPerMK==physical,"physical material source untouched");
  var w=new GameWorld(new WeatherSimulator(WeatherSimulator.DefaultSeed,WeatherAssetLoader.LoadBaseline(),null,123456789UL));
  try{
   w.BeeCycle.SeedDemo();var runtime=w.BeeCycle;var c=runtime.Settings;
   var heat=(Dictionary<(EquipmentThermalState,int),double>)typeof(BeeCycleRuntime).GetField("heat",Flags).GetValue(runtime);
   foreach(var stage in new[]{CycleStage.Egg,CycleStage.Larva,CycleStage.Pupa}){
    var bee=runtime.Bees.First(x=>x.stage==stage);Check(w.BeeLocations.TryGet(bee.id,out var pos),"brood location");
    var comb=(EquipmentSpatialCell)Call(runtime,"CombCell",pos);Check(comb!=null,"brood maps to equipment-owned comb");
    heat.Clear();double before=runtime.Counters.heatJ;Call(runtime,"BaseHeat",bee);
    double rate=stage==CycleStage.Egg?c.eggBasalHeatJ:stage==CycleStage.Larva?c.larvaBasalHeatJ:c.pupaBasalHeatJ;
    Check(heat.Count==1&&heat.ContainsKey((comb.Equipment.ThermalState,comb.Index)),"brood heat enters its own heat-capacity cell");
    Near(heat.Values.Sum(),rate*c.representativeScale,"one and only one representative heat multiplier");
    Near(runtime.Counters.heatJ-before,heat.Values.Sum(),"source accounting");
    comb.Equipment.ThermalState.RestoreEnergy(comb.Index,comb.Equipment.ThermalState.HeatCapacityJPerK[comb.Index]*33);
    Call(runtime,"RefreshRegions");Near(runtime.Regions.First(r=>r.position.Equals(pos)).temperature,33,"brood reads comb temperature");
   }
   heat.Clear();var heater=runtime.Bees.First(x=>x.stage==CycleStage.Adult&&!x.queen);Check(w.BeeLocations.TryGet(heater.id,out var hp),"heater position");
   var target=(EquipmentSpatialCell)Call(runtime,"CombCell",hp);Call(runtime,"AddHeat",heater,5.0,true);
   Check(heat.Count==1&&heat.ContainsKey((target.Equipment.ThermalState,target.Index)),"active heater delivers heat to comb");Near(heat.Values.Sum(),5,"contact heating conserves source energy");
   heat.Clear();var dead=new CycleBee{stage=CycleStage.Dead};Call(runtime,"BaseHeat",dead);Check(heat.Count==0,"dead bees produce no heat");
   heater.path.Add(new CyclePoint(hp));heater.pathIndex=1;heater.progress=2;Call(runtime,"Die",heater);
   Check(heater.path.Count==0&&heater.pathIndex==0&&heater.progress==0,"death clears movement cursor");
   // Reproduce the old writer's dead empty-route state; live invalid paths remain rejected.
   heater.pathIndex=1;heater.progress=2;
   var saved=WorldSaveCodec.Encode(new WorldSaveSnapshot{World=w,UtcTicks=1,TargetMinute=w.CurrentGameMinute});WorldSaveCodec.Validate(saved);var restored=WorldSaveCodec.Decode(saved).World;
   try{Check(restored.BeeCycle.Settings.broodThermalCoupling==1,"thermal mode persisted");Near(restored.BeeCycle.Settings.pupaBasalHeatJ,c.pupaBasalHeatJ,"stage heat persisted");var corpse=restored.BeeCycle.Bees.First(x=>x.id==heater.id);Check(corpse.pathIndex==0&&corpse.progress==0&&corpse.stage==CycleStage.Dead,"old dead route repaired on load");}finally{restored.Thermal?.Dispose();}
   var live=runtime.Bees.First(x=>x.stage==CycleStage.Adult);live.path.Clear();live.pathIndex=1;bool rejected=false;
   try{WorldSaveCodec.Validate(WorldSaveCodec.Encode(new WorldSaveSnapshot{World=w,UtcTicks=1,TargetMinute=w.CurrentGameMinute}));}catch(InvalidDataException){rejected=true;}
   Check(rejected,"invalid live paths still rejected");
  }finally{w.Thermal?.Dispose();}
  Debug.Log("REPRESENTATIVE THERMAL TESTS PASS");
 }
}
