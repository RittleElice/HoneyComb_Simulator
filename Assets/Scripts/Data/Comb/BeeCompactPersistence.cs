using System;
using System.IO;
using System.Collections.Generic;
public sealed partial class BeeCycleRuntime
{
    void WriteCompactBees(BinaryWriter w)=>WriteCompactBeesAsync(w,null).GetAwaiter().GetResult();
    async System.Threading.Tasks.Task WriteCompactBeesAsync(BinaryWriter w,Func<System.Threading.Tasks.Task> checkpoint)
    {
        var ids=new Dictionary<CycleDesireState,int>();var states=new List<CycleDesireState>();
        CycleDesireState State(CycleBee b)=>b.mutableDesires==null?(b.desireState??CycleDesireState.Empty):CycleDesireState.From(b.mutableDesires);
        int visited=0;foreach(var bee in Bees){if(checkpoint!=null&&(visited++&255)==0)await checkpoint();var state=State(bee);if(!ids.ContainsKey(state)){ids.Add(state,states.Count);states.Add(state);}}
        w.Write(states.Count);
        foreach(var state in states){w.Write(state.Values.Length);foreach(var d in state.Values)CycleBinary.Write(w,typeof(CycleDesire),new CycleDesire{task=d.task,target=d.target,weight=d.weight});}
        w.Write(Bees.Count);visited=0;foreach(var bee in Bees){if(checkpoint!=null&&(visited++&255)==0)await checkpoint();CycleBinary.WriteCompactBee(w,bee,ids[State(bee)]);}
    }
    void ReadCompactBees(BinaryReader r,bool validateOnly)
    {
        int count=SaveBinary.Count(r,1000000);var states=new CycleDesireState[count];
        for(int i=0;i<count;i++){int length=SaveBinary.Count(r,1000000);var values=new CycleDesireState.Entry[length];for(int j=0;j<length;j++){var d=(CycleDesire)CycleBinary.Read(r,typeof(CycleDesire),3);values[j]=new CycleDesireState.Entry(d.task,d.target,d.weight);}states[i]=InternDesires(new CycleDesireState(values));}
        count=SaveBinary.Count(r,10000000);Bees=new List<CycleBee>(validateOnly?0:count);var seen=new HashSet<long>();
        for(int i=0;i<count;i++){
            var b=CycleBinary.ReadCompactBee(r,states);
            if(b.forageSource < -1 || b.forageSource >= (World.Weather?.Nectar?.Sources.Length??0))throw new InvalidDataException("Invalid forage source.");
            if(b.id<=0||!seen.Add(b.id)||b.reserve<0||b.energy<0||b.health<0||b.development<0||b.cargo.pollen<0||b.pathIndex<0||b.pathIndex>b.path.Count||b.progress<0)throw new InvalidDataException("Invalid bee state.");
            if(b.stage!=CycleStage.Dead&&!b.outside&&!World.BeeLocations.TryGet(b.id,out _))throw new InvalidDataException("Missing bee location.");
            foreach(var sugar in b.cargo.sugars)if(sugar.mass<0||sugar.source<0)throw new InvalidDataException("Invalid cargo.");
            nextId=Math.Max(nextId,b.id+1);if(!validateOnly)Bees.Add(b);
        }
    }
}
