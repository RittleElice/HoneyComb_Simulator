using System;
using System.IO;
using System.Collections.Generic;

public partial struct CellData
{
    internal void WriteSnapshot(BinaryWriter w)
    { w.Write(flags); w.Write(content); w.Write(Construction); w.Write(CappingProgress); w.Write(FillRatio); }
    internal static CellData ReadSnapshot(BinaryReader r)
    {
        var c = new CellData { flags = r.ReadByte(), content = r.ReadByte(), Construction = r.ReadByte(), CappingProgress = r.ReadByte(), FillRatio = r.ReadSingle() };
        if (c.flags > 3 || c.Construction > 100 || c.CappingProgress > 100 || !ValidRatio(c.FillRatio) ||
            (c.IsComplete ? c.Construction <= 50 : c.Construction >= 80) ||
            (c.Content != CellContentKind.Empty && c.Content != CellContentKind.Nectar && c.Content != CellContentKind.Pollen && c.Content != CellContentKind.Brood) ||
            (!c.IsComplete && c.Content != CellContentKind.Empty) ||
            (c.Content == CellContentKind.Empty && (c.FillRatio != 0 || c.CappingProgress != 0)))
            throw new InvalidDataException("Invalid comb cell state.");
        return c;
    }
}
public sealed partial class FrameSurface
{
    internal void WriteSnapshot(BinaryWriter w)
    {
        var sugars = new List<KeyValuePair<int,float>>();
        foreach (var c in cells) c.WriteSnapshot(w);
        for (int i = 0; i < cells.Length; i++)
        {
            bool quantified = materials != null && materials.Has(i); w.Write(quantified);
            if (!quantified) continue;
            switch (cells[i].Content)
            {
                case CellContentKind.Nectar:
                    w.Write(materials.Mass(i)); sugars.Clear(); materials.CopySugars(i, sugars);
                    w.Write(sugars.Count); foreach (var sugar in sugars) { w.Write(sugar.Key); w.Write(sugar.Value); } break;
                case CellContentKind.Pollen: w.Write(materials.Mass(i)); break;
                case CellContentKind.Brood: w.Write(materials.Bee(i)); break;
                default: throw new InvalidDataException("Unexpected material payload.");
            }
        }
    }
    internal void ReadSnapshot(BinaryReader r)
    {
        for (int i = 0; i < cells.Length; i++) cells[i] = CellData.ReadSnapshot(r);
        for (int i = 0; i < cells.Length; i++)
        {
            bool quantified = r.ReadBoolean();
            if (!quantified) { if (cells[i].Content == CellContentKind.Brood) throw new InvalidDataException("Brood is missing BeeId."); continue; }
            if (!cells[i].IsComplete || cells[i].FillRatio != 0) throw new InvalidDataException("Invalid quantified cell.");
            if (materials == null) materials = new CellMaterialStore(cells.Length);
            switch (cells[i].Content)
            {
                case CellContentKind.Nectar:
                    float water = ReadMass(r); int count = SaveBinary.Count(r, 100000); var sources = new HashSet<int>();
                    materials.AddNectar(i, 0, 0, water);
                    // Restore physical masses without today's capacity limits: tuning must not discard stored material.
                    var savedSugars = new List<KeyValuePair<int,float>>(count);
                    for (int n = 0; n < count; n++)
                    {
                        int source = r.ReadInt32(); float mass = ReadMass(r);
                        if (source < 0 || mass <= 0 || !sources.Add(source)) throw new InvalidDataException("Invalid nectar source.");
                        savedSugars.Add(new KeyValuePair<int,float>(source,mass));
                    }
                    // Keep linked-list iteration order identical for deterministic future floating-point sums.
                    for (int n = savedSugars.Count - 1; n >= 0; n--)
                        if (!materials.AddNectar(i,savedSugars[n].Key,savedSugars[n].Value,0)) throw new InvalidDataException("Invalid nectar mass.");
                    if (!materials.Has(i)) throw new InvalidDataException("Empty quantified nectar."); break;
                case CellContentKind.Pollen:
                    float pollen = ReadMass(r); if (pollen <= 0) throw new InvalidDataException("Empty pollen."); materials.AddPollen(i,pollen); break;
                case CellContentKind.Brood:
                    long bee = r.ReadInt64(); if (bee <= 0) throw new InvalidDataException("Invalid BeeId."); materials.SetBee(i,bee); break;
                default: throw new InvalidDataException("Unexpected material type.");
            }
        }
        AdvanceRevision();
    }
    private static float ReadMass(BinaryReader r)
    { float value = r.ReadSingle(); if (!CellMaterialStore.ValidMass(value)) throw new InvalidDataException("Invalid mass."); return value; }
}
