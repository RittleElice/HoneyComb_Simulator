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
    // Only persistence deduplicates immutable snapshots. Runtime desires belong to each bee.
    readonly Dictionary<CycleDesireState,CycleDesireState> desireStates=new Dictionary<CycleDesireState,CycleDesireState>();
    double DesireTotal(CycleBee bee,double initial)
    {
        for(int i=0;i<bee.DesireCount;i++){var d=bee.DesireAt(i);initial+=ForageChoiceWeight(bee,d.task,d.weight);}
        return initial;
    }
    void BeginDesireTick(){desireStates.Clear();}
    CycleDesireState InternDesires(CycleDesireState state)
    {
        if(desireStates.TryGetValue(state,out var prior))return prior;
        desireStates.Add(state,state);return state;
    }
    void UpdateDesires(CycleBee b)
    {
        // Materialize a private list once after loading; never change another bee's snapshot.
        if(b.mutableDesires==null){var own=b.desires;}
        UpdateMutableDesires(b);
    }
}
