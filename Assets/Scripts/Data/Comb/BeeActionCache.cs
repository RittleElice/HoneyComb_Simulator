using System.Collections.Generic;
public sealed partial class BeeCycleRuntime
{
    // Derived caches only; neither save layout nor individual path ownership changes.
    readonly Dictionary<(BeeGridPosition,BeeGridPosition,bool),CyclePoint[]> pathTemplates=new Dictionary<(BeeGridPosition,BeeGridPosition,bool),CyclePoint[]>();
    readonly Dictionary<(BeeGridPosition,BeeGridPosition),double> edgeLengths=new Dictionary<(BeeGridPosition,BeeGridPosition),double>();
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
    void ClearActionGeometry(){pathTemplates.Clear();edgeLengths.Clear();cachedPathPoints=0;positionKnown=airKnown=false;}
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
}
