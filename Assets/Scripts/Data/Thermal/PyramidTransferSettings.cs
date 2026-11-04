using System;
using UnityEngine;
[CreateAssetMenu(menuName="HoneyComb/Pyramid Heat Transfer")]
public sealed class PyramidTransferSettings:ScriptableObject {
    [Min(0)] public double AirAir=5,AirWood=3,AirComb=3,AirInsulation=.3;
    [Min(0)] public double WoodWood=10,WoodComb=5,WoodInsulation=.3;
    [Min(0)] public double CombComb=5,CombInsulation=.3,InsulationInsulation=1;
    [Min(0)] public double AirOutside=5,WoodOutside=2,CombOutside=2,InsulationOutside=.3;
    private ThermalTransferCoefficients cached;
    public ThermalTransferCoefficients Read(){if(cached==null||!cached.Matches(this))cached=new ThermalTransferCoefficients(this);return cached;}
}
// Effective whole-interface coefficients in W/(m2 K); no thickness or conductivity resistance.
public sealed class ThermalTransferCoefficients {
    private readonly double[] h;
    public static readonly ThermalTransferCoefficients Default=new ThermalTransferCoefficients(new[]{5d,3,3,.3,10,5,.3,5,.3,1,5,2,2,.3});
    public static readonly ThermalTransferCoefficients Sealed=new ThermalTransferCoefficients(new double[14]);
    private static double[] Values(PyramidTransferSettings s)=>new[]{s.AirAir,s.AirWood,s.AirComb,s.AirInsulation,s.WoodWood,s.WoodComb,s.WoodInsulation,s.CombComb,s.CombInsulation,s.InsulationInsulation,s.AirOutside,s.WoodOutside,s.CombOutside,s.InsulationOutside};
    private ThermalTransferCoefficients(double[] values){foreach(double value in values)if(!ThermalExchange.Finite(value)||value<0)throw new ArgumentOutOfRangeException("Heat transfer coefficients must be finite and non-negative");h=values;}
    public ThermalTransferCoefficients(PyramidTransferSettings s):this(Values(s)){}
    public bool Matches(PyramidTransferSettings s)=>h[0]==s.AirAir&&h[1]==s.AirWood&&h[2]==s.AirComb&&h[3]==s.AirInsulation&&h[4]==s.WoodWood&&h[5]==s.WoodComb&&h[6]==s.WoodInsulation&&h[7]==s.CombComb&&h[8]==s.CombInsulation&&h[9]==s.InsulationInsulation&&h[10]==s.AirOutside&&h[11]==s.WoodOutside&&h[12]==s.CombOutside&&h[13]==s.InsulationOutside;
    public double Between(HiveMaterial a,HiveMaterial b){int x=(int)a,y=(int)b;if(x>y){int t=x;x=y;y=t;}return h[x*4-x*(x-1)/2+y-x];}
    public double Outside(HiveMaterial material)=>h[10+(int)material];
}
