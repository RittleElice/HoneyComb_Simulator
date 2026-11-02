using System;
using System.Collections.Generic;
using System.IO;

// Authoritative float sensible energy, referenced to 0 C (negative energies are valid).
// E=C*T is an engineering cp-based model, not absolute thermodynamic internal energy.
// Equipment owns state even when detached. Derived SoA arrays are never serialized.
public sealed class EquipmentThermalState : IReadOnlyList<float>
{
    private readonly ItemData item;
    private readonly float[] energy;
    private readonly bool[] initialized, dirty;
    private readonly List<int> dirtyIndices = new List<int>();
    private readonly float[] capacity, conductivity, airVolume, waxMass;
    private readonly float[] logicalWax = new float[420];
    private int frontRevision = -1, backRevision = -1, balanceRevision = -1;
    private readonly int[] cellCacheRevision;
    internal int CellCacheRevision(int index) => cellCacheRevision[index];
    private readonly float[] contentCapacity;
    private readonly float[] depthConductivity;
    public float Conductivity(int index, int axis) => axis == 1 ? depthConductivity[index] : conductivity[index];
    public IReadOnlyList<float> ContentHeatCapacityJPerK => contentCapacity;
    private ThermalMaterials lastMaterials;
    public HiveMaterial ExchangeMaterial(int index) {
        var g=item.EquipmentGrid;
        return g.GetCell(index%g.SizeX,index/g.SizeX%g.SizeY,index/(g.SizeX*g.SizeY))?.Material ?? HiveMaterial.Air;
    }
    public int Count => energy.Length;
    // A retained read-only view follows the owner even across moves and buffer release.
    public IReadOnlyList<float> EnergyJ => this;
    float IReadOnlyList<float>.this[int index] => ReadEnergy(index);
    public IEnumerator<float> GetEnumerator()
    {
        for (int i = 0; i < Count; i++) yield return ReadEnergy(i);
    }
    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();

    internal ThermalExchange ActiveExchange { get; private set; }
    private int[] activeIndices;
    internal void BindEnergy(ThermalExchange exchange, int[] indices)
    {
        ActiveExchange = exchange;
        activeIndices = indices;
    }
    internal float ReadEnergy(int index)
    {
        int row = activeIndices == null ? -1 : activeIndices[index];
        return row < 0 ? energy[index] : ActiveExchange.ReadEnergy(row);
    }
    private void SetEnergy(int index, float value)
    {
        int row = activeIndices == null ? -1 : activeIndices[index];
        if (row < 0) energy[index] = value;
        else ActiveExchange.SetEnergy(row, value);
    }
    internal float[] HeatCapacityBuffer => capacity;
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
        cellCacheRevision = new int[count]; contentCapacity = new float[count]; depthConductivity = new float[count];
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
        SetEnergy(index, joules); initialized[index] = true;
    }
    // Solver-only result commit. Initialization and input validation happen before the Job.
    internal void CommitExchangeEnergy(int index, float joules) => energy[index] = joules;
    public float TemperatureC(int index)
    {
        if (!initialized[index] || dirty[index] || Diagnostic != null) throw new InvalidOperationException("Thermal cache is not ready.");
        return ReadEnergy(index) / capacity[index];
    }
    public void Refresh(ThermalMaterials materials, float outsideC)
    {
        if (materials == null || !ThermalMath.Finite(outsideC) || outsideC < -273.15f) throw new ArgumentOutOfRangeException(nameof(outsideC));
        if (!ReferenceEquals(lastMaterials, materials)) { MarkAllDirty(); lastMaterials = materials; frontRevision = -1; }
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
                float oldDepth = depthConductivity[i];
                Compute(i, materials, out float a, out float c, out float k);
                if (!initialized[i] || capacity[i] != c || conductivity[i] != k || oldDepth != depthConductivity[i])
                    cellCacheRevision[i] = CacheRevision + 1;
                float e = initialized[i] ? ReadEnergy(i) : c * outsideC;
                if (!ThermalMath.Finite(e)) throw new InvalidOperationException("Non-finite initial thermal energy");
                // Dirty remains set until the entire refresh is successful.
                airVolume[i] = a; capacity[i] = c; conductivity[i] = k;
                SetEnergy(i, e); initialized[i] = true;
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
        // Conservatively project onto the existing 7x5 interior; no mass is assigned to wood.
        for (int z = 1; z < HiveGridLayout.FrameSizeZ - 1; z++) for (int y = 0; y < 2; y++) for (int x = 1; x < HiveGridLayout.FrameSizeX - 1; x++)
        {
            float mass = (float)((g.X[x+1]-g.X[x]) * (g.Z[z+1]-g.Z[z]) * .25e-9 * lastMaterials[ThermalMaterialKind.Wax].Density);
            for (int lz = 0; lz < 10; lz++) for (int lx = 0; lx < 14; lx++)
            {
                float area = ThermalMath.Overlap(g.X[x], g.X[x+1], 10 + lx * 30, 10 + (lx+1) * 30) *
                    ThermalMath.Overlap(g.Z[z], g.Z[z+1], 10 + lz * 20, 10 + (lz+1) * 20);
                mass += logicalWax[lx + 14 * (y * 2 + 3 * lz)] * (area / 600f);
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
        if (item.Kind == ItemKind.Frame && kind == HiveMaterial.Comb && item.GetContent<FrameData>() == null)
            waxMass[index] = volume * .02f * m[ThermalMaterialKind.Wax].Density;
        solidVolume *= (float)g.SolidFraction(y);
        ThermalMath.Mix(volume, solidVolume, solid, waxMass[index], m, out air, out c, out k);
        depthConductivity[index] = k;
        if (item.Kind == ItemKind.Frame && kind == HiveMaterial.Comb)
        {
            // Lump 10 mm face + half of the old 5 mm centre. Preserve the
            // old plane-to-plane resistance, rather than using parallel k in Y.
            float faceVolume = volume * .8f, centreVolume = volume * .2f;
            float foundation = centreVolume * .1f * m[ThermalMaterialKind.Wax].Density;
            ThermalMath.Mix(faceVolume, 0, solid, Math.Max(0, waxMass[index]-foundation), m, out _, out _, out float faceK);
            ThermalMath.Mix(centreVolume, 0, solid, foundation, m, out _, out _, out float centreK);
            depthConductivity[index] = 1f / (.8f / faceK + .2f / centreK);
        }
        else if (item.Kind == ItemKind.DividerBoard || item.Kind == ItemKind.InsulationBoard || item.Kind == ItemKind.HeatingPanel)
        {
            float f = (float)g.SolidFraction(y);
            depthConductivity[index] = 1f / (f / solid.Conductivity + (1-f) / m[ThermalMaterialKind.Air].Conductivity);
        }
        // Existing 7x5 content regions map once to each face of the 7x5 interior.
        // Heat-capacity-only coupling: no invented content volume/conductivity or transported enthalpy.
        double contents = cell?.ContentHeatCapacityJPerK ?? 0;
        contentCapacity[index] = (float)contents;
        c = (float)(c + contents);
        if (!ThermalMath.Finite(contentCapacity[index]) || contentCapacity[index] < 0 || !ThermalMath.Positive(c))
            throw new InvalidOperationException("Invalid contents heat capacity");
    }
    // Uniform volumetric heat source across the owned panel.
    internal void SupplyHeat(double joules)
    {
        if (item.Kind != ItemKind.HeatingPanel || !ThermalExchange.Finite(joules) || joules < 0)
            throw new ArgumentOutOfRangeException(nameof(joules));
        var g=item.EquipmentGrid;double volume=0;
        for(int i=0;i<Count;i++){int x=i%g.SizeX,y=i/g.SizeX%g.SizeY,z=i/(g.SizeX*g.SizeY);volume+=(g.X[x+1]-g.X[x])*(g.Y[y+1]-g.Y[y])*(g.Z[z+1]-g.Z[z])*g.SolidFraction(y);}
        if(!(volume>0))throw new InvalidOperationException("Heating panel has no solid volume.");
        for(int i=0;i<Count;i++){int x=i%g.SizeX,y=i/g.SizeX%g.SizeY,z=i/(g.SizeX*g.SizeY);double v=(g.X[x+1]-g.X[x])*(g.Y[y+1]-g.Y[y])*(g.Z[z+1]-g.Z[z])*g.SolidFraction(y);
            double next=ReadEnergy(i)+joules*v/volume;if(!initialized[i]||dirty[i]||!ThermalExchange.Finite(next)||next>float.MaxValue)throw new InvalidOperationException("Invalid heating state");SetEnergy(i,(float)next);
        }
    }
    internal void WriteSnapshot(BinaryWriter w)
    {
        if (Diagnostic != null) throw new InvalidDataException(Diagnostic);
        w.Write(Count);
        for (int i = 0; i < Count; i++) { w.Write(initialized[i]); if (initialized[i]) { float value = ReadEnergy(i); if (!ThermalMath.Finite(value)) throw new InvalidDataException("Invalid energy"); w.Write(value); } }
    }
    internal void ReadSnapshot(BinaryReader r)
    {
        if (r.ReadInt32() != Count) throw new InvalidDataException("Thermal geometry length mismatch");
        for (int i = 0; i < Count; i++) if (r.ReadBoolean()) { float e = r.ReadSingle(); if (!ThermalMath.Finite(e)) throw new InvalidDataException("Invalid saved energy"); RestoreEnergy(i, e); }
        MarkAllDirty();
    }
}
