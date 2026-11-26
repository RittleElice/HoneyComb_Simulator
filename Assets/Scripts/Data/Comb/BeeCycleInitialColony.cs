using System;
using System.Collections.Generic;
using System.Linq;
public sealed partial class BeeCycleRuntime
{
    static string[] LoadInitialCombLayout()
    {
        var asset=UnityEngine.Resources.Load<UnityEngine.TextAsset>("BeeInitialCombLayout");
        if(asset==null)throw new InvalidOperationException("Initial comb layout asset is missing.");
        var rows=asset.text.Split(new[]{'\r','\n'},StringSplitOptions.RemoveEmptyEntries).Select(s=>s.Trim()).Where(s=>s.Length>0&&!s.StartsWith(";")).ToArray();
        if(rows.Length!=FrameData.DefaultHeight||rows.Any(s=>s.Length!=FrameData.DefaultWidth||s.Any(c=>"#.HPB".IndexOf(c)<0)))throw new InvalidOperationException("Initial comb layout must be 25x12 using # . H P B.");
        return rows;
    }
    void SeedInitialContents(HashSet<int> createdFrames,string[] layout)
    {
        var slots=new List<(Region region,int x,int y,char role,int face,int rank)>();int faceIndex=0;
        foreach(var frame in items.Values.Where(i=>createdFrames.Contains(i.Id)).OrderBy(i=>i.Id))
        {
            var data=frame.GetContent<FrameData>();
            foreach(var face in new[]{data.Front,data.Back})
            {
                int rank=0;
                for(int row=0;row<face.Height;row++)for(int x=0;x<face.Width;x++)
                {
                    char role=layout[row][x];if(role!='H'&&role!='P'&&role!='B')continue;int y=face.Height-1-row;
                    var region=regions.Find(r=>r.frame.Id==frame.Id&&ReferenceEquals(r.surface,face)&&r.x==x/5&&r.z==y/4);
                    if(region==null)throw new InvalidOperationException("Initial content cell has no reachable region.");
                    slots.Add((region,x,y,role,faceIndex,rank++));
                }
                faceIndex++;
            }
        }
        var honey=slots.Where(c=>c.role=='H').ToList();var pollenSlots=slots.Where(c=>c.role=='P').ToList();
        var brood=slots.Where(c=>c.role=='B').OrderBy(c=>c.rank).ThenBy(c=>c.face).ToList();
        long broodCount=(long)Settings.demoEggs+Settings.demoLarvae+Settings.demoPupae;
        if(broodCount>brood.Count)throw new InvalidOperationException("Initial B cells are insufficient for the configured brood count.");
        if(CombBalance.NectarVolumeMl(Settings.demoSugar,Settings.demoWater)>honey.Count*CombBalance.LiquidCapacityMl*0.999999||Settings.demoPollen>pollenSlots.Count*CombBalance.PollenCapacityMg*0.999999)throw new InvalidOperationException("Initial H/P cells have insufficient capacity for configured food.");
        double sugar=Settings.demoSugar,water=Settings.demoWater,pollen=Settings.demoPollen;
        var balance=CombBalance.Snapshot();
        double initialVolume=CombBalance.NectarVolumeMl(sugar,water);
        double cellVolume=honey.Count>0?initialVolume/honey.Count:0;
        double waterVolumeFraction=initialVolume>0?water/(1000*balance.WaterDensityGPerMl)/initialVolume:0;
        // Five source bands across each face and five repeating moisture levels
        // down the H rows. Center the levels over actual H slots, including partial
        // rows, so the existing whole-colony sugar/water totals are preserved.
        double meanLevel=honey.Count>0?honey.Average(c=>(c.region.surface.Height-1-c.y)%5):0;
        double moistureVariation=1.6*Math.Min(waterVolumeFraction,1-waterVolumeFraction);
        for(int i=0;i<honey.Count;i++)
        {
            var c=honey[i];int n=honey.Count-i;
            double fraction=waterVolumeFraction+moistureVariation*((c.region.surface.Height-1-c.y)%5-meanLevel)/4;
            float w=(float)(n==1?water:Math.Min(water,cellVolume*fraction*1000*balance.WaterDensityGPerMl));
            float s=(float)(n==1?sugar:Math.Min(sugar,cellVolume*(1-fraction)*1000*balance.SugarEffectiveDensityGPerMl));
            int source=Math.Min(4,c.x*5/c.region.surface.Width);
            if((s>0||w>0)&&!c.region.surface.TryAddNectar(c.x,c.y,source,s,w))throw new InvalidOperationException("Initial nectar placement failed.");
            sugar=Math.Max(0,sugar-s);water=Math.Max(0,water-w);
        }
        for(int i=0;i<pollenSlots.Count;i++)
        {
            var c=pollenSlots[i];float amount=(float)(pollen/(pollenSlots.Count-i));
            if(amount>0&&!c.region.surface.TryAddPollen(c.x,c.y,amount))throw new InvalidOperationException("Initial pollen placement failed.");
            pollen=Math.Max(0,pollen-amount);
        }
        int next=0;
        foreach(var stage in new[]{CycleStage.Egg,CycleStage.Larva,CycleStage.Pupa})
        {
            int count=stage==CycleStage.Egg?Settings.demoEggs:stage==CycleStage.Larva?Settings.demoLarvae:Settings.demoPupae;
            for(int i=0;i<count;i++)
            {
                var c=brood[next++];var b=Add(c.region.position,c.region.box);b.stage=stage;b.development=stage==CycleStage.Egg?0:stage==CycleStage.Larva?Settings.eggEnd:Settings.larvaEnd;b.reserve=0;b.broodColumn=c.x;b.broodRow=c.y;
                if(!c.region.surface.TrySetBrood(c.x,c.y,b.id))throw new InvalidOperationException("Initial brood placement failed.");
            }
        }
    }
}
