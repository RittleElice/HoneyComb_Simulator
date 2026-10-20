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
            if(!InBounds(x,y)) return false;
            cells[y*Width+x]=state;return true;
        }
    }
    public sealed class Frame
    {
        public const int DefaultWidth=90, DefaultHeight=40;
        public string Id { get; }
        internal FrameSlot Owner { get; set; }
        public FrameSurface Front { get; }
        public FrameSurface Back { get; }
        public int TotalCellCount => Front.CellCount+Back.CellCount;
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
    }
}
