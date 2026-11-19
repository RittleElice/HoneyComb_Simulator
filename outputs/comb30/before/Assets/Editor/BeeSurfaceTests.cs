using System;
using System.IO;
using System.Linq;
using System.Threading;
using UnityEngine;

public static class BeeSurfaceTests
{
    static int checks;
    static void Check(bool value,string label) { checks++; if(!value) throw new Exception(label); }
    static byte[] Save(GameWorld w)=>WorldSaveCodec.Encode(new WorldSaveSnapshot{World=w,UtcTicks=1,TargetMinute=w.CurrentGameMinute});
    static void Tick(GameWorld w,int minute)=>GameTimeRuntime.CreateWorldScheduler(w).RunToAsync(minute,()=>System.Threading.Tasks.Task.CompletedTask,()=>System.Threading.Tasks.Task.CompletedTask,CancellationToken.None).GetAwaiter().GetResult();
    static bool Edge(GameWorld w,BeeGridPosition a,BeeGridPosition b)=>w.BeeLocations.GetNeighbours(w,a).Any(m=>m.Target.Equals(b));
    static void Reject(Action action,string label) { bool rejected=false;try{action();}catch(ArgumentException){rejected=true;}Check(rejected,label); }
    [UnityEditor.MenuItem("Tools/HoneyComb/검증/Run Bee Surface Tests")]
    public static void Run()
    {
        checks=0;var w=new GameWorld(new WeatherSimulator(42,WeatherAssetLoader.LoadBaseline())); GameWorld restored=null;
        try
        {
            for(int i=0;i<w.Inventory.Capacity;i++) w.Inventory.GetSlot(i).Take();
            var empty=Save(w);Check(BitConverter.ToInt32(empty,0)==15,"schema15");
            var old=empty.Take(empty.Length-4).ToArray();Array.Copy(BitConverter.GetBytes(13),old,4);
            Check(WorldSaveCodec.Decode(old).World.BeeLocations.Count==0,"schema13 compatibility");
            var root=w.Apiary.GetItemSlot(0,0);var box=root.Item;
            var a=new ItemData(10000,"frame A",ItemKind.Frame,new FrameData());
            var b=new ItemData(10001,"frame B",ItemKind.Frame,new FrameData());
            var upper=new ItemData(10002,"super",ItemKind.Super);
            Check(box.InteriorSlots.GetSlot(2).TryInsert(a),"insert A");Check(box.InteriorSlots.GetSlot(3).TryInsert(b),"insert B");
            var tmp=new ItemSlot();tmp.TryInsert(upper);Check(root.TryCombineFrom(tmp),"stack");
            // A real v14 payload layout: old positions appended to an otherwise identical world.
            var baseSave=Save(w);
            using(var stream=new MemoryStream())
            {
                stream.Write(baseSave,0,baseSave.Length-4);
                using(var writer=new BinaryWriter(stream,System.Text.Encoding.UTF8,true))
                {
                    writer.Write(2);
                    writer.Write(101L);writer.Write(a.Id);writer.Write((int)FrameSide.Front);writer.Write(44d);writer.Write(110d/3);
                    writer.Write(102L);writer.Write(a.Id);writer.Write((int)FrameSide.Back);writer.Write(439.9d);writer.Write(219.9d);
                }
                var legacy=stream.ToArray();Array.Copy(BitConverter.GetBytes(14),legacy,4);
                var converted=WorldSaveCodec.Decode(legacy).World;
                Check(converted.BeeLocations.TryGet(101,out var first)&&first.Equals(new BeeGridPosition(a.Id,0,0,0)),"v14 front conversion");
                Check(converted.BeeLocations.TryGet(102,out var last)&&last.Equals(new BeeGridPosition(a.Id,4,1,2)),"v14 back conversion");
                Check(BitConverter.ToInt32(Save(converted),0)==15,"migrated save version");
            }
            for(int y=0;y<2;y++)for(int z=0;z<3;z++)for(int x=0;x<5;x++)
            {
                var p=new BeeGridPosition(a.Id,x,y,z);w.BeeLocations.Set(w,1+y*15+z*5+x,p);
                Check(w.BeeLocations.CountAt(p)==1,"occupancy count");
                if(y==1) Check(Edge(w,p,new BeeGridPosition(b.Id,x,0,z)),"all15 facing connections");
            }
            var front=new BeeGridPosition(a.Id,2,0,1);var back=new BeeGridPosition(a.Id,2,1,1);var target=new BeeGridPosition(b.Id,2,0,1);
            Check(!Edge(w,front,back),"no comb interior shortcut");
            Check(Edge(w,new BeeGridPosition(a.Id,0,0,1),new BeeGridPosition(a.Id,0,1,1)),"around frame edge");
            var seen=new System.Collections.Generic.HashSet<BeeGridPosition>{front};var queue=new System.Collections.Generic.Queue<BeeGridPosition>();queue.Enqueue(front);
            while(queue.Count>0)foreach(var step in w.BeeLocations.GetNeighbours(w,queue.Dequeue()))if(step.Target.EquipmentId==a.Id&&seen.Add(step.Target))queue.Enqueue(step.Target);
            Check(seen.Count==30,"all30 frame cells reachable around rim");
            w.BeeLocations.Set(w,100,back);Check(w.BeeLocations.TryMove(w,100,target),"cross adjacent frames");
            Check(w.BeeLocations.TryMove(w,100,back),"reverse crossing");
            Check(!w.BeeLocations.TryMove(w,100,front),"movement rejects interior");
            int builds=w.BeeLocations.TopologyBuilds;
            for(int i=0;i<100;i++)w.BeeLocations.GetNeighbours(w,front);
            Check(w.BeeLocations.TopologyBuilds==builds,"shared topology reused");
            Check(box.InteriorSlots.GetSlot(3).TryMoveTo(box.InteriorSlots.GetSlot(4)),"separate frames");
            Check(!Edge(w,back,new BeeGridPosition(b.Id,2,0,1)),"no empty-slot jump");
            Check(!w.BeeLocations.TryMove(w,100,target),"reject stale move");
            var panel=new ItemData(10003,"divider",ItemKind.DividerBoard);Check(box.InteriorSlots.GetSlot(3).TryInsert(panel),"insert divider");
            Check(!Edge(w,back,target),"no panel crossing");box.InteriorSlots.GetSlot(3).Take();
            Check(box.InteriorSlots.GetSlot(4).TryMoveTo(box.InteriorSlots.GetSlot(3)),"restore neighbour");
            Check(Edge(w,back,target),"connection rebuilt");
            var wall=new BeeGridPosition(box.Id,0,10,4);w.BeeLocations.Set(w,200,wall);
            Reject(()=>w.BeeLocations.Set(w,201,new BeeGridPosition(box.Id,1,10,4)),"air is not attachment");
            Reject(()=>w.BeeLocations.Set(w,202,new BeeGridPosition(a.Id,5,0,0)),"invalid coordinate");
            Reject(()=>w.BeeLocations.Set(w,0,front),"invalid bee ID");
            w.PrepareThermal(ThermalMaterials.Load());Tick(w,1);
            Check(BeeSurfaceEnvironmentReader.TryReadAt(w,front,out var climate)&&climate.Kind==BeeSurfaceEnvironmentKind.AdjacentAir,"frame adjacent climate");
            Check(BeeSurfaceEnvironmentReader.TryRead(w,200,out var wallClimate)&&wallClimate.Kind==BeeSurfaceEnvironmentKind.Mixed,"wall inner+outer aggregate");
            Check(!double.IsNaN(wallClimate.RelativeHumidityPercent),"finite aggregate humidity");
            var bytes=Save(w);restored=WorldSaveCodec.Decode(bytes).World;
            Check(bytes.SequenceEqual(Save(restored)),"schema15 exact roundtrip");
            Check(restored.BeeLocations.TryGet(200,out var wallSaved)&&wallSaved.Equals(wall),"wall save");
            Check(box.InteriorSlots.GetSlot(2).TryMoveTo(w.Hand.Slot),"lift frame");
            Check(w.BeeLocations.TryGet(100,out var carried)&&carried.Equals(back),"attachment survives lift");
            Check(!Edge(w,back,target),"detached frame loses crossing");
            Check(BeeSurfaceEnvironmentReader.TryRead(w,100,out var outside)&&outside.Kind==BeeSurfaceEnvironmentKind.Exterior,"detached ambient");
            Check(w.Hand.Slot.TryMoveTo(upper.InteriorSlots.GetSlot(7)),"move tier");
            Check(!BeeSurfaceEnvironmentReader.TryReadAt(w,front,out _),"stale thermal data rejected");Tick(w,2);
            Check(BeeSurfaceEnvironmentReader.TryReadAt(w,front,out var moved)&&moved.BoxId==upper.Id,"new box climate");
            Check(upper.InteriorSlots.GetSlot(7).TryMoveTo(w.Trash.GetSlot(0)),"trash frame");
            Check(!w.TryEmptyTrash(out _),"attached frame deletion guard");
            // A detached wall-bearing item is protected as well.
            var spare=new ItemData(10004,"spare super",ItemKind.Super);Check(w.Trash.GetSlot(1).TryInsert(spare),"trash wall equipment");
            w.BeeLocations.Set(w,300,new BeeGridPosition(spare.Id,0,10,2));
            Check(w.Trash.GetSlot(0).TryMoveTo(w.Warehouse.GetSlot(0)),"clear frame from trash");
            Check(!w.TryEmptyTrash(out _),"wall equipment deletion guard");
            restored.Weather.BindBaseline(WeatherAssetLoader.LoadBaseline());restored.PrepareThermal(ThermalMaterials.Load());Tick(restored,2);
            Check(BeeSurfaceEnvironmentReader.TryReadAt(restored,front,out _),"restored climate");
            var report="PASS "+checks+" checks: grid-only positions, all30 frame cells, all15 adjacent-frame links, no solid/empty-slot/panel shortcuts, shared cache invalidation, wall/ambient climate, lift/reinsert, v13/v14 migration, v15 roundtrip, equipment deletion guards.";
            var path=Environment.GetEnvironmentVariable("BEE_REPORT");if(!string.IsNullOrEmpty(path))File.WriteAllText(path,report);Debug.Log(report);
        }
        finally { w.Thermal?.Dispose();restored?.Thermal?.Dispose(); }
    }
}
