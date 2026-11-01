using System;
using System.Collections.Generic;
using System.Linq;

public static class ThermalTopology
{
    public static object[] Signature(GameWorld world)
    {
        var result = new List<object>();
        foreach(var hive in world.Apiary.Hives) result.Add(hive.Slot.Value);
        for(int y=0;y<world.Apiary.Grid.Height;y++) for(int x=0;x<world.Apiary.Grid.UnlockedWidth;x++) result.Add(world.Apiary.GetItemSlot(x,y).Value);
        for(int i=0;i<world.Inventory.Capacity;i++) result.Add(world.Inventory.GetSlot(i).Value);
        result.Add(world.Hand.Value);
        foreach(var item in ThermalWorld.Items(world)) { result.Add(item); if(item.SpatialGrid!=null) result.Add(item.SpatialGrid.Revision); }
        return result.ToArray();
    }
    // Six neighbours are valid only because all placed cell bounds are conforming.
    // CSR still stores the full adjacency, avoiding a silent six-entry truncation.
    public static ThermalExchange Build(GameWorld world)
    {
        var items = ThermalWorld.Items(world).ToArray();
        var cells = new List<ThermalExchange.Cell>(); var faces = new List<ThermalExchange.Face>();
        var indices = new Dictionary<(EquipmentThermalState,int),int>();
        var grids = new Dictionary<HiveSpatialGrid,int[]>();
        foreach (var item in items)
        {
            var grid = item.SpatialGrid; if (grid == null) continue;
            var map = new int[grid.CellCount]; grids.Add(grid,map); var l = grid.Layout;
            for (int i=0;i<map.Length;i++)
            {
                var source = grid.GetCell(i).SourceCell;
                var owner = source?.Equipment.ThermalState ?? item.ThermalState; int local = source?.Index ?? i;
                l.Coordinates(i,out int x,out int y,out int z);
                if (source != null)
                {
                    var eg = source.Equipment.EquipmentGrid;
                    int sx=local%eg.SizeX, sy=local/eg.SizeX%eg.SizeY, sz=local/(eg.SizeX*eg.SizeY);
                    if (!Same(l.X[x+1]-l.X[x],eg.X[sx+1]-eg.X[sx]) || !Same(l.Y[y+1]-l.Y[y],eg.Y[sy+1]-eg.Y[sy]) || !Same(l.Z[z+1]-l.Z[z],eg.Z[sz+1]-eg.Z[sz]))
                        throw new InvalidOperationException("Nonconforming placed cell: equipment "+source.Equipment.Id+" local "+local+" grid "+i+"; six-neighbour topology invalid");
                }
                map[i] = Add(cells,indices,owner,local,"box "+item.Id+" cell "+i+" ("+x+","+y+","+z+") owner "+(source?.Equipment.Id ?? item.Id)+":"+local);
            }
            foreach (var edge in grid.Edges())
            {
                l.Coordinates(edge.A,out int x,out int y,out int z);
                int axis = edge.B-edge.A==1?0:edge.B-edge.A==18?1:2;
                var coords = axis==0?l.X:axis==1?l.Y:l.Z; int p=axis==0?x:axis==1?y:z;
                // Queen excluder is a zero-thickness permeable face; no calibrated thermal resistance exists.
                faces.Add(new ThermalExchange.Face(map[edge.A],map[edge.B],edge.AreaMm2*1e-6,(coords[p+1]-coords[p])*.0005,(coords[p+2]-coords[p+1])*.0005));
            }
        }
        // Unplaced equipment retains its own closed local network. Hidden box air is excluded.
        foreach (var item in items)
        {
            var g=item.EquipmentGrid; if(g==null || item.SpatialGrid!=null || g.SizeZ==0) continue;
            int count=g.SizeX*g.SizeY*g.SizeZ;
            int present=0; for(int i=0;i<count;i++) if(indices.ContainsKey((item.ThermalState,i))) present++;
            if(present==count) continue;
            if(present!=0) throw new InvalidOperationException("Partially mapped equipment "+item.Id+"; no connections omitted");
            var map=new int[count];
            for(int i=0;i<count;i++) map[i]=Add(cells,indices,item.ThermalState,i,"detached equipment "+item.Id+" cell "+i);
            for(int z=0;z<g.SizeZ;z++) for(int y=0;y<g.SizeY;y++) for(int x=0;x<g.SizeX;x++)
            {
                int i=g.Index(x,y,z); double dx=g.X[x+1]-g.X[x],dy=g.Y[y+1]-g.Y[y],dz=g.Z[z+1]-g.Z[z];
                for(int axis=0;axis<3;axis++)
                {
                    int p=axis==0?x:axis==1?y:z, size=axis==0?g.SizeX:axis==1?g.SizeY:g.SizeZ;
                    if(p+1==size) continue; var a=axis==0?g.X:axis==1?g.Y:g.Z;
                    int j=g.Index(x+(axis==0?1:0),y+(axis==1?1:0),z+(axis==2?1:0));
                    faces.Add(new ThermalExchange.Face(map[i],map[j],(axis==0?dy*dz:axis==1?dx*dz:dx*dy)*1e-6,(a[p+1]-a[p])*.0005,(a[p+2]-a[p+1])*.0005));
                }
            }
        }
        var roots=new List<MovableItem>();
        foreach(var hive in world.Apiary.Hives) roots.Add(hive.Slot.Value);
        for(int y=0;y<world.Apiary.Grid.Height;y++) for(int x=0;x<world.Apiary.Grid.UnlockedWidth;x++) roots.Add(world.Apiary.GetItemSlot(x,y).Value);
        for(int i=0;i<world.Inventory.Capacity;i++) roots.Add(world.Inventory.GetSlot(i).Value);
        roots.Add(world.Hand.Value);
        var seen=new HashSet<MovableItem>();
        foreach(var root in roots) AddStacks(root,seen,grids,faces);
        return new ThermalExchange(cells.ToArray(),faces.ToArray());
    }
    private static bool Same(double a,double b) => Math.Abs(a-b)<1e-9;
    private static int Add(List<ThermalExchange.Cell> cells,Dictionary<(EquipmentThermalState,int),int> map,EquipmentThermalState owner,int index,string label)
    {
        if(map.ContainsKey((owner,index))) throw new InvalidOperationException("Duplicate active owner: "+label);
        int result=cells.Count; map.Add((owner,index),result); cells.Add(new ThermalExchange.Cell(owner,index,label)); return result;
    }
    private static void AddStacks(MovableItem root,HashSet<MovableItem> seen,Dictionary<HiveSpatialGrid,int[]> grids,List<ThermalExchange.Face> faces)
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
                    if(lower!=null) Join(lower,upper,grids,faces);
                    lower=upper;
                }
                AddStacks(part,seen,grids,faces);
            }
        }
        else if(root is ItemData item)
        {
            if(item.TopSlot!=null) AddStacks(item.TopSlot.Value,seen,grids,faces);
            if(item.InteriorSlots!=null) for(int i=0;i<item.InteriorSlots.Capacity;i++) AddStacks(item.InteriorSlots.GetSlot(i).Value,seen,grids,faces);
        }
    }
    private static void Join(HiveSpatialGrid lower,HiveSpatialGrid upper,Dictionary<HiveSpatialGrid,int[]> grids,List<ThermalExchange.Face> faces)
    {
        var a=lower.Layout; var b=upper.Layout;
        if(!a.X.SequenceEqual(b.X)||!a.Y.SequenceEqual(b.Y)) throw new InvalidOperationException("Nonconforming stack interface "+lower.Box.Id+" / "+upper.Box.Id+"; six-direction index invalid. Overlap-face construction required; no partial graph accepted.");
        for(int y=0;y<52;y++) for(int x=0;x<18;x++)
            faces.Add(new ThermalExchange.Face(grids[lower][a.Index(x,y,a.SizeZ-1)],grids[upper][b.Index(x,y,0)],(a.X[x+1]-a.X[x])*(a.Y[y+1]-a.Y[y])*1e-6,(a.Z[a.SizeZ]-a.Z[a.SizeZ-1])*.0005,(b.Z[1]-b.Z[0])*.0005));
    }
}
