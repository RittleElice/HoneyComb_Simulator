using System;
using System.Collections.Generic;
using HoneyComb.Simulation.Apiary;
using HoneyComb.Simulation.Hive;
using HoneyComb.Simulation.Environment;
namespace HoneyComb.Simulation.Bees
{
    public sealed class BeePopulation
    {
        private readonly ApiaryState apiary;
        private readonly List<ColonyState> colonies=new List<ColonyState>();
        private readonly BeeView bees;
        public BeeStore Store { get; } = new BeeStore();
        private readonly BeeLifecycleSettings lifecycle;
        private long lastHour;
        public long CurrentHour => lastHour;
        private readonly List<BeeState> pending=new List<BeeState>();
        private sealed class BeeBucket
        {
            private readonly BeeView items;
            public IReadOnlyList<BeeState> View => items;
            public BeeBucket(BeeStore store) { items=new BeeView(store); }
            public void Add(BeeState bee) => items.Add(bee);
            public void Remove(BeeState bee) => items.Remove(bee);
        }
        private static readonly IReadOnlyList<BeeState> EmptyBees=Array.AsReadOnly(Array.Empty<BeeState>());
        private readonly Dictionary<string,BeeBucket> byFrame=new Dictionary<string,BeeBucket>();
        private readonly Dictionary<(string,FrameSide),BeeBucket> bySurface=new Dictionary<(string,FrameSide),BeeBucket>();
        private readonly Dictionary<BeeLocation,BeeBucket> byLocation=new Dictionary<BeeLocation,BeeBucket>();
        private long nextBeeId=1,nextColonyId=1;
        public IReadOnlyList<ColonyState> Colonies { get; }
        public IReadOnlyList<BeeState> Bees { get; }
        public ExternalRegionState ExternalRegion { get; }
        public BeePopulation(ApiaryState apiary,ExternalRegionState externalRegion=null,BeeLifecycleSettings lifecycle=null,long initialHour=0)
        {
            this.apiary=apiary ?? throw new ArgumentNullException(nameof(apiary));
            ExternalRegion=externalRegion ?? new ExternalRegionState("outside-1","Outside environment");
            if(initialHour<0) throw new ArgumentOutOfRangeException(nameof(initialHour));
            this.lifecycle=lifecycle??BeeLifecycleSettings.Default;lastHour=initialHour;
            bees=new BeeView(Store);Colonies=colonies.AsReadOnly();Bees=bees;
        }
        public bool TryGetBee(long beeId,out BeeState bee) => Store.TryGet(beeId,out bee);
        public IReadOnlyList<BeeState> GetBeesOnFrame(string frameId)
            => frameId!=null && byFrame.TryGetValue(frameId,out var bucket) ? bucket.View : EmptyBees;
        // Includes adults on the surface and brood inside cells on that side.
        public IReadOnlyList<BeeState> GetBeesOnSurface(string frameId,FrameSide side)
            => frameId!=null && bySurface.TryGetValue((frameId,side),out var bucket) ? bucket.View : EmptyBees;
        public IReadOnlyList<BeeState> GetBeesAt(BeeLocation location)
            => byLocation.TryGetValue(location,out var bucket) ? bucket.View : EmptyBees;
        public IReadOnlyList<BeeState> GetBeesInBase(string hiveId) => GetBeesAt(BeeLocation.AtHiveBase(hiveId));
        public IReadOnlyList<BeeState> GetBeesOutdoors(int x,int y) => GetBeesAt(BeeLocation.AtApiary(apiary.Id,x,y));
        public IReadOnlyList<BeeState> GetBeesOutside() => GetBeesAt(BeeLocation.AtExternalRegion(ExternalRegion.Id));
        public BeeState? GetBeeInCell(Frame frame,FrameSide side,int x,int y)
        {
            if(frame==null || !frame.TryGetCell(side,x,y,out var cell) || cell.OccupantBeeId==0 || !TryGetBee(cell.OccupantBeeId,out var bee)) return null;
            return bee.Location.Equals(BeeLocation.InCell(frame.Id,side,x,y)) ? bee : null;
        }
        private BeeBucket Bucket<TKey>(Dictionary<TKey,BeeBucket> map,TKey key)
        {
            if(!map.TryGetValue(key,out var bucket)) { bucket=new BeeBucket(Store);map.Add(key,bucket); }
            return bucket;
        }
        private void AddLocation(BeeState bee,Frame frame,HiveState baseHive)
        {
            Bucket(byLocation,bee.Location).Add(bee);
            if(frame!=null)
            {
                Bucket(byFrame,frame.Id).Add(bee);Bucket(bySurface,(frame.Id,bee.Location.Side)).Add(bee);frame.ResidentBeeCount++;
            }
            if(baseHive!=null) baseHive.Base.ResidentBeeCount++;
        }
        private void RemoveLocation(BeeState bee,Frame frame,HiveState baseHive)
        {
            byLocation[bee.Location].Remove(bee);
            if(frame!=null) { byFrame[frame.Id].Remove(bee);bySurface[(frame.Id,bee.Location.Side)].Remove(bee);frame.ResidentBeeCount--; }
            if(baseHive!=null) baseHive.Base.ResidentBeeCount--;
        }
        public ColonyState GetColony(string hiveId) => colonies.Find(c=>c.HomeHiveId==hiveId);
        public HiveState FindHive(string hiveId)
        { foreach(var hive in apiary.Hives) if(hive.Id==hiveId) return hive;return null; }
        private Frame FindFrame(string id,out HiveState physicalHive)
        {
            physicalHive=null;if(id==null) return null;
            foreach(var hive in apiary.Hives) foreach(var super in hive.Supers) foreach(var slot in super.Slots)
                if(slot.Frame?.Id==id) { physicalHive=hive;return slot.Frame; }
            return null;
        }
        public HiveState FindPhysicalHive(string frameId) { FindFrame(frameId,out var hive);return hive; }
        public HiveState FindPhysicalHive(BeeLocation location)
            => location.Kind==BeeLocationKind.HiveBase ? FindHive(location.HiveId) : FindPhysicalHive(location.FrameId);
        private bool Resolve(BeeLocation location,out Frame frame,out HiveState baseHive)
        {
            frame=null;baseHive=null;
            switch(location.Kind)
            {
                case BeeLocationKind.HiveBase: baseHive=FindHive(location.HiveId);return baseHive!=null;
                case BeeLocationKind.ApiaryOutdoor: return location.ApiaryId==apiary.Id && location.X.HasValue && location.Y.HasValue
                    && apiary.GetCell(location.X.Value,location.Y.Value)!=null;
                case BeeLocationKind.Outside: return location.RegionId==ExternalRegion.Id;
                case BeeLocationKind.FrameSurface:
                case BeeLocationKind.BroodCell:
                    frame=FindFrame(location.FrameId,out _);
                    if(frame==null || frame.GetSurface(location.Side)==null) return false;
                    return location.Kind==BeeLocationKind.FrameSurface || (location.X.HasValue && location.Y.HasValue
                        && frame.GetCell(location.Side,location.X.Value,location.Y.Value).HasValue);
                default:return false;
            }
        }
        public bool TryMoveAdult(long beeId,BeeLocation destination,out string error)
        {
            error=null;
            if(!TryGetBee(beeId,out var bee) || bee.Development!=BeeDevelopment.Adult || destination.Kind==BeeLocationKind.BroodCell)
            { error="Only adults can use manual movement; brood remains in its cell.";return false; }
            if(!Resolve(destination,out var targetFrame,out var targetBase) || !Resolve(bee.Location,out var oldFrame,out var oldBase))
            { error="Source or destination is no longer available.";return false; }
            if(bee.Location.Equals(destination)) return true;
            // All validation precedes mutation. Colony identity is deliberately unchanged.
            RemoveLocation(bee,oldFrame,oldBase);bee.MoveTo(destination);Store.Update(bee);AddLocation(bee,targetFrame,targetBase);return true;
        }
        public bool TryAddAdult(HiveState colonyHome,BeeLocation destination,BeeSpawnSettings settings,long hour,out BeeState bee,out string error)
        {
            bee=default;error=null;
            if(settings.Development!=BeeDevelopment.Adult || destination.Kind==BeeLocationKind.BroodCell)
            { error="Only adults can be placed outside brood cells.";return false; }
            return TryCreate(colonyHome,destination,settings,hour,out bee,out error);
        }
        public bool TryAddBee(HiveState hive,Frame frame,FrameSide side,int x,int y,BeeSpawnSettings settings,long hour,out BeeState bee,out string error)
        {
            bee=default;error=null;
            if(hive==null || frame==null || !ReferenceEquals(FindFrame(frame.Id,out var physicalHive),frame) || !ReferenceEquals(physicalHive,hive))
            { error="Select an installed Frame in this hive.";return false; }
            var location=new BeeLocation(frame.Id,side,settings.Development!=BeeDevelopment.Adult,x,y);
            return TryCreate(hive,location,settings,hour,out bee,out error);
        }
        private bool TryCreate(HiveState home,BeeLocation location,BeeSpawnSettings settings,long hour,out BeeState bee,out string error)
        {
            bee=default;error=null;
            if(home==null || !ReferenceEquals(FindHive(home.Id),home) || hour<lastHour || !Resolve(location,out var frame,out var baseHive))
            { error="Choose a valid colony home and destination.";return false; }
            bool brood=settings.Development!=BeeDevelopment.Adult;
            if(brood && (location.Kind!=BeeLocationKind.BroodCell || !frame.TryGetCell(location.Side,location.X.Value,location.Y.Value,out var cell)
                || !cell.IsBuilt || cell.Content!=CellContentType.Empty))
            { error="Brood needs a completed, empty cell.";return false; }
            if(nextBeeId==long.MaxValue || nextColonyId==long.MaxValue) { error="ID range exhausted.";return false; }
            var colony=GetColony(home.Id);string colonyId=colony?.Id ?? apiary.Id+"/colony/"+nextColonyId;
            bee=new BeeState(nextBeeId,colonyId,settings.Caste,settings.Development,settings.Health,hour,location,lifecycle.For(settings.Caste));
            if(brood && !frame.TryOccupyBrood(location.Side,location.X.Value,location.Y.Value,bee.Id))
            { bee=default;error="Cell unavailable.";return false; }
            if(colony==null) { colony=new ColonyState(colonyId,home.Id,Store);colonies.Add(colony);nextColonyId++;home.HasColony=true; }
            nextBeeId++;Store.Add(bee);bees.Add(bee);colony.Add(bee);AddLocation(bee,frame,baseHive);return true;
        }
        // Explicit debug readiness; newly emerged queens are not assumed to have mated.
        public bool SetQueenLaying(long id,bool enabled)
        {
            if(!TryGetBee(id,out var bee)||bee.Caste!=BeeCaste.Queen||bee.Development!=BeeDevelopment.Adult) return false;
            bee.SetLaying(enabled);Store.Update(bee);return true;
        }
        public void TickHour(long hour)
        {
            if(lastHour==long.MaxValue || hour!=lastHour+1) throw new ArgumentOutOfRangeException(nameof(hour),"Deliver consecutive hourly ticks.");
            pending.Clear();
            // Read phase: no index or cell changes while enumerating.
            foreach(var bee in Bees)
            {
                if(bee.Development==BeeDevelopment.Adult || bee.CreatedHour>=hour) continue;
                var next=bee;next.Advance(lifecycle.ProgressPerHour,hour);pending.Add(next);
            }
            foreach(var next in pending)
            {
                Store.TryGet(next.Id,out var old);
                var updated=next;
                if(next.Development==BeeDevelopment.Adult)
                {
                    if(!Resolve(old.Location,out var frame,out _) || !frame.GetSurface(old.Location.Side).TryReleaseBrood(old.Location.X.Value,old.Location.Y.Value,old.Id))
                        throw new InvalidOperationException("Brood occupancy is inconsistent.");
                    // Still on the same frame and face; keep those indexes and counts untouched.
                    byLocation[old.Location].Remove(old);
                    updated.MoveTo(BeeLocation.OnFrame(frame.Id,old.Location.Side));
                    Store.Update(updated);Bucket(byLocation,updated.Location).Add(updated);
                }
                else Store.Update(updated);
            }
            lastHour=hour;
            // Births happen after development, so a newborn never develops in its birth tick.
            if(lifecycle.AutomaticLaying)
            {
                int count=Bees.Count;
                for(int i=0;i<count;i++)
                {
                    var queen=Bees[i];
                    if(!queen.CanLayEggs || queen.CreatedHour>=hour) continue;
                    for(int n=0;n<lifecycle.EggsPerQueenHour;n++) if(!TryLayEgg(queen.Id,hour,out _,out _)) break;
                }
            }
        }
        public bool TryLayEgg(long queenId,long hour,out BeeState egg,out string error)
        {
            egg=default;error="A ready adult queen in a hive and a completed empty cell are required.";
            if(hour!=lastHour || !TryGetBee(queenId,out var queen)||!queen.CanLayEggs||queen.Caste!=BeeCaste.Queen||queen.Development!=BeeDevelopment.Adult) return false;
            var hive=FindPhysicalHive(queen.Location);if(hive==null) return false;
            var colony=colonies.Find(c=>c.Id==queen.ColonyId);var home=colony==null ? null : FindHive(colony.HomeHiveId);
            if(home==null) return false;
            foreach(var super in hive.Supers) foreach(var slot in super.Slots)
            {
                var frame=slot.Frame;if(frame==null) continue;
                for(int side=0;side<2;side++)
                {
                    var surface=frame.GetSurface((FrameSide)side);
                    for(int y=0;y<surface.Height;y++) for(int x=0;x<surface.Width;x++)
                    {
                        surface.TryGetCell(x,y,out var cell);
                        if(!cell.IsBuilt || cell.Content!=CellContentType.Empty) continue;
                        return TryCreate(home,BeeLocation.InCell(frame.Id,(FrameSide)side,x,y),
                            new BeeSpawnSettings(BeeCaste.Worker,BeeDevelopment.Egg,lifecycle.NewbornHealth),hour,out egg,out error);
                    }
                }
            }
            return false;
        }
    }
}
