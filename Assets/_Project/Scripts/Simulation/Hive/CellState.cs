using System;
namespace HoneyComb.Simulation.Hive
{
    // Existing numeric values stay stable; Nectar is newly reserved.
    public enum CellContentType : byte { Empty=0, Brood=1, Honey=2, Pollen=3, Nectar=4 }
    public readonly struct CellState
    {
        public byte Construction { get; }
        public CellContentType Content { get; }
        public long OccupantBeeId { get; }
        public bool IsBuilt => Construction==100;
        public CellState(int construction,CellContentType content=CellContentType.Empty,long occupantBeeId=0)
        {
            if(construction<0 || construction>100) throw new ArgumentOutOfRangeException(nameof(construction));
            if(content<CellContentType.Empty || content>CellContentType.Nectar) throw new ArgumentOutOfRangeException(nameof(content));
            if(construction<100 && content!=CellContentType.Empty)
                throw new ArgumentException("An unfinished cell cannot contain brood or food.",nameof(content));
            if((content==CellContentType.Brood && occupantBeeId<=0) || (content!=CellContentType.Brood && occupantBeeId!=0))
                throw new ArgumentException("Brood requires one Bee ID; other content cannot have an occupant.",nameof(occupantBeeId));
            Construction=(byte)construction;Content=content;OccupantBeeId=occupantBeeId;
        }
    }
}
