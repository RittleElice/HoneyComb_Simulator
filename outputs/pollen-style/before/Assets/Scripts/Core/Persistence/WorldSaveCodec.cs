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
/// <summary>Schema 25 adds a queen development limit; schemas 17-24 remain readable.
/// Geometry, observers and rendering caches are derived and rebuilt. Unknown Content fails closed.</summary>
public sealed class WorldSaveCodec
{
    private readonly HashSet<MovableItem> seen = new HashSet<MovableItem>();
    private readonly HashSet<object> contents = new HashSet<object>();
    private List<HiveData> hives;
    private int nodes;
    public const int CurrentSchema = 25;
    private int schema = CurrentSchema;
    private const int CompressedMagic=0x5A434832;
    public static byte[] Encode(WorldSaveSnapshot snapshot)=>EncodeCore(snapshot,true);
    // Uncompressed reflected layout supports comparison audits; both layouts use the current schema.
    public static byte[] EncodeLegacy(WorldSaveSnapshot snapshot)=>EncodeCore(snapshot,false);
    static byte[] EncodeCore(WorldSaveSnapshot snapshot,bool compact)
    {
        ValidateTime(snapshot);
        if(snapshot.World.Thermal?.Failure!=null)throw new InvalidDataException("Unsafe thermal tick cannot be saved: "+snapshot.World.Thermal.Failure);
        bool compress=compact&&snapshot.World.BeeCycle.Bees.Count>=10000;
        using(var destination=new MemoryStream()){
            if(compress){
                using(var header=new BinaryWriter(destination,Encoding.UTF8,true))header.Write(CompressedMagic);
                using(var gzip=new System.IO.Compression.GZipStream(destination,System.IO.Compression.CompressionLevel.Fastest,true))
                using(var writer=new BinaryWriter(gzip,Encoding.UTF8,true))WriteSnapshot(writer,snapshot,compact);
            }else using(var writer=new BinaryWriter(destination,Encoding.UTF8,true))WriteSnapshot(writer,snapshot,compact);
            return destination.ToArray();
        }
    }
    static void WriteSnapshot(BinaryWriter w,WorldSaveSnapshot snapshot,bool compact)
    {WritePrefix(w,snapshot,compact);snapshot.World.BeeLocations.Write(w,snapshot.World);snapshot.World.BeeCycle.Write(w,compact);}
    static void WritePrefix(BinaryWriter w,WorldSaveSnapshot snapshot,bool compact)
    {
            snapshot.World.Thermal?.FlushClimate();
            int version = CurrentSchema;
            w.Write(version); w.Write(snapshot.UtcTicks); w.Write(snapshot.RemainderTicks); w.Write(snapshot.HighWaterUtcTicks); w.Write(snapshot.TargetMinute);
            var world = snapshot.World; w.Write(world.CurrentGameMinute);
            var codec = new WorldSaveCodec { hives = world.Apiary.Hives, schema = version };
            SaveBinary.Text(w,world.Apiary.Name); w.Write(world.Apiary.Grid.UnlockedWidth); w.Write(world.Apiary.Grid.Width); w.Write(world.Apiary.Grid.Height);
            w.Write(codec.hives.Count); var ids=new HashSet<int>();
            foreach (var hive in codec.hives)
            { if (hive == null || !ids.Add(hive.Id)) throw new InvalidDataException("Duplicate or missing hive."); w.Write(hive.Id); w.Write(hive.X); w.Write(hive.Y); }
            foreach (var hive in codec.hives) codec.WriteItem(w,hive.Slot.Value,0);
            for(int y=0;y<world.Apiary.Grid.Height;y++) for(int x=0;x<world.Apiary.Grid.UnlockedWidth;x++) codec.WriteItem(w,world.Apiary.GetItemSlot(x,y).Value,0);
            w.Write(world.Inventory.Capacity);
            for(int i=0;i<world.Inventory.Capacity;i++) codec.WriteItem(w,world.Inventory.GetSlot(i).Value,0);
            codec.WriteItem(w,world.Hand.Value,0);
            w.Write(world.Weather != null);
            if (world.Weather != null) world.Weather.WriteSnapshot(w);
            w.Write(world.LastShopId);
            for (int i = 0; i < world.Warehouse.Capacity; i++)
                codec.WriteItem(w, world.Warehouse.GetSlot(i).Value, 0);
            for (int i = 0; i < world.Trash.Capacity; i++)
                codec.WriteItem(w, world.Trash.GetSlot(i).Value, 0);


    }
    public static async System.Threading.Tasks.Task<byte[]> EncodeAsync(WorldSaveSnapshot snapshot,Func<System.Threading.Tasks.Task> checkpoint)
    {
        ValidateTime(snapshot);if(snapshot.World.Thermal?.Failure!=null)throw new InvalidDataException("Unsafe thermal tick.");
        bool compress=snapshot.World.BeeCycle.Bees.Count>=10000;
        using(var destination=new MemoryStream()){
            if(compress){
                using(var header=new BinaryWriter(destination,Encoding.UTF8,true))header.Write(CompressedMagic);
                using(var gzip=new System.IO.Compression.GZipStream(destination,System.IO.Compression.CompressionLevel.Fastest,true))
                using(var writer=new BinaryWriter(gzip,Encoding.UTF8,true)){WritePrefix(writer,snapshot,true);await snapshot.World.BeeLocations.WriteAsync(writer,snapshot.World,checkpoint);await snapshot.World.BeeCycle.WriteAsync(writer,checkpoint);}
            }else using(var writer=new BinaryWriter(destination,Encoding.UTF8,true)){WritePrefix(writer,snapshot,true);await snapshot.World.BeeLocations.WriteAsync(writer,snapshot.World,checkpoint);await snapshot.World.BeeCycle.WriteAsync(writer,checkpoint);}
            return destination.ToArray();
        }
    }
    public static WorldSaveSnapshot Decode(byte[] bytes)=>DecodeCore(bytes,false);
    public static void Validate(byte[] bytes){var snapshot=DecodeCore(bytes,true);snapshot.World.Thermal?.Dispose();}
    static WorldSaveSnapshot DecodeCore(byte[] bytes,bool validateOnly)
    {
        using(var stream=new MemoryStream(bytes,false))using(var reader=new BinaryReader(stream,Encoding.UTF8,true)){
            int schema=reader.ReadInt32();
            if(schema==CompressedMagic){
                using(var gzip=new System.IO.Compression.GZipStream(stream,System.IO.Compression.CompressionMode.Decompress,true))
                using(var r=new BinaryReader(gzip,Encoding.UTF8,true))return ReadSnapshot(r,r.ReadInt32(),validateOnly);
            }
            return ReadSnapshot(reader,schema,validateOnly);
        }
    }
    static WorldSaveSnapshot ReadSnapshot(BinaryReader r,int schema,bool validateOnly)
    {
            if(schema!=17 && schema!=18 && schema!=19 && schema!=20 && schema!=21 && schema!=22 && schema!=23 && schema!=24 && schema!=CurrentSchema) throw new InvalidDataException("이전 소방 규격의 저장 데이터입니다. 25×12 소방에서는 새 게임을 시작해 주세요.");
            var snapshot=new WorldSaveSnapshot { UtcTicks=r.ReadInt64(), RemainderTicks=r.ReadInt64(), HighWaterUtcTicks=r.ReadInt64(), TargetMinute=r.ReadInt64() };
            var clock=GameClock.Restore(r.ReadInt64()); string name=SaveBinary.Text(r);
            int unlocked=SaveBinary.Count(r,10000), width=SaveBinary.Count(r,10001), height=SaveBinary.Count(r,10000);
            if(unlocked<1 || height<1 || width<=unlocked || (long)width*height>1000000) throw new InvalidDataException("Invalid apiary size.");
            var apiary=new ApiaryData(name,unlocked,height); if(width>apiary.Grid.Width) apiary.Grid.Expand(width,height);
            var codec=new WorldSaveCodec { hives=apiary.Hives, schema=schema }; int count=SaveBinary.Count(r,100000); var ids=new HashSet<int>();
            for(int i=0;i<count;i++) { int id=r.ReadInt32(), x=r.ReadInt32(), y=r.ReadInt32(); if(!ids.Add(id)) throw new InvalidDataException("Duplicate hive ID."); codec.hives.Add(new HiveData(id,x,y)); }
            foreach(var hive in codec.hives) Insert(hive.Slot,codec.ReadItem(r,0));
            for(int y=0;y<height;y++) for(int x=0;x<unlocked;x++) Insert(apiary.GetItemSlot(x,y),codec.ReadItem(r,0));
            int capacity=SaveBinary.Count(r,100000); if(capacity<1) throw new InvalidDataException("Invalid inventory.");
            var inventory=new InventoryData(capacity); for(int i=0;i<capacity;i++) Insert(inventory.GetSlot(i),codec.ReadItem(r,0));
            var hand=new HandData(); Insert(hand.Slot,codec.ReadItem(r,0));
            snapshot.World=new GameWorld(clock,apiary,inventory,hand);
            if(schema>=2 && r.ReadBoolean()) snapshot.World.AttachWeather(WeatherSimulator.ReadSnapshot(r,clock.CurrentGameMinute));
            if (schema >= 10)
            {
                snapshot.World.LastShopId = r.ReadInt32();
                if (snapshot.World.LastShopId < 0) throw new InvalidDataException("Invalid shop ID.");
                var warehouse = snapshot.World.Warehouse;
                for (int i = 0; i < (schema == 10 ? 12 : warehouse.Capacity); i++)
                    Insert(warehouse.GetSlot(i), codec.ReadItem(r, 0));
            }
            if (schema >= 11)
            {
                var trash = snapshot.World.Trash;
                for (int i = 0; i < trash.Capacity; i++)
                    Insert(trash.GetSlot(i), codec.ReadItem(r, 0));
            }
            if(schema>=14) snapshot.World.BeeLocations.Read(r,snapshot.World,schema);
            if(schema >= 19) snapshot.World.BeeCycle.Read(r,validateOnly);
            ValidateTime(snapshot);
            if(r.BaseStream.ReadByte()!=-1) throw new InvalidDataException("Unexpected trailing save data.");
            return snapshot;
    }
    private static void ValidateTime(WorldSaveSnapshot s)
    {
        if(s.World != null && (s.World.BeeCycle.Failure != null || s.World.BeeCycle.LastMinute != s.World.CurrentGameMinute))
            throw new InvalidDataException("Bee cycle is not at a complete save boundary.");
        if(s.World!=null && s.World.Weather!=null && s.World.Weather.Minute!=s.World.CurrentGameMinute)
            throw new InvalidDataException("날씨와 게임 시간의 저장 시점이 다릅니다.");
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
            w.Write((byte)2); w.Write(frame.Width); w.Write(frame.Height); w.Write((int)frame.FacingSide); frame.Front.WriteSnapshot(w,schema>=12); frame.Back.WriteSnapshot(w,schema>=12);
        }
        else if(item.Content is HiveOccupancyProfile profile)
        { w.Write((byte)3); w.Write(profile.FirstY); w.Write(profile.CountY); w.Write((int)profile.Material); }
        else if(item.Content is HeatingPanelData heater)
        { w.Write((byte)4);w.Write(heater.Enabled);w.Write(heater.OutputWatts); }
        else throw new InvalidDataException("저장할 수 없는 장비 내용물: "+item.Content.GetType().Name);
        if(item.InteriorSlots!=null) for(int i=0;i<item.InteriorSlots.Capacity;i++) WriteItem(w,item.InteriorSlots.GetSlot(i).Value,depth+1);
        if(item.TopSlot!=null) WriteItem(w,item.TopSlot.Value,depth+1);
        if(schema >= 3) { w.Write(item.ExistingThermalState != null); item.ExistingThermalState?.WriteSnapshot(w); item.ExistingThermalState?.WriteClimate(w); }
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
        if(kind<0 || kind>(int)(schema>=8?ItemKind.HeatingPanel:ItemKind.InsulationBoard)) throw new InvalidDataException("Unknown item kind.");
        object content=null; byte contentTag=r.ReadByte();
        switch(contentTag)
        {
            case 0: break;
            case 1: int index=r.ReadInt32(); if(index<0||index>=hives.Count) throw new InvalidDataException("Invalid hive reference."); content=hives[index]; break;
            case 2:
                int width=SaveBinary.Count(r,10000), height=SaveBinary.Count(r,10000), side=r.ReadInt32();
                if(width<1||height<1||(long)width*height>1000000||side<0||side>1) throw new InvalidDataException("Invalid frame dimensions.");
                var frame=new FrameData(width,height); frame.SetFacingSide((FrameSide)side); frame.Front.ReadSnapshot(r,schema>=12); frame.Back.ReadSnapshot(r,schema>=12); content=frame; break;
            case 3: content=new HiveOccupancyProfile(r.ReadInt32(),r.ReadInt32(),(HiveMaterial)r.ReadInt32()); break;
            case 4:
                if(schema<8 || kind!=(int)ItemKind.HeatingPanel)throw new InvalidDataException("Invalid heater content.");
                var heater=new HeatingPanelData();if(!heater.TryConfigure(r.ReadBoolean(),r.ReadSingle()))throw new InvalidDataException("Invalid heater power.");content=heater;break;
            default: throw new InvalidDataException("Unknown content type.");
        }
        var item=new ItemData(id,name,(ItemKind)kind,content); if(!item.TrySetAging(aging)) throw new InvalidDataException("Invalid item aging.");
        if(item.InteriorSlots!=null) for(int i=0;i<item.InteriorSlots.Capacity;i++) Insert(item.InteriorSlots.GetSlot(i),ReadItem(r,depth+1));
        if(item.TopSlot!=null) Insert(item.TopSlot,ReadItem(r,depth+1));
        if(schema >= 3 && r.ReadBoolean())
        {
            if(item.ThermalState == null) throw new InvalidDataException("Thermal state on unsupported equipment");
            item.ThermalState.ReadSnapshot(r, schema);
            if(schema>=12)item.ThermalState.ReadClimate(r, schema);
        }
        return item;
    }
    private static void Insert(ItemSlot slot,MovableItem value)
    { if(value!=null && !slot.TryInsertValue(value)) throw new InvalidDataException("Invalid item placement."); }
}

