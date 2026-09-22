using System;
namespace HoneyComb.Simulation.Hive
{
    public enum FrameSide { Front, Back }
    public sealed class FrameSurface
    {
        // Row-major contiguous values. No object allocation per cell.
        private readonly CellState[] cells;
        public FrameSide Side { get; }
        public int Width { get; }
        public int Height { get; }
        public int CellCount => cells.Length;
        private long constructionTotal;
        public int CompletedCellCount { get; private set; }
        public double AverageConstruction => (double)constructionTotal/CellCount;
        internal FrameSurface(FrameSide side,int width,int height)
        {
            Side=side;Width=width;Height=height;cells=new CellState[width*height];
        }
        private bool InBounds(int x,int y) => x>=0 && y>=0 && x<Width && y<Height;
        // Returns a copy; changes must go through TrySetCell.
        public CellState? GetCell(int x,int y) => InBounds(x,y) ? cells[y*Width+x] : (CellState?)null;
        public bool TryGetCell(int x,int y,out CellState state)
        {
            state=default;
            if(!InBounds(x,y)) return false;
            state=cells[y*Width+x];return true;
        }
        public bool TrySetCell(int x,int y,CellState state)
        {
            if(!InBounds(x,y) || cells[y*Width+x].OccupantBeeId!=0 || state.OccupantBeeId!=0) return false;
            return SetCellCore(x,y,state);
        }
        private bool SetCellCore(int x,int y,CellState state)
        {
            int index=y*Width+x;var old=cells[index];
            constructionTotal+=state.Construction-old.Construction;
            CompletedCellCount+=(state.IsBuilt ? 1 : 0)-(old.IsBuilt ? 1 : 0);
            cells[index]=state;return true;
        }
        public bool TrySetConstruction(int x,int y,int construction)
        {
            if(!TryGetCell(x,y,out var old) || construction<0 || construction>100
                || (construction<100 && old.Content!=CellContentType.Empty)) return false;
            return old.OccupantBeeId!=0 ? construction==100 : TrySetCell(x,y,new CellState(construction,old.Content));
        }
        public bool TrySetContent(int x,int y,CellContentType content)
        {
            if(content==CellContentType.Brood || !TryGetCell(x,y,out var old) || old.OccupantBeeId!=0 || content<CellContentType.Empty || content>CellContentType.Nectar
                || (!old.IsBuilt && content!=CellContentType.Empty)) return false;
            return TrySetCell(x,y,new CellState(old.Construction,content));
        }
        internal bool TryOccupyBrood(int x,int y,long beeId)
        {
            if(beeId<=0 || !TryGetCell(x,y,out var old) || !old.IsBuilt || old.Content!=CellContentType.Empty) return false;
            return SetCellCore(x,y,new CellState(100,CellContentType.Brood,beeId));
        }
        internal bool TryReleaseBrood(int x,int y,long beeId)
        {
            if(beeId<=0 || !TryGetCell(x,y,out var cell) || cell.Content!=CellContentType.Brood || cell.OccupantBeeId!=beeId) return false;
            return SetCellCore(x,y,new CellState(cell.Construction));
        }
        internal bool CanSetAllConstruction(int construction)
        {
            if(construction<0 || construction>100) return false;
            if(construction<100) foreach(var cell in cells) if(cell.Content!=CellContentType.Empty) return false;
            return true;
        }
        public bool TrySetAllConstruction(int construction)
        {
            if(!CanSetAllConstruction(construction)) return false;
            for(int i=0;i<cells.Length;i++) cells[i]=new CellState(construction,cells[i].Content,cells[i].OccupantBeeId);
            constructionTotal=(long)construction*CellCount;
            CompletedCellCount=construction==100 ? CellCount : 0;
            return true;
        }    }
    public sealed class Frame
    {
        public const int DefaultWidth=90, DefaultHeight=40;
        public string Id { get; }
        internal FrameSlot Owner { get; set; }
        public int ResidentBeeCount { get; internal set; }
        internal bool TryOccupyBrood(FrameSide side,int x,int y,long beeId) => GetSurface(side)?.TryOccupyBrood(x,y,beeId) ?? false;
        public FrameSurface Front { get; }
        public FrameSurface Back { get; }
        public int TotalCellCount => Front.CellCount+Back.CellCount;
        public int CompletedCellCount => Front.CompletedCellCount+Back.CompletedCellCount;
        public double AverageConstruction => (Front.AverageConstruction*Front.CellCount+Back.AverageConstruction*Back.CellCount)/TotalCellCount;
        public static void ValidateDimensions(int width,int height)
        {
            if(width<=0 || height<=0 || (long)width*height>65536)
                throw new ArgumentOutOfRangeException(nameof(width),"Each frame face must contain between 1 and 65536 cells.");
        }
        public Frame(string id,int width=DefaultWidth,int height=DefaultHeight)
        {
            if(string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Frame ID is required.",nameof(id));
            ValidateDimensions(width,height);Id=id;
            Front=new FrameSurface(FrameSide.Front,width,height);Back=new FrameSurface(FrameSide.Back,width,height);
        }
        public FrameSurface GetSurface(FrameSide side) => side==FrameSide.Front ? Front : side==FrameSide.Back ? Back : null;
        public CellState? GetCell(FrameSide side,int x,int y) => GetSurface(side)?.GetCell(x,y);
        public bool TryGetCell(FrameSide side,int x,int y,out CellState state)
        {
            state=default;var surface=GetSurface(side);
            return surface!=null && surface.TryGetCell(x,y,out state);
        }
        public bool TrySetCell(FrameSide side,int x,int y,CellState state) => GetSurface(side)?.TrySetCell(x,y,state) ?? false;
        public bool TrySetConstruction(FrameSide side,int x,int y,int construction) => GetSurface(side)?.TrySetConstruction(x,y,construction) ?? false;
        public bool TrySetContent(FrameSide side,int x,int y,CellContentType content) => GetSurface(side)?.TrySetContent(x,y,content) ?? false;
        public bool TrySetAllConstruction(int construction)
        {
            // Validate both sides first: no partial edits when either side contains food/brood.
            if(!Front.CanSetAllConstruction(construction) || !Back.CanSetAllConstruction(construction)) return false;
            Front.TrySetAllConstruction(construction);Back.TrySetAllConstruction(construction);return true;
        }
    }
}
