using System;
using System.Collections.Generic;
using System.Linq;
public sealed partial class BeeCycleRuntime
{
    // Local initialization RNG; does not consume simulation randomness. Persisted
    // contents/development retain the arrangement after loading a saved game.
    static void ShuffleInitialPatterns<T>(T[] values,Func<T,int> face,Random random)
    {
        var groups=Enumerable.Range(0,values.Length).GroupBy(i=>face(values[i])).Select(g=>g.ToArray()).ToArray();
        foreach(var indices in groups)for(int n=indices.Length-1;n>0;n--)
        {int a=indices[n],b=indices[random.Next(n+1)];var value=values[a];values[a]=values[b];values[b]=value;}
    }
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
        var visualRandom=new Random(unchecked((int)(World.Weather?.Seed??WeatherSimulator.DefaultSeed)));
        var honeyPatterns=honey.ToArray();var pollenPatterns=pollenSlots.ToArray();
        ShuffleInitialPatterns(honeyPatterns,c=>c.face,visualRandom);
        ShuffleInitialPatterns(pollenPatterns,c=>c.face,visualRandom);
        double sugar=Settings.demoSugar,water=Settings.demoWater,pollen=Settings.demoPollen;
        var balance=CombBalance.Snapshot();
        // Preserve the five-level palette, shuffled independently on each face.
        // The defaults match this palette. Normalize against configured totals so custom
        // initial sugar/water settings remain authoritative (including zero-mass cases).
        var sugarWeights=new double[honey.Count];var waterWeights=new double[honey.Count];
        double[] moistureLevels={.3,.4,.5,.65,.8};
        double totalSugarWeight=0,totalWaterWeight=0;
        for(int i=0;i<honey.Count;i++)
        {
            var c=honeyPatterns[i];double fill=.1+.2*(c.x%5),moisture=moistureLevels[(c.region.surface.Height-1-c.y)%5];
            double mass=CombBalance.LiquidCapacityMl*fill/((1-moisture)/(1000*balance.SugarEffectiveDensityGPerMl)+moisture/(1000*balance.WaterDensityGPerMl));
            sugarWeights[i]=mass*(1-moisture);waterWeights[i]=mass*moisture;
            totalSugarWeight+=sugarWeights[i];totalWaterWeight+=waterWeights[i];
        }
        for(int i=0;i<honey.Count;i++)
            if(CombBalance.NectarVolumeMl(Settings.demoSugar*sugarWeights[i]/totalSugarWeight,Settings.demoWater*waterWeights[i]/totalWaterWeight)>CombBalance.LiquidCapacityMl)
                throw new InvalidOperationException("Initial nectar palette exceeds cell capacity; reduce configured initial sugar/water.");
        for(int i=0;i<honey.Count;i++)
        {
            var c=honey[i];int n=honey.Count-i;
            float s=(float)(n==1?sugar:Math.Min(sugar,Settings.demoSugar*sugarWeights[i]/totalSugarWeight));
            float w=(float)(n==1?water:Math.Min(water,Settings.demoWater*waterWeights[i]/totalWaterWeight));
            int source=Math.Min(4,honeyPatterns[i].x*5/c.region.surface.Width);
            if((s>0||w>0)&&!c.region.surface.TryAddNectar(c.x,c.y,source,s,w))throw new InvalidOperationException("Initial nectar placement failed.");
            sugar=Math.Max(0,sugar-s);water=Math.Max(0,water-w);
        }
        // Shuffled five fill levels retain the configured total mass.
        double pollenWeight=pollenSlots.Sum(c=>.1+.2*(c.x%5));
        foreach(var c in pollenSlots)
            if(Settings.demoPollen*(.1+.2*(c.x%5))/pollenWeight>CombBalance.PollenCapacityMg)
                throw new InvalidOperationException("Initial pollen palette exceeds cell capacity; reduce configured initial pollen.");
        for(int i=0;i<pollenSlots.Count;i++)
        {
            var c=pollenSlots[i];float amount=(float)(i==pollenSlots.Count-1?pollen:Math.Min(pollen,Settings.demoPollen*(.1+.2*(pollenPatterns[i].x%5))/pollenWeight));
            if(amount>0&&!c.region.surface.TryAddPollen(c.x,c.y,amount))throw new InvalidOperationException("Initial pollen placement failed.");
            pollen=Math.Max(0,pollen-amount);
        }
        int next=0;
        foreach(var stage in new[]{CycleStage.Egg,CycleStage.Larva,CycleStage.Pupa})
        {
            int count=stage==CycleStage.Egg?Settings.demoEggs:stage==CycleStage.Larva?Settings.demoLarvae:Settings.demoPupae;
            var progressPatterns=new (int face,double progress)[count];
            for(int i=0;i<count;i++)progressPatterns[i]=(brood[next+i].face,new[]{0.0,.25,.5,.75,.95}[(i/Math.Max(1,faceIndex))%5]);
            ShuffleInitialPatterns(progressPatterns,c=>c.face,visualRandom);
            for(int i=0;i<count;i++)
            {
                var c=brood[next++];var b=Add(c.region.position,c.region.box);b.stage=stage;b.development=stage==CycleStage.Egg?0:stage==CycleStage.Larva?Settings.eggEnd:Settings.larvaEnd;
                // Each face keeps the same age distribution in a mixed order.
                double progress=progressPatterns[i].progress;
                if(stage==CycleStage.Larva)b.development=Settings.eggEnd+progress*(Settings.larvaEnd-Settings.eggEnd);
                else if(stage==CycleStage.Pupa)b.development=Settings.larvaEnd+progress*(Settings.adultStart-Settings.larvaEnd);
                b.reserve=0;b.broodColumn=c.x;b.broodRow=c.y;
                if(!c.region.surface.TrySetBrood(c.x,c.y,b.id))throw new InvalidOperationException("Initial brood placement failed.");
            }
        }
    }
}
