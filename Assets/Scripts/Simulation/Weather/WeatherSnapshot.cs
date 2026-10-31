/// <summary>Data only. Precipitation/solar are per-minute amounts; physical units marked inferred in the source specification.</summary>
public readonly struct WeatherSnapshot
{
    public long Minute { get; }
    public double CloudFraction { get; }
    public double WindSpeed { get; }
    public double Precipitation { get; }
    public double VaporPressureHpa { get; }
    public double SolarEnergy { get; }
    public double TemperatureCelsius { get; }
    public double RelativeHumidityPercent { get; }
    public double SoilWaterStorage { get; }
    internal WeatherSnapshot(long minute,WeatherValues v)
    {
        Minute=minute; CloudFraction=v.Cloud_Cover; WindSpeed=v.Wind_Cover; Precipitation=v.Precipitation;
        VaporPressureHpa=v.Vapor_Cover; SolarEnergy=v.Solar_Cover; TemperatureCelsius=v.Temperature_Cover;
        RelativeHumidityPercent=v.RelativeHumidity; SoilWaterStorage=v.SoilWater_Cover;
    }
}
