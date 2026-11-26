using System;
public sealed partial class BeeCycleRuntime
{
    public double ForageLoadRatio(CycleBee bee,bool pollen)
    {
        double capacity=(pollen?Settings.pollenCarryCapacity:Settings.carryCapacity)*RepresentedBees(bee);
        return Math.Max(0,Math.Min(1,(pollen?bee.cargo.pollen:bee.cargo.Sugar)/capacity));
    }
    public double ForageReturnProbability(CycleBee bee)
    {
        if(Settings.forageLoadBased==0)return 0;
        return Math.Max(0,Math.Min(1,FRatio("forageReturnChance",ForageLoadRatio(bee,bee.task==CycleTask.ForagePollen))));
    }
    public double ForageShortageReturnProbability(double requested,double acquired)
    {
        double shortage=requested>0?Math.Max(0,Math.Min(1,1-acquired/requested)):0;
        return Math.Max(0,Math.Min(1,FRatio("forageShortageReturnChance",shortage)));
    }
    double ForageChoiceWeight(CycleBee bee,CycleTask task,double weight)
    {
        if(Settings.forageLoadBased==0||(task!=CycleTask.ForageNectar&&task!=CycleTask.ForagePollen))return weight;
        return weight*FRatio("forageDepartureWeight",ForageLoadRatio(bee,task==CycleTask.ForagePollen));
    }
    bool HasForageStock(int source,bool pollen)
    {
        var n=World.Weather?.Nectar;
        return n!=null&&source>=0&&source<n.Sources.Length&&(pollen?n.Sources[source].pollen:n.Sources[source].sugar)>0;
    }
    void RunLoadForage(CycleBee bee)
    {
        Act(bee,CycleAction.Outside);
        if(bee.returning){FinishForage(bee,bee.interruptAfterReturn?CycleEnd.Interrupted:CycleEnd.Completed);return;}
        bool pollen=bee.task==CycleTask.ForagePollen;
        if(CargoFree(bee,pollen)<=1e-8){FinishForage(bee,CycleEnd.Completed);return;}
        if(!HasForageStock(bee.forageSource,pollen)){
            forageReady[pollen?1:0]=false;bee.forageSource=SelectForageSource(pollen);
            // No alternative: queue an unmet request and test return once after allocation.
        }
        bee.lastAcquired=0;
        double amount=Math.Min(CargoFree(bee,pollen),F(pollen?"forageCollectPollen":"forageCollectSugar",new CycleInputs{time=1})*RepresentedBees(bee));
        forageRequests.Add(new ForageRequest{bee=bee,source=bee.forageSource,amount=amount,pollen=pollen});
    }
    void FinishLoadForageTick(CycleBee bee,bool pollen,double requested)
    {
        // Allocation for every bee is complete before testing return probability.
        if(CargoFree(bee,pollen)<=1e-8){FinishForage(bee,CycleEnd.Completed);BaseHeat(bee);return;}
        double chance=ForageReturnProbability(bee);
        if(chance>0&&Random()<chance){FinishForage(bee,CycleEnd.Completed);BaseHeat(bee);return;}
        double shortageChance=ForageShortageReturnProbability(requested,bee.lastAcquired);
        if(shortageChance>0&&Random()<shortageChance){FinishForage(bee,ForageLoadRatio(bee,pollen)>0?CycleEnd.Completed:CycleEnd.ResourceMissing);BaseHeat(bee);return;}
        if(bee.lastAcquired<requested||!HasForageStock(bee.forageSource,pollen))
            bee.forageSource=SelectForageSource(pollen); // -1 means search again next tick.
    }
}
