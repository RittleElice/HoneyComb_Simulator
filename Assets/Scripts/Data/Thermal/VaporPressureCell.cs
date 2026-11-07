public readonly struct VaporPressureCell
{
    public readonly bool Available,Shared;
    public readonly double TemperatureC, AirPa, MaterialPa;
    public readonly double AirWaterKg,WoodWaterKg,InsulationWaterKg,NectarWaterKg;
    public double TotalWaterKg=>AirWaterKg+WoodWaterKg+InsulationWaterKg+NectarWaterKg;
    public VaporPressureCell(bool available,double temperatureC,double airPa,double materialPa,
        double airWaterKg=0,double woodWaterKg=0,double insulationWaterKg=0,double nectarWaterKg=0,bool shared=false)
    {
        Available=available;TemperatureC=temperatureC;AirPa=airPa;MaterialPa=materialPa;
        AirWaterKg=airWaterKg;WoodWaterKg=woodWaterKg;InsulationWaterKg=insulationWaterKg;
        NectarWaterKg=nectarWaterKg;Shared=shared;
    }
}
