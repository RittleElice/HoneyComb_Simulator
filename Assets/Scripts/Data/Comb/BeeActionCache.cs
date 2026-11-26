using System.Collections.Generic;
public sealed partial class BeeCycleRuntime
{
    // Derived caches only; neither save layout nor individual path ownership changes.
    readonly Dictionary<(BeeGridPosition,BeeGridPosition,bool),CyclePoint[]> pathTemplates=new Dictionary<(BeeGridPosition,BeeGridPosition,bool),CyclePoint[]>();
    readonly Dictionary<(BeeGridPosition,BeeGridPosition),double> edgeLengths=new Dictionary<(BeeGridPosition,BeeGridPosition),double>();
    // Keep the original reverse-path summation result, never an algebraic shortcut.
    readonly Dictionary<(BeeGridPosition,BeeGridPosition,bool),double> routeDistances=new Dictionary<(BeeGridPosition,BeeGridPosition,bool),double>();
    double RouteDistance(BeeGridPosition start,BeeGridPosition target,bool queen)
    {
        var key=(start,target,queen);
        if(routeDistances.TryGetValue(key,out double distance))return distance;
        distance=RouteDistanceUncached(start,target,queen);
        if(routeDistances.Count<65536)routeDistances.Add(key,distance);
        return distance;
    }
    int cachedPathPoints;
    CycleBee activePositionBee;
    bool positionKnown,positionFound,airKnown;
    BeeGridPosition cachedPosition,cachedAirPosition;
    BeeGridTopology.Cell cachedAir;
    void BeginBeePosition(CycleBee bee){activePositionBee=bee;positionKnown=airKnown=false;}
    void InvalidateBeePosition(CycleBee bee){if(ReferenceEquals(bee,activePositionBee))positionKnown=airKnown=false;}
    bool BeePosition(CycleBee bee,out BeeGridPosition position)
    {
        if(!ReferenceEquals(bee,activePositionBee))return World.BeeLocations.TryGet(bee.id,out position);
        if(!positionKnown){positionFound=World.BeeLocations.TryGet(bee.id,out cachedPosition);positionKnown=true;}
        position=cachedPosition;return positionFound;
    }
    void ClearActionGeometry(){ClearDirections();freshMovements.Clear();movementGeometry.Clear();storageChoices.Clear();routeDistances.Clear();pathTemplates.Clear();edgeLengths.Clear();cachedPathPoints=0;positionKnown=airKnown=false;}
    List<CyclePoint> FindPath(BeeGridPosition start,BeeGridPosition target,bool queen)
    {
        var key=(start,target,queen);if(pathTemplates.TryGetValue(key,out var template))return new List<CyclePoint>(template);
        var result=FindPathUncached(start,target,queen);
        if(result!=null&&pathTemplates.Count<4096&&cachedPathPoints+result.Count<=65536){pathTemplates.Add(key,result.ToArray());cachedPathPoints+=result.Count;}
        return result;
    }
    double Distance(BeeGridPosition a,BeeGridPosition b)
    {
        var key=(a,b);if(edgeLengths.TryGetValue(key,out var distance))return distance;
        distance=DistanceUncached(a,b);if(edgeLengths.Count<16384)edgeLengths.Add(key,distance);return distance;
    }

    struct StorageChoice
    {
        internal Region Region;
        internal bool Ready;
        internal double Distance,Weight;
    }
    // Only derived per-tick data. Each bee still visits eligible candidates and draws its own RNG.
    readonly Dictionary<(BeeGridPosition,bool,bool,bool),StorageChoice[]> storageChoices=new Dictionary<(BeeGridPosition,bool,bool,bool),StorageChoice[]>();
    bool TrySelectStorageRegionCached(CycleBee bee,BeeGridPosition here,int group,List<Region> candidates,out Region selected)
    {
        selected=null;
        bool sugar=bee.cargo.Sugar>0,pollen=bee.cargo.pollen>0;
        var key=(here,bee.queen,sugar,pollen);
        if(!storageChoices.TryGetValue(key,out var choices))
        {
            if(storageChoices.Count>=4096)return false;
            var eligible=new List<StorageChoice>();
            foreach(var r in candidates)
                if(r.group==group&&!r.clearing&&((sugar&&r.freeSugar>0)||(pollen&&r.freePollen>0)))
                    eligible.Add(new StorageChoice{Region=r});
            choices=eligible.ToArray();storageChoices.Add(key,choices);
        }
        double total=0;
        for(int i=0;i<choices.Length;i++)
        {
            var entry=choices[i];var r=entry.Region;
            if(bee.hasStoreExclusion&&r.position.Equals(bee.storeExclusion.Grid))continue;
            // Lazy evaluation preserves the original error path for excluded/unreachable candidates.
            if(!entry.Ready)
            {
                entry.Distance=RouteDistance(here,r.position,bee.queen);
                if(!double.IsPositiveInfinity(entry.Distance))
                    entry.Weight=F("storagePreference",new CycleInputs{available=(sugar?r.freeSugar:0)+(pollen?r.freePollen:0),height=r.height/Settings.storageHeightScale,distance=entry.Distance});
                entry.Ready=true;choices[i]=entry;
            }
            if(double.IsPositiveInfinity(entry.Distance))continue;
            total+=entry.Weight;
            if(Random()*total<entry.Weight)selected=r;
        }
        return true;
    }

    struct MovementEdge
    {
        internal double Distance;
        internal bool Worker,Queen;
    }
    readonly Dictionary<(BeeGridPosition,BeeGridPosition),MovementEdge> movementGeometry=new Dictionary<(BeeGridPosition,BeeGridPosition),MovementEdge>();
    MovementEdge MovementGeometry(BeeGridPosition start,BeeGridPosition target)
    {
        var key=(start,target);
        MovementEdge edge=default;
        if(forageSelectionActive&&movementGeometry.TryGetValue(key,out edge))return edge;
        edge.Distance=Distance(start,target);
        var neighbours=World.BeeLocations.GetNeighbours(World,start);
        for(int i=0;i<neighbours.Count;i++)
        {
            var move=neighbours[i];if(!move.Target.Equals(target))continue;
            if(!AllowedSurfaceEdge(start,target))continue;edge.Worker=true;edge.Queen|=move.AllowsQueen;
        }
        if(forageSelectionActive&&movementGeometry.Count<65536)movementGeometry.Add(key,edge);
        return edge;
    }

    // Reuse only results of the original step-by-step loop for a fresh immutable path template.
    // Each bee retains its own mutable path, and location/energy/action commits still run individually.
    struct FreshMovement
    {
        internal BeeGridPosition Position;
        internal double Remaining,Progress;
        internal int Index;
        internal bool Advanced;
    }
    readonly Dictionary<(CyclePoint[],long,long),FreshMovement> freshMovements=new Dictionary<(CyclePoint[],long,long),FreshMovement>();
}
