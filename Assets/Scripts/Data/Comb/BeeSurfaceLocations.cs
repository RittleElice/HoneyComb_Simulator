using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

// Coordinates identify air relative to the equipment being touched, including its exterior.
public enum BeeFace : byte { MinusX, PlusX, MinusY, PlusY, MinusZ, PlusZ }

public readonly struct BeeGridPosition : IEquatable<BeeGridPosition>
{
    public readonly int EquipmentId, X, Y, Z;
    public readonly BeeFace Face;

    public BeeGridPosition(int equipmentId, int x, int y, int z, BeeFace face)
    {
        if ((byte)face > (byte)BeeFace.PlusZ) throw new ArgumentOutOfRangeException(nameof(face));
        EquipmentId = equipmentId; X = x; Y = y; Z = z; Face = face;
    }

    internal static int Offset(BeeFace face, int axis)
        => (int)face / 2 == axis ? (((int)face & 1) == 0 ? -1 : 1) : 0;

    public static BeeGridPosition FromSupport(int id, int x, int y, int z, BeeFace face)
        => new BeeGridPosition(id, x + Offset(face, 0), y + Offset(face, 1), z + Offset(face, 2), face);

    internal (int id, int x, int y, int z) Support =>
        (EquipmentId, X - Offset(Face, 0), Y - Offset(Face, 1), Z - Offset(Face, 2));
    public bool Equals(BeeGridPosition p)
        => EquipmentId == p.EquipmentId && X == p.X && Y == p.Y && Z == p.Z && Face == p.Face;
    public override bool Equals(object obj) => obj is BeeGridPosition p && Equals(p);
    public override int GetHashCode() => (EquipmentId, X, Y, Z, Face).GetHashCode();
    public override string ToString() => $"{EquipmentId}:({X},{Y},{Z}) {Face}";
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
    public IEnumerable<KeyValuePair<long, BeeGridPosition>> Entries { get { Refresh(); return positions.Select(p => p); } }
    public bool TryGet(long id, out BeeGridPosition p) { Refresh(); return positions.TryGetValue(id, out p); }
    public int CountAt(BeeGridPosition p) { Refresh(); return counts.TryGetValue(p, out int n) ? n : 0; }
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
        positions[id] = p; counts.TryGetValue(p, out int n); counts[p] = n + 1;
    }
    internal BeeGridTopology Topology(GameWorld world)
    {
        if (topology == null) topology = new BeeGridTopology(world);
        if (!ReferenceEquals(topology.World, world)) throw new InvalidOperationException("Locations belong to another world.");
        Refresh(); return topology;
    }
    void Refresh()
    {
        if (topology == null) return;
        int old = topology.Builds;
        topology.Ensure();
        if (old == topology.Builds || positions.Count == 0) return;
        var covered = counts.Keys.Where(p => !topology.IsExposed(p)).ToArray();
        if (covered.Length == 0) return;
        var replacements = covered.ToDictionary(p => p, topology.NearestExposed);
        foreach (var pair in positions.ToArray())
            if (replacements.TryGetValue(pair.Value, out var next)) Store(pair.Key, next);
    }
    public IReadOnlyList<BeeGridPosition> AtAir(GameWorld world, HiveSpatialGrid grid, int index)
        => Topology(world).AtAir(grid, index);
    public void Set(GameWorld world, long id, BeeGridPosition p)
    {
        if (id <= 0) throw new ArgumentOutOfRangeException(nameof(id));
        Topology(world).Validate(p);
        Store(id, p);
    }
    public IReadOnlyList<BeeGridMove> GetNeighbours(GameWorld world, BeeGridPosition p) => Topology(world).Neighbours(p);
    // Atomic, validated movement step. The behaviour scheduler supplies timing; no speed is invented here.
    public bool TryMove(GameWorld world, long id, BeeGridPosition destination, bool isQueen = false)
    {
        if (!TryGet(id, out var start)) return false;
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
            w.Write(pair.Value.X); w.Write(pair.Value.Y); w.Write(pair.Value.Z); w.Write((byte)pair.Value.Face);
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
                int x = r.ReadInt32(), y = r.ReadInt32(), z = r.ReadInt32();
                p = schema == 17 ? t.FromLegacy(equipment, x, y, z)
                    : new BeeGridPosition(equipment, x, y, z, (BeeFace)r.ReadByte());
                if (id <= 0 || positions.ContainsKey(id)) throw new ArgumentException("Invalid or duplicate bee ID.");
                t.Validate(p); Store(id, p);
            }
            catch (ArgumentException ex) { throw new InvalidDataException("Invalid bee grid location.", ex); }
            catch (InvalidOperationException ex) { throw new InvalidDataException("Missing bee equipment.", ex); }
        }
    }
}

internal sealed class BeeGridTopology
{
    internal readonly GameWorld World;
    internal sealed class Cell
    {
        internal int Group, X, Y, Z, Index;
        internal HiveSpatialGrid Grid;
        internal EquipmentSpatialCell Source;
        internal int LocalX, LocalY, LocalZ;
        internal double Dx, Dy, Dz;
        internal bool Solid => Source != null && Source.Material != HiveMaterial.Air;
        internal BeeGridPosition Face(int direction) =>
            BeeGridPosition.FromSupport(Source.Equipment.Id, LocalX, LocalY, LocalZ, (BeeFace)direction);
    }
    readonly Dictionary<int, ItemData> items = new Dictionary<int, ItemData>();
    readonly Dictionary<(int,int,int,int), Cell> cells = new Dictionary<(int,int,int,int), Cell>();
    readonly Dictionary<(int,int,int,int), Cell> solids = new Dictionary<(int,int,int,int), Cell>();
    readonly Dictionary<BeeGridPosition, Cell> airByPosition = new Dictionary<BeeGridPosition, Cell>();
    readonly Dictionary<(HiveSpatialGrid,int), List<BeeGridPosition>> supports = new Dictionary<(HiveSpatialGrid,int), List<BeeGridPosition>>();
    readonly Dictionary<BeeGridPosition, IReadOnlyList<BeeGridMove>> moves = new Dictionary<BeeGridPosition, IReadOnlyList<BeeGridMove>>();
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
        watched.Clear(); grids.Clear(); items.Clear(); cells.Clear(); solids.Clear(); airByPosition.Clear(); supports.Clear(); moves.Clear(); queenBarriers.Clear();
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
            if (g == null || item.SpatialGrid != null || g.SizeZ == 0 || solids.Keys.Any(p => p.Item1 == item.Id)) continue;
            int group=next++;
            for (int z=0;z<g.SizeZ;z++) for(int y=0;y<g.SizeY;y++) for(int x=0;x<g.SizeX;x++)
                Add(new Cell { Group=group,X=x,Y=y,Z=z,Source=g.GetCell(x,y,z),Dx=g.X[x+1]-g.X[x],Dy=g.Y[y+1]-g.Y[y],Dz=g.Z[z+1]-g.Z[z] });
        }
        foreach (var solid in solids.Values)
        for (int face = 0; face < 6; face++)
        {
            var air = Neighbour(solid, face);
            if (air != null && air.Solid) continue;
            var position = solid.Face(face);
            airByPosition.Add(position, air);
            if (air?.Grid == null) continue;
            var key = (air.Grid, air.Index);
            if (!supports.TryGetValue(key, out var list)) supports.Add(key, list = new List<BeeGridPosition>());
            list.Add(position);
        }
        foreach (var pair in airByPosition)
            moves.Add(pair.Key, BuildMoves(pair.Key, pair.Value));
        dirty = false;
        Builds++;
    }

    IReadOnlyList<BeeGridMove> BuildMoves(BeeGridPosition position, Cell air)
    {
        var solid = solids[position.Support];
        int face = (int)position.Face;
        var result = new List<BeeGridMove>(8);
        void AddFace(Cell target, int side, bool allowsQueen)
        {
            if (target == null || !target.Solid) return;
            var next = target.Face(side);
            if (airByPosition.ContainsKey(next)) result.Add(new BeeGridMove(next, allowsQueen));
        }
        for (int tangent = 0; tangent < 6; tangent++)
        {
            if (tangent / 2 == face / 2) continue;
            var alongSurface = Neighbour(solid, tangent);
            // Query relative to the support even when the current air is outside the volume.
            var diagonal = OffsetCell(solid, face, tangent);
            if (diagonal != null && diagonal.Solid)
            {
                // Inside corner. Both supports touch the very same volume of air.
                if (air != null) AddFace(diagonal, tangent ^ 1, true);
                continue;
            }
            bool queen = !queenBarriers.Contains((air, diagonal));
            if (alongSurface != null && alongSurface.Solid)
                AddFace(alongSurface, face, queen);
            else // Outside corner. Curl around one exposed edge, through open space.
                AddFace(solid, tangent, queen && !queenBarriers.Contains((diagonal, alongSurface)));
        }
        // Transfer across a horizontal 10 mm gap by moving one air cell into the next.
        if (air != null && face / 2 < 2 && IsSurfaceLayer(air, face))
        {
            var nextAir = Neighbour(air, face);
            if (nextAir != null && !nextAir.Solid && IsSurfaceLayer(nextAir, face))
                AddFace(Neighbour(nextAir, face), face ^ 1, !queenBarriers.Contains((air, nextAir)));
        }
        return result.ToArray();
    }

    static bool IsSurfaceLayer(Cell c, int face)
        => Math.Abs((face / 2 == 0 ? c.Dx : c.Dy) - HiveGridLayout.SurfaceAirMm) < 1e-8;

    Cell OffsetCell(Cell c, int a, int b)
    {
        int Offset(int axis) => BeeGridPosition.Offset((BeeFace)a, axis) + BeeGridPosition.Offset((BeeFace)b, axis);
        cells.TryGetValue((c.Group, c.X + Offset(0), c.Y + Offset(1), c.Z + Offset(2)), out var target);
        return target;
    }

    void Add(Cell c)
    {
        cells.Add((c.Group,c.X,c.Y,c.Z),c);
        if (!c.Solid) return;
        var g = c.Source.Equipment.EquipmentGrid;
        int i = c.Source.Index;
        c.LocalX = i % g.SizeX; c.LocalY = i / g.SizeX % g.SizeY; c.LocalZ = i / (g.SizeX * g.SizeY);
        solids.Add((c.Source.Equipment.Id, c.LocalX, c.LocalY, c.LocalZ), c);
    }
    Cell Neighbour(Cell c,int d)
    {
        int x=c.X,y=c.Y,z=c.Z; switch(d){case 0:x--;break;case 1:x++;break;case 2:y--;break;case 3:y++;break;case 4:z--;break;case 5:z++;break;}
        cells.TryGetValue((c.Group,x,y,z),out var n); return n;
    }
    internal void Validate(BeeGridPosition p)
    {
        if (!IsExposed(p)) throw new ArgumentException("Location must be air touching the selected support.");
    }
    internal bool IsExposed(BeeGridPosition p) => airByPosition.ContainsKey(p);
    internal IReadOnlyList<BeeGridMove> Neighbours(BeeGridPosition p) =>
        moves.TryGetValue(p, out var list) ? list : Array.Empty<BeeGridMove>();
    internal IReadOnlyList<BeeGridPosition> AtAir(HiveSpatialGrid grid, int index) =>
        supports.TryGetValue((grid, index), out var list) ? list.AsReadOnly() : (IReadOnlyList<BeeGridPosition>)Array.Empty<BeeGridPosition>();
    internal IEnumerable<BeeGridPosition> Positions => airByPosition.Keys;
    internal Cell SupportCell(BeeGridPosition p) => solids[p.Support];
    internal bool TryAir(BeeGridPosition p, out Cell air) => airByPosition.TryGetValue(p, out air);

    internal BeeGridPosition FromLegacy(int id, int x, int y, int z)
    {
        if (!items.TryGetValue(id, out var item)) throw new ArgumentException("Missing equipment");
        if (item.SpatialGrid != null)
        {
            if ((uint)x >= 9 || (uint)y >= 42 || (uint)z >= item.SpatialGrid.Layout.SizeZ)
                throw new ArgumentException("Invalid legacy equipment coordinate");
            x = HiveGridLayout.MapLegacyX(x); y = HiveGridLayout.MapLegacyY(y);
        }
        if (!solids.TryGetValue((id,x,y,z), out var solid)) throw new ArgumentException("Missing legacy support");
        int preferred = item.Kind == ItemKind.Frame ? (y == 0 ? 2 : 3) :
            x == 0 ? 1 : x == item.EquipmentGrid.SizeX-1 ? 0 :
            y == 0 ? 3 : y == item.EquipmentGrid.SizeY-1 ? 2 : item.Kind == ItemKind.Lid ? 4 : 5;
        var old = solid.Face(preferred);
        return IsExposed(old) ? old : NearestExposed(old);
    }

    internal BeeGridPosition NearestExposed(BeeGridPosition old)
    {
        var anchor = old.Support;
        // Only needed when assembly changes cover a previously occupied face.
        return airByPosition.Keys.Where(p => p.EquipmentId == old.EquipmentId)
            .OrderBy(p => Math.Abs(p.Support.x-anchor.x) + Math.Abs(p.Support.y-anchor.y) + Math.Abs(p.Support.z-anchor.z))
            .ThenBy(p => p.Face != old.Face).ThenBy(p => p.Z).ThenBy(p => p.Y).ThenBy(p => p.X).ThenBy(p => p.Face).First();
    }
}

public enum BeeSurfaceEnvironmentKind { Unavailable, AdjacentAir, Exterior, Mixed }
public readonly struct BeeSurfaceEnvironment
{
    public readonly BeeSurfaceEnvironmentKind Kind;
    public readonly double TemperatureC, RelativeHumidityPercent;
    // Interior samples identify the physical air cell; exterior samples use current weather.
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
        if (!world.BeeLocations.Topology(world).TryAir(p, out var air)) return false;
        if (air?.Grid == null)
        {
            if (world.Weather == null) return false;
            var weather = world.Weather.Current;
            environment = new BeeSurfaceEnvironment(BeeSurfaceEnvironmentKind.Exterior,
                weather.TemperatureCelsius, weather.RelativeHumidityPercent);
            return true;
        }
        var owner = air.Source?.Equipment.ThermalState ?? air.Grid.Box.ThermalState;
        var values = new VaporPressureCell[owner.Count];
        var buffers = new Dictionary<EquipmentThermalState,VaporPressureCell[]> { {owner, values} };
        if (world.Thermal == null || !world.Thermal.TryReadVaporPressures(buffers)) return false;
        var value = values[air.Source?.Index ?? air.Index];
        if (!value.Available || double.IsNaN(value.AirPa)) return false;
        environment = new BeeSurfaceEnvironment(BeeSurfaceEnvironmentKind.AdjacentAir,
            value.TemperatureC, 100 * value.AirPa / Saturation(value.TemperatureC), air.Grid.Box.Id, air.Index);
        return true;
    }
    static double Saturation(double t)=>610.94*Math.Exp(17.625*t/(243.04+t));
}
