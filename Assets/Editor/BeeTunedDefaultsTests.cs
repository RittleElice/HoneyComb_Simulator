using System;using System.Linq;using UnityEngine;
public static class BeeTunedDefaultsTests {
 static void Check(bool ok,string label){if(!ok)throw new Exception(label);}
 public static void Run(){
 var c=Resources.Load<BeeCycleSettingsAsset>("BeeCycleParameters").values;c.Validate();var defaults=new BeeCycleSettings();
 foreach(var f in typeof(BeeCycleSettings).GetFields())if(f.FieldType==typeof(double)||f.FieldType==typeof(int))Check(f.GetValue(c).Equals(f.GetValue(defaults)),"asset/default mismatch "+f.Name);
 Check(c.commandMaximum==0.003&&c.naturalDecay==10&&c.sugarPerBuildPoint==8,"tuned defaults and retained rules");
 var w=new GameWorld(new WeatherSimulator(WeatherSimulator.DefaultSeed,WeatherAssetLoader.LoadBaseline(),null,123456789UL));w.BeeCycle.SeedDemo();
 try{Check(w.BeeCycle.Bees.Count==109,"population preserved");double sugar=0,water=0,pollen=0;int brood=0;
 foreach(var item in ThermalWorld.Items(w))if(item.GetContent<FrameData>() is FrameData f)foreach(var face in new[]{f.Front,f.Back})for(int y=0;y<face.Height;y++)for(int x=0;x<face.Width;x++){sugar+=face.GetTotalSugarMassMg(x,y);water+=face.GetWaterMassMg(x,y);pollen+=face.GetPollenMassMg(x,y);Check(face.GetCapacityFillRatio(x,y)<=1.000001,"cell capacity");if(face.GetCell(x,y).Content==CellContentKind.Brood)brood++;}
 Check(Math.Abs(sugar-42000)<0.01&&Math.Abs(water-10000)<0.01&&Math.Abs(pollen-4000)<0.01&&brood==60,"initial resources and brood");
 var snap=new WorldSaveSnapshot{World=w,UtcTicks=1,TargetMinute=w.CurrentGameMinute};var bytes=WorldSaveCodec.Encode(snap);WorldSaveCodec.Validate(bytes);var restored=WorldSaveCodec.Decode(bytes).World;
 try {Check(restored.BeeCycle.Settings.commandMaximum==0.003&&restored.BeeCycle.Settings.demoSugar==42000,"new settings persisted");}finally{restored.Thermal?.Dispose();}

 var cfg=w.BeeCycle.Settings;cfg.desiredEmptyCellsPerBee=5;w.BeeCycle.ApplySettings(cfg);
 var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;var refresh=typeof(BeeCycleRuntime).GetMethod("RefreshCommands",flags);refresh.Invoke(w.BeeCycle,null);Check(!w.BeeCycle.Commands.Any(x=>x.task==CycleTask.Build),"42g reserve blocks new building at 49g threshold");
 int filled=0;foreach(var r in w.BeeCycle.Regions){for(int y=r.z*4;y<(r.z+1)*4;y++)for(int x=r.x*5;x<(r.x+1)*5;x++)if(filled<5&&r.surface.GetCell(x,y).IsComplete&&r.surface.GetCell(x,y).Content==CellContentKind.Empty){Check(r.surface.TryAddNectar(x,y,0,2000,0),"reserve fixture");filled++;}}Check(filled==5,"five reserve cells");typeof(BeeCycleRuntime).GetMethod("RefreshRegions",flags).Invoke(w.BeeCycle,null);refresh.Invoke(w.BeeCycle,null);Check(w.BeeCycle.Commands.Any(x=>x.task==CycleTask.Build),"52g surplus permits building when needed");
 }finally{w.Thermal.Dispose();}
 var legacy=WorldSaveCodec.Decode(System.IO.File.ReadAllBytes(System.IO.Path.Combine(Application.dataPath,"Editor/ForageFixtures/legacy21-colony.bytes"))).World;
 try{Check(legacy.BeeCycle.Bees.Count==49,"legacy population preserved");Check(legacy.BeeCycle.Settings.commandMaximum==3&&legacy.BeeCycle.Settings.demoSugar==300,"legacy settings not overwritten");}finally{legacy.Thermal?.Dispose();}
 BeeInitialColonyTests.Run();BeeBuildStockGateTests.Run();
 Debug.Log("TUNED DEFAULTS PASS: config agreement, 109 bees, 42g/10g/4g capacity, save/validate/load, legacy unchanged");
 }
}