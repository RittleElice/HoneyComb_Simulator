using System;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
public static class BeeBuildReserveTests {
 public static void Run(){
  BeeBuildStockGateTests.Run();
  var w=new GameWorld(new WeatherSimulator(WeatherSimulator.DefaultSeed,WeatherAssetLoader.LoadBaseline(),null,123456789UL));w.BeeCycle.SeedDemo();
  try {
   var cfg=w.BeeCycle.Settings;cfg.buildStockSugarPerBee=31;w.BeeCycle.ApplySettings(cfg);
   var snapshot=new WorldSaveSnapshot{World=w,UtcTicks=1,TargetMinute=w.CurrentGameMinute};
   var compact=WorldSaveCodec.Encode(snapshot);
   var asyncBytes=WorldSaveCodec.EncodeAsync(snapshot,()=>Task.CompletedTask).GetAwaiter().GetResult();
   if(!compact.SequenceEqual(asyncBytes))throw new Exception("async save differs");
   foreach(var bytes in new[]{compact,WorldSaveCodec.EncodeLegacy(snapshot)}){
    WorldSaveCodec.Validate(bytes);var restored=WorldSaveCodec.Decode(bytes).World;
    try {if(restored.BeeCycle.Settings.buildStockSugarPerBee!=31||restored.BeeCycle.Settings.stockSugarPerBee!=12)throw new Exception("separate thresholds roundtrip");} finally {restored.Thermal?.Dispose();}
   }
   Debug.Log("BUILD RESERVE SAVE PASS: compact/reflected/async, independent thresholds");
  } finally {w.Thermal.Dispose();}
 }
}
