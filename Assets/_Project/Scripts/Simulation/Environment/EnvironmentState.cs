using System;
namespace HoneyComb.Simulation.Environment
{
    // Immutable snapshot of external conditions, separate from future hive conditions.
    public readonly struct EnvironmentState
    {
        public float TemperatureCelsius { get; }
        public float HumidityPercent { get; }
        public float PrecipitationMmPerHour { get; }
        public float WindSpeedMetersPerSecond { get; }
        public float Daylight { get; }
        public EnvironmentState(float temperature, float humidity, float precipitation, float windSpeed, float daylight)
        {
            if (!Finite(temperature) || temperature < -273.15f) throw new ArgumentOutOfRangeException(nameof(temperature));
            if (!Finite(humidity) || humidity < 0 || humidity > 100) throw new ArgumentOutOfRangeException(nameof(humidity));
            if (!Finite(precipitation) || precipitation < 0) throw new ArgumentOutOfRangeException(nameof(precipitation));
            if (!Finite(windSpeed) || windSpeed < 0) throw new ArgumentOutOfRangeException(nameof(windSpeed));
            if (!Finite(daylight) || daylight < 0 || daylight > 1) throw new ArgumentOutOfRangeException(nameof(daylight));
            TemperatureCelsius = temperature; HumidityPercent = humidity;
            PrecipitationMmPerHour = precipitation; WindSpeedMetersPerSecond = windSpeed; Daylight = daylight;
        }
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
