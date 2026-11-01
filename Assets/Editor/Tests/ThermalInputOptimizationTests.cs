using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

// Run Before on the original implementation, then After on the optimized implementation.
// Each minute fingerprints the raw energy/capacity/temperature bits, not rounded text.
public static class ThermalInputOptimizationTests
{
    const string Folder="outputs/thermal-input-optimization";
    static void Check(bool ok,string message) { if(!ok) throw new Exception(message); }
    static string Hash(byte[] bytes) { using(var sha=SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(bytes)); }
    static byte[] Save(GameWorld w) => WorldSaveCodec.Encode(new WorldSaveSnapshot {World=w,UtcTicks=638000000000000000,TargetMinute=w.CurrentGameMinute});
    static SimulationScheduler Scheduler(GameWorld w)
    { var s=new SimulationScheduler(w.Clock);s.Register(w.Thermal);s.Register(w.Weather);return s; }
    public static async void Before() { await Run(false); }
    public static async void After() { await Run(true); }
    static async Task Run(bool compare)
    {
        Directory.CreateDirectory(Folder);
        var settings=CombBalance.Snapshot();
        try
        {
            var trace=new List<string>();var materials=ThermalMaterials.Load();
            foreach(bool mixed in new[]{false,true})
            {
                var world=new GameWorld(new WeatherSimulator(42,WeatherAssetLoader.LoadBaseline()));
                ItemData frame=null;
                if(mixed)
                {
                    frame=new ItemData(9970,"trace frame",ItemKind.Frame,new FrameData());
                    frame.GetContent<FrameData>().Front.SetAllConstruction(100);
                    frame.GetContent<FrameData>().Back.SetAllConstruction(100);
                    Check(world.Hand.Slot.TryInsert(frame),"put frame in hand");
                }
                world.PrepareThermal(materials);world.Thermal.PrepareExchange();
                var graph=world.Thermal.Exchange;
                for(int i=0;i<graph.CellCount;i++){var c=graph.GetCell(i);c.Owner.RestoreEnergy(c.Index,c.Owner.HeatCapacityJPerK[c.Index]*(-5+(i%91)*.5f));}
                var scheduler=Scheduler(world);
                for(int minute=1;minute<=120;minute++)
                {
                    var slot=world.Apiary.GetItemSlot(0,0);
                    var box=slot.Value is ItemAssemblyData assembly?assembly.Parts[0]:slot.Item;
                    if(mixed)
                    {
                        var f=frame.GetContent<FrameData>();
                        if(minute==15) Check(f.Front.TryAddNectar(0,0,1,100,100),"nectar");
                        if(minute==25) Check(f.Back.TryAddPollen(2,2,100),"pollen");
                        if(minute==35) f.Front.TryClearContent(0,0);
                        if(minute==45) Check(box.InteriorSlots.GetSlot(0).TryInsert(world.Hand.Slot.Take()),"place frame");
                        if(minute==55) f.Flip();
                        if(minute==65) Check(world.Hand.Slot.TryInsert(box.InteriorSlots.GetSlot(0).Take()),"detach frame");
                        if(minute==75) {var p=CombBalance.Snapshot();p.PollenSpecificHeatJPerKgK=2000;Check(CombBalance.TryApply(p,out _),"capacity settings");}
                        if(minute==85)
                        {
                            var temp=new ItemSlot();temp.TryInsert(new ItemData(9971,"trace super",ItemKind.Super));Check(slot.TryCombineFrom(temp),"stack super");
                            temp.TryInsert(new ItemData(9972,"trace lid",ItemKind.Lid));Check(slot.TryCombineFrom(temp),"stack lid");
                        }
                        if(minute==95) Check(slot.TrySplitTo(2,world.Inventory.GetSlot(11)),"split lid");
                    }
                    if(minute==105)
                    {
                        var bytes=Save(world);trace.Add((mixed?"mixed":"default")+" save104 "+Hash(bytes));
                        world.Thermal.Exchange.Dispose();world=WorldSaveCodec.Decode(bytes).World;
                        await WeatherAssetLoader.PrepareLoadedWorldAsync(world);world.PrepareThermal(materials);world.Thermal.PrepareExchange();
                        scheduler=Scheduler(world);
                        if(mixed) frame=ThermalWorld.Items(world).Single(i=>i.Id==9970);
                    }
                    if(minute==115) world.Thermal.Exchange.Dispose();
                    await scheduler.RunToAsync(minute,()=>Task.CompletedTask,()=>Task.CompletedTask,CancellationToken.None);
                    graph=world.Thermal.Exchange;
                    using(var stream=new MemoryStream())
                    {
                        using(var writer=new BinaryWriter(stream,System.Text.Encoding.UTF8,true))
                        {
                            writer.Write(graph.CellCount);
                            for(int i=0;i<graph.CellCount;i++)
                            {
                                var c=graph.GetCell(i);float e=c.Owner.EnergyJ[c.Index],capacity=c.Owner.HeatCapacityJPerK[c.Index];
                                writer.Write(c.Index);writer.Write(e);writer.Write(capacity);writer.Write((double)e/capacity);
                            }
                        }
                        trace.Add((mixed?"mixed":"default")+" minute"+minute+" "+Hash(stream.ToArray()));
                    }
                }
                trace.Add((mixed?"mixed":"default")+" save120 "+Hash(Save(world)));
                world.Thermal.Exchange.Dispose();CombBalance.TryApply(settings,out _);
            }
            var file=Folder+(compare?"/after-trace.txt":"/before-trace.txt");File.WriteAllLines(file,trace);
            if(compare)
            {
                var expected=File.ReadAllLines(Folder+"/before-trace.txt");
                Check(trace.SequenceEqual(expected),"Raw-bit traces or serialized saves differ from pre-optimization implementation");
                File.WriteAllText(Folder+"/comparison.txt","PASS: "+trace.Count+" exact SHA-256 fingerprints match (240 per-minute full-grid energy/capacity/double-temperature traces + 4 serialized saves). Includes contents, capacity tuning, frame flip/move, stack/split, save/restore and buffer recreation.");
            }
            ThermalExchange.ReleaseAllBuffers();
            // Timing is separate from tracing; identical benchmark runs on each implementation.
            ThermalTimingBenchmark.RunBatch();
        }
        catch(Exception e)
        {
            File.WriteAllText(Folder+"/failure.txt",e.ToString());Debug.LogException(e);ThermalExchange.ReleaseAllBuffers();if(Application.isBatchMode)EditorApplication.Exit(1);
        }
        finally {CombBalance.TryApply(settings,out _);}
    }
}
