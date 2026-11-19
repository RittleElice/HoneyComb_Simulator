public enum FrameSide
{
    Front,
    Back
}

public sealed class FrameData
{
    public const int DefaultWidth = 30;
    public const int DefaultHeight = 15;

    // Preserve the former 85 x 39 frame totals when reducing logical cells.
    public const float CellAreaScale = (85f * 39f) / (DefaultWidth * DefaultHeight);

    public FrameSurface Front { get; }
    public FrameSurface Back { get; }

    // 현재 소비장이 향하고 있는 면
    public FrameSide FacingSide { get; private set; }
        = FrameSide.Front;

    public int Width => Front.Width;
    public int Height => Front.Height;

    public int TotalCellCount =>
        Front.CellCount + Back.CellCount;

    public FrameData(
        int width = DefaultWidth,
        int height = DefaultHeight)
    {
        Front = new FrameSurface(width, height);
        Back = new FrameSurface(width, height);
    }

    public FrameSurface GetSurface(FrameSide side)
    {
        return side == FrameSide.Front
            ? Front
            : Back;
    }

    public FrameSurface GetFacingSurface()
    {
        return GetSurface(FacingSide);
    }

    public void SetFacingSide(FrameSide side)
    {
        FacingSide = side;
    }

    public void Flip()
    {
        FacingSide =
            FacingSide == FrameSide.Front
                ? FrameSide.Back
                : FrameSide.Front;
    }

    public int CountCompletedCells()
    {
        return Front.CountCompletedCells() +
               Back.CountCompletedCells();
    }

    public float GetAverageConstruction()
    {
        return (
            Front.GetAverageConstruction() +
            Back.GetAverageConstruction()
        ) * 0.5f;
    }
}