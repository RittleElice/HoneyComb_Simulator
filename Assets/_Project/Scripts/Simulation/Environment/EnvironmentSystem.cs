using System;
using HoneyComb.Simulation.Environment.Weather;
namespace HoneyComb.Simulation.Environment
{
    public sealed class EnvironmentSystem
    {
        private readonly EnvironmentState initialConditions;
        private readonly WeatherSimulator weather;
        public bool HasWeather => weather != null;
        public WeatherSample WeatherCurrent => weather == null ? default : weather.Current;
        public long ProcessedMinuteTicks { get; private set; }
        public EnvironmentSystem(WeatherSimulator weather, long initialMinute)
        {
            this.weather=weather??throw new ArgumentNullException(nameof(weather));
            Current=weather.Step(initialMinute).ToEnvironment();
            LastUpdatedHour=initialMinute/60;
        }
        public void TickMinute(long minute)
        {
            if(weather==null)throw new InvalidOperationException("Weather is not configured.");
            Current=weather.Step(minute).ToEnvironment();
            ProcessedMinuteTicks++;
        }
        public EnvironmentState Current { get; private set; }
        public long LastUpdatedHour { get; private set; }
        public long ProcessedTicks { get; private set; }
        public EnvironmentSystem(EnvironmentState initialConditions, long initialHour)
        {
            if (initialHour < 0) throw new ArgumentOutOfRangeException(nameof(initialHour));
            this.initialConditions = initialConditions;
            Current = initialConditions;
            LastUpdatedHour = initialHour;
        }
        public void TickHour(long hour)
        {
            if (LastUpdatedHour == long.MaxValue || hour != LastUpdatedHour + 1)
                throw new ArgumentOutOfRangeException(nameof(hour), "Environment must receive each hour exactly once, in order.");
            // Fixed external input for v0.1. A future weather model replaces this calculation.
            if(weather==null) Current = initialConditions;
            LastUpdatedHour = hour;
            ProcessedTicks++;
        }
    }
}
