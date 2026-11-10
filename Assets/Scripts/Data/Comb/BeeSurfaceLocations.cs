using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

// Coordinates are millimetres from the frame's local lower X/Z corner.
// Front and Back share the same local axes; flipping the presentation does not move bees.
public readonly struct BeeSurfacePosition
{
    public readonly int FrameId;
    public readonly FrameSide Side;
    public readonly double Xmm, Zmm;
    public BeeSurfacePosition(int frameId, FrameSide side, double xmm, double zmm)
    {
        if ((side != FrameSide.Front && side != FrameSide.Back) || double.IsNaN(xmm) || double.IsInfinity(xmm) ||
            double.IsNaN(zmm) || double.IsInfinity(zmm) || xmm < 0 || zmm < 0)
            throw new ArgumentException("Invalid bee surface position.");
        FrameId=frameId;Side=side;Xmm=xmm;Zmm=zmm;
    }
}

public sealed class BeeSurfaceLocations
{
    readonly SortedDictionary<long,BeeSurfacePosition> positions=new SortedDictionary<long,BeeSurfacePosition>();
    public int Count=>positions.Count;
    public IEnumerable<KeyValuePair<long,BeeSurfacePosition>> Entries=>positions.Select(p=>p);
    public bool TryGet(long beeId,out BeeSurfacePosition position)=>positions.TryGetValue(beeId,out position);
    public bool Remove(long beeId)=>positions.Remove(beeId);
    public bool HasFrame(int frameId)=>positions.Values.Any(p=>p.FrameId==frameId);
    public void Set(GameWorld world,long beeId,BeeSurfacePosition position)
    {
        if(beeId<=0)throw new ArgumentOutOfRangeException(nameof(beeId));
        Resolve(world,position);positions[beeId]=position;
    }
    internal static ItemData Resolve(GameWorld world,BeeSurfacePosition p)
    {
        if(world==null)throw new ArgumentNullException(nameof(world));
        ItemData frame=null;
        foreach(var item in ThermalWorld.Items(world))if(item.Id==p.FrameId){
            if(frame!=null)throw new InvalidOperationException("Ambiguous frame ID.");frame=item;
        }
        if(frame==null||frame.Kind!=ItemKind.Frame||frame.GetContent<FrameData>()==null)throw new InvalidOperationException("Bee frame is missing.");
        var g=frame.EquipmentGrid;
        if((p.Side!=FrameSide.Front&&p.Side!=FrameSide.Back)||double.IsNaN(p.Xmm)||double.IsNaN(p.Zmm)||p.Xmm<0||p.Zmm<0||p.Xmm>=g.X[g.SizeX]-g.X[0]||p.Zmm>=g.Z[g.SizeZ]-g.Z[0])
            throw new ArgumentOutOfRangeException(nameof(p),"Position is outside frame surface.");
        return frame;
    }
    internal void Write(BinaryWriter writer,GameWorld world)
    {
        writer.Write(positions.Count);
        foreach(var pair in positions){Resolve(world,pair.Value);writer.Write(pair.Key);writer.Write(pair.Value.FrameId);writer.Write((int)pair.Value.Side);writer.Write(pair.Value.Xmm);writer.Write(pair.Value.Zmm);}
    }
    internal void Read(BinaryReader reader,GameWorld world)
    {
        int count=SaveBinary.Count(reader,10000000);
        for(int i=0;i<count;i++){
            long id=reader.ReadInt64();var p=new BeeSurfacePosition(reader.ReadInt32(),(FrameSide)reader.ReadInt32(),reader.ReadDouble(),reader.ReadDouble());
            if(positions.ContainsKey(id))throw new InvalidDataException("Duplicate bee ID.");
            try{Set(world,id,p);}catch(ArgumentException ex){throw new InvalidDataException("Invalid bee location.",ex);}catch(InvalidOperationException ex){throw new InvalidDataException("Missing bee frame.",ex);}
        }
    }
}

public enum BeeSurfaceEnvironmentKind { Unavailable, AdjacentAir, Exterior }
public readonly struct BeeSurfaceEnvironment
{
    public readonly BeeSurfaceEnvironmentKind Kind;
    public readonly double TemperatureC,RelativeHumidityPercent;
    public readonly int BoxId,CellIndex;
    public BeeSurfaceEnvironment(BeeSurfaceEnvironmentKind kind,double temperature,double humidity,int boxId=0,int cellIndex=-1)
    {Kind=kind;TemperatureC=temperature;RelativeHumidityPercent=humidity;BoxId=boxId;CellIndex=cellIndex;}
}

public static class BeeSurfaceEnvironmentReader
{
    public static bool TryResolveAir(GameWorld world,BeeSurfacePosition position,out HiveSpatialGrid grid,out int index)
    {
        var frame=BeeSurfaceLocations.Resolve(world,position);grid=null;index=-1;
        foreach(var box in ThermalWorld.Items(world)){
            if(box.Kind!=ItemKind.BroodBox&&box.Kind!=ItemKind.Super)continue;
            for(int slot=0;slot<box.InteriorSlots.Capacity;slot++)if(ReferenceEquals(box.InteriorSlots.GetSlot(slot).Item,frame)){
                var g=frame.EquipmentGrid;int x=Axis(g.X,position.Xmm),z=Axis(g.Z,position.Zmm);
                int y=1+slot*HiveGridLayout.SlotDepthCells+g.SlotFirstY+(position.Side==FrameSide.Front?-1:g.SizeY);
                grid=box.SpatialGrid;index=grid.Layout.Index(x+2,y,z+grid.Layout.SlotBottom);
                return grid.GetCell(index).Material==HiveMaterial.Air;
            }
        }
        return false;
    }
    static int Axis(IReadOnlyList<double> bounds,double local)
    {for(int i=0;i<bounds.Count-1;i++)if(local<bounds[i+1]-bounds[0])return i;throw new ArgumentOutOfRangeException(nameof(local));}
    public static bool TryRead(GameWorld world,long beeId,out BeeSurfaceEnvironment environment)
    {
        environment=default;
        if(!world.BeeLocations.TryGet(beeId,out var position))return false;
        try{
            if(!TryResolveAir(world,position,out var grid,out int index)){
                if(grid!=null||world.Weather==null)return false;
                environment=new BeeSurfaceEnvironment(BeeSurfaceEnvironmentKind.Exterior,world.Weather.Current.TemperatureCelsius,world.Weather.Current.RelativeHumidityPercent);return true;
            }
            if(world.Thermal==null)return false;
            var source=grid.GetCell(index).SourceCell;var owner=source?.Equipment.ThermalState??grid.Box.ThermalState;int local=source?.Index??index;
            var values=new VaporPressureCell[owner.Count];var target=new Dictionary<EquipmentThermalState,VaporPressureCell[]>{{owner,values}};
            if(!world.Thermal.TryReadVaporPressures(target))return false;
            var v=values[local];if(!v.Available||double.IsNaN(v.AirPa))return false;
            double saturation=610.94*Math.Exp(17.625*v.TemperatureC/(243.04+v.TemperatureC));
            environment=new BeeSurfaceEnvironment(BeeSurfaceEnvironmentKind.AdjacentAir,v.TemperatureC,100*v.AirPa/saturation,grid.Box.Id,index);return true;
        }catch(InvalidOperationException){return false;}catch(ArgumentException){return false;}
    }
}
