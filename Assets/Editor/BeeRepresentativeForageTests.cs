using System;using System.Linq;using System.Threading;using UnityEngine;
public static class BeeRepresentativeForageTests {
 static void Near(double a,double b,string s){if(Math.Abs(a-b)>1e-7)throw new Exception(s+": "+a+" != "+b);}
 public static void Run(){
  var w=new GameWorld(new WeatherSimulator(WeatherSimulator.DefaultSeed,WeatherAssetLoader.LoadBaseline(),null,123456789UL));
  try{
   w.BeeCycle.SeedDemo(new BeeCycleSettings{demoWorkers=0,demoEggs=0,demoLarvae=0,demoPupae=0});var r=w.BeeCycle;
   w.BeeLocations.TryGet(r.Bees[0].id,out var pos);foreach(var bee in r.Bees)w.BeeLocations.Remove(bee.id);r.Bees.Clear();
   var c=r.Settings;c.interruptPerElapsedMinute=c.commandMaximum=c.feedWeight=c.restWeight=c.recoverWeight=c.storeWeight=c.basalSugar=c.basalEnergy=0;r.ApplySettings(c);
   foreach(var stock in w.Weather.Nectar.Sources)stock.sugar=stock.water=stock.pollen=0;
   var resource=w.Weather.Nectar.Sources[0];resource.sugar=12;resource.water=24;resource.pollen=8;
   CycleBee Returner(bool pollen,double demand){
    var b=r.Add(pos,pos.EquipmentId);b.task=pollen?CycleTask.ForagePollen:CycleTask.ForageNectar;b.returnPoint=new CyclePoint(pos);b.forageSource=0;b.outside=true;b.outsideRemaining=1;
    double capacity=(pollen?c.pollenCarryCapacity:c.carryCapacity)*r.RepresentedBees(b);
    if(pollen)b.cargo.pollen=capacity-demand;else b.cargo.Add(0,capacity-demand);w.BeeLocations.Remove(b.id);return b;
   }
   var a=Returner(false,5);var b=Returner(false,15);var p=Returner(true,5);var q=Returner(true,15);
   r.CalculateMinuteAsync(r.LastMinute+1,CancellationToken.None).GetAwaiter().GetResult();
   Near(a.lastAcquired,3,"represented nectar share 5:15 A");Near(b.lastAcquired,9,"represented nectar share 5:15 B");Near(p.lastAcquired,2,"represented pollen share A");Near(q.lastAcquired,6,"represented pollen share B");
   Near(resource.sugar,0,"finite sugar depleted exactly");Near(resource.water,0,"source water depleted proportionally");Near(resource.pollen,0,"finite pollen depleted exactly");Near(r.Counters.externalSugar,12,"physical sugar accounting");Near(r.Counters.externalPollen,8,"physical pollen accounting");Near(r.Counters.discardedWater,24,"collected water discarded once");
  }finally{w.Thermal?.Dispose();}
  Debug.Log("REPRESENTATIVE FINITE FORAGE TESTS PASS");
 }
}
