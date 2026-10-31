using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class WorldSaveTests
{
    private static int checks;
    private static void Check(bool ok,string message) { checks++; if(!ok) throw new Exception(message); }
    public static void Run()
    {
        var world=new GameWorld(); var box=world.Apiary.GetItemSlot(0,0).Item;
        var frameItem=world.Inventory.GetSlot(4).Take(); var frame=frameItem.GetContent<FrameData>();
        frame.SetFacingSide(FrameSide.Back); frameItem.TrySetAging(.63f);
        frame.Front.TrySetConstruction(0,0,100); frame.Front.TrySetConstruction(0,0,60);
        Check(frame.Front.TryAddNectar(0,0,12,30,12),"nectar"); Check(frame.Front.TryAddNectar(0,0,23,20,5),"mixed nectar");
        frame.Front.TrySetCappingProgress(0,0,57); frame.Front.TrySetType(0,0,CombCellType.Drone);
        frame.Front.TrySetConstruction(1,0,60); // historical incomplete, same construction as completed cell 0
        frame.Front.TrySetConstruction(2,0,100); Check(frame.Front.TryAddPollen(2,0,10),"pollen");
        frame.Front.TrySetConstruction(3,0,100); Check(frame.Front.TrySetBrood(3,0,9876543212345),"brood ID");
        frame.Back.TrySetConstruction(5,1,100); Check(frame.Back.TrySetStoredContent(5,1,CellContentKind.Nectar,.4f),"legacy content");
        Check(box.InteriorSlots.GetSlot(0).TryInsert(frameItem),"interior ownership");
        Check(box.TopSlot.TryInsert(world.Inventory.GetSlot(9).Take()),"top slot");
        Check(world.Apiary.GetItemSlot(0,0).TryCombineFrom(world.Inventory.GetSlot(1)),"assembly");
        Check(world.Inventory.GetSlot(5).TryMoveTo(world.Hand.Slot),"hand");
        world.Apiary.Grid.TryUnlockNextColumn(); world.Apiary.Grid.TryUnlockNextColumn();
        Check(world.Inventory.GetSlot(10).TryInsert(new ItemData(200,"custom insulation",ItemKind.InsulationBoard,new HiveOccupancyProfile(2,1,HiveMaterial.Insulation))),"profile");
        world.Apiary.Hives[0].Slot.TryInsert(new ItemData(201,"legacy hive slot"));
        new SimulationScheduler(world.Clock).RunToAsync(1234, () => System.Threading.Tasks.Task.CompletedTask, () => System.Threading.Tasks.Task.CompletedTask, System.Threading.CancellationToken.None).GetAwaiter().GetResult();
        var saved=new WorldSaveSnapshot { World=world, UtcTicks=new DateTime(2026,10,31,0,0,0,DateTimeKind.Utc).Ticks, RemainderTicks=123456, HighWaterUtcTicks=0, TargetMinute=2000 };
        byte[] original=WorldSaveCodec.Encode(saved); var loaded=WorldSaveCodec.Decode(original);
        Check(original.SequenceEqual(WorldSaveCodec.Encode(loaded)),"byte-identical roundtrip authoritative state");
        var assembly=(ItemAssemblyData)loaded.World.Apiary.GetItemSlot(0,0).Value;
        var restoredBox=assembly.Parts[0]; var restored=restoredBox.InteriorSlots.GetSlot(0).Item.GetContent<FrameData>();
        Check(ReferenceEquals(restoredBox.Content,loaded.World.Apiary.Hives[0]),"shared hive reference");
        Check(restored.Front.GetCell(0,0).IsComplete&&!restored.Front.GetCell(1,0).IsComplete,"completion hysteresis");
        Check(restored.Front.GetBeeId(3,0)==9876543212345 && restored.Front.GetTotalSugarMassMg(0,0)==50,"quantities exact");
        Check(loaded.World.Hand.Slot.TryMoveTo(loaded.World.Inventory.GetSlot(5)),"restored owner can move");
        Check(!loaded.World.Inventory.GetSlot(4).TryInsertValue(assembly),"cannot duplicate restored owner");
        string directory=Path.Combine(Path.GetTempPath(),"HoneyComb-SaveTest-"+Guid.NewGuid().ToString("N")); var store=new WorldSaveStore(directory);
        store.Save(original); saved.TargetMinute=3000; store.Save(WorldSaveCodec.Encode(saved));
        Check(store.Load(out var recovered).TargetMinute==3000&&!recovered,"newest generation");
        File.WriteAllText(Path.Combine(directory,"world-b.save"),"interrupted/corrupt");
        Check(store.Load(out recovered).TargetMinute==2000&&recovered,"fallback to valid previous generation");
        File.WriteAllText(Path.Combine(directory,"world-a.save.tmp"),"incomplete temporary file");
        Check(store.Load(out _).TargetMinute==2000,"ignore interrupted temporary write");
        store.Save(WorldSaveCodec.Encode(saved)); Check(store.Load(out _).TargetMinute==3000,"recover after corrupt generation");
        var previous=File.ReadAllBytes(Path.Combine(directory,"world-a.save"));
        saved.World.Inventory.GetSlot(11).TryInsert(new ItemData(999,"unsupported",new object()));
        bool rejected=false; try { WorldSaveCodec.Encode(saved); } catch(InvalidDataException) { rejected=true; }
        Check(rejected && previous.SequenceEqual(File.ReadAllBytes(Path.Combine(directory,"world-a.save"))),"unknown state fails without overwriting save");
        bool invalid=false; try { store.Save(new byte[]{1,2,3}); } catch(IOException) { invalid=true; }
        Check(invalid && store.Load(out _).TargetMinute==3000,"invalid snapshot cannot replace good generation");
        File.WriteAllText(Path.Combine(directory,"world-a.save"),"bad"); File.WriteAllText(Path.Combine(directory,"world-b.save"),"bad");
        rejected=false; try { store.Load(out _); } catch(InvalidDataException) { rejected=true; } Check(rejected,"both corrupt fail closed");
        File.WriteAllText("save-tests.txt","PASS "+checks+" save/restore/integrity checks. Test files: "+directory);
    }
    public static async void RunBatch()
    {
        try { Run(); await GameTimeTests.RunAll(); ItemAssemblyTests.RunAll(); HiveSpatialGridTests.RunAll(); CellStateTests.RunAll(); CombThermalTests.RunAll(); EquipmentCellDataTests.RunAll(); UnifiedButtonTests.RunBatch(); }
        catch(Exception e) { Debug.LogException(e); File.WriteAllText("save-tests.txt",e.ToString()); EditorApplication.Exit(1); }
    }
}

