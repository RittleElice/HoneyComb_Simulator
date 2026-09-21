using HoneyComb.Simulation.Environment;
using UnityEngine;
namespace HoneyComb.Unity.Configuration
{
    [CreateAssetMenu(menuName = "HoneyComb/Environment Config", fileName = "EnvironmentConfig")]
    public sealed class EnvironmentConfig : ScriptableObject
    {
        [Header("External environment — copied when Play starts")]
        [Tooltip("Outside air temperature, Celsius; not hive temperature.")]
        public float defaultTemperature = 20f;
        [Range(0, 100)] public float defaultHumidity = 60f;
        [Tooltip("Rainfall intensity in millimeters per hour, not accumulated rainfall.")]
        [Min(0)] public float defaultPrecipitation = 0f;
        [Tooltip("Meters per second.")]
        [Min(0)] public float defaultWindSpeed = 0f;
        [Tooltip("Normalized brightness: 0 dark, 1 full daylight. Fixed in this prototype.")]
        [Range(0, 1)] public float defaultDaylight = 1f;
        public EnvironmentState CreateSnapshot() => new EnvironmentState(defaultTemperature,
            defaultHumidity, defaultPrecipitation, defaultWindSpeed, defaultDaylight);
    }
}
