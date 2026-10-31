using System;

// Compiled from FormulaDefinitions.csv. No Excel/formula interpreter runs in the player.
internal static class WeatherEquations
{
    public static void Evaluate(long minute, WeatherParameters p, WeatherValues previous,
        WeatherValues c, WeatherWindows windows, IWeatherRandom random)
    {
        c.Total_Time = minute;
        c.Total_Day = minute / 1440 + 1;
        c.Total_Year = minute / 525600 + 1;
        c.Year_Day = minute / 1440 % 365 + 1;
        c.Date_Min = minute % 1440;
        var date = new DateTime(2001, 1, 1).AddDays(c.Year_Day - 1);
        c.Date_Month = date.Month;
        c.Date_Day = date.Day;
        c.Vapor_PreRain = 0d; // Calculate!AG2 is the sole literal initial calculation cell.
        // Calculate!AT — Calculate_AT_045
        c.Surface_Air_HeatTransfer = (p.Surface_Air_TransferFactor * ((((minute + 2d) == 2d) ? p.Surface_InitialTemperature : previous.Surface_Temperature) - (((minute + 2d) == 2d) ? p.Air_InitialTemperature : previous.Air_Temperature)));
        // Calculate!T — Calculate_T_020
        c.Snow_Fraction = Math.Min(1d, Math.Max(0d, ((p.Rain_Temperature - ((((minute + 2d) == 2d) ? p.Air_InitialTemperature : previous.Air_Temperature) - 273.15d)) / (p.Rain_Temperature - p.Snow_Temperature))));
        // Calculate!A — Calculate_A_001, Calculate_A_058
        if (minute == 0)
        {
            c.Cloud_Slow = p.Cloud_Slow_Initial_rand;
        }
        else
        {
            c.Cloud_Slow = ((p.Cloud_Slow_p * previous.Cloud_Slow) + ((random.Normal(WeatherStream.CloudSlow) * p.Cloud_Slow_SD) * Math.Sqrt((1d - Math.Pow(p.Cloud_Slow_p, 2d)))));
        }
        // Calculate!AB — Calculate_AB_028, Calculate_AB_068
        if (minute == 0)
        {
            c.Vapor_Variation = (p.Vapor_SD * random.Normal(WeatherStream.VaporVariation));
        }
        else
        {
            c.Vapor_Variation = ((p.Vapor_AR * previous.Vapor_Variation) + ((p.Vapor_SD * Math.Sqrt((1d - Math.Pow(p.Vapor_AR, 2d)))) * random.Normal(WeatherStream.VaporVariation)));
        }
        // Calculate!AO — Calculate_AO_040
        c.Soil_Surface_HeatTransfer = (p.Soil_Surface_TransferFactor * ((((minute + 2d) == 2d) ? p.Surface_InitialTemperature : previous.Surface_Temperature) - (((minute + 2d) == 2d) ? p.Soil_InitialTemperature : previous.Soil_Temperature)));
        // Calculate!B — Calculate_B_002, Calculate_B_059
        if (minute == 0)
        {
            c.Cloud_Fast = p.Cloud_Fast_Initial_rand;
        }
        else
        {
            c.Cloud_Fast = ((p.Cloud_Fast_p * previous.Cloud_Fast) + ((random.Normal(WeatherStream.CloudFast) * p.Cloud_Fast_SD) * Math.Sqrt((1d - Math.Pow(p.Cloud_Fast_p, 2d)))));
        }
        // Calculate!F — Calculate_F_006, Calculate_F_060
        if (minute == 0)
        {
            c.Wind_Day = p.Wind_Day_Initial;
        }
        else
        {
            c.Wind_Day = ((c.Date_Min == 0d) ? ((p.Wind_Day_AR * previous.Wind_Day) + (random.Normal(WeatherStream.WindDay) * p.Wind_Day_SD)) : previous.Wind_Day);
        }
        // Calculate!I — Calculate_I_009, Calculate_I_062
        if (minute == 0)
        {
            c.Rain_Supply_AdjustedBaseline = p.Rain_Supply_AdjustedBaseline_Initial_rand;
        }
        else
        {
            c.Rain_Supply_AdjustedBaseline = ((p.Rain_Supply_p * previous.Rain_Supply_AdjustedBaseline) + ((random.Normal(WeatherStream.RainSupply) * p.Rain_Supply_SD) * Math.Sqrt((1d - Math.Pow(p.Rain_Supply_p, 2d)))));
        }
        // Calculate!P — Calculate_P_016, Calculate_P_066
        if (minute == 0)
        {
            c.Rain_Intensity_Noise = p.Rain_Intensity_Initial_rand;
        }
        else
        {
            c.Rain_Intensity_Noise = ((p.Rain_Intensity_AR * previous.Rain_Intensity_Noise) + ((random.Normal(WeatherStream.RainIntensity) * p.Rain_Intensity_SD) * Math.Sqrt((1d - Math.Pow(p.Rain_Intensity_AR, 2d)))));
        }
        // Calculate!AC — Calculate_AC_029
        c.Vapor_Rain_Supply_AdjustedBaseline = (c.Vapor_Baseline * Math.Exp((((c.Vapor_Variation + (c.Rain_Supply_AdjustedBaseline * p.Vapor_SupplyFactor)) * Math.Pow((10d / c.Vapor_Baseline), p.Vapor_VariationExponent)) - ((0.5d * (Math.Pow((p.Vapor_SupplyFactor * p.Rain_Supply_SD), 2d) + Math.Pow(p.Vapor_SD, 2d))) * Math.Pow((10d / c.Vapor_Baseline), (2d * p.Vapor_VariationExponent))))));
        // Calculate!AL — Calculate_AL_037
        c.Soil_Energy = ((((minute + 2d) == 2d) ? p.Soil_InitialEnergy : previous.Soil_Energy) + c.Soil_Surface_HeatTransfer);
        // Calculate!C — Calculate_C_003
        c.Cloud_AdjustedBaseline = ((c.Cloud_Baseline + c.Cloud_Slow) + c.Cloud_Fast);
        // Calculate!J — Calculate_J_010
        c.Rain_Supply = (c.Rain_Supply_Baseline * Math.Exp((c.Rain_Supply_AdjustedBaseline - (Math.Pow(p.Rain_Supply_SD, 2d) / 2d))));
        // Calculate!AN — Calculate_AN_039
        c.Soil_Temperature = (c.Soil_Energy / p.Soil_HeatCapacity);
        // Calculate!D — Calculate_D_004
        c.Cloud_Overflow = ((c.Cloud_AdjustedBaseline < 0d) ? c.Cloud_AdjustedBaseline : ((c.Cloud_AdjustedBaseline > 1d) ? (c.Cloud_AdjustedBaseline - 1d) : 0d));
        // Calculate!E — Calculate_E_005
        c.Cloud_OverflowCorrected = Math.Max(0d, Math.Min(1d, c.Cloud_AdjustedBaseline));
        // Calculate!G — Calculate_G_007, Calculate_G_061
        if (minute == 0)
        {
            c.Wind_Hour = p.Wind_Hour_Initial;
        }
        else
        {
            c.Wind_Hour = ((p.Wind_Hour_AR * previous.Wind_Hour) + (random.Normal(WeatherStream.WindMinute) * p.Wind_Hour_SD));
        }
        // Calculate!K — Calculate_K_011, Calculate_K_063
        if (minute == 0)
        {
            c.Rain_Storage = (p.Rain_Storage_Initial + c.Rain_Supply);
        }
        else
        {
            c.Rain_Storage = ((previous.Rain_Storage + c.Rain_Supply) - previous.Rain_Emission);
        }
        // Calculate!Q — Calculate_Q_017, Calculate_Q_067
        if (minute == 0)
        {
            c.Rain_Intensity_Smoothed = p.Rain_Intensity_Smoothed_Initial_rand;
        }
        else
        {
            c.Rain_Intensity_Smoothed = ((p.Rain_Intensity_Smooth * previous.Rain_Intensity_Smoothed) + ((1d - p.Rain_Intensity_Smooth) * c.Rain_Intensity_Noise));
        }
        // Calculate!H — Calculate_H_008
        c.Wind_AdjustedBaseline = Math.Exp(((((c.Wind_Baseline + p.Wind_K) + (p.Wind_K_S * c.Wind_Season_Baseline)) + (p.Wind_K_D * c.Wind_Day)) + (p.Wind_K_H * c.Wind_Hour)));
        // Result!I — Result_I_008
        c.Cloud_Cover = Math.Pow(c.Cloud_OverflowCorrected, p.Cloud_Polarize);
        // Calculate!AD — Calculate_AD_030
        c.Vapor_Cloud = (c.Vapor_Rain_Supply_AdjustedBaseline * Math.Exp((p.Vapor_CloudFactor * (c.Cloud_Cover - c.Cloud_Avg))));
        // Calculate!AH — Calculate_AH_033
        c.Solar_Cloud_Effect = (1d - (p.Solar_CloudFactor * Math.Pow(c.Cloud_Cover, p.Solar_CloudExponent)));
        // Calculate!L — Calculate_L_012
        c.Rain_Start_Threshold = (p.Rain_Start_Moisture - (p.Rain_Start_CloudReduction * c.Cloud_Cover));
        // Calculate!M — Calculate_M_013
        c.Rain_Stop_Threshold = (p.Rain_Stop_Moisture - (p.Rain_Stop_CloudReduction * c.Cloud_Cover));
        // Result!J — Result_J_009
        c.Wind_Cover = Math.Max(0d, (c.Wind_AdjustedBaseline - p.Wind_Zero));
        // Calculate!N — Calculate_N_014, Calculate_N_064
        if (minute == 0)
        {
            c.Rain_Potential = Math.Min(c.Rain_Storage, (((Math.Exp((c.Rain_Intensity_Smoothed - (p.Rain_Intensity_Variance / 2d))) * (c.Rain_Storage - c.Rain_Start_Threshold)) * p.Rain_Start_Intensity) * c.Cloud_Cover));
        }
        else
        {
            double age = previous.Rain_State;
            double phaseA = Math.Max(0d, Math.Min(1d, (((age - 60d) + (p.Rain_Transition_Minutes / 2d)) / p.Rain_Transition_Minutes)));
            double phaseB = Math.Max(0d, Math.Min(1d, (((age - 120d) + (p.Rain_Transition_Minutes / 2d)) / p.Rain_Transition_Minutes)));
            double blendA = (Math.Pow(phaseA, 2d) * (3d - (2d * phaseA)));
            double blendB = (Math.Pow(phaseB, 2d) * (3d - (2d * phaseB)));
            double storage = c.Rain_Storage;
            double cloud = c.Cloud_Cover;
            c.Rain_Potential = Math.Min(storage, ((Math.Exp((c.Rain_Intensity_Smoothed - (p.Rain_Intensity_Variance / 2d))) * cloud) * (((((1d - blendA) * (storage - c.Rain_Start_Threshold)) * p.Rain_Start_Intensity) + (((blendA - blendB) * (storage - c.Rain_Stop_Threshold)) * p.Rain_Second_Intensity)) + ((blendB * (storage - c.Rain_Stop_Threshold)) * p.Rain_Intensity))));
        }
        // Calculate!O — Calculate_O_015, Calculate_O_065
        if (minute == 0)
        {
            c.Rain_State = ((c.Rain_Potential < p.Rain_Min_Amount) ? 0d : ((random.Uniform(WeatherStream.RainDecision) < (1d - Math.Pow((1d - (p.Rain_Start_Probability * Math.Pow(c.Cloud_Cover, p.Rain_Start_CloudExponent))), (1d / 60d)))) ? 1d : 0d));
        }
        else
        {
            c.Rain_State = ((c.Rain_Potential < p.Rain_Min_Amount) ? 0d : ((previous.Rain_State == 0d) ? ((random.Uniform(WeatherStream.RainDecision) < (1d - Math.Pow((1d - (p.Rain_Start_Probability * Math.Pow(c.Cloud_Cover, p.Rain_Start_CloudExponent))), (1d / 60d)))) ? 1d : 0d) : ((random.Uniform(WeatherStream.RainDecision) < Math.Pow((((previous.Rain_State < 120d) ? p.Rain_Continue_Probability : p.Rain_Long_Continue_Probability) * Math.Pow(c.Cloud_Cover, p.Rain_Continue_CloudExponent)), (1d / 60d))) ? Math.Min((previous.Rain_State + 1d), Math.Max(121d, (Math.Ceiling((120d + (p.Rain_Transition_Minutes / 2d))) + 1d))) : 0d)));
        }
        // Calculate!R — Calculate_R_018
        c.Rain_Emission = ((c.Rain_State > 0d) ? c.Rain_Potential : 0d);
        // Calculate!S — Calculate_S_019
        c.Rain_Edge_Pass1 = windows.MeanEmission(c.Rain_Emission);
        // Result!K — Result_K_010
        c.Precipitation = windows.MeanPass1(c.Rain_Edge_Pass1);
        // Calculate!AE — Calculate_AE_031, Calculate_AE_069
        if (minute == 0)
        {
            c.Rain_Wetness = (c.Precipitation / (c.Precipitation + p.Vapor_RainScale));
        }
        else
        {
            c.Rain_Wetness = (previous.Rain_Wetness + (((c.Precipitation / (c.Precipitation + p.Vapor_RainScale)) - previous.Rain_Wetness) * (1d - (((c.Precipitation / (c.Precipitation + p.Vapor_RainScale)) > previous.Rain_Wetness) ? p.Vapor_RainRisePersistence : p.Vapor_RainPersistence))));
        }
        // Calculate!AI — Calculate_AI_034
        c.Solar_Rain_Effect = Math.Exp(((-p.Solar_RainFactor) * Math.Pow((c.Precipitation * 60d), p.Solar_RainExponent)));
        // Calculate!U — Calculate_U_021
        c.Rain_Amount = (c.Precipitation * (1d - c.Snow_Fraction));
        // Calculate!V — Calculate_V_022
        c.Snow_Amount = (c.Precipitation * c.Snow_Fraction);
        // Calculate!AG — Calculate_AG_070
        if (minute != 0)
        {
            c.Vapor_PreRain = ((p.Vapor_PreRain_Persistence * previous.Vapor_PreRain) + ((((((1d - p.Vapor_PreRain_Persistence) * p.Vapor_PreRain_Max) * Math.Max(0d, Math.Min(1d, (((c.Rain_Storage - c.Rain_Start_Threshold) + p.Vapor_PreRain_StorageWindow) / p.Vapor_PreRain_StorageWindow)))) * Math.Max(0d, Math.Min(1d, ((c.Cloud_Cover - p.Vapor_PreRain_CloudFloor) / (1d - p.Vapor_PreRain_CloudFloor))))) * Math.Max(0d, Math.Min(1d, (0.5d + ((0.5d * c.Rain_Supply_AdjustedBaseline) / p.Rain_Supply_SD))))) * (1d - c.Rain_Wetness)));
        }
        // Calculate!AF — Calculate_AF_032
        c.Vapor_Rain_Effect = ((p.Vapor_RainFactor * Math.Max(c.Rain_Wetness, c.Vapor_PreRain)) * Math.Max(0d, ((c.Vapor_Baseline + p.Vapor_RainOffset) - c.Vapor_Cloud)));
        // Result!L — Result_L_011, Result_L_016
        if (minute == 0)
        {
            c.Vapor_Cover = (p.Vapor_LevelScale * (c.Vapor_Cloud + c.Vapor_Rain_Effect));
        }
        else
        {
            c.Vapor_Cover = ((p.Vapor_OutputPersistence * previous.Vapor_Cover) + (((1d - p.Vapor_OutputPersistence) * p.Vapor_LevelScale) * (c.Vapor_Cloud + c.Vapor_Rain_Effect)));
        }
        // Calculate!AJ — Calculate_AJ_035
        c.PrecipitableWater = Math.Max(0d, (((1.7d * c.Vapor_Cover) - 0.1d) / 10d));
        // Calculate!AM — Calculate_AM_038
        c.Sky_Emissivity = ((((1d - (0.84d * c.Cloud_Cover)) * 1.24d) * Math.Pow((c.Vapor_Cover / (((minute + 2d) == 2d) ? p.Air_InitialTemperature : previous.Air_Temperature)), (1d / 7d))) + (0.84d * c.Cloud_Cover));
        // Calculate!AW — Calculate_AW_048
        c.Air_DewPoint = ((243.5d * Math.Log((c.Vapor_Cover / 6.112d))) / (17.67d - Math.Log((c.Vapor_Cover / 6.112d))));
        // Calculate!AK — Calculate_AK_036
        c.Solar_Vapor_Effect = (1d - ((2.4959d * (c.PrecipitableWater * c.AirMass)) / (Math.Pow((1d + (79.034d * (c.PrecipitableWater * c.AirMass))), 0.6828d) + (6.385d * (c.PrecipitableWater * c.AirMass)))));
        // Calculate!AQ — Calculate_AQ_042
        c.Surface_RadiationEnergy = ((((p.Surface_RadiationFactor * 0.00006d) * 0.00000005670374419d) * (Math.Pow((((minute + 2d) == 2d) ? p.Surface_InitialTemperature : previous.Surface_Temperature), 4d) - Math.Pow(p.Surface_RadiationReferenceTemperature, 4d))) * (1d - c.Sky_Emissivity));
        // Calculate!AV — Calculate_AV_047
        c.Air_RadiationEnergy = ((((((p.Air_RadiationFactor * 0.00006d) * 0.00000005670374419d) * (Math.Pow((((minute + 2d) == 2d) ? p.Air_InitialTemperature : previous.Air_Temperature), 4d) - Math.Pow(p.Air_RadiationReferenceTemperature, 4d))) * (1d - c.Sky_Emissivity)) * Math.Exp(((-p.Air_VaporCoolingFactor) * Math.Log((c.Vapor_Cover / c.Vapor_Baseline))))) / p.Air_CoolingNormalization);
        // Calculate!AX — Calculate_AX_049
        c.Air_RainCooling = (p.Air_HeatCapacity * Math.Min(Math.Max(0d, (((((minute + 2d) == 2d) ? p.Air_InitialTemperature : previous.Air_Temperature) - 273.15d) - c.Air_DewPoint)), (((p.Air_RainCoolingFactor * c.Rain_Amount) / (c.Rain_Amount + p.Air_RainCoolingScale)) * Math.Max(0d, (1d - Math.Min(1d, (c.Vapor_Cover / (6.112d * Math.Exp(((17.67d * ((((minute + 2d) == 2d) ? p.Air_InitialTemperature : previous.Air_Temperature) - 273.15d)) / (((((minute + 2d) == 2d) ? p.Air_InitialTemperature : previous.Air_Temperature) - 273.15d) + 243.5d)))))))))));
        // Result!M — Result_M_012
        c.Solar_Cover = (((((c.Solar_Baseline * c.Solar_Cloud_Effect) * c.Solar_Rain_Effect) * c.Solar_Vapor_Effect) * Math.Pow(Math.Max(1d, c.AirMass), p.Solar_AirMassPower)) * Math.Exp(((((p.Solar_SeasonLogMean + (p.Solar_SeasonCos1 * Math.Cos((((2d * Math.PI) * ((c.Total_Time % 525600d) + 0.5d)) / 525600d)))) + (p.Solar_SeasonSin1 * Math.Sin((((2d * Math.PI) * ((c.Total_Time % 525600d) + 0.5d)) / 525600d)))) + (p.Solar_SeasonCos2 * Math.Cos((((4d * Math.PI) * ((c.Total_Time % 525600d) + 0.5d)) / 525600d)))) + (p.Solar_SeasonSin2 * Math.Sin((((4d * Math.PI) * ((c.Total_Time % 525600d) + 0.5d)) / 525600d))))));
        // Calculate!AU — Calculate_AU_046
        c.Air_SolarEnergy = (c.Solar_Cover * p.Air_SolarFactor);
        // Calculate!X — Calculate_X_024
        c.Snow_Melt = (((minute + 2d) == 2d) ? 0d : Math.Min((((minute + 2d) == 2d) ? p.Snow_InitialStorage : previous.Snow_Storage), ((p.Snow_MeltFactor * Math.Max((((((minute + 2d) == 2d) ? p.Air_InitialTemperature : previous.Air_Temperature) - 273.15d) - p.Snow_MeltTemperature), 0d)) + ((p.Snow_SolarMeltFactor * c.Solar_Cover) * Math.Max(0d, Math.Min(1d, ((((((minute + 2d) == 2d) ? p.Air_InitialTemperature : previous.Air_Temperature) - 273.15d) + p.Snow_SolarMeltColdRange) / p.Snow_SolarMeltColdRange)))))));
        // Calculate!AY — Calculate_AY_050
        c.Air_SaturationAdjustment = (p.Air_SaturationAdjustmentFactor * Math.Max(0d, ((p.Air_HeatCapacity * (c.Air_DewPoint + 273.15d)) - (((((((minute + 2d) == 2d) ? p.Air_InitialEnergy : previous.Air_Energy) + c.Surface_Air_HeatTransfer) + c.Air_SolarEnergy) - c.Air_RadiationEnergy) - c.Air_RainCooling))));
        // Calculate!BB — Calculate_BB_053
        c.SoilWater_Input = ((c.Rain_Amount + c.Snow_Melt) * p.SoilWater_Input_Ratio);
        // Calculate!W — Calculate_W_023
        c.Snow_Storage = (((((minute + 2d) == 2d) ? p.Snow_InitialStorage : previous.Snow_Storage) + c.Snow_Amount) - c.Snow_Melt);
        // Calculate!Y — Calculate_Y_025
        c.Snow_Compaction = (Math.Max(0d, ((((((minute + 2d) == 2d) ? p.Snow_InitialStorage : previous.Snow_Storage) > 0d) ? ((((minute + 2d) == 2d) ? p.Snow_InitialDepth : previous.Snow_Depth) * (1d - (c.Snow_Melt / (((minute + 2d) == 2d) ? p.Snow_InitialStorage : previous.Snow_Storage)))) : 0d) - ((Math.Max(0d, ((((minute + 2d) == 2d) ? p.Snow_InitialStorage : previous.Snow_Storage) - c.Snow_Melt)) / p.Snow_MaxDensity) * 100d))) * (1d - Math.Exp((-p.Snow_CompactionRate))));
        // Calculate!AZ — Calculate_AZ_051
        c.Air_Energy = ((((((((minute + 2d) == 2d) ? p.Air_InitialEnergy : previous.Air_Energy) + c.Surface_Air_HeatTransfer) + c.Air_SolarEnergy) - c.Air_RadiationEnergy) - c.Air_RainCooling) + c.Air_SaturationAdjustment);
        // Calculate!Z — Calculate_Z_026
        c.Snow_Depth = ((Math.Max(((Math.Max(0d, ((((minute + 2d) == 2d) ? p.Snow_InitialStorage : previous.Snow_Storage) - c.Snow_Melt)) / p.Snow_MaxDensity) * 100d), (((((minute + 2d) == 2d) ? p.Snow_InitialStorage : previous.Snow_Storage) > 0d) ? ((((minute + 2d) == 2d) ? p.Snow_InitialDepth : previous.Snow_Depth) * (1d - (c.Snow_Melt / (((minute + 2d) == 2d) ? p.Snow_InitialStorage : previous.Snow_Storage)))) : 0d)) - c.Snow_Compaction) + ((c.Snow_Amount / p.Snow_Density) * 100d));
        // Calculate!AA — Calculate_AA_027
        c.Snow_Cover = Math.Min(1d, (Math.Max(0d, c.Snow_Depth) / p.Snow_FullCoverDepth));
        // Calculate!BA — Calculate_BA_052
        c.Air_Temperature = (c.Air_Energy / p.Air_HeatCapacity);
        // Calculate!AP — Calculate_AP_041
        c.Surface_SolarEnergy = ((p.Surface_SolarFactor * (1d - (p.Surface_SnowReflectance * c.Snow_Cover))) * c.Solar_Cover);
        // Result!N — Result_N_013
        c.Temperature_Cover = (c.Air_Temperature - 273.15d);
        // Calculate!AR — Calculate_AR_043
        c.Surface_Energy = (((((((minute + 2d) == 2d) ? p.Surface_InitialEnergy : previous.Surface_Energy) - c.Soil_Surface_HeatTransfer) - c.Surface_Air_HeatTransfer) + c.Surface_SolarEnergy) - c.Surface_RadiationEnergy);
        // Calculate!BD — Calculate_BD_055
        c.SoilWater_Evaporation_SolarResponse = ((((4098d * (0.6108d * Math.Exp(((17.27d * c.Temperature_Cover) / (c.Temperature_Cover + 237.3d))))) / Math.Pow((c.Temperature_Cover + 237.3d), 2d)) / (((4098d * (0.6108d * Math.Exp(((17.27d * c.Temperature_Cover) / (c.Temperature_Cover + 237.3d))))) / Math.Pow((c.Temperature_Cover + 237.3d), 2d)) + p.SoilWater_Evaporation_PsychrometricConstant)) / (((4098d * (0.6108d * Math.Exp(((17.27d * p.SoilWater_Evaporation_ReferenceTemperature) / (p.SoilWater_Evaporation_ReferenceTemperature + 237.3d))))) / Math.Pow((p.SoilWater_Evaporation_ReferenceTemperature + 237.3d), 2d)) / (((4098d * (0.6108d * Math.Exp(((17.27d * p.SoilWater_Evaporation_ReferenceTemperature) / (p.SoilWater_Evaporation_ReferenceTemperature + 237.3d))))) / Math.Pow((p.SoilWater_Evaporation_ReferenceTemperature + 237.3d), 2d)) + p.SoilWater_Evaporation_PsychrometricConstant)));
        // Result!O — Result_O_014
        c.RelativeHumidity = Math.Min(100d, ((100d * c.Vapor_Cover) / (6.112d * Math.Exp(((17.67d * c.Temperature_Cover) / (c.Temperature_Cover + 243.5d))))));
        // Calculate!AS — Calculate_AS_044
        c.Surface_Temperature = (c.Surface_Energy / p.Surface_HeatCapacity);
        // Calculate!BC — Calculate_BC_054
        c.SoilWater_VPD = Math.Max(0d, ((0.6108d * Math.Exp(((17.27d * c.Temperature_Cover) / (c.Temperature_Cover + 237.3d)))) * (1d - ((p.SoilWater_Evaporation_HumidityFactor * c.RelativeHumidity) / 100d))));
        // Calculate!BE — Calculate_BE_056
        c.SoilWater_Evaporation_Demand = Math.Max(0d, (p.SoilWater_Evaporation_Scale * (((p.SoilWater_Evaporation_SolarFactor * c.Solar_Cover) * c.SoilWater_Evaporation_SolarResponse) + (((p.SoilWater_Evaporation_AirFactor * c.SoilWater_VPD) / (0.6108d * Math.Exp(((17.27d * p.SoilWater_Evaporation_ReferenceTemperature) / (p.SoilWater_Evaporation_ReferenceTemperature + 237.3d))))) * (1d + (p.SoilWater_Evaporation_WindFactor * c.Wind_Cover))))));
        // Calculate!BF — Calculate_BF_057
        c.SoilWater_Evapotranspiration = Math.Min((((minute + 2d) == 2d) ? p.SoilWater_InitialStorage : previous.SoilWater_Cover), (c.SoilWater_Evaporation_Demand * Math.Min(1d, ((((minute + 2d) == 2d) ? p.SoilWater_InitialStorage : previous.SoilWater_Cover) / (p.SoilWater_MaxStorage * p.SoilWater_Evaporation_StressThreshold)))));
        // Result!P — Result_P_015
        c.SoilWater_Cover = Math.Max(0d, Math.Min(p.SoilWater_MaxStorage, (((((minute + 2d) == 2d) ? p.SoilWater_InitialStorage : previous.SoilWater_Cover) + c.SoilWater_Input) - c.SoilWater_Evapotranspiration)));
    }
}
