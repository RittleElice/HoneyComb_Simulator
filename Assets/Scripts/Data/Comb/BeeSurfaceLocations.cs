using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

// The only per-bee location data: equipment identity and equipment-local cell.
// Occupancy means attachment to this cell, never residence inside solid material.
public readonly struct BeeGridPosition : IEquatable<BeeGridPosition>
{
    public readonly int EquipmentId, X, Y, Z;
    public BeeGridPosition(int equipmentId, int x, int y, int z)
    {
        if (x < 0 || y < 0 || z < 0) throw new ArgumentOutOfRangeException("Grid coordinate");
        EquipmentId = equipmentId; X = x; Y = y; Z = z;
    }
    public bool Equals(BeeGridPosition p) => EquipmentId == p.EquipmentId && X == p.X && Y == p.Y && Z == p.Z;
    public override bool Equals(object obj) => obj is BeeGridPosition p && Equals(p);
    public override int GetHashCode() { unchecked { return ((EquipmentId * 397 + X) * 397 + Y) * 397 + Z; } }
    public override string ToString() => $"{EquipmentId}:({X},{Y},{Z})";
}

public readonly struct BeeGridMove
{
    public readonly BeeGridPosition Target;
    public readonly bool AllowsQueen;
    public BeeGridMove(BeeGridPosition target, bool allowsQueen) { Target = target; AllowsQueen = allowsQueen; }
}

public sealed class BeeSurfaceLocations
{
    readonly SortedDictionary<long, BeeGridPosition> positions = new SortedDictionary<long, BeeGridPosition>();
    readonly Dictionary<BeeGridPosition, int> counts = new Dictionary<BeeGridPosition, int>();
    BeeGridTopology topology;
    public int Count => positions.Count;
    public int TopologyBuilds => topology?.Builds ?? 0;
    public IEnumerable<KeyValuePair<long, BeeGridPosition>> Entries => positions.Select(p => p);
    public bool TryGet(long id, out BeeGridPosition p) => positions.TryGetValue(id, out p);
    public int CountAt(BeeGridPosition p) => counts.TryGetValue(p, out int n) ? n : 0;
    public bool HasEquipment(int id) => counts.Keys.Any(p => p.EquipmentId == id);
    public bool Remove(long id)
    {
        if (!positions.TryGetValue(id, out var p)) return false;
        positions.Remove(id); Decrement(p); return true;
    }
    void Decrement(BeeGridPosition p) { if (--counts[p] == 0) counts.Remove(p); }
    void Store(long id, BeeGridPosition p)
    {
        if (positions.TryGetValue(id, out var old)) Decrement(old);
        positions[id] = p; counts[p] = CountAt(p) + 1;
    }
    internal BeeGridTopology Topology(GameWorld world)
    {
        if (topology == null) topology = new BeeGridTopology(world);
        if (!ReferenceEquals(topology.World, world)) throw new InvalidOperationException("Locations belong to another world.");
        topology.Ensure(); return topology;
    }
    public void Set(GameWorld world, long id, BeeGridPosition p)
    {
        if (id <= 0) throw new ArgumentOutOfRangeException(nameof(id));
        var t = Topology(world); t.Validate(p);
        if (!t.IsExposed(p)) throw new ArgumentException("Bee cell has no exposed boundary.");
        Store(id, p);
    }
    public IReadOnlyList<BeeGridMove> GetNeighbours(GameWorld world, BeeGridPosition p) => Topology(world).Neighbours(p);
    // Atomic, validated movement step. The behaviour scheduler supplies timing; no speed is invented here.
    public bool TryMove(GameWorld world, long id, BeeGridPosition destination, bool isQueen = false)
    {
        if (!positions.TryGetValue(id, out var start)) return false;
        foreach (var move in GetNeighbours(world, start))
            if (move.Target.Equals(destination) && (!isQueen || move.AllowsQueen)) { Store(id, destination); return true; }
        return false;
    }
    internal void Write(BinaryWriter w, GameWorld world)
    {
        w.Write(positions.Count); if (positions.Count == 0) return;
        var t = Topology(world);
        foreach (var pair in positions)
        {
            t.Validate(pair.Value); w.Write(pair.Key); w.Write(pair.Value.EquipmentId);
            w.Write(pair.Value.X); w.Write(pair.Value.Y); w.Write(pair.Value.Z);
        }
    }
    internal void Read(BinaryReader r, GameWorld world, int schema)
    {
        int count = SaveBinary.Count(r, 10000000); if (count == 0) return;
        var t = Topology(world);
        for (int i = 0; i < count; i++)
        {
            try
            {
                long id = r.ReadInt64(); int equipment = r.ReadInt32(); BeeGridPosition p;
                if (schema == 14)
                {
                    int side = r.ReadInt32(); double x = r.ReadDouble(), z = r.ReadDouble();
                    if (side != (int)FrameSide.Front && side != (int)FrameSide.Back) throw new ArgumentException("Invalid legacy side.");
                    var item = t.Item(equipment);
                    if (item.Kind != ItemKind.Frame) throw new ArgumentException("Legacy position requires a frame.");
                    p = new BeeGridPosition(equipment, Axis(item.EquipmentGrid.X, x), side == (int)FrameSide.Front ? 0 : 1, Axis(item.EquipmentGrid.Z, z));
                }
                else p = new BeeGridPosition(equipment, r.ReadInt32(), r.ReadInt32(), r.ReadInt32());
                if (id <= 0 || positions.ContainsKey(id)) throw new ArgumentException("Invalid or duplicate bee ID.");
                // Keep attachment identity even if later equipment placement occluded its boundary.
                t.Validate(p); Store(id, p);
            }
            catch (ArgumentException ex) { throw new InvalidDataException("Invalid bee grid location.", ex); }
            catch (InvalidOperationException ex) { throw new InvalidDataException("Missing bee equipment.", ex); }
        }
    }
    static int Axis(IReadOnlyList<double> bounds, double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value) || value < 0) throw new ArgumentException("Invalid legacy coordinate.");
        for (int i = 0; i + 1 < bounds.Count; i++) if (value < bounds[i + 1] - bounds[0]) return i;
        throw new ArgumentException("Legacy coordinate outside frame.");
    }
}

// Derived, shared attachment graph. Face directions are construction scratch data,
// not part of a bee's position. Distinct exposed sides of one cell are intentionally collapsed.
internal sealed class BeeGridTopology
{
    internal readonly GameWorld World;
    internal sealed class Cell
    {
        internal int Group, X, Y, Z, Index;
        internal HiveSpatialGrid Grid;
        internal EquipmentSpatialCell Source;
        internal BeeGridPosition Position;
        internal double Dx, Dy, Dz;
        internal bool Solid => Source != null && Source.Material != HiveMaterial.Air;
    }
    internal readonly struct Contact
    {
        internal readonly Cell Air; // null means exterior
        internal readonly double Area;
        internal Contact(Cell air, double area) { Air = air; Area = area; }
    }
    readonly Dictionary<int, ItemData> items = new Dictionary<int, ItemData>();
    readonly Dictionary<(int,int,int,int), Cell> cells = new Dictionary<(int,int,int,int), Cell>();
    readonly Dictionary<BeeGridPosition, Cell> solids = new Dictionary<BeeGridPosition, Cell>();
    readonly Dictionary<BeeGridPosition, List<Contact>> contacts = new Dictionary<BeeGridPosition, List<Contact>>();
    readonly Dictionary<BeeGridPosition, IReadOnlyList<BeeGridMove>> moves = new Dictionary<BeeGridPosition, IReadOnlyList<BeeGridMove>>();
    readonly Dictionary<BeeGridPosition, Dictionary<BeeGridPosition, bool>> building = new Dictionary<BeeGridPosition, Dictionary<BeeGridPosition, bool>>();
    readonly HashSet<ItemSlot> watched = new HashSet<ItemSlot>();
    readonly List<HiveSpatialGrid> grids = new List<HiveSpatialGrid>();
    readonly HashSet<(Cell,Cell)> queenBarriers = new HashSet<(Cell,Cell)>();
    bool dirty = true;
    int width, height, hiveCount;
    internal int Builds { get; private set; }
    internal BeeGridTopology(GameWorld world) { World = world ?? throw new ArgumentNullException(nameof(world)); }
    void Changed(ItemSlot _) { dirty = true; }
    void ChangedGrid(HiveSpatialGrid _) { dirty = true; }
    void Watch(ItemSlot s) { if (s != null && watched.Add(s)) s.OnChanged += Changed; }
    internal void Ensure()
    {
        if (!dirty && width == World.Apiary.Grid.UnlockedWidth && height == World.Apiary.Grid.Height && hiveCount == World.Apiary.Hives.Count) return;
        foreach (var s in watched) s.OnChanged -= Changed;
        foreach (var g in grids) g.Changed -= ChangedGrid;
        watched.Clear(); grids.Clear(); items.Clear(); cells.Clear(); solids.Clear(); contacts.Clear(); moves.Clear(); building.Clear(); queenBarriers.Clear();
        width = World.Apiary.Grid.UnlockedWidth; height = World.Apiary.Grid.Height; hiveCount = World.Apiary.Hives.Count;
        foreach (var h in World.Apiary.Hives) Watch(h.Slot);
        for (int y = 0; y < height; y++) for (int x = 0; x < width; x++) Watch(World.Apiary.GetItemSlot(x,y));
        foreach (var bag in new[] { World.Inventory, World.Warehouse, World.Trash }) for (int i = 0; i < bag.Capacity; i++) Watch(bag.GetSlot(i));
        Watch(World.Hand.Slot);
        var all = ThermalWorld.Items(World).ToArray();
        foreach (var item in all)
        {
            if (items.ContainsKey(item.Id)) throw new InvalidOperationException("Ambiguous equipment ID.");
            items.Add(item.Id, item); Watch(item.TopSlot);
            if (item.InteriorSlots != null) for (int s = 0; s < item.InteriorSlots.Capacity; s++) Watch(item.InteriorSlots.GetSlot(s));
            if (item.SpatialGrid != null) { grids.Add(item.SpatialGrid); item.SpatialGrid.Changed += ChangedGrid; }
        }
        var groups = new Dictionary<object, int>(); int next = 0;
        foreach (var grid in grids)
        {
            object root = grid.Box.Owner is ItemAssemblyData assembly ? (object)assembly : grid.Box;
            if (!groups.TryGetValue(root, out int group)) groups.Add(root, group = next++);
            int offset = 0;
            if (root is ItemAssemblyData stack) foreach (var part in stack.Parts) { if (ReferenceEquals(part, grid.Box)) break; if (part.SpatialGrid != null) offset += part.SpatialGrid.Layout.SizeZ; }
            var l = grid.Layout;
            for (int i = 0; i < grid.CellCount; i++)
            {
                l.Coordinates(i, out int x, out int y, out int z);
                Add(new Cell { Group=group, X=x, Y=y, Z=z+offset, Grid=grid, Index=i, Source=grid.GetCell(i).SourceCell,
                    Dx=l.X[x+1]-l.X[x], Dy=l.Y[y+1]-l.Y[y], Dz=l.Z[z+1]-l.Z[z] });
            }
            foreach (var edge in grid.Edges()) if (edge.AllowsWorkers && !edge.AllowsQueen)
            {
                l.Coordinates(edge.A, out int ax, out int ay, out int az); l.Coordinates(edge.B, out int bx, out int by, out int bz);
                var a=cells[(group,ax,ay,az+offset)]; var b=cells[(group,bx,by,bz+offset)]; queenBarriers.Add((a,b)); queenBarriers.Add((b,a));
            }
        }
        // Detached equipment gets a separate space; unrelated inventory items never touch.
        foreach (var item in all)
        {
            var g = item.EquipmentGrid;
            if (g == null || item.SpatialGrid != null || g.SizeZ == 0 || solids.Keys.Any(p => p.EquipmentId == item.Id)) continue;
            int group=next++;
            for (int z=0;z<g.SizeZ;z++) for(int y=0;y<g.SizeY;y++) for(int x=0;x<g.SizeX;x++)
                Add(new Cell { Group=group,X=x,Y=y,Z=z,Source=g.GetCell(x,y,z),Dx=g.X[x+1]-g.X[x],Dy=g.Y[y+1]-g.Y[y],Dz=g.Z[z+1]-g.Z[z] });
        }
        foreach (var c in solids.Values)
        {
            var list=new List<Contact>(); contacts.Add(c.Position,list); building.Add(c.Position,new Dictionary<BeeGridPosition,bool>());
            for(int d=0;d<6;d++) { var air=Neighbour(c,d); if(air==null || !air.Solid) list.Add(new Contact(air?.Grid==null?null:air,Area(c,d))); }
        }
        foreach (var c in solids.Values)
        {
            if (!IsExposed(c.Position)) continue;
            for(int d=0;d<6;d++)
            {
                var n=Neighbour(c,d); if(n==null || !n.Solid || !IsExposed(n.Position)) continue;
                // Continuous flat boundary, including a convex turn at an outer edge.
                for(int face=0;face<6;face++) if(face/2!=d/2)
                {
                    var a=Neighbour(c,face); var b=Neighbour(n,face);
                    if((a==null || !a.Solid) && (b==null || !b.Solid)) Link(c,n,!queenBarriers.Contains((a,b)));
                }
            }
        }
        // Concave corners: perpendicular solid neighbours around the same air cell.
        foreach(var air in cells.Values) if(!air.Solid)
            for(int a=0;a<6;a++) for(int b=a+1;b<6;b++) if(a/2!=b/2)
            {
                var x=Neighbour(air,a); var y=Neighbour(air,b);
                if(x!=null && y!=null && x.Solid && y.Solid) Link(x,y,true);
            }
        // Only consecutive occupied frame slots. Never bridge an empty slot or a panel.
        foreach(var grid in grids) if(grid.Box.InteriorSlots!=null)
            for(int s=0;s+1<grid.Box.InteriorSlots.Capacity;s++)
            {
                var a=grid.Box.InteriorSlots.GetSlot(s).Item; var b=grid.Box.InteriorSlots.GetSlot(s+1).Item;
                if(a?.Kind!=ItemKind.Frame || b?.Kind!=ItemKind.Frame) continue;
                for(int z=0;z<HiveGridLayout.CombRows;z++) for(int x=0;x<HiveGridLayout.CombColumns;x++)
                {
                    var from=solids[new BeeGridPosition(a.Id,x,1,z)]; var to=solids[new BeeGridPosition(b.Id,x,0,z)];
                    var air1=Neighbour(from,3); var air2=air1==null?null:Neighbour(air1,3);
                    if(air1!=null && air2!=null && !air1.Solid && !air2.Solid && ReferenceEquals(Neighbour(air2,3),to)) Link(from,to,true);
                }
            }
        foreach(var pair in building) moves.Add(pair.Key,Array.AsReadOnly(pair.Value.OrderBy(p=>p.Key.EquipmentId).ThenBy(p=>p.Key.Z).ThenBy(p=>p.Key.Y).ThenBy(p=>p.Key.X).Select(p=>new BeeGridMove(p.Key,p.Value)).ToArray()));
        building.Clear(); dirty=false; Builds++;
    }
    void Add(Cell c)
    {
        cells.Add((c.Group,c.X,c.Y,c.Z),c);
        if(!c.Solid) return;
        var g=c.Source.Equipment.EquipmentGrid; int i=c.Source.Index;
        c.Position=new BeeGridPosition(c.Source.Equipment.Id,i%g.SizeX,i/g.SizeX%g.SizeY,i/(g.SizeX*g.SizeY)); solids.Add(c.Position,c);
    }
    Cell Neighbour(Cell c,int d)
    {
        int x=c.X,y=c.Y,z=c.Z; switch(d){case 0:x--;break;case 1:x++;break;case 2:y--;break;case 3:y++;break;case 4:z--;break;case 5:z++;break;}
        cells.TryGetValue((c.Group,x,y,z),out var n); return n;
    }
    static double Area(Cell c,int d)=>d/2==0?c.Dy*c.Dz:d/2==1?c.Dx*c.Dz:c.Dx*c.Dy;
    void Link(Cell a,Cell b,bool queen)
    {
        if(a.Position.Equals(b.Position)) return;
        building[a.Position][b.Position]=queen || (building[a.Position].TryGetValue(b.Position,out bool old) && old);
        building[b.Position][a.Position]=building[a.Position][b.Position];
    }
    internal ItemData Item(int id) { if(!items.TryGetValue(id,out var item)) throw new InvalidOperationException("Equipment missing."); return item; }
    internal void Validate(BeeGridPosition p) { if(!solids.ContainsKey(p)) throw new ArgumentException("Location must identify an equipment-owned solid grid cell."); }
    internal bool IsExposed(BeeGridPosition p)=>contacts.TryGetValue(p,out var c) && c.Count>0;
    internal IReadOnlyList<BeeGridMove> Neighbours(BeeGridPosition p) { Validate(p); return moves[p]; }
    internal IReadOnlyList<Contact> Contacts(BeeGridPosition p) { Validate(p); return contacts[p]; }
}

public enum BeeSurfaceEnvironmentKind { Unavailable, AdjacentAir, Exterior, Mixed }
public readonly struct BeeSurfaceEnvironment
{
    public readonly BeeSurfaceEnvironmentKind Kind;
    public readonly double TemperatureC, RelativeHumidityPercent;
    // A single sample has an address; an aggregate deliberately has no misleading cell address.
    public readonly int BoxId, CellIndex;
    public BeeSurfaceEnvironment(BeeSurfaceEnvironmentKind kind,double temperature,double humidity,int boxId=0,int cellIndex=-1)
    { Kind=kind;TemperatureC=temperature;RelativeHumidityPercent=humidity;BoxId=boxId;CellIndex=cellIndex; }
}

public static class BeeSurfaceEnvironmentReader
{
    public static bool TryRead(GameWorld world,long beeId,out BeeSurfaceEnvironment environment)
    {
        environment=default;
        return world.BeeLocations.TryGet(beeId,out var p) && TryReadAt(world,p,out environment);
    }
    // Call once per occupied grid cell when processing many bees; all its occupants share this value.
    public static bool TryReadAt(GameWorld world,BeeGridPosition p,out BeeSurfaceEnvironment environment)
    {
        environment=default;
        try
        {
            var contacts=world.BeeLocations.Topology(world).Contacts(p);
            if(contacts.Count==0) return false;
            var buffers=new Dictionary<EquipmentThermalState,VaporPressureCell[]>();
            foreach(var c in contacts) if(c.Air!=null)
            {
                var owner=c.Air.Source?.Equipment.ThermalState ?? c.Air.Grid?.Box.ThermalState;
                if(owner==null) return false;
                if(!buffers.ContainsKey(owner)) buffers.Add(owner,new VaporPressureCell[owner.Count]);
            }
            if(buffers.Count>0 && (world.Thermal==null || !world.Thermal.TryReadVaporPressures(buffers))) return false;
            double area=0,temp=0,pressure=0; int inside=0,outside=0;
            foreach(var c in contacts)
            {
                double t,v;
                if(c.Air==null)
                {
                    if(world.Weather==null) return false;
                    t=world.Weather.Current.TemperatureCelsius; v=Saturation(t)*world.Weather.Current.RelativeHumidityPercent/100; outside++;
                }
                else
                {
                    var source=c.Air.Source; var owner=source?.Equipment.ThermalState ?? c.Air.Grid.Box.ThermalState;
                    var value=buffers[owner][source?.Index ?? c.Air.Index];
                    if(!value.Available || double.IsNaN(value.AirPa)) return false;
                    t=value.TemperatureC;v=value.AirPa;inside++;
                }
                area+=c.Area;temp+=c.Area*t;pressure+=c.Area*v;
            }
            temp/=area;pressure/=area;
            var kind=inside==0?BeeSurfaceEnvironmentKind.Exterior:outside==0?BeeSurfaceEnvironmentKind.AdjacentAir:BeeSurfaceEnvironmentKind.Mixed;
            var only=contacts.Count==1?contacts[0].Air:null;
            environment=new BeeSurfaceEnvironment(kind,temp,100*pressure/Saturation(temp),only?.Grid?.Box.Id ?? 0,only?.Index ?? -1);return true;
        }
        catch(ArgumentException){return false;} catch(InvalidOperationException){return false;}
    }
    static double Saturation(double t)=>610.94*Math.Exp(17.625*t/(243.04+t));
}
