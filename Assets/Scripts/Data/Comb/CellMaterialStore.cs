using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

public enum CellRemovalReason { ConstructionCollapse, ExplicitClear, LegacyReplacement }

// Emitted AFTER commit. Subscribers must not throw. Bulk edits stop if a subscriber
// throws, but every already-emitted cell is fully committed. No bee is killed here.
public readonly struct CellContentLoss
{
    public readonly int Column, Row;
    public readonly CellContentKind Content;
    public readonly CellRemovalReason Reason;
    public readonly double SugarMg, WaterMg, PollenMg;
    public readonly long BeeId;
    public readonly float LegacyFillRatio;
    internal CellContentLoss(int x, int y, CellData cell, CellRemovalReason reason,
        double sugar, double water, double pollen, long bee)
    { Column = x; Row = y; Content = cell.Content; Reason = reason; SugarMg = sugar;
      WaterMg = water; PollenMg = pollen; BeeId = bee; LegacyFillRatio = cell.FillRatio; }
}

// Owned by one surface, allocated only on the first quantitative write.
// No references or per-cell objects. Nectar: 1-based linked-list head + water;
// Pollen: mass; Brood: positive opaque 64-bit ID. Content discriminates the union.
internal sealed class CellMaterialStore
{
    [StructLayout(LayoutKind.Explicit, Size = 8)]
    private struct Payload
    {
        [FieldOffset(0)] public long Bee;
        [FieldOffset(0)] public int Head;
        [FieldOffset(4)] public float Mass;
    }
    private struct Sugar { public int Source, Next; public float Mass; }
    private readonly Payload[] payloads;
    private readonly List<Sugar> sugars = new List<Sugar>();
    private int free;
    internal CellMaterialStore(int count) { payloads = new Payload[count]; }
    internal bool Has(int index) => payloads[index].Bee != 0;
    internal float Mass(int index) => payloads[index].Mass;
    internal long Bee(int index) => payloads[index].Bee;
    internal static bool ValidMass(float value) => !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0;
    internal double TotalSugar(int index)
    {
        double sum = 0;
        for (int n = payloads[index].Head; n != 0; n = sugars[n - 1].Next) sum += sugars[n - 1].Mass;
        return sum;
    }
    internal float SugarMass(int index, int source)
    {
        for (int n = payloads[index].Head; n != 0; n = sugars[n - 1].Next)
            if (sugars[n - 1].Source == source) return sugars[n - 1].Mass;
        return 0;
    }
    // Caller-provided reusable list: no live mutable payload escapes.
    internal void CopySugars(int index, List<KeyValuePair<int, float>> output)
    {
        for (int n = payloads[index].Head; n != 0; n = sugars[n - 1].Next)
        { var s = sugars[n - 1]; output.Add(new KeyValuePair<int, float>(s.Source, s.Mass)); }
    }
    internal bool AddNectar(int index, int source, float sugar, float water)
    {
        var p = payloads[index];
        float nextWater = p.Mass + water;
        if (!ValidMass(nextWater)) return false;
        int found = 0;
        for (int n = p.Head; n != 0; n = sugars[n - 1].Next)
            if (sugars[n - 1].Source == source) { found = n; break; }
        float nextSugar = (found == 0 ? 0 : sugars[found - 1].Mass) + sugar;
        if (!ValidMass(nextSugar)) return false;
        if (sugar > 0)
        {
            if (found != 0) { var s = sugars[found - 1]; s.Mass = nextSugar; sugars[found - 1] = s; }
            else
            {
                var s = new Sugar { Source = source, Mass = sugar, Next = p.Head };
                int node;
                if (free != 0) { node = free; free = sugars[node - 1].Next; sugars[node - 1] = s; }
                else { sugars.Add(s); node = sugars.Count; }
                p.Head = node;
            }
        }
        p.Mass = nextWater; payloads[index] = p; return true;
    }
    internal bool AddPollen(int index, float mass)
    {
        float next = payloads[index].Mass + mass;
        if (!ValidMass(next)) return false;
        payloads[index].Mass = next; return true;
    }
    internal void SetBee(int index, long bee) { payloads[index].Bee = bee; }
    internal void Clear(int index, CellContentKind kind)
    {
        if (kind == CellContentKind.Nectar)
        {
            int n = payloads[index].Head;
            while (n != 0)
            {
                var s = sugars[n - 1]; int next = s.Next;
                s = new Sugar { Next = free }; sugars[n - 1] = s; free = n; n = next;
            }
        }
        payloads[index] = default;
    }
    // Capacity, not merely live entries; cleared sugar nodes are reused.
    internal long AllocatedDataBytes => (long)payloads.Length * 8 + (long)sugars.Capacity * 12;
}
