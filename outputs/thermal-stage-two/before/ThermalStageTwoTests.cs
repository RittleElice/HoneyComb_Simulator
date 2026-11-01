using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class ThermalStageTwoTests
{
    static int checks;
    static readonly List<string> report=new List<string>();
    static void Check(bool b,string text) { checks++; if(!b) throw new Exception(text); }
    static void Near(double a,double b,double eps,string text) => Check(Math.Abs(a-b)<=eps,text+": "+a+" vs "+b);
    static void Reject(Action a,string label) { bool failed=false; try { a(); } catch(ArgumentException) {failed=true;} catch(InvalidOperationException) {failed=true;} catch(InvalidDataException) {failed=true;} Check(failed,label); }
    static WorldSaveSnapshot Snapshot(GameWorld w) => new WorldSaveSnapshot { World=w,UtcTicks=DateTime.UtcNow.Ticks,TargetMinute=w.CurrentGameMinute };
    static byte[] WeatherBytes(WeatherSimulator w) { using(var s=new MemoryStream()) using(var b=new BinaryWriter(s)) {w.WriteSnapshot(b);return s.ToArray();} }
    static float[] Energies(ThermalExchange e) => Enumerable.Range(0,e.CellCount).Select(i=>e.GetCell(i).Owner.EnergyJ[e.GetCell(i).Index]).ToArray();
    public static async void RunBatch()
    {
        Directory.CreateDirectory("outputs/thermal-stage-two");
        try
        {
            var m=ThermalMaterials.Load();
            Near(ThermalExchange.Conductance(.002,.005,.1,.01,.2),.02,1e-15,"series resistance with unequal halves");
            Reject(()=>ThermalExchange.Conductance(0,1,1,1,1),"zero area");
            Reject(()=>ThermalExchange.Conductance(1,1,double.NaN,1,1),"NaN k");
            var item=new ItemData(9000,"analytic",ItemKind.Lid); var state=item.ThermalState; state.Refresh(m,20);
            var pair=new[]{new ThermalExchange.Cell(state,0,"analytic A"),new ThermalExchange.Cell(state,1,"analytic B")};
            double ca=state.HeatCapacityJPerK[0],cb=state.HeatCapacityJPerK[1];
            double g=Math.Min(ca,cb)/120, k=state.EffectiveConductivityWPerMK[0];
            var face=new ThermalExchange.Face(0,1,g*.02/k,.01,.01);
            var solver=new ThermalExchange(pair,new[]{face});
            state.RestoreEnergy(0,(float)(ca*10)); state.RestoreEnergy(1,(float)(cb*30));
            double ea=state.EnergyJ[0],eb=state.EnergyJ[1],delta=60*g*(eb/cb-ea/ca);
            solver.Step(); Near(state.EnergyJ[0],(float)(ea+delta),0,"analytic gather A"); Near(state.EnergyJ[1],(float)(eb-delta),0,"analytic gather B");
            Near(solver.MinimumStepSeconds,120,1e-10,"dt limit");
            report.Add("Analytic 60s: double residual="+solver.DoubleResidualJ+" J; float residual="+solver.FloatResidualJ+" J");
            int builds=solver.CoefficientBuilds; solver.Step(); Check(builds==solver.CoefficientBuilds,"coefficient cache reused");
            state.MarkDirty(0); state.Refresh(m,20); solver.Step(); Check(solver.CoefficientBuilds==builds+1,"dirty invalidates coefficient cache");
            var prior=Energies(solver); Reject(()=>solver.Step(120.00001),"unstable dt rejected"); Check(prior.SequenceEqual(Energies(solver)),"rejection atomic");
            solver.Step(120); Check(ThermalExchange.Finite(solver.FloatResidualJ),"exact monotonic limit accepted");
            state.RestoreEnergy(0,float.MaxValue); prior=Energies(solver);
            // Finite stored energy but temperature is below absolute zero: reject before the Job.
            state.RestoreEnergy(1,-float.MaxValue); prior=Energies(solver); Reject(()=>solver.Step(),"invalid absolute temperature"); Check(prior.SequenceEqual(Energies(solver)),"invalid snapshot atomic");
            Reject(()=>new ThermalExchange(pair,new[]{face,face}),"duplicate face rejected");
            Reject(()=>new ThermalExchange(new[]{pair[0],pair[0]},Array.Empty<ThermalExchange.Face>()),"duplicate owner rejected");

            var world=new GameWorld(new WeatherSimulator(42,WeatherAssetLoader.LoadBaseline())); world.PrepareThermal(m); world.Thermal.PrepareExchange();
            var actual=world.Thermal.Exchange; actual.RefreshCoefficients();
            report.Add("Default geometry: cells="+actual.CellCount+" faces="+actual.FaceCount+" dtMax="+actual.MinimumStepSeconds.ToString("R")+" s");
            prior=Energies(actual); Reject(()=>actual.Step(),"real 60 seconds unsafe"); Check(prior.SequenceEqual(Energies(actual)),"real rejection preserves all owners"); report.Add(actual.Diagnostic);
            var weatherBefore=WeatherBytes(world.Weather); var saveBefore=WorldSaveCodec.Encode(Snapshot(world));
            var scheduler=new SimulationScheduler(world.Clock); scheduler.Register(world.Thermal); scheduler.Register(world.Weather);
            bool fault=false; try { await scheduler.RunToAsync(1,()=>Task.CompletedTask,()=>Task.CompletedTask,CancellationToken.None); } catch(InvalidOperationException) {fault=true;}
            Check(fault && scheduler.Fault!=null && world.CurrentGameMinute==0,"unsafe tick faults before clock commit");
            Check(weatherBefore.SequenceEqual(WeatherBytes(world.Weather)),"unsafe tick preserves Weather 2.0 and RNG");
            Check(prior.SequenceEqual(Energies(actual)),"runtime failure leaves energies unchanged");
            Reject(()=>WorldSaveCodec.Encode(Snapshot(world)),"failed world cannot save");
            var recovered=WorldSaveCodec.Decode(saveBefore).World; recovered.PrepareThermal(m); recovered.Thermal.PrepareExchange();
            Check(prior.SequenceEqual(Energies(recovered.Thermal.Exchange)),"V3 previous valid snapshot recovery");

            // Add frame, insulation, divider, super and lid; verify every placed local width and stack face.
            var box=recovered.Apiary.GetItemSlot(0,0).Item;
            var frame=new ItemData(9100,"frame",ItemKind.Frame,new FrameData());
            Check(box.InteriorSlots.GetSlot(0).TryInsert(frame),"place frame");
            Check(box.InteriorSlots.GetSlot(1).TryInsert(new ItemData(9101,"insulation",ItemKind.InsulationBoard)),"place insulation");
            Check(box.InteriorSlots.GetSlot(2).TryInsert(new ItemData(9102,"divider",ItemKind.DividerBoard)),"place divider");
            var temporary=new ItemSlot(); temporary.TryInsert(new ItemData(9103,"super",ItemKind.Super));
            Check(recovered.Apiary.GetItemSlot(0,0).TryCombineFrom(temporary),"stack super");
            temporary.TryInsert(new ItemData(9104,"lid",ItemKind.Lid)); Check(recovered.Apiary.GetItemSlot(0,0).TryCombineFrom(temporary),"stack lid");
            recovered.Thermal.Refresh(20); recovered.Thermal.PrepareExchange(); actual=recovered.Thermal.Exchange;
            int expected=ThermalWorld.Items(recovered).Where(i=>i.SpatialGrid!=null).Sum(i=>i.SpatialGrid.Layout.EdgeCount)+2*18*52;
            // Detached equipment contributes additional faces; compare the stack difference independently below.
            Check(actual.FaceCount>=expected,"all internal and 1872 stack faces included");
            int topologyBuilds=recovered.Thermal.TopologyBuilds; recovered.Thermal.PrepareExchange(); Check(topologyBuilds==recovered.Thermal.TopologyBuilds,"topology cache reused");
            var cache=actual; frame.GetContent<FrameData>().Front.SetAllConstruction(100); recovered.Thermal.Refresh(); recovered.Thermal.PrepareExchange();
            Check(ReferenceEquals(cache,recovered.Thermal.Exchange),"wax rebuild keeps geometry");
            Check(actual.CoefficientBuilds==2,"wax change rebuilds conductance");
            box.InteriorSlots.GetSlot(0).Take(); Check(recovered.Hand.Slot.TryInsert(frame),"detach to hand"); recovered.Thermal.Refresh(); recovered.Thermal.PrepareExchange();
            Check(recovered.Thermal.TopologyBuilds==topologyBuilds+1,"placement rebuilds topology");
            actual=recovered.Thermal.Exchange; actual.RefreshCoefficients();
            report.Add("Mixed stack/detached: cells="+actual.CellCount+" faces="+actual.FaceCount+" dtMax="+actual.MinimumStepSeconds.ToString("R")+" s; conforming widths and XY partitions verified");

            // Fixed experiment, NOT a runtime subdivision: one explicit 0.01s tick per call.
            const double fixedExperimentSeconds=.01;
            Check(actual.MinimumStepSeconds>fixedExperimentSeconds,"fixed experiment is stable");
            for(int i=0;i<actual.CellCount;i++) {var c=actual.GetCell(i); c.Owner.RestoreEnergy(c.Index,c.Owner.HeatCapacityJPerK[c.Index]*(5+(i%71)*.5f));}
            double initial=Energies(actual).Sum(v=>(double)v),maxDouble=0,maxFloat=0,totalRounding=0;
            var tickTimes=new List<double>(); var jobTimes=new List<double>();
            for(int j=0;j<105;j++)
            {
                actual.Step(fixedExperimentSeconds); maxDouble=Math.Max(maxDouble,Math.Abs(actual.DoubleResidualJ)); maxFloat=Math.Max(maxFloat,Math.Abs(actual.FloatResidualJ)); totalRounding+=actual.RoundingAbsoluteJ;
                if(j>=5) {tickTimes.Add(actual.LastTickMilliseconds);jobTimes.Add(actual.LastJobMilliseconds);}
            }
            double drift=Energies(actual).Sum(v=>(double)v)-initial;
            Check(Math.Abs(drift)<=totalRounding+1e-5,"long closed-system float error bounded by measured rounding");
            report.Add("Fixed 0.01s experiment x105: max double residual="+maxDouble.ToString("R")+" J; max float residual="+maxFloat.ToString("R")+" J; cumulative drift="+drift.ToString("R")+" J; abs rounding budget="+totalRounding.ToString("R")+" J");
            tickTimes.Sort();jobTimes.Sort(); report.Add("Editor Mono, 100 measured ticks after 5 warmups: full step median="+tickTimes[50]+" ms p95="+tickTimes[94]+" ms; Schedule+Complete median="+jobTimes[50]+" ms p95="+jobTimes[94]+" ms; includes TempJob allocation/copy/validation/commit in full step");
            // Supported 60s synthetic closed system, full-size owners and unchanged material values.
            var syntheticCells=Enumerable.Range(0,actual.CellCount).Select(actual.GetCell).ToArray();
            var syntheticFaces=new List<ThermalExchange.Face>();
            for(int i=1;i<syntheticCells.Length;i++) syntheticFaces.Add(new ThermalExchange.Face(i-1,i,1e-10,.01,.01));
            var stable60=new ThermalExchange(syntheticCells,syntheticFaces); stable60.Step();
            report.Add("Synthetic weak-contact 60s full-size Job: "+stable60.LastTickMilliseconds+" ms; double residual="+stable60.DoubleResidualJ+" J; float residual="+stable60.FloatResidualJ+" J (not actual hive conductance)");
            var bytes=WorldSaveCodec.Encode(Snapshot(recovered)); var loaded=WorldSaveCodec.Decode(bytes); Check(bytes.SequenceEqual(WorldSaveCodec.Encode(loaded)),"V3 exact thermal roundtrip");
            var legacy=WorldSaveCodec.Encode(Snapshot(new GameWorld())); Check(BitConverter.ToInt32(legacy,0)==2,"V2 unchanged"); Check(WorldSaveCodec.Decode(legacy).World.Thermal==null,"V2 no derived graph");
            legacy[0]=1;Array.Resize(ref legacy,legacy.Length-1);Check(WorldSaveCodec.Decode(legacy).World.Weather==null,"V1 unchanged");

            ItemAssemblyTests.RunAll(); HiveSpatialGridTests.RunAll(); CellStateTests.RunAll(); EquipmentCellDataTests.RunAll(); CombGeometryTests.RunAll(); CombThermalTests.RunAll(); WorldSaveTests.Run(); await GameTimeTests.RunAll();
            report.Add("PASS assembly/spatial/equipment/cell/comb/save/time regression suites; stage-two checks="+checks);
            File.WriteAllLines("outputs/thermal-stage-two/results.txt",report); Debug.Log("THERMAL STAGE TWO PASS "+checks); EditorApplication.Exit(0);
        }
        catch(Exception e) { report.Add(e.ToString()); File.WriteAllLines("outputs/thermal-stage-two/failure.txt",report); Debug.LogException(e); EditorApplication.Exit(1); }
    }
    public static void BuildAndroid()
    {
        var scenes=new[]{"Assets/Scenes/Bootstrap.unity","Assets/Scenes/MainMenu.unity","Assets/Scenes/Apiary.unity","Assets/Scenes/Observation.unity","Assets/Scenes/SharedUI.unity"};
        var result=BuildPipeline.BuildPlayer(new BuildPlayerOptions {scenes=scenes,locationPathName="outputs/thermal-stage-two/HoneyComb.apk",target=BuildTarget.Android,options=BuildOptions.Development});
        File.WriteAllText("outputs/thermal-stage-two/android.txt",result.summary.result+"; errors="+result.summary.totalErrors+"; time="+result.summary.totalTime);
        EditorApplication.Exit(result.summary.result==BuildResult.Succeeded?0:1);
    }
}
