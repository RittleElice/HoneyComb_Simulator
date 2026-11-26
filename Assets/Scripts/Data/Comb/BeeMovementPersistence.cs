using System;
using System.IO;
using System.Collections.Generic;
using System.Threading.Tasks;

public sealed partial class BeeCycleRuntime
{
    HashSet<long> movementReadIds;
    Dictionary<long,byte> movementReadFlags;
    async Task WriteMovementExtension(BinaryWriter w,Func<Task> checkpoint=null)
    {
        int count=0;foreach(var b in Bees)if(b.directionActive||b.apiaryFlight!=null)count++;
        w.Write(count);int visited=0;
        foreach(var b in Bees){
            if(checkpoint!=null&&(visited++&255)==0)await checkpoint();
            if(!b.directionActive&&b.apiaryFlight==null)continue;
            w.Write(b.id);w.Write(b.directionActive);
            if(b.directionActive)CycleBinary.Write(w,typeof(CyclePoint),b.directionTarget);
            w.Write(b.apiaryFlight!=null);
            if(b.apiaryFlight!=null)CycleBinary.Write(w,typeof(CycleApiaryFlight),b.apiaryFlight);
        }
    }
    void ReadMovementExtension(BinaryReader r,bool validateOnly)
    {
        int count=SaveBinary.Count(r,10000000);var seen=new HashSet<long>();var grid=World.Apiary.Grid;
        for(int i=0;i<count;i++){
            long id=r.ReadInt64();
            byId.TryGetValue(id,out var bee);
            if(!seen.Add(id)||(bee==null&&(movementReadIds==null||!movementReadIds.Contains(id))))throw new InvalidDataException("Invalid movement bee reference.");
            byte flags=0;if(bee!=null)flags=(byte)((bee.stage==CycleStage.Dead?1:0)|(bee.outside?2:0));else movementReadFlags.TryGetValue(id,out flags);
            bool direction=r.ReadBoolean();CyclePoint target=direction?(CyclePoint)CycleBinary.Read(r,typeof(CyclePoint),14):default;
            bool flying=r.ReadBoolean();var flight=flying?(CycleApiaryFlight)CycleBinary.Read(r,typeof(CycleApiaryFlight),14):null;
            if((!direction&&!flying)||(flags&1)!=0||(direction&&flying)||(direction&&(flags&2)!=0)||(flying&&(flags&2)==0))throw new InvalidDataException("Invalid movement mode.");
            if(flight!=null){
                if(flight.phase<1||flight.phase>3||flight.targetEquipment<=0||!grid.IsUnlocked(flight.x,flight.y)||!grid.IsUnlocked(flight.nextX,flight.nextY)||flight.remainingSeconds<0||flight.stepSeconds<0||flight.remainingSeconds>flight.stepSeconds+1e-8||flight.elapsedSeconds<0||Math.Abs(flight.nextX-flight.x)+Math.Abs(flight.nextY-flight.y)>1||(flight.phase==1&&!grid.IsUnlocked(flight.targetX,flight.targetY)))throw new InvalidDataException("Invalid apiary flight.");
            }
            if(bee!=null){bee.directionActive=direction;bee.directionTarget=target;bee.apiaryFlight=flight;}
        }
        movementReadIds=null;movementReadFlags=null;
    }
}
