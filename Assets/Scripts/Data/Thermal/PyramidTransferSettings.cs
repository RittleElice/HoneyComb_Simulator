using System;
using UnityEngine;
[CreateAssetMenu(menuName="HoneyComb/Climate Balance Parameters")]
public sealed class PyramidTransferSettings:ScriptableObject {
    [Min(0)] public double AirOutside=5,WoodOutside=10,CombOutside=10,InsulationOutside=.1;
    [Min(0)] public double HeatWindCoefficient=4;
    [Min(0.01f),Tooltip("Temporary heat-transfer correction for unresolved air circulation. Physical air conductivity stays unchanged; no air flow or vapor advection is simulated.")]
    public double AirConductivityMultiplier=4;
    void OnValidate(){cached=null;ThermalMaterials.NotifyAssetsChanged();}
    [Min(0)] public double MoistureAirPermeability=1.85e-10,MoistureWoodPermeability=2e-12;
    [Min(0)] public double MoistureAirMobility=1,MoistureInsulationMobility=.008,MoistureEvaporationMobility=.04;
    [Min(0)] public double MoistureExteriorAirFilmM=.01,MoistureWindCoefficient=.025;
    public double BalanceRelativeTolerance=1e-11,BalanceMassTolerance=1e-10;
    public double BalanceMassAbsoluteToleranceKg=1e-12;
    public int BalanceLinearIterations=3000,BalanceNonlinearIterations=30;
    ThermalTransferCoefficients cached;
    public ThermalTransferCoefficients Read(){if(cached==null||!cached.Matches(this))cached=new ThermalTransferCoefficients(this);return cached;}
}
public sealed class ThermalTransferCoefficients {
    readonly double[] h;
    static double[] Values(PyramidTransferSettings s)=>new[]{s.AirOutside,s.WoodOutside,s.CombOutside,s.InsulationOutside,s.HeatWindCoefficient,s.MoistureAirPermeability,s.MoistureWoodPermeability,s.MoistureAirMobility,s.MoistureInsulationMobility,s.MoistureEvaporationMobility,s.MoistureExteriorAirFilmM,s.MoistureWindCoefficient,s.BalanceRelativeTolerance,s.BalanceMassTolerance,(double)s.BalanceLinearIterations,s.BalanceNonlinearIterations,s.BalanceMassAbsoluteToleranceKg,s.AirConductivityMultiplier};
    public static readonly ThermalTransferCoefficients Default=new ThermalTransferCoefficients(new[]{5d,10,10,.1,4,1.85e-10,2e-12,1,.008,.04,.01,.025,1e-11,1e-10,3000,30,1e-12,1});
    public static readonly ThermalTransferCoefficients Sealed=new ThermalTransferCoefficients(new[]{0d,0,0,0,0,1.85e-10,2e-12,1,.008,.04,.01,0,1e-11,1e-10,3000,30,1e-12,1});
    ThermalTransferCoefficients(double[] v){foreach(double x in v)if(!ThermalExchange.Finite(x)||x<0)throw new ArgumentOutOfRangeException("Climate balance parameter");if(v[17]<=0||v[10]<=0||v[12]<=0||v[12]>=1||v[13]<=0||v[13]>=1||v[14]<1||v[15]<1)throw new ArgumentOutOfRangeException("Climate balance tolerance");h=v;}
    public ThermalTransferCoefficients(PyramidTransferSettings s):this(Values(s)){}
    public bool Matches(PyramidTransferSettings s){var v=Values(s);for(int i=0;i<h.Length;i++)if(h[i]!=v[i])return false;return true;}
    public double AirConductivityMultiplier=>h[17];
    public double BalanceMassAbsoluteToleranceKg=>h[16];
    public double Outside(HiveMaterial m)=>h[(int)m];
    public double HeatWindCoefficient=>h[4];
    public double MoistureAirPermeability=>h[5];public double MoistureWoodPermeability=>h[6];
    public double MoistureAirMobility=>h[7];public double MoistureInsulationMobility=>h[8];public double MoistureEvaporationMobility=>h[9];
    public double MoistureExteriorAirFilmM=>h[10];public double MoistureWindCoefficient=>h[11];
    public double BalanceRelativeTolerance=>h[12];public double BalanceMassTolerance=>h[13];public int BalanceLinearIterations=>(int)h[14];public int BalanceNonlinearIterations=>(int)h[15];
}
