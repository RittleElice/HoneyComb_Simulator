using System;
using System.Collections.Generic;

// Serialized separately as a sparse extension: only bees currently in transit allocate this state.
public sealed class CycleApiaryFlight
{
    public int x,y,nextX,nextY,phase; // 1: departure, 2: forage return, 3: another interior
    public int targetEquipment,targetX,targetY;
    public CyclePoint interior;
    public CycleEnd result;
    public double remainingSeconds,elapsedSeconds,stepSeconds;
    public bool waiting;
}
public sealed partial class BeeCycleRuntime
{
    readonly Dictionary<int,(int x,int y)> apiaryEquipment=new Dictionary<int,(int,int)>();
    readonly Dictionary<int,BeeGridPosition> apiaryEntries=new Dictionary<int,BeeGridPosition>();
    readonly Dictionary<(int,int,int,int,int),(int x,int y)[]> arrivalRings=new Dictionary<(int,int,int,int,int),(int,int)[]>();
    void RefreshApiaryPlacement()
    {
        apiaryEquipment.Clear();apiaryEntries.Clear();var grid=World.Apiary.Grid;
        for(int x=0;x<grid.UnlockedWidth;x++)for(int y=0;y<grid.Height;y++)MapApiaryItem(grid.GetItemSlot(x,y)?.Value,x,y);
        foreach(var pair in entrances){var air=Air(pair.Value);if(air?.Grid==null)continue;
            apiaryEntries[air.Grid.Box.Id]=pair.Value;
            foreach(var region in regions)if(region.group==pair.Key){apiaryEntries[region.box]=pair.Value;apiaryEntries[region.frame.Id]=pair.Value;}
        }
        var topology=World.BeeLocations.Topology(World);
        foreach(var p in topology.Positions)if(apiaryEquipment.ContainsKey(p.EquipmentId)&&!apiaryEntries.ContainsKey(p.EquipmentId)&&entrances.TryGetValue(topology.SupportCell(p).Group,out var entry))apiaryEntries[p.EquipmentId]=entry;
    }
    void MapApiaryItem(MovableItem root,int x,int y)
    {
        if(root is ItemAssemblyData assembly){foreach(var part in assembly.Parts)MapApiaryItem(part,x,y);return;}
        if(!(root is ItemData item))return;
        apiaryEquipment[item.Id]=(x,y);
        if(item.InteriorSlots!=null)for(int i=0;i<item.InteriorSlots.Capacity;i++)MapApiaryItem(item.InteriorSlots.GetSlot(i).Value,x,y);
        if(item.TopSlot!=null)MapApiaryItem(item.TopSlot.Value,x,y);
    }
    bool AllowedSurfaceEdge(BeeGridPosition a,BeeGridPosition b)
        =>a.EquipmentId==b.EquipmentId||(apiaryEquipment.ContainsKey(a.EquipmentId)&&apiaryEquipment.ContainsKey(b.EquipmentId));
    (int x,int y) ReturnLanding(int targetX,int targetY)
    {
        var grid=World.Apiary.Grid;int width=grid.UnlockedWidth,height=grid.Height;
        int max=Math.Max(targetX,width-1-targetX)+Math.Max(targetY,height-1-targetY),distance=0;
        // Stopping at the boundary folds the entire geometric tail into the farthest ring.
        while(distance<max&&Random()>=Settings.apiaryArrivalStopPercent/100d)distance++;
        var key=(targetX,targetY,distance,width,height);
        if(!arrivalRings.TryGetValue(key,out var ring)){
            var points=new List<(int,int)>();
            for(int x=Math.Max(0,targetX-distance);x<=Math.Min(width-1,targetX+distance);x++){
                int dy=distance-Math.Abs(x-targetX),low=targetY-dy,high=targetY+dy;
                if(low>=0&&low<height)points.Add((x,low));
                if(high!=low&&high>=0&&high<height)points.Add((x,high));
            }
            ring=points.ToArray();if(arrivalRings.Count>=256)arrivalRings.Clear();arrivalRings.Add(key,ring);
        }
        return ring.Length==1?ring[0]:ring[(int)(Random()*ring.Length)];
    }
    bool BeginForageReturn(CycleBee bee,CycleEnd result)
    {
        if(!apiaryEquipment.TryGetValue(bee.homeBox,out var target))return false;
        var landed=ReturnLanding(target.x,target.y);
        bee.apiaryFlight=new CycleApiaryFlight{x=landed.x,y=landed.y,nextX=landed.x,nextY=landed.y,phase=2,targetEquipment=bee.homeBox,result=result,interior=bee.returnPoint};
        bee.outside=true;bee.directionActive=false;bee.hasWaypoint=false;bee.progress=0;bee.path.Clear();bee.pathIndex=0;
        Counters.apiaryLandings++;Counters.apiaryLandingDistance+=Math.Abs(landed.x-target.x)+Math.Abs(landed.y-target.y);
        double noTime=0;AdvanceApiary(bee,ref noTime);return true;
    }
    bool StartApiary(CycleBee bee,int phase,BeeGridPosition interior,ref double time)
    {
        if(!BeePosition(bee,out var p)||!apiaryEquipment.TryGetValue(p.EquipmentId,out var origin))return false;
        bee.apiaryFlight=new CycleApiaryFlight{x=origin.x,y=origin.y,nextX=origin.x,nextY=origin.y,phase=phase,targetEquipment=interior.EquipmentId,interior=new CyclePoint(interior)};
        bee.returnPoint=new CyclePoint(p);
        if(phase==1){var boundary=DepartureBoundary(origin.x,origin.y);bee.apiaryFlight.targetX=boundary.x;bee.apiaryFlight.targetY=boundary.y;}
        bee.outside=true;bee.directionActive=false;bee.hasWaypoint=false;bee.progress=0;bee.path.Clear();bee.pathIndex=0;
        World.BeeLocations.Remove(bee.id);InvalidateBeePosition(bee);
        return AdvanceApiary(bee,ref time);
    }
    (int x,int y) DepartureBoundary(int x,int y)
    {
        var grid=World.Apiary.Grid;int tx=0,ty=y,best=x;
        if(grid.UnlockedWidth-1-x<best){best=grid.UnlockedWidth-1-x;tx=grid.UnlockedWidth-1;ty=y;}
        if(y<best){best=y;tx=x;ty=0;}
        if(grid.Height-1-y<best){tx=x;ty=grid.Height-1;}
        return(tx,ty);
    }
    bool AdvanceApiary(CycleBee bee,ref double time)
    {
        var f=bee.apiaryFlight;if(f==null)return true;
        var grid=World.Apiary.Grid;
        (int x,int y) target;
        if(f.phase==1)target=(f.targetX,f.targetY);
        else if(!apiaryEquipment.TryGetValue(f.targetEquipment,out target)||!apiaryEntries.ContainsKey(f.targetEquipment)){
            f.elapsedSeconds+=time*60;Counters.apiaryWaitSeconds+=time*60;Act(bee,CycleAction.Idle,time);time=0;return false;
        }
        while(true){
            if(f.remainingSeconds<=1e-10&&f.x==target.x&&f.y==target.y){
                Counters.apiaryArrivals++;Counters.apiaryTravelSeconds+=f.elapsedSeconds;
                bee.apiaryFlight=null;
                if(f.phase==1){bee.outsideRemaining=Settings.forageLoadBased!=0?0:F("forageDuration");if(time>0)Act(bee,CycleAction.Outside,time);time=0;return false;}
                var entry=apiaryEntries[f.targetEquipment];
                World.BeeLocations.Set(World,bee.id,entry);InvalidateBeePosition(bee);bee.outside=bee.returning=bee.interruptAfterReturn=false;
                if(f.phase==2)End(bee,f.result);
                return true;
            }
            if(time<=1e-12)return false;
            if(f.remainingSeconds<=1e-10){
                double pick=Random()*100;int dx=0,dy=0;
                f.waiting=pick<Settings.apiaryWaitPercent;
                if(!f.waiting){
                    bool follow=pick<Settings.apiaryWaitPercent+Settings.apiaryFollowDirectionPercent;
                    int count=0,chosen=0;
                    // At most four candidates; no list allocations in the per-bee step.
                    for(int dir=0;dir<4;dir++){
                        int nx=f.x+(dir==0?-1:dir==1?1:0),ny=f.y+(dir==2?-1:dir==3?1:0);
                        if(!grid.IsUnlocked(nx,ny))continue;
                        bool closer=Math.Abs(nx-target.x)+Math.Abs(ny-target.y)<Math.Abs(f.x-target.x)+Math.Abs(f.y-target.y);
                        if(closer!=follow)continue;count++;
                    }
                    if(count==0)f.waiting=true;
                    else{
                        int index=count==1?0:(int)(Random()*count);
                        for(int dir=0;dir<4;dir++){
                            int nx=f.x+(dir==0?-1:dir==1?1:0),ny=f.y+(dir==2?-1:dir==3?1:0);
                            if(!grid.IsUnlocked(nx,ny))continue;
                            bool closer=Math.Abs(nx-target.x)+Math.Abs(ny-target.y)<Math.Abs(f.x-target.x)+Math.Abs(f.y-target.y);
                            if(closer!=follow)continue;if(index--==0){chosen=dir;break;}
                        }
                        dx=chosen==0?-1:chosen==1?1:0;dy=chosen==2?-1:chosen==3?1:0;
                    }
                }
                f.nextX=f.x+dx;f.nextY=f.y+dy;f.remainingSeconds=f.stepSeconds=Settings.apiaryCellMeters/Settings.apiaryFlightMetersPerSecond;
            }
            double seconds=Math.Min(time*60,f.remainingSeconds),minutes=seconds/60;
            f.remainingSeconds-=seconds;f.elapsedSeconds+=seconds;time=Math.Max(0,time-minutes);
            if(f.waiting){Counters.apiaryWaitSeconds+=seconds;Act(bee,CycleAction.Idle,minutes);}
            else{Counters.apiaryMoveSeconds+=seconds;bee.energy=Math.Max(0,bee.energy-F("movementEnergyCost",new CycleInputs{time=minutes}));Act(bee,CycleAction.Move,minutes);}
            if(f.remainingSeconds<=1e-10){f.remainingSeconds=0;if(!f.waiting)Counters.apiarySteps++;f.x=f.nextX;f.y=f.nextY;}
        }
    }
    public bool TryGetApiaryPosition(long id,out int x,out int y)
    {x=y=0;if(!byId.TryGetValue(id,out var bee)||bee.apiaryFlight==null)return false;x=bee.apiaryFlight.x;y=bee.apiaryFlight.y;return true;}
}
