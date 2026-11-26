using System;using System.IO;using System.Linq;using System.Reflection;using UnityEngine;
public static class BeeRepresentativeTests {
 static void Check(bool v,string s){if(!v)throw new Exception(s);}
 static void Near(double a,double b,string s){Check(Math.Abs(a-b)<1e-8*Math.Max(1,Math.Abs(b)),s+" "+a+" != "+b);}
 static object Call(BeeCycleRuntime r,string name,params object[] args)=>typeof(BeeCycleRuntime).GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(r,args);
 public static void Run(){
 var c=Resources.Load<BeeCycleSettingsAsset>("BeeCycleParameters").values;c.Validate();
 foreach(var f in typeof(BeeCycleSettings).GetFields())if(f.FieldType==typeof(double)||f.FieldType==typeof(int))Check(f.GetValue(c).Equals(f.GetValue(new BeeCycleSettings())),"asset/default "+f.Name);
 var a=new GameWorld(new WeatherSimulator(WeatherSimulator.DefaultSeed,WeatherAssetLoader.LoadBaseline(),null,123456789UL));
 var z=new GameWorld(new WeatherSimulator(WeatherSimulator.DefaultSeed,WeatherAssetLoader.LoadBaseline(),null,123456789UL));
 try{
 a.BeeCycle.SeedDemo(new BeeCycleSettings{representativeScale=1});z.BeeCycle.SeedDemo();double k=z.BeeCycle.Settings.representativeScale;
 Check(z.BeeCycle.Bees.Count==109,"48 workers + queen + 60 brood");
 foreach(bool queen in new[]{false,true}){
 var x=a.BeeCycle.Bees.First(b=>b.stage==CycleStage.Adult&&b.queen==queen);var y=z.BeeCycle.Bees.First(b=>b.stage==CycleStage.Adult&&b.queen==queen);double f=queen?1:k;
 Near(z.BeeCycle.ReserveTarget(y),a.BeeCycle.ReserveTarget(x)*f,"reserve");Near(z.BeeCycle.CargoFree(y,false),a.BeeCycle.CargoFree(x,false)*f,"sugar capacity");Near(z.BeeCycle.CargoFree(y,true),a.BeeCycle.CargoFree(x,true)*f,"pollen capacity");
 x.cargo.Add(0,10);y.cargo.Add(0,10*f);Near(z.BeeCycle.CargoRatio(y),a.BeeCycle.CargoRatio(x),"desire fraction");
 x.energy=y.energy=50;x.reserve=8;y.reserve=8*f;double oldX=x.reserve,oldY=y.reserve;Call(a.BeeCycle,"Rest",x,1.0,true);Call(z.BeeCycle,"Rest",y,1.0,true);Near(x.energy,y.energy,"energy unchanged");Near((oldY-y.reserve),(oldX-x.reserve)*f,"rest physical consumption");Near(x.development,y.development,"age unchanged");
 double hx=a.BeeCycle.Counters.heatJ,hy=z.BeeCycle.Counters.heatJ;Call(a.BeeCycle,"BaseHeat",x);Call(z.BeeCycle,"BaseHeat",y);Near(z.BeeCycle.Counters.heatJ-hy,(a.BeeCycle.Counters.heatJ-hx)*f,"heat scale and queen exemption");
 double m=(double)Call(z.BeeCycle,"BeeAmount",y,"feedAmount",new CycleInputs{time=1,amount=0.5*f});Near(m,0.5*f,"limited feed amount");
 }
 foreach(var r in a.BeeCycle.Regions)r.temperature=34;foreach(var r in z.BeeCycle.Regions)r.temperature=34;
 var la=a.BeeCycle.Bees.First(b=>b.stage==CycleStage.Larva);var lz=z.BeeCycle.Bees.First(b=>b.stage==CycleStage.Larva);la.broodSugar=la.broodPollen=10;lz.broodSugar=lz.broodPollen=10*k;la.energy=lz.energy=50;
 Call(a.BeeCycle,"DevelopBrood",la);Call(z.BeeCycle,"DevelopBrood",lz);Near(la.development,lz.development,"brood timing");Near(la.energy,lz.energy,"brood normalized energy");Near(lz.broodSugar,la.broodSugar*k,"brood sugar");Near(lz.broodPollen,la.broodPollen*k,"brood pollen");
 var bytes=WorldSaveCodec.Encode(new WorldSaveSnapshot{World=z,UtcTicks=1,TargetMinute=z.CurrentGameMinute});WorldSaveCodec.Validate(bytes);var restored=WorldSaveCodec.Decode(bytes).World;
 try{Near(restored.BeeCycle.Settings.representativeScale,k,"scale persisted");Near(restored.BeeCycle.Bees.First(b=>b.id==lz.id).broodSugar,lz.broodSugar,"physical mass persisted");}finally{restored.Thermal?.Dispose();}
 var old=WorldSaveCodec.Decode(File.ReadAllBytes(Path.GetFullPath(Path.Combine(Application.dataPath,"Editor/ForageFixtures/representative-v7-day0.bytes")))).World;
 try{Check(old.BeeCycle.Settings.broodThermalCoupling==0,"legacy heat location unchanged");Near(old.BeeCycle.Settings.pupaBasalHeatJ,old.BeeCycle.Settings.basalHeatJ,"legacy heat quantity unchanged");Near(old.BeeCycle.Settings.representativeScale,1,"v7 legacy unchanged");Near(old.BeeCycle.Settings.demoSugar,42000,"legacy food unchanged");Check(old.BeeCycle.Settings.broodTarget==60,"legacy goal unchanged");}finally{old.Thermal?.Dispose();}
 }finally{a.Thermal?.Dispose();z.Thermal?.Dispose();}
 BeeTunedDefaultsTests.Run();BeeRepresentativeThermalTests.Run();BeeRepresentativeForageTests.Run();
 Debug.Log("REPRESENTATIVE SCALE TESTS PASS");
 }
 public static void RunAndExit(){try{Run();UnityEditor.EditorApplication.Exit(0);}catch(Exception e){Debug.LogException(e);UnityEditor.EditorApplication.Exit(1);}}
}
