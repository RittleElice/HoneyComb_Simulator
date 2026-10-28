
using System;

public sealed class FrameSurface
{
    private readonly CellData[] cells;

    public int Width { get; }
    public int Height { get; }
    public int CellCount => cells.Length;

    public FrameSurface(int width, int height)
    {
        if (width <= 0)
            throw new ArgumentOutOfRangeException(nameof(width));

        if (height <= 0)
            throw new ArgumentOutOfRangeException(nameof(height));

        Width = width;
        Height = height;

        cells = new CellData[checked(width * height)];
    }

    public bool IsInside(int x, int y)
    {
        return x >= 0 && x < Width &&
               y >= 0 && y < Height;
    }

    public int GetIndex(int x, int y)
    {
        if (!IsInside(x, y))
            throw new ArgumentOutOfRangeException();

        return y * Width + x;
    }

    public CellData GetCell(int x, int y)
    {
        return cells[GetIndex(x, y)];
    }

    public bool TrySetConstruction(int x, int y, byte value)
    {
        if (!IsInside(x, y))
            return false;

        int index = GetIndex(x, y);

        cells[index].SetConstruction(value);
        return true;
    }

    public bool TrySetContent(
        int x,
        int y,
        CellContentKind content)
    {
        if (!IsInside(x, y))
            return false;

        int index = GetIndex(x, y);

        return cells[index].TrySetContent(content);
    }

    public void SetAllConstruction(byte value)
    {
        for (int i = 0; i < cells.Length; i++)
            cells[i].SetConstruction(value);
    }

    public int CountCompletedCells()
    {
        int count = 0;

        for (int i = 0; i < cells.Length; i++)
        {
            if (cells[i].IsComplete)
                count++;
        }

        return count;
    }

    public float GetAverageConstruction()
    {
        long sum = 0;

        for (int i = 0; i < cells.Length; i++)
            sum += cells[i].Construction;

        return cells.Length > 0
            ? (float)sum / cells.Length
            : 0f;
    }
}
