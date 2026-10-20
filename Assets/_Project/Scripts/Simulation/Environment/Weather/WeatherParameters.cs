using System;
namespace HoneyComb.Simulation.Environment.Weather
{
    [Serializable]
    public sealed class WeatherParameters
    {
        // Cloud_Slow_p
        public double A2 = 0.9988662310203552;
        // Wind_Day_AR
        public double B2 = 0.487;
        // Rain_Supply_p
        public double C2 = 0.9997988105540737;
        // Snow_Temperature
        public double D2 = -1.0;
        // Vapor_AR
        public double E2 = 0.9998610990344649;
        // Solar_CloudFactor
        public double F2 = 0.7793;
        // Soil_InitialEnergy
        public double G2 = 9565.792158077998;
        // SoilWater_MaxStorage
        public double H2 = 100.0;
        // Cloud_Slow_SD
        public double A4 = 0.8208;
        // Wind_Day_SD
        public double B4 = 0.3352284;
        // Rain_Supply_SD
        public double C4 = 0.85;
        // Rain_Temperature
        public double D4 = 2.0;
        // Vapor_SD
        public double E4 = 0.173;
        // Solar_CloudExponent
        public double F4 = 3.05854;
        // Soil_HeatCapacity
        public double G4 = 33.1007722;
        // SoilWater_InitialStorage
        public double H4 = 70.0;
        // Cloud_Fast_p
        public double A6 = 0.9834589984230164;
        // Wind_Hour_AR
        public double B6 = 0.9904710963072534;
        // Rain_Supply_AdjustedBaseline_Initial_rand
        public double C6 = 0.9524855240260738;
        // Snow_MeltFactor
        public double D6 = 0.0021875;
        // Vapor_SupplyFactor
        public double E6 = 0.257;
        // Solar_RainFactor
        public double F6 = 0.97758;
        // Soil_InitialTemperature
        public double G6 = 288.98999999999995;
        // SoilWater_Input_Ratio
        public double H6 = 0.8;
        // Cloud_Fast_SD
        public double A8 = 0.075;
        // Wind_Hour_SD
        public double B8 = 0.07834481905317466;
        // Rain_Storage_Initial
        public double C8 = 13.0;
        // Snow_MeltTemperature
        public double D8 = 0.0;
        // Vapor_VariationExponent
        public double E8 = 0.7;
        // Solar_RainExponent
        public double F8 = 0.32775;
        // Surface_InitialEnergy
        public double G8 = 250.92772183395;
        // SoilWater_Evaporation_HumidityFactor
        public double H8 = 1.0;
        // Cloud_Over
        public double A10 = 0.0;
        // Wind_K
        public double B10 = 0.677;
        // Rain_Start_Moisture
        public double C10 = 19.50356;
        // Snow_FullCoverDepth
        public double D10 = 5.0;
        // Vapor_CloudFactor
        public double E10 = 0.29;
        // Solar_SeasonLogMean
        public double F10 = -0.1027248;
        // Surface_HeatCapacity
        public double G10 = 0.908664573;
        // SoilWater_Evaporation_ReferenceTemperature
        public double H10 = 20.0;
        // Cloud_Polarize
        public double A12 = 0.7048;
        // Wind_K_S
        public double B12 = 0.486;
        // Rain_Start_CloudReduction
        public double C12 = 12.69122;
        // Snow_Density
        public double D12 = 70.0;
        // Vapor_RainFactor
        public double E12 = 0.98;
        // Solar_SeasonCos1
        public double F12 = -0.0304153;
        // Surface_InitialTemperature
        public double G12 = 276.15;
        // SoilWater_Evaporation_PsychrometricConstant
        public double H12 = 0.0665;
        // Cloud_Slow_Initial_rand
        public double A14 = -1.0235384521761817;
        // Wind_K_D
        public double B14 = 0.485;
        // Rain_Stop_Moisture
        public double C14 = 15.03066;
        // Snow_InitialStorage
        public double D14 = 0.0;
        // Vapor_RainOffset
        public double E14 = 4.87;
        // Solar_SeasonSin1
        public double F14 = -0.0092107;
        // Air_InitialEnergy
        public double G14 = 9.017271362938498;
        // SoilWater_Evaporation_SolarFactor
        public double H14 = 0.1;
        // Cloud_Fast_Initial_rand
        public double A16 = -0.029046133608109072;
        // Wind_K_H
        public double B16 = 0.368;
        // Rain_Stop_CloudReduction
        public double C16 = 8.94642;
        // Snow_InitialDepth
        public double D16 = 0.0;
        // Vapor_RainScale
        public double E16 = 0.0016666666666666668;
        // Solar_SeasonCos2
        public double F16 = -0.027214;
        // Air_HeatCapacity
        public double G16 = 0.033378757589999995;
        // SoilWater_Evaporation_AirFactor
        public double H16 = 0.0008333333333333334;
        // Wind_Zero
        public double B18 = 1.714;
        // Rain_Start_Intensity
        public double C18 = 0.0027008333333333333;
        // Snow_MaxDensity
        public double D18 = 300.0;
        // Vapor_RainPersistence
        public double E18 = 0.9973787695237809;
        // Solar_SeasonSin2
        public double F18 = 0.0009115;
        // Air_InitialTemperature
        public double G18 = 270.15;
        // SoilWater_Evaporation_WindFactor
        public double H18 = 0.4;
        // Wind_Day_Initial
        public double B20 = -0.28944899193056267;
        // Rain_Second_Intensity
        public double C20 = 0.000385;
        // Snow_CompactionRate
        public double D20 = 0.0002;
        // Vapor_RainRisePersistence
        public double E20 = 0.9721466731817027;
        // Solar_AirMassPower
        public double F20 = 0.27254;
        // Soil_Surface_TransferFactor
        public double G20 = 2.15182515e-05;
        // SoilWater_Evaporation_Scale
        public double H20 = 0.88;
        // Wind_Hour_Initial
        public double B22 = -0.3870808480865897;
        // Rain_Intensity
        public double C22 = 0.004412;
        // Snow_SolarMeltFactor
        public double D22 = 0.04;
        // Vapor_OutputPersistence
        public double E22 = 0.9601647386287255;
        // Surface_Air_TransferFactor
        public double G22 = 0.00013962531560656502;
        // SoilWater_Evaporation_StressThreshold
        public double H22 = 0.4;
        // Rain_Min_Amount
        public double C24 = 0.0010483333333333332;
        // Snow_SolarMeltColdRange
        public double D24 = 5.0;
        // Vapor_LevelScale
        public double E24 = 0.962;
        // Surface_SolarFactor
        public double G24 = 0.0098799672556411;
        // Rain_Start_Probability
        public double C26 = 0.13331;
        // Vapor_PreRain_Max
        public double E26 = 0.65;
        // Surface_SnowReflectance
        public double G26 = 0.693978;
        // Rain_Start_CloudExponent
        public double C28 = 107.33961;
        // Vapor_PreRain_StorageWindow
        public double E28 = 1.0;
        // Air_SolarFactor
        public double G28 = 0.054578877364179;
        // Rain_Continue_Probability
        public double C30 = 0.51875;
        // Vapor_PreRain_CloudFloor
        public double E30 = 0.8;
        // Surface_RadiationFactor
        public double G30 = 0.00062108484065359;
        // Rain_Continue_CloudExponent
        public double C32 = 45.65606;
        // Vapor_PreRain_Persistence
        public double E32 = 0.9780228724846005;
        // Surface_RadiationReferenceTemperature
        public double G32 = 191.850587;
        // Rain_Long_Continue_Probability
        public double C34 = 0.94675;
        // Air_RadiationFactor
        public double G34 = 0.24583174340575104;
        // Rain_Transition_Minutes
        public double C36 = 30.0;
        // Air_RadiationReferenceTemperature
        public double G36 = 219.93547;
        // Rain_Intensity_AR
        public double C38 = 0.9848445060025462;
        // Air_VaporCoolingFactor
        public double G38 = 0.48;
        // Rain_Intensity_SD
        public double C40 = 1.04438;
        // Air_CoolingNormalization
        public double G40 = 1.006223;
        // Rain_Intensity_Smooth
        public double C42 = 0.9048374180359595;
        // Air_RainCoolingFactor
        public double G42 = 0.18978327833333333;
        // Rain_Intensity_Initial_rand
        public double C44 = 0.1467302684088087;
        // Air_RainCoolingScale
        public double G44 = 0.016666666666666666;
        // Rain_Intensity_Smoothed_Initial_rand
        public double C46 = 0.3048993178670212;
        // Air_SaturationAdjustmentFactor
        public double G46 = 1.0;
        // Rain_Intensity_Variance
        public double C48 = 0.9464854025201636;
        // Rain_Edge_Window
        public double C50 = 5.0;
        public WeatherParameters Copy() => (WeatherParameters)MemberwiseClone();
        public void Validate()
        {
            foreach(var field in typeof(WeatherParameters).GetFields())
            {
                double value=(double)field.GetValue(this);
                if(double.IsNaN(value)||double.IsInfinity(value))throw new ArgumentException("Non-finite parameter "+field.Name);
            }
            if(C50<1 || C50>1440 || C50!=Math.Floor(C50) || D4<=D2 || D10<=0 || D12<=0 || D18<=0 ||
                G4<=0 || G10<=0 || G16<=0 || G40<=0 || H2<=0 || H22<=0 || C4<=0 || C36<=0 ||
                E16<=0 || E28<=0 || E30>=1 || G44<=0)
                throw new ArgumentException("Invalid weather parameter domain.");
            foreach(double rho in new[]{A2,A6,C2,C38,E2})
                if(Math.Abs(rho)>1)throw new ArgumentException("Invalid persistence coefficient.");
        }
    }
}
