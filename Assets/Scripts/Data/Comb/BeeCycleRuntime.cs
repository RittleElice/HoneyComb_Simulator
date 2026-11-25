using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

public sealed partial class BeeCycleRuntime : ISlicedGameMinuteParticipant
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
    readonly LinkedList<(BeeGridPosition,bool)> routeOrder=new LinkedList<(BeeGridPosition,bool)>();
    readonly Dictionary<(BeeGridPosition,bool),LinkedListNode<(BeeGridPosition,bool)>> routeNodes=new Dictionary<(BeeGridPosition,bool),LinkedListNode<(BeeGridPosition,bool)>>();
    bool TryRouteTree(BeeGridPosition start,bool queen,out Dictionary<BeeGridPosition,BeeGridPosition> tree)
    {
        var key=(start,queen);if(!routeTrees.TryGetValue(key,out tree))return false;
        var node=routeNodes[key];routeOrder.Remove(node);routeOrder.AddFirst(node);return true;
    }
    void CacheRouteTree(BeeGridPosition start,bool queen,Dictionary<BeeGridPosition,BeeGridPosition> tree)
    {
        // Evict only the least recently used tree; a new hive must not flush every other hive's routes.
        while(routeTrees.Count>=Settings.routeCacheEntries){var key=routeOrder.Last.Value;routeOrder.RemoveLast();routeNodes.Remove(key);routeTrees.Remove(key);}
        var added=(start,queen);routeTrees.Add(added,tree);routeNodes.Add(added,routeOrder.AddFirst(added));
    }
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
        var next=new BeeCycleRules(settings);Settings=settings;rules=next;unitInputs.Clear();scalarInputs.Clear();developmentInputs.Clear();
    }
    double F(string name,CycleInputs input=default)=>rules.Eval(name,input);
    readonly Dictionary<string,double> unitInputs=new Dictionary<string,double>();
    readonly Dictionary<(string,long),double> scalarInputs=new Dictionary<(string,long),double>();
    readonly Dictionary<(long,long),double> developmentInputs=new Dictionary<(long,long),double>();
    double FRatio(string name,double value){var key=(name,BitConverter.DoubleToInt64Bits(value));if(!scalarInputs.TryGetValue(key,out double result)){result=F(name,new CycleInputs{ratio=value});if(scalarInputs.Count>=8192)scalarInputs.Clear();scalarInputs.Add(key,result);}return result;}
    double FInterrupt(double elapsed){var key=("interruptChance",BitConverter.DoubleToInt64Bits(elapsed));if(!scalarInputs.TryGetValue(key,out double result)){result=F("interruptChance",new CycleInputs{elapsed=elapsed});if(scalarInputs.Count>=8192)scalarInputs.Clear();scalarInputs.Add(key,result);}return result;}
    double FDevelopment(double rate,double time){var key=(BitConverter.DoubleToInt64Bits(rate),BitConverter.DoubleToInt64Bits(time));if(!developmentInputs.TryGetValue(key,out double result)){result=F("development",new CycleInputs{actionRate=rate,time=time});if(developmentInputs.Count>=8192)developmentInputs.Clear();developmentInputs.Add(key,result);}return result;}
    double FUnit(string name){if(!unitInputs.TryGetValue(name,out double value)){value=F(name,new CycleInputs{time=1});unitInputs.Add(name,value);}return value;}
    public double Random()
    {uint x=RandomState;x^=x<<13;x^=x>>17;x^=x<<5;RandomState=x==0?1:x;return RandomState/4294967296.0;}
    public static double Clamp(double x)=>Math.Max(0,Math.Min(1,x));
    public CycleBee Add(BeeGridPosition p,int home,bool queen=false)
    {
        while(World.BeeLocations.TryGet(nextId,out _)||byId.ContainsKey(nextId))nextId++;
        var b=new CycleBee{id=nextId++,homeBox=home,queen=queen,reserve=Settings.reserveTarget,energy=Settings.maxEnergy,health=Settings.maxHealth,development=Settings.adultStart};
        World.BeeLocations.Set(World,b.id,p);Bees.Add(b);byId.Add(b.id,b);return b;
    }
    public void SeedDemo(BeeCycleSettings initialSettings=null)
    {
        if(Bees.Count>0)return;
        var defaults=UnityEngine.Resources.Load<BeeCycleSettingsAsset>("BeeCycleParameters");
        if(initialSettings!=null)ApplySettings(initialSettings);
        else if(defaults!=null){var settings=UnityEngine.JsonUtility.FromJson<BeeCycleSettings>(UnityEngine.JsonUtility.ToJson(defaults.values));settings.UpgradeForageFormula();ApplySettings(settings);}
        var box=World.Apiary.GetItemSlot(0,0).Item;if(box?.InteriorSlots==null)return;
        var initialLayout=LoadInitialCombLayout();
        int highest=ThermalWorld.Items(World).Max(i=>i.Id);
        var createdFrames=new HashSet<int>();
        for(int k=0;k<Math.Min(Settings.demoFrames,box.InteriorSlots.Capacity-2);k++)
        {
            var slot=box.InteriorSlots.GetSlot(2+k);if(!slot.IsEmpty)continue;
            var frame=new FrameData();frame.Front.SetAllConstruction((byte)Settings.demoConstruction);frame.Back.SetAllConstruction((byte)Settings.demoConstruction);
            for(int y=0;y<frame.Front.Height;y++)for(int x=0;x<frame.Front.Width;x++)if(initialLayout[frame.Front.Height-1-y][x]!='#'){frame.Front.TrySetConstruction(x,y,100);frame.Back.TrySetConstruction(x,y,100);}
            int frameId=++highest;
            if(!slot.TryInsert(new ItemData(frameId,"벌 시험 소비장",ItemKind.Frame,frame)))throw new InvalidOperationException("Initial frame insertion failed.");
            createdFrames.Add(frameId);
        }
        // New-game weather preparation has no thermal participant yet. Refresh after
        // inserting frames so region temperature reads also include the new equipment.
        World.PrepareThermal(ThermalMaterials.Load());
        RefreshRegions();if(regions.Count==0)return;
        var r=regions.Where(v=>v.box==box.Id&&createdFrames.Contains(v.frame.Id)).OrderBy(v=>Math.Abs(v.x-2)+Math.Abs(v.z-1)).FirstOrDefault()??regions.Find(v=>v.box==box.Id);if(r==null)return;
        for(int i=0;i<Settings.demoWorkers;i++){var b=Add(r.position,box.Id);b.reserve*=0.4+0.6*Random();b.energy*=0.5+0.5*Random();}
        Add(r.position,box.Id,true);
        SeedInitialContents(createdFrames,initialLayout);
        RefreshRegions();
    }
    void RefreshRegions()=>RefreshRegionsAsync(null).GetAwaiter().GetResult();
    async Task RefreshRegionsAsync(SimulationSlice slice)
    {
        var t=World.BeeLocations.Topology(World);
        if(topologyVersion!=t.Builds)
        {
            regions.Clear();regionAt.Clear();items.Clear();entrances.Clear();groups.Clear();routeTrees.Clear();routeNodes.Clear();routeOrder.Clear();ClearActionGeometry();
            desireKeys.Clear();indexedCommands.Clear();foreach(var bee in Bees)bee.desireIndex=null;
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
            groupRegions.Clear();foreach(var region in regions){if(!groupRegions.TryGetValue(region.group,out var members)){members=new List<Region>();groupRegions.Add(region.group,members);}members.Add(region);}
            topologyVersion=t.Builds;
        }
        foreach(var g in groups.Values){g.count=0;g.brood=0;g.sugar=g.pollen=0;g.empty=0;}
        foreach(var r in regions)
        {
            Scan(r);r.brood.Clear();r.workers.Clear();r.nurseSugar=r.nursePollen=0;r.clearing=false;
            var g=groups[r.group];g.sugar+=r.sugar;g.pollen+=r.pollen;g.empty+=r.empty;
            var air=Air(r.position);r.temperature=air==null?34:Owner(air).TemperatureC(OwnerIndex(air));
        }
        int visited=0;foreach(var b in Bees)
        {
            if(slice!=null&&(visited++&255)==0)await slice.CheckpointAsync();
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
    BeeGridTopology.Cell Air(BeeGridPosition p)
    {
        if(activePositionBee!=null&&airKnown&&p.Equals(cachedAirPosition))return cachedAir;
        var result=World.BeeLocations.Topology(World).TryAir(p,out var c)?c:null;
        if(activePositionBee!=null){cachedAirPosition=p;cachedAir=result;airKnown=true;}return result;
    }
    static EquipmentThermalState Owner(BeeGridTopology.Cell air)=>air.Source?.Equipment.ThermalState??air.Grid.Box.ThermalState;
    static int OwnerIndex(BeeGridTopology.Cell air)=>air.Source?.Index??air.Index;
    bool Position(CycleBee b,out BeeGridPosition p)
    {if(b.outside){p=b.returnPoint.Grid;return true;}return BeePosition(b,out p);}
    public bool BelongsTo(CycleBee b,ISet<int> equipment)
    {return b.outside?equipment.Contains(b.homeBox):BeePosition(b,out var p)&&equipment.Contains(p.EquipmentId);}
    public Task CalculateMinuteAsync(long minute,CancellationToken cancellation)
        => CalculateMinuteSlicedAsync(minute,cancellation,null);
    public async Task CalculateMinuteSlicedAsync(long minute,CancellationToken cancellation,SimulationSlice slice)
    {
        if(Failure!=null)throw new InvalidOperationException("Bee cycle requires restore.",Failure);
        var watch=System.Diagnostics.Stopwatch.StartNew();
        double initialWait=slice?.WaitMilliseconds??0;
        try
        {
            if(minute!=LastMinute+1)throw new InvalidOperationException("Bee minute mismatch.");
            cancellation.ThrowIfCancellationRequested();
            if(Bees.Count==0){LastMinute=minute;return;}
            World.Thermal.Refresh();await RefreshRegionsAsync(slice);RefreshCommands();stimulusCache.Clear();decayCache.Clear();requests.Clear();forageRequests.Clear();heat.Clear();
            int count=Bees.Count;
            for(int i=0;i<count;i++)
            {
                if(slice!=null && (i&15)==0)await slice.CheckpointAsync();
                var b=Bees[i];BeginBeePosition(b);if(b.stage==CycleStage.Dead)continue;b.action=CycleAction.Idle;b.tickActivity=0;b.lastAcquired=0;
                if(b.health<=0||b.development>=DevelopmentLimit(b)){Die(b);continue;}
                if(b.stage!=CycleStage.Adult){DevelopBrood(b);BaseHeat(b);continue;}
                double basal=Math.Min(b.reserve,FUnit("basalSugarCost"));b.reserve-=basal;Counters.consumedSugar+=basal;
                b.energy=Math.Max(0,b.energy-FUnit("basalEnergyCost"));
                if(b.reserve<=0&&b.energy<=0)b.health=Math.Max(0,b.health-FUnit("starvation"));
                UpdateDesires(b);Run(b);BaseHeat(b);
                if(b.health<=0||b.development>=DevelopmentLimit(b))Die(b);
            }
            activePositionBee=null;
            await ResolveForageRequestsAsync(slice);
            await ResolveRequestsAsync(slice);
            foreach(var h in heat)h.Key.Item1.QueueBeeHeat(h.Key.Item2,h.Value);
            LastMinute=minute;
        }
        catch(Exception e){Failure=e;throw;}
        finally{activePositionBee=null;LastMilliseconds=watch.Elapsed.TotalMilliseconds-((slice?.WaitMilliseconds??0)-initialWait);}
        return;
    }
    public List<CycleCommand> Commands { get; private set; }=new List<CycleCommand>();
    readonly HashSet<(CycleTask,BeeGridPosition)> activeCommands=new HashSet<(CycleTask,BeeGridPosition)>();
    readonly Dictionary<(CycleTask,BeeGridPosition),CycleCommand> commandIndex=new Dictionary<(CycleTask,BeeGridPosition),CycleCommand>();
    readonly Dictionary<int,List<Region>> groupRegions=new Dictionary<int,List<Region>>();
    void Command(CycleTask task,BeeGridPosition target,int group,double need,bool localOnly=false)
    {
        if(need<=0)return;
        activeCommands.Add((task,target));
        commandIndex.TryGetValue((task,target),out var command);
        if(command==null)
        {
            // Gate new construction commands only; existing commands keep their lifecycle.
            if(task==CycleTask.Build && groups[group].sugar<=groups[group].count*Settings.buildStockSugarPerBee)return;
            if(Random()>=F("commandChance",new CycleInputs{need=need}))return;
            command=new CycleCommand{task=task,target=new CyclePoint(target)};Commands.Add(command);commandIndex.Add((task,target),command);Counters.commands++;
        }
        command.group=group;command.need=need;command.localOnly=localOnly;
    }
    void RefreshCommands()
    {
        activeCommands.Clear();commandIndex.Clear();foreach(var command in Commands){var key=(command.task,command.target.Grid);if(!commandIndex.ContainsKey(key))commandIndex.Add(key,command);}
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
        IndexCommands();
    }
    void IndexCommands()
    {
        indexedCommands.Clear();
        foreach(var command in Commands)
            indexedCommands.Add(new IndexedCommand { command=command, key=DesireKey(command.task,command.target.Grid) });
        BeginDesireTick();
    }
    // Per-minute memoization of pure formulas; no state/RNG or iteration order changes.
    readonly Dictionary<CycleCommand,double> stimulusCache=new Dictionary<CycleCommand,double>();
    readonly Dictionary<double,double> decayCache=new Dictionary<double,double>();
    double Stimulus(CycleCommand command)
    {
        if(!stimulusCache.TryGetValue(command,out var value))
            stimulusCache.Add(command,value=F("commandStimulus",new CycleInputs{need=command.need}));
        return value;
    }
    double Decay(double amount)
    {
        if(!decayCache.TryGetValue(amount,out var value))
            decayCache.Add(amount,value=F("desireNext",new CycleInputs{amount=amount}));
        return value;
    }
    // Runtime-only identities retain tuple equality, without hashing the same grid for every bee.
    // IDs and lookup order never participate in simulation or persistence.
    readonly Dictionary<(CycleTask,BeeGridPosition),int> desireKeys=new Dictionary<(CycleTask,BeeGridPosition),int>();
    struct IndexedCommand { public CycleCommand command; public int key; }
    readonly List<IndexedCommand> indexedCommands=new List<IndexedCommand>();
    int DesireKey(CycleTask task,BeeGridPosition target)
    {
        var key=(task,target);
        if(!desireKeys.TryGetValue(key,out int value)){value=desireKeys.Count;desireKeys.Add(key,value);}
        return value;
    }
    void UpdateMutableDesires(CycleBee b)
    {
        foreach(var d in b.desires)d.stimulatedThisTick=false;
        if(b.desireIndex==null){b.desireIndex=new Dictionary<int,CycleDesire>();foreach(var d in b.desires)b.desireIndex[DesireKey(d.task,d.target.Grid)]=d;}
        if(Position(b,out var p)&&Air(p) is BeeGridTopology.Cell air)
        foreach(var indexed in indexedCommands)
        {
            var command=indexed.command;
            if(command.group!=air.Group||(command.localOnly&&!command.target.Grid.Equals(p))||(b.queen!=(command.task==CycleTask.Lay)))continue;
            if(!b.desireIndex.TryGetValue(indexed.key,out var desire)){desire=new CycleDesire{task=command.task,target=command.target};b.desires.Add(desire);b.desireIndex.Add(indexed.key,desire);}
            double stimulus=Stimulus(command);
            if(stimulus>0){desire.weight+=stimulus;desire.stimulatedThisTick=true;}
        }
        for(int i=b.desires.Count-1;i>=0;i--){var d=b.desires[i];if(!d.stimulatedThisTick)d.weight=Decay(d.weight);if(d.weight==0){b.desireIndex.Remove(DesireKey(d.task,d.target.Grid));b.desires.RemoveAt(i);}}
    }
    public double CargoFree(CycleBee b,bool pollen)=>Math.Max(0,(pollen?Settings.pollenCarryCapacity:Settings.carryCapacity)-(pollen?b.cargo.pollen:b.cargo.Sugar));
    public double CargoRatio(CycleBee b)=>Math.Max(b.cargo.Sugar/Settings.carryCapacity,b.cargo.pollen/Settings.pollenCarryCapacity);
    void Choose(CycleBee b)
    {
        double feed=FRatio("feedDesire",1-b.reserve/Settings.reserveTarget),rest=FRatio("restDesire",1-b.energy/Settings.maxEnergy),recover=FRatio("recoverDesire",1-b.health/Settings.maxHealth),store=FRatio("storeDesire",CargoRatio(b));
        double total=DesireTotal(b,feed+rest+recover+store);
        if(total<=0)return;double choice=Random()*total;b.elapsed=0;
        if((choice-=feed)<0)b.task=CycleTask.Feed;
        else if((choice-=rest)<0)b.task=CycleTask.Rest;
        else if((choice-=recover)<0)b.task=CycleTask.Recover;
        else if((choice-=store)<0)b.task=CycleTask.Store;
        else for(int i=0;i<b.DesireCount;i++){var d=b.DesireAt(i);if((choice-=d.weight)<0){b.task=d.task;b.target=d.target;b.hasTarget=true;break;}}
    }
    void End(CycleBee b,CycleEnd end)
    {
        switch(end){case CycleEnd.Completed:Counters.completed++;break;case CycleEnd.ResourceMissing:Counters.resourceFailed++;break;case CycleEnd.PathBlocked:Counters.pathFailed++;break;case CycleEnd.Interrupted:Counters.interrupted++;break;}
        if(b.task==CycleTask.Store&&end==CycleEnd.Completed)b.hasStoreExclusion=false;
        b.forageSource=-1;b.task=CycleTask.None;b.hasTarget=b.hasWaypoint=false;b.path.Clear();b.pathIndex=0;b.progress=0;b.elapsed=0;
    }
    void Act(CycleBee b,CycleAction action,double time=1)
    {
        b.action=action;Counters.actions[(int)action]++;b.tickActivity+=time;
        double rate=action==CycleAction.Move?Settings.moveDevelopment:action==CycleAction.Outside?Settings.forageDevelopment:action==CycleAction.Build?Settings.buildDevelopment:action==CycleAction.Rest?Settings.restDevelopment:action==CycleAction.Recover?Settings.recoverDevelopment:action==CycleAction.Feed?Settings.feedDevelopment:action==CycleAction.Acquire?Settings.acquireDevelopment:action==CycleAction.Store?Settings.storeDevelopment:action==CycleAction.Nurse?Settings.nurseDevelopment:action==CycleAction.Heat?Settings.heatDevelopment:action==CycleAction.Lay?Settings.layDevelopment:action==CycleAction.Clear?Settings.clearDevelopment:Settings.idleDevelopment;
        b.development+=FDevelopment(rate,time);
    }
    void Run(CycleBee b)
    {
        if(b.task==CycleTask.None)Choose(b);
        if(b.task==CycleTask.None){Act(b,CycleAction.Idle);return;}
        b.elapsed++;
        if(Random()<FInterrupt(b.elapsed))
        {if(b.outside){b.returning=true;b.interruptAfterReturn=true;}else{End(b,CycleEnd.Interrupted);Act(b,CycleAction.Idle);return;}}
        if(b.outside)
        {
            Act(b,CycleAction.Outside);b.outsideRemaining--;
            if(b.returning||b.outsideRemaining<=0)
            {
                if(!b.returning)
                {
                    // Version-19 in-flight bees have no recorded source: select at completion once.
                    if(b.forageSource<0)b.forageSource=SelectForageSource(b.task==CycleTask.ForagePollen);
                    double free=CargoFree(b,b.task==CycleTask.ForagePollen);
                    double amount=Math.Min(free,F(b.task==CycleTask.ForagePollen?"foragePollenAmount":"forageSugarAmount"));
                    forageRequests.Add(new ForageRequest{bee=b,source=b.forageSource,amount=amount,pollen=b.task==CycleTask.ForagePollen});
                }
                else FinishForage(b,b.interruptAfterReturn?CycleEnd.Interrupted:CycleEnd.Completed);
            }return;
        }
        if(!BeePosition(b,out var here)){End(b,CycleEnd.PathBlocked);return;}
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
                var dest=SelectRegion(b,here,r=>!r.clearing&&(!b.hasStoreExclusion||!r.position.Equals(b.storeExclusion.Grid))&&((b.cargo.Sugar>0&&r.freeSugar>0)||(b.cargo.pollen>0&&r.freePollen>0)),true);
                if(dest==null){End(b,CycleEnd.ResourceMissing);break;}
                if(!Move(b,dest.position,ref time))break;
                requests.Add(new Request{bee=b,region=dest,amount=F("storeAmount",new CycleInputs{time=time,amount=b.cargo.Mass}),kind=2});Act(b,CycleAction.Store,time);break;
            case CycleTask.ForageNectar:case CycleTask.ForagePollen:
                if(CargoFree(b,b.task==CycleTask.ForagePollen)<=1e-8){End(b,CycleEnd.Completed);break;}
                if(Move(b,b.target.Grid,ref time)){
                    b.forageSource=SelectForageSource(b.task==CycleTask.ForagePollen);
                    if(b.forageSource<0){End(b,CycleEnd.ResourceMissing);break;}
                    b.returnPoint=new CyclePoint(b.target.Grid);b.outside=true;b.outsideRemaining=F("forageDuration");World.BeeLocations.Remove(b.id);InvalidateBeePosition(b);Act(b,CycleAction.Outside,time);
                }break;
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
                else if(b.task==CycleTask.Clear){bool takeSugar=r.sugar>0&&CargoFree(b,false)>1e-8;bool takePollen=r.pollen>0&&CargoFree(b,true)>1e-8;if(!takeSugar&&!takePollen){End(b,CycleEnd.Completed);break;}requests.Add(new Request{bee=b,region=r,kind=takeSugar?0:1,amount=Math.Min(Settings.acquireRate*time,CargoFree(b,!takeSugar))});b.hasStoreExclusion=true;b.storeExclusion=new CyclePoint(r.position);Act(b,CycleAction.Clear,time);}
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
            else if(BeePosition(b,out var p))Acquire(b,p,false);
            return;
        }
        double gain=Math.Min(F("restAmount",new CycleInputs{time=time,amount=Settings.maxEnergy-b.energy}),b.reserve*Settings.energyPerSugar);
        b.reserve-=gain/Settings.energyPerSugar;Counters.consumedSugar+=gain/Settings.energyPerSugar;b.energy+=gain;Act(b,CycleAction.Rest,time);
    }
    void Acquire(CycleBee b,BeeGridPosition here,bool pollen)
    {
        if(CargoFree(b,pollen)<=1e-8){End(b,CycleEnd.ResourceMissing);return;}
        var r=SelectRegion(b,here,v=>(pollen?v.pollen:v.sugar)>0,false);
        if(r==null){End(b,CycleEnd.ResourceMissing);return;}
        double time=1;if(!Move(b,r.position,ref time))return;
        requests.Add(new Request{bee=b,region=r,kind=pollen?1:0,amount=F("acquireAmount",new CycleInputs{amount=CargoFree(b,pollen),time=time})});Act(b,CycleAction.Acquire,time);
    }
    Region SelectRegion(CycleBee b,BeeGridPosition here,Predicate<Region> eligible,bool storage)
    {
        if(b.hasWaypoint&&regionAt.TryGetValue(b.waypoint.Grid,out var old)&&eligible(old))return old;
        var air=Air(here);if(air==null)return null;
        Region selected=null;double total=0,best=double.PositiveInfinity;
        if(!groupRegions.TryGetValue(air.Group,out var candidates))return null;
        foreach(var r in candidates)
        {
            if(r.group!=air.Group||!eligible(r))continue;
            double dist=RouteDistance(here,r.position,b.queen);if(double.IsPositiveInfinity(dist))continue;
            if(!storage){if(dist<best){best=dist;selected=r;}continue;}
            double w=F("storagePreference",new CycleInputs{available=(b.cargo.Sugar>0?r.freeSugar:0)+(b.cargo.pollen>0?r.freePollen:0),height=r.height/Settings.storageHeightScale,distance=dist});total+=w;if(Random()*total<w)selected=r;
        }
        if(selected!=null){b.waypoint=new CyclePoint(selected.position);b.hasWaypoint=true;}
        return selected;
    }
    List<CyclePoint> FindPathUncached(BeeGridPosition start,BeeGridPosition target,bool queen)
    {
        if(start.Equals(target))return new List<CyclePoint>();
        if(TryRouteTree(start,queen,out var cached))return Reconstruct(start,target,cached);
        var queue=new Queue<BeeGridPosition>();var previous=new Dictionary<BeeGridPosition,BeeGridPosition>();queue.Enqueue(start);previous[start]=start;
        while(queue.Count>0)
        {
            var p=queue.Dequeue();foreach(var m in World.BeeLocations.GetNeighbours(World,p))
            {
                if((queen&&!m.AllowsQueen)||previous.ContainsKey(m.Target))continue;previous[m.Target]=p;
                queue.Enqueue(m.Target);
            }
        }
        CacheRouteTree(start,queen,previous);
        return Reconstruct(start,target,previous);
    }
    static List<CyclePoint> Reconstruct(BeeGridPosition start,BeeGridPosition target,Dictionary<BeeGridPosition,BeeGridPosition> previous)
    {if(!previous.ContainsKey(target))return null;var result=new List<CyclePoint>();for(var q=target;!q.Equals(start);q=previous[q])result.Add(new CyclePoint(q));result.Reverse();return result;}
    double RouteDistance(BeeGridPosition start,BeeGridPosition target,bool queen)
    {
        if(start.Equals(target))return 0;
        if(!TryRouteTree(start,queen,out var previous)){FindPathUncached(start,target,queen);if(!TryRouteTree(start,queen,out previous))return double.PositiveInfinity;}
        if(!previous.ContainsKey(target))return double.PositiveInfinity;
        double n=0;for(var p=target;!p.Equals(start);p=previous[p])n+=Distance(previous[p],p);return n;
    }
    double DistanceUncached(BeeGridPosition a,BeeGridPosition b)
    {var x=World.BeeLocations.Topology(World).SupportCell(a);var y=World.BeeLocations.Topology(World).SupportCell(b);return Math.Max(5,Math.Abs(x.X-y.X)*(x.Dx+y.Dx)/2+Math.Abs(x.Y-y.Y)*(x.Dy+y.Dy)/2+Math.Abs(x.Z-y.Z)*(x.Dz+y.Dz)/2);}
    double PathLength(BeeGridPosition p,List<CyclePoint> path){double n=0;foreach(var q in path){n+=Distance(p,q.Grid);p=q.Grid;}return n;}
    bool Move(CycleBee b,BeeGridPosition target,ref double time)
    {
        if(!BeePosition(b,out var here)){End(b,CycleEnd.PathBlocked);return false;}
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
        double original=time,speed=FUnit("moveDistance");bool advanced=false;
        if(speed<=0){End(b,CycleEnd.PathBlocked);return false;}
        while(time>1e-12&&b.pathIndex<b.path.Count)
        {
            var next=b.path[b.pathIndex].Grid;double distance=Distance(here,next),need=(distance-b.progress)/speed;
            if(need>time){b.progress+=time*speed;time=0;break;}
            if(!World.BeeLocations.CanMove(World,here,next,b.queen)){
                if(advanced){World.BeeLocations.Set(World,b.id,here);InvalidateBeePosition(b);}
                b.path.Clear();b.progress=0;End(b,CycleEnd.PathBlocked);return false;
            }
            time-=need;b.progress=0;b.pathIndex++;here=next;advanced=true;
        }
        // Move does not yield or notify observers between steps. Validate every edge in the
        // original order, but publish the final location once, before any subsequent action.
        if(advanced){World.BeeLocations.Set(World,b.id,here);InvalidateBeePosition(b);}
        double used=original-time;b.energy=Math.Max(0,b.energy-F("movementEnergyCost",new CycleInputs{time=used}));Act(b,CycleAction.Move,used);
        return here.Equals(target)&&time>1e-12;
    }
    void BaseHeat(CycleBee b){if(b.stage!=CycleStage.Dead)AddHeat(b,FUnit("basalHeat"));}
    void AddHeat(CycleBee b,double joules)
    {
        if(b.outside||!BeePosition(b,out var p))return;var air=Air(p);if(air?.Grid==null)return;
        var key=(Owner(air),OwnerIndex(air));heat.TryGetValue(key,out var old);heat[key]=old+joules;Counters.heatJ+=joules;
    }
    double DevelopmentLimit(CycleBee b)=>b.queen?Settings.queenMaxDevelopment:Settings.maxDevelopment;
    void Die(CycleBee b)
    {
        if(b.stage==CycleStage.Dead)return;
        if(b.stage!=CycleStage.Adult&&BeePosition(b,out var p)&&items.TryGetValue(p.EquipmentId,out var item)&&item.GetContent<FrameData>() is FrameData frame)
        {var face=p.Face==BeeFace.MinusY?frame.Front:frame.Back;if(face.GetBeeId(b.broodColumn,b.broodRow)==b.id)face.TryClearContent(b.broodColumn,b.broodRow);}
        Counters.lostSugar+=b.cargo.Sugar+b.reserve+b.broodSugar;Counters.lostPollen+=b.cargo.pollen+b.broodPollen;
        b.cargo=new CycleCargo();b.reserve=b.broodSugar=b.broodPollen=0;b.stage=CycleStage.Dead;b.task=CycleTask.None;b.action=CycleAction.Dead;b.outside=b.returning=false;b.path.Clear();b.ClearDesires();World.BeeLocations.Remove(b.id);InvalidateBeePosition(b);Counters.deaths++;
    }
}

