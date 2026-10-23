public class ApiaryGrid
{
    public int Width { get; private set; }
    public int Height { get; private set; }

    private int[,] occupiedBy;

    public ApiaryGrid(int width, int height)
    {
        Width = width;
        Height = height;

        occupiedBy = new int[width, height];
    }

    public bool IsInside(int x, int y)
    {
        return x >= 0 && x < Width &&
               y >= 0 && y < Height;
    }

    public bool IsEmpty(int x, int y)
    {
        return IsInside(x, y) &&
               occupiedBy[x, y] == 0;
    }

    public bool TryPlace(int hiveId, int x, int y)
    {
        if (hiveId <= 0 || !IsEmpty(x, y))
            return false;

        occupiedBy[x, y] = hiveId;
        return true;
    }

    public int GetHiveId(int x, int y)
    {
        if (!IsInside(x, y))
            return 0;

        return occupiedBy[x, y];
    }
    
    public bool Expand(int newWidth, int newHeight)
    {
        if (newWidth < Width || newHeight < Height)
            return false;

        if (newWidth == Width && newHeight == Height)
            return false;

        int[,] expanded = new int[newWidth, newHeight];

        for (int x = 0; x < Width; x++)
        {
            for (int y = 0; y < Height; y++)
            {
                expanded[x, y] = occupiedBy[x, y];
            }
        }

        occupiedBy = expanded;
        Width = newWidth;
        Height = newHeight;

        return true;
    }

}