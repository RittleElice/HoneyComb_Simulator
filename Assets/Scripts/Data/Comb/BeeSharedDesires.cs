using System;
using System.Collections.Generic;

// Immutable runtime values. Equal vectors can share storage without sharing mutable bee state.
internal sealed class CycleDesireState : IEquatable<CycleDesireState>
{
    internal readonly struct Entry
    {
        internal readonly CycleTask task; internal readonly CyclePoint target; internal readonly double weight;
        internal Entry(CycleTask task,CyclePoint target,double weight){this.task=task;this.target=target;this.weight=weight;}
    }
    internal static readonly CycleDesireState Empty=new CycleDesireState(Array.Empty<Entry>());
    internal readonly Entry[] Values;
    readonly int hash;
    internal CycleDesireState(Entry[] values)
    {
        Values=values;unchecked{int h=17;foreach(var v in values){h=h*31+(int)v.task;h=h*31+v.target.equipment;h=h*31+v.target.x;h=h*31+v.target.y;h=h*31+v.target.z;h=h*31+v.target.face;long bits=BitConverter.DoubleToInt64Bits(v.weight);h=h*31+(int)bits;h=h*31+(int)(bits>>32);}hash=h;}
    }
    public override int GetHashCode()=>hash;
    public override bool Equals(object obj)=>obj is CycleDesireState s&&Equals(s);
    public bool Equals(CycleDesireState s)
    {
        if(ReferenceEquals(this,s))return true;if(s==null||hash!=s.hash||Values.Length!=s.Values.Length)return false;
        for(int i=0;i<Values.Length;i++){var a=Values[i];var b=s.Values[i];if(a.task!=b.task||a.target.equipment!=b.target.equipment||a.target.x!=b.target.x||a.target.y!=b.target.y||a.target.z!=b.target.z||a.target.face!=b.target.face||BitConverter.DoubleToInt64Bits(a.weight)!=BitConverter.DoubleToInt64Bits(b.weight))return false;}return true;
    }
    internal List<CycleDesire> Materialize(){var result=new List<CycleDesire>(Values.Length);foreach(var v in Values)result.Add(new CycleDesire{task=v.task,target=v.target,weight=v.weight});return result;}
    internal static CycleDesireState From(List<CycleDesire> list){if(list.Count==0)return Empty;var values=new Entry[list.Count];for(int i=0;i<values.Length;i++){var d=list[i];values[i]=new Entry(d.task,d.target,d.weight);}return new CycleDesireState(values);}
}
public sealed partial class BeeCycleRuntime
{
    sealed class DesireContext { internal readonly List<IndexedCommand> commands=new List<IndexedCommand>(); }
    readonly Dictionary<(int,bool,BeeGridPosition),DesireContext> desireContexts=new Dictionary<(int,bool,BeeGridPosition),DesireContext>();
    readonly Dictionary<(CycleDesireState,DesireContext),CycleDesireState> desireTransitions=new Dictionary<(CycleDesireState,DesireContext),CycleDesireState>();
    readonly Dictionary<CycleDesireState,CycleDesireState> desireStates=new Dictionary<CycleDesireState,CycleDesireState>();
    readonly Dictionary<(int,bool),List<IndexedCommand>> groupCommands=new Dictionary<(int,bool),List<IndexedCommand>>();
    readonly HashSet<int> localCommandGroups=new HashSet<int>();
    readonly Dictionary<(CycleDesireState,long),double> desireTotals=new Dictionary<(CycleDesireState,long),double>();
    double DesireTotal(CycleBee bee,double initial){if(Settings.forageLoadBased!=0){for(int i=0;i<bee.DesireCount;i++){var d=bee.DesireAt(i);initial+=ForageChoiceWeight(bee,d.task,d.weight);}return initial;}if(bee.mutableDesires!=null){for(int i=0;i<bee.DesireCount;i++)initial+=bee.DesireAt(i).weight;return initial;}var state=bee.desireState??CycleDesireState.Empty;var key=(state,BitConverter.DoubleToInt64Bits(initial));if(desireTotals.TryGetValue(key,out double result))return result;result=initial;foreach(var d in state.Values)result+=d.weight;if(desireTotals.Count<8192)desireTotals.Add(key,result);return result;}
    void BeginDesireTick(){desireTotals.Clear();desireContexts.Clear();desireTransitions.Clear();desireStates.Clear();localCommandGroups.Clear();groupCommands.Clear();foreach(var c in indexedCommands){var command=c.command;if(command.localOnly)localCommandGroups.Add(command.group);var key=(command.group,command.task==CycleTask.Lay);if(!groupCommands.TryGetValue(key,out var list)){list=new List<IndexedCommand>();groupCommands.Add(key,list);}list.Add(c);}}
    CycleDesireState InternDesires(CycleDesireState state){if(desireStates.TryGetValue(state,out var prior))return prior;desireStates.Add(state,state);return state;}
    void UpdateDesires(CycleBee b)
    {
        // A caller that exposes mutable desires gets its own list and retains legacy mutation semantics.
        if(b.mutableDesires!=null){UpdateMutableDesires(b);return;}
        bool located=Position(b,out var p);var air=located?Air(p):null;int group=air==null?int.MinValue:air.Group;
        var key=(group,b.queen,localCommandGroups.Contains(group)?p:default(BeeGridPosition));
        if(!desireContexts.TryGetValue(key,out var context)){
            context=new DesireContext();
            if(air!=null&&groupCommands.TryGetValue((group,b.queen),out var eligible))foreach(var c in eligible){var command=c.command;if(command.group==group&&(!command.localOnly||command.target.Grid.Equals(p))&&(b.queen==(command.task==CycleTask.Lay)))context.commands.Add(c);}
            desireContexts.Add(key,context);
        }
        var prior=b.desireState??CycleDesireState.Empty;
        if(!desireTransitions.TryGetValue((prior,context),out var next)){
            var list=prior.Materialize();var lookup=new Dictionary<int,CycleDesire>(list.Count);
            foreach(var d in list){if(Settings.demandEnabled!=0)d.weight=Decay(d.weight);lookup[DesireKey(d.task,d.target.Grid)]=d;}
            foreach(var indexed in context.commands){var c=indexed.command;if(!lookup.TryGetValue(indexed.key,out var d)){d=new CycleDesire{task=c.task,target=c.target};list.Add(d);lookup.Add(indexed.key,d);}double amount=Stimulus(c);if(amount>0){d.weight+=amount;d.stimulatedThisTick=true;}}
            for(int i=list.Count-1;i>=0;i--){var d=list[i];if(Settings.demandEnabled==0&&!d.stimulatedThisTick)d.weight=Decay(d.weight);if(d.weight==0)list.RemoveAt(i);}
            next=InternDesires(CycleDesireState.From(list));desireTransitions.Add((prior,context),next);
        }
        b.desireState=next;
    }
}
