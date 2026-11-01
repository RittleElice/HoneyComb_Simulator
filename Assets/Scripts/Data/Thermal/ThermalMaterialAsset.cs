using System;
using UnityEngine;

public enum ThermalMaterialKind { Air, Wood, Wax, Insulation, Sugar, Water, Pollen }

[CreateAssetMenu(menuName = "HoneyComb/Thermal Material (20 C)")]
public sealed class ThermalMaterialAsset : ScriptableObject
{
    public ThermalMaterialKind Kind;
    public float ReferenceTemperatureC = 20;
    public float DensityKgPerM3;
    public float SpecificHeatJPerKgK;
    public float ConductivityWPerMK;
    [TextArea(2, 8)] public string Sources;
    [TextArea(2, 8)] public string Assumptions;
    public bool Uncertain;
    public ThermalMaterialValue Read() => new ThermalMaterialValue(DensityKgPerM3, SpecificHeatJPerKgK, ConductivityWPerMK);
}

// A detached immutable snapshot: no Unity API is used by the data model or save decoder.
public readonly struct ThermalMaterialValue
{
    public readonly float Density, SpecificHeat, Conductivity;
    public ThermalMaterialValue(float density, float specificHeat, float conductivity)
    {
        if (!ThermalMath.Positive(density) || !ThermalMath.Positive(specificHeat) || !ThermalMath.Positive(conductivity))
            throw new ArgumentOutOfRangeException("Thermal material must be finite and positive.");
        Density = density; SpecificHeat = specificHeat; Conductivity = conductivity;
    }
}

public sealed class ThermalMaterials
{
    private readonly ThermalMaterialValue[] values;
    public double MaximumExchangeFractionPerMinute { get; }
    public double ExteriorFilmCoefficientWPerM2K { get; }
    public ThermalMaterialValue this[ThermalMaterialKind kind] => values[(int)kind];
    public ThermalMaterials(ThermalMaterialValue[] source, double maximumExchangeFractionPerMinute = 0.1, double exteriorFilmCoefficientWPerM2K = 5)
    {
        if (source == null || source.Length != 7) throw new ArgumentException("Seven materials required.");
        if (!ThermalExchange.Finite(maximumExchangeFractionPerMinute) || maximumExchangeFractionPerMinute <= 0 || maximumExchangeFractionPerMinute > 0.5)
            throw new ArgumentOutOfRangeException(nameof(maximumExchangeFractionPerMinute));
        if (!ThermalExchange.Finite(exteriorFilmCoefficientWPerM2K) || exteriorFilmCoefficientWPerM2K < 0)
            throw new ArgumentOutOfRangeException(nameof(exteriorFilmCoefficientWPerM2K));
        ExteriorFilmCoefficientWPerM2K = exteriorFilmCoefficientWPerM2K;
        MaximumExchangeFractionPerMinute = maximumExchangeFractionPerMinute;
        values = (ThermalMaterialValue[])source.Clone();
        foreach (var v in values) new ThermalMaterialValue(v.Density, v.SpecificHeat, v.Conductivity);
    }
    public static ThermalMaterials Load()
    {
        var result = new ThermalMaterialValue[7];
        for (int i = 0; i < result.Length; i++)
        {
            var asset = Resources.Load<ThermalMaterialAsset>("Thermal/" + (ThermalMaterialKind)i);
            if (asset == null || asset.Kind != (ThermalMaterialKind)i || asset.ReferenceTemperatureC != 20)
                throw new InvalidOperationException("Missing/invalid thermal material: " + (ThermalMaterialKind)i);
            result[i] = asset.Read();
        }
        var settings = Resources.Load<ThermalExchangeSettings>("Thermal/ExchangeSettings");
        if (settings == null) throw new InvalidOperationException("Missing thermal exchange settings");
        return new ThermalMaterials(result, settings.MaximumFractionPerMinute, settings.ExteriorFilmCoefficientWPerM2K);
    }
}
