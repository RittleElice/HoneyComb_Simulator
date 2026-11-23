using System;
using System.Collections.Generic;
using System.Linq;
public sealed partial class BeeCycleRuntime
{
    void SeedInitialContents(HashSet<int> createdFrames)
    {
        var slots=new List<(Region region,int x,int y)>();
        foreach(var frame in items.Values.Where(i=>createdFrames.Contains(i.Id)).OrderBy(i=>i.Id))
        {
            var data=frame.GetContent<FrameData>();
            foreach(var face in new[]{data.Front,data.Back})
            for(int y=0;y<face.Height;y++)for(int x=0;x<face.Width;x++)
            {
                var cell=face.GetCell(x,y);if(!cell.IsComplete||cell.Content!=CellContentKind.Empty)continue;
                var region=regions.Find(r=>r.frame.Id==frame.Id&&ReferenceEquals(r.surface,face)&&r.x==x/5&&r.z==y/4);
                if(region!=null)slots.Add((region,x,y));
            }
        }
        double sugar=Settings.demoSugar,water=Settings.demoWater,pollen=Settings.demoPollen;int next=0;
        while(sugar>0.0001||water>0.0001)
        {
            if(next>=slots.Count)throw new InvalidOperationException("Not enough completed initial cells for nectar.");
            var c=slots[next++];double volume=CombBalance.NectarVolumeMl(sugar,water);
            double fraction=Math.Min(1,CombBalance.LiquidCapacityMl*0.999999/volume);
            float s=(float)(sugar*fraction),w=(float)(water*fraction);
            if(!c.region.surface.TryAddNectar(c.x,c.y,Settings.forageSourceId,s,w))throw new InvalidOperationException("Initial nectar placement failed.");
            sugar=Math.Max(0,sugar-s);water=Math.Max(0,water-w);
        }
        while(pollen>0.0001)
        {
            if(next>=slots.Count)throw new InvalidOperationException("Not enough completed initial cells for pollen.");
            var c=slots[next++];float amount=(float)Math.Min(pollen,CombBalance.PollenCapacityMg*0.999999);
            if(!c.region.surface.TryAddPollen(c.x,c.y,amount))throw new InvalidOperationException("Initial pollen placement failed.");
            pollen=Math.Max(0,pollen-amount);
        }
        long broodCount=(long)Settings.demoEggs+Settings.demoLarvae+Settings.demoPupae;
        if(broodCount>slots.Count-next)throw new InvalidOperationException("Not enough completed initial cells for brood. Increase initial completed cells or reduce initial contents.");
        foreach(var stage in new[]{CycleStage.Egg,CycleStage.Larva,CycleStage.Pupa})
        {
            int count=stage==CycleStage.Egg?Settings.demoEggs:stage==CycleStage.Larva?Settings.demoLarvae:Settings.demoPupae;
            for(int i=0;i<count;i++)
            {
                var c=slots[next++];var b=Add(c.region.position,c.region.box);b.stage=stage;b.development=stage==CycleStage.Egg?0:stage==CycleStage.Larva?Settings.eggEnd:Settings.larvaEnd;b.reserve=0;b.broodColumn=c.x;b.broodRow=c.y;
                if(!c.region.surface.TrySetBrood(c.x,c.y,b.id))throw new InvalidOperationException("Initial brood placement failed.");
            }
        }
    }
}
