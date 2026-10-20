using System;
using System.Collections.Generic;
namespace HoneyComb.Simulation.Hive
{
    public enum HiveStatus { Active }
    public sealed class HiveState
    {
        private readonly List<Super> supers=new List<Super>();
        private long nextSuper=1,nextFrame=1;
        private readonly int frameWidth,frameHeight;
        public string Id { get; }
        public HiveStatus Status { get; } = HiveStatus.Active;
        internal object PlacementOwner { get; set; }
        public HiveBase Base { get; } = new HiveBase();
        // Index 0 is bottom; AddSuper appends at the top. IDs are stable after removal.
        public IReadOnlyList<Super> Supers { get; }
        public HiveState(string id,int frameWidth=Frame.DefaultWidth,int frameHeight=Frame.DefaultHeight)
        {
            if(string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Hive ID is required.",nameof(id));
            Frame.ValidateDimensions(frameWidth,frameHeight);
            this.frameWidth=frameWidth;this.frameHeight=frameHeight;
            Id=id; Supers=supers.AsReadOnly();
        }
        public Super AddSuper()
        {
            var super=new Super(Id+"/super/"+nextSuper);
            nextSuper=checked(nextSuper+1);supers.Add(super);return super;
        }
        public Super GetSuper(string id) => supers.Find(s=>s.Id==id);
        public bool RemoveSuper(string id)
        {
            var super=GetSuper(id);
            if(super==null || !super.IsEmpty) return false;
            return supers.Remove(super);
        }
        public bool InsertFrame(string superId,int index,Frame frame)
        {
            var slot=GetSuper(superId)?.GetSlot(index);
            if(slot==null || slot.Frame!=null || frame==null || frame.Owner!=null || ContainsFrame(frame.Id)) return false;
            slot.Frame=frame;frame.Owner=slot;return true;
        }
        public bool TryCreateFrame(string superId,int index,out Frame frame)
        {
            frame=null;var slot=GetSuper(superId)?.GetSlot(index);
            if(slot==null || slot.Frame!=null) return false;
            string id;
            do { if(nextFrame==long.MaxValue) return false;id=Id+"/frame/"+nextFrame++; } while(ContainsFrame(id));
            var created=new Frame(id,frameWidth,frameHeight);
            if(!InsertFrame(superId,index,created)) return false;
            frame=created;return true;
        }
        private bool ContainsFrame(string id)
        {
            foreach(var super in supers) foreach(var slot in super.Slots) if(slot.Frame?.Id==id) return true;
            return false;
        }
        public bool RemoveFrame(string superId,int index)
        {
            var slot=GetSuper(superId)?.GetSlot(index);
            if(slot?.Frame==null) return false;
            slot.Frame.Owner=null;slot.Frame=null;return true;
        }
        public bool MoveFrame(string fromSuper,int fromIndex,string toSuper,int toIndex)
            => MoveFrameTo(fromSuper,fromIndex,this,toSuper,toIndex);
        public bool MoveFrameTo(string fromSuper,int fromIndex,HiveState destination,string toSuper,int toIndex)
        {
            var from=GetSuper(fromSuper)?.GetSlot(fromIndex);
            var to=destination?.GetSuper(toSuper)?.GetSlot(toIndex);
            if(from?.Frame==null || to==null || to.Frame!=null || from.Frame.Owner!=from) return false;
            if(!ReferenceEquals(this,destination) && destination.ContainsFrame(from.Frame.Id)) return false;
            // Validate everything before touching either hive. No remove/insert gap.
            var frame=from.Frame;
            from.Frame=null;to.Frame=frame;frame.Owner=to;return true;
        }
    }
}
