using System;

// Shared tuning values, not per-cell fields. Defaults are modelling presets;
// component densities intentionally use a simple additive-volume approximation.
[Serializable]
public sealed class CombBalanceParameters
{
    public float CellLiquidCapacityMl = 0.30f;
    public float CellPollenCapacityMg = 200f;
    public float WaterSpecificHeatJPerKgK = 4180f;
    public float SugarSpecificHeatJPerKgK = 1250f;
    public float PollenSpecificHeatJPerKgK = 1800f;
    public float BroodHeatCapacityJPerK = 0.30f; // per occupied cell, pending bee mass model
    public float AirDensityKgPerM3 = 1.2f;
    public float AirSpecificHeatJPerKgK = 1005f;
    public float WoodDensityKgPerM3 = 500f;
    public float WoodSpecificHeatJPerKgK = 1600f;
    public float CombEffectiveDensityKgPerM3 = 80f; // empty comb envelope, NOT solid wax
    public float CombSpecificHeatJPerKgK = 2100f;
    public float InsulationDensityKgPerM3 = 30f;
    public float InsulationSpecificHeatJPerKgK = 1400f;
    // Additive-volume approximation; sugar density is a tunable effective value,
    // not the measured density of solid sugar or a concentration-dependent table.
    public float WaterDensityGPerMl = 1f;
    public float SugarEffectiveDensityGPerMl = 1.6f;

    public CombBalanceParameters Copy() => (CombBalanceParameters)MemberwiseClone();
    public bool Validate(out string error)
    {
        foreach (var field in typeof(CombBalanceParameters).GetFields())
        {
            float v = (float)field.GetValue(this);
            if (float.IsNaN(v) || float.IsInfinity(v) || v < 0)
            { error = field.Name + " must be finite and nonnegative."; return false; }
        }
        if (CellLiquidCapacityMl <= 0 || CellPollenCapacityMg <= 0 ||
            WaterDensityGPerMl <= 0 || SugarEffectiveDensityGPerMl <= 0)
        { error = "Capacities and liquid component densities must be positive."; return false; }
        error = null; return true;
    }
}

public static class CombBalance
{
    private static CombBalanceParameters current = new CombBalanceParameters();
    internal static CombBalanceParameters Current => current;
    public static int Revision { get; private set; }
    public static CombBalanceParameters Snapshot() => current.Copy();
    public static bool TryApply(CombBalanceParameters parameters, out string error)
    {
        if (parameters == null) { error = "Missing parameters"; return false; }
        if (!parameters.Validate(out error)) return false;
        current = parameters.Copy(); unchecked { Revision++; } return true;
    }
    public static double LiquidCapacityMl => current.CellLiquidCapacityMl;
    public static double PollenCapacityMg => current.CellPollenCapacityMg;
    public static double NectarDensityGPerMl(double fraction)
    {
        if (double.IsNaN(fraction) || fraction < 0 || fraction > 1) throw new ArgumentOutOfRangeException(nameof(fraction));
        // Compatibility helper: derived from the same additive-volume model.
        return 1 / (fraction / current.SugarEffectiveDensityGPerMl + (1 - fraction) / current.WaterDensityGPerMl);
    }
    public static double NectarVolumeMl(double sugarMg, double waterMg)
    {
        if (double.IsNaN(sugarMg) || double.IsInfinity(sugarMg) || sugarMg < 0 ||
            double.IsNaN(waterMg) || double.IsInfinity(waterMg) || waterMg < 0) throw new ArgumentOutOfRangeException();
        double total = sugarMg + waterMg;
        if (double.IsInfinity(total)) throw new ArgumentOutOfRangeException();
        double volume = sugarMg / (1000.0 * current.SugarEffectiveDensityGPerMl) +
                        waterMg / (1000.0 * current.WaterDensityGPerMl);
        if (double.IsInfinity(volume)) throw new ArgumentOutOfRangeException();
        return volume;
    }
    public static double MaterialHeatCapacityJPerK(HiveMaterial material, double volumeMm3)
    {
        if (double.IsNaN(volumeMm3) || double.IsInfinity(volumeMm3) || volumeMm3 < 0) throw new ArgumentOutOfRangeException(nameof(volumeMm3));
        double density, specificHeat;
        switch (material)
        {
            case HiveMaterial.Air: density = current.AirDensityKgPerM3; specificHeat = current.AirSpecificHeatJPerKgK; break;
            case HiveMaterial.Wood: density = current.WoodDensityKgPerM3; specificHeat = current.WoodSpecificHeatJPerKgK; break;
            case HiveMaterial.Comb: density = current.CombEffectiveDensityKgPerM3; specificHeat = current.CombSpecificHeatJPerKgK; break;
            case HiveMaterial.Insulation: density = current.InsulationDensityKgPerM3; specificHeat = current.InsulationSpecificHeatJPerKgK; break;
            default: throw new ArgumentOutOfRangeException(nameof(material));
        }
        return volumeMm3 * 1e-9 * density * specificHeat;
    }
}

// Regional masses cached independently of tuning. No mass guesses for old FillRatio data.
public struct CombRegionThermalMass
{
    public double SugarMg, WaterMg, PollenMg;
    public int BroodCount, UnquantifiedCellCount;
    public double HeatCapacityJPerK =>
        1e-6 * (SugarMg * CombBalance.Current.SugarSpecificHeatJPerKgK +
                WaterMg * CombBalance.Current.WaterSpecificHeatJPerKgK +
                PollenMg * CombBalance.Current.PollenSpecificHeatJPerKgK) +
        BroodCount * (double)CombBalance.Current.BroodHeatCapacityJPerK;
}
