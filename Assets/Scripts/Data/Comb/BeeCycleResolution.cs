using System;
using System.Collections.Generic;

public sealed partial class BeeCycleRuntime
{
    readonly List<Request> batch=new List<Request>();
    readonly Dictionary<(Region,int),List<Request>> requestBuckets=new Dictionary<(Region,int),List<Request>>();
    readonly List<List<Request>> requestBucketPool=new List<List<Request>>();
    async System.Threading.Tasks.Task ResolveRequestsAsync(SimulationSlice slice)
    {
        foreach(var bucket in requestBucketPool)bucket.Clear();requestBuckets.Clear();int used=0;
        int visited=0;foreach(var q in requests){if(slice!=null&&(visited++&255)==0)await slice.CheckpointAsync();var key=(q.region,q.kind);if(!requestBuckets.TryGetValue(key,out var bucket)){if(used==requestBucketPool.Count)requestBucketPool.Add(new List<Request>());bucket=requestBucketPool[used++];requestBuckets.Add(key,bucket);}bucket.Add(q);}
        foreach(var r in regions)
        {
            if(slice!=null)await slice.CheckpointAsync();
            for(int kind=0;kind<=4;kind++)
            {
                batch.Clear();double total=0;
                if(!requestBuckets.TryGetValue((r,kind),out var bucket))continue;
                foreach(var q in bucket)if(q.bee.stage!=CycleStage.Dead){batch.Add(q);total+=q.amount;}
                if(total<=0)continue;
                if(kind<=1)
                {
                    bool pollen=kind==1;var cargo=new CycleCargo();double remaining=total;
                    Cells(r,(x,y)=>{if(remaining<=1e-10)return;var taken=r.surface.WithdrawCycle(x,y,remaining,pollen,out double water);remaining-=taken.Mass;cargo.Add(taken,1);Counters.discardedWater+=water;});
                    foreach(var q in batch){double share=q.amount/total;q.bee.cargo.Add(cargo,share);q.bee.lastAcquired=cargo.Mass*share;if(q.bee.task==CycleTask.Clear)RelieveWork(q.bee,CycleTask.Clear,q.bee.lastAcquired/Math.Max(1e-9,Settings.acquireRate*RepresentedBees(q.bee)),Settings.workClearRelief);if(cargo.Mass<=0){RecordDemandFailure(q.bee,pollen?1:0);End(q.bee,CycleEnd.ResourceMissing);}}
                }
                else if(kind==2)StoreBatch(r);
                else if(kind==3)BuildBatch(r,total);
                else NurseBatch(r);
            }
        }
    }
    double[] storeSugarDemand=Array.Empty<double>(),storePollenDemand=Array.Empty<double>();
    CycleCargo[] storeCargo=Array.Empty<CycleCargo>();
    void StoreBatch(Region r)
    {
        if(storeSugarDemand.Length<batch.Count){storeSugarDemand=new double[batch.Count];storePollenDemand=new double[batch.Count];}
        if(storeCargo.Length<batch.Count)storeCargo=new CycleCargo[batch.Count];
        var sugarDemand=storeSugarDemand;var pollenDemand=storePollenDemand;
        for(int i=0;i<batch.Count;i++){var q=batch[i];var cargo=q.bee.cargo;storeCargo[i]=cargo;double sugar=cargo.Sugar,mass=sugar+cargo.pollen;double fraction=mass>0?Math.Min(1,q.amount/mass):0;sugarDemand[i]=sugar*fraction;pollenDemand[i]=cargo.pollen*fraction;}
        Cells(r,(x,y)=>{
            double sf=Free(r.surface,x,y,false),pf=Free(r.surface,x,y,true);
            if(sf<=0&&pf<=0)return;
            double sugar=0,pollen=0;for(int i=0;i<batch.Count;i++){sugar+=sugarDemand[i];pollen+=pollenDemand[i];}
            bool usePollen=sf<=0||(pf>0&&pollen>0&&Random()*(sugar+pollen)<pollen);
            double total=usePollen?pollen:sugar;if(total<=0)return;
            double allocation=Math.Min(total,usePollen?pf:sf);
            for(int i=0;i<batch.Count;i++)
            {
                var cargo=storeCargo[i];double request=allocation*(usePollen?pollenDemand[i]:sugarDemand[i])/total;
                if(usePollen){double actual=r.surface.DepositCycle(x,y,0,request,true);cargo.pollen=Math.Max(0,cargo.pollen-actual);pollenDemand[i]=Math.Max(0,pollenDemand[i]-actual);}
                else
                {
                    double own=cargo.Sugar;if(own<=0)continue;double actual=0;
                    foreach(var s in cargo.sugars){double n=r.surface.DepositCycle(x,y,s.source,request*s.mass/own,false);s.mass=Math.Max(0,s.mass-n);actual+=n;}
                    sugarDemand[i]=Math.Max(0,sugarDemand[i]-actual);
                }
            }
        });
        foreach(var q in batch)if(q.bee.cargo.Mass<1e-7)End(q.bee,CycleEnd.Completed);
        Array.Clear(storeCargo,0,batch.Count);
    }
    void BuildBatch(Region r,double total)
    {
        double remaining=Math.Floor(total),actual=0;
        // A cell is selected probabilistically; adjacent started cells increase its preference.
        while(remaining>=1)
        {
            int cx=-1,cy=-1;double weights=0;
            Cells(r,(x,y)=>{
                var c=r.surface.GetCell(x,y);if(Settings.workDesireEnabled!=0?c.Construction>=100:c.IsComplete)return;
                int adjacent=0;
                for(int dy=-1;dy<=1;dy++)for(int dx=-1;dx<=1;dx++)if(Math.Abs(dx)+Math.Abs(dy)==1&&r.surface.IsInside(x+dx,y+dy)&&r.surface.GetCell(x+dx,y+dy).Construction>0)adjacent++;
                double w=F("buildCellWeight",new CycleInputs{amount=adjacent});weights+=w;if(Random()*weights<w){cx=x;cy=y;}
            });
            if(cx<0)break;int amount=(int)Math.Min(remaining,100-r.surface.GetCell(cx,cy).Construction);
            if(!r.surface.TrySetConstruction(cx,cy,(byte)(r.surface.GetCell(cx,cy).Construction+amount)))break;
            actual+=amount;remaining-=amount;
        }
        foreach(var q in batch){double work=actual*q.amount/total;Counters.consumedSugar+=q.bee.cargo.Consume(work*BuildPointCost);RelieveWork(q.bee,CycleTask.Build,work/Math.Max(1e-9,Settings.buildPoints),Settings.workBuildRelief);}
        Counters.built+=actual;
    }
    void NurseBatch(Region r)
    {
        foreach(bool pollen in new[]{false,true})
        {
            double need=0,supply=0;
            foreach(var b in r.brood)if(b.stage==CycleStage.Larva)need+=Math.Max(0,(pollen?Settings.larvaPollenPerTick:Settings.larvaSugarPerTick)*Settings.representativeScale*Settings.broodReserveTicks-(pollen?b.broodPollen:b.broodSugar));
            foreach(var q in batch)supply+=Math.Min(q.amount,pollen?q.bee.cargo.pollen:q.bee.cargo.Sugar);
            double actual=Math.Min(need,supply);if(actual<=0)continue;
            foreach(var q in batch)
            {
                double n=actual*Math.Min(q.amount,pollen?q.bee.cargo.pollen:q.bee.cargo.Sugar)/supply;
                if(pollen)q.bee.cargo.pollen=Math.Max(0,q.bee.cargo.pollen-n);else q.bee.cargo.Consume(n);
                RelieveWork(q.bee,CycleTask.Nurse,n/Math.Max(1e-9,Settings.nurseRate*RepresentedBees(q.bee)),Settings.workNurseRelief);
            }
            foreach(var b in r.brood)if(b.stage==CycleStage.Larva)
            {
                double n=actual*Math.Max(0,(pollen?Settings.larvaPollenPerTick:Settings.larvaSugarPerTick)*Settings.representativeScale*Settings.broodReserveTicks-(pollen?b.broodPollen:b.broodSugar))/need;
                if(pollen)b.broodPollen+=n;else b.broodSugar+=n;
            }
        }
    }
    void Lay(CycleBee queen,Region r,double time)
    {
        if(!queen.queen||r.empty==0){if(queen.queen&&r.empty==0)RecordDemandFailure(queen,2);End(queen,CycleEnd.Completed);return;}
        Act(queen,CycleAction.Lay,time);if(Random()>=F("layChance",new CycleInputs{time=time}))return;
        int cx=-1,cy=-1;double total=0;bool hasBrood=regions.Exists(v=>v.frame==r.frame&&v.brood.Count>0);
        Cells(r,(x,y)=>{
            var c=r.surface.GetCell(x,y);if(!c.IsComplete||c.Content!=CellContentKind.Empty)return;
            int adjacent=0;for(int dy=-1;dy<=1;dy++)for(int dx=-1;dx<=1;dx++)if(r.surface.IsInside(x+dx,y+dy)&&r.surface.GetCell(x+dx,y+dy).Content==CellContentKind.Brood)adjacent++;
            double w=F("layCellWeight",new CycleInputs{ratio=hasBrood?1:0,amount=adjacent,distance=Math.Abs(x-(r.surface.Width-1)*0.5)+Math.Abs(y-(r.surface.Height-1)*0.5)});total+=w;if(Random()*total<w){cx=x;cy=y;}
        });
        if(cx<0)return;
        var b=Add(r.position,queen.homeBox);b.stage=CycleStage.Egg;b.development=0;b.reserve=0;b.broodColumn=cx;b.broodRow=cy;
        if(!r.surface.TrySetBrood(cx,cy,b.id))throw new InvalidOperationException("Brood reservation changed during action.");
        Counters.eggs++;RelieveWork(queen,CycleTask.Lay,1,Settings.workLayRelief);
    }
    void DevelopBrood(CycleBee b)
    {
        if(!World.BeeLocations.TryGet(b.id,out var p)){b.health=0;Die(b);return;}
        if(!regionAt.TryGetValue(p,out var r))
        {
            if(!items.TryGetValue(p.EquipmentId,out var item)||item.GetContent<FrameData>()==null){b.health=0;Die(b);return;}
            var f=item.GetContent<FrameData>();r=new Region{frame=item,surface=p.Face==BeeFace.MinusY?f.Front:f.Back,position=p,temperature=World.Weather?.Current.TemperatureCelsius??Settings.broodIdealTemperature};
        }
        if(r.surface.GetBeeId(b.broodColumn,b.broodRow)!=b.id){b.health=0;Die(b);return;}
        double factor=F("broodTemperature",new CycleInputs{temperature=r.temperature});
        if(b.stage==CycleStage.Larva)
        {
            double resource=Math.Min(1,Math.Min(b.broodSugar/(Settings.larvaSugarPerTick*RepresentedBees(b)),b.broodPollen/(Settings.larvaPollenPerTick*RepresentedBees(b))));
            double energy=Math.Min(1,b.energy/Settings.larvaEnergyPerTick);factor=F("larvaDevelopmentFactor",new CycleInputs{ratio=factor,available=resource,amount=energy});
            factor=Math.Min(factor,Math.Min(resource,energy));
            double sugar=Settings.larvaSugarPerTick*RepresentedBees(b)*factor,pollen=Settings.larvaPollenPerTick*RepresentedBees(b)*factor;
            b.broodSugar=Math.Max(0,b.broodSugar-sugar);b.broodPollen=Math.Max(0,b.broodPollen-pollen);b.energy=Math.Max(0,b.energy-Settings.larvaEnergyPerTick*factor);
            // Assimilation uses the consumed sugar; the provisional conversion is editable.
            b.energy=Math.Min(Settings.maxEnergy,b.energy+sugar*EnergyPerSugar(b));
            Counters.consumedSugar+=sugar;Counters.consumedPollen+=pollen;
            if(resource<=0)b.health=Math.Max(0,b.health-F("starvation",new CycleInputs{time=1}));
        }
        b.development+=F("broodDevelopment",new CycleInputs{ratio=factor,time=1});b.action=CycleAction.Develop;Counters.actions[(int)CycleAction.Develop]++;
        if(b.development>=Settings.adultStart){r.surface.TryClearContent(b.broodColumn,b.broodRow);b.stage=CycleStage.Adult;Counters.emerged++;}
        else if(b.development>=Settings.larvaEnd)b.stage=CycleStage.Pupa;
        else if(b.development>=Settings.eggEnd)b.stage=CycleStage.Larva;
        if(b.health<=0||b.development>=DevelopmentLimit(b))Die(b);
    }
}
