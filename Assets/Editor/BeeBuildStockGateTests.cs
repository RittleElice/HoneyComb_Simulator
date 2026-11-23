using System;
using System.Linq;
using System.Reflection;
using UnityEngine;
public static class BeeBuildStockGateTests {
 static void Call(BeeCycleRuntime r,string m)=>typeof(BeeCycleRuntime).GetMethod(m,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(r,null);
 static void Check(bool b,string s){if(!b)throw new Exception(s);}
 public static void Run(){
 var w=new GameWorld(new WeatherSimulator(WeatherSimulator.DefaultSeed,WeatherAssetLoader.LoadBaseline(),null,123456789UL));w.BeeCycle.SeedDemo();
 try{var r=w.BeeCycle;Call(r,"RefreshCommands");var existing=r.Commands.Where(c=>c.task==CycleTask.Build).ToArray();Check(existing.Length>0,"surplus permits new construction commands");
 FrameSurface food=null;int fx=0,fy=0;
 foreach(var region in r.Regions)for(int y=region.z*4;y<(region.z+1)*4;y++)for(int x=region.x*5;x<(region.x+1)*5;x++)if(region.surface.GetCell(x,y).Content==CellContentKind.Nectar){region.surface.TryClearContent(x,y);food=region.surface;fx=x;fy=y;}
 Call(r,"RefreshRegions");Call(r,"RefreshCommands");Check(existing.All(c=>r.Commands.Contains(c)),"existing commands persist after stock depleted");
 r.Commands.Clear();Call(r,"RefreshCommands");Check(!r.Commands.Any(c=>c.task==CycleTask.Build),"zero stock blocks new construction");Check(r.Commands.Any(c=>c.task==CycleTask.ForageNectar),"forage unaffected");
 double threshold=r.Bees.Count(b=>b.stage==CycleStage.Adult)*r.Settings.stockSugarPerBee;
 Check(food.TryAddNectar(fx,fy,0,(float)threshold,0),"fixture threshold food");Call(r,"RefreshRegions");Call(r,"RefreshCommands");Check(!r.Commands.Any(c=>c.task==CycleTask.Build),"equality is not surplus");
 Check(food.TryAddNectar(fx,fy,0,1,0),"fixture surplus food");Call(r,"RefreshRegions");Call(r,"RefreshCommands");Check(r.Commands.Any(c=>c.task==CycleTask.Build),"positive surplus allows new commands");
 Debug.Log("BUILD STOCK GATE PASS: surplus/zero/equality, existing commands preserved, forage unaffected");
 }finally{w.Thermal.Dispose();}
 }
}
