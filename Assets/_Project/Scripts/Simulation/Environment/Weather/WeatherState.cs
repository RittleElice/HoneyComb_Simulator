using System;
namespace HoneyComb.Simulation.Environment.Weather
{
    // Complete evolving state; clone on capture/restore so callers cannot mutate the running model.
    [Serializable]
    public sealed class WeatherState
    {
        public long Minute;
        public uint RandomState, WindRandomState;
        public bool HasSpare, WindHasSpare;
        public double Spare, WindSpare;
        public double Slow, Fast, Supply, VaporNoise, IntensityNoise, SmoothIntensity;
        public double Reservoir, Age, Overshoot, Wet, PreWet, Vapor, RawRain;
        public double WindDay, WindFast, GroundEnergy, SurfaceEnergy, AirEnergy;
        public double SnowWater, SnowDepth, SoilWater;
        public double[] RainQueue, PassQueue;
        public WeatherState Copy()
        {
            var result=(WeatherState)MemberwiseClone();
            result.RainQueue=(double[])RainQueue.Clone();
            result.PassQueue=(double[])PassQueue.Clone();
            return result;
        }
    }
}
