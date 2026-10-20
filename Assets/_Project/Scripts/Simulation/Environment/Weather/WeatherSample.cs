namespace HoneyComb.Simulation.Environment.Weather
{
    public readonly struct WeatherSample
    {
        public readonly long Minute;
        public readonly double CloudCover, WindSpeedMetersPerSecond, VaporPressureHpa;
        public readonly double PrecipitationMm, SolarEnergyMJPerM2, TemperatureCelsius;
        public readonly double RelativeHumidityPercent, SnowDepthCm, SoilWaterMm;
        public readonly double LiquidRainMm, SnowfallWaterMm, SnowMeltMm;
        public double PrecipitationMmPerHour => PrecipitationMm*60;
        // Display-only mapping: 1 corresponds to 1000 W/m2; energy remains unchanged.
        public double Daylight => System.Math.Min(1,System.Math.Max(0,SolarEnergyMJPerM2*1000000/60/1000));
        public WeatherSample(long minute,double cloud,double wind,double vapor,double rain,double solar,
            double temperature,double humidity,double snow,double soil,double liquid,double snowfall,double melt)
        {
            Minute=minute;CloudCover=cloud;WindSpeedMetersPerSecond=wind;VaporPressureHpa=vapor;
            PrecipitationMm=rain;SolarEnergyMJPerM2=solar;TemperatureCelsius=temperature;
            RelativeHumidityPercent=humidity;SnowDepthCm=snow;SoilWaterMm=soil;
            LiquidRainMm=liquid;SnowfallWaterMm=snowfall;SnowMeltMm=melt;
        }
        public EnvironmentState ToEnvironment() => new EnvironmentState((float)TemperatureCelsius,
            (float)RelativeHumidityPercent,(float)PrecipitationMmPerHour,(float)WindSpeedMetersPerSecond,(float)Daylight);
    }
}
