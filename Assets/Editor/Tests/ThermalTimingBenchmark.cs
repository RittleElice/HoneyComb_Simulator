using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using UnityEditor;
using UnityEngine;

public static class ThermalTimingBenchmark
{
    static string Stats(string label,List<double> samples)
    {
        var sorted=samples.OrderBy(x=>x).ToArray();
        return label+": n="+sorted.Length+"; mean="+samples.Average().ToString("F4")+" ms; median="+((sorted[(sorted.Length-1)/2]+sorted[sorted.Length/2])/2).ToString("F4")+" ms; p95="+sorted[(int)Math.Ceiling(.95*sorted.Length)-1].ToString("F4")+" ms; max="+sorted.Last().ToString("F4")+" ms";
    }
    public static void RunBatch()
    {
        const string folder="outputs/thermal-timing-calculation-only";Directory.CreateDirectory(folder);var report=new List<string>();
        try
        {
            report.Add("Unity "+Application.unityVersion+"; Editor Mono batchmode; "+SystemInfo.processorType+"; logical processors="+SystemInfo.processorCount);
            report.Add("Synthetic default and mixed game worlds; no player saves, rendering, UI, checkpoints or device timing. Stopwatch wall time; no forced GC. 10 warmups, 200 measured game minutes per scenario.");
            var materials=ThermalMaterials.Load();var baseline=WeatherAssetLoader.LoadBaseline();
            foreach(bool mixed in new[]{false,true})
            {
                var world=new GameWorld(new WeatherSimulator(42,baseline));ItemData frame=null;
                var box=world.Apiary.GetItemSlot(0,0).Item;
                if(mixed)
                {
                    frame=new ItemData(9990,"timing frame",ItemKind.Frame,new FrameData());
                    frame.GetContent<FrameData>().Front.SetAllConstruction(100);
                    for(int y=0;y<40;y++)for(int x=0;x<84;x++)frame.GetContent<FrameData>().Front.TryAddNectar(x,y,0,0,100);
                    world.Hand.Slot.TryInsert(frame);
                    box.InteriorSlots.GetSlot(1).TryInsert(new ItemData(9991,"insulation",ItemKind.InsulationBoard));
                    box.InteriorSlots.GetSlot(2).TryInsert(new ItemData(9992,"divider",ItemKind.DividerBoard));
                    var temp=new ItemSlot();temp.TryInsert(new ItemData(9993,"super",ItemKind.Super));world.Apiary.GetItemSlot(0,0).TryCombineFrom(temp);
                    temp.TryInsert(new ItemData(9994,"lid",ItemKind.Lid));world.Apiary.GetItemSlot(0,0).TryCombineFrom(temp);
                }
                var timer=Stopwatch.StartNew();world.PrepareThermal(materials);double initialState=timer.Elapsed.TotalMilliseconds;
                timer.Restart();world.Thermal.PrepareExchange();double initialTopology=timer.Elapsed.TotalMilliseconds;
                var exchange=world.Thermal.Exchange;
                exchange.MeasurePerformance=true;
                for(int i=0;i<exchange.CellCount;i++){var cell=exchange.GetCell(i);cell.Owner.RestoreEnergy(cell.Index,cell.Owner.HeatCapacityJPerK[cell.Index]*(5+(i%71)*.5f));}
                report.Add((mixed?"MIXED":"DEFAULT")+": cells="+exchange.CellCount+"; internal faces="+exchange.FaceCount+"; exterior faces="+exchange.BoundaryCount+"; state init="+initialState.ToString("F3")+" ms; first topology/coefficients="+initialTopology.ToString("F3")+" ms (single cold measurements)");
                var thermal=new List<double>();var step=new List<double>();var job=new List<double>();var weather=new List<double>();var overhead=new List<double>();var total=new List<double>();
                int gc0=GC.CollectionCount(0),gc1=GC.CollectionCount(1),gc2=GC.CollectionCount(2);
                for(int n=1;n<=210;n++)
                {
                    timer.Restart();world.Thermal.CalculateMinuteAsync(n,CancellationToken.None).GetAwaiter().GetResult();double a=timer.Elapsed.TotalMilliseconds;
                    timer.Restart();world.Weather.Advance(n);double b=timer.Elapsed.TotalMilliseconds;
                    if(n<=10)continue;
                    thermal.Add(a);step.Add(exchange.LastTickMilliseconds);job.Add(exchange.LastJobMilliseconds);weather.Add(b);overhead.Add(a-exchange.LastTickMilliseconds);total.Add(a+b);
                }
                report.Add(Stats("Thermal minute: refresh + topology check + full step",thermal));
                report.Add(Stats("Full step: input refresh + Job + direct commit; diagnostics disabled",step));
                report.Add(Stats("Job Schedule+Complete",job));
                report.Add(Stats("Thermal refresh/topology-check overhead",overhead));
                report.Add(Stats("Weather Evaluate/Advance",weather));
                report.Add(Stats("Thermal + weather",total));
                report.Add("GC collections during warmup+measurement: gen0="+(GC.CollectionCount(0)-gc0)+", gen1="+(GC.CollectionCount(1)-gc1)+", gen2="+(GC.CollectionCount(2)-gc2));
                report.Add("Persistent buffers: allocations="+exchange.BufferAllocations+"; coefficient uploads="+exchange.CoefficientUploads+"; bytes="+exchange.BufferBytes);
                var monitor=new HiveTemperatureMonitor();var capture=new List<double>();
                for(int n=0;n<110;n++){timer.Restart();bool ok=monitor.Capture(box.SpatialGrid,210,world.Weather.Current.TemperatureCelsius,out var why);double elapsed=timer.Elapsed.TotalMilliseconds;if(!ok)throw new Exception(why);if(n>=10)capture.Add(elapsed);}
                report.Add(Stats("Monitor data capture for one box, excludes drawing",capture));
                if(mixed)
                {
                    frame.GetContent<FrameData>().Front.TryAddNectar(0,0,1,1,0);
                    timer.Restart();world.Thermal.CalculateMinuteAsync(211,CancellationToken.None).GetAwaiter().GetResult();report.Add("One contents edit + next thermal minute="+timer.Elapsed.TotalMilliseconds.ToString("F3")+" ms");world.Weather.Advance(211);
                    var moved=world.Hand.Slot.Take();if(!box.InteriorSlots.GetSlot(0).TryInsert(moved))throw new Exception("Cannot place frame");
                    timer.Restart();world.Thermal.CalculateMinuteAsync(212,CancellationToken.None).GetAwaiter().GetResult();report.Add("One frame placement + next thermal minute (topology rebuild)="+timer.Elapsed.TotalMilliseconds.ToString("F3")+" ms");
                }
            }
            ThermalExchange.ReleaseAllBuffers();
            File.WriteAllLines(folder+"/results.txt",report);UnityEngine.Debug.Log("THERMAL TIMING COMPLETE");if(Application.isBatchMode)EditorApplication.Exit(0);
        }
        catch(Exception e){ThermalExchange.ReleaseAllBuffers();report.Add(e.ToString());File.WriteAllLines(folder+"/failure.txt",report);UnityEngine.Debug.LogException(e);if(Application.isBatchMode)EditorApplication.Exit(1);}
    }
}
