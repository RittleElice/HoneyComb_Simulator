public sealed partial class BeeCycleRuntime
{
    // Read-only, indexed lookup for the currently observed comb. No whole-colony scan.
    public bool TryGetObservationDetails(long id,out CycleStage stage,out double development,out double health)
    {stage=default;development=health=0;if(!byId.TryGetValue(id,out var bee))return false;stage=bee.stage;development=bee.development;health=bee.health;return true;}
    public bool TryGetObservationStage(long beeId, out CycleStage stage)
    {
        if (byId.TryGetValue(beeId, out var bee)) { stage = bee.stage; return true; }
        stage = default; return false;
    }
    // Progress is local to the current brood stage, using the simulation's thresholds.
    public bool TryGetObservationDevelopment(long beeId, out CycleStage stage, out double progress)
    {
        progress=0;stage=default;
        if(!byId.TryGetValue(beeId,out var bee))return false;
        stage=bee.stage;
        double start,end;
        if(stage==CycleStage.Larva){start=Settings.eggEnd;end=Settings.larvaEnd;}
        else if(stage==CycleStage.Pupa){start=Settings.larvaEnd;end=Settings.adultStart;}
        else return true;
        progress=System.Math.Max(0,System.Math.Min(1,(bee.development-start)/(end-start)));
        return true;
    }

}
