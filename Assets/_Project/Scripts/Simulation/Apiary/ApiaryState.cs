using System;
using System.Collections.Generic;
using HoneyComb.Simulation.Hive;
namespace HoneyComb.Simulation.Apiary
{
    public sealed class ApiaryState
    {
        // Allocation guard for the prototype, not a gameplay capacity rule.
        public const int MaxGridCells = 65536;
        private readonly ApiaryCell[,] cells;
        private readonly List<HiveState> hives = new List<HiveState>();
        private long nextHiveNumber=1;
        private readonly int frameWidth,frameHeight;
        public string Id { get; }
        public string Name { get; }
        public int Width { get; }
        public int Height { get; }
        public int CellCount => Width * Height;
        public int HiveCount => hives.Count;
        public IReadOnlyList<HiveState> Hives { get; }
        public ApiaryState(string id,string name,int width,int height,int initialHiveCount,int frameWidth=Frame.DefaultWidth,int frameHeight=Frame.DefaultHeight)
        {
            if(string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Apiary ID is required.",nameof(id));
            if(string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Apiary name is required.",nameof(name));
            if(width<=0 || height<=0 || (long)width*height>MaxGridCells)
                throw new ArgumentOutOfRangeException(nameof(width),"Grid dimensions must be positive and contain at most 65536 cells.");
            if(initialHiveCount<0 || initialHiveCount>(long)width*height) throw new ArgumentOutOfRangeException(nameof(initialHiveCount));
            Frame.ValidateDimensions(frameWidth,frameHeight);
            this.frameWidth=frameWidth;this.frameHeight=frameHeight;
            Id=id.Trim(); Name=name.Trim(); Width=width; Height=height;
            Hives=hives.AsReadOnly(); cells=new ApiaryCell[width,height];
            for(int y=0;y<height;y++) for(int x=0;x<width;x++) cells[x,y]=new ApiaryCell(x,y);
            // Deterministic initial placement: left to right, bottom to top.
            for(int i=0;i<initialHiveCount;i++) TryCreateHive(i%width,i/width,out _);
        }
        public ApiaryCell GetCell(int x,int y) => x>=0 && y>=0 && x<Width && y<Height ? cells[x,y] : null;
        public HiveState GetHive(int x,int y) => GetCell(x,y)?.Hive;
        public bool PlaceHive(HiveState hive,int x,int y)
        {
            var cell=GetCell(x,y);
            if(cell==null || cell.Hive!=null || hive==null || hive.PlacementOwner!=null || hives.Exists(h=>h.Id==hive.Id)) return false;
            cell.Hive=hive; hive.PlacementOwner=this; hives.Add(hive); return true;
        }
        public bool TryCreateHive(int x,int y,out HiveState hive)
        {
            hive=null;
            var cell=GetCell(x,y);
            if(cell==null || cell.Hive!=null) return false;
            string id;
            do {
                if(nextHiveNumber==long.MaxValue) return false;
                id=Id+"/hive/"+nextHiveNumber++;
            } while(hives.Exists(h=>h.Id==id));
            var created=new HiveState(id,frameWidth,frameHeight);
            if(!PlaceHive(created,x,y)) return false;
            hive=created; return true;
        }
        public bool RemoveHive(int x,int y)
        {
            var cell=GetCell(x,y);
            if(cell==null || cell.Hive==null || cell.Hive.HasColony || cell.Hive.HasResidentBees) return false;
            var hive=cell.Hive; cell.Hive=null; hive.PlacementOwner=null; hives.Remove(hive); return true;
        }
        public bool MoveHive(int fromX,int fromY,int toX,int toY)
        {
            var from=GetCell(fromX,fromY); var to=GetCell(toX,toY);
            if(from==null || to==null || from.Hive==null || to.Hive!=null) return false;
            to.Hive=from.Hive; from.Hive=null; return true;
        }
    }
}
