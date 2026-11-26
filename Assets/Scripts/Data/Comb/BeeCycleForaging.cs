using System;
using System.Collections.Generic;
using System.Threading.Tasks;

public sealed partial class BeeCycleRuntime
{
    struct ForageRequest { public CycleBee bee;public int source;public double amount;public bool pollen; }
    readonly List<ForageRequest> forageRequests=new List<ForageRequest>();
    bool forageSelectionActive;
    readonly double[][] forageWeights=new double[2][];
    readonly double[] forageTotals=new double[2];
    readonly bool[] forageReady=new bool[2];
    void ResetForageSelection(){forageReady[0]=forageReady[1]=false;}
    int SelectForageSource(bool pollen)
    {
        if(!forageSelectionActive)ResetForageSelection();
        var nectar=World.Weather?.Nectar;if(nectar==null)return -1;
        int kind=pollen?1:0;
        if(!forageReady[kind])
        {
            if(forageWeights[kind]==null||forageWeights[kind].Length!=nectar.Sources.Length)forageWeights[kind]=new double[nectar.Sources.Length];
            double sum=0;
            for(int i=0;i<nectar.Sources.Length;i++)
            {
                var source=nectar.Sources[i];double available=source==null?0:pollen?source.pollen:source.sugar;
                double weight=available>0?F("forageSourceWeight",new CycleInputs{available=available}):0;
                forageWeights[kind][i]=weight;sum+=weight;
            }
            forageTotals[kind]=sum;forageReady[kind]=true;
        }
        double total=forageTotals[kind];if(total<=0)return -1;
        if(double.IsInfinity(total))throw new InvalidOperationException("Nonfinite forage source weight.");
        double pick=Random()*total;int last=-1;
        for(int i=0;i<forageWeights[kind].Length;i++)
        {
            double weight=forageWeights[kind][i];if(weight<=0)continue;
            last=i;if((pick-=weight)<0)return i;
        }
        return last;
    }
    void FinishForage(CycleBee bee,CycleEnd result)
    {
        World.BeeLocations.Set(World,bee.id,bee.returnPoint.Grid);InvalidateBeePosition(bee);
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
        if(Settings.forageLoadBased!=0)ResetForageSelection();
        foreach(var q in forageRequests){
            if(q.bee.stage==CycleStage.Dead)continue;
            if(Settings.forageLoadBased!=0){FinishLoadForageTick(q.bee,q.pollen,q.amount);continue;}
            FinishForage(q.bee,q.bee.lastAcquired>0||q.amount<=0?CycleEnd.Completed:CycleEnd.ResourceMissing);
            BaseHeat(q.bee);
        }
    }
}
