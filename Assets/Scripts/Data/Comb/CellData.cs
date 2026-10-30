public enum CellContentKind
{
    Empty,
    Egg,
    Larva,
    Pupa,
    Honey,
    Pollen,
    Nectar
}

public enum CombCellType { Worker, Drone }

public struct CellData
{
    public CombCellType Type { get; private set; }
    public void SetType(CombCellType type)
    {
        if (type != CombCellType.Worker && type != CombCellType.Drone)
            throw new System.ArgumentOutOfRangeException(nameof(type));
        Type = type;
    }

    public byte Construction { get; private set; }
    public CellContentKind Content { get; private set; }

    public bool IsComplete => Construction == 100;

    public CellData(byte construction, CellContentKind content)
    {
        Type = CombCellType.Worker;
        Construction = (byte)System.Math.Min(100, (int)construction);

        Content = Construction == 100
            ? content
            : CellContentKind.Empty;
    }

    public void SetConstruction(byte value)
    {
        Construction = (byte)System.Math.Min(100, (int)value);

        if (Construction < 100)
            Content = CellContentKind.Empty;
    }

    public bool TrySetContent(CellContentKind content)
    {
        if (!IsComplete && content != CellContentKind.Empty)
            return false;

        Content = content;
        return true;
    }
}