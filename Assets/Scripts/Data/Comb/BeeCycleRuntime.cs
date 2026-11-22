using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

public sealed partial class BeeCycleRuntime : IGameMinuteParticipant
{
    public readonly GameWorld World;
    public BeeCycleSettings Settings { get; private set; }=new BeeCycleSettings();
    public List<CycleBee> Bees { get; private set; }=new List<CycleBee>();
    public CycleCounters Counters { get; private set; }=new CycleCounters();
    public long LastMinute { get; private set; }
    public Exception Failure { get; private set; }
    public double LastMilliseconds { get; private set; }
    public uint RandomState=1789231;
    BeeCycleRules rules;
    int topologyVersion=-1;
    readonly List<Region> regions=new List<Region>();
    readonly Dictionary<BeeGridPosition,Region> regionAt=new Dictionary<BeeGridPosition,Region>();
    readonly Dictionary<int,ItemData> items=new Dictionary<int,ItemData>();
    readonly Dictionary<int,BeeGridPosition> entrances=new Dictionary<int,BeeGridPosition>();
    readonly Dictionary<int,Group> groups=new Dictionary<int,Group>();
    readonly Dictionary<long,CycleBee> byId=new Dictionary<long,CycleBee>();
    readonly List<Request> requests=new List<Request>();
    readonly Dictionary<BeeGridPosition,List<CycleBee>> occupants=new Dictionary<BeeGridPosition,List<CycleBee>>();
    readonly Dictionary<(EquipmentThermalState,int),double> heat=new Dictionary<(EquipmentThermalState,int),double>();
    long nextId=1;
    readonly Dictionary<(BeeGridPosition,bool),Dictionary<BeeGridPosition,BeeGridPosition>> routeTrees=new Dictionary<(BeeGridPosition,bool),Dictionary<BeeGridPosition,BeeGridPosition>>();
    public sealed class Region
    {
        public BeeGridPosition position;public FrameSurface surface;public ItemData frame;public int x,z,group,box;
        public int revision=-1,empty,unfinished;public double sugar,pollen,freeSugar,freePollen,buildRemaining,height,temperature;
        public double nurseSugar,nursePollen;public readonly List<CycleBee> brood=new List<CycleBee>();
        public readonly List<CycleBee> workers=new List<CycleBee>();
        public bool clearing;
    }
    sealed class Group { public int count,brood;public double sugar,pollen;public int empty; }
    struct Request { public CycleBee bee;public Region region;public double amount;public int kind; }
    public IReadOnlyList<Region> Regions=>regions;
    public BeeCycleRuntime(GameWorld world){World=world;LastMinute=world.CurrentGameMinute;rules=new BeeCycleRules(Settings);}
    public void ApplySettings(BeeCycleSettings settings)
    {
        var next=new BeeCycleRules(settings);Settings=settings;rules=next;
    }
    double F(string name,CycleInputs input=default)=>rules.Eval(name,input);
    public double Random()
    {uint x=RandomState;x^=x<<13;x^=x>>17;x^=x<<5;RandomState=x==0?1:x;return RandomState/4294967296.0;}
    public static double Clamp(double x)=>Math.Max(0,Math.Min(1,x));
    public CycleBee Add(BeeGridPosition p,int home,bool queen=false)
    {
        while(World.BeeLocations.TryGet(nextId,out _)||byId.ContainsKey(nextId))nextId++;
        var b=new CycleBee{id=nextId++,homeBox=home,queen=queen,reserve=Settings.reserveTarget,energy=Settings.maxEnergy,health=Settings.maxHealth,development=Settings.adultStart};
        World.BeeLocations.Set(World,b.id,p);Bees.Add(b);byId.Add(b.id,b);return b;
    }
    public void SeedDemo()
    {
        if(Bees.Count>0)return;
        var defaults=UnityEngine.Resources.Load<BeeCycleSettingsAsset>("BeeCycleParameters");
        if(defaults!=null)ApplySettings(UnityEngine.JsonUtility.FromJson<BeeCycleSettings>(UnityEngine.JsonUtility.ToJson(defaults.values)));
        var box=World.Apiary.GetItemSlot(0,0).Item;if(box?.InteriorSlots==null)return;
        int highest=ThermalWorld.Items(World).Max(i=>i.Id);
        for(int k=0;k<Math.Min(Settings.demoFrames,box.InteriorSlots.Capacity-2);k++)
        {
            var slot=box.InteriorSlots.GetSlot(2+k);if(!slot.IsEmpty)continue;
            var frame=new FrameData();frame.Front.SetAllConstruction((byte)Settings.demoConstruction);frame.Back.SetAllConstruction((byte)Settings.demoConstruction);
            for(int y=0;y<4;y++)for(int x=0;x<5;x++){frame.Front.TrySetConstruction(x,y,100);frame.Back.TrySetConstruction(x,y,100);}
            if(k==0){frame.Front.TryAddNectar(0,0,Settings.forageSourceId,(float)Settings.demoSugar,(float)Settings.demoWater);frame.Front.TryAddPollen(1,0,(float)Settings.demoPollen);}
            slot.TryInsert(new ItemData(++highest,"벌 시험 소비장",ItemKind.Frame,frame));
        }
        RefreshRegions();if(regions.Count==0)return;
        var r=regions.Find(v=>v.box==box.Id);if(r==null)return;
        for(int i=0;i<Settings.demoWorkers;i++){var b=Add(r.position,box.Id);b.reserve*=0.4+0.6*Random();b.energy*=0.5+0.5*Random();}
        Add(r.position,box.Id,true);
    }
    void RefreshRegions()
    {
        var t=World.BeeLocations.Topology(World);
        if(topologyVersion!=t.Builds)
        {
            regions.Clear();regionAt.Clear();items.Clear();entrances.Clear();groups.Clear();routeTrees.Clear();
            foreach(var item in ThermalWorld.Items(World))items[item.Id]=item;
            foreach(var p in t.Positions)
            {
                if(!t.TryAir(p,out var air)||air?.Grid==null)continue;
                if(!groups.ContainsKey(air.Group))groups.Add(air.Group,new Group());
                var layout=air.Grid.Layout;layout.Coordinates(air.Index,out int ax,out int ay,out int az);
                if(layout.IsEntranceCell(ax,ay,az)&&!entrances.ContainsKey(air.Group))entrances.Add(air.Group,p);
                if(!items.TryGetValue(p.EquipmentId,out var item)||item.Kind!=ItemKind.Frame)continue;
                var s=p.Support;if((p.Face!=BeeFace.MinusY&&p.Face!=BeeFace.PlusY)||s.x<0||s.x>=5||s.z<0||s.z>=3)continue;
                var frame=item.GetContent<FrameData>();if(frame==null)continue;
                var r=new Region{position=p,frame=item,surface=p.Face==BeeFace.MinusY?frame.Front:frame.Back,x=s.x,z=s.z,group=air.Group,box=air.Grid.Box.Id,height=air.Z};
                regions.Add(r);regionAt.Add(p,r);
            }
            regions.Sort((a,b)=>{int n=a.frame.Id.CompareTo(b.frame.Id);if(n!=0)return n;n=a.position.Face.CompareTo(b.position.Face);return n!=0?n:a.z!=b.z?a.z.CompareTo(b.z):a.x.CompareTo(b.x);});
            topologyVersion=t.Builds;
        }
        foreach(var g in groups.Values){g.count=0;g.brood=0;g.sugar=g.pollen=0;g.empty=0;}
        foreach(var r in regions)
        {
            Scan(r);r.brood.Clear();r.workers.Clear();r.nurseSugar=r.nursePollen=0;r.clearing=false;
            var g=groups[r.group];g.sugar+=r.sugar;g.pollen+=r.pollen;g.empty+=r.empty;
            var air=Air(r.position);r.temperature=air==null?34:Owner(air).TemperatureC(OwnerIndex(air));
        }
        foreach(var b in Bees)
        {
            if(b.stage==CycleStage.Dead)continue;
            if(!Position(b,out var p))continue;
            var air=Air(p);if(air==null)continue;
            if(b.stage==CycleStage.Adult){groups[air.Group].count++;if(!b.outside&&regionAt.TryGetValue(p,out var r))r.workers.Add(b);}
            else if(regionAt.TryGetValue(p,out var r))
            {
                r.brood.Add(b);groups[r.group].brood++;
                if(b.stage==CycleStage.Larva){r.nurseSugar+=Math.Max(0,Settings.larvaSugarPerTick*Settings.broodReserveTicks-b.broodSugar);r.nursePollen+=Math.Max(0,Settings.larvaPollenPerTick*Settings.broodReserveTicks-b.broodPollen);}
            }
        }
    }
    void Scan(Region r)
    {
        if(r.revision==r.surface.Revision)return;
        r.empty=r.unfinished=0;r.sugar=r.pollen=r.freeSugar=r.freePollen=r.buildRemaining=0;
        Cells(r,(x,y)=>{
            var c=r.surface.GetCell(x,y);
            if(!c.IsComplete){r.unfinished++;r.buildRemaining+=100-c.Construction;return;}
            if(c.Content==CellContentKind.Empty)r.empty++;
            if(c.IsCapped)return;
            r.sugar+=r.surface.GetTotalSugarMassMg(x,y);r.pollen+=r.surface.GetPollenMassMg(x,y);
            r.freeSugar+=Free(r.surface,x,y,false);r.freePollen+=Free(r.surface,x,y,true);
        });r.revision=r.surface.Revision;
    }
    static void Cells(Region r,Action<int,int> action)
    {for(int y=r.z*4;y<(r.z+1)*4;y++)for(int x=r.x*5;x<(r.x+1)*5;x++)action(x,y);}
    static double Free(FrameSurface f,int x,int y,bool pollen)
    {
        var c=f.GetCell(x,y);var kind=pollen?CellContentKind.Pollen:CellContentKind.Nectar;
        if(!c.IsComplete||c.IsCapped||(c.Content!=CellContentKind.Empty&&c.Content!=kind))return 0;
        if(c.Content!=CellContentKind.Empty&&!f.HasQuantifiedContent(x,y))return 0;
        return pollen?Math.Max(0,CombBalance.PollenCapacityMg-f.GetPollenMassMg(x,y)):
            Math.Max(0,(CombBalance.LiquidCapacityMl-CombBalance.NectarVolumeMl(f.GetTotalSugarMassMg(x,y),f.GetWaterMassMg(x,y)))/CombBalance.NectarVolumeMl(1,0));
    }
    BeeGridTopology.Cell Air(BeeGridPosition p)=>World.BeeLocations.Topology(World).TryAir(p,out var c)?c:null;
    static EquipmentThermalState Owner(BeeGridTopology.Cell air)=>air.Source?.Equipment.ThermalState??air.Grid.Box.ThermalState;
    static int OwnerIndex(BeeGridTopology.Cell air)=>air.Source?.Index??air.Index;
    bool Position(CycleBee b,out BeeGridPosition p)
    {if(b.outside){p=b.returnPoint.Grid;return true;}return World.BeeLocations.TryGet(b.id,out p);}
    public bool BelongsTo(CycleBee b,ISet<int> equipment)
    {return b.outside?equipment.Contains(b.homeBox):World.BeeLocations.TryGet(b.id,out var p)&&equipment.Contains(p.EquipmentId);}
    public Task CalculateMinuteAsync(long minute,CancellationToken cancellation)
    {
        if(Failure!=null)throw new InvalidOperationException("Bee cycle requires restore.",Failure);
        var watch=System.Diagnostics.Stopwatch.StartNew();
        try
        {
            if(minute!=LastMinute+1)throw new InvalidOperationException("Bee minute mismatch.");
            cancellation.ThrowIfCancellationRequested();
            if(Bees.Count==0){LastMinute=minute;return Task.CompletedTask;}
            World.Thermal.Refresh();RefreshRegions();RefreshCommands();requests.Clear();heat.Clear();
            int count=Bees.Count;
            for(int i=0;i<count;i++)
            {
                var b=Bees[i];if(b.stage==CycleStage.Dead)continue;b.action=CycleAction.Idle;b.tickActivity=0;b.lastAcquired=0;
                if(b.health<=0||b.development>=Settings.maxDevelopment){Die(b);continue;}
                if(b.stage!=CycleStage.Adult){DevelopBrood(b);BaseHeat(b);continue;}
                double basal=Math.Min(b.reserve,F("basalSugarCost",new CycleInputs{time=1}));b.reserve-=basal;Counters.consumedSugar+=basal;
                b.energy=Math.Max(0,b.energy-F("basalEnergyCost",new CycleInputs{time=1}));
                if(b.reserve<=0&&b.energy<=0)b.health=Math.Max(0,b.health-F("starvation",new CycleInputs{time=1}));
                UpdateDesires(b);Run(b);BaseHeat(b);
                if(b.health<=0||b.development>=Settings.maxDevelopment)Die(b);
            }
            ResolveRequests();
            foreach(var h in heat)h.Key.Item1.RestoreEnergy(h.Key.Item2,(float)(h.Key.Item1.EnergyJ[h.Key.Item2]+h.Value));
            LastMinute=minute;
        }
        catch(Exception e){Failure=e;throw;}
        finally{LastMilliseconds=watch.Elapsed.TotalMilliseconds;}
        return Task.CompletedTask;
    }
    public List<CycleCommand> Commands { get; private set; }=new List<CycleCommand>();
    readonly HashSet<(CycleTask,BeeGridPosition)> activeCommands=new HashSet<(CycleTask,BeeGridPosition)>();
    void Command(CycleTask task,BeeGridPosition target,int group,double need,bool localOnly=false)
    {
        if(need<=0)return;
        activeCommands.Add((task,target));
        var command=Commands.Find(c=>c.task==task&&c.target.Grid.Equals(target));
        if(command==null)
        {
            if(Random()>=F("commandChance",new CycleInputs{need=need}))return;
            command=new CycleCommand{task=task,target=new CyclePoint(target)};Commands.Add(command);Counters.commands++;
        }
        command.group=group;command.need=need;command.localOnly=localOnly;
    }
    void RefreshCommands()
    {
        activeCommands.Clear();
        foreach(var pair in groups)
        {
            var g=pair.Value;if(!entrances.TryGetValue(pair.Key,out var entry))continue;
            Command(CycleTask.ForageNectar,entry,pair.Key,F("deficit",new CycleInputs{amount=g.sugar,available=g.count*Settings.stockSugarPerBee}));
            Command(CycleTask.ForagePollen,entry,pair.Key,F("deficit",new CycleInputs{amount=g.pollen,available=g.count*Settings.stockPollenPerBee}));
        }
        foreach(var r in regions)
        {
            var g=groups[r.group];
            if(r.empty>0)Command(CycleTask.Lay,r.position,r.group,F("deficit",new CycleInputs{amount=g.brood,available=Settings.broodTarget})*F("broodTemperature",new CycleInputs{temperature=r.temperature}));
            if(r.unfinished>0)Command(CycleTask.Build,r.position,r.group,F("deficit",new CycleInputs{amount=g.empty,available=g.count*Settings.desiredEmptyCellsPerBee})*F("buildPreference",new CycleInputs{amount=r.unfinished}),r.buildRemaining<=r.workers.Count*Settings.buildPoints);
            if(r.nurseSugar+r.nursePollen>0)Command(CycleTask.Nurse,r.position,r.group,F("nurseNeed",new CycleInputs{amount=r.nurseSugar+r.nursePollen,available=r.brood.Count*(Settings.larvaSugarPerTick+Settings.larvaPollenPerTick)*Settings.broodReserveTicks}),r.nurseSugar+r.nursePollen<=r.workers.Count*Settings.nurseRate);
            if(r.brood.Count>0&&r.temperature<Settings.broodIdealTemperature)Command(CycleTask.Heat,r.position,r.group,F("heatNeed",new CycleInputs{amount=Settings.broodIdealTemperature-r.temperature}),r.workers.Count>=r.brood.Count);
            if(g.empty==0&&g.brood<Settings.broodTarget&&r.sugar+r.pollen>0){r.clearing=true;Command(CycleTask.Clear,r.position,r.group,1);}
        }
        Commands.RemoveAll(c=>!activeCommands.Contains((c.task,c.target.Grid)));
    }
    void UpdateDesires(CycleBee b)
    {
        if(b.desireIndex==null){b.desireIndex=new Dictionary<(CycleTask,BeeGridPosition),CycleDesire>();foreach(var d in b.desires)b.desireIndex[(d.task,d.target.Grid)]=d;}
        if(Position(b,out var p)&&Air(p) is BeeGridTopology.Cell air)
        foreach(var command in Commands)
        {
            if(command.group!=air.Group||(command.localOnly&&!command.target.Grid.Equals(p))||(b.queen!=(command.task==CycleTask.Lay)))continue;
            if(!b.desireIndex.TryGetValue((command.task,command.target.Grid),out var desire)){desire=new CycleDesire{task=command.task,target=command.target};b.desires.Add(desire);b.desireIndex.Add((command.task,command.target.Grid),desire);}
            desire.weight+=F("commandStimulus",new CycleInputs{need=command.need});
        }
        for(int i=b.desires.Count-1;i>=0;i--){var d=b.desires[i];d.weight=F("desireNext",new CycleInputs{amount=d.weight});if(d.weight==0){b.desireIndex.Remove((d.task,d.target.Grid));b.desires.RemoveAt(i);}}
    }
    void Choose(CycleBee b)
    {
        double feed=F("feedDesire",new CycleInputs{ratio=1-b.reserve/Settings.reserveTarget}),rest=F("restDesire",new CycleInputs{ratio=1-b.energy/Settings.maxEnergy}),recover=F("recoverDesire",new CycleInputs{ratio=1-b.health/Settings.maxHealth}),store=F("storeDesire",new CycleInputs{ratio=b.cargo.Mass/Settings.carryCapacity});
        double total=feed+rest+recover+store;foreach(var d in b.desires)total+=d.weight;
        if(total<=0)return;double choice=Random()*total;b.elapsed=0;
        if((choice-=feed)<0)b.task=CycleTask.Feed;
        else if((choice-=rest)<0)b.task=CycleTask.Rest;
        else if((choice-=recover)<0)b.task=CycleTask.Recover;
        else if((choice-=store)<0)b.task=CycleTask.Store;
        else foreach(var d in b.desires)if((choice-=d.weight)<0){b.task=d.task;b.target=d.target;b.hasTarget=true;break;}
    }
    void End(CycleBee b,CycleEnd end)
    {
        switch(end){case CycleEnd.Completed:Counters.completed++;break;case CycleEnd.ResourceMissing:Counters.resourceFailed++;break;case CycleEnd.PathBlocked:Counters.pathFailed++;break;case CycleEnd.Interrupted:Counters.interrupted++;break;}
        if(b.task==CycleTask.Store&&end==CycleEnd.Completed)b.hasStoreExclusion=false;
        b.task=CycleTask.None;b.hasTarget=b.hasWaypoint=false;b.path.Clear();b.pathIndex=0;b.progress=0;b.elapsed=0;
    }
    void Act(CycleBee b,CycleAction action,double time=1)
    {
        b.action=action;Counters.actions[(int)action]++;b.tickActivity+=time;
        double rate=action==CycleAction.Move?Settings.moveDevelopment:action==CycleAction.Outside?Settings.forageDevelopment:action==CycleAction.Build?Settings.buildDevelopment:action==CycleAction.Rest?Settings.restDevelopment:action==CycleAction.Recover?Settings.recoverDevelopment:action==CycleAction.Feed?Settings.feedDevelopment:action==CycleAction.Acquire?Settings.acquireDevelopment:action==CycleAction.Store?Settings.storeDevelopment:action==CycleAction.Nurse?Settings.nurseDevelopment:action==CycleAction.Heat?Settings.heatDevelopment:action==CycleAction.Lay?Settings.layDevelopment:action==CycleAction.Clear?Settings.clearDevelopment:Settings.idleDevelopment;
        b.development+=F("development",new CycleInputs{actionRate=rate,time=time});
    }
    void Run(CycleBee b)
    {
        if(b.task==CycleTask.None)Choose(b);
        if(b.task==CycleTask.None){Act(b,CycleAction.Idle);return;}
        b.elapsed++;
        if(Random()<F("interruptChance",new CycleInputs{elapsed=b.elapsed}))
        {if(b.outside){b.returning=true;b.interruptAfterReturn=true;}else{End(b,CycleEnd.Interrupted);Act(b,CycleAction.Idle);return;}}
        if(b.outside)
        {
            Act(b,CycleAction.Outside);b.outsideRemaining--;
            if(b.returning||b.outsideRemaining<=0)
            {
                if(!b.returning)
                {
                    double free=Math.Max(0,Settings.carryCapacity-b.cargo.Mass);
                    if(b.task==CycleTask.ForageNectar){double n=Math.Min(free,F("forageSugarAmount"));b.cargo.Add(Settings.forageSourceId,n);Counters.externalSugar+=n;Counters.discardedWater+=F("forageWaterAmount")*(n/Math.Max(1e-12,F("forageSugarAmount")));}
                    else{double n=Math.Min(free,F("foragePollenAmount"));b.cargo.pollen+=n;Counters.externalPollen+=n;}
                }
                World.BeeLocations.Set(World,b.id,b.returnPoint.Grid);b.outside=false;b.returning=false;
                End(b,b.interruptAfterReturn?CycleEnd.Interrupted:CycleEnd.Completed);b.interruptAfterReturn=false;
            }return;
        }
        if(!World.BeeLocations.TryGet(b.id,out var here)){End(b,CycleEnd.PathBlocked);return;}
        double time=1;
        switch(b.task)
        {
            case CycleTask.Feed:
                if(b.reserve>=Settings.reserveTarget-1e-9){End(b,CycleEnd.Completed);break;}
                if(b.cargo.Sugar<=0){Acquire(b,here,false);break;}
                double eat=b.cargo.Consume(F("feedAmount",new CycleInputs{amount=Settings.reserveTarget-b.reserve,time=time}));b.reserve+=eat;Act(b,CycleAction.Feed,time);break;
            case CycleTask.Rest: Rest(b,time);break;
            case CycleTask.Recover:
                if(b.health>=Settings.maxHealth-1e-9){End(b,CycleEnd.Completed);break;}
                if(b.energy<=0){Rest(b,time,false);break;}
                double heal=Math.Min(F("recoverAmount",new CycleInputs{time=time,amount=Settings.maxHealth-b.health}),b.energy/Settings.recoveryEnergy);b.energy-=heal*Settings.recoveryEnergy;b.health+=heal;Act(b,CycleAction.Recover,time);break;
            case CycleTask.Store:
                if(b.cargo.Mass<=1e-8){End(b,CycleEnd.Completed);break;}
                var dest=SelectRegion(b,here,r=>!r.clearing&&(!b.hasStoreExclusion||!r.position.Equals(b.storeExclusion.Grid))&&(b.cargo.Sugar>0?r.freeSugar:r.freePollen)>0,true);
                if(dest==null){End(b,CycleEnd.ResourceMissing);break;}
                if(!Move(b,dest.position,ref time))break;
                requests.Add(new Request{bee=b,region=dest,amount=F("storeAmount",new CycleInputs{time=time,amount=b.cargo.Mass}),kind=2});Act(b,CycleAction.Store,time);break;
            case CycleTask.ForageNectar:case CycleTask.ForagePollen:
                if(b.cargo.Mass>=Settings.carryCapacity-1e-8){End(b,CycleEnd.Completed);break;}
                if(Move(b,b.target.Grid,ref time)){b.returnPoint=new CyclePoint(b.target.Grid);b.outside=true;b.outsideRemaining=F("forageDuration");World.BeeLocations.Remove(b.id);Act(b,CycleAction.Outside,time);}break;
            default:
                if(!b.hasTarget||!regionAt.TryGetValue(b.target.Grid,out var r)){End(b,CycleEnd.PathBlocked);break;}
                if(b.task==CycleTask.Build&&b.cargo.Sugar<Settings.sugarPerBuildPoint){Acquire(b,here,false);break;}
                if(b.task==CycleTask.Nurse&&((r.nurseSugar>0&&b.cargo.Sugar<=0)||(r.nurseSugar<=0&&r.nursePollen>0&&b.cargo.pollen<=0)))
                {Acquire(b,here,r.nurseSugar<=0);break;}
                if(!Move(b,r.position,ref time))break;
                if(b.task==CycleTask.Build){if(r.unfinished==0){End(b,CycleEnd.Completed);break;}requests.Add(new Request{bee=b,region=r,kind=3,amount=Math.Min(F("buildAmount",new CycleInputs{time=time}),b.cargo.Sugar/Settings.sugarPerBuildPoint)});Act(b,CycleAction.Build,time);}
                else if(b.task==CycleTask.Lay){Lay(b,r,time);}
                else if(b.task==CycleTask.Nurse){if(r.nurseSugar+r.nursePollen<=1e-9){End(b,CycleEnd.Completed);break;}requests.Add(new Request{bee=b,region=r,kind=4,amount=F("nurseAmount",new CycleInputs{time=time})});Act(b,CycleAction.Nurse,time);}
                else if(b.task==CycleTask.Heat){if(r.temperature>=Settings.broodIdealTemperature||r.brood.Count==0){End(b,CycleEnd.Completed);break;}if(b.energy<=0){Rest(b,time,false);break;}double fraction=Math.Min(time,b.energy/Settings.heatEnergy);b.energy-=fraction*Settings.heatEnergy;AddHeat(b,F("additionalHeat",new CycleInputs{time=fraction}));Act(b,CycleAction.Heat,fraction);}
                else if(b.task==CycleTask.Clear){if(b.cargo.Mass>=Settings.carryCapacity-1e-8||r.sugar+r.pollen<=1e-9){End(b,CycleEnd.Completed);break;}requests.Add(new Request{bee=b,region=r,kind=r.sugar>0?0:1,amount=Math.Min(Settings.acquireRate*time,Settings.carryCapacity-b.cargo.Mass)});b.hasStoreExclusion=true;b.storeExclusion=new CyclePoint(r.position);Act(b,CycleAction.Clear,time);}
                break;
        }
        if(b.tickActivity<1)Act(b,CycleAction.Idle,1-b.tickActivity);
    }
    void Rest(CycleBee b,double time,bool complete=true)
    {
        if(b.energy>=Settings.maxEnergy-1e-9){if(complete)End(b,CycleEnd.Completed);return;}
        if(b.reserve<=0)
        {
            if(b.cargo.Sugar>0){double n=b.cargo.Consume(Math.Min(Settings.feedRate*time,Settings.reserveTarget-b.reserve));b.reserve+=n;Act(b,CycleAction.Feed,time);}
            else if(World.BeeLocations.TryGet(b.id,out var p))Acquire(b,p,false);
            return;
        }
        double gain=Math.Min(F("restAmount",new CycleInputs{time=time,amount=Settings.maxEnergy-b.energy}),b.reserve*Settings.energyPerSugar);
        b.reserve-=gain/Settings.energyPerSugar;Counters.consumedSugar+=gain/Settings.energyPerSugar;b.energy+=gain;Act(b,CycleAction.Rest,time);
    }
    void Acquire(CycleBee b,BeeGridPosition here,bool pollen)
    {
        if(b.cargo.Mass>=Settings.carryCapacity-1e-8){End(b,CycleEnd.ResourceMissing);return;}
        var r=SelectRegion(b,here,v=>(pollen?v.pollen:v.sugar)>0,false);
        if(r==null){End(b,CycleEnd.ResourceMissing);return;}
        double time=1;if(!Move(b,r.position,ref time))return;
        requests.Add(new Request{bee=b,region=r,kind=pollen?1:0,amount=F("acquireAmount",new CycleInputs{amount=Settings.carryCapacity-b.cargo.Mass,time=time})});Act(b,CycleAction.Acquire,time);
    }
    Region SelectRegion(CycleBee b,BeeGridPosition here,Predicate<Region> eligible,bool storage)
    {
        if(b.hasWaypoint&&regionAt.TryGetValue(b.waypoint.Grid,out var old)&&eligible(old))return old;
        var air=Air(here);if(air==null)return null;
        Region selected=null;double total=0,best=double.PositiveInfinity;
        foreach(var r in regions)
        {
            if(r.group!=air.Group||!eligible(r))continue;
            double dist=RouteDistance(here,r.position,b.queen);if(double.IsPositiveInfinity(dist))continue;
            if(!storage){if(dist<best){best=dist;selected=r;}continue;}
            double w=F("storagePreference",new CycleInputs{available=b.cargo.Sugar>0?r.freeSugar:r.freePollen,height=r.height/Settings.storageHeightScale,distance=dist});total+=w;if(Random()*total<w)selected=r;
        }
        if(selected!=null){b.waypoint=new CyclePoint(selected.position);b.hasWaypoint=true;}
        return selected;
    }
    List<CyclePoint> FindPath(BeeGridPosition start,BeeGridPosition target,bool queen)
    {
        if(start.Equals(target))return new List<CyclePoint>();
        if(routeTrees.TryGetValue((start,queen),out var cached))return Reconstruct(start,target,cached);
        var queue=new Queue<BeeGridPosition>();var previous=new Dictionary<BeeGridPosition,BeeGridPosition>();queue.Enqueue(start);previous[start]=start;
        while(queue.Count>0)
        {
            var p=queue.Dequeue();foreach(var m in World.BeeLocations.GetNeighbours(World,p))
            {
                if((queen&&!m.AllowsQueen)||previous.ContainsKey(m.Target))continue;previous[m.Target]=p;
                queue.Enqueue(m.Target);
            }
        }
        if(routeTrees.Count>=Settings.routeCacheEntries)routeTrees.Clear();routeTrees[(start,queen)]=previous;
        return Reconstruct(start,target,previous);
    }
    static List<CyclePoint> Reconstruct(BeeGridPosition start,BeeGridPosition target,Dictionary<BeeGridPosition,BeeGridPosition> previous)
    {if(!previous.ContainsKey(target))return null;var result=new List<CyclePoint>();for(var q=target;!q.Equals(start);q=previous[q])result.Add(new CyclePoint(q));result.Reverse();return result;}
    double RouteDistance(BeeGridPosition start,BeeGridPosition target,bool queen)
    {
        if(start.Equals(target))return 0;
        if(!routeTrees.TryGetValue((start,queen),out var previous)){FindPath(start,target,queen);if(!routeTrees.TryGetValue((start,queen),out previous))return double.PositiveInfinity;}
        if(!previous.ContainsKey(target))return double.PositiveInfinity;
        double n=0;for(var p=target;!p.Equals(start);p=previous[p])n+=Distance(previous[p],p);return n;
    }
    double Distance(BeeGridPosition a,BeeGridPosition b)
    {var x=World.BeeLocations.Topology(World).SupportCell(a);var y=World.BeeLocations.Topology(World).SupportCell(b);return Math.Max(5,Math.Abs(x.X-y.X)*(x.Dx+y.Dx)/2+Math.Abs(x.Y-y.Y)*(x.Dy+y.Dy)/2+Math.Abs(x.Z-y.Z)*(x.Dz+y.Dz)/2);}
    double PathLength(BeeGridPosition p,List<CyclePoint> path){double n=0;foreach(var q in path){n+=Distance(p,q.Grid);p=q.Grid;}return n;}
    bool Move(CycleBee b,BeeGridPosition target,ref double time)
    {
        if(!World.BeeLocations.TryGet(b.id,out var here)){End(b,CycleEnd.PathBlocked);return false;}
        if(here.Equals(target)){b.hasWaypoint=false;return true;}
        bool valid=b.pathIndex<b.path.Count&&b.path[b.path.Count-1].Grid.Equals(target);
        if(valid&&b.topologyVersion!=topologyVersion)
        {
            var previous=here;
            for(int i=b.pathIndex;i<b.path.Count;i++)
            {var next=b.path[i].Grid;bool connected=false;foreach(var m in World.BeeLocations.GetNeighbours(World,previous))if(m.Target.Equals(next)&&(!b.queen||m.AllowsQueen)){connected=true;break;}if(!connected){valid=false;break;}previous=next;}
            b.topologyVersion=topologyVersion;
        }
        if(!valid)
        {
            var path=FindPath(here,target,b.queen);if(path==null){End(b,CycleEnd.PathBlocked);return false;}
            b.path=path;b.pathIndex=0;b.progress=0;b.topologyVersion=topologyVersion;
        }
        double original=time,speed=F("moveDistance",new CycleInputs{time=1});
        if(speed<=0){End(b,CycleEnd.PathBlocked);return false;}
        while(time>1e-12&&b.pathIndex<b.path.Count)
        {
            var next=b.path[b.pathIndex].Grid;double distance=Distance(here,next),need=(distance-b.progress)/speed;
            if(need>time){b.progress+=time*speed;time=0;break;}
            if(!World.BeeLocations.TryMove(World,b.id,next,b.queen)){b.path.Clear();b.progress=0;End(b,CycleEnd.PathBlocked);return false;}
            time-=need;b.progress=0;b.pathIndex++;here=next;
        }
        double used=original-time;b.energy=Math.Max(0,b.energy-F("movementEnergyCost",new CycleInputs{time=used}));Act(b,CycleAction.Move,used);
        return here.Equals(target)&&time>1e-12;
    }
    void BaseHeat(CycleBee b){if(b.stage!=CycleStage.Dead)AddHeat(b,F("basalHeat",new CycleInputs{time=1}));}
    void AddHeat(CycleBee b,double joules)
    {
        if(b.outside||!World.BeeLocations.TryGet(b.id,out var p))return;var air=Air(p);if(air?.Grid==null)return;
        var key=(Owner(air),OwnerIndex(air));heat.TryGetValue(key,out var old);heat[key]=old+joules;Counters.heatJ+=joules;
    }
    void Die(CycleBee b)
    {
        if(b.stage==CycleStage.Dead)return;
        if(b.stage!=CycleStage.Adult&&World.BeeLocations.TryGet(b.id,out var p)&&items.TryGetValue(p.EquipmentId,out var item)&&item.GetContent<FrameData>() is FrameData frame)
        {var face=p.Face==BeeFace.MinusY?frame.Front:frame.Back;if(face.GetBeeId(b.broodColumn,b.broodRow)==b.id)face.TryClearContent(b.broodColumn,b.broodRow);}
        Counters.lostSugar+=b.cargo.Sugar+b.reserve+b.broodSugar;Counters.lostPollen+=b.cargo.pollen+b.broodPollen;
        b.cargo=new CycleCargo();b.reserve=b.broodSugar=b.broodPollen=0;b.stage=CycleStage.Dead;b.task=CycleTask.None;b.action=CycleAction.Dead;b.outside=b.returning=false;b.path.Clear();b.desires.Clear();b.desireIndex?.Clear();World.BeeLocations.Remove(b.id);Counters.deaths++;
    }
}
