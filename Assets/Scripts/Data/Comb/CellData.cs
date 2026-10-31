using System;

// Numeric values retained for old callers/data. Egg/Larva/Pupa require explicit
// migration to a BeeId; no developmental state is stored in the new cell model.
public enum CellContentKind { Empty = 0, Egg = 1, Larva = 2, Pupa = 3, Honey = 4, Pollen = 5, Nectar = 6, Brood = 7 }
public enum CombCellType { Worker, Drone }

// Eight managed bytes. Completion is historical, not derivable from Construction.
public partial struct CellData
{
    private byte flags; // bit 0: drone; bit 1: complete
    private byte content;
    public byte Construction { get; private set; }
    public byte CappingProgress { get; private set; }
    public float FillRatio { get; private set; } // legacy display hint, NOT a mass
    public CombCellType Type => (CombCellType)(flags & 1);
    public CellContentKind Content => (CellContentKind)content;
    public bool IsComplete => (flags & 2) != 0;
    public bool IsCapped => CappingProgress == 100;
    public bool HasStoredMaterial => IsStorageContent(Content);
    public static bool IsStorageContent(CellContentKind kind) => kind == CellContentKind.Honey || kind == CellContentKind.Nectar || kind == CellContentKind.Pollen;
    internal static CellContentKind Normalize(CellContentKind kind) => kind == CellContentKind.Honey ? CellContentKind.Nectar : kind;
    private static bool IsLegacyInput(CellContentKind kind) => kind == CellContentKind.Empty || IsStorageContent(kind);
    public CellData(byte construction, CellContentKind content) : this()
    {
        if (!IsLegacyInput(content)) throw new ArgumentOutOfRangeException(nameof(content), "Brood requires a BeeId; legacy stages cannot be inferred.");
        SetConstruction(construction);
        if (IsComplete) TrySetContent(content);
    }
    public void SetType(CombCellType type)
    {
        if (type != CombCellType.Worker && type != CombCellType.Drone) throw new ArgumentOutOfRangeException(nameof(type));
        flags = (byte)((flags & 2) | (byte)type);
    }
    public void SetConstruction(byte value)
    {
        Construction = (byte)Math.Min(100, (int)value);
        if (!IsComplete && Construction >= 80) flags |= 2;
        else if (IsComplete && Construction <= 50) { flags &= 1; ClearContent(); }
    }
    private void ClearContent() { content = 0; FillRatio = 0; CappingProgress = 0; }
    public bool TrySetContent(CellContentKind kind)
    {
        if (!IsLegacyInput(kind) || (!IsComplete && kind != CellContentKind.Empty)) return false;
        kind = Normalize(kind);
        if (Content == kind) return true;
        content = (byte)kind; FillRatio = IsStorageContent(kind) ? 1 : 0; CappingProgress = 0;
        return true;
    }
    public bool TrySetStoredContent(CellContentKind kind, float fillRatio)
    {
        if (!IsComplete || !IsStorageContent(kind) || !ValidRatio(fillRatio)) return false;
        TrySetContent(kind); FillRatio = fillRatio;
        if (fillRatio == 0) ClearContent();
        return true;
    }
    public bool TrySetFillRatio(float value)
    {
        if (!IsComplete || !HasStoredMaterial || !ValidRatio(value)) return false;
        FillRatio = value; if (value == 0) ClearContent(); return true;
    }
    private static bool ValidRatio(float value) => !float.IsNaN(value) && value >= 0 && value <= 1;
    public bool TrySetCapped(bool value) => TrySetCappingProgress((byte)(value ? 100 : 0));
    public bool TrySetCappingProgress(byte value)
    {
        if (value > 100 || (value > 0 && (!IsComplete || Content == CellContentKind.Empty))) return false;
        CappingProgress = value; return true;
    }
    internal void SetQuantifiedContent(CellContentKind kind)
    {
        if (Content != kind) CappingProgress = 0;
        content = (byte)kind; FillRatio = 0; // capacity/density conversion is deliberately unspecified
    }
}
