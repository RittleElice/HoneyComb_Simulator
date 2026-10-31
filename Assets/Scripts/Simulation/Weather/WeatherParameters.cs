using System;

// Exact Ver.2.0 parameter expressions; random initial values are regenerated from the seed.
internal sealed class WeatherParameters
{
    public double Cloud_Slow_p; // Parameter!A2
    public double Wind_Day_AR; // Parameter!B2
    public double Rain_Supply_p; // Parameter!C2
    public double Snow_Temperature; // Parameter!D2
    public double Vapor_AR; // Parameter!E2
    public double Solar_CloudFactor; // Parameter!F2
    public double Soil_InitialEnergy; // Parameter!G2
    public double SoilWater_MaxStorage; // Parameter!H2
    public double Cloud_Slow_SD; // Parameter!A4
    public double Wind_Day_SD; // Parameter!B4
    public double Rain_Supply_SD; // Parameter!C4
    public double Rain_Temperature; // Parameter!D4
    public double Vapor_SD; // Parameter!E4
    public double Solar_CloudExponent; // Parameter!F4
    public double Soil_HeatCapacity; // Parameter!G4
    public double SoilWater_InitialStorage; // Parameter!H4
    public double Cloud_Fast_p; // Parameter!A6
    public double Wind_Hour_AR; // Parameter!B6
    public double Rain_Supply_AdjustedBaseline_Initial_rand; // Parameter!C6
    public double Snow_MeltFactor; // Parameter!D6
    public double Vapor_SupplyFactor; // Parameter!E6
    public double Solar_RainFactor; // Parameter!F6
    public double Soil_InitialTemperature; // Parameter!G6
    public double SoilWater_Input_Ratio; // Parameter!H6
    public double Cloud_Fast_SD; // Parameter!A8
    public double Wind_Hour_SD; // Parameter!B8
    public double Rain_Storage_Initial; // Parameter!C8
    public double Snow_MeltTemperature; // Parameter!D8
    public double Vapor_VariationExponent; // Parameter!E8
    public double Solar_RainExponent; // Parameter!F8
    public double Surface_InitialEnergy; // Parameter!G8
    public double SoilWater_Evaporation_HumidityFactor; // Parameter!H8
    public double Cloud_Over; // Parameter!A10
    public double Wind_K; // Parameter!B10
    public double Rain_Start_Moisture; // Parameter!C10
    public double Snow_FullCoverDepth; // Parameter!D10
    public double Vapor_CloudFactor; // Parameter!E10
    public double Solar_SeasonLogMean; // Parameter!F10
    public double Surface_HeatCapacity; // Parameter!G10
    public double SoilWater_Evaporation_ReferenceTemperature; // Parameter!H10
    public double Cloud_Polarize; // Parameter!A12
    public double Wind_K_S; // Parameter!B12
    public double Rain_Start_CloudReduction; // Parameter!C12
    public double Snow_Density; // Parameter!D12
    public double Vapor_RainFactor; // Parameter!E12
    public double Solar_SeasonCos1; // Parameter!F12
    public double Surface_InitialTemperature; // Parameter!G12
    public double SoilWater_Evaporation_PsychrometricConstant; // Parameter!H12
    public double Cloud_Slow_Initial_rand; // Parameter!A14
    public double Wind_K_D; // Parameter!B14
    public double Rain_Stop_Moisture; // Parameter!C14
    public double Snow_InitialStorage; // Parameter!D14
    public double Vapor_RainOffset; // Parameter!E14
    public double Solar_SeasonSin1; // Parameter!F14
    public double Air_InitialEnergy; // Parameter!G14
    public double SoilWater_Evaporation_SolarFactor; // Parameter!H14
    public double Cloud_Fast_Initial_rand; // Parameter!A16
    public double Wind_K_H; // Parameter!B16
    public double Rain_Stop_CloudReduction; // Parameter!C16
    public double Snow_InitialDepth; // Parameter!D16
    public double Vapor_RainScale; // Parameter!E16
    public double Solar_SeasonCos2; // Parameter!F16
    public double Air_HeatCapacity; // Parameter!G16
    public double SoilWater_Evaporation_AirFactor; // Parameter!H16
    public double Wind_Zero; // Parameter!B18
    public double Rain_Start_Intensity; // Parameter!C18
    public double Snow_MaxDensity; // Parameter!D18
    public double Vapor_RainPersistence; // Parameter!E18
    public double Solar_SeasonSin2; // Parameter!F18
    public double Air_InitialTemperature; // Parameter!G18
    public double SoilWater_Evaporation_WindFactor; // Parameter!H18
    public double Wind_Day_Initial; // Parameter!B20
    public double Rain_Second_Intensity; // Parameter!C20
    public double Snow_CompactionRate; // Parameter!D20
    public double Vapor_RainRisePersistence; // Parameter!E20
    public double Solar_AirMassPower; // Parameter!F20
    public double Soil_Surface_TransferFactor; // Parameter!G20
    public double SoilWater_Evaporation_Scale; // Parameter!H20
    public double Wind_Hour_Initial; // Parameter!B22
    public double Rain_Intensity; // Parameter!C22
    public double Snow_SolarMeltFactor; // Parameter!D22
    public double Vapor_OutputPersistence; // Parameter!E22
    public double Surface_Air_TransferFactor; // Parameter!G22
    public double SoilWater_Evaporation_StressThreshold; // Parameter!H22
    public double Rain_Min_Amount; // Parameter!C24
    public double Snow_SolarMeltColdRange; // Parameter!D24
    public double Vapor_LevelScale; // Parameter!E24
    public double Surface_SolarFactor; // Parameter!G24
    public double Rain_Start_Probability; // Parameter!C26
    public double Vapor_PreRain_Max; // Parameter!E26
    public double Surface_SnowReflectance; // Parameter!G26
    public double Rain_Start_CloudExponent; // Parameter!C28
    public double Vapor_PreRain_StorageWindow; // Parameter!E28
    public double Air_SolarFactor; // Parameter!G28
    public double Rain_Continue_Probability; // Parameter!C30
    public double Vapor_PreRain_CloudFloor; // Parameter!E30
    public double Surface_RadiationFactor; // Parameter!G30
    public double Rain_Continue_CloudExponent; // Parameter!C32
    public double Vapor_PreRain_Persistence; // Parameter!E32
    public double Surface_RadiationReferenceTemperature; // Parameter!G32
    public double Rain_Long_Continue_Probability; // Parameter!C34
    public double Air_RadiationFactor; // Parameter!G34
    public double Rain_Transition_Minutes; // Parameter!C36
    public double Air_RadiationReferenceTemperature; // Parameter!G36
    public double Rain_Intensity_AR; // Parameter!C38
    public double Air_VaporCoolingFactor; // Parameter!G38
    public double Rain_Intensity_SD; // Parameter!C40
    public double Air_CoolingNormalization; // Parameter!G40
    public double Rain_Intensity_Smooth; // Parameter!C42
    public double Air_RainCoolingFactor; // Parameter!G42
    public double Rain_Intensity_Initial_rand; // Parameter!C44
    public double Air_RainCoolingScale; // Parameter!G44
    public double Rain_Intensity_Smoothed_Initial_rand; // Parameter!C46
    public double Air_SaturationAdjustmentFactor; // Parameter!G46
    public double Rain_Intensity_Variance; // Parameter!C48
    public double Rain_Edge_Window; // Parameter!C50
    public static WeatherParameters Create(IWeatherRandom random)
    {
        var p = new WeatherParameters();
        p.Wind_Day_AR = 0.48699999999999999d;
        p.Snow_Temperature = -1d;
        p.Solar_CloudFactor = 0.77929999999999999d;
        p.SoilWater_MaxStorage = 100d;
        p.Cloud_Slow_SD = 0.82079999999999997d;
        p.Wind_Day_SD = 0.33522839999999998d;
        p.Rain_Supply_SD = 0.85d;
        p.Rain_Temperature = 2d;
        p.Vapor_SD = 0.17299999999999999d;
        p.Solar_CloudExponent = 3.0585399999999998d;
        p.Soil_HeatCapacity = 33.100772200000002d;
        p.SoilWater_InitialStorage = 70d;
        p.Snow_MeltFactor = 2.1875000000000002E-3d;
        p.Vapor_SupplyFactor = 0.25700000000000001d;
        p.Solar_RainFactor = 0.97758d;
        p.SoilWater_Input_Ratio = 0.8d;
        p.Cloud_Fast_SD = 7.4999999999999997E-2d;
        p.Rain_Storage_Initial = 13d;
        p.Snow_MeltTemperature = 0d;
        p.Vapor_VariationExponent = 0.7d;
        p.Solar_RainExponent = 0.32774999999999999d;
        p.SoilWater_Evaporation_HumidityFactor = 1d;
        p.Cloud_Over = 0d;
        p.Wind_K = 0.67700000000000005d;
        p.Rain_Start_Moisture = 19.50356d;
        p.Snow_FullCoverDepth = 5d;
        p.Vapor_CloudFactor = 0.28999999999999998d;
        p.Solar_SeasonLogMean = -0.1027248d;
        p.Surface_HeatCapacity = 0.90866457300000003d;
        p.SoilWater_Evaporation_ReferenceTemperature = 20d;
        p.Cloud_Polarize = 0.70479999999999998d;
        p.Wind_K_S = 0.48599999999999999d;
        p.Rain_Start_CloudReduction = 12.69122d;
        p.Snow_Density = 70d;
        p.Vapor_RainFactor = 0.98d;
        p.Solar_SeasonCos1 = -3.0415299999999999E-2d;
        p.Surface_InitialTemperature = 276.14999999999998d;
        p.SoilWater_Evaporation_PsychrometricConstant = 6.6500000000000004E-2d;
        p.Wind_K_D = 0.48499999999999999d;
        p.Rain_Stop_Moisture = 15.030659999999999d;
        p.Vapor_RainOffset = 4.87d;
        p.Solar_SeasonSin1 = -9.2107000000000005E-3d;
        p.SoilWater_Evaporation_SolarFactor = 0.1d;
        p.Wind_K_H = 0.36799999999999999d;
        p.Rain_Stop_CloudReduction = 8.9464199999999998d;
        p.Snow_InitialDepth = 0d;
        p.Solar_SeasonCos2 = -2.7213999999999999E-2d;
        p.Air_HeatCapacity = 3.3378757589999995E-2d;
        p.SoilWater_Evaporation_AirFactor = 8.3333333333333339E-4d;
        p.Wind_Zero = 1.714d;
        p.Snow_MaxDensity = 300d;
        p.Solar_SeasonSin2 = 9.1149999999999998E-4d;
        p.Air_InitialTemperature = 270.14999999999998d;
        p.SoilWater_Evaporation_WindFactor = 0.4d;
        p.Snow_CompactionRate = 2.0000000000000001E-4d;
        p.Solar_AirMassPower = 0.27254d;
        p.Soil_Surface_TransferFactor = 2.1518251500000001E-5d;
        p.SoilWater_Evaporation_Scale = 0.88d;
        p.Snow_SolarMeltFactor = 0.04d;
        p.Surface_Air_TransferFactor = 1.3962531560656502E-4d;
        p.SoilWater_Evaporation_StressThreshold = 0.4d;
        p.Snow_SolarMeltColdRange = 5d;
        p.Vapor_LevelScale = 0.96199999999999997d;
        p.Surface_SolarFactor = 9.8799672556411002E-3d;
        p.Rain_Start_Probability = 0.13331000000000001d;
        p.Vapor_PreRain_Max = 0.65d;
        p.Surface_SnowReflectance = 0.69397799999999998d;
        p.Rain_Start_CloudExponent = 107.33960999999999d;
        p.Vapor_PreRain_StorageWindow = 1d;
        p.Air_SolarFactor = 5.4578877364178997E-2d;
        p.Rain_Continue_Probability = 0.51875000000000004d;
        p.Vapor_PreRain_CloudFloor = 0.8d;
        p.Surface_RadiationFactor = 6.2108484065358997E-4d;
        p.Rain_Continue_CloudExponent = 45.656059999999997d;
        p.Surface_RadiationReferenceTemperature = 191.85058699999999d;
        p.Rain_Long_Continue_Probability = 0.94674999999999998d;
        p.Air_RadiationFactor = 0.24583174340575104d;
        p.Rain_Transition_Minutes = 30d;
        p.Air_RadiationReferenceTemperature = 219.93547000000001d;
        p.Air_VaporCoolingFactor = 0.48d;
        p.Rain_Intensity_SD = 1.0443800000000001d;
        p.Air_CoolingNormalization = 1.0062230000000001d;
        p.Air_RainCoolingFactor = 0.18978327833333333d;
        p.Air_RainCoolingScale = 1.6666666666666666E-2d;
        p.Air_SaturationAdjustmentFactor = 1d;
        p.Rain_Edge_Window = 5d;
        p.Cloud_Slow_Initial_rand = (random.Normal(WeatherStream.CloudSlow) * p.Cloud_Slow_SD);
        p.Cloud_Fast_Initial_rand = (random.Normal(WeatherStream.CloudFast) * p.Cloud_Fast_SD);
        p.Cloud_Slow_p = Math.Pow(0.9342d, (1d / 60d));
        p.Cloud_Fast_p = Math.Pow(0.3676d, (1d / 60d));
        p.Wind_Day_Initial = ((random.Normal(WeatherStream.WindDay) * p.Wind_Day_SD) / Math.Sqrt((1d - Math.Pow(p.Wind_Day_AR, 2d))));
        p.Wind_Hour_AR = Math.Pow(0.563d, (1d / 60d));
        p.Rain_Start_Intensity = (0.16205d / 60d);
        p.Rain_Supply_p = Math.Pow(0.988d, (1d / 60d));
        p.Rain_Second_Intensity = (0.0231d / 60d);
        p.Rain_Intensity = (0.26472d / 60d);
        p.Rain_Min_Amount = (0.0629d / 60d);
        p.Rain_Intensity_AR = Math.Pow(0.4d, (1d / 60d));
        p.Rain_Intensity_Smooth = Math.Exp(((-1d) / 10d));
        p.Rain_Intensity_Initial_rand = (random.Normal(WeatherStream.RainIntensity) * p.Rain_Intensity_SD);
        p.Rain_Supply_AdjustedBaseline_Initial_rand = (random.Normal(WeatherStream.RainSupply) * p.Rain_Supply_SD);
        p.Snow_InitialStorage = ((p.Snow_Density / 100d) * p.Snow_InitialDepth);
        p.Vapor_RainScale = (0.1d / 60d);
        p.Vapor_RainPersistence = Math.Exp(((-1d) / (60d * 6.35d)));
        p.Vapor_AR = Math.Pow(0.9917d, (1d / 60d));
        p.Vapor_RainRisePersistence = Math.Exp(((-1d) / (60d * 0.59d)));
        p.Vapor_OutputPersistence = Math.Exp(((-1d) / (60d * 0.41d)));
        p.Vapor_PreRain_Persistence = Math.Exp(((-1d) / (60d * 0.75d)));
        p.Air_InitialEnergy = (p.Air_HeatCapacity * p.Air_InitialTemperature);
        p.Soil_InitialTemperature = (273.15d + 15.84d);
        p.Surface_InitialEnergy = (p.Surface_HeatCapacity * p.Surface_InitialTemperature);
        p.Wind_Hour_SD = (0.4701442d * Math.Sqrt(((1d - Math.Pow(p.Wind_Hour_AR, 2d)) / (1d - Math.Pow(0.563d, 2d)))));
        p.Rain_Intensity_Variance = ((((Math.Pow(p.Rain_Intensity_SD, 2d) * (1d - p.Rain_Intensity_Smooth)) / (1d + p.Rain_Intensity_Smooth)) * (1d + (p.Rain_Intensity_Smooth * p.Rain_Intensity_AR))) / (1d - (p.Rain_Intensity_Smooth * p.Rain_Intensity_AR)));
        p.Soil_InitialEnergy = (p.Soil_HeatCapacity * p.Soil_InitialTemperature);
        p.Wind_Hour_Initial = ((random.Normal(WeatherStream.WindMinute) * p.Wind_Hour_SD) / Math.Sqrt((1d - Math.Pow(p.Wind_Hour_AR, 2d))));
        p.Rain_Intensity_Smoothed_Initial_rand = ((((1d - p.Rain_Intensity_Smooth) / (1d - (p.Rain_Intensity_Smooth * p.Rain_Intensity_AR))) * p.Rain_Intensity_Initial_rand) + (Math.Sqrt((p.Rain_Intensity_Variance - (Math.Pow(((1d - p.Rain_Intensity_Smooth) / (1d - (p.Rain_Intensity_Smooth * p.Rain_Intensity_AR))), 2d) * Math.Pow(p.Rain_Intensity_SD, 2d)))) * random.Normal(WeatherStream.RainInitialSmooth)));
        return p;
    }
}
