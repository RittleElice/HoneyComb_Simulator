using System;
using System.Collections.Generic;
using System.IO;

// Authoritative float sensible energy, referenced to 0 C (negative energies are valid).
// E=C*T is an engineering cp-based model, not absolute thermodynamic internal energy.
// Equipment owns state even when detached. Derived SoA arrays are never serialized.
public sealed class EquipmentThermalState
{
    private readonly ItemData item;
    private readonly float[] energy;
    private readonly bool[] initialized, dirty;
    private readonly List<int> dirtyIndices = new List<int>();
    private readonly float[] capacity, conductivity, airVolume, waxMass;
    private readonly float[] logicalWax = new float[420];
    private int frontRevision = -1, backRevision = -1, balanceRevision = -1;
    private readonly float[] contentCapacity;
    public IReadOnlyList<float> ContentHeatCapacityJPerK => contentCapacity;
    private ThermalMaterials lastMaterials;
    public int Count => energy.Length;
    public IReadOnlyList<float> EnergyJ => energy;
    public IReadOnlyList<float> HeatCapacityJPerK => capacity;
    public IReadOnlyList<float> EffectiveConductivityWPerMK => conductivity;
    public IReadOnlyList<float> AirVolumeM3 => airVolume;
    public IReadOnlyList<float> WaxMassKg => waxMass;
    public IReadOnlyList<float> LogicalWaxMassKg => logicalWax;
    public IReadOnlyList<int> DirtyIndices => dirtyIndices;
    public string Diagnostic { get; private set; }
    public int CacheRevision { get; private set; }
    public string MappingNotice { get; private set; }
    public bool IsInitialized(int index) => initialized[index];
    public bool IsDirty(int index) => dirty[index];
    internal EquipmentThermalState(ItemData owner)
    {
        item = owner; var g = owner.EquipmentGrid;
        int count = g.SizeX * g.SizeY * g.SizeZ;
        energy = new float[count]; initialized = new bool[count]; dirty = new bool[count];
        contentCapacity = new float[count];
        capacity = new float[count]; conductivity = new float[count]; airVolume = new float[count]; waxMass = new float[count];
        MarkAllDirty();
    }
    public void MarkDirty(int index)
    {
        if (index < 0 || index >= Count) throw new ArgumentOutOfRangeException(nameof(index));
        if (dirty[index]) return;
        dirty[index] = true; dirtyIndices.Add(index);
    }
    public void MarkAllDirty() { for (int i = 0; i < Count; i++) MarkDirty(i); }
    public void RestoreEnergy(int index, float joules)
    {
        if (!ThermalMath.Finite(joules)) throw new ArgumentOutOfRangeException(nameof(joules));
        energy[index] = joules; initialized[index] = true;
    }
    // Solver-only result commit. Initialization and input validation happen before the Job.
    internal void CommitExchangeEnergy(int index, float joules) => energy[index] = joules;
    public float TemperatureC(int index)
    {
        if (!initialized[index] || dirty[index] || Diagnostic != null) throw new InvalidOperationException("Thermal cache is not ready.");
        return energy[index] / capacity[index];
    }
    public void Refresh(ThermalMaterials materials, float outsideC)
    {
        if (materials == null || !ThermalMath.Finite(outsideC) || outsideC < -273.15f) throw new ArgumentOutOfRangeException(nameof(outsideC));
        if (!ReferenceEquals(lastMaterials, materials)) { MarkAllDirty(); lastMaterials = materials; }
        var frame = item.GetContent<FrameData>();
        if (item.Kind == ItemKind.Frame && frame != null && (frame.Front.Revision != frontRevision || frame.Back.Revision != backRevision || balanceRevision != CombBalance.Revision))
        {
            RebuildWax(frame);
            // Content can change without changing wax. Shared specific-heat tuning also invalidates C.
            MarkAllDirty();
            frontRevision = frame.Front.Revision; backRevision = frame.Back.Revision; balanceRevision = CombBalance.Revision;
        }
        Diagnostic = null;
        int computingIndex = -1;
        try
        {
            foreach (int i in dirtyIndices)
            {
                computingIndex = i;
                Compute(i, materials, out float a, out float c, out float k);
                float e = initialized[i] ? energy[i] : c * outsideC;
                if (!ThermalMath.Finite(e)) throw new InvalidOperationException("Non-finite initial thermal energy");
                // Dirty remains set until the entire refresh is successful.
                airVolume[i] = a; capacity[i] = c; conductivity[i] = k;
                energy[i] = e; initialized[i] = true;
            }
            if (dirtyIndices.Count > 0) CacheRevision++;
            foreach (int i in dirtyIndices) dirty[i] = false;
            dirtyIndices.Clear();
        }
        catch (Exception e) { Diagnostic = "Equipment " + item.Id + ", cell " + computingIndex + ": " + e.Message; throw new InvalidOperationException(Diagnostic, e); }
    }
    private void RebuildWax(FrameData frame)
    {
        bool standard = frame.Width == 84 && frame.Height == 40;
        MappingNotice = standard ? null : "Legacy dimensions: normalized 14x10 projection; groups are not 6x4. Original comb data retained.";
        Array.Clear(logicalWax, 0, logicalWax.Length);
        foreach (var side in new[] { FrameSide.Front, FrameSide.Back })
        {
            var surface = frame.GetSurface(side);
            for (int z = 0; z < frame.Height; z++) for (int x = 0; x < frame.Width; x++)
            {
                int index = standard ? ThermalMath.LogicalIndex(x, z, side) :
                    x * 14 / frame.Width + 14 * ((side == FrameSide.Front ? 0 : 2) + 3 * (z * 10 / frame.Height));
                logicalWax[index] += ThermalMath.WaxMassKg(surface.GetCell(x, z));
            }
        }
        var g = item.EquipmentGrid;
        // Logical 14x10 groups uniformly cover the actual 420x200 opening.
        // Conservatively project onto the existing 12x8 interior; no mass is assigned to wood.
        for (int z = 1; z < 9; z++) for (int y = 0; y < 3; y += 2) for (int x = 1; x < 13; x++)
        {
            float mass = 0;
            for (int lz = 0; lz < 10; lz++) for (int lx = 0; lx < 14; lx++)
            {
                float area = ThermalMath.Overlap(g.X[x], g.X[x+1], 10 + lx * 30, 10 + (lx+1) * 30) *
                    ThermalMath.Overlap(g.Z[z], g.Z[z+1], 10 + lz * 20, 10 + (lz+1) * 20);
                mass += logicalWax[lx + 14 * (y + 3 * lz)] * (area / 600f);
            }
            int index = g.Index(x, y, z);
            if (waxMass[index] != mass) { waxMass[index] = mass; MarkDirty(index); }
        }
    }
    private void Compute(int index, ThermalMaterials m, out float air, out float c, out float k)
    {
        var g = item.EquipmentGrid;
        int x = index % g.SizeX, y = index / g.SizeX % g.SizeY, z = index / (g.SizeX * g.SizeY);
        var cell = g.GetCell(x, y, z);
        float volume = (float)((g.X[x+1]-g.X[x])*(g.Y[y+1]-g.Y[y])*(g.Z[z+1]-g.Z[z])*1e-9);
        var kind = cell?.Material ?? HiveMaterial.Air;
        var solid = m[kind == HiveMaterial.Insulation ? ThermalMaterialKind.Insulation : ThermalMaterialKind.Wood];
        float solidVolume = kind == HiveMaterial.Wood || kind == HiveMaterial.Insulation ? volume : 0;
        if (item.Kind == ItemKind.Frame && kind == HiveMaterial.Comb && y == 1)
        {
            // Nominal 440x200 centred on the opening, clipped against the actual wood border.
            float area = ThermalMath.Overlap(g.X[x],g.X[x+1],10,430) * ThermalMath.Overlap(g.Z[z],g.Z[z+1],10,210);
            waxMass[index] = area * .5f * 1e-9f * m[ThermalMaterialKind.Wax].Density;
        }
        ThermalMath.Mix(volume, solidVolume, solid, waxMass[index], m, out air, out c, out k);
        // Existing 7x5 content regions map once to each face of the 12x8 interior.
        // Heat-capacity-only coupling: no invented content volume/conductivity or transported enthalpy.
        double contents = cell?.ContentHeatCapacityJPerK ?? 0;
        contentCapacity[index] = (float)contents;
        c = (float)(c + contents);
        if (!ThermalMath.Finite(contentCapacity[index]) || contentCapacity[index] < 0 || !ThermalMath.Positive(c))
            throw new InvalidOperationException("Invalid contents heat capacity");
    }
    internal void WriteSnapshot(BinaryWriter w)
    {
        if (Diagnostic != null) throw new InvalidDataException(Diagnostic);
        w.Write(Count);
        for (int i = 0; i < Count; i++) { w.Write(initialized[i]); if (initialized[i]) { if (!ThermalMath.Finite(energy[i])) throw new InvalidDataException("Invalid energy"); w.Write(energy[i]); } }
    }
    internal void ReadSnapshot(BinaryReader r)
    {
        if (r.ReadInt32() != Count) throw new InvalidDataException("Thermal geometry length mismatch");
        for (int i = 0; i < Count; i++) if (r.ReadBoolean()) { float e = r.ReadSingle(); if (!ThermalMath.Finite(e)) throw new InvalidDataException("Invalid saved energy"); RestoreEnergy(i, e); }
        MarkAllDirty();
    }
}
