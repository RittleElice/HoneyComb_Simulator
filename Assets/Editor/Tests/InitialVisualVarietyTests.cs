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
   var actualNectar=new List<string>();var expectedNectar=new List<string>();var actualPollen=new List<int>();var expectedPollen=new List<int>();int mixedNectar=0,mixedPollen=0;
   var sugars=new List<KeyValuePair<int,float>>();
   for(int y=0;y<face.Height;y++)for(int x=0;x<face.Width;x++){
    var c=face.GetCell(x,y);double s=face.GetTotalSugarMassMg(x,y),w=face.GetWaterMassMg(x,y);sugar+=s;water+=w;pollen+=face.GetPollenMassMg(x,y);
    if(c.Content==CellContentKind.Pollen){int fill=(int)Math.Round(face.GetCapacityFillRatio(x,y)*100);actualPollen.Add(fill);expectedPollen.Add(10+20*(x%5));if(fill!=10+20*(x%5))mixedPollen++;Check(face.GetPollenAppearanceSeed(x,y)!=0,"pollen appearance seed exists");}
    if(c.Content==CellContentKind.Nectar){nectar++;double fill=.1+.2*(x%5),moisture=new[]{.3,.4,.5,.65,.8}[(face.Height-1-y)%5];face.CopyNectarSugars(x,y,sugars);int source=sugars.Single().Key;string actual=$"{source}:{Math.Round(face.GetCapacityFillRatio(x,y)*100)}:{Math.Round(w/(w+s)*100)}";string expected=$"{Math.Min(4,x*5/face.Width)}:{Math.Round(fill*100)}:{Math.Round(moisture*100)}";actualNectar.Add(actual);expectedNectar.Add(expected);if(actual!=expected)mixedNectar++;}
    if(c.Content==CellContentKind.Brood&&runtime.TryGetObservationDevelopment(face.GetBeeId(x,y),out var stage,out var progress)){if(stage==CycleStage.Larva)larva.Add(progress);if(stage==CycleStage.Pupa)pupa.Add(progress);}
   }
   Check(actualNectar.OrderBy(v=>v).SequenceEqual(expectedNectar.OrderBy(v=>v)),"nectar recipe distribution preserved");Check(actualPollen.OrderBy(v=>v).SequenceEqual(expectedPollen.OrderBy(v=>v)),"pollen fill distribution preserved");Check(mixedNectar>actualNectar.Count/2&&mixedPollen>actualPollen.Count/2,"visibly mixed instead of ordered bands");
   Check(!larva.SequenceEqual(larva.OrderBy(v=>v))&&!pupa.SequenceEqual(pupa.OrderBy(v=>v)),"brood progress mixed on each face");
   foreach(var list in new[]{larva,pupa}){list.Sort();Check(list.Count==5,"five per stage per face");for(int i=0;i<5;i++)Check(Math.Abs(list[i]-progressLevels[i])<1e-9,"five development levels");}
  }
  Check(nectar==524,"nectar layout unchanged");Check(Math.Abs(sugar-runtime.Settings.demoSugar)<.01&&Math.Abs(water-runtime.Settings.demoWater)<.01,"configured initial totals");Check(Math.Abs(pollen-runtime.Settings.demoPollen)<.01,"configured pollen total");
  foreach(var stage in new[]{CycleStage.Egg,CycleStage.Larva,CycleStage.Pupa})Check(runtime.Bees.Count(b=>b.stage==stage)==20,"brood counts unchanged");
  var snapshot=new WorldSaveSnapshot{World=world,UtcTicks=1};var bytes=WorldSaveCodec.Encode(snapshot);var decoded=WorldSaveCodec.Decode(bytes);restored=decoded.World;Check(bytes.SequenceEqual(WorldSaveCodec.Encode(decoded)),"initial variety survives exact save roundtrip");
  var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Item/InteriorSlot/Frame/PF_Frame_Hand.prefab");typeof(BroodSpriteTests).GetMethod("Render",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{world,prefab,output});
  File.WriteAllText(output+"/result.txt",$"PASS {checks} initial palette, all-face brood progress, quantities and saved-state checks. Sugar={sugar:R}mg Water={water:R}mg Pollen={pollen:R}mg.");
  world.Thermal?.Dispose();world=null;restored.Thermal?.Dispose();restored=null;
  BeeInitialColonyTests.Run();InitialNectarPaletteTests.Run();
 }catch(Exception e){File.WriteAllText(output+"/result.txt","FAIL "+e);Debug.LogException(e);EditorApplication.Exit(1);}finally{world?.Thermal?.Dispose();restored?.Thermal?.Dispose();}}
}
