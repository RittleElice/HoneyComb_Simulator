using System;
using System.IO;
using System.Text;
using System.Collections.Generic;

public sealed class WorldSaveSnapshot
{
    public GameWorld World;
    public long UtcTicks, RemainderTicks, HighWaterUtcTicks, TargetMinute;
}
internal static class SaveBinary
{
    public static int Count(BinaryReader r, int max)
    { int n = r.ReadInt32(); if (n < 0 || n > max) throw new InvalidDataException("Invalid save collection length."); return n; }
    public static void Text(BinaryWriter w, string s)
    { var bytes = Encoding.UTF8.GetBytes(s ?? ""); if (bytes.Length > 65536) throw new InvalidDataException("Text too large."); w.Write(bytes.Length); w.Write(bytes); }
    public static string Text(BinaryReader r)
    { int n = Count(r,65536); var b=r.ReadBytes(n); if(b.Length!=n) throw new EndOfStreamException(); return new UTF8Encoding(false,true).GetString(b); }
}
/// <summary>Schema 1 covers every authoritative field in the current GameWorld model.
/// Geometry, observers and rendering caches are derived and rebuilt. Unknown Content fails closed.</summary>
public sealed class WorldSaveCodec
{
    private readonly HashSet<MovableItem> seen = new HashSet<MovableItem>();
    private readonly HashSet<object> contents = new HashSet<object>();
    private List<HiveData> hives;
    private int nodes;
    public static byte[] Encode(WorldSaveSnapshot snapshot)
    {
        ValidateTime(snapshot);
        using (var stream = new MemoryStream())
        using (var w = new BinaryWriter(stream,Encoding.UTF8,true))
        {
            w.Write(1); w.Write(snapshot.UtcTicks); w.Write(snapshot.RemainderTicks); w.Write(snapshot.HighWaterUtcTicks); w.Write(snapshot.TargetMinute);
            var world = snapshot.World; w.Write(world.CurrentGameMinute);
            var codec = new WorldSaveCodec { hives = world.Apiary.Hives };
            SaveBinary.Text(w,world.Apiary.Name); w.Write(world.Apiary.Grid.UnlockedWidth); w.Write(world.Apiary.Grid.Width); w.Write(world.Apiary.Grid.Height);
            w.Write(codec.hives.Count); var ids=new HashSet<int>();
            foreach (var hive in codec.hives)
            { if (hive == null || !ids.Add(hive.Id)) throw new InvalidDataException("Duplicate or missing hive."); w.Write(hive.Id); w.Write(hive.X); w.Write(hive.Y); }
            foreach (var hive in codec.hives) codec.WriteItem(w,hive.Slot.Value,0);
            for(int y=0;y<world.Apiary.Grid.Height;y++) for(int x=0;x<world.Apiary.Grid.UnlockedWidth;x++) codec.WriteItem(w,world.Apiary.GetItemSlot(x,y).Value,0);
            w.Write(world.Inventory.Capacity);
            for(int i=0;i<world.Inventory.Capacity;i++) codec.WriteItem(w,world.Inventory.GetSlot(i).Value,0);
            codec.WriteItem(w,world.Hand.Value,0);
            return stream.ToArray();
        }
    }
    public static WorldSaveSnapshot Decode(byte[] bytes)
    {
        using(var stream=new MemoryStream(bytes,false))
        using(var r=new BinaryReader(stream,Encoding.UTF8,true))
        {
            if(r.ReadInt32()!=1) throw new InvalidDataException("지원하지 않는 저장 버전입니다.");
            var snapshot=new WorldSaveSnapshot { UtcTicks=r.ReadInt64(), RemainderTicks=r.ReadInt64(), HighWaterUtcTicks=r.ReadInt64(), TargetMinute=r.ReadInt64() };
            var clock=GameClock.Restore(r.ReadInt64()); string name=SaveBinary.Text(r);
            int unlocked=SaveBinary.Count(r,10000), width=SaveBinary.Count(r,10001), height=SaveBinary.Count(r,10000);
            if(unlocked<1 || height<1 || width<=unlocked || (long)width*height>1000000) throw new InvalidDataException("Invalid apiary size.");
            var apiary=new ApiaryData(name,unlocked,height); if(width>apiary.Grid.Width) apiary.Grid.Expand(width,height);
            var codec=new WorldSaveCodec { hives=apiary.Hives }; int count=SaveBinary.Count(r,100000); var ids=new HashSet<int>();
            for(int i=0;i<count;i++) { int id=r.ReadInt32(), x=r.ReadInt32(), y=r.ReadInt32(); if(!ids.Add(id)) throw new InvalidDataException("Duplicate hive ID."); codec.hives.Add(new HiveData(id,x,y)); }
            foreach(var hive in codec.hives) Insert(hive.Slot,codec.ReadItem(r,0));
            for(int y=0;y<height;y++) for(int x=0;x<unlocked;x++) Insert(apiary.GetItemSlot(x,y),codec.ReadItem(r,0));
            int capacity=SaveBinary.Count(r,100000); if(capacity<1) throw new InvalidDataException("Invalid inventory.");
            var inventory=new InventoryData(capacity); for(int i=0;i<capacity;i++) Insert(inventory.GetSlot(i),codec.ReadItem(r,0));
            var hand=new HandData(); Insert(hand.Slot,codec.ReadItem(r,0));
            snapshot.World=new GameWorld(clock,apiary,inventory,hand); ValidateTime(snapshot);
            if(stream.Position!=stream.Length) throw new InvalidDataException("Unexpected trailing save data.");
            return snapshot;
        }
    }
    private static void ValidateTime(WorldSaveSnapshot s)
    {
        if(s.World==null || s.UtcTicks<DateTime.MinValue.Ticks || s.UtcTicks>DateTime.MaxValue.Ticks || s.HighWaterUtcTicks<0 || s.HighWaterUtcTicks>DateTime.MaxValue.Ticks ||
            s.RemainderTicks<0 || s.RemainderTicks>=5*TimeSpan.TicksPerSecond || s.TargetMinute<s.World.CurrentGameMinute)
            throw new InvalidDataException("Invalid saved timeline.");
    }
    private void WriteItem(BinaryWriter w,MovableItem value,int depth)
    {
        if(value==null) { w.Write((byte)0); return; }
        if(depth>64 || ++nodes>100000 || !seen.Add(value)) throw new InvalidDataException("Cyclic or duplicate item ownership.");
        if(value is ItemAssemblyData assembly)
        { w.Write((byte)2); w.Write(assembly.Parts.Count); foreach(var part in assembly.Parts) WriteItem(w,part,depth+1); return; }
        if(!(value is ItemData item)) throw new InvalidDataException("Unsupported movable item.");
        w.Write((byte)1); w.Write(item.Id); SaveBinary.Text(w,item.Name); w.Write((int)item.Kind); w.Write(item.Aging);
        if(item.Content==null) w.Write((byte)0);
        else if(item.Content is HiveData hive)
        { int index=hives.IndexOf(hive); if(index<0) throw new InvalidDataException("Unregistered hive content."); w.Write((byte)1); w.Write(index); }
        else if(item.Content is FrameData frame)
        {
            if(!contents.Add(frame)) throw new InvalidDataException("Shared frame content is unsupported.");
            w.Write((byte)2); w.Write(frame.Width); w.Write(frame.Height); w.Write((int)frame.FacingSide); frame.Front.WriteSnapshot(w); frame.Back.WriteSnapshot(w);
        }
        else if(item.Content is HiveOccupancyProfile profile)
        { w.Write((byte)3); w.Write(profile.FirstY); w.Write(profile.CountY); w.Write((int)profile.Material); }
        else throw new InvalidDataException("저장할 수 없는 장비 내용물: "+item.Content.GetType().Name);
        if(item.InteriorSlots!=null) for(int i=0;i<item.InteriorSlots.Capacity;i++) WriteItem(w,item.InteriorSlots.GetSlot(i).Value,depth+1);
        if(item.TopSlot!=null) WriteItem(w,item.TopSlot.Value,depth+1);
    }
    private MovableItem ReadItem(BinaryReader r,int depth)
    {
        byte tag=r.ReadByte(); if(tag==0) return null;
        if(depth>64 || ++nodes>100000) throw new InvalidDataException("Item graph too large.");
        if(tag==2)
        {
            int n=SaveBinary.Count(r,10000); if(n<2) throw new InvalidDataException("Invalid assembly."); var parts=new List<ItemData>();
            for(int i=0;i<n;i++) { var part=ReadItem(r,depth+1) as ItemData; if(part==null) throw new InvalidDataException("Invalid assembly member."); parts.Add(part); }
            return new ItemAssemblyData(parts);
        }
        if(tag!=1) throw new InvalidDataException("Invalid item tag.");
        int id=r.ReadInt32(); string name=SaveBinary.Text(r); int kind=r.ReadInt32(); float aging=r.ReadSingle();
        if(kind<0 || kind>(int)ItemKind.InsulationBoard) throw new InvalidDataException("Unknown item kind.");
        object content=null; byte contentTag=r.ReadByte();
        switch(contentTag)
        {
            case 0: break;
            case 1: int index=r.ReadInt32(); if(index<0||index>=hives.Count) throw new InvalidDataException("Invalid hive reference."); content=hives[index]; break;
            case 2:
                int width=SaveBinary.Count(r,10000), height=SaveBinary.Count(r,10000), side=r.ReadInt32();
                if(width<1||height<1||(long)width*height>1000000||side<0||side>1) throw new InvalidDataException("Invalid frame dimensions.");
                var frame=new FrameData(width,height); frame.SetFacingSide((FrameSide)side); frame.Front.ReadSnapshot(r); frame.Back.ReadSnapshot(r); content=frame; break;
            case 3: content=new HiveOccupancyProfile(r.ReadInt32(),r.ReadInt32(),(HiveMaterial)r.ReadInt32()); break;
            default: throw new InvalidDataException("Unknown content type.");
        }
        var item=new ItemData(id,name,(ItemKind)kind,content); if(!item.TrySetAging(aging)) throw new InvalidDataException("Invalid item aging.");
        if(item.InteriorSlots!=null) for(int i=0;i<item.InteriorSlots.Capacity;i++) Insert(item.InteriorSlots.GetSlot(i),ReadItem(r,depth+1));
        if(item.TopSlot!=null) Insert(item.TopSlot,ReadItem(r,depth+1));
        return item;
    }
    private static void Insert(ItemSlot slot,MovableItem value)
    { if(value!=null && !slot.TryInsertValue(value)) throw new InvalidDataException("Invalid item placement."); }
}
