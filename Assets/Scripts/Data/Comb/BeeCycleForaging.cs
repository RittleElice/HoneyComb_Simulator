using System;
using System.Collections.Generic;
using System.Threading.Tasks;

public sealed partial class BeeCycleRuntime
{
    struct ForageRequest { public CycleBee bee;public int source;public double amount;public bool pollen; }
    readonly List<ForageRequest> forageRequests=new List<ForageRequest>();
    int SelectForageSource(bool pollen)
    {
        var nectar=World.Weather?.Nectar;if(nectar==null)return -1;
        double total=0;
        for(int i=0;i<nectar.Sources.Length;i++){
            var s=nectar.Sources[i];double available=s==null?0:pollen?s.pollen:s.sugar;
            if(available>0)total+=F("forageSourceWeight",new CycleInputs{available=available});
        }
        if(total<=0)return -1;
        if(double.IsInfinity(total))throw new InvalidOperationException("Nonfinite forage source weight.");
        double pick=Random()*total;int last=-1;
        for(int i=0;i<nectar.Sources.Length;i++){
            var s=nectar.Sources[i];double available=s==null?0:pollen?s.pollen:s.sugar;
            if(available<=0)continue;double weight=F("forageSourceWeight",new CycleInputs{available=available});if(weight<=0)continue;
            last=i;if((pick-=weight)<0)return i;
        }
        return last;
    }
    void FinishForage(CycleBee bee,CycleEnd result)
    {
        World.BeeLocations.Set(World,bee.id,bee.returnPoint.Grid);
        bee.outside=false;bee.returning=false;bee.interruptAfterReturn=false;
        End(bee,result);
    }
    async Task ResolveForageRequestsAsync(SimulationSlice slice)
    {
        if(forageRequests.Count==0)return;
        var nectar=World.Weather?.Nectar;
        int count=nectar?.Sources.Length??0;
        for(int source=0;source<count;source++){
            if(slice!=null)await slice.CheckpointAsync();
            double sugar=0,pollen=0;
            foreach(var q in forageRequests)if(q.source==source && q.bee.stage!=CycleStage.Dead){if(q.pollen)pollen+=q.amount;else sugar+=q.amount;}
            if(sugar<=0 && pollen<=0)continue;
            var taken=NectarHarvest.Withdraw(nectar,source,sugar,pollen);
            Counters.discardedWater+=taken.water;
            foreach(var q in forageRequests){
                if(q.source!=source || q.bee.stage==CycleStage.Dead)continue;
                double amount=q.pollen?(pollen>0?taken.pollen*(q.amount/pollen):0):(sugar>0?taken.sugar*(q.amount/sugar):0);
                q.bee.lastAcquired=amount;
                if(q.pollen){q.bee.cargo.pollen+=amount;Counters.externalPollen+=amount;}
                else {q.bee.cargo.Add(source,amount);Counters.externalSugar+=amount;}
            }
        }
        foreach(var q in forageRequests){
            if(q.bee.stage==CycleStage.Dead)continue;
            FinishForage(q.bee,q.bee.lastAcquired>0||q.amount<=0?CycleEnd.Completed:CycleEnd.ResourceMissing);
            BaseHeat(q.bee);
        }
    }
}
