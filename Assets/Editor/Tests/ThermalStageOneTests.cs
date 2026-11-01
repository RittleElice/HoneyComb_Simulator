using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class ThermalStageOneTests
{
    static ThermalStageOneTests() { EditorApplication.update += Watch; }
    static void Watch()
    {
        const string trigger = "Temp/run-thermal-stage-one";
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists(trigger)) return;
        File.Delete(trigger); RunBatch();
    }
    static int checks;
    static void Check(bool b, string message) { checks++; if (!b) throw new Exception(message); }
    static void Near(double a, double b, double tolerance, string message) => Check(Math.Abs(a-b) <= tolerance, message + ": " + a + " != " + b);
    static void Throws(Action action, string message) { bool caught = false; try { action(); } catch (ArgumentException) { caught = true; } catch (InvalidOperationException) { caught = true; } Check(caught, message); }
    static WorldSaveSnapshot Snapshot(GameWorld w) => new WorldSaveSnapshot { World = w, UtcTicks = DateTime.UtcNow.Ticks, TargetMinute = w.CurrentGameMinute };
    static byte[] WeatherBytes(WeatherSimulator w) { using(var s=new MemoryStream()) using(var b=new BinaryWriter(s)) { w.WriteSnapshot(b); return s.ToArray(); } }
    public static async void RunBatch()
    {
        checks = 0;
        Directory.CreateDirectory("outputs/thermal-stage-one");
        try
        {
            var m = ThermalMaterials.Load();
            foreach (ThermalMaterialKind kind in Enum.GetValues(typeof(ThermalMaterialKind)))
            {
                var a = Resources.Load<ThermalMaterialAsset>("Thermal/"+kind);
                Check(a != null && a.Kind == kind && a.ReferenceTemperatureC == 20, "material asset "+kind);
                Check(!string.IsNullOrWhiteSpace(a.Sources) && !string.IsNullOrWhiteSpace(a.Assumptions), "material provenance "+kind);
                Check(ThermalMath.Positive(a.Read().Density), "material valid");
            }
            Throws(()=>new ThermalMaterialValue(float.NaN, 1000, .1f), "NaN rejected");
            Throws(()=>new ThermalMaterialValue(1000, 0, .1f), "zero cp rejected");
            Throws(()=>new ThermalMaterialValue(1000, 1000, float.PositiveInfinity), "infinite k rejected");
            var wood = m[ThermalMaterialKind.Wood];
            ThermalMath.Mix(.001f, 0, wood, 0, m, out float air, out float capacity, out float k);
            Near(air,.001,1e-9,"litre air volume"); Near(capacity,1.2048,1e-6,"air capacity units"); Near(k,.0254,1e-7,"air k");
            ThermalMath.Mix(.001f, .001f, wood, 0, m, out air, out capacity, out k);
            Near(air,0,0,"wood displaces all air"); Near(capacity,800,.0001,"wood capacity"); Near(k,.12,1e-7,"wood conductivity");
            ThermalMath.Mix(.001f, 0, wood, .095f, m, out air, out capacity, out k);
            Near(air,.0009,1e-9,"wax displaces air"); Near(capacity,220.495+1.08432,.0001,"wax plus residual air"); Near(k,.04786,1e-7,"volume weighted k");
            Throws(()=>ThermalMath.Mix(.001f,.0011f,wood,0,m,out _,out _,out _),"solid overflow");
            Throws(()=>ThermalMath.Mix(.001f,0,wood,1,m,out _,out _,out _),"wax overflow");
            Throws(()=>ThermalMath.Mix(0,0,wood,0,m,out _,out _,out _),"zero volume");
            Throws(()=>ThermalMath.LogicalIndex(84,0,FrameSide.Front),"column bound");
            Throws(()=>ThermalMath.LogicalIndex(0,40,FrameSide.Back),"row bound");
            Throws(()=>ThermalMath.LogicalIndex(0,0,(FrameSide)2),"side bound");
            var counts = new int[420];
            foreach(var side in new[]{FrameSide.Front,FrameSide.Back}) for(int z=0;z<40;z++) for(int x=0;x<84;x++) counts[ThermalMath.LogicalIndex(x,z,side)]++;
            for(int z=0;z<10;z++) for(int y=0;y<3;y++) for(int x=0;x<14;x++) Check(counts[x+14*(y+3*z)] == (y==1?0:24),"6x4 14x3x10 mapping");
            var f = new FrameData(); var frame = new ItemData(900,"thermal frame",ItemKind.Frame,f); var t = frame.ThermalState; var g = frame.EquipmentGrid;
            Check(t.Count==420,"equipment grid 14x3x10"); Check(t.DirtyIndices.Count==420,"initial dirty list");
            t.MarkDirty(0); Check(t.DirtyIndices.Count==420,"dirty deduplication");
            t.Refresh(m,10);
            Near(t.WaxMassKg.Sum(v=>(double)v),.0399,1e-8,"clipped foundation 39.9g");
            Check(t.DirtyIndices.Count==0,"clean cache");
            for(int i=0;i<t.Count;i++) { Near(t.TemperatureC(i),10,1e-5,"initial fixed temperature"); }
            for(int z=0;z<10;z++) for(int y=0;y<3;y++) for(int x=0;x<14;x++) if(g.GetCell(x,y,z).Material==HiveMaterial.Wood) Near(t.WaxMassKg[g.Index(x,y,z)],0,0,"no wax inside wood");
            float[] before=t.EnergyJ.ToArray(); f.Front.TrySetConstruction(0,0,50); t.Refresh(m,30);
            Near(t.WaxMassKg.Sum(v=>(double)v),.0399+7.5e-6,1e-8,"half worker wax mass");
            Check(before.SequenceEqual(t.EnergyJ),"dirty rebuild preserves energy, ignores changed outside temperature");
            f.Front.TrySetType(0,0,CombCellType.Drone); t.Refresh(m,30);
            Near(t.WaxMassKg.Sum(v=>(double)v),.0399+10e-6,1e-8,"half drone wax mass");
            f.Front.SetAllConstruction(100); f.Back.SetAllConstruction(100); t.Refresh(m,-10);
            Near(t.LogicalWaxMassKg.Sum(v=>(double)v),.1008+5e-6,1e-7,"all front/back logical wax");
            Near(t.WaxMassKg.Sum(v=>(double)v),.0399+.1008+5e-6,1e-7,"projection conserves all wax");
            float[] cap=t.HeatCapacityJPerK.ToArray(); f.Front.TryAddNectar(0,0,0,0,100); f.Back.TryAddPollen(0,0,100); f.Front.TrySetBrood(1,0,1234); t.Refresh(m,0);
            Check(cap.SequenceEqual(t.HeatCapacityJPerK),"nectar pollen brood excluded from stage1");
            f.Flip(); t.Refresh(m,0); Check(cap.SequenceEqual(t.HeatCapacityJPerK),"visual flip does not swap physical faces");
            t.RestoreEnergy(g.Index(1,0,1),-3.25f); t.MarkAllDirty(); t.Refresh(m,99); Near(t.EnergyJ[g.Index(1,0,1)],-3.25,0,"restore preserves signed energy");
            Throws(()=>t.RestoreEnergy(0,float.NaN),"invalid energy");
            Throws(()=>t.MarkDirty(420),"dirty index bounds");
            var ins=new ItemData(901,"insulation",ItemKind.InsulationBoard); ins.ThermalState.Refresh(m,20);
            Near(ins.ThermalState.AirVolumeM3[0],0,0,"EPS bulk pores not counted again");
            var ex=new ItemData(902,"excluder",ItemKind.QueenExcluder); ex.ThermalState.Refresh(m,20); Check(ex.ThermalState.Count==0,"zero thickness excluder has no invented mass");
            var legacyFrame=new FrameData(78,43); legacyFrame.Front.SetAllConstruction(100);
            var legacyItem=new ItemData(904,"legacy",ItemKind.Frame,legacyFrame); legacyItem.ThermalState.Refresh(m,20);
            Check(legacyItem.ThermalState.MappingNotice!=null,"legacy geometry disclosed");
            Near(legacyItem.ThermalState.WaxMassKg.Sum(v=>(double)v),.0399+78*43*15e-6,1e-7,"legacy mass preserved");

            var baseline=WeatherAssetLoader.LoadBaseline(); var world=new GameWorld(new WeatherSimulator(42,baseline));
            var weatherBefore=WeatherBytes(world.Weather); world.PrepareThermal(m);
            Check(weatherBefore.SequenceEqual(WeatherBytes(world.Weather)),"thermal init does not advance weather or RNG");
            foreach(var item in ThermalWorld.Items(world)) if(item.ThermalState!=null) for(int i=0;i<item.ThermalState.Count;i++)
                Near(item.ThermalState.TemperatureC(i),world.Weather.Current.TemperatureCelsius,.0001,"actual weather init");
            var box=world.Apiary.GetItemSlot(0,0).Item; var savedFrame=world.Inventory.GetSlot(4).Take();
            Check(box.InteriorSlots.GetSlot(0).TryInsert(savedFrame),"insert frame"); world.Thermal.Refresh();
            int local=savedFrame.EquipmentGrid.Index(1,0,1); savedFrame.ThermalState.RestoreEnergy(local,123.25f);
            int global=-1; for(int i=0;i<box.SpatialGrid.CellCount;i++) if(ReferenceEquals(box.SpatialGrid.GetCell(i).SourceCell,savedFrame.EquipmentGrid.GetCell(1,0,1))) {global=i;break;}
            var view=world.Thermal.View(box.SpatialGrid); Near(view.EnergyJ(global),123.25,0,"hive uses equipment original energy");
            box.InteriorSlots.GetSlot(0).Take(); world.Hand.Slot.TryInsert(savedFrame); world.Thermal.Refresh(17);
            Near(view.TemperatureC(global),17,.00001,"new vacancy air initialized with current boundary");
            Near(savedFrame.ThermalState.EnergyJ[local],123.25,0,"detached energy persists");
            var bytes=WorldSaveCodec.Encode(Snapshot(world)); Check(BitConverter.ToInt32(bytes,0)==3,"thermal schema3");
            var loaded=WorldSaveCodec.Decode(bytes); Check(bytes.SequenceEqual(WorldSaveCodec.Encode(loaded)),"schema3 byte identical roundtrip before caches");
            loaded.World.PrepareThermal(m); Near(loaded.World.Hand.Item.ThermalState.EnergyJ[local],123.25,0,"loaded energy survives weather initialization");
            Check(bytes.SequenceEqual(WorldSaveCodec.Encode(loaded)),"rebuilding caches does not change saved energy");
            var legacy=WorldSaveCodec.Encode(Snapshot(new GameWorld())); Check(BitConverter.ToInt32(legacy,0)==2,"no thermal state retains schema2");
            var legacy2=WorldSaveCodec.Decode(legacy); Check(ThermalWorld.Items(legacy2.World).All(i=>i.ExistingThermalState==null),"old save no invented thermal energy");
            legacy[0]=1; Array.Resize(ref legacy,legacy.Length-1); Check(WorldSaveCodec.Decode(legacy).World.Weather==null,"schema1 migration");
            var scheduler=new SimulationScheduler(world.Clock); scheduler.Register(world.Weather); float energyBefore=savedFrame.ThermalState.EnergyJ[local];
            await scheduler.RunToAsync(61,()=>Task.CompletedTask,()=>Task.CompletedTask,CancellationToken.None);
            world.Thermal.Refresh(); Near(savedFrame.ThermalState.EnergyJ[local],energyBefore,0,"61 minutes no thermal solver");
            Check(scheduler.ParticipantCount==1,"scheduler weather only");
            var newcomer=new ItemData(903,"new frame",ItemKind.Frame,new FrameData()); world.Inventory.GetSlot(4).TryInsert(newcomer); world.Thermal.Refresh();
            Near(newcomer.ThermalState.TemperatureC(0),world.Weather.Current.TemperatureCelsius,.0001,"new equipment uses latest weather");
            var control=WorldSaveCodec.Decode(WorldSaveCodec.Encode(Snapshot(world))); await WeatherAssetLoader.PrepareLoadedWorldAsync(control.World);
            var next=new SimulationScheduler(control.World.Clock); next.Register(control.World.Weather);
            await scheduler.RunToAsync(120,()=>Task.CompletedTask,()=>Task.CompletedTask,CancellationToken.None);
            await next.RunToAsync(120,()=>Task.CompletedTask,()=>Task.CompletedTask,CancellationToken.None);
            Check(WeatherBytes(world.Weather).SequenceEqual(WeatherBytes(control.World.Weather)),"saved weather deterministic continuation");
            File.WriteAllText("outputs/thermal-stage-one/thermal-tests.txt","PASS "+checks+" thermal checks\n");
            ItemAssemblyTests.RunAll(); HiveSpatialGridTests.RunAll(); CellStateTests.RunAll(); EquipmentCellDataTests.RunAll(); CombGeometryTests.RunAll(); CombThermalTests.RunAll();
            WorldSaveTests.Run(); await GameTimeTests.RunAll();
            File.WriteAllText("outputs/thermal-stage-one/regression-tests.txt","PASS assembly, spatial, cell state, equipment, comb geometry, legacy thermal, world save, game time tests\n");
            Debug.Log("THERMAL STAGE ONE PASS "+checks); if (Application.isBatchMode) EditorApplication.Exit(0);
        }
        catch(Exception e) { Debug.LogException(e); File.WriteAllText("outputs/thermal-stage-one/failure.txt",e.ToString()); if (Application.isBatchMode) EditorApplication.Exit(1); }
    }
}
