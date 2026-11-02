using System;

public static class ThermalMath
{
    public static bool Finite(float v) => !float.IsNaN(v) && !float.IsInfinity(v);
    public static bool Positive(float v) => Finite(v) && v > 0;
    public static float WaxMassKg(CellData cell) => (cell.Type == CombCellType.Drone ? 20e-6f : 15e-6f) * (cell.Construction / 100f);
    public static int LogicalIndex(int column, int row, FrameSide side)
    {
        return LogicalIndex(column, row, side, FrameData.DefaultWidth, FrameData.DefaultHeight);
    }
    internal static int LogicalIndex(int column, int row, FrameSide side, int width, int height)
    {
        if (column < 0 || column >= width || row < 0 || row >= height ||
            (side != FrameSide.Front && side != FrameSide.Back))
            throw new ArgumentOutOfRangeException("Comb coordinate");
        int layer = side == FrameSide.Front ? 0 : 1;
        return CombSpatialMapper.RegionX(column, width) + HiveGridLayout.CombColumns *
            (layer + 2 * CombSpatialMapper.RegionZ(row, height));
    }
    public static float Overlap(double a, double b, double c, double d) => (float)Math.Max(0, Math.Min(b, d) - Math.Max(a, c));
    // No clipping of occupied mass. Overflow is a modelling error and cannot become valid state.
    public static void Mix(float volume, float solidVolume, ThermalMaterialValue solid, float waxMass,
        ThermalMaterials materials, out float airVolume, out float capacity, out float conductivity)
    {
        if (!Positive(volume) || !Finite(solidVolume) || solidVolume < 0 || !Finite(waxMass) || waxMass < 0)
            throw new ArgumentOutOfRangeException("Invalid cell volume/mass");
        var wax = materials[ThermalMaterialKind.Wax]; var air = materials[ThermalMaterialKind.Air];
        float waxVolume = waxMass / wax.Density;
        double free = (double)volume - solidVolume - waxVolume;
        if (free < -volume * 1e-6) throw new InvalidOperationException("Occupied volume exceeds cell volume: " + (solidVolume + waxVolume) + " > " + volume + " m3");
        airVolume = (float)Math.Max(0, free);
        // Each material contributes volume times its effective volumetric heat capacity.
        // Keep the original multiplication order when the override is disabled.
        float airCapacity = air.EffectiveVolumetricHeatCapacityJPerM3K > 0
            ? airVolume * air.EffectiveVolumetricHeatCapacityJPerM3K
            : airVolume * air.Density * air.SpecificHeat;
        float solidCapacity = solid.EffectiveVolumetricHeatCapacityJPerM3K > 0
            ? solidVolume * solid.EffectiveVolumetricHeatCapacityJPerM3K
            : solidVolume * solid.Density * solid.SpecificHeat;
        float waxCapacity = wax.EffectiveVolumetricHeatCapacityJPerM3K > 0
            ? waxVolume * wax.EffectiveVolumetricHeatCapacityJPerM3K
            : waxMass * wax.SpecificHeat;
        capacity = solidCapacity + waxCapacity + airCapacity;
        conductivity = (solidVolume * solid.Conductivity + waxVolume * wax.Conductivity + airVolume * air.Conductivity) / volume;
        if (!Positive(capacity) || !Positive(conductivity)) throw new InvalidOperationException("Non-finite thermal mixture");
    }
}
