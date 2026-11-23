using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using UnityEngine;
public static class BeeDesireChangeTests {
 static void Near(double a,double b,string label){if(Math.Abs(a-b)>1e-8)throw new Exception(label+": "+a+" vs "+b);}
 public static void Run(){
 var w=new GameWorld(new WeatherSimulator(WeatherSimulator.DefaultSeed,WeatherAssetLoader.LoadBaseline(),null,123456789UL));w.BeeCycle.SeedDemo();
 try{
 Near(w.BeeCycle.Settings.commandMaximum,3,"new game asset maximum");
 var rules=new BeeCycleRules(w.BeeCycle.Settings);Near(rules.Eval("commandStimulus",new CycleInputs{need=1}),3,"maximum stimulus");
 w.BeeCycle.CalculateMinuteAsync(1,CancellationToken.None).GetAwaiter().GetResult();
 var bee=w.BeeCycle.Bees.First(b=>!b.queen&&b.desires.Count>0);var desire=bee.desires[0];var cmd=w.BeeCycle.Commands.First(c=>c.task==desire.task&&c.target.Grid.Equals(desire.target.Grid));
 w.BeeCycle.Commands.Clear();w.BeeCycle.Commands.Add(cmd);cmd.need=1;cmd.localOnly=false;
 var type=typeof(BeeCycleRuntime);Action index=()=>type.GetMethod("IndexCommands",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(w.BeeCycle,null);
 Action update=()=>type.GetMethod("UpdateDesires",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(w.BeeCycle,new object[]{bee});
 Action clear=()=>((System.Collections.IDictionary)type.GetField("stimulusCache",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(w.BeeCycle)).Clear();
 index();clear();desire.weight=20;update();Near(desire.weight,23,"positive stimulus skips decay");
 cmd.need=0;clear();update();Near(desire.weight,13,"zero stimulus decays despite command");
 cmd.need=0.1;clear();update();Near(desire.weight,13.3,"small positive stimulus skips decay");
 w.BeeCycle.Commands.Clear();index();update();Near(desire.weight,3.3,"removed command decays");update();Near(desire.weight,0,"floor zero");if(bee.desires.Contains(desire))throw new Exception("zero desire not removed");
 Debug.Log("DESIRE CHANGE PASS: max3, positive/zero/small stimulus, removed command, floor and removal");
 }finally{w.Thermal.Dispose();}
 BeeCycleTests.FinalSmoke();
 }
}
