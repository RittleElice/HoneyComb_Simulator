using System;
using UnityEngine;
[CreateAssetMenu(menuName="HoneyComb/Material Exchange Parameters")]
public sealed class PyramidTransferSettings:ScriptableObject {
    [Min(0)] public double AirAir=5,AirWood=3,AirComb=3,AirInsulation=.3;
    [Min(0)] public double WoodWood=10,WoodComb=5,WoodInsulation=.3;
    [Min(0)] public double CombComb=5,CombInsulation=.3,InsulationInsulation=1;
    [Min(0)] public double AirOutside=5,WoodOutside=2,CombOutside=2,InsulationOutside=.3;
    [Min(0)] public double MoistureAirPermeability=1.85e-10;
    [Min(0)] public double MoistureWoodPermeability=2e-12;
    [Min(0)] public double MoistureAirMobility=1;
    [Min(0)] public double MoistureInsulationMobility=.008;
    [Min(0)] public double MoistureEvaporationMobility=.04;
    [Min(0)] public double MoistureExteriorAirFilmM=.01;
    [Min(0)] public double MoistureWindCoefficient=.025;
    private ThermalTransferCoefficients cached;
    public ThermalTransferCoefficients Read(){if(cached==null||!cached.Matches(this))cached=new ThermalTransferCoefficients(this);return cached;}
}
// Effective whole-interface coefficients in W/(m2 K); no thickness or conductivity resistance.
public sealed class ThermalTransferCoefficients {
    private readonly double[] h;
    public static readonly ThermalTransferCoefficients Default=new ThermalTransferCoefficients(new[]{5d,3,3,.3,10,5,.3,5,.3,1,5,2,2,.3});
    public static readonly ThermalTransferCoefficients Sealed=new ThermalTransferCoefficients(new double[14]);
    private static double[] Values(PyramidTransferSettings s)=>new[]{s.AirAir,s.AirWood,s.AirComb,s.AirInsulation,s.WoodWood,s.WoodComb,s.WoodInsulation,s.CombComb,s.CombInsulation,s.InsulationInsulation,s.AirOutside,s.WoodOutside,s.CombOutside,s.InsulationOutside,s.MoistureAirPermeability,s.MoistureWoodPermeability,s.MoistureAirMobility,s.MoistureInsulationMobility,s.MoistureEvaporationMobility,s.MoistureExteriorAirFilmM,s.MoistureWindCoefficient};
    private ThermalTransferCoefficients(double[] values){
        if(values.Length==14){Array.Resize(ref values,21);values[14]=1.85e-10;values[15]=2e-12;values[16]=1;values[17]=.008;values[18]=.04;values[19]=.01;values[20]=.025;}
        if(values[19]<=0)throw new ArgumentOutOfRangeException("외기 공기막 두께는 0보다 커야 합니다.");foreach(double value in values)if(!ThermalExchange.Finite(value)||value<0)throw new ArgumentOutOfRangeException("Heat transfer coefficients must be finite and non-negative");h=values;}
    public ThermalTransferCoefficients(PyramidTransferSettings s):this(Values(s)){}
    public bool Matches(PyramidTransferSettings s)=>h[0]==s.AirAir&&h[1]==s.AirWood&&h[2]==s.AirComb&&h[3]==s.AirInsulation&&h[4]==s.WoodWood&&h[5]==s.WoodComb&&h[6]==s.WoodInsulation&&h[7]==s.CombComb&&h[8]==s.CombInsulation&&h[9]==s.InsulationInsulation&&h[10]==s.AirOutside&&h[11]==s.WoodOutside&&h[12]==s.CombOutside&&h[13]==s.InsulationOutside&&h[14]==s.MoistureAirPermeability&&h[15]==s.MoistureWoodPermeability&&h[16]==s.MoistureAirMobility&&h[17]==s.MoistureInsulationMobility&&h[18]==s.MoistureEvaporationMobility&&h[19]==s.MoistureExteriorAirFilmM&&h[20]==s.MoistureWindCoefficient;
    public double MoistureAirPermeability=>h[14];
    public double MoistureWoodPermeability=>h[15];
    public double MoistureAirMobility=>h[16];
    public double MoistureInsulationMobility=>h[17];
    public double MoistureEvaporationMobility=>h[18];
    public double MoistureExteriorAirFilmM=>h[19];
    public double MoistureWindCoefficient=>h[20];
    public double Between(HiveMaterial a,HiveMaterial b){int x=(int)a,y=(int)b;if(x>y){int t=x;x=y;y=t;}return h[x*4-x*(x-1)/2+y-x];}
    public double Outside(HiveMaterial material)=>h[10+(int)material];
}
