using System;
using System.IO;
using System.Linq;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
public static class CombTickTiming
{
    sealed class Timed : IGameMinuteParticipant {
        readonly IGameMinuteParticipant inner; public double Ms;
        public Timed(IGameMinuteParticipant value){inner=value;}
        public async Task CalculateMinuteAsync(long minute,CancellationToken token){var s=Stopwatch.StartNew();await inner.CalculateMinuteAsync(minute,token);Ms=s.Elapsed.TotalMilliseconds;}
    }
    public static void Run(){
        string folder="outputs/tick-timing";Directory.CreateDirectory(folder);
        try{
            Resources.Load<CombBalanceSettings>("CombBalanceSettings").TryApply(out _);
            File.WriteAllText(folder+"/environment.txt",SystemInfo.processorType+"\nLogical processors="+SystemInfo.processorCount+"\nRAM MB="+SystemInfo.systemMemorySize+"\nUnity="+Application.unityVersion+"\n30x15; 3 tiers, 30 frames per hive; all cells built; nectar 40mg sugar +100mg water at one cell per each face-region (30 wet cells/frame). Weather seed 42. First tick includes climate initialization; 10 warmup ticks then 100 samples. No rendering, frame yielding or autosave in measured ordinary ticks.\n");
            using(var csv=new StreamWriter(folder+"/ticks.csv"))using(var summary=new StreamWriter(folder+"/summary.csv")){
                csv.AutoFlush=true;summary.AutoFlush=true;
                csv.WriteLine("hives,minute,thermal_ms,weather_nectar_ms,total_ms");
                summary.WriteLine("hives,frames,logical_cells,thermal_cells,faces,boundaries,first_ms,mean_ms,p50_ms,p95_ms,max_ms,thermal_mean_ms,weather_mean_ms,encode_ms,store_validate_ms,save_bytes");
                foreach(int count in new[]{1,10,100}){
                    var world=new GameWorld(new WeatherSimulator(42,WeatherAssetLoader.LoadBaseline()));
                    try{
                        for(int i=0;i<world.Inventory.Capacity;i++)world.Inventory.GetSlot(i).Take();
                        int height=world.Apiary.Grid.Height,id=10000;
                        while(world.Apiary.Grid.UnlockedWidth*height<count)if(!world.Apiary.Grid.TryUnlockNextColumn())throw new Exception("expand");
                        for(int h=0;h<count;h++){
                            int x=h/height,y=h%height;
                            if(h>0){var hive=new HiveData(h+1,x,y);if(!world.Apiary.AddHive(hive,new ItemData(id++,"brood",ItemKind.BroodBox,hive)))throw new Exception("hive");}
                            var root=world.Apiary.GetItemSlot(x,y);var box=root.Item;
                            for(int level=0;level<3;level++){
                                if(level>0){box=new ItemData(id++,"super",ItemKind.Super);var slot=new ItemSlot();slot.TryInsert(box);if(!root.TryCombineFrom(slot))throw new Exception("stack");}
                                for(int j=0;j<10;j++){
                                    var f=new FrameData();foreach(var side in new[]{FrameSide.Front,FrameSide.Back}){
                                        var s=f.GetSurface(side);s.SetAllConstruction(100);
                                        for(int z=0;z<3;z++)for(int col=0;col<5;col++)if(!s.TryAddNectar(col*6,z*5,0,40,100))throw new Exception("nectar");
                                    }
                                    if(!box.InteriorSlots.GetSlot(j).TryInsert(new ItemData(id++,"frame",ItemKind.Frame,f)))throw new Exception("frame");
                                }
                            }
                            var lid=new ItemSlot();lid.TryInsert(new ItemData(id++,"lid",ItemKind.Lid));if(!root.TryCombineFrom(lid))throw new Exception("lid");
                        }
                        world.PrepareThermal(ThermalMaterials.Load());world.Thermal.PrepareExchange();
                        var thermal=new Timed(world.Thermal);var weather=new Timed(world.Weather);var scheduler=new SimulationScheduler(world.Clock);scheduler.Register(thermal);scheduler.Register(weather);
                        var total=new double[100];var th=new double[100];var we=new double[100];double first=0;
                        for(int minute=1;minute<=110;minute++){
                            var s=Stopwatch.StartNew();scheduler.RunToAsync(minute,()=>Task.CompletedTask,()=>Task.CompletedTask,CancellationToken.None).GetAwaiter().GetResult();s.Stop();
                            if(world.CurrentGameMinute!=minute||world.Weather.Minute!=minute)throw new Exception("clock mismatch");
                            csv.WriteLine(FormattableString.Invariant($"{count},{minute},{thermal.Ms:F6},{weather.Ms:F6},{s.Elapsed.TotalMilliseconds:F6}"));
                            if(minute==1)first=s.Elapsed.TotalMilliseconds;
                            if(minute>10){total[minute-11]=s.Elapsed.TotalMilliseconds;th[minute-11]=thermal.Ms;we[minute-11]=weather.Ms;}
                        }
                        var timer=Stopwatch.StartNew();var bytes=WorldSaveCodec.Encode(new WorldSaveSnapshot{World=world,UtcTicks=DateTime.UtcNow.Ticks,TargetMinute=world.CurrentGameMinute});double encode=timer.Elapsed.TotalMilliseconds;timer.Restart();new WorldSaveStore(Path.GetFullPath(folder+"/save-"+count)).Save(bytes);double write=timer.Elapsed.TotalMilliseconds;
                        var ordered=total.OrderBy(v=>v).ToArray();var ex=world.Thermal.Exchange;
                        summary.WriteLine(FormattableString.Invariant($"{count},{count*30},{count*27000},{ex.CellCount},{ex.FaceCount},{ex.BoundaryCount},{first:F6},{total.Average():F6},{(ordered[49]+ordered[50])/2:F6},{ordered[94]:F6},{ordered[99]:F6},{th.Average():F6},{we.Average():F6},{encode:F6},{write:F6},{bytes.Length}"));
                        UnityEngine.Debug.Log("TIMING COMPLETE hives="+count);
                    }finally{world.Thermal?.Dispose();}GC.Collect();GC.WaitForPendingFinalizers();
                }
            }
            EditorApplication.Exit(0);
        }catch(Exception e){File.WriteAllText(folder+"/error.txt",e.ToString());UnityEngine.Debug.LogException(e);EditorApplication.Exit(1);}
    }
}
