using System;

public static class ThermalMath
{
    public static bool Finite(float v) => !float.IsNaN(v) && !float.IsInfinity(v);
    public static bool Positive(float v) => Finite(v) && v > 0;
    public static float WaxMassKg(CellData cell) => (cell.Type == CombCellType.Drone ? 20e-6f : 15e-6f) * (cell.Construction / 100f);
    public static int LogicalIndex(int column, int row, FrameSide side)
    {
        if (column < 0 || column >= 84 || row < 0 || row >= 40 || (side != FrameSide.Front && side != FrameSide.Back))
            throw new ArgumentOutOfRangeException("Comb coordinate");
        return column / 6 + 14 * ((side == FrameSide.Front ? 0 : 2) + 3 * (row / 4));
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
        capacity = solidVolume * solid.Density * solid.SpecificHeat + waxMass * wax.SpecificHeat + airVolume * air.Density * air.SpecificHeat;
        conductivity = (solidVolume * solid.Conductivity + waxVolume * wax.Conductivity + airVolume * air.Conductivity) / volume;
        if (!Positive(capacity) || !Positive(conductivity)) throw new InvalidOperationException("Non-finite thermal mixture");
    }
}
