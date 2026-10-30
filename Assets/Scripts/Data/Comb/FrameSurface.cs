
using System;

public sealed class FrameSurface
{
    private readonly CellData[] cells;

    public int Revision { get; private set; }
    public bool TrySetType(int x, int y, CombCellType type)
    {
        if (!IsInside(x, y)) return false;
        int index = GetIndex(x, y);
        var before = cells[index];
        cells[index].SetType(type);
        MarkChanged(index, before);
        return true;
    }

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

        var before = cells[index];
        cells[index].SetConstruction(value);
        MarkChanged(index, before);
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

        var before = cells[index];
        bool success = cells[index].TrySetContent(content);
        MarkChanged(index, before);
        return success;
    }

    public bool TrySetStoredContent(int x, int y, CellContentKind content, float fillRatio)
    {
        if (!IsInside(x, y)) return false;
        int index = GetIndex(x, y);
        var before = cells[index];
        bool success = cells[index].TrySetStoredContent(content, fillRatio);
        MarkChanged(index, before);
        return success;
    }

    public bool TrySetFillRatio(int x, int y, float value)
    {
        if (!IsInside(x, y)) return false;
        int index = GetIndex(x, y);
        var before = cells[index];
        bool success = cells[index].TrySetFillRatio(value);
        MarkChanged(index, before);
        return success;
    }

    public bool TrySetCapped(int x, int y, bool value)
    {
        if (!IsInside(x, y)) return false;
        int index = GetIndex(x, y);
        var before = cells[index];
        bool success = cells[index].TrySetCapped(value);
        MarkChanged(index, before);
        return success;
    }

    private static bool SameState(CellData a, CellData b) =>
        a.Type == b.Type && a.Construction == b.Construction && a.Content == b.Content &&
        a.FillRatio == b.FillRatio && a.IsCapped == b.IsCapped;

    private void MarkChanged(int index, CellData before)
    {
        if (!SameState(before, cells[index])) unchecked { Revision++; }
    }

    public void SetAllConstruction(byte value)
    {
        bool changed = false;
        for (int i = 0; i < cells.Length; i++)
        {
            var before = cells[i];
            cells[i].SetConstruction(value);
            changed |= !SameState(before, cells[i]);
        }
        if (changed) unchecked { Revision++; }
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
