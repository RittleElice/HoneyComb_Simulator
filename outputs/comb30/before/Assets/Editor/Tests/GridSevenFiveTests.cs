using System;
using System.IO;
using System.Linq;
using System.Threading;
using UnityEngine;
using UnityEditor;
public static class GridSevenFiveTests {
 static void Check(bool b,string s){if(!b)throw new Exception(s);}
 public static void Run(){
 string folder=Path.GetFullPath("outputs/grid-5x3");Directory.CreateDirectory(folder);
 try{
 var m=ThermalMaterials.Load();BroodEntranceTests.Run(m);HiveSpatialGridTests.RunAll();CombThermalTests.RunAll();CombGeometryTests.RunAll();WorldSaveTests.Run();
 ContentThermalIntegrationTests.Run(m);ExteriorThermalTests.Run(m);PersistentThermalTests.Run(m);EffectiveThermalTests.Run(m);
 var frame=new ItemData(80000,"mapping",ItemKind.Frame,new FrameData());var f=frame.GetContent<FrameData>();var g=frame.EquipmentGrid;
 var counts=new int[g.CellCount];
 foreach(var side in new[]{FrameSide.Front,FrameSide.Back})for(int z=0;z<39;z++)for(int x=0;x<85;x++){
  var c=CombSpatialMapper.GetContentCell(frame,side,x,z);counts[c.Index]++;
  Check(c.Index==g.Index(1+x/17,side==FrameSide.Front?0:1,1+z/13),"mapping identity");
 }
 Check(counts.Count(n=>n==221)==30&&counts.Sum()==6630,"mapping coverage");
 frame.ThermalState.Refresh(m,20);Check(Math.Abs(frame.ThermalState.WaxMassKg.Sum(v=>(double)v)-.0399)<1e-7,"foundation mass");
 f.Front.SetAllConstruction(100);f.Back.SetAllConstruction(100);frame.ThermalState.Refresh(m,20);
 double expected=.0399;foreach(var side in new[]{FrameSide.Front,FrameSide.Back})for(int z=0;z<39;z++)for(int x=0;x<85;x++)expected+=ThermalMath.WaxMassKg(f.GetSurface(side).GetCell(x,z));
 Check(Math.Abs(frame.ThermalState.WaxMassKg.Sum(v=>(double)v)-expected)<1e-5,"built wax conserved");
 var w=new GameWorld(new WeatherSimulator(42,WeatherAssetLoader.LoadBaseline()));w.PrepareThermal(m);w.Thermal.PrepareExchange();int cells=w.Thermal.Exchange.CellCount,faces=w.Thermal.Exchange.FaceCount;
 var box=ThermalWorld.Items(w).First(i=>i.Kind==ItemKind.BroodBox);Check(box.InteriorSlots.GetSlot(0).TryInsert(frame),"insert");
 var sw=new System.Diagnostics.Stopwatch();double total=0;
 for(int n=1;n<=1440;n++){
  if(n==480)Check(box.InteriorSlots.GetSlot(0).TryMoveTo(box.InteriorSlots.GetSlot(3)),"move");
  if(n==960)Check(box.InteriorSlots.GetSlot(3).TryMoveTo(box.InteriorSlots.GetSlot(0)),"return");
  sw.Restart();w.Thermal.CalculateMinuteAsync(n,CancellationToken.None).GetAwaiter().GetResult();sw.Stop();total+=sw.Elapsed.TotalMilliseconds;w.Weather.Advance(n);
  var e=w.Thermal.Exchange;for(int i=0;i<e.CellCount;i++){var c=e.GetCell(i);Check(ThermalExchange.Finite(c.Owner.EnergyJ[c.Index]),"finite energy");}
 }
 var fresh=new GameWorld(new WeatherSimulator(20261031,WeatherAssetLoader.LoadBaseline()));fresh.PrepareThermal(m);
 var snapshot=new WorldSaveSnapshot{World=fresh,UtcTicks=DateTime.UtcNow.Ticks,TargetMinute=0};var bytes=WorldSaveCodec.Encode(snapshot);var loaded=WorldSaveCodec.Decode(bytes);loaded.World.PrepareThermal(m);Check(bytes.SequenceEqual(WorldSaveCodec.Encode(loaded)),"schema9 roundtrip");
 var old=(byte[])bytes.Clone();old[0]=8;bool rejected=false;try{WorldSaveCodec.Decode(old);}catch(InvalidDataException){rejected=true;}Check(rejected,"old schema rejected");
 new WorldSaveStore(Path.Combine(folder,"new-save-schema9")).Save(bytes);
 File.WriteAllText(Path.Combine(folder,"results.txt"),"PASS geometry, comb capacity, content integration, exterior, persistent buffers, effective solver, 6630 mapping identities, foundation/built wax conservation, movement, 1440 finite ticks, schema9 exact roundtrip, schema8 rejection.\nDefault cells="+cells+" faces="+faces+"\n1440 ticks including inserted frame/moves mean ms="+(total/1440)+" (not an A/B benchmark).\nFresh minute0 save prepared in new-save; user save untouched by test.");
 ThermalExchange.ReleaseAllBuffers();EditorApplication.Exit(0);
 }catch(Exception ex){File.WriteAllText(Path.Combine(folder,"results.txt"),ex.ToString());Debug.LogException(ex);ThermalExchange.ReleaseAllBuffers();EditorApplication.Exit(1);}
 }
}


