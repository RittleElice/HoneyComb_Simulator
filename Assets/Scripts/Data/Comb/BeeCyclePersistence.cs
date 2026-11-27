using System;
using System.Collections;
using System.IO;
using System.Reflection;

public sealed partial class BeeCycleRuntime
{
    public int SnapshotSize(){using(var s=new MemoryStream())using(var w=new BinaryWriter(s)){Write(w);return checked((int)s.Length);}}
    internal void Write(BinaryWriter w,bool compact=true)
    {
        if(Failure!=null||LastMinute!=World.CurrentGameMinute)throw new InvalidDataException("Incomplete bee tick.");
        w.Write(16);w.Write(compact);w.Write(LastMinute);w.Write(RandomState);w.Write(nextId);
        CycleBinary.Write(w,typeof(BeeCycleSettings),Settings);
        if(compact)WriteCompactBees(w);else CycleBinary.Write(w,Bees.GetType(),Bees);
        CycleBinary.Write(w,typeof(CycleCounters),Counters);CycleBinary.Write(w,Commands.GetType(),Commands);WriteMovementExtension(w).GetAwaiter().GetResult();WriteDemandState(w);
    }
    internal async System.Threading.Tasks.Task WriteAsync(BinaryWriter w,Func<System.Threading.Tasks.Task> checkpoint)
    {
        if(Failure!=null||LastMinute!=World.CurrentGameMinute)throw new InvalidDataException("Incomplete bee tick.");
        w.Write(16);w.Write(true);w.Write(LastMinute);w.Write(RandomState);w.Write(nextId);CycleBinary.Write(w,typeof(BeeCycleSettings),Settings);
        await WriteCompactBeesAsync(w,checkpoint);CycleBinary.Write(w,typeof(CycleCounters),Counters);CycleBinary.Write(w,Commands.GetType(),Commands);await WriteMovementExtension(w,checkpoint);WriteDemandState(w);
    }
    internal void Read(BinaryReader r,bool validateOnly=false)
    {
        int version=r.ReadInt32();if(version!=1&&version!=2&&version!=3&&version!=4&&version!=5&&version!=6&&version!=7&&version!=8&&version!=9&&version!=10&&version!=11&&version!=12&&version!=13&&version!=14&&version!=15&&version!=16)throw new InvalidDataException("Unknown bee cycle version.");
        bool compact=version>=6?r.ReadBoolean():version==5;
        LastMinute=r.ReadInt64();RandomState=r.ReadUInt32();nextId=r.ReadInt64();
        Settings=(BeeCycleSettings)CycleBinary.Read(r,typeof(BeeCycleSettings),version);if(version<8)Settings.representativeScale=1;if(version<9){Settings.eggBasalHeatJ=Settings.larvaBasalHeatJ=Settings.pupaBasalHeatJ=Settings.basalHeatJ;Settings.broodThermalCoupling=0;Settings.UpgradeBroodHeatFormula(true);}if(version==1)Settings.UpgradeForageFormula();if(version<4)Settings.demoEggs=Settings.demoLarvae=Settings.demoPupae=0;if(version<10)Settings.UpgradeRelocationFormulas();if(version<12)Settings.UpgradeLoadForageFormulas();rules=new BeeCycleRules(Settings);unitInputs.Clear();scalarInputs.Clear();developmentInputs.Clear();
        if(compact)ReadCompactBees(r,validateOnly,version);else Bees=(System.Collections.Generic.List<CycleBee>)CycleBinary.Read(r,typeof(System.Collections.Generic.List<CycleBee>),version);
        Counters=(CycleCounters)CycleBinary.Read(r,typeof(CycleCounters),version);Commands=(System.Collections.Generic.List<CycleCommand>)CycleBinary.Read(r,typeof(System.Collections.Generic.List<CycleCommand>),version);byId.Clear();
        if(LastMinute!=World.CurrentGameMinute||RandomState==0||nextId<=0||Counters.actions.Length!=15)throw new InvalidDataException("Invalid bee clock.");
        foreach(var b in Bees)
        {
            if(b.forageSource < -1 || b.forageSource >= (World.Weather?.Nectar?.Sources.Length??0))throw new InvalidDataException("Invalid forage source.");
            // Older deaths cleared the route but retained its cursor. Only dead empty routes are repairable.
            if(b.stage==CycleStage.Dead&&b.path.Count==0&&b.pathIndex>0){b.pathIndex=0;b.progress=0;}
            if(b.id<=0||byId.ContainsKey(b.id)||b.reserve<0||b.energy<0||b.health<0||b.development<0||b.cargo.pollen<0||b.pathIndex<0||b.pathIndex>b.path.Count||b.progress<0)throw new InvalidDataException("Invalid bee state.");
            if(b.stage!=CycleStage.Dead&&!b.outside&&!World.BeeLocations.TryGet(b.id,out _))throw new InvalidDataException("Missing bee location.");
            foreach(var s in b.cargo.sugars)if(s.mass<0||s.source<0)throw new InvalidDataException("Invalid cargo.");
            b.cargo.NormalizeWildflowerSources();
            byId.Add(b.id,b);nextId=Math.Max(nextId,b.id+1);
            if(b.mutableDesires!=null){b.desireState=InternDesires(CycleDesireState.From(b.mutableDesires));b.mutableDesires=null;b.desireIndex=null;}
        }
        if(version>=14)ReadMovementExtension(r,validateOnly);if(version>=15)ReadDemandState(r);
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
    sealed class FieldPlan
    {
        public FieldInfo Field; public int Since;
        public string Name=>Field?.Name??"desires";
        public Type Type=>Field?.FieldType??typeof(System.Collections.Generic.List<CycleDesire>);
        public object Get(object value)=>Field!=null?Field.GetValue(value):((CycleBee)value).desires;
        public void Set(object target,object value){if(Field!=null)Field.SetValue(target,value);else ((CycleBee)target).desires=(System.Collections.Generic.List<CycleDesire>)value;}
    }
    static readonly System.Collections.Concurrent.ConcurrentDictionary<Type,FieldPlan[]> fieldPlans
        =new System.Collections.Concurrent.ConcurrentDictionary<Type,FieldPlan[]>();
    static FieldPlan[] Fields(Type t)=>fieldPlans.GetOrAdd(t,type=>
    {
        var fields=type.GetFields(BindingFlags.Instance|BindingFlags.Public);
        var result=new FieldPlan[fields.Length];
        for(int i=0;i<fields.Length;i++)result[i]=new FieldPlan { Field=fields[i], Since=fields[i].GetCustomAttribute<CycleSinceAttribute>()?.Version??1 };
        if(type==typeof(CycleBee)){
            var list=new System.Collections.Generic.List<FieldPlan>(result);
            int index=list.FindIndex(f=>f.Name=="action")+1;
            list.Insert(index,new FieldPlan{Since=1});return list.ToArray();
        }
        return result;
    });
    // Keep the original reflected wire order. Fall back if these public layouts change.
    static bool Layout(Type type,params string[] names)
    {
        var fields=Fields(type);if(fields.Length!=names.Length)return false;
        for(int i=0;i<fields.Length;i++)if(fields[i].Name!=names[i]||fields[i].Since!=1)return false;
        return true;
    }
    static readonly bool fastPoint=Layout(typeof(CyclePoint),"equipment","x","y","z","face");
    static readonly bool fastDesire=fastPoint&&Layout(typeof(CycleDesire),"task","target","weight");
    static readonly System.Collections.Generic.HashSet<int> tasks=TaskValues();
    static System.Collections.Generic.HashSet<int> TaskValues()
    {
        var values=new System.Collections.Generic.HashSet<int>();
        foreach(CycleTask value in Enum.GetValues(typeof(CycleTask)))values.Add((int)value);
        return values;
    }
    static void Point(BinaryWriter w,CyclePoint p){w.Write(p.equipment);w.Write(p.x);w.Write(p.y);w.Write(p.z);w.Write(p.face);}
    static CyclePoint Point(BinaryReader r)=>new CyclePoint{equipment=r.ReadInt32(),x=r.ReadInt32(),y=r.ReadInt32(),z=r.ReadInt32(),face=r.ReadInt32()};
    static double Finite(double n){if(double.IsNaN(n)||double.IsInfinity(n))throw new InvalidDataException("Nonfinite value.");return n;}
    internal static void WriteDesires(BinaryWriter w,CycleBee bee){w.Write(bee.DesireCount);for(int i=0;i<bee.DesireCount;i++){var d=bee.DesireAt(i);w.Write((int)d.task);Point(w,d.target);w.Write(Finite(d.weight));}}
    // Version-5 bee records have an explicit stable layout. Bump the wire version when changing it.
    // Enum tables are typed once, avoiding per-bee boxing/reflection during validation and load.
    static class EnumValues<T> where T:struct
    {
        internal static readonly System.Collections.Generic.Dictionary<int,T> Values=Create();
        static System.Collections.Generic.Dictionary<int,T> Create(){var result=new System.Collections.Generic.Dictionary<int,T>();foreach(T value in Enum.GetValues(typeof(T)))result[Convert.ToInt32(value)]=value;return result;}
    }
    static T ReadEnum<T>(BinaryReader r) where T:struct {if(!EnumValues<T>.Values.TryGetValue(r.ReadInt32(),out var value))throw new InvalidDataException("Invalid enum.");return value;}
    internal static void WriteCompactBee(BinaryWriter w,CycleBee bee,int state)
    {
        w.Write(bee.id);
        w.Write(bee.queen);
        w.Write(bee.homeBox);
        w.Write(bee.broodColumn);
        w.Write(bee.broodRow);
        w.Write((int)bee.stage);
        w.Write(Finite(bee.development));
        w.Write(Finite(bee.broodSugar));
        w.Write(Finite(bee.broodPollen));
        w.Write(Finite(bee.reserve));
        w.Write(Finite(bee.energy));
        w.Write(Finite(bee.health));
        w.Write(bee.cargo.sugars.Count);foreach(var sugar in bee.cargo.sugars){w.Write(sugar.source);w.Write(Finite(sugar.mass));}w.Write(Finite(bee.cargo.pollen));
        w.Write((int)bee.task);
        w.Write((int)bee.action);
        w.Write(state);
        Point(w,bee.target);
        Point(w,bee.waypoint);
        Point(w,bee.returnPoint);
        w.Write(bee.hasTarget);
        w.Write(bee.hasWaypoint);
        w.Write(bee.outside);
        w.Write(bee.returning);
        w.Write(bee.interruptAfterReturn);
        w.Write(bee.hasStoreExclusion);
        Point(w,bee.storeExclusion);
        w.Write(Finite(bee.elapsed));
        w.Write(Finite(bee.outsideRemaining));
        w.Write(Finite(bee.progress));
        w.Write(bee.path.Count);foreach(var point in bee.path)Point(w,point);
        w.Write(bee.pathIndex);
        w.Write(bee.topologyVersion);
        w.Write(Finite(bee.lastAcquired));
        w.Write(Finite(bee.tickActivity));
        w.Write(bee.forageSource);
    }
    internal static CycleBee ReadCompactBee(BinaryReader r,CycleDesireState[] states)
    {
        var bee=new CycleBee();
        bee.id=r.ReadInt64();
        bee.queen=r.ReadBoolean();
        bee.homeBox=r.ReadInt32();
        bee.broodColumn=r.ReadInt32();
        bee.broodRow=r.ReadInt32();
        bee.stage=ReadEnum<CycleStage>(r);
        bee.development=Finite(r.ReadDouble());
        bee.broodSugar=Finite(r.ReadDouble());
        bee.broodPollen=Finite(r.ReadDouble());
        bee.reserve=Finite(r.ReadDouble());
        bee.energy=Finite(r.ReadDouble());
        bee.health=Finite(r.ReadDouble());
        int sugarCount=SaveBinary.Count(r,1000000);bee.cargo.sugars.Capacity=sugarCount;for(int i=0;i<sugarCount;i++)bee.cargo.sugars.Add(new CycleSugar{source=r.ReadInt32(),mass=Finite(r.ReadDouble())});bee.cargo.pollen=Finite(r.ReadDouble());
        bee.task=ReadEnum<CycleTask>(r);
        bee.action=ReadEnum<CycleAction>(r);
        int state=r.ReadInt32();if(state<0||state>=states.Length)throw new InvalidDataException("Invalid desire reference.");bee.desireState=states[state];
        bee.target=Point(r);
        bee.waypoint=Point(r);
        bee.returnPoint=Point(r);
        bee.hasTarget=r.ReadBoolean();
        bee.hasWaypoint=r.ReadBoolean();
        bee.outside=r.ReadBoolean();
        bee.returning=r.ReadBoolean();
        bee.interruptAfterReturn=r.ReadBoolean();
        bee.hasStoreExclusion=r.ReadBoolean();
        bee.storeExclusion=Point(r);
        bee.elapsed=Finite(r.ReadDouble());
        bee.outsideRemaining=Finite(r.ReadDouble());
        bee.progress=Finite(r.ReadDouble());
        int pathCount=SaveBinary.Count(r,1000000);bee.path.Capacity=pathCount;for(int i=0;i<pathCount;i++)bee.path.Add(Point(r));
        bee.pathIndex=r.ReadInt32();
        bee.topologyVersion=r.ReadInt32();
        bee.lastAcquired=Finite(r.ReadDouble());
        bee.tickActivity=Finite(r.ReadDouble());
        bee.forageSource=r.ReadInt32();
        if(bee.stage==CycleStage.Dead&&bee.path.Count==0&&bee.pathIndex>0){bee.pathIndex=0;bee.progress=0;}
        return bee;
    }
    // Keep field order and rejection rules aligned with ReadCompactBee and ReadCompactBees.
    // Validation consumes records without constructing per-bee cargo or path objects.
    internal static long ValidateCompactBee(BinaryReader r,int stateCount,int sources,System.Collections.Generic.HashSet<long> seen,Func<long,bool> hasLocation,System.Collections.Generic.Dictionary<long,byte> movementFlags=null)
    {
        long id=r.ReadInt64();
        r.ReadBoolean();
        r.ReadInt32();
        r.ReadInt32();
        r.ReadInt32();
        CycleStage stage=ReadEnum<CycleStage>(r);
        double development=Finite(r.ReadDouble());
        Finite(r.ReadDouble());
        Finite(r.ReadDouble());
        double reserve=Finite(r.ReadDouble());
        double energy=Finite(r.ReadDouble());
        double health=Finite(r.ReadDouble());
        int sugarCount=SaveBinary.Count(r,1000000);bool cargoInvalid=false;for(int i=0;i<sugarCount;i++){int source=r.ReadInt32();double mass=Finite(r.ReadDouble());cargoInvalid |= source<0||mass<0;}double pollen=Finite(r.ReadDouble());
        ReadEnum<CycleTask>(r);
        ReadEnum<CycleAction>(r);
        int state=r.ReadInt32();if(state<0||state>=stateCount)throw new InvalidDataException("Invalid desire reference.");
        Point(r);
        Point(r);
        Point(r);
        r.ReadBoolean();
        r.ReadBoolean();
        bool outside=r.ReadBoolean();
        r.ReadBoolean();
        r.ReadBoolean();
        r.ReadBoolean();
        Point(r);
        Finite(r.ReadDouble());
        Finite(r.ReadDouble());
        double progress=Finite(r.ReadDouble());
        int pathCount=SaveBinary.Count(r,1000000);for(int i=0;i<pathCount;i++)Point(r);
        int pathIndex=r.ReadInt32();
        r.ReadInt32();
        Finite(r.ReadDouble());
        Finite(r.ReadDouble());
        int forageSource=r.ReadInt32();
        if(forageSource < -1 || forageSource >= sources)throw new InvalidDataException("Invalid forage source.");
        // Match the repair in Read; this lightweight validation path creates no bee objects.
        if(stage==CycleStage.Dead&&pathCount==0&&pathIndex>0){pathIndex=0;progress=0;}
        if(id<=0||!seen.Add(id)||reserve<0||energy<0||health<0||development<0||pollen<0||pathIndex<0||pathIndex>pathCount||progress<0)throw new InvalidDataException("Invalid bee state.");
        if(stage!=CycleStage.Dead&&!outside&&!hasLocation(id))throw new InvalidDataException("Missing bee location.");
        if(cargoInvalid)throw new InvalidDataException("Invalid cargo.");
        if(movementFlags!=null&&(outside||stage==CycleStage.Dead))movementFlags[id]=(byte)((stage==CycleStage.Dead?1:0)|(outside?2:0));
        return id;
    }
    public static void Write(BinaryWriter w,Type t,object v)
    {
        if(t==typeof(CycleDesire)&&fastDesire){var d=(CycleDesire)v;w.Write((int)d.task);Point(w,d.target);w.Write(Finite(d.weight));return;}
        if(t==typeof(CyclePoint)&&fastPoint){Point(w,(CyclePoint)v);return;}
        if(t==typeof(double)){double n=(double)v;if(double.IsNaN(n)||double.IsInfinity(n))throw new InvalidDataException("Nonfinite value.");w.Write(n);}
        else if(t==typeof(int))w.Write((int)v);else if(t==typeof(long))w.Write((long)v);else if(t==typeof(bool))w.Write((bool)v);else if(t==typeof(string))SaveBinary.Text(w,(string)v);else if(t.IsEnum)w.Write(Convert.ToInt32(v));
        else if(t.IsArray||typeof(IList).IsAssignableFrom(t)){var a=(IList)v;w.Write(a.Count);var item=t.IsArray?t.GetElementType():t.GetGenericArguments()[0];foreach(var x in a)Write(w,item,x);}
        else foreach(var f in Fields(t)){
            if(t==typeof(CycleBee)&&f.Name=="desires")WriteDesires(w,(CycleBee)v);
            else Write(w,f.Type,f.Get(v));
        }
    }
    public static object Read(BinaryReader r,Type t,int version=4)
    {
        if(t==typeof(CycleDesire)&&fastDesire){int task=r.ReadInt32();if(!tasks.Contains(task))throw new InvalidDataException("Invalid enum.");return new CycleDesire{task=(CycleTask)task,target=Point(r),weight=Finite(r.ReadDouble())};}
        if(t==typeof(CyclePoint)&&fastPoint)return Point(r);
        if(t==typeof(double)){double n=r.ReadDouble();if(double.IsNaN(n)||double.IsInfinity(n))throw new InvalidDataException("Nonfinite value.");return n;}
        if(t==typeof(int))return r.ReadInt32();if(t==typeof(long))return r.ReadInt64();if(t==typeof(bool))return r.ReadBoolean();if(t==typeof(string))return SaveBinary.Text(r);
        if(t.IsEnum){int n=r.ReadInt32();if(!Enum.IsDefined(t,n))throw new InvalidDataException("Invalid enum.");return Enum.ToObject(t,n);}
        if(t.IsArray||typeof(IList).IsAssignableFrom(t)){int n=SaveBinary.Count(r,1000000);var item=t.IsArray?t.GetElementType():t.GetGenericArguments()[0];var a=(IList)(t.IsArray?Array.CreateInstance(item,n):Activator.CreateInstance(t));for(int i=0;i<n;i++){var v=Read(r,item,version);if(t.IsArray)a[i]=v;else a.Add(v);}return a;}
        var result=Activator.CreateInstance(t);foreach(var f in Fields(t))if(f.Since<=version)f.Set(result,Read(r,f.Type,version));return result;
    }
}

