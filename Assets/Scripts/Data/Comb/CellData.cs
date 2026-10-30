using System;

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
    public byte Construction { get; private set; }
    public CellContentKind Content { get; private set; }
    // Fraction of storage capacity (0..1), not brood growth or bee occupancy.
    // Empty and brood cells always have zero storage fill.
    public float FillRatio { get; private set; }
    public bool IsCapped { get; private set; }
    public bool IsComplete => Construction == 100;
    public bool HasStoredMaterial => IsStorageContent(Content);

    public static bool IsStorageContent(CellContentKind content) =>
        content == CellContentKind.Honey || content == CellContentKind.Pollen ||
        content == CellContentKind.Nectar;

    private static bool IsValidContent(CellContentKind content) =>
        content >= CellContentKind.Empty && content <= CellContentKind.Nectar;

    public CellData(byte construction, CellContentKind content)
    {
        if (!IsValidContent(content)) throw new ArgumentOutOfRangeException(nameof(content));
        Type = CombCellType.Worker;
        Construction = (byte)Math.Min(100, (int)construction);
        Content = Construction == 100 ? content : CellContentKind.Empty;
        // Legacy calls specified a kind without an amount: interpret stored material as full.
        FillRatio = IsStorageContent(Content) ? 1f : 0f;
        IsCapped = false;
    }

    public void SetType(CombCellType type)
    {
        if (type != CombCellType.Worker && type != CombCellType.Drone)
            throw new ArgumentOutOfRangeException(nameof(type));
        Type = type;
    }

    public void SetConstruction(byte value)
    {
        Construction = (byte)Math.Min(100, (int)value);
        if (!IsComplete) ClearContent();
    }

    private void ClearContent()
    {
        Content = CellContentKind.Empty;
        FillRatio = 0f;
        IsCapped = false;
    }

    // Reassigning the same kind preserves quantity and capping.
    // Changing kind is a replacement: reset quantity and remove the cap.
    public bool TrySetContent(CellContentKind content)
    {
        if (!IsValidContent(content) || (!IsComplete && content != CellContentKind.Empty)) return false;
        if (Content == content) return true;
        Content = content;
        FillRatio = IsStorageContent(content) ? 1f : 0f;
        IsCapped = false;
        return true;
    }

    // Atomic assignment for callers that know the actual stored amount.
    // A failed operation never partially changes the cell.
    public bool TrySetStoredContent(CellContentKind content, float fillRatio)
    {
        if (!IsComplete || !IsStorageContent(content) || !IsValidRatio(fillRatio)) return false;
        bool replaced = Content != content;
        Content = content;
        FillRatio = fillRatio;
        if (replaced) IsCapped = false;
        if (fillRatio == 0f) ClearContent();
        return true;
    }

    public bool TrySetFillRatio(float value)
    {
        if (!IsComplete || !HasStoredMaterial || !IsValidRatio(value)) return false;
        FillRatio = value;
        if (value == 0f) ClearContent();
        return true;
    }

    private static bool IsValidRatio(float value) =>
        !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0f && value <= 1f;

    // This stores a state; the bee/simulation rules decide when capping occurs.
    public bool TrySetCapped(bool value)
    {
        if (value && (!IsComplete || Content == CellContentKind.Empty)) return false;
        IsCapped = value;
        return true;
    }
}
