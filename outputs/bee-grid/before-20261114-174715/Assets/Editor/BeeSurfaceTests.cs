using System;using System.IO;using System.Linq;using System.Threading;using UnityEngine;
public static class BeeSurfaceTests{
static int checks;static void Check(bool b,string s){checks++;if(!b)throw new Exception(s);}
static void Tick(GameWorld w,int n)=>GameTimeRuntime.CreateWorldScheduler(w).RunToAsync(n,()=>System.Threading.Tasks.Task.CompletedTask,()=>System.Threading.Tasks.Task.CompletedTask,CancellationToken.None).GetAwaiter().GetResult();
static byte[] Save(GameWorld w)=>WorldSaveCodec.Encode(new WorldSaveSnapshot{World=w,UtcTicks=1,TargetMinute=w.CurrentGameMinute});
[UnityEditor.MenuItem("Tools/HoneyComb/검증/Run Bee Surface Tests")]
public static void Run(){var w=new GameWorld(new WeatherSimulator(42,WeatherAssetLoader.LoadBaseline()));GameWorld restored=null;try{
for(int i=0;i<w.Inventory.Capacity;i++)w.Inventory.GetSlot(i).Take();
var empty=Save(w);Check(BitConverter.ToInt32(empty,0)==14,"schema14");var old=empty.Take(empty.Length-4).ToArray();Array.Copy(BitConverter.GetBytes(13),old,4);Check(WorldSaveCodec.Decode(old).World.BeeLocations.Count==0,"schema13 compatibility");
var root=w.Apiary.GetItemSlot(0,0);var box=root.Item;var frame=new ItemData(10000,"test frame",ItemKind.Frame,new FrameData());Check(box.InteriorSlots.GetSlot(2).TryInsert(frame),"insert");
var upper=new ItemData(10001,"super",ItemKind.Super);var tmp=new ItemSlot();tmp.TryInsert(upper);Check(root.TryCombineFrom(tmp),"stack");
for(int side=0;side<2;side++)for(int z=0;z<3;z++)for(int x=0;x<5;x++){
var pos=new BeeSurfacePosition(frame.Id,side==0?FrameSide.Front:FrameSide.Back,x*88+44,z*(220d/3)+110d/3);
w.BeeLocations.Set(w,1+side*15+z*5+x,pos);Check(BeeSurfaceEnvironmentReader.TryResolveAir(w,pos,out var grid,out int index),"resolve");grid.Layout.Coordinates(index,out int gx,out int gy,out int gz);Check(gx==x+2&&gz==z+grid.Layout.SlotBottom&&gy==9+(side==0?0:3),"correct face air");Check(grid.GetCell(index).Material==HiveMaterial.Air,"air not comb");}
bool invalid=false;try{w.BeeLocations.Set(w,99,new BeeSurfacePosition(frame.Id,FrameSide.Front,440,0));}catch(ArgumentException){invalid=true;}Check(invalid,"edge rejected");
w.PrepareThermal(ThermalMaterials.Load());Tick(w,1);
foreach(var pair in w.BeeLocations.Entries){Check(BeeSurfaceEnvironmentReader.TryRead(w,pair.Key,out var e)&&e.Kind==BeeSurfaceEnvironmentKind.AdjacentAir,"read air");Check(!double.IsNaN(e.TemperatureC)&&!double.IsNaN(e.RelativeHumidityPercent),"finite climate");}
var bytes=Save(w);restored=WorldSaveCodec.Decode(bytes).World;Check(bytes.SequenceEqual(Save(restored)),"exact location save");Check(restored.BeeLocations.Count==30,"all saved");
Check(box.InteriorSlots.GetSlot(2).TryMoveTo(w.Hand.Slot),"hand");Check(BeeSurfaceEnvironmentReader.TryRead(w,1,out var outside)&&outside.Kind==BeeSurfaceEnvironmentKind.Exterior,"detached exterior");Check(outside.TemperatureC==w.Weather.Current.TemperatureCelsius,"outside temperature");
Check(w.Hand.Slot.TryMoveTo(upper.InteriorSlots.GetSlot(7)),"new tier slot");Check(!BeeSurfaceEnvironmentReader.TryRead(w,1,out _),"no stale climate after move");Tick(w,2);Check(BeeSurfaceEnvironmentReader.TryRead(w,1,out var moved)&&moved.BoxId==upper.Id,"follows new box");
upper.SpatialGrid.Layout.Coordinates(moved.CellIndex,out _,out int y,out _);Check(y==29,"follows new slot");
Check(upper.InteriorSlots.GetSlot(7).TryMoveTo(w.Warehouse.GetSlot(0)),"warehouse");Check(BeeSurfaceEnvironmentReader.TryRead(w,1,out var stored)&&stored.Kind==BeeSurfaceEnvironmentKind.Exterior,"warehouse ambient");
Check(w.Warehouse.GetSlot(0).TryMoveTo(w.Trash.GetSlot(0)),"trash");Check(!w.TryEmptyTrash(out _)&&w.Trash.GetSlot(0).Item==frame,"prevent orphan deletion");
Check(w.Trash.GetSlot(0).TryMoveTo(box.InteriorSlots.GetSlot(0)),"return");Tick(w,3);Check(BeeSurfaceEnvironmentReader.TryRead(w,1,out var back)&&back.BoxId==box.Id,"reattached");
var after=Save(w);var r=WorldSaveCodec.Decode(after).World;Check(r.BeeLocations.TryGet(1,out var p)&&p.FrameId==frame.Id&&p.Side==FrameSide.Front&&p.Xmm==44,"identity persists after moves");
restored.Weather.BindBaseline(WeatherAssetLoader.LoadBaseline());restored.PrepareThermal(ThermalMaterials.Load());Tick(restored,2);Check(BeeSurfaceEnvironmentReader.TryRead(restored,1,out _),"restored environment");
File.WriteAllText(Environment.GetEnvironmentVariable("BEE_REPORT"),"PASS "+checks+" checks: both surfaces/all15regions, adjacent air, hand/warehouse/tier/slot moves, stale rejection, schema13 read/schema14 roundtrip, deletion guard, resumed tick.");Debug.Log("BEE SURFACE PASS "+checks);
}finally{w.Thermal?.Dispose();restored?.Thermal?.Dispose();}}
}
