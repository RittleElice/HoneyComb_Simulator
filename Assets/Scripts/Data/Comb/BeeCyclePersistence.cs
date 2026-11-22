using System;
using System.Collections;
using System.IO;
using System.Reflection;

public sealed partial class BeeCycleRuntime
{
    public int SnapshotSize(){using(var s=new MemoryStream())using(var w=new BinaryWriter(s)){Write(w);return checked((int)s.Length);}}
    internal void Write(BinaryWriter w)
    {
        if(Failure!=null||LastMinute!=World.CurrentGameMinute)throw new InvalidDataException("Incomplete bee tick.");
        w.Write(2);w.Write(LastMinute);w.Write(RandomState);w.Write(nextId);
        CycleBinary.Write(w,typeof(BeeCycleSettings),Settings);CycleBinary.Write(w,Bees.GetType(),Bees);CycleBinary.Write(w,typeof(CycleCounters),Counters);CycleBinary.Write(w,Commands.GetType(),Commands);
    }
    internal void Read(BinaryReader r)
    {
        int version=r.ReadInt32();if(version!=1&&version!=2)throw new InvalidDataException("Unknown bee cycle version.");
        LastMinute=r.ReadInt64();RandomState=r.ReadUInt32();nextId=r.ReadInt64();
        Settings=(BeeCycleSettings)CycleBinary.Read(r,typeof(BeeCycleSettings),version);if(version==1)Settings.UpgradeForageFormula();rules=new BeeCycleRules(Settings);
        Bees=(System.Collections.Generic.List<CycleBee>)CycleBinary.Read(r,typeof(System.Collections.Generic.List<CycleBee>),version);
        Counters=(CycleCounters)CycleBinary.Read(r,typeof(CycleCounters),version);Commands=(System.Collections.Generic.List<CycleCommand>)CycleBinary.Read(r,typeof(System.Collections.Generic.List<CycleCommand>),version);byId.Clear();
        if(LastMinute!=World.CurrentGameMinute||RandomState==0||nextId<=0||Counters.actions.Length!=15)throw new InvalidDataException("Invalid bee clock.");
        foreach(var b in Bees)
        {
            if(b.forageSource < -1 || b.forageSource >= (World.Weather?.Nectar?.Sources.Length??0))throw new InvalidDataException("Invalid forage source.");
            if(b.id<=0||byId.ContainsKey(b.id)||b.reserve<0||b.energy<0||b.health<0||b.development<0||b.cargo.pollen<0||b.pathIndex<0||b.pathIndex>b.path.Count||b.progress<0)throw new InvalidDataException("Invalid bee state.");
            if(b.stage!=CycleStage.Dead&&!b.outside&&!World.BeeLocations.TryGet(b.id,out _))throw new InvalidDataException("Missing bee location.");
            foreach(var s in b.cargo.sugars)if(s.mass<0||s.source<0)throw new InvalidDataException("Invalid cargo.");
            byId.Add(b.id,b);nextId=Math.Max(nextId,b.id+1);
        }
    }
}
[AttributeUsage(AttributeTargets.Field)]
internal sealed class CycleSinceAttribute : Attribute
{
    public readonly int Version;
    public CycleSinceAttribute(int version){Version=version;}
}
internal static class CycleBinary
{
    static FieldInfo[] Fields(Type t)=>t.GetFields(BindingFlags.Instance|BindingFlags.Public);
    public static void Write(BinaryWriter w,Type t,object v)
    {
        if(t==typeof(double)){double n=(double)v;if(double.IsNaN(n)||double.IsInfinity(n))throw new InvalidDataException("Nonfinite value.");w.Write(n);}
        else if(t==typeof(int))w.Write((int)v);else if(t==typeof(long))w.Write((long)v);else if(t==typeof(bool))w.Write((bool)v);else if(t==typeof(string))SaveBinary.Text(w,(string)v);else if(t.IsEnum)w.Write(Convert.ToInt32(v));
        else if(t.IsArray||typeof(IList).IsAssignableFrom(t)){var a=(IList)v;w.Write(a.Count);var item=t.IsArray?t.GetElementType():t.GetGenericArguments()[0];foreach(var x in a)Write(w,item,x);}
        else foreach(var f in Fields(t))Write(w,f.FieldType,f.GetValue(v));
    }
    public static object Read(BinaryReader r,Type t,int version=2)
    {
        if(t==typeof(double)){double n=r.ReadDouble();if(double.IsNaN(n)||double.IsInfinity(n))throw new InvalidDataException("Nonfinite value.");return n;}
        if(t==typeof(int))return r.ReadInt32();if(t==typeof(long))return r.ReadInt64();if(t==typeof(bool))return r.ReadBoolean();if(t==typeof(string))return SaveBinary.Text(r);
        if(t.IsEnum){int n=r.ReadInt32();if(!Enum.IsDefined(t,n))throw new InvalidDataException("Invalid enum.");return Enum.ToObject(t,n);}
        if(t.IsArray||typeof(IList).IsAssignableFrom(t)){int n=SaveBinary.Count(r,1000000);var item=t.IsArray?t.GetElementType():t.GetGenericArguments()[0];var a=(IList)(t.IsArray?Array.CreateInstance(item,n):Activator.CreateInstance(t));for(int i=0;i<n;i++){var v=Read(r,item,version);if(t.IsArray)a[i]=v;else a.Add(v);}return a;}
        var result=Activator.CreateInstance(t);foreach(var f in Fields(t))if((f.GetCustomAttribute<CycleSinceAttribute>()?.Version??1)<=version)f.SetValue(result,Read(r,f.FieldType,version));return result;
    }
}
