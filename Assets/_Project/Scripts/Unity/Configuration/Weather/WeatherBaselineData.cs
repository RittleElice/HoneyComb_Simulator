using HoneyComb.Simulation.Environment.Weather;
using UnityEngine;
namespace HoneyComb.Unity.Configuration
{
    [CreateAssetMenu(menuName="HoneyComb/Weather Baseline",fileName="WeatherBaseline")]
    public sealed class WeatherBaselineData : ScriptableObject
    {
        // Minute-major: cloud, wind log, wind season, rain supply, vapor, cloud mean, solar, air mass.
        // Hidden to avoid drawing 4.2 million Inspector fields.
        [SerializeField, HideInInspector] private double[] values;
        public int MinuteCount => values == null ? 0 : values.Length / WeatherBaseline.Columns;
        public WeatherBaseline CreateBaseline() => new WeatherBaseline(values);
    }
}
