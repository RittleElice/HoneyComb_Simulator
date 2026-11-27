using System;
public sealed partial class BeeCycleRuntime
{
    static bool IsWorkTask(CycleTask task)=>task==CycleTask.Build||task==CycleTask.Nurse||task==CycleTask.Heat||task==CycleTask.Clear||task==CycleTask.Lay||task==CycleTask.ForageNectar||task==CycleTask.ForagePollen;
    bool BuildWorkAvailable(Region r){Scan(r);return Settings.workDesireEnabled!=0?r.finishable>0:r.unfinished>0;}
    bool WorkAvailable(CycleBee bee,CycleTask task,BeeGridPosition target)
    {
        if(!IsWorkTask(task))return true;
        if(task==CycleTask.ForageNectar||task==CycleTask.ForagePollen){
            // A temporary empty source is handled by the existing search/return rules.
            return CargoFree(bee,task==CycleTask.ForagePollen)>1e-8;
        }
        if(!regionAt.TryGetValue(target,out var r))return false;
        if(task==CycleTask.Build)return BuildWorkAvailable(r);
        if(task==CycleTask.Heat)return r.brood.Count>0&&r.temperature<Settings.broodIdealTemperature;
        if(task==CycleTask.Nurse){foreach(var b in r.brood)if(b.stage==CycleStage.Larva&&
            (b.broodSugar+1e-9<Settings.larvaSugarPerTick*RepresentedBees(b)*Settings.broodReserveTicks||b.broodPollen+1e-9<Settings.larvaPollenPerTick*RepresentedBees(b)*Settings.broodReserveTicks))return true;return false;}
        Scan(r);
        if(task==CycleTask.Lay)return r.empty>0;
        return r.clearing&&r.sugar+r.pollen>1e-9;
    }
    void PruneWorkDesires(CycleBee bee)
    {
        if(Settings.workDesireEnabled==0)return;
        var list=bee.desires;bool removed=false;
        for(int i=list.Count-1;i>=0;i--){var d=list[i];if(d.weight<=0||!WorkAvailable(bee,d.task,d.target.Grid)){list.RemoveAt(i);removed=true;}}
        if(removed)bee.desireIndex=null;
    }
    bool HasCurrentWorkDesire(CycleBee bee)
    {
        if(!IsWorkTask(bee.task))return true;
        for(int i=0;i<bee.DesireCount;i++){var d=bee.DesireAt(i);if(d.task==bee.task&&d.target.Grid.Equals(bee.target.Grid)&&d.weight>0)return true;}
        return false;
    }
    void EndExhaustedWork(CycleBee bee)
    {
        if(Settings.workDesireEnabled==0||!IsWorkTask(bee.task)||HasCurrentWorkDesire(bee))return;
        // Never cancel a physical return flight or teleport a bee inside.
        if(bee.apiaryFlight!=null)return;
        if(bee.outside){FinishForage(bee,CycleEnd.Completed);return;}
        End(bee,CycleEnd.Completed);
    }
    void RelieveWork(CycleBee bee,CycleTask task,double units,double coefficient)
    {
        if(Settings.workDesireEnabled==0||units<=0||bee.task!=task)return;
        var list=bee.desires;
        for(int i=0;i<list.Count;i++){var d=list[i];if(d.task!=task||!d.target.Grid.Equals(bee.target.Grid))continue;
            d.weight=Math.Max(0,d.weight-F("workDesireRelief",new CycleInputs{ratio=units,actionRate=coefficient}));
            if(d.weight==0){list.RemoveAt(i);bee.desireIndex=null;}return;}
    }
}
