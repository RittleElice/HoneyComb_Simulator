using System;

// Millimetres, origin at the bottom-left of the 420 x 200 comb area.
// Geometry is independent of content and of the equipment's current placement.
public sealed class CombGeometry
{
    public const double AreaWidth = 420, AreaHeight = 200;
    public int Columns { get; }
    public int Rows { get; }
    public double Radius { get; }
    public double StepX => Math.Sqrt(3) * Radius;
    public double StepY => 1.5 * Radius;
    public double Width => StepX * (Columns + (Rows > 1 ? 0.5 : 0));
    public double Height => Radius * (1.5 * (Rows - 1) + 2);
    public CombGeometry(int columns = 84, int rows = 40)
    {
        if (columns <= 0 || rows <= 0) throw new ArgumentOutOfRangeException();
        Columns = columns; Rows = rows;
        Radius = Math.Min(AreaWidth / (Math.Sqrt(3) * (columns + (rows > 1 ? 0.5 : 0))),
            AreaHeight / (1.5 * (rows - 1) + 2));
    }
    public void Center(int column, int row, out double x, out double z)
    {
        if (column < 0 || column >= Columns || row < 0 || row >= Rows) throw new ArgumentOutOfRangeException();
        x = (AreaWidth - Width) / 2 + StepX * (0.5 + column + (row & 1) * 0.5);
        z = (AreaHeight - Height) / 2 + Radius + StepY * row;
    }
    public bool TryFind(double x, double z, out int column, out int row)
    {
        column = row = -1;
        if (x < 0 || x > AreaWidth || z < 0 || z > AreaHeight) return false;
        int nearestRow = (int)Math.Round((z - (AreaHeight - Height) / 2 - Radius) / StepY);
        for (int r = Math.Max(0, nearestRow - 1); r <= Math.Min(Rows - 1, nearestRow + 1); r++)
        {
            int c = (int)Math.Round((x - (AreaWidth - Width) / 2) / StepX - 0.5 - (r & 1) * 0.5);
            if (c < 0 || c >= Columns) continue;
            Center(c, r, out double cx, out double cz);
            double dx = Math.Abs(x - cx), dz = Math.Abs(z - cz);
            if (dx <= StepX / 2 + 1e-9 && dz + dx / Math.Sqrt(3) <= Radius + 1e-9)
            { column = c; row = r; return true; }
        }
        return false;
    }
}

public readonly struct CombCellId
{
    public readonly int FrameId, Column, Row;
    public readonly FrameSide Side;
    public CombCellId(ItemData item, FrameSide side, int column, int row)
    {
        var frame = item == null ? null : item.GetContent<FrameData>();
        if (item == null || item.Kind != ItemKind.Frame || frame == null ||
            (side != FrameSide.Front && side != FrameSide.Back) || !frame.Front.IsInside(column, row))
            throw new ArgumentException("Invalid frame cell identity");
        FrameId = item.Id; Side = side; Column = column; Row = row;
    }
    public override string ToString() => $"{FrameId}:{Side}:{Column}:{Row}";
}

public static class CombSpatialMapper
{
    // Logical 7 x 5 blocks, NOT geometric containment or area-weighted overlap.
    // Hexagons cross physical 35 x 25 mm boundaries; their centres can also
    // belong to a different physical block. Y is the chosen depth layer (0..2).
    public static EquipmentSpatialCell GetLogicalCell(ItemData item, int column, int row, int depth)
    {
        var frame = item == null ? null : item.GetContent<FrameData>();
        if (item == null || item.Kind != ItemKind.Frame || frame == null || frame.Width != 84 || frame.Height != 40)
            throw new ArgumentException("Logical mapping requires an 84 x 40 frame");
        frame.Front.GetIndex(column, row);
        return item.EquipmentGrid.GetCell(1 + column / 7, depth, 1 + row / 5);
    }
}
