using System;
using System.Collections.Generic;

public sealed partial class BeeCycleRuntime
{
    struct RelocationSite { public BeeGridPosition Position; public int Group; public EquipmentThermalState Owner; public int Index; }
    struct RelocationContribution { public (EquipmentThermalState,int) Here,Target; public bool HasHere,HasTarget; public double Amount; }
    readonly List<RelocationSite> relocationSites=new List<RelocationSite>();
    readonly Dictionary<(EquipmentThermalState,int),double> relocationOccupancy=new Dictionary<(EquipmentThermalState,int),double>();
    readonly Dictionary<long,RelocationContribution> relocationContributions=new Dictionary<long,RelocationContribution>();
    readonly HashSet<(EquipmentThermalState,int)> relocationSeen=new HashSet<(EquipmentThermalState,int)>();
    int relocationTopology=-1;
    void BeginRelocationTick()
    {
        relocationOccupancy.Clear();relocationContributions.Clear();
        if(Settings.relocateEnabled==0)return;
        var topology=World.BeeLocations.Topology(World);
        if(relocationTopology!=topology.Builds){
            relocationSites.Clear();
            foreach(var p in topology.Positions)if(RelocationSiteAt(p,out var site))relocationSites.Add(site);
            relocationTopology=topology.Builds;
        }
        foreach(var bee in Bees)TrackRelocationBee(bee);
    }
    bool RelocationSiteAt(BeeGridPosition p,out RelocationSite site)
    {
        site=default;var air=Air(p);if(air?.Grid==null)return false;
        var owner=Owner(air);int index=OwnerIndex(air);
        if(owner.AirVolumeM3[index]<=0)return false;
        site=new RelocationSite{Position=p,Group=air.Group,Owner=owner,Index=index};return true;
    }
    void ChangeRelocationCount((EquipmentThermalState,int) key,double amount)
    {relocationOccupancy.TryGetValue(key,out var old);relocationOccupancy[key]=old+amount;}
    void TrackRelocationBee(CycleBee bee)
    {
        if(Settings.relocateEnabled==0)return;
        if(relocationContributions.TryGetValue(bee.id,out var old)){
            if(old.HasHere)ChangeRelocationCount(old.Here,-old.Amount);
            if(old.HasTarget)ChangeRelocationCount(old.Target,-old.Amount);
            relocationContributions.Remove(bee.id);
        }
        if(bee.stage!=CycleStage.Adult||bee.outside||!BeePosition(bee,out var p)||!RelocationSiteAt(p,out var here))return;
        var entry=new RelocationContribution{HasHere=true,Here=(here.Owner,here.Index),Amount=RepresentedBees(bee)};
        ChangeRelocationCount(entry.Here,entry.Amount);
        if(bee.task==CycleTask.Relocate&&bee.hasTarget&&RelocationSiteAt(bee.target.Grid,out var target)){
            entry.Target=(target.Owner,target.Index);entry.HasTarget=!entry.Target.Equals(entry.Here);
            if(entry.HasTarget)ChangeRelocationCount(entry.Target,entry.Amount);
        }
        relocationContributions[bee.id]=entry;
    }
    double RelocationDensity(RelocationSite site,double incoming=0)
    {relocationOccupancy.TryGetValue((site.Owner,site.Index),out var count);return Math.Max(0,count+incoming)/(site.Owner.AirVolumeM3[site.Index]*1e6);}
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
        double currentT=RelocationTemperatureScore(here.Owner.TemperatureC(here.Index)),currentD=RelocationCrowdingScore(RelocationDensity(here));
        double total=0;bool found=false;BeeGridPosition selected=default;relocationSeen.Clear();
        foreach(var site in relocationSites){
            var key=(site.Owner,site.Index);if(site.Group!=here.Group||key.Equals((here.Owner,here.Index))||relocationSeen.Contains(key))continue;
            double t=RelocationTemperatureScore(site.Owner.TemperatureC(site.Index)),d=RelocationCrowdingScore(RelocationDensity(site,RepresentedBees(bee)));
            double improvement=currentT+currentD-t-d;
            if(t>currentT||d>currentD||improvement<=Settings.relocateMinImprovement)continue;
            double distance=RouteDistance(p,site.Position,bee.queen);if(double.IsPositiveInfinity(distance))continue;
            relocationSeen.Add(key);double weight=F("relocateDestination",new CycleInputs{amount=improvement,distance=distance});if(weight<=0)continue;
            total+=weight;if(Random()*total<weight){selected=site.Position;found=true;}
        }
        if(!found){Counters.relocationNoTarget++;return false;}
        if(bee.task!=CycleTask.None)End(bee,CycleEnd.Interrupted);
        Counters.relocationStarted++;bee.task=CycleTask.Relocate;bee.target=new CyclePoint(selected);bee.hasTarget=true;bee.elapsed=0;
        return true;
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
        if(!bee.hasTarget||!BeePosition(bee,out var p)||!RelocationSiteAt(p,out var site)){End(bee,CycleEnd.PathBlocked);return;}
        if(RelocationTemperatureScore(site.Owner.TemperatureC(site.Index))+RelocationCrowdingScore(RelocationDensity(site))<=0||Move(bee,bee.target.Grid,ref time)){
            Counters.relocationCompleted++;End(bee,CycleEnd.Completed);bee.task=CycleTask.Rest;
            if(time>1e-12)Rest(bee,time);
        }
    }
}
