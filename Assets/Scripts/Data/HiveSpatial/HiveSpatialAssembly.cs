using System;
using System.Collections.Generic;

public readonly struct HiveStackFace
{
    public HiveSpatialGrid Lower { get; }
    public HiveSpatialGrid Upper { get; }
    public int LowerCell { get; }
    public int UpperCell { get; }
    public double AreaMm2 { get; }
    public double DistanceMm { get; }
    public HiveStackFace(HiveSpatialGrid lower, HiveSpatialGrid upper, int x, int y)
    {
        Lower = lower; Upper = upper;
        LowerCell = lower.Layout.Index(x, y, lower.Layout.SizeZ - 1);
        UpperCell = upper.Layout.Index(x, y, 0);
        AreaMm2 = (lower.Layout.X[x + 1] - lower.Layout.X[x]) * (lower.Layout.Y[y + 1] - lower.Layout.Y[y]);
        DistanceMm = ((lower.Layout.Z[lower.Layout.SizeZ] - lower.Layout.Z[lower.Layout.SizeZ - 1]) + upper.Layout.Z[1]) / 2;
    }
}

// Bind to an existing placement/hand slot. Combine/split/move events rebuild only the stack view.
// Dispose when the observer is no longer used. ItemData retains each box's spatial grid.
public sealed class HiveSpatialAssembly : IDisposable
{
    private readonly ItemSlot source;
    private readonly List<HiveSpatialGrid> boxes = new List<HiveSpatialGrid>();
    public IReadOnlyList<HiveSpatialGrid> Boxes { get; }
    public ItemData Lid { get; private set; }
    public HiveSpatialGrid LidGrid => Lid?.SpatialGrid;
    public bool HasOpenTop => boxes.Count > 0 && Lid == null;
    public event Action Changed;

    public HiveSpatialAssembly(ItemSlot source)
    {
        this.source = source ?? throw new ArgumentNullException(nameof(source));
        Boxes = boxes.AsReadOnly();
        source.OnChanged += Rebind;
        Rebind(source);
    }
    private void Rebind(ItemSlot slot)
    {
        foreach (var grid in boxes) grid.Changed -= GridChanged;
        boxes.Clear(); Lid = null;
        if (slot.Value is ItemAssemblyData assembly)
            foreach (var part in assembly.Parts) Add(part);
        else if (slot.Item != null) Add(slot.Item);
        Changed?.Invoke();
    }
    private void Add(ItemData part)
    {
        if (part.Kind == ItemKind.Lid) Lid = part;
        else if (part.SpatialGrid != null)
        {
            boxes.Add(part.SpatialGrid);
            part.SpatialGrid.Changed += GridChanged;
        }
    }
    private void GridChanged(HiveSpatialGrid grid) => Changed?.Invoke();
    public IEnumerable<HiveStackFace> Interfaces()
    {
        for (int i = 0; i + 1 < boxes.Count; i++)
        for (int y = 0; y < 52; y++)
        for (int x = 0; x < 18; x++)
            yield return new HiveStackFace(boxes[i], boxes[i + 1], x, y);
        if (boxes.Count > 0 && LidGrid != null)
        for (int y = 0; y < 52; y++)
        for (int x = 0; x < 18; x++)
            yield return new HiveStackFace(boxes[boxes.Count - 1], LidGrid, x, y);
    }
    public void Dispose()
    {
        source.OnChanged -= Rebind;
        foreach (var grid in boxes) grid.Changed -= GridChanged;
        boxes.Clear(); Lid = null;
    }
}
