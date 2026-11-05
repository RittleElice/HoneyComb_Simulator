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
        for(int i=0;i<world.Warehouse.Capacity;i++) result.Add(world.Warehouse.GetSlot(i).Value);
        for(int i=0;i<world.Trash.Capacity;i++) result.Add(world.Trash.GetSlot(i).Value);
        result.Add(world.Hand.Value);
        foreach(var item in items ?? ThermalWorld.Items(world)) { result.Add(item); if(item.SpatialGrid!=null) result.Add(item.SpatialGrid.Revision); }
        return result.ToArray();
    }
    private static int EmptySlotMask(HiveSpatialGrid grid)
    {
        var slots = grid.Box.InteriorSlots;
        if (slots == null) return 0;
        int mask = 0;
        for (int slot = 0; slot < slots.Capacity; slot++)
            if (slots.GetSlot(slot).IsEmpty) mask |= 1 << slot;
        return mask;
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
            foreach (var cell in grid.ActiveCells())
            {
                int i = cell.firstIndex;
                var source = grid.GetCell(i).SourceCell;
                var owner = source?.Equipment.ThermalState ?? item.ThermalState; int local = source?.Index ?? i;
                l.Coordinates(i,out int x,out int y,out int z);
                bool grouped = cell.depthCells > 1;
                if (source != null)
                {
                    var eg = source.Equipment.EquipmentGrid;
                    int sx=local%eg.SizeX, sy=local/eg.SizeX%eg.SizeY, sz=local/(eg.SizeX*eg.SizeY);
                    if (!Same(l.X[x+1]-l.X[x],eg.X[sx+1]-eg.X[sx]) || !Same(l.Y[y+1]-l.Y[y],eg.Y[sy+1]-eg.Y[sy]) || !Same(l.Z[z+1]-l.Z[z],eg.Z[sz+1]-eg.Z[sz]))
                        throw new InvalidOperationException("Nonconforming placed cell: equipment "+source.Equipment.Id+" local "+local+" grid "+i+"; six-neighbour topology invalid");
                }
                map[i] = Add(cells,indices,owner,local,"box "+item.Id+" cell "+i+" ("+x+","+y+","+z+") owner "+(source?.Equipment.Id ?? item.Id)+":"+local,grouped);
                for (int layer=1;layer<cell.depthCells;layer++) map[i + layer * HiveGridLayout.SizeX] = map[i];
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
                if (a == b) continue; // Internal to a single mixed volume.
                double halfA = (coords[p+1]-coords[p])*.0005;
                double halfB = (coords[p+2]-coords[p+1])*.0005;
                double area = edge.AreaMm2*1e-6;
                if (cells[a].IsSlotGroup || cells[b].IsSlotGroup)
                {
                    if (axis == 1)
                    {
                        if (cells[a].IsSlotGroup) halfA = HiveGridLayout.SlotDepthMm*.0005;
                        if (cells[b].IsSlotGroup) halfB = HiveGridLayout.SlotDepthMm*.0005;
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
        for(int i=0;i<world.Warehouse.Capacity;i++) roots.Add(world.Warehouse.GetSlot(i).Value);
        for(int i=0;i<world.Trash.Capacity;i++) roots.Add(world.Trash.GetSlot(i).Value);
        roots.Add(world.Hand.Value);
        var seen=new HashSet<MovableItem>();
        var joins = new List<(int lower,int upper)>();
        foreach(var entry in grids)AddEmptySlotEndAir(entry.Key,entry.Value,joins);
        foreach(var root in roots) AddStacks(root,seen,grids,faces,boundaries,joins);
        if(joins.Count>0) MergeAir(cells,faces,boundaries,joins,grids);
        var exchange = new ThermalExchange(cells.ToArray(),faces.ToArray(),boundaries.Values.OrderBy(b=>b.Cell).ThenBy(b=>b.Direction).ToArray(),transfers);
        if(joins.Count==0) exchange.TopologyBinding = new Binding(world,grids,items);
        return exchange;
    }
    // Extend an empty comb bay only at its two ends. The top end can then
    // connect across an open stack interface to the next empty bay's bottom end.
    private static void AddEmptySlotEndAir(HiveSpatialGrid grid,int[] map,List<(int lower,int upper)> joins)
    {
        int empty=EmptySlotMask(grid);
        if(empty==0)return;
        var layout=grid.Layout;
        int bottom=layout.SlotBottom;
        int top=bottom+HiveGridLayout.FrameSizeZ-1;
        for(int slot=0;slot<HiveGridLayout.SlotCount;slot++){
            if((empty & (1<<slot))==0)continue;
            for(int layer=0;layer<HiveGridLayout.SlotDepthCells;layer++){
                int y=1+slot*HiveGridLayout.SlotDepthCells+layer;
                for(int x=1;x<HiveGridLayout.SizeX-1;x++){
                    JoinIfAir(x,y,bottom-1);
                    if(grid.TopBoundaryEquipment==null)JoinIfAir(x,y,top);
                }
            }
        }
        void JoinIfAir(int x,int y,int z){
            int a=layout.Index(x,y,z),b=layout.Index(x,y,z+1);
            if(grid.GetCell(a).Material==HiveMaterial.Air && grid.GetCell(b).Material==HiveMaterial.Air)
                joins.Add((map[a],map[b]));
        }
    }
    // Join only contacting air rows. A wide Y row can touch four narrow rows.
    // Keep every equipment member address for reads, save/load and separation.
    private static void MergeAir(List<ThermalExchange.Cell> cells,List<ThermalExchange.Face> faces,
        Dictionary<(int,int),ThermalExchange.Boundary> boundaries,List<(int lower,int upper)> joins,
        Dictionary<HiveSpatialGrid,int[]> grids)
    {
        var parents=new Dictionary<int,int>();
        int Root(int row){
            if(!parents.ContainsKey(row))parents.Add(row,row);
            int root=row;
            while(parents[root]!=root)root=parents[root];
            while(row!=root){int next=parents[row];parents[row]=root;row=next;}
            return root;
        }
        foreach(var pair in joins){int lo=Root(pair.lower),hi=Root(pair.upper);if(lo!=hi)parents[hi]=lo;}
        var layouts=grids.Keys.ToDictionary(g=>g.Box.ThermalState,g=>g.Layout);
        var sets=new Dictionary<int,List<(EquipmentThermalState,int,double)>>();
        var mergedRows=new Dictionary<int,int>();
        var compact=new List<ThermalExchange.Cell>(cells.Count);
        var remap=new int[cells.Count];
        for(int i=0;i<cells.Count;i++){
            if(!parents.ContainsKey(i)){remap[i]=compact.Count;compact.Add(cells[i]);continue;}
            int root=Root(i);
            if(!sets.TryGetValue(root,out var parts)){
                sets[root]=parts=new List<(EquipmentThermalState,int,double)>();
                mergedRows[root]=compact.Count;compact.Add(default);
            }
            remap[i]=mergedRows[root];
            var cell=cells[i];
            for(int k=0;k<cell.MemberCount;k++){
                int member=cell.MemberIndex(k);
                parts.Add((cell.Owner,member,layouts[cell.Owner].VolumeMm3(member)));
            }
        }
        foreach(var entry in sets){
            var stack=new ThermalExchange.Cell.StackMembers(entry.Value);
            compact[mergedRows[entry.Key]]=new ThermalExchange.Cell(stack,"shared air / "+cells[entry.Key].Label);
        }
        var parallelFaces=new Dictionary<(int,int,int),int>();
        int write=0;
        for(int i=0;i<faces.Count;i++){
            var face=faces[i];int a=remap[face.A],b=remap[face.B];if(a==b)continue;
            var key=(a,b,face.Axis);
            if(compact[a].IsMergedAir || compact[b].IsMergedAir){
                if(compact[a].IsMergedAir)compact[a].Stack.FaceAreas[face.Axis]+=face.AreaM2*.5;
                if(compact[b].IsMergedAir)compact[b].Stack.FaceAreas[face.Axis]+=face.AreaM2*.5;
                if(parallelFaces.TryGetValue(key,out int existing)){
                    faces[existing]=new ThermalExchange.Face(a,b,faces[existing].AreaM2+face.AreaM2,face.HalfAM,face.HalfBM,face.Axis);
                    continue;
                }
                parallelFaces.Add(key,write);
            }
            faces[write++]=new ThermalExchange.Face(a,b,face.AreaM2,face.HalfAM,face.HalfBM,face.Axis);
        }
        faces.RemoveRange(write,faces.Count-write);
        var exterior=boundaries.Values.ToArray();boundaries.Clear();
        foreach(var face in exterior){
            int row=remap[face.Cell];double area=face.AreaM2;
            if(compact[row].IsMergedAir)compact[row].Stack.FaceAreas[face.Direction/2]+=area*.5;
            if(boundaries.TryGetValue((row,face.Direction),out var old))area+=old.AreaM2;
            boundaries[(row,face.Direction)]=new ThermalExchange.Boundary(row,face.Direction,area,face.HalfM);
        }
        cells.Clear();cells.AddRange(compact);
    }
    // Fixed geometric lattice: slot moves only change the equipment that owns a cell.
    // Membership/stack changes retain the full builder as an explicit structural fallback.
    internal sealed class Binding
    {
        private readonly HiveSpatialGrid[] grids;
        private readonly int[][] cellMaps;
        private readonly int[] revisions, emptyMasks;
        private readonly ItemData[] topItems;
        private readonly ItemData[] detached;
        private readonly List<(HiveSpatialGrid,HiveSpatialGrid)> pairs;
        private readonly List<KeyValuePair<int,ThermalExchange.Cell>> replacements = new List<KeyValuePair<int,ThermalExchange.Cell>>();
        public Binding(GameWorld world,Dictionary<HiveSpatialGrid,int[]> maps, IReadOnlyList<ItemData> items)
        {
            grids=items.Where(i=>i.SpatialGrid!=null).Select(i=>i.SpatialGrid).ToArray();
            
            topItems=grids.Select(g=>g.TopBoundaryEquipment).ToArray();
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
            for(int i=0;i<world.Inventory.Capacity;i++)visit(world.Inventory.GetSlot(i).Value);
            for(int i=0;i<world.Warehouse.Capacity;i++)visit(world.Warehouse.GetSlot(i).Value);
            for(int i=0;i<world.Trash.Capacity;i++)visit(world.Trash.GetSlot(i).Value);
            visit(world.Hand.Value);
            return result;
        }
        public bool TryUpdate(GameWorld world,ThermalExchange exchange, IReadOnlyList<ItemData> items = null)
        {
            items ??= ThermalWorld.Items(world).ToArray();
            if(!grids.SequenceEqual(items.Where(i=>i.SpatialGrid!=null).Select(i=>i.SpatialGrid)) || !detached.SequenceEqual(Detached(items)) || !pairs.SequenceEqual(Pairs(world)))return false;
            replacements.Clear();
            for(int g=0;g<grids.Length;g++){
                var grid=grids[g];if(revisions[g]==grid.Revision)continue;
                if (grid.TopBoundaryEquipment!=topItems[g] || EmptySlotMask(grid) != emptyMasks[g]) return false;
                var l=grid.Layout;
                for(int i=0;i<grid.CellCount;i++){
                    var source=grid.GetCell(i).SourceCell;var owner=source?.Equipment.ThermalState??grid.Box.ThermalState;int local=source?.Index??i;
                    var old=exchange.GetCell(cellMaps[g][i]);if(old.IsSlotGroup)continue;if(ReferenceEquals(old.Owner,owner)&&old.Index==local)continue;
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
            if(x==0) AddBoundary(i,0,dy*dz*1e-6,dx*.0005);
            if(x==nx-1) AddBoundary(i,1,dy*dz*1e-6,dx*.0005);
            if(y==0) AddBoundary(i,2,dx*dz*1e-6,dy*.0005);
            if(y==ny-1) AddBoundary(i,3,dx*dz*1e-6,dy*.0005);
            if(z==0) AddBoundary(i,4,dx*dy*1e-6,dz*.0005);
            if(z==nz-1) AddBoundary(i,5,dx*dy*1e-6,dz*.0005);
        }
        void AddBoundary(int row,int direction,double area,double half)
        {
            var key = (row,direction);
            if (boundaries.TryGetValue(key,out var previous)) area += previous.AreaM2;
            boundaries[key] = new ThermalExchange.Boundary(row,direction,area,half);
        }
    }
    private static bool Same(double a,double b) => Math.Abs(a-b)<1e-9;
    private static int Add(List<ThermalExchange.Cell> cells,Dictionary<(EquipmentThermalState,int),int> map,EquipmentThermalState owner,int index,string label,bool grouped=false)
    {
        if(map.ContainsKey((owner,index))) throw new InvalidOperationException("Duplicate active owner: "+label);
        int result=cells.Count; map.Add((owner,index),result); cells.Add(new ThermalExchange.Cell(owner,index,label,grouped)); return result;
    }
    private static void AddStacks(MovableItem root,HashSet<MovableItem> seen,Dictionary<HiveSpatialGrid,int[]> grids,List<ThermalExchange.Face> faces,Dictionary<(int,int),ThermalExchange.Boundary> boundaries,List<(int lower,int upper)> joins)
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
                    if(lower!=null) Join(lower,upper,grids,faces,boundaries,joins);
                    lower=upper;
                }
                AddStacks(part,seen,grids,faces,boundaries,joins);
            }
        }
        else if(root is ItemData item)
        {
            if(item.TopSlot!=null) AddStacks(item.TopSlot.Value,seen,grids,faces,boundaries,joins);
            if(item.InteriorSlots!=null) for(int i=0;i<item.InteriorSlots.Capacity;i++) AddStacks(item.InteriorSlots.GetSlot(i).Value,seen,grids,faces,boundaries,joins);
        }
    }
    private static void Join(HiveSpatialGrid lower,HiveSpatialGrid upper,Dictionary<HiveSpatialGrid,int[]> grids,List<ThermalExchange.Face> faces,Dictionary<(int,int),ThermalExchange.Boundary> boundaries,List<(int lower,int upper)> joins)
    {
        var a=lower.Layout; var b=upper.Layout;
        if(!a.X.SequenceEqual(b.X)||!a.Y.SequenceEqual(b.Y)) throw new InvalidOperationException("Nonconforming stack interface "+lower.Box.Id+" / "+upper.Box.Id+"; six-direction index invalid. Overlap-face construction required; no partial graph accepted.");
        var lowerRows = new HashSet<int>();
        var upperRows = new HashSet<int>();
        var interfaceFaces = new Dictionary<(int,int),int>();
        double halfA = (a.Z[a.SizeZ]-a.Z[a.SizeZ-1])*.0005;
        double halfB = (b.Z[1]-b.Z[0])*.0005;
        for(int y=0;y<HiveGridLayout.SizeY;y++) for(int x=0;x<HiveGridLayout.SizeX;x++)
        {
            int lo=grids[lower][a.Index(x,y,a.SizeZ-1)], hi=grids[upper][b.Index(x,y,0)];
            if ((lowerRows.Add(lo) && !boundaries.Remove((lo,5))) ||
                (upperRows.Add(hi) && !boundaries.Remove((hi,4))))
                throw new InvalidOperationException("Stack face already occluded");
            if(lower.TopBoundaryEquipment==null && !b.IsLid &&
                lower.GetCell(x,y,a.SizeZ-1).Material==HiveMaterial.Air &&
                upper.GetCell(x,y,0).Material==HiveMaterial.Air)
                joins.Add((lo,hi));
            double area=(a.X[x+1]-a.X[x])*(a.Y[y+1]-a.Y[y])*1e-6;
            if(interfaceFaces.TryGetValue((lo,hi),out int id))
                faces[id]=new ThermalExchange.Face(lo,hi,faces[id].AreaM2+area,halfA,halfB,2);
            else
            {
                interfaceFaces.Add((lo,hi),faces.Count);
                faces.Add(new ThermalExchange.Face(lo,hi,area,halfA,halfB,2));
            }
        }
    }
}
