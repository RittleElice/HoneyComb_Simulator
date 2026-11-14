using System;using System.IO;using System.Linq;using System.Threading;using System.Reflection;using UnityEditor;using UnityEngine;
public static class ReducedGridIntegrationTests {
static int checks;static void Check(bool ok,string message){checks++;if(!ok)throw new Exception(message);}
static void Tick(GameWorld w,long target){GameTimeRuntime.CreateWorldScheduler(w).RunToAsync(target,()=>System.Threading.Tasks.Task.CompletedTask,()=>System.Threading.Tasks.Task.CompletedTask,CancellationToken.None).GetAwaiter().GetResult();}
static void Populate(ItemSlot root,ref int id){var box=root.Item;for(int tier=0;tier<3;tier++){
if(tier>0){box=new ItemData(id++,"super",ItemKind.Super);var slot=new ItemSlot();slot.TryInsert(box);Check(root.TryCombineFrom(slot),"stack");}
for(int i=0;i<10;i++){var data=new FrameData();data.Front.SetAllConstruction(100);data.Back.SetAllConstruction(100);
foreach(var side in new[]{FrameSide.Front,FrameSide.Back})for(int z=0;z<3;z++)for(int x=0;x<5;x++)Check(data.GetSurface(side).TryAddNectar(x*17,z*13,0,40,100),"region nectar");
Check(box.InteriorSlots.GetSlot(i).TryInsert(new ItemData(id++,"frame",ItemKind.Frame,data)),"frame insert");}}
var lid=new ItemSlot();lid.TryInsert(new ItemData(id++,"lid",ItemKind.Lid));Check(root.TryCombineFrom(lid),"lid");}
static void GridChecks(GameWorld w){foreach(var grid in HiveSpatialGridDebugWindow.PlacementGrids(w.Apiary.GetItemSlot(0,0).Value)){
var monitor=new HiveTemperatureMonitor();Check(monitor.Capture(grid,w.CurrentGameMinute,w.Weather.Current.TemperatureCelsius,out var reason),"temperature capture "+reason);
var vapor=new HiveVaporPressureMonitor();Check(vapor.Capture(grid,w),"vapor capture");Check(vapor.Cells.Length==grid.CellCount,"vapor dimensions");
for(int i=0;i<grid.CellCount;i++){monitor.Select(i);Check(monitor.Selected>=0&&monitor.Selected<grid.CellCount,"selection bounds");var v=vapor.At(i);Check(v.Available&&v.TotalWaterKg>=0&&ThermalExchange.Finite(v.TemperatureC),"vapor/water details");}
var l=grid.Layout;for(int axis=0;axis<3;axis++){int depth=axis==0?HiveGridLayout.SizeX:axis==1?HiveGridLayout.SizeY:l.SizeZ;int width=axis==0?HiveGridLayout.SizeY:HiveGridLayout.SizeX;int height=axis==2?HiveGridLayout.SizeY:l.SizeZ;
for(int slice=0;slice<depth;slice++)for(int v=0;v<height;v++)for(int h=0;h<width;h++)if(HiveSpatialGridDebugWindow.TrySliceCell(grid,axis,slice,h,v,out int index,out int span,out Rect rect))Check(index>=0&&index<grid.CellCount&&span>0&&rect.width>0&&rect.height>0,"slice hit geometry");}
}}
[MenuItem("Tools/HoneyComb/검증/Run Reduced Grid Integration Tests")]
public static void Run(){checks=0;string folder=Environment.GetEnvironmentVariable("REDUCED_PRODUCTION_REPORT")??Path.Combine(Path.GetTempPath(),"HoneyComb-ReducedGrid-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);GameWorld w=null,resumed=null;
try{
HiveSpatialGridTests.RunAll();CombGeometryTests.RunAll();CombThermalTests.RunAll();ContentThermalIntegrationTests.Run(ThermalMaterials.Load());
var frame=new ItemData(90000,"mapping",ItemKind.Frame,new FrameData());var fg=frame.EquipmentGrid;Check(fg.SizeX==5&&fg.SizeZ==3&&fg.CellCount==30,"frame dimensions");var counts=new int[30];
foreach(var side in new[]{FrameSide.Front,FrameSide.Back})for(int z=0;z<39;z++)for(int x=0;x<85;x++){var c=CombSpatialMapper.GetContentCell(frame,side,x,z);Check(c.Index==fg.Index(x/17,side==FrameSide.Front?0:1,z/13)&&c.Material==HiveMaterial.Comb,"6630 logical mappings");counts[c.Index]++;}Check(counts.All(n=>n==221),"all regions covered");
frame.ThermalState.Refresh(ThermalMaterials.Load(),20);Check(Math.Abs(frame.ThermalState.WaxMassKg.Sum(v=>(double)v)-.04598)<1e-7,"expanded foundation mass");
w=new GameWorld(new WeatherSimulator(42,WeatherAssetLoader.LoadBaseline()));for(int i=0;i<w.Inventory.Capacity;i++)w.Inventory.GetSlot(i).Take();int id=10000;Populate(w.Apiary.GetItemSlot(0,0),ref id);
w.PrepareThermal(ThermalMaterials.Load());Tick(w,3);Check(w.Thermal.Exchange.CellCount==6244&&w.Thermal.Exchange.FaceCount==17530,"full stack topology");GridChecks(w);
var frames=ThermalWorld.Items(w).Where(i=>i.Kind==ItemKind.Frame).ToArray();foreach(var f in frames)foreach(var side in new[]{FrameSide.Front,FrameSide.Back})for(int z=0;z<3;z++)for(int x=0;x<5;x++){float m=f.GetContent<FrameData>().GetSurface(side).GetWaterMassMg(x*17,z*13);Check(m>0&&m<100,"all30 regions evaporate");}
var snapshot=new WorldSaveSnapshot{World=w,UtcTicks=DateTime.UtcNow.Ticks,TargetMinute=w.CurrentGameMinute};var bytes=WorldSaveCodec.Encode(snapshot);Check(BitConverter.ToInt32(bytes,0)==15,"schema15");var decoded=WorldSaveCodec.Decode(bytes);Check(bytes.SequenceEqual(WorldSaveCodec.Encode(decoded)),"exact save roundtrip");resumed=decoded.World;
var store=new WorldSaveStore(Path.Combine(folder,"save-test"));store.Save(bytes);Check(store.Load(out bool recovered).World.CurrentGameMinute==3&&!recovered,"store reload");
foreach(int old in new[]{9,10,11,12}){var bad=(byte[])bytes.Clone();Array.Copy(BitConverter.GetBytes(old),bad,4);bool rejected=false;try{WorldSaveCodec.Decode(bad);}catch(InvalidDataException){rejected=true;}Check(rejected,"old geometry fails closed");}
resumed.Weather.BindBaseline(WeatherAssetLoader.LoadBaseline());resumed.PrepareThermal(ThermalMaterials.Load());Tick(w,4);Tick(resumed,4);var e=w.Thermal.Exchange;var other=resumed.Thermal.Exchange;double maxT=0;
for(int i=0;i<e.CellCount;i++){var a=e.GetCell(i);var b=other.GetCell(i);maxT=Math.Max(maxT,Math.Abs(a.EnergyJ/a.HeatCapacityJPerK-b.EnergyJ/b.HeatCapacityJPerK));}Check(maxT<.002,"resume temperature "+maxT);GridChecks(resumed);
var box=ThermalWorld.Items(resumed).First(i=>i.Kind==ItemKind.BroodBox);var moved=box.InteriorSlots.GetSlot(0).Item;Check(box.InteriorSlots.GetSlot(0).TryMoveTo(resumed.Inventory.GetSlot(0)),"detach frame");Tick(resumed,5);Check(resumed.Inventory.GetSlot(0).TryMoveTo(box.InteriorSlots.GetSlot(0)),"replace frame");Tick(resumed,6);GridChecks(resumed);
resumed.Thermal.ObserveMoistureFlow("test",moved.ThermalState);Tick(resumed,7);Check(resumed.Thermal.TryReadMoistureFlow(moved.ThermalState,0,out var flow)&&flow!=null,"flow inspector");
for(int tick=8;tick<=60;tick++)Tick(resumed,tick);Check(resumed.Thermal.Failure==null,"60 ticks finite");
var final=WorldSaveCodec.Encode(new WorldSaveSnapshot{World=resumed,UtcTicks=DateTime.UtcNow.Ticks,TargetMinute=60});Check(final.SequenceEqual(WorldSaveCodec.Encode(WorldSaveCodec.Decode(final))),"post-move roundtrip");
File.WriteAllBytes(Path.Combine(folder,"new-game-test.bin"),final);File.WriteAllText(Path.Combine(folder,"validation.txt"),"PASS "+checks+" checks; schema15 exact saves and resume, old schema rejection, 6630 mappings, all30 evaporation regions, 60 live ticks, frame moves, all Hive Grid slices/selections, temperature/vapor/water/flow inspection. Max resume temperature delta="+maxT+" C. Native UI validation is separate.\n");Debug.Log("REDUCED GRID INTEGRATION PASSED "+checks);
}finally{w?.Thermal?.Dispose();resumed?.Thermal?.Dispose();}}
}
