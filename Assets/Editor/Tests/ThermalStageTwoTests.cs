using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

[InitializeOnLoad]
public static class ThermalStageTwoTests
{
    static ThermalStageTwoTests() { EditorApplication.update += Watch; }
    static void Watch()
    {
        if(EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        if(File.Exists("Temp/run-thermal-stage-two")) { File.Delete("Temp/run-thermal-stage-two"); RunBatch(); }
        if(File.Exists("Temp/build-thermal-stage-two")) { File.Delete("Temp/build-thermal-stage-two"); BuildAndroid(); }
    }
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
        Directory.CreateDirectory("outputs/persistent-thermal");
        try
        {
            checks=0; report.Clear();
            var m=ThermalMaterials.Load();
            PersistentThermalTests.Run(m);
            report.Add("PASS persistent allocation reuse, changed coefficients, bit-exact fresh/reused results, disposal, topology replacement, unchanged save bytes, all placement parts and monitor capture");
            EffectiveThermalTests.Run(m);
            ContentThermalIntegrationTests.Run(m);
            ExteriorThermalTests.Run(m);
            report.Add("PASS exterior analytic heating/cooling, combined budget, open/covered boundaries, weather forcing and geometry changes");
            Near(ThermalExchange.Conductance(.002,.005,.1,.01,.2),.02,1e-15,"series resistance with unequal halves");
            Reject(()=>ThermalExchange.Conductance(0,1,1,1,1),"zero area");
            Reject(()=>ThermalExchange.Conductance(1,1,double.NaN,1,1),"NaN k");
            var item=new ItemData(9000,"analytic",ItemKind.Lid); var state=item.ThermalState; state.Refresh(m,20);
            var pair=new[]{new ThermalExchange.Cell(state,0,"analytic A"),new ThermalExchange.Cell(state,1,"analytic B")};
            double ca=state.HeatCapacityJPerK[0],cb=state.HeatCapacityJPerK[1];
            double g=Math.Min(ca,cb)/120, k=state.EffectiveConductivityWPerMK[0];
            var face=new ThermalExchange.Face(0,1,g*.02/k,.01,.01);
            var solver=new ThermalExchange(pair,new[]{face},null);
            state.RestoreEnergy(0,(float)(ca*10)); state.RestoreEnergy(1,(float)(cb*30));
            double ea=state.EnergyJ[0],eb=state.EnergyJ[1],delta=60*g*(eb/cb-ea/ca);
            solver.Step(); Near(state.EnergyJ[0],(float)(ea+delta),0,"analytic gather A"); Near(state.EnergyJ[1],(float)(eb-delta),0,"analytic gather B");
            Near(solver.MinimumStepSeconds,120,1e-10,"dt limit");
            report.Add("PASS analytical pair transfer with direct result commit");
            int builds=solver.CoefficientBuilds; solver.Step(); Check(builds==solver.CoefficientBuilds,"coefficient cache reused");
            state.MarkDirty(0); state.Refresh(m,20); solver.Step(); Check(solver.CoefficientBuilds==builds+1,"dirty invalidates coefficient cache");
            var prior=Energies(solver); Reject(()=>solver.Step(120.00001),"unstable dt rejected"); Check(prior.SequenceEqual(Energies(solver)),"rejection atomic");
            solver.Step(120); Check(Energies(solver).All(v=>ThermalMath.Finite(v)),"exact monotonic limit accepted");
            Reject(()=>solver.Step(double.NaN),"NaN timestep rejected");
            Reject(()=>state.RestoreEnergy(0,float.PositiveInfinity),"infinite input rejected");
            state.RestoreEnergy(0,(float)(ca*10)); prior=Energies(solver);
            // Finite stored energy but temperature is below absolute zero: reject before the Job.
            state.RestoreEnergy(1,-float.MaxValue); prior=Energies(solver); Reject(()=>solver.Step(),"invalid absolute temperature"); Check(prior.SequenceEqual(Energies(solver)),"invalid snapshot atomic");
            Reject(()=>new ThermalExchange(pair,new[]{face,face}),"duplicate face rejected");
            Reject(()=>new ThermalExchange(new[]{pair[0],pair[0]},Array.Empty<ThermalExchange.Face>()),"duplicate owner rejected");

            var world=new GameWorld(new WeatherSimulator(42,WeatherAssetLoader.LoadBaseline())); world.PrepareThermal(m); world.Thermal.PrepareExchange();
            var actual=world.Thermal.Exchange; actual.RefreshCoefficients();
            report.Add("Default geometry: cells="+actual.CellCount+" faces="+actual.FaceCount+" dtMax="+actual.MinimumStepSeconds.ToString("R")+" s");
            Check(actual.RawMinimumStepSeconds < 1 && actual.MinimumStepSeconds >= 60,"effective coefficients support real geometry at 60s");
            var scheduler=new SimulationScheduler(world.Clock); scheduler.Register(world.Thermal); scheduler.Register(world.Weather);
            await scheduler.RunToAsync(3,()=>Task.CompletedTask,()=>Task.CompletedTask,CancellationToken.None);
            Check(scheduler.Fault==null && world.CurrentGameMinute==3 && world.Weather.Minute==3,"effective thermal/weather clock advances");
            var recovered=WorldSaveCodec.Decode(WorldSaveCodec.Encode(Snapshot(world))).World;
            await WeatherAssetLoader.PrepareLoadedWorldAsync(recovered); recovered.PrepareThermal(m); recovered.Thermal.PrepareExchange();
            Check(Energies(actual).SequenceEqual(Energies(recovered.Thermal.Exchange)),"thermal snapshot recovery");
            var resumedScheduler=new SimulationScheduler(recovered.Clock); resumedScheduler.Register(recovered.Thermal); resumedScheduler.Register(recovered.Weather);
            await scheduler.RunToAsync(5,()=>Task.CompletedTask,()=>Task.CompletedTask,CancellationToken.None);
            await resumedScheduler.RunToAsync(5,()=>Task.CompletedTask,()=>Task.CompletedTask,CancellationToken.None);
            Check(Energies(actual).SequenceEqual(Energies(recovered.Thermal.Exchange)),"effective thermal save/resume deterministic");
            Check(WeatherBytes(world.Weather).SequenceEqual(WeatherBytes(recovered.Weather)),"coupled weather save/resume deterministic");
            report.Add("PASS actual 60s runtime ticks and coupled save/resume");

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
            var placed=new HashSet<ItemData>();
            foreach(var it in ThermalWorld.Items(recovered)) if(it.SpatialGrid!=null)
                for(int i=0;i<it.SpatialGrid.CellCount;i++) {var source=it.SpatialGrid.GetCell(i).SourceCell;if(source!=null)placed.Add(source.Equipment);}
            foreach(var it in ThermalWorld.Items(recovered)) if(it.SpatialGrid==null && it.EquipmentGrid!=null && !placed.Contains(it))
            {var gg=it.EquipmentGrid; if(gg.SizeZ>0) expected+=(gg.SizeX-1)*gg.SizeY*gg.SizeZ+gg.SizeX*(gg.SizeY-1)*gg.SizeZ+gg.SizeX*gg.SizeY*(gg.SizeZ-1);}
            Check(actual.FaceCount==expected,"exact internal, detached and 1872 stack face count");
            Check(actual.MaximumDegree==6,"actual conforming topology fits six directions without dropped faces");
            int topologyBuilds=recovered.Thermal.TopologyBuilds; recovered.Thermal.PrepareExchange(); Check(topologyBuilds==recovered.Thermal.TopologyBuilds,"topology cache reused");
            var cache=actual; frame.GetContent<FrameData>().Front.SetAllConstruction(100); recovered.Thermal.Refresh(); recovered.Thermal.PrepareExchange();
            Check(ReferenceEquals(cache,recovered.Thermal.Exchange),"wax rebuild keeps geometry");
            Check(actual.CoefficientBuilds==2,"wax change rebuilds conductance");
            box.InteriorSlots.GetSlot(0).Take(); Check(recovered.Hand.Slot.TryInsert(frame),"detach to hand"); recovered.Thermal.Refresh(); recovered.Thermal.PrepareExchange();
            Check(recovered.Thermal.TopologyBuilds==topologyBuilds+1,"placement rebuilds topology");
            actual=recovered.Thermal.Exchange; actual.RefreshCoefficients();
            report.Add("Mixed stack/detached: cells="+actual.CellCount+" faces="+actual.FaceCount+" dtMax="+actual.MinimumStepSeconds.ToString("R")+" s; conforming widths and XY partitions verified");

            // Actual gameplay: one explicit 60s update per minute.
            const double fixedExperimentSeconds=60;
            Check(actual.MinimumStepSeconds>fixedExperimentSeconds,"fixed experiment is stable");
            for(int i=0;i<actual.CellCount;i++) {var c=actual.GetCell(i); c.Owner.RestoreEnergy(c.Index,c.Owner.HeatCapacityJPerK[c.Index]*(5+(i%71)*.5f));}
            double initial=Energies(actual).Sum(v=>(double)v),maxBalance=0,totalRounding=0,totalExternal=0,previousTotal=0;
            var old=Energies(actual); var reference=old.Select(v=>(double)v).ToArray();
            // Independently limit each physical face using both endpoint total budgets.
            var rawSums=new double[actual.CellCount];
            var rawG=new double[actual.FaceCount];
            for(int f=0;f<actual.FaceCount;f++)
            {
                var edge=actual.GetFace(f);var a=actual.GetCell(edge.A);var b=actual.GetCell(edge.B);
                rawG[f]=ThermalExchange.Conductance(edge.AreaM2,edge.HalfAM,a.Owner.EffectiveConductivityWPerMK[a.Index],edge.HalfBM,b.Owner.EffectiveConductivityWPerMK[b.Index]);
                rawSums[edge.A]+=rawG[f];rawSums[edge.B]+=rawG[f];
            }
            for(int i=0;i<actual.CellCount;i++) rawSums[i]+=actual.RawExteriorConductance(i);
            reference=old.Select(v=>(double)v).ToArray();
            for(int f=0;f<actual.FaceCount;f++)
            {
                var edge=actual.GetFace(f);var a=actual.GetCell(edge.A);var b=actual.GetCell(edge.B);
                double sa=Math.Min(1,m.MaximumExchangeFractionPerMinute*a.Owner.HeatCapacityJPerK[a.Index]/(60*rawSums[edge.A]));
                double sb=Math.Min(1,m.MaximumExchangeFractionPerMinute*b.Owner.HeatCapacityJPerK[b.Index]/(60*rawSums[edge.B]));
                double q=60*rawG[f]*Math.Min(sa,sb)*((double)old[edge.B]/b.Owner.HeatCapacityJPerK[b.Index]-(double)old[edge.A]/a.Owner.HeatCapacityJPerK[a.Index]);
                reference[edge.A]+=q;reference[edge.B]-=q;
            }
            for(int i=0;i<actual.CellCount;i++)
            {
                var c=actual.GetCell(i);
                reference[i]+=60*actual.ExteriorConductance(i)*(20-(double)old[i]/c.Owner.HeatCapacityJPerK[c.Index]);
                Check(60*actual.SumConductance(i)/c.Owner.HeatCapacityJPerK[c.Index] <= m.MaximumExchangeFractionPerMinute+1e-12,"per-cell minute budget");
            }
            previousTotal=initial;
            var tickTimes=new List<double>(); var jobTimes=new List<double>();
            for(int j=0;j<105;j++)
            {
                actual.Step(fixedExperimentSeconds,20); totalExternal+=actual.ExternalEnergyJ;
                // Offline audit only: the runtime no longer scans or rejects results.
                var audited=Energies(actual);double currentTotal=audited.Sum(v=>(double)v);
                double budget=audited.Sum(v=>Math.Abs((double)v))*1.2e-7;
                double balance=Math.Abs(currentTotal-previousTotal-actual.ExternalEnergyJ);
                Check(balance<=budget+1e-5,"independent per-step external energy balance");
                maxBalance=Math.Max(maxBalance,balance);totalRounding+=budget;previousTotal=currentTotal;
                if(j==0) {var calculated=Energies(actual);for(int i=0;i<calculated.Length;i++) Near(calculated[i],(float)reference[i],Math.Max(1e-9,Math.Abs(reference[i])*1.2e-7),"parallel gather matches independent serial pair flux "+i);}
                if(j>=5) {tickTimes.Add(actual.LastTickMilliseconds);jobTimes.Add(actual.LastJobMilliseconds);}
            }
            double drift=Energies(actual).Sum(v=>(double)v)-initial-totalExternal;
            Check(Math.Abs(drift)<=totalRounding+1e-5,"open-system energy minus external flux bounded by float rounding allowance");
            report.Add("Actual effective 60s updates x105: independent max balance error="+maxBalance.ToString("R")+" J; external energy="+totalExternal.ToString("R")+" J; cumulative balance error="+drift.ToString("R")+" J; conservative float rounding budget="+totalRounding.ToString("R")+" J");
            tickTimes.Sort();jobTimes.Sort(); report.Add("Editor Mono, 100 measured ticks after 5 warmups: full step median="+tickTimes[50]+" ms p95="+tickTimes[94]+" ms; Schedule+Complete median="+jobTimes[50]+" ms p95="+jobTimes[94]+" ms; includes persistent buffer reuse/input refresh/validation/direct commit; no runtime result audit");
            // Supported 60s synthetic closed system, full-size owners and unchanged material values.
            var syntheticCells=Enumerable.Range(0,actual.CellCount).Select(actual.GetCell).ToArray();
            var syntheticFaces=new List<ThermalExchange.Face>();
            for(int i=1;i<syntheticCells.Length;i++) syntheticFaces.Add(new ThermalExchange.Face(i-1,i,1e-10,.01,.01));
            var stable60=new ThermalExchange(syntheticCells,syntheticFaces.ToArray()); stable60.Step();
            report.Add("Synthetic weak-contact 60s full-size Job: "+stable60.LastTickMilliseconds+" ms (not actual hive conductance)");
            var bytes=WorldSaveCodec.Encode(Snapshot(recovered)); var loaded=WorldSaveCodec.Decode(bytes); Check(bytes.SequenceEqual(WorldSaveCodec.Encode(loaded)),"V3 exact thermal roundtrip");
            var legacy=WorldSaveCodec.Encode(Snapshot(new GameWorld())); Check(BitConverter.ToInt32(legacy,0)==2,"V2 unchanged"); Check(WorldSaveCodec.Decode(legacy).World.Thermal==null,"V2 no derived graph");
            legacy[0]=1;Array.Resize(ref legacy,legacy.Length-1);Check(WorldSaveCodec.Decode(legacy).World.Weather==null,"V1 unchanged");

            // Weather's own long regression remains independent of the thermal tick.
            var weatherWorld=new GameWorld(new WeatherSimulator(77,WeatherAssetLoader.LoadBaseline()));
            var weatherScheduler=new SimulationScheduler(weatherWorld.Clock);weatherScheduler.Register(weatherWorld.Weather);
            await weatherScheduler.RunToAsync(2881,()=>Task.CompletedTask,()=>Task.CompletedTask,CancellationToken.None);
            var weatherLoaded=WorldSaveCodec.Decode(WorldSaveCodec.Encode(Snapshot(weatherWorld))).World;
            await WeatherAssetLoader.PrepareLoadedWorldAsync(weatherLoaded);
            var resumed=new SimulationScheduler(weatherLoaded.Clock);resumed.Register(weatherLoaded.Weather);
            await weatherScheduler.RunToAsync(6000,()=>Task.CompletedTask,()=>Task.CompletedTask,CancellationToken.None);
            await resumed.RunToAsync(6000,()=>Task.CompletedTask,()=>Task.CompletedTask,CancellationToken.None);
            Check(WeatherBytes(weatherWorld.Weather).SequenceEqual(WeatherBytes(weatherLoaded.Weather)),"6000 minute Weather 2.0 deterministic save/resume");
            report.Add("PASS Weather 2.0 independent 6000-minute save/resume deterministic regression (weather-only regression in addition to coupled runtime checks)");

            ItemAssemblyTests.RunAll(); HiveSpatialGridTests.RunAll(); CellStateTests.RunAll(); EquipmentCellDataTests.RunAll(); CombGeometryTests.RunAll(); CombThermalTests.RunAll(); WorldSaveTests.Run(); await GameTimeTests.RunAll();
            report.Add("PASS assembly/spatial/equipment/cell/comb/save/time regression suites; stage-two checks="+checks);
            ThermalExchange.ReleaseAllBuffers();
            File.WriteAllLines("outputs/persistent-thermal/results.txt",report); Debug.Log("THERMAL STAGE TWO PASS "+checks); if(Application.isBatchMode) EditorApplication.Exit(0);
        }
        catch(Exception e) { ThermalExchange.ReleaseAllBuffers(); report.Add(e.ToString()); File.WriteAllLines("outputs/persistent-thermal/failure.txt",report); Debug.LogException(e); if(Application.isBatchMode) EditorApplication.Exit(1); }
    }
    public static void BuildAndroid()
    {
        var scenes=new[]{"Assets/Scenes/Bootstrap.unity","Assets/Scenes/MainMenu.unity","Assets/Scenes/Apiary.unity","Assets/Scenes/Observation.unity","Assets/Scenes/SharedUI.unity"};
        var result=BuildPipeline.BuildPlayer(new BuildPlayerOptions {scenes=scenes,locationPathName="outputs/persistent-thermal/HoneyComb.apk",target=BuildTarget.Android,options=BuildOptions.Development});
        File.WriteAllText("outputs/persistent-thermal/android.txt",result.summary.result+"; errors="+result.summary.totalErrors+"; time="+result.summary.totalTime);
        if(Application.isBatchMode) EditorApplication.Exit(result.summary.result==BuildResult.Succeeded?0:1);
    }
}
