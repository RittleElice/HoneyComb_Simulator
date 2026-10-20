using System;
using HoneyComb.Simulation.Hive;
namespace HoneyComb.Simulation.Bees
{
    public enum BeeLocationKind { Outside=0, HiveBase=1, FrameSurface=2, BroodCell=3, ApiaryOutdoor=4 }
    public readonly struct BeeLocation : IEquatable<BeeLocation>
    {
        public BeeLocationKind Kind { get; }
        public string HiveId { get; }
        public string ApiaryId { get; }
        public string RegionId { get; }
        public string FrameId { get; }
        public FrameSide Side { get; }
        public int? X { get; }
        public int? Y { get; }
        private BeeLocation(BeeLocationKind kind,string hiveId,string apiaryId,string regionId,string frameId,FrameSide side,int? x,int? y)
        { Kind=kind;HiveId=hiveId;ApiaryId=apiaryId;RegionId=regionId;FrameId=frameId;Side=side;X=x;Y=y; }
        internal BeeLocation(string frameId,FrameSide side,bool brood,int x,int y)
            : this(brood ? BeeLocationKind.BroodCell : BeeLocationKind.FrameSurface,null,null,null,frameId,side,brood ? x : (int?)null,brood ? y : (int?)null) { }
        public static BeeLocation AtHiveBase(string id) => new BeeLocation(BeeLocationKind.HiveBase,id,null,null,null,default,null,null);
        public static BeeLocation AtApiary(string id,int x,int y) => new BeeLocation(BeeLocationKind.ApiaryOutdoor,null,id,null,null,default,x,y);
        public static BeeLocation AtExternalRegion(string id) => new BeeLocation(BeeLocationKind.Outside,null,null,id,null,default,null,null);
        public static BeeLocation OnFrame(string id,FrameSide side) => new BeeLocation(id,side,false,0,0);
        public static BeeLocation InCell(string id,FrameSide side,int x,int y) => new BeeLocation(id,side,true,x,y);
        public bool Equals(BeeLocation other) => Kind==other.Kind && HiveId==other.HiveId && ApiaryId==other.ApiaryId
            && RegionId==other.RegionId && FrameId==other.FrameId && Side==other.Side && X==other.X && Y==other.Y;
        public override bool Equals(object other) => other is BeeLocation location && Equals(location);
        public override int GetHashCode()
        {
            unchecked { int hash=(int)Kind;hash=hash*31+(HiveId?.GetHashCode()??0);hash=hash*31+(ApiaryId?.GetHashCode()??0);
                hash=hash*31+(RegionId?.GetHashCode()??0);hash=hash*31+(FrameId?.GetHashCode()??0);hash=hash*31+(int)Side;
                hash=hash*31+X.GetHashCode();return hash*31+Y.GetHashCode(); }
        }
        public override string ToString()
        {
            switch(Kind) {
                case BeeLocationKind.HiveBase:return "Base: "+HiveId;
                case BeeLocationKind.ApiaryOutdoor:return $"Apiary: {ApiaryId} ({X},{Y})";
                case BeeLocationKind.Outside:return "Outside: "+RegionId;
                case BeeLocationKind.FrameSurface:return $"Frame: {FrameId} / {Side}";
                default:return $"Cell: {FrameId} / {Side} ({X},{Y})";
            }
        }
    }
}
