using System;
using System.Collections.Generic;

public sealed partial class BeeCycleRuntime
{
    struct RelocationSite { public BeeGridPosition Position; public int Group; public EquipmentThermalState Owner; public int Index; public RelocationCell Cell; }
    struct RelocationContribution { public RelocationCell Here,Target; public bool HasHere,HasTarget; public double Amount; }
    readonly Dictionary<long,RelocationContribution> relocationContributions=new Dictionary<long,RelocationContribution>();
    // Derived state only. Counts use the original sequential additions/subtractions.
    sealed class RelocationCell
    {
        internal double Count,TemperatureRatio,CrowdingRatio,TemperatureScore,CrowdingScore;
        internal bool TemperatureReady,CrowdingReady;
        internal BeeCycleRules Rules;
        internal int Seen;
    }
    readonly Dictionary<(EquipmentThermalState,int),RelocationCell> relocationCells=new Dictionary<(EquipmentThermalState,int),RelocationCell>();
    sealed class RelocationNeighbour
    {
        internal RelocationSite Site;
        internal bool AllowsQueen,DistanceReady;
        internal double Distance;
    }
    readonly Dictionary<BeeGridPosition,RelocationNeighbour[]> relocationNeighbours=new Dictionary<BeeGridPosition,RelocationNeighbour[]>();
    RelocationNeighbour[] RelocationNeighbours(BeeGridPosition p)
    {
        if(forageSelectionActive&&relocationNeighbours.TryGetValue(p,out var cached))return cached;
        var result=new List<RelocationNeighbour>();
        foreach(var move in World.BeeLocations.GetNeighbours(World,p))
            if(AllowedSurfaceEdge(p,move.Target)&&RelocationSiteAt(move.Target,out var site))
                result.Add(new RelocationNeighbour{Site=site,AllowsQueen=move.AllowsQueen});
        var entries=result.ToArray();
        // Derived, tick-local geometry only. Live counts and scores are never frozen.
        if(forageSelectionActive){if(relocationNeighbours.Count>=16384)relocationNeighbours.Clear();relocationNeighbours.Add(p,entries);}
        return entries;
    }
    int relocationSelectionStamp;
    void PrepareRelocationScores(RelocationCell cell)
    {
        if(ReferenceEquals(cell.Rules,rules))return;
        cell.Rules=rules;cell.TemperatureReady=cell.CrowdingReady=false;
    }
    double CachedRelocationTemperature(RelocationSite site)
    {
        var cell=site.Cell;PrepareRelocationScores(cell);
        double temperature=site.Owner.TemperatureC(site.Index);
        double ratio=Math.Max(Settings.relocateColdStop-temperature,temperature-Settings.relocateHotStop)/Settings.relocateTemperatureScale;
        if(!cell.TemperatureReady||BitConverter.DoubleToInt64Bits(cell.TemperatureRatio)!=BitConverter.DoubleToInt64Bits(ratio)){
            cell.TemperatureScore=DestinationRelocationScore("relocateTemperatureDesire",ratio);cell.TemperatureRatio=ratio;cell.TemperatureReady=true;
        }
        return cell.TemperatureScore;
    }
    double CachedRelocationCrowding(RelocationSite site,double incoming=0)=>CachedRelocationCrowding(site,incoming,out _);
    double CachedRelocationCrowding(RelocationSite site,double incoming,out double density)
    {
        var cell=site.Cell;PrepareRelocationScores(cell);
        density=Math.Max(0,cell.Count+incoming)/(site.Owner.AirVolumeM3[site.Index]*1e6);
        double ratio=(density-Settings.relocateDensityStop)/(Settings.relocateDensityStart-Settings.relocateDensityStop);
        if(!cell.CrowdingReady||BitConverter.DoubleToInt64Bits(cell.CrowdingRatio)!=BitConverter.DoubleToInt64Bits(ratio)){
            cell.CrowdingScore=DestinationRelocationScore("relocateCrowdingDesire",ratio);cell.CrowdingRatio=ratio;cell.CrowdingReady=true;
        }
        return cell.CrowdingScore;
    }
    double DestinationRelocationScore(string formula,double ratio)
    {
        double score=F(formula,new CycleInputs{ratio=ratio});
        // The default desire is capped at ratio 1. Retain an excess-pressure
        // gradient for choosing destinations, scaled by the configured formula at 1.
        return ratio>1?score+(ratio-1)*FRatio(formula,1):score;
    }
    int relocationTopology=-1;
    void BeginRelocationTick()
    {
        relocationNeighbours.Clear();
        relocationContributions.Clear();
        foreach(var cell in relocationCells.Values){cell.Count=0;cell.Seen=0;}relocationSelectionStamp=0;
        if(Settings.relocateEnabled==0)return;
        var topology=World.BeeLocations.Topology(World);
        if(relocationTopology!=topology.Builds){
            relocationCells.Clear();
            relocationTopology=topology.Builds;
        }
        foreach(var bee in Bees)TrackRelocationBee(bee);
    }
    bool RelocationSiteAt(BeeGridPosition p,out RelocationSite site)
    {
        site=default;var air=Air(p);if(air?.Grid==null)return false;
        var owner=Owner(air);int index=OwnerIndex(air);
        if(owner.AirVolumeM3[index]<=0)return false;
        var key=(owner,index);
        if(!relocationCells.TryGetValue(key,out var cell)){
            cell=new RelocationCell();relocationCells.Add(key,cell);
        }
        site=new RelocationSite{Position=p,Group=air.Group,Owner=owner,Index=index,Cell=cell};return true;
    }
    void ChangeRelocationCount((EquipmentThermalState,int) key,double amount)
    {if(!relocationCells.TryGetValue(key,out var cell)){cell=new RelocationCell();relocationCells.Add(key,cell);}ChangeRelocationCount(cell,amount);}
    static void ChangeRelocationCount(RelocationCell cell,double amount)=>cell.Count=cell.Count+amount;
    void TrackRelocationBee(CycleBee bee)
    {
        if(Settings.relocateEnabled==0)return;
        if(relocationContributions.TryGetValue(bee.id,out var old)){
            if(old.HasHere)ChangeRelocationCount(old.Here,-old.Amount);
            if(old.HasTarget)ChangeRelocationCount(old.Target,-old.Amount);
            relocationContributions.Remove(bee.id);
        }
        if(bee.stage!=CycleStage.Adult||bee.outside||!BeePosition(bee,out var p)||!RelocationSiteAt(p,out var here))return;
        var entry=new RelocationContribution{HasHere=true,Here=here.Cell,Amount=RepresentedBees(bee)};
        ChangeRelocationCount(entry.Here,entry.Amount);
        if(bee.task==CycleTask.Relocate&&bee.hasTarget&&RelocationSiteAt(bee.target.Grid,out var target)){
            entry.Target=target.Cell;entry.HasTarget=!ReferenceEquals(entry.Target,entry.Here);
            if(entry.HasTarget)ChangeRelocationCount(entry.Target,entry.Amount);
        }
        relocationContributions[bee.id]=entry;
    }
    double RelocationDensity(RelocationSite site,double incoming=0)
    {return Math.Max(0,site.Cell.Count+incoming)/(site.Owner.AirVolumeM3[site.Index]*1e6);}
    double RelocationTemperatureScore(double temperature)
    {return F("relocateTemperatureDesire",new CycleInputs{ratio=Math.Max(Settings.relocateColdStop-temperature,temperature-Settings.relocateHotStop)/Settings.relocateTemperatureScale});}
    double RelocationCrowdingScore(double density)
    {return F("relocateCrowdingDesire",new CycleInputs{ratio=(density-Settings.relocateDensityStop)/(Settings.relocateDensityStart-Settings.relocateDensityStop)});}
    bool RelocationEligible(CycleBee bee)=>Settings.relocateEnabled!=0&&bee.stage==CycleStage.Adult&&!bee.outside&&(bee.task==CycleTask.None||bee.task==CycleTask.Rest||bee.task==CycleTask.Relocate)&&bee.energy>=Settings.maxEnergy*Settings.relocateMinEnergyFraction;
    // State desire, recomputed from the completed/current state; no command accumulation or RNG.
    public double RelocationWeight(CycleBee bee)
    {
        if(!RelocationEligible(bee)||!BeePosition(bee,out var p)||!RelocationSiteAt(p,out var site))return 0;
        double t=site.Owner.TemperatureC(site.Index),density=RelocationDensity(site);
        return (t<Settings.relocateColdStart||t>Settings.relocateHotStart?RelocationTemperatureScore(t):0)+(density>Settings.relocateDensityStart?RelocationCrowdingScore(density):0);
    }
    bool StartRelocation(CycleBee bee)
    {
        if(!RelocationEligible(bee)||!BeePosition(bee,out var p)||!RelocationSiteAt(p,out var here))return false;
        bool found=SelectLocalRelocation(bee,p,here,out var selected);
        if(!found){Counters.relocationNoTarget++;return false;}
        if(bee.task!=CycleTask.None)End(bee,CycleEnd.Interrupted);
        Counters.relocationStarted++;bee.task=CycleTask.Relocate;SetLocalRelocationTarget(bee,selected);bee.elapsed=0;
        return true;
    }
    bool SelectLocalRelocation(CycleBee bee,BeeGridPosition p,RelocationSite here,out BeeGridPosition selected)
    {
        double currentT=CachedRelocationTemperature(here),currentD=CachedRelocationCrowding(here,0,out double currentDensity);
        bool avoidCrowding=FRatio("relocateCrowdingDesire",1)>0;
        double total=0;bool found=false;selected=default;
        if(relocationSelectionStamp==int.MaxValue){foreach(var cell in relocationCells.Values)cell.Seen=0;relocationSelectionStamp=0;}
        int stamp=++relocationSelectionStamp;
        // Only directly traversable surface neighbours; keep topology order and queen gates.
        foreach(var entry in RelocationNeighbours(p)){
            if(bee.queen&&!entry.AllowsQueen)continue;
            var site=entry.Site;var cell=site.Cell;
            if(site.Group!=here.Group||ReferenceEquals(cell,here.Cell)||cell.Seen==stamp)continue;
            double t=CachedRelocationTemperature(site),d=CachedRelocationCrowding(site,RepresentedBees(bee),out double nextDensity);
            double improvement=currentT+currentD-t-d;
            // Never make an already uncomfortable density worse, including a bee's
            // projected arrival and existing reservations. Allow temperature/crowding
            // tradeoffs only when the combined destination discomfort improves.
            if((avoidCrowding&&nextDensity>Math.Max(currentDensity,Settings.relocateDensityStop))||improvement<=Settings.relocateMinImprovement)continue;
            cell.Seen=stamp;
            if(!entry.DistanceReady){entry.Distance=Distance(p,site.Position);entry.DistanceReady=true;}
            double weight=F("relocateDestination",new CycleInputs{amount=improvement,distance=entry.Distance});if(weight<=0)continue;
            total+=weight;if(Random()*total<weight){selected=site.Position;found=true;}
        }
        return found;
    }
    void SetLocalRelocationTarget(CycleBee bee,BeeGridPosition selected)
    {
        bee.target=new CyclePoint(selected);bee.hasTarget=true;bee.hasWaypoint=false;bee.directionActive=false;
        // The chosen neighbour is already connected: retain the regular movement/energy
        // accounting with a one-edge path, avoiding a whole-space BFS for a single step.
        bee.path.Clear();bee.path.Add(bee.target);bee.pathIndex=0;bee.progress=0;bee.topologyVersion=topologyVersion;
    }
    void CompleteLocalRelocation(CycleBee bee,double time)
    {
        Counters.relocationCompleted++;End(bee,CycleEnd.Completed);bee.task=CycleTask.Rest;
        TrackRelocationBee(bee);
        if(time>1e-12)Rest(bee,time);
    }
    void TryRelocateFromRest(CycleBee bee)
    {
        double move=RelocationWeight(bee);if(move<=0)return;
        double other=FRatio("feedDesire",1-bee.reserve/ReserveTarget(bee))+FRatio("restDesire",1-bee.energy/Settings.maxEnergy)+FRatio("recoverDesire",1-bee.health/Settings.maxHealth)+FRatio("storeDesire",CargoRatio(bee));
        if(Random()*(move+DesireTotal(bee,other))<move)StartRelocation(bee);
    }
    void RunRelocation(CycleBee bee,ref double time)
    {
        if(!RelocationEligible(bee)){End(bee,CycleEnd.Interrupted);bee.task=CycleTask.Rest;return;}
        while(true){
            if(!RelocationEligible(bee)){End(bee,CycleEnd.Interrupted);bee.task=CycleTask.Rest;TrackRelocationBee(bee);if(time>1e-12)Rest(bee,time);return;}
            if(!BeePosition(bee,out var p)||!RelocationSiteAt(p,out var site)){End(bee,CycleEnd.PathBlocked);return;}
            if(!bee.hasTarget&&RelocationTemperatureScore(site.Owner.TemperatureC(site.Index))+RelocationCrowdingScore(RelocationDensity(site))<=0){CompleteLocalRelocation(bee,time);return;}
            if(!bee.hasTarget){
                if(!SelectLocalRelocation(bee,p,site,out var selected)){CompleteLocalRelocation(bee,time);return;}
                SetLocalRelocationTarget(bee,selected);
            }
            if(time<=1e-12)return;
            // A saved in-progress distant target is finished before choosing local neighbours.
            if(!p.Equals(bee.target.Grid)){
                Move(bee,bee.target.Grid,ref time);
                if(bee.task!=CycleTask.Relocate||!BeePosition(bee,out p)||!p.Equals(bee.target.Grid))return;
            }
            // Reassess even at an exact minute boundary; a selected next edge is saved
            // in the existing target/path layout and is traversed when time is available.
            bee.hasTarget=bee.hasWaypoint=false;bee.directionActive=false;bee.progress=0;bee.path.Clear();bee.pathIndex=0;
            TrackRelocationBee(bee);
        }
    }
}
