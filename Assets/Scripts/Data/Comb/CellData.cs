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

public struct CellData
{
    public byte Construction { get; private set; }
    public CellContentKind Content { get; private set; }

    public bool IsComplete => Construction == 100;

    public CellData(byte construction, CellContentKind content)
    {
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