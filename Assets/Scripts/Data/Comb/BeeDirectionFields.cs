using System;
using System.Collections.Generic;
using System.Linq;

public sealed partial class BeeCycleRuntime
{
    sealed class DirectionGraph
    {
        internal readonly Dictionary<BeeGridPosition,int> Ids=new Dictionary<BeeGridPosition,int>();
        internal readonly List<BeeGridPosition> Points=new List<BeeGridPosition>();
        internal List<(int node,bool queen)>[] Forward,Reverse;
    }
    sealed class DirectionField
    {
        internal DirectionGraph Graph;
        internal int[] Next;
        internal long Bytes=>(long)Next.Length*4;
    }
    readonly Dictionary<int,DirectionGraph> directionGraphs=new Dictionary<int,DirectionGraph>();
    readonly Dictionary<(BeeGridPosition,bool),DirectionField> directionFields=new Dictionary<(BeeGridPosition,bool),DirectionField>();
    readonly LinkedList<(BeeGridPosition,bool)> directionOrder=new LinkedList<(BeeGridPosition,bool)>();
    readonly Dictionary<(BeeGridPosition,bool),LinkedListNode<(BeeGridPosition,bool)>> directionNodes=new Dictionary<(BeeGridPosition,bool),LinkedListNode<(BeeGridPosition,bool)>>();
    readonly HashSet<BeeGridPosition> sharedDestinations=new HashSet<BeeGridPosition>();
    long directionBytes;
    int directionVersion=-1;
    struct DirectedMovement { internal BeeGridPosition Position;internal CyclePoint Waypoint;internal double Remaining,Progress;internal bool HasWaypoint,Active; }
    readonly Dictionary<(BeeGridPosition,BeeGridPosition,bool,long,long),DirectedMovement> directedMovements=new Dictionary<(BeeGridPosition,BeeGridPosition,bool,long,long),DirectedMovement>();
    void ClearDirections(){directedMovements.Clear();sharedDestinations.Clear();directionGraphs.Clear();directionFields.Clear();directionOrder.Clear();directionNodes.Clear();directionBytes=0;directionVersion=-1;}
    bool WantsDirections(BeeGridPosition target,bool queen)
    {
        EnsureDirectionRegistry();
        // Admission depends only on topology/settings, never warm-cache history or bee order.
        // Solitary queens and arbitrary relocation surfaces retain source-rooted route reuse.
        return !queen&&sharedDestinations.Contains(target);
    }
    void EnsureDirectionRegistry()
    {
        var topology=World.BeeLocations.Topology(World);
        if(directionVersion!=topology.Builds){
            ClearDirections();directionVersion=topology.Builds;
            foreach(var p in topology.Positions){
                int group=topology.SupportCell(p).Group;
                if(!directionGraphs.TryGetValue(group,out var graph))directionGraphs.Add(group,graph=new DirectionGraph());
                graph.Ids.Add(p,graph.Points.Count);graph.Points.Add(p);
            }
            var candidates=new Dictionary<int,List<BeeGridPosition>>();var seen=new HashSet<BeeGridPosition>();
            void Add(BeeGridPosition p){if(!seen.Add(p))return;int group=topology.SupportCell(p).Group;if(!candidates.TryGetValue(group,out var list))candidates.Add(group,list=new List<BeeGridPosition>());list.Add(p);}
            foreach(var p in entrances.Values.OrderBy(p=>p.EquipmentId).ThenBy(p=>p.X).ThenBy(p=>p.Y).ThenBy(p=>p.Z).ThenBy(p=>p.Face))Add(p);
            foreach(var r in regions)Add(r.position);
            int groups=Math.Max(1,candidates.Count),limit=(int)Math.Min(4096L,Math.Max(1L,(long)Settings.routeCacheEntries*32));
            foreach(var pair in candidates){
                int count=directionGraphs[pair.Key].Points.Count;
                int quota=(int)Math.Min(limit/groups,RouteByteBudget/groups/Math.Max(1L,(long)count*4));
                for(int i=0;i<Math.Min(quota,pair.Value.Count);i++)sharedDestinations.Add(pair.Value[i]);
            }
        }
    }
    DirectionField DirectionTo(BeeGridPosition target,bool queen)
    {
        EnsureDirectionRegistry();var topology=World.BeeLocations.Topology(World);
        var key=(target,queen);
        if(directionFields.TryGetValue(key,out var field)){
            var node=directionNodes[key];directionOrder.Remove(node);directionOrder.AddFirst(node);return field;
        }
        int targetGroup=topology.SupportCell(target).Group;
        if(!directionGraphs.TryGetValue(targetGroup,out var g)||!g.Ids.TryGetValue(target,out int goal))return null;
        int count=g.Points.Count;
        if(g.Forward==null){
            g.Forward=new List<(int,bool)>[count];g.Reverse=new List<(int,bool)>[count];
            for(int i=0;i<count;i++){g.Forward[i]=new List<(int,bool)>();g.Reverse[i]=new List<(int,bool)>();}
            for(int i=0;i<count;i++)foreach(var move in World.BeeLocations.GetNeighbours(World,g.Points[i])){
                if(!AllowedSurfaceEdge(g.Points[i],move.Target))continue;
                if(!g.Ids.TryGetValue(move.Target,out int next))throw new InvalidOperationException("Movement edge crosses direction domain.");
                g.Forward[i].Add((next,move.AllowsQueen));g.Reverse[next].Add((i,move.AllowsQueen));
            }
        }
        var hops=new int[count];var queue=new int[count];var nexts=new int[count];
        for(int i=0;i<count;i++){hops[i]=-1;nexts[i]=-1;}
        int head=0,tail=0;queue[tail++]=goal;hops[goal]=0;nexts[goal]=goal;
        while(head<tail){int p=queue[head++];foreach(var edge in g.Reverse[p]){
            if((queen&&!edge.queen)||hops[edge.node]>=0)continue;
            hops[edge.node]=hops[p]+1;queue[tail++]=edge.node;
        }}
        // Original neighbor order resolves equal-hop paths. Never reverse a directed edge.
        for(int i=0;i<count;i++)if(hops[i]>0)foreach(var edge in g.Forward[i])
            if((!queen||edge.queen)&&hops[edge.node]==hops[i]-1){nexts[i]=edge.node;break;}
        field=new DirectionField{Graph=g,Next=nexts};
        int limit=(int)Math.Min(4096L,Math.Max(1L,(long)Settings.routeCacheEntries*32));
        while(directionFields.Count>0&&(directionFields.Count>=limit||directionBytes+field.Bytes>RouteByteBudget)){
            var old=directionOrder.Last.Value;directionBytes-=directionFields[old].Bytes;directionOrder.RemoveLast();directionNodes.Remove(old);directionFields.Remove(old);
        }
        directionFields.Add(key,field);directionNodes.Add(key,directionOrder.AddFirst(key));directionBytes+=field.Bytes;return field;
    }
    bool DirectionStep(DirectionField field,BeeGridPosition here,out BeeGridPosition next)
    {
        next=default;if(field==null||!field.Graph.Ids.TryGetValue(here,out int id)||field.Next[id]<0)return false;
        next=field.Graph.Points[field.Next[id]];return true;
    }
    bool MoveDirected(CycleBee bee,BeeGridPosition target,ref double time)
    {
        if(!BeePosition(bee,out var here)){End(bee,CycleEnd.PathBlocked);return false;}
        if(here.Equals(target)){bee.directionActive=false;bee.hasWaypoint=false;bee.progress=0;return true;}
        if(!bee.directionActive||!bee.directionTarget.Grid.Equals(target)){
            bee.directionActive=true;bee.directionTarget=new CyclePoint(target);bee.hasWaypoint=false;bee.progress=0;
        }
        double original=time,speed=FUnit("moveDistance");bool advanced=false;
        if(speed<=0){End(bee,CycleEnd.PathBlocked);return false;}
        bool reusable=!bee.hasWaypoint&&bee.progress==0;
        var movementKey=(here,target,bee.queen,BitConverter.DoubleToInt64Bits(speed),BitConverter.DoubleToInt64Bits(time));
        if(reusable&&directedMovements.TryGetValue(movementKey,out var cached)){
            if(!here.Equals(cached.Position)){World.BeeLocations.Set(World,bee.id,cached.Position);InvalidateBeePosition(bee);}
            bee.waypoint=cached.Waypoint;bee.hasWaypoint=cached.HasWaypoint;bee.progress=cached.Progress;bee.directionActive=cached.Active;time=cached.Remaining;
            double spent=original-time;bee.energy=Math.Max(0,bee.energy-F("movementEnergyCost",new CycleInputs{time=spent}));Act(bee,CycleAction.Move,spent);
            return cached.Position.Equals(target)&&time>1e-12;
        }
        var field=DirectionTo(target,bee.queen);
        while(time>1e-12&&!here.Equals(target)){
            if(!bee.hasWaypoint){
                if(!DirectionStep(field,here,out var next)){
                    if(advanced){World.BeeLocations.Set(World,bee.id,here);InvalidateBeePosition(bee);}
                    End(bee,CycleEnd.PathBlocked);return false;
                }
                bee.waypoint=new CyclePoint(next);bee.hasWaypoint=true;
            }
            var edge=MovementGeometry(here,bee.waypoint.Grid);
            if(!(bee.queen?edge.Queen:edge.Worker)){
                if(advanced){World.BeeLocations.Set(World,bee.id,here);InvalidateBeePosition(bee);}
                End(bee,CycleEnd.PathBlocked);return false;
            }
            double need=(edge.Distance-bee.progress)/speed;
            if(need>time){bee.progress+=time*speed;time=0;break;}
            time-=need;bee.progress=0;here=bee.waypoint.Grid;bee.hasWaypoint=false;advanced=true;
        }
        if(advanced){World.BeeLocations.Set(World,bee.id,here);InvalidateBeePosition(bee);}
        double used=original-time;bee.energy=Math.Max(0,bee.energy-F("movementEnergyCost",new CycleInputs{time=used}));Act(bee,CycleAction.Move,used);
        if(here.Equals(target))bee.directionActive=false;
        if(reusable&&directedMovements.Count<8192)directedMovements[movementKey]=new DirectedMovement{Position=here,Waypoint=bee.waypoint,Remaining=time,Progress=bee.progress,HasWaypoint=bee.hasWaypoint,Active=bee.directionActive};
        return here.Equals(target)&&time>1e-12;
    }
}
