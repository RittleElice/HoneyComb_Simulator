using System;
using System.IO;
using System.Linq;
using UnityEngine;
public static class BeeInitialColonyTests {
 static void Check(bool ok,string label){if(!ok)throw new Exception(label);}
 static byte[] Save(GameWorld w)=>WorldSaveCodec.Encode(new WorldSaveSnapshot{World=w,UtcTicks=1,TargetMinute=w.CurrentGameMinute});
 public static void Run(){
  var w=new GameWorld(new WeatherSimulator(WeatherSimulator.DefaultSeed,WeatherAssetLoader.LoadBaseline(),null,123456789UL));w.BeeCycle.SeedDemo(new BeeCycleSettings{demoSugar=5000,demoWater=5000,demoPollen=1000});
  try{
   Check(w.BeeCycle.Bees.Count==109,"109 initial bees");Check(w.BeeCycle.Bees.Count(b=>b.stage==CycleStage.Adult)==49,"49 adults");
   foreach(var stage in new[]{CycleStage.Egg,CycleStage.Larva,CycleStage.Pupa})Check(w.BeeCycle.Bees.Count(b=>b.stage==stage)==20,"20 each brood stage");
   double sugar=0,water=0,pollen=0;int unbuilt=0,complete=0,brood=0;
   foreach(var item in ThermalWorld.Items(w))if((item.Id==w.Apiary.GetItemSlot(0,0).Item.InteriorSlots.GetSlot(2).Item.Id||item.Id==w.Apiary.GetItemSlot(0,0).Item.InteriorSlots.GetSlot(3).Item.Id)&&item.GetContent<FrameData>() is FrameData f)foreach(var face in new[]{f.Front,f.Back})for(int y=0;y<face.Height;y++)for(int x=0;x<face.Width;x++){
    var cell=face.GetCell(x,y);if(cell.IsComplete)complete++;else{Check(cell.Construction==0,"unfinished starts0");unbuilt++;}
    sugar+=face.GetTotalSugarMassMg(x,y);water+=face.GetWaterMassMg(x,y);pollen+=face.GetPollenMassMg(x,y);Check(face.GetCapacityFillRatio(x,y)<=1.000001,"cell capacity respected");
    if(cell.Content==CellContentKind.Brood){brood++;long id=face.GetBeeId(x,y);var b=w.BeeCycle.Bees.Single(z=>z.id==id);Check(b.broodColumn==x&&b.broodRow==y,"brood cell mapping");Check(w.BeeLocations.TryGet(id,out var pos)&&pos.EquipmentId==item.Id,"brood location");}
   }
   Check(Math.Abs(sugar-5000)<0.001&&Math.Abs(water-5000)<0.001&&Math.Abs(pollen-1000)<0.001,"initial stored masses");Check(complete==80&&unbuilt==1120&&brood==60,"initial cell counts");
   w.BeeCycle.SeedDemo(new BeeCycleSettings{demoSugar=5000,demoWater=5000,demoPollen=1000});Check(w.BeeCycle.Bees.Count==109,"seed idempotent");var data=Save(w);var restored=WorldSaveCodec.Decode(data).World;
   try{restored.Weather.BindBaseline(WeatherAssetLoader.LoadBaseline());restored.PrepareThermal(ThermalMaterials.Load());Check(data.SequenceEqual(Save(restored)),"initial save exact roundtrip");}finally{restored.Thermal?.Dispose();}
  }finally{w.Thermal.Dispose();}
  var legacy=WorldSaveCodec.Decode(File.ReadAllBytes(Path.Combine(Application.dataPath,"Editor/ForageFixtures/legacy21-colony.bytes"))).World;
  try{Check(legacy.BeeCycle.Bees.Count==49,"old population retained");Check(legacy.BeeCycle.Settings.demoEggs==0&&legacy.BeeCycle.Settings.demoLarvae==0&&legacy.BeeCycle.Settings.demoPupae==0,"old initial settings retained");}finally{legacy.Thermal?.Dispose();}
  Debug.Log("INITIAL COLONY TESTS PASS");
 }
 public static void RunAll(){Run();BeeCargoSplitTests.Run();}
}
