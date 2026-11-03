using System;
using System.IO;
[Serializable] public sealed class NectarParameters
{
    public double SiteArea = 15, SourceFactor = 100;
    [Serializable] public class Source
    {
        public string Name;
        public double BaseGrowthTemperature, BloomStartGDD, BloomActivationGDDRange, BaseBloomRate, BaseWiltingRate, OptimalBloomTemperature, BloomTemperatureRange, OptimalFlowerMaintenanceTemperature, ColdWiltingTemperatureRange, HeatWiltingTemperatureRange, WiltingTemperatureExponent, NectarStorageFactor, NectarProductionFactor, OptimalNectarTemperature, NectarTemperatureRange, CarbonResponseTime, CarbonHalfSaturation, MinimumCarbonFactor, SoilWaterZeroProduction, SoilWaterFullProduction, WaterProductionFactor, MinimumWaterProductionTemperature, FullWaterProductionTemperature, HeatWaterProductionTemperature, MaximumWaterProductionTemperature, WaterProductionSoilMinimum, WaterProductionSoilFull, WaterProductionSoilExponent, NectarEvaporationRate, NectarEvaporationWindFactor, PollenDevelopmentHours, PollenProductionFactor, PollenHeatStartTemperature, PollenHeatZeroTemperature, PollenSoilMinimum, PollenSoilFull, PollenRainLossCoefficient, BaseNectarYieldRatio, AnnualYieldFactor, MinimumBloomingFlowerRatio, NectarSugarMolarMass, NectarNorrishConstant;
    }
    public Source[] Sources = {
        new Source { Name = "RapeSeed", BaseGrowthTemperature = 5, BloomStartGDD = 135, BloomActivationGDDRange = 325, BaseBloomRate = 3.2000000000000001E-2, BaseWiltingRate = 0.01, OptimalBloomTemperature = 20, BloomTemperatureRange = 10, OptimalFlowerMaintenanceTemperature = 20, ColdWiltingTemperatureRange = 15, HeatWiltingTemperatureRange = 10, WiltingTemperatureExponent = 1.5, NectarStorageFactor = 100, NectarProductionFactor = 10, OptimalNectarTemperature = 22, NectarTemperatureRange = 15, CarbonResponseTime = 24, CarbonHalfSaturation = 0.5, MinimumCarbonFactor = 0.2, SoilWaterZeroProduction = 15, SoilWaterFullProduction = 65, WaterProductionFactor = 6.2873650000000003, MinimumWaterProductionTemperature = 5, FullWaterProductionTemperature = 18, HeatWaterProductionTemperature = 28, MaximumWaterProductionTemperature = 38, WaterProductionSoilMinimum = 10, WaterProductionSoilFull = 70, WaterProductionSoilExponent = 1, NectarEvaporationRate = 1.4999999999999999E-2, NectarEvaporationWindFactor = 0.25, PollenDevelopmentHours = 168, PollenProductionFactor = 100, PollenHeatStartTemperature = 30, PollenHeatZeroTemperature = 40, PollenSoilMinimum = 10, PollenSoilFull = 70, PollenRainLossCoefficient = 0.1, BaseNectarYieldRatio = 0.18, AnnualYieldFactor = 1.0390084397491477, MinimumBloomingFlowerRatio = 1E-3, NectarSugarMolarMass = 180.15600000000001, NectarNorrishConstant = 2.25 },
        new Source { Name = "CherryBlossom", BaseGrowthTemperature = 5, BloomStartGDD = 129.024, BloomActivationGDDRange = 40, BaseBloomRate = 7.5976299999999997E-2, BaseWiltingRate = 1.4616799999999999E-2, OptimalBloomTemperature = 15, BloomTemperatureRange = 12, OptimalFlowerMaintenanceTemperature = 15, ColdWiltingTemperatureRange = 15, HeatWiltingTemperatureRange = 10, WiltingTemperatureExponent = 1.5, NectarStorageFactor = 100, NectarProductionFactor = 7.6543960000000002, OptimalNectarTemperature = 18, NectarTemperatureRange = 13, CarbonResponseTime = 24, CarbonHalfSaturation = 0.5, MinimumCarbonFactor = 0.2, SoilWaterZeroProduction = 15, SoilWaterFullProduction = 65, WaterProductionFactor = 7.0040940000000003, MinimumWaterProductionTemperature = 2, FullWaterProductionTemperature = 12, HeatWaterProductionTemperature = 24, MaximumWaterProductionTemperature = 34, WaterProductionSoilMinimum = 10, WaterProductionSoilFull = 70, WaterProductionSoilExponent = 1, NectarEvaporationRate = 1.2E-2, NectarEvaporationWindFactor = 0.25, PollenDevelopmentHours = 168, PollenProductionFactor = 73.948753999999994, PollenHeatStartTemperature = 30, PollenHeatZeroTemperature = 40, PollenSoilMinimum = 10, PollenSoilFull = 70, PollenRainLossCoefficient = 0.1, BaseNectarYieldRatio = 0.18, AnnualYieldFactor = 1.1534792222725421, MinimumBloomingFlowerRatio = 1E-3, NectarSugarMolarMass = 180.15600000000001, NectarNorrishConstant = 2.25 },
        new Source { Name = "BlackLocust", BaseGrowthTemperature = 5, BloomStartGDD = 410, BloomActivationGDDRange = 110, BaseBloomRate = 3.5999999999999997E-2, BaseWiltingRate = 2.1999999999999999E-2, OptimalBloomTemperature = 22, BloomTemperatureRange = 12, OptimalFlowerMaintenanceTemperature = 22, ColdWiltingTemperatureRange = 15, HeatWiltingTemperatureRange = 10, WiltingTemperatureExponent = 1.5, NectarStorageFactor = 100, NectarProductionFactor = 31.276924000000001, OptimalNectarTemperature = 23, NectarTemperatureRange = 12, CarbonResponseTime = 24, CarbonHalfSaturation = 0.5, MinimumCarbonFactor = 0.2, SoilWaterZeroProduction = 15, SoilWaterFullProduction = 65, WaterProductionFactor = 22.811833, MinimumWaterProductionTemperature = 8, FullWaterProductionTemperature = 18, HeatWaterProductionTemperature = 28, MaximumWaterProductionTemperature = 35, WaterProductionSoilMinimum = 10, WaterProductionSoilFull = 70, WaterProductionSoilExponent = 1, NectarEvaporationRate = 8.0000000000000002E-3, NectarEvaporationWindFactor = 0.25, PollenDevelopmentHours = 168, PollenProductionFactor = 37.777852000000003, PollenHeatStartTemperature = 30, PollenHeatZeroTemperature = 40, PollenSoilMinimum = 10, PollenSoilFull = 70, PollenRainLossCoefficient = 0.1, BaseNectarYieldRatio = 0.18, AnnualYieldFactor = 0.73234686925284032, MinimumBloomingFlowerRatio = 1E-3, NectarSugarMolarMass = 180.15600000000001, NectarNorrishConstant = 2.25 },
        new Source { Name = "Chestnut", BaseGrowthTemperature = 5, BloomStartGDD = 855, BloomActivationGDDRange = 110, BaseBloomRate = 2.1999999999999999E-2, BaseWiltingRate = 2.1999999999999999E-2, OptimalBloomTemperature = 25, BloomTemperatureRange = 12, OptimalFlowerMaintenanceTemperature = 25, ColdWiltingTemperatureRange = 15, HeatWiltingTemperatureRange = 10, WiltingTemperatureExponent = 1.5, NectarStorageFactor = 100, NectarProductionFactor = 32.043480000000002, OptimalNectarTemperature = 25, NectarTemperatureRange = 13, CarbonResponseTime = 24, CarbonHalfSaturation = 0.5, MinimumCarbonFactor = 0.2, SoilWaterZeroProduction = 15, SoilWaterFullProduction = 65, WaterProductionFactor = 42.557549000000002, MinimumWaterProductionTemperature = 10, FullWaterProductionTemperature = 20, HeatWaterProductionTemperature = 30, MaximumWaterProductionTemperature = 38, WaterProductionSoilMinimum = 10, WaterProductionSoilFull = 70, WaterProductionSoilExponent = 1, NectarEvaporationRate = 1.2E-2, NectarEvaporationWindFactor = 0.25, PollenDevelopmentHours = 168, PollenProductionFactor = 222.52243899999999, PollenHeatStartTemperature = 30, PollenHeatZeroTemperature = 40, PollenSoilMinimum = 10, PollenSoilFull = 70, PollenRainLossCoefficient = 0.1, BaseNectarYieldRatio = 0.18, AnnualYieldFactor = 0.75305750818629769, MinimumBloomingFlowerRatio = 1E-3, NectarSugarMolarMass = 180.15600000000001, NectarNorrishConstant = 2.25 },
        new Source { Name = "Wildflower_1", BaseGrowthTemperature = 5, BloomStartGDD = 24.797000000000001, BloomActivationGDDRange = 185, BaseBloomRate = 4.4308999999999998E-3, BaseWiltingRate = 1.3162200000000001E-2, OptimalBloomTemperature = 15, BloomTemperatureRange = 15, OptimalFlowerMaintenanceTemperature = 15, ColdWiltingTemperatureRange = 15, HeatWiltingTemperatureRange = 10, WiltingTemperatureExponent = 1.5, NectarStorageFactor = 100, NectarProductionFactor = 4.8056369999999999, OptimalNectarTemperature = 18, NectarTemperatureRange = 14, CarbonResponseTime = 24, CarbonHalfSaturation = 0.5, MinimumCarbonFactor = 0.2, SoilWaterZeroProduction = 15, SoilWaterFullProduction = 65, WaterProductionFactor = 4.5318110000000003, MinimumWaterProductionTemperature = 2, FullWaterProductionTemperature = 12, HeatWaterProductionTemperature = 24, MaximumWaterProductionTemperature = 34, WaterProductionSoilMinimum = 10, WaterProductionSoilFull = 70, WaterProductionSoilExponent = 1, NectarEvaporationRate = 1.7999999999999999E-2, NectarEvaporationWindFactor = 0.25, PollenDevelopmentHours = 168, PollenProductionFactor = 53.972535999999998, PollenHeatStartTemperature = 30, PollenHeatZeroTemperature = 40, PollenSoilMinimum = 10, PollenSoilFull = 70, PollenRainLossCoefficient = 0.1, BaseNectarYieldRatio = 7.0000000000000007E-2, AnnualYieldFactor = 0.8232351723331639, MinimumBloomingFlowerRatio = 1E-3, NectarSugarMolarMass = 180.15600000000001, NectarNorrishConstant = 2.25 },
        new Source { Name = "Wildflower_2", BaseGrowthTemperature = 5, BloomStartGDD = 470.93799999999999, BloomActivationGDDRange = 1315, BaseBloomRate = 3.6538999999999999E-3, BaseWiltingRate = 1.01685E-2, OptimalBloomTemperature = 22, BloomTemperatureRange = 15, OptimalFlowerMaintenanceTemperature = 22, ColdWiltingTemperatureRange = 15, HeatWiltingTemperatureRange = 10, WiltingTemperatureExponent = 1.5, NectarStorageFactor = 100, NectarProductionFactor = 10.289790999999999, OptimalNectarTemperature = 18, NectarTemperatureRange = 14, CarbonResponseTime = 24, CarbonHalfSaturation = 0.5, MinimumCarbonFactor = 0.2, SoilWaterZeroProduction = 15, SoilWaterFullProduction = 65, WaterProductionFactor = 4.0882990000000001, MinimumWaterProductionTemperature = 5, FullWaterProductionTemperature = 15, HeatWaterProductionTemperature = 25, MaximumWaterProductionTemperature = 35, WaterProductionSoilMinimum = 10, WaterProductionSoilFull = 70, WaterProductionSoilExponent = 1, NectarEvaporationRate = 6.0000000000000001E-3, NectarEvaporationWindFactor = 0.25, PollenDevelopmentHours = 168, PollenProductionFactor = 78.392989, PollenHeatStartTemperature = 30, PollenHeatZeroTemperature = 40, PollenSoilMinimum = 10, PollenSoilFull = 70, PollenRainLossCoefficient = 0.1, BaseNectarYieldRatio = 7.0000000000000007E-2, AnnualYieldFactor = 1.1538679619753871, MinimumBloomingFlowerRatio = 1E-3, NectarSugarMolarMass = 180.15600000000001, NectarNorrishConstant = 2.25 },
        new Source { Name = "Wildflower_3", BaseGrowthTemperature = 5, BloomStartGDD = 1280.7360000000001, BloomActivationGDDRange = 1090, BaseBloomRate = 3.3600000000000001E-3, BaseWiltingRate = 1.43538E-2, OptimalBloomTemperature = 26, BloomTemperatureRange = 15, OptimalFlowerMaintenanceTemperature = 26, ColdWiltingTemperatureRange = 15, HeatWiltingTemperatureRange = 10, WiltingTemperatureExponent = 1.5, NectarStorageFactor = 100, NectarProductionFactor = 3.5957970000000001, OptimalNectarTemperature = 25, NectarTemperatureRange = 13, CarbonResponseTime = 24, CarbonHalfSaturation = 0.5, MinimumCarbonFactor = 0.2, SoilWaterZeroProduction = 15, SoilWaterFullProduction = 65, WaterProductionFactor = 3.7109649999999998, MinimumWaterProductionTemperature = 8, FullWaterProductionTemperature = 20, HeatWaterProductionTemperature = 30, MaximumWaterProductionTemperature = 38, WaterProductionSoilMinimum = 10, WaterProductionSoilFull = 70, WaterProductionSoilExponent = 1, NectarEvaporationRate = 8.0000000000000002E-3, NectarEvaporationWindFactor = 0.25, PollenDevelopmentHours = 168, PollenProductionFactor = 33.399152000000001, PollenHeatStartTemperature = 30, PollenHeatZeroTemperature = 40, PollenSoilMinimum = 10, PollenSoilFull = 70, PollenRainLossCoefficient = 0.1, BaseNectarYieldRatio = 7.0000000000000007E-2, AnnualYieldFactor = 1.3815304829868995, MinimumBloomingFlowerRatio = 1E-3, NectarSugarMolarMass = 180.15600000000001, NectarNorrishConstant = 2.25 },
        new Source { Name = "Wildflower_4", BaseGrowthTemperature = 5, BloomStartGDD = 1978.5730000000001, BloomActivationGDDRange = 970, BaseBloomRate = 3.5875E-3, BaseWiltingRate = 1.1207999999999999E-2, OptimalBloomTemperature = 23, BloomTemperatureRange = 15, OptimalFlowerMaintenanceTemperature = 23, ColdWiltingTemperatureRange = 15, HeatWiltingTemperatureRange = 10, WiltingTemperatureExponent = 1.5, NectarStorageFactor = 100, NectarProductionFactor = 2.2252510000000001, OptimalNectarTemperature = 22, NectarTemperatureRange = 14, CarbonResponseTime = 24, CarbonHalfSaturation = 0.5, MinimumCarbonFactor = 0.2, SoilWaterZeroProduction = 15, SoilWaterFullProduction = 65, WaterProductionFactor = 3.5292520000000001, MinimumWaterProductionTemperature = 5, FullWaterProductionTemperature = 15, HeatWaterProductionTemperature = 27, MaximumWaterProductionTemperature = 36, WaterProductionSoilMinimum = 10, WaterProductionSoilFull = 70, WaterProductionSoilExponent = 1, NectarEvaporationRate = 1.7999999999999999E-2, NectarEvaporationWindFactor = 0.25, PollenDevelopmentHours = 168, PollenProductionFactor = 37.370350000000002, PollenHeatStartTemperature = 30, PollenHeatZeroTemperature = 40, PollenSoilMinimum = 10, PollenSoilFull = 70, PollenRainLossCoefficient = 0.1, BaseNectarYieldRatio = 7.0000000000000007E-2, AnnualYieldFactor = 1.5213344817917773, MinimumBloomingFlowerRatio = 1E-3, NectarSugarMolarMass = 180.15600000000001, NectarNorrishConstant = 2.25 },
    };

    public void Write(BinaryWriter w)
    {
        w.Write(SiteArea); w.Write(SourceFactor);
        foreach(var p in Sources) {
            w.Write(p.Name);
            w.Write(p.BaseGrowthTemperature);
            w.Write(p.BloomStartGDD);
            w.Write(p.BloomActivationGDDRange);
            w.Write(p.BaseBloomRate);
            w.Write(p.BaseWiltingRate);
            w.Write(p.OptimalBloomTemperature);
            w.Write(p.BloomTemperatureRange);
            w.Write(p.OptimalFlowerMaintenanceTemperature);
            w.Write(p.ColdWiltingTemperatureRange);
            w.Write(p.HeatWiltingTemperatureRange);
            w.Write(p.WiltingTemperatureExponent);
            w.Write(p.NectarStorageFactor);
            w.Write(p.NectarProductionFactor);
            w.Write(p.OptimalNectarTemperature);
            w.Write(p.NectarTemperatureRange);
            w.Write(p.CarbonResponseTime);
            w.Write(p.CarbonHalfSaturation);
            w.Write(p.MinimumCarbonFactor);
            w.Write(p.SoilWaterZeroProduction);
            w.Write(p.SoilWaterFullProduction);
            w.Write(p.WaterProductionFactor);
            w.Write(p.MinimumWaterProductionTemperature);
            w.Write(p.FullWaterProductionTemperature);
            w.Write(p.HeatWaterProductionTemperature);
            w.Write(p.MaximumWaterProductionTemperature);
            w.Write(p.WaterProductionSoilMinimum);
            w.Write(p.WaterProductionSoilFull);
            w.Write(p.WaterProductionSoilExponent);
            w.Write(p.NectarEvaporationRate);
            w.Write(p.NectarEvaporationWindFactor);
            w.Write(p.PollenDevelopmentHours);
            w.Write(p.PollenProductionFactor);
            w.Write(p.PollenHeatStartTemperature);
            w.Write(p.PollenHeatZeroTemperature);
            w.Write(p.PollenSoilMinimum);
            w.Write(p.PollenSoilFull);
            w.Write(p.PollenRainLossCoefficient);
            w.Write(p.BaseNectarYieldRatio);
            w.Write(p.AnnualYieldFactor);
            w.Write(p.MinimumBloomingFlowerRatio);
            w.Write(p.NectarSugarMolarMass);
            w.Write(p.NectarNorrishConstant);
        }
    }
    public static NectarParameters Read(BinaryReader r)
    {
        var result=new NectarParameters { SiteArea=r.ReadDouble(), SourceFactor=r.ReadDouble() };
        foreach(var p in result.Sources) {
            p.Name=r.ReadString();
            p.BaseGrowthTemperature=r.ReadDouble();
            p.BloomStartGDD=r.ReadDouble();
            p.BloomActivationGDDRange=r.ReadDouble();
            p.BaseBloomRate=r.ReadDouble();
            p.BaseWiltingRate=r.ReadDouble();
            p.OptimalBloomTemperature=r.ReadDouble();
            p.BloomTemperatureRange=r.ReadDouble();
            p.OptimalFlowerMaintenanceTemperature=r.ReadDouble();
            p.ColdWiltingTemperatureRange=r.ReadDouble();
            p.HeatWiltingTemperatureRange=r.ReadDouble();
            p.WiltingTemperatureExponent=r.ReadDouble();
            p.NectarStorageFactor=r.ReadDouble();
            p.NectarProductionFactor=r.ReadDouble();
            p.OptimalNectarTemperature=r.ReadDouble();
            p.NectarTemperatureRange=r.ReadDouble();
            p.CarbonResponseTime=r.ReadDouble();
            p.CarbonHalfSaturation=r.ReadDouble();
            p.MinimumCarbonFactor=r.ReadDouble();
            p.SoilWaterZeroProduction=r.ReadDouble();
            p.SoilWaterFullProduction=r.ReadDouble();
            p.WaterProductionFactor=r.ReadDouble();
            p.MinimumWaterProductionTemperature=r.ReadDouble();
            p.FullWaterProductionTemperature=r.ReadDouble();
            p.HeatWaterProductionTemperature=r.ReadDouble();
            p.MaximumWaterProductionTemperature=r.ReadDouble();
            p.WaterProductionSoilMinimum=r.ReadDouble();
            p.WaterProductionSoilFull=r.ReadDouble();
            p.WaterProductionSoilExponent=r.ReadDouble();
            p.NectarEvaporationRate=r.ReadDouble();
            p.NectarEvaporationWindFactor=r.ReadDouble();
            p.PollenDevelopmentHours=r.ReadDouble();
            p.PollenProductionFactor=r.ReadDouble();
            p.PollenHeatStartTemperature=r.ReadDouble();
            p.PollenHeatZeroTemperature=r.ReadDouble();
            p.PollenSoilMinimum=r.ReadDouble();
            p.PollenSoilFull=r.ReadDouble();
            p.PollenRainLossCoefficient=r.ReadDouble();
            p.BaseNectarYieldRatio=r.ReadDouble();
            p.AnnualYieldFactor=r.ReadDouble();
            p.MinimumBloomingFlowerRatio=r.ReadDouble();
            p.NectarSugarMolarMass=r.ReadDouble();
            p.NectarNorrishConstant=r.ReadDouble();
        }
        return result;
    }
}
