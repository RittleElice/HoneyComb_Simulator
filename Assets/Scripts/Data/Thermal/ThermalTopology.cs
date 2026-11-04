using System;
using System.Collections.Generic;
using System.Linq;

public static class ThermalTopology
{
    public static object[] Signature(GameWorld world, IReadOnlyList<ItemData> items = null)
    {
        var result = new List<object>();
        foreach(var hive in world.Apiary.Hives) result.Add(hive.Slot.Value);
        for(int y=0;y<world.Apiary.Grid.Height;y++) for(int x=0;x<world.Apiary.Grid.UnlockedWidth;x++) result.Add(world.Apiary.GetItemSlot(x,y).Value);
        for(int i=0;i<world.Inventory.Capacity;i++) result.Add(world.Inventory.GetSlot(i).Value);
        result.Add(world.Hand.Value);
        foreach(var item in items ?? ThermalWorld.Items(world)) { result.Add(item); if(item.SpatialGrid!=null) result.Add(item.SpatialGrid.Revision); }
        return result.ToArray();
    }
    private static int EmptySlotMask(HiveSpatialGrid grid)
    {
        // Keep every empty-slot air cell independent for thermal exchange.
        return 0;
    }
    private static bool IsGrouped(int mask, HiveGridLayout layout, int x, int y, int z)
    {
        if (mask == 0 || x < 1 || x >= HiveGridLayout.SizeX - 1 ||
            y < 1 || y >= HiveGridLayout.SizeY - 1 || z < layout.SlotBottom ||
            z >= layout.SlotBottom + HiveGridLayout.FrameSizeZ) return false;
        return (mask & (1 << ((y - 1) / HiveGridLayout.SlotDepthCells))) != 0;
    }
    // Six neighbours are valid only because all placed cell bounds are conforming.
    // CSR still stores the full adjacency, avoiding a silent six-entry truncation.
    public static ThermalExchange Build(GameWorld world, ThermalTransferCoefficients transfers = null, IReadOnlyList<ItemData> items = null)
    {
        items ??= ThermalWorld.Items(world).ToArray();
        var cells = new List<ThermalExchange.Cell>(); var faces = new List<ThermalExchange.Face>();
        var boundaries = new Dictionary<(int,int),ThermalExchange.Boundary>();
        var indices = new Dictionary<(EquipmentThermalState,int),int>();
        var grids = new Dictionary<HiveSpatialGrid,int[]>();
        foreach (var item in items)
        {
            var grid = item.SpatialGrid; if (grid == null) continue;
            var map = new int[grid.CellCount]; grids.Add(grid,map); var l = grid.Layout;
            int emptyMask = EmptySlotMask(grid);
            for (int i=0;i<map.Length;i++)
            {
                var source = grid.GetCell(i).SourceCell;
                var owner = source?.Equipment.ThermalState ?? item.ThermalState; int local = source?.Index ?? i;
                l.Coordinates(i,out int x,out int y,out int z);
                bool slotAir = IsGrouped(emptyMask, l, x, y, z);
                if (slotAir)
                {
                    if (source != null || grid.GetCell(i).Material != HiveMaterial.Air)
                        throw new InvalidOperationException("Expected air in empty slot");
                    if ((y - 1) % HiveGridLayout.SlotDepthCells != 0)
                    {
                        map[i] = map[i - HiveGridLayout.SizeX];
                        continue;
                    }
                }
                if (source != null)
                {
                    var eg = source.Equipment.EquipmentGrid;
                    int sx=local%eg.SizeX, sy=local/eg.SizeX%eg.SizeY, sz=local/(eg.SizeX*eg.SizeY);
                    if (!Same(l.X[x+1]-l.X[x],eg.X[sx+1]-eg.X[sx]) || !Same(l.Y[y+1]-l.Y[y],eg.Y[sy+1]-eg.Y[sy]) || !Same(l.Z[z+1]-l.Z[z],eg.Z[sz+1]-eg.Z[sz]))
                        throw new InvalidOperationException("Nonconforming placed cell: equipment "+source.Equipment.Id+" local "+local+" grid "+i+"; six-neighbour topology invalid");
                }
                map[i] = Add(cells,indices,owner,local,"box "+item.Id+" cell "+i+" ("+x+","+y+","+z+") owner "+(source?.Equipment.Id ?? item.Id)+":"+local,slotAir);
            }
            AddExterior(map,l.X,l.Y,l.Z,boundaries);
            var mergedFaces = new Dictionary<(int,int),int>();
            foreach (var edge in grid.Edges())
            {
                l.Coordinates(edge.A,out int x,out int y,out int z);
                int axis = edge.B-edge.A==1?0:edge.B-edge.A==HiveGridLayout.SizeX?1:2;
                var coords = axis==0?l.X:axis==1?l.Y:l.Z; int p=axis==0?x:axis==1?y:z;
                // Queen excluder is a zero-thickness permeable face; no calibrated thermal resistance exists.
                int a = map[edge.A], b = map[edge.B];
                if (a == b) continue; // Internal to a single mixed air volume.
                double halfA = (coords[p+1]-coords[p])*.0005;
                double halfB = (coords[p+2]-coords[p+1])*.0005;
                double area = edge.AreaMm2*1e-6;
                if (cells[a].IsSlotAir || cells[b].IsSlotAir)
                {
                    if (axis == 1)
                    {
                        if (cells[a].IsSlotAir) halfA = HiveGridLayout.SlotDepthMm*.0005;
                        if (cells[b].IsSlotAir) halfB = HiveGridLayout.SlotDepthMm*.0005;
                    }
                    if (mergedFaces.TryGetValue((a,b), out int existing))
                    {
                        var face = faces[existing];
                        faces[existing] = new ThermalExchange.Face(a,b,face.AreaM2+area,halfA,halfB,axis);
                        continue;
                    }
                    mergedFaces.Add((a,b),faces.Count);
                }
                faces.Add(new ThermalExchange.Face(a,b,area,halfA,halfB,axis));
            }
        }
        // Unplaced equipment has its own network exposed to ambient; hidden box air is excluded.
        foreach (var item in items)
        {
            var g=item.EquipmentGrid; if(g==null || item.SpatialGrid!=null || g.SizeZ==0) continue;
            int count=g.SizeX*g.SizeY*g.SizeZ;
            int present=0; for(int i=0;i<count;i++) if(indices.ContainsKey((item.ThermalState,i))) present++;
            if(present==count) continue;
            if(present!=0) throw new InvalidOperationException("Partially mapped equipment "+item.Id+"; no connections omitted");
            var map=new int[count];
            for(int i=0;i<count;i++) map[i]=Add(cells,indices,item.ThermalState,i,"detached equipment "+item.Id+" cell "+i);
            AddExterior(map,g.X,g.Y,g.Z,boundaries);
            for(int z=0;z<g.SizeZ;z++) for(int y=0;y<g.SizeY;y++) for(int x=0;x<g.SizeX;x++)
            {
                int i=g.Index(x,y,z); double dx=g.X[x+1]-g.X[x],dy=g.Y[y+1]-g.Y[y],dz=g.Z[z+1]-g.Z[z];
                for(int axis=0;axis<3;axis++)
                {
                    int p=axis==0?x:axis==1?y:z, size=axis==0?g.SizeX:axis==1?g.SizeY:g.SizeZ;
                    if(p+1==size) continue; var a=axis==0?g.X:axis==1?g.Y:g.Z;
                    int j=g.Index(x+(axis==0?1:0),y+(axis==1?1:0),z+(axis==2?1:0));
                    faces.Add(new ThermalExchange.Face(map[i],map[j],(axis==0?dy*dz:axis==1?dx*dz:dx*dy)*1e-6,(a[p+1]-a[p])*.0005,(a[p+2]-a[p+1])*.0005,axis));
                }
            }
        }
        var roots=new List<MovableItem>();
        foreach(var hive in world.Apiary.Hives) roots.Add(hive.Slot.Value);
        for(int y=0;y<world.Apiary.Grid.Height;y++) for(int x=0;x<world.Apiary.Grid.UnlockedWidth;x++) roots.Add(world.Apiary.GetItemSlot(x,y).Value);
        for(int i=0;i<world.Inventory.Capacity;i++) roots.Add(world.Inventory.GetSlot(i).Value);
        roots.Add(world.Hand.Value);
        var seen=new HashSet<MovableItem>();
        foreach(var root in roots) AddStacks(root,seen,grids,faces,boundaries);
        var exchange = new ThermalExchange(cells.ToArray(),faces.ToArray(),boundaries.Values.OrderBy(b=>b.Cell).ThenBy(b=>b.Direction).ToArray(),transfers);
        exchange.TopologyBinding = new Binding(world,grids,items);
        return exchange;
    }
    // Fixed geometric lattice: slot moves only change the equipment that owns a cell.
    // Membership/stack changes retain the full builder as an explicit structural fallback.
    internal sealed class Binding
    {
        private readonly HiveSpatialGrid[] grids;
        private readonly int[][] cellMaps;
        private readonly int[] revisions, emptyMasks;
        private readonly ItemData[] detached;
        private readonly List<(HiveSpatialGrid,HiveSpatialGrid)> pairs;
        private readonly List<KeyValuePair<int,ThermalExchange.Cell>> replacements = new List<KeyValuePair<int,ThermalExchange.Cell>>();
        public Binding(GameWorld world,Dictionary<HiveSpatialGrid,int[]> maps, IReadOnlyList<ItemData> items)
        {
            grids=items.Where(i=>i.SpatialGrid!=null).Select(i=>i.SpatialGrid).ToArray();
            cellMaps=grids.Select(g=>maps[g]).ToArray();emptyMasks=grids.Select(EmptySlotMask).ToArray();revisions=grids.Select(g=>g.Revision).ToArray();
            detached=Detached(items);pairs=Pairs(world);
        }
        private static ItemData[] Detached(IReadOnlyList<ItemData> items)
        {
            var placed=new HashSet<ItemData>();
            foreach(var item in items)if(item.InteriorSlots!=null)for(int j=0;j<item.InteriorSlots.Capacity;j++)if(item.InteriorSlots.GetSlot(j).Item!=null)placed.Add(item.InteriorSlots.GetSlot(j).Item);
            return items.Where(i=>i.SpatialGrid==null && i.EquipmentGrid!=null && i.EquipmentGrid.SizeZ>0 && !placed.Contains(i)).ToArray();
        }
        private static List<(HiveSpatialGrid,HiveSpatialGrid)> Pairs(GameWorld world)
        {
            var result=new List<(HiveSpatialGrid,HiveSpatialGrid)>();var seen=new HashSet<MovableItem>();
            Action<MovableItem> visit=null;
            visit=value=>{
                if(value==null || !seen.Add(value))return;
                if(value is ItemAssemblyData assembly){HiveSpatialGrid lower=null;foreach(var part in assembly.Parts){var upper=part.SpatialGrid;if(upper!=null){if(lower!=null)result.Add((lower,upper));lower=upper;}visit(part);}}
                else if(value is ItemData item){if(item.TopSlot!=null)visit(item.TopSlot.Value);if(item.InteriorSlots!=null)for(int j=0;j<item.InteriorSlots.Capacity;j++)visit(item.InteriorSlots.GetSlot(j).Value);}
            };
            foreach(var h in world.Apiary.Hives)visit(h.Slot.Value);
            for(int y=0;y<world.Apiary.Grid.Height;y++)for(int x=0;x<world.Apiary.Grid.UnlockedWidth;x++)visit(world.Apiary.GetItemSlot(x,y).Value);
            for(int i=0;i<world.Inventory.Capacity;i++)visit(world.Inventory.GetSlot(i).Value);visit(world.Hand.Value);
            return result;
        }
        public bool TryUpdate(GameWorld world,ThermalExchange exchange, IReadOnlyList<ItemData> items = null)
        {
            items ??= ThermalWorld.Items(world).ToArray();
            if(!grids.SequenceEqual(items.Where(i=>i.SpatialGrid!=null).Select(i=>i.SpatialGrid)) || !detached.SequenceEqual(Detached(items)) || !pairs.SequenceEqual(Pairs(world)))return false;
            replacements.Clear();
            for(int g=0;g<grids.Length;g++){
                var grid=grids[g];if(revisions[g]==grid.Revision)continue;
                if (EmptySlotMask(grid) != emptyMasks[g]) return false;
                var l=grid.Layout;
                for(int i=0;i<grid.CellCount;i++){
                    var source=grid.GetCell(i).SourceCell;var owner=source?.Equipment.ThermalState??grid.Box.ThermalState;int local=source?.Index??i;
                    var old=exchange.GetCell(cellMaps[g][i]);if(old.IsSlotAir)continue;if(ReferenceEquals(old.Owner,owner)&&old.Index==local)continue;
                    if(!exchange.CanRebind(owner))return false;
                    l.Coordinates(i,out int x,out int y,out int z);
                    if(source!=null){var eg=source.Equipment.EquipmentGrid;int sx=local%eg.SizeX,sy=local/eg.SizeX%eg.SizeY,sz=local/(eg.SizeX*eg.SizeY);
                        if(!Same(l.X[x+1]-l.X[x],eg.X[sx+1]-eg.X[sx])||!Same(l.Y[y+1]-l.Y[y],eg.Y[sy+1]-eg.Y[sy])||!Same(l.Z[z+1]-l.Z[z],eg.Z[sz+1]-eg.Z[sz]))throw new InvalidOperationException("Nonconforming moved equipment");}
                    replacements.Add(new KeyValuePair<int,ThermalExchange.Cell>(cellMaps[g][i],new ThermalExchange.Cell(owner,local,"box "+grid.Box.Id+" cell "+i+" ("+x+","+y+","+z+") owner "+(source?.Equipment.Id??grid.Box.Id)+":"+local)));
                }
            }
            exchange.RebindCells(replacements);
            for(int g=0;g<grids.Length;g++)revisions[g]=grids[g].Revision;
            return true;
        }
    }
    private static void AddExterior(int[] map,IReadOnlyList<double> xs,IReadOnlyList<double> ys,IReadOnlyList<double> zs,Dictionary<(int,int),ThermalExchange.Boundary> boundaries)
    {
        int nx=xs.Count-1, ny=ys.Count-1, nz=zs.Count-1;
        for(int z=0;z<nz;z++) for(int y=0;y<ny;y++) for(int x=0;x<nx;x++)
        {
            int i=map[x+nx*(y+ny*z)];
            double dx=xs[x+1]-xs[x],dy=ys[y+1]-ys[y],dz=zs[z+1]-zs[z];
            if(x==0) boundaries.Add((i,0),new ThermalExchange.Boundary(i,0,dy*dz*1e-6,dx*.0005));
            if(x==nx-1) boundaries.Add((i,1),new ThermalExchange.Boundary(i,1,dy*dz*1e-6,dx*.0005));
            if(y==0) boundaries.Add((i,2),new ThermalExchange.Boundary(i,2,dx*dz*1e-6,dy*.0005));
            if(y==ny-1) boundaries.Add((i,3),new ThermalExchange.Boundary(i,3,dx*dz*1e-6,dy*.0005));
            if(z==0) boundaries.Add((i,4),new ThermalExchange.Boundary(i,4,dx*dy*1e-6,dz*.0005));
            if(z==nz-1) boundaries.Add((i,5),new ThermalExchange.Boundary(i,5,dx*dy*1e-6,dz*.0005));
        }
    }
    private static bool Same(double a,double b) => Math.Abs(a-b)<1e-9;
    private static int Add(List<ThermalExchange.Cell> cells,Dictionary<(EquipmentThermalState,int),int> map,EquipmentThermalState owner,int index,string label,bool slotAir=false)
    {
        if(map.ContainsKey((owner,index))) throw new InvalidOperationException("Duplicate active owner: "+label);
        int result=cells.Count; map.Add((owner,index),result); cells.Add(new ThermalExchange.Cell(owner,index,label,slotAir)); return result;
    }
    private static void AddStacks(MovableItem root,HashSet<MovableItem> seen,Dictionary<HiveSpatialGrid,int[]> grids,List<ThermalExchange.Face> faces,Dictionary<(int,int),ThermalExchange.Boundary> boundaries)
    {
        if(root==null || !seen.Add(root)) return;
        if(root is ItemAssemblyData assembly)
        {
            HiveSpatialGrid lower=null;
            foreach(var part in assembly.Parts)
            {
                var upper=part.SpatialGrid;
                if(upper!=null)
                {
                    if(lower!=null) Join(lower,upper,grids,faces,boundaries);
                    lower=upper;
                }
                AddStacks(part,seen,grids,faces,boundaries);
            }
        }
        else if(root is ItemData item)
        {
            if(item.TopSlot!=null) AddStacks(item.TopSlot.Value,seen,grids,faces,boundaries);
            if(item.InteriorSlots!=null) for(int i=0;i<item.InteriorSlots.Capacity;i++) AddStacks(item.InteriorSlots.GetSlot(i).Value,seen,grids,faces,boundaries);
        }
    }
    private static void Join(HiveSpatialGrid lower,HiveSpatialGrid upper,Dictionary<HiveSpatialGrid,int[]> grids,List<ThermalExchange.Face> faces,Dictionary<(int,int),ThermalExchange.Boundary> boundaries)
    {
        var a=lower.Layout; var b=upper.Layout;
        if(!a.X.SequenceEqual(b.X)||!a.Y.SequenceEqual(b.Y)) throw new InvalidOperationException("Nonconforming stack interface "+lower.Box.Id+" / "+upper.Box.Id+"; six-direction index invalid. Overlap-face construction required; no partial graph accepted.");
        for(int y=0;y<HiveGridLayout.SizeY;y++) for(int x=0;x<HiveGridLayout.SizeX;x++)
        {
            int lo=grids[lower][a.Index(x,y,a.SizeZ-1)], hi=grids[upper][b.Index(x,y,0)];
            if (!boundaries.Remove((lo,5)) || !boundaries.Remove((hi,4))) throw new InvalidOperationException("Stack face already occluded");
            faces.Add(new ThermalExchange.Face(grids[lower][a.Index(x,y,a.SizeZ-1)],grids[upper][b.Index(x,y,0)],(a.X[x+1]-a.X[x])*(a.Y[y+1]-a.Y[y])*1e-6,(a.Z[a.SizeZ]-a.Z[a.SizeZ-1])*.0005,(b.Z[1]-b.Z[0])*.0005,2));
        }
    }
}
