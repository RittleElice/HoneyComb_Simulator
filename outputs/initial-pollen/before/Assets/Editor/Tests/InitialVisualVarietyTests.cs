using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
public static class InitialVisualVarietyTests
{
 static int checks;static void Check(bool ok,string msg){checks++;if(!ok)throw new Exception(msg);}
 public static void Run(){const string output="outputs/initial-variety";Directory.CreateDirectory(output);GameWorld world=null,restored=null;
 try{
  world=new GameWorld(new WeatherSimulator(WeatherSimulator.DefaultSeed,WeatherAssetLoader.LoadBaseline()));world.BeeCycle.SeedDemo();var runtime=world.BeeCycle;
  var frames=ThermalWorld.Items(world).Where(i=>i.Kind==ItemKind.Frame&&i.GetContent<FrameData>().Front.CountCompletedCells()>0).ToArray();Check(frames.Length==2,"two initially populated frames");
  double sugar=0,water=0,pollen=0;int nectar=0;var progressLevels=new[]{0,.25,.5,.75,.95};
  foreach(var item in frames)foreach(var face in new[]{item.GetContent<FrameData>().Front,item.GetContent<FrameData>().Back}){
   var larva=new List<double>();var pupa=new List<double>();
   for(int y=0;y<face.Height;y++)for(int x=0;x<face.Width;x++){
    var c=face.GetCell(x,y);double s=face.GetTotalSugarMassMg(x,y),w=face.GetWaterMassMg(x,y);sugar+=s;water+=w;pollen+=face.GetPollenMassMg(x,y);
    if(c.Content==CellContentKind.Nectar){nectar++;double fill=.1+.2*(x%5),moisture=new[]{.3,.4,.5,.65,.8}[(face.Height-1-y)%5];Check(Math.Abs(face.GetCapacityFillRatio(x,y)-fill)<.0001,"target nectar fill");Check(Math.Abs(w/(w+s)-moisture)<.0001,"target water mass fraction");}
    if(c.Content==CellContentKind.Brood&&runtime.TryGetObservationDevelopment(face.GetBeeId(x,y),out var stage,out var progress)){if(stage==CycleStage.Larva)larva.Add(progress);if(stage==CycleStage.Pupa)pupa.Add(progress);}
   }
   foreach(var list in new[]{larva,pupa}){list.Sort();Check(list.Count==5,"five per stage per face");for(int i=0;i<5;i++)Check(Math.Abs(list[i]-progressLevels[i])<1e-9,"five development levels");}
  }
  Check(nectar==524,"nectar layout unchanged");Check(Math.Abs(sugar-runtime.Settings.demoSugar)<.01&&Math.Abs(water-runtime.Settings.demoWater)<.01,"configured initial totals");Check(Math.Abs(pollen-runtime.Settings.demoPollen)<.01,"pollen total unchanged");
  foreach(var stage in new[]{CycleStage.Egg,CycleStage.Larva,CycleStage.Pupa})Check(runtime.Bees.Count(b=>b.stage==stage)==20,"brood counts unchanged");
  var snapshot=new WorldSaveSnapshot{World=world,UtcTicks=1};var bytes=WorldSaveCodec.Encode(snapshot);var decoded=WorldSaveCodec.Decode(bytes);restored=decoded.World;Check(bytes.SequenceEqual(WorldSaveCodec.Encode(decoded)),"initial variety survives exact save roundtrip");
  var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Item/InteriorSlot/Frame/PF_Frame_Hand.prefab");typeof(BroodSpriteTests).GetMethod("Render",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{world,prefab,output});
  File.WriteAllText(output+"/result.txt",$"PASS {checks} initial palette, all-face brood progress, quantities and saved-state checks. Sugar={sugar:R}mg Water={water:R}mg Pollen={pollen:R}mg.");
  world.Thermal?.Dispose();world=null;restored.Thermal?.Dispose();restored=null;
  BeeInitialColonyTests.Run();InitialNectarPaletteTests.Run();
 }catch(Exception e){File.WriteAllText(output+"/result.txt","FAIL "+e);Debug.LogException(e);EditorApplication.Exit(1);}finally{world?.Thermal?.Dispose();restored?.Thermal?.Dispose();}}
}
