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
    [Tooltip("유효 체적 열용량 [J/(m³·K)]. 0이면 밀도 × 비열을 사용합니다.")]
    [Min(0)] public float EffectiveVolumetricHeatCapacityJPerM3K;
    [TextArea(2, 8)] public string Sources;
    [TextArea(2, 8)] public string Assumptions;
    public bool Uncertain;
    private void OnValidate() => ThermalMaterials.NotifyAssetsChanged();
    public ThermalMaterialValue Read() => new ThermalMaterialValue(DensityKgPerM3, SpecificHeatJPerKgK, ConductivityWPerMK, EffectiveVolumetricHeatCapacityJPerM3K);
}

// A detached immutable snapshot: no Unity API is used by the data model or save decoder.
public readonly struct ThermalMaterialValue
{
    public readonly float Density, SpecificHeat, Conductivity;
    public readonly float EffectiveVolumetricHeatCapacityJPerM3K;
    public ThermalMaterialValue(float density, float specificHeat, float conductivity, float effectiveVolumetricHeatCapacityJPerM3K = 0)
    {
        if (!ThermalMath.Positive(density) || !ThermalMath.Positive(specificHeat) || !ThermalMath.Positive(conductivity))
            throw new ArgumentOutOfRangeException("Thermal material must be finite and positive.");
        if (!ThermalMath.Finite(effectiveVolumetricHeatCapacityJPerM3K) || effectiveVolumetricHeatCapacityJPerM3K < 0)
            throw new ArgumentOutOfRangeException(nameof(effectiveVolumetricHeatCapacityJPerM3K));
        EffectiveVolumetricHeatCapacityJPerM3K = effectiveVolumetricHeatCapacityJPerM3K;
        Density = density; SpecificHeat = specificHeat; Conductivity = conductivity;
    }
}

public sealed class ThermalMaterials
{
    private static int assetRevision;
    private int loadedRevision;
    public static void NotifyAssetsChanged() => System.Threading.Interlocked.Increment(ref assetRevision);
    // Detached test/save snapshots remain immutable. Loaded live settings refresh only after edits.
    public ThermalMaterials RefreshLiveSnapshot() => liveSettings != null && loadedRevision != assetRevision ? Load() : this;
    private readonly ThermalMaterialValue[] values;
    private readonly double maximumFraction, exteriorFilm;
    private ThermalExchangeSettings liveSettings;
    public ThermalPairCoefficients Pairs => liveSettings != null ? liveSettings.ReadPairs() : pairs;
    private readonly ThermalPairCoefficients pairs;
    public double MaximumExchangeFractionPerMinute => liveSettings != null ? liveSettings.ValidFraction : maximumFraction;
    public double ExteriorFilmCoefficientWPerM2K => liveSettings != null ? liveSettings.ValidExterior : exteriorFilm;
    public ThermalMaterialValue this[ThermalMaterialKind kind] => values[(int)kind];
    public ThermalMaterials(ThermalMaterialValue[] source, double maximumExchangeFractionPerMinute = 0.1, double exteriorFilmCoefficientWPerM2K = 5, ThermalPairCoefficients pairs = null)
    {
        if (source == null || source.Length != 7) throw new ArgumentException("Seven materials required.");
        if (!ThermalExchange.Finite(maximumExchangeFractionPerMinute) || maximumExchangeFractionPerMinute <= 0 || maximumExchangeFractionPerMinute > ThermalExchangeSettings.MaxFraction)
            throw new ArgumentOutOfRangeException(nameof(maximumExchangeFractionPerMinute));
        if (!ThermalExchange.Finite(exteriorFilmCoefficientWPerM2K) || exteriorFilmCoefficientWPerM2K < 0)
            throw new ArgumentOutOfRangeException(nameof(exteriorFilmCoefficientWPerM2K));
        this.pairs=pairs;
        exteriorFilm = exteriorFilmCoefficientWPerM2K;
        maximumFraction = maximumExchangeFractionPerMinute;
        values = (ThermalMaterialValue[])source.Clone();
        foreach (var v in values) new ThermalMaterialValue(v.Density, v.SpecificHeat, v.Conductivity, v.EffectiveVolumetricHeatCapacityJPerM3K);
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
        return new ThermalMaterials(result, settings.ValidFraction, settings.ValidExterior) { liveSettings = settings, loadedRevision = assetRevision };
    }
}
