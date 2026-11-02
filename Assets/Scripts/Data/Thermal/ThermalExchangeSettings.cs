using UnityEngine;

[CreateAssetMenu(menuName = "HoneyComb/Thermal Exchange Settings")]
public sealed class ThermalExchangeSettings : ScriptableObject
{
    public const float MaxFraction = 1f;
    [Range(0.001f, MaxFraction)]
    [Tooltip("한 분 동안 주변 온도 차를 반영하는 최대 비율입니다. 0.1은 최대 10%입니다. 실행 중 변경하면 다음 열 계산부터 적용됩니다. 실제 물성 기반 속도가 아닌 게임용 제한입니다.")]
    public double MaximumFractionPerMinute = 0.1;
    [Min(0)]
    [Tooltip("외기와 표면 사이의 게임용 전달 계수(W/m²/K). 0이면 외기 교환을 끕니다. 실측 보정값이 아닙니다. 변경하면 다음 열 계산부터 적용됩니다.")]
    public double ExteriorFilmCoefficientWPerM2K = 5;
    public double ValidFraction => double.IsNaN(MaximumFractionPerMinute) || double.IsInfinity(MaximumFractionPerMinute) ? 0.1 : System.Math.Clamp(MaximumFractionPerMinute,0.001,(double)MaxFraction);
    public double ValidExterior => double.IsNaN(ExteriorFilmCoefficientWPerM2K) || double.IsInfinity(ExteriorFilmCoefficientWPerM2K) ? 5 : System.Math.Max(0,ExteriorFilmCoefficientWPerM2K);
    public bool UseMaterialPairs = true;
    [Min(0)] public double AirAir=5, AirWood=5, AirComb=5, AirInsulation=5;
    [Min(0)] public double WoodWood=100, WoodComb=20, WoodInsulation=10;
    [Min(0)] public double CombComb=100, CombInsulation=10, InsulationInsulation=100;
    [Min(0)] public double AirOutside=20, WoodOutside=5, CombOutside=5, InsulationOutside=5;
    private ThermalPairCoefficients cachedPairs;
    public ThermalPairCoefficients ReadPairs() {
        if(!UseMaterialPairs)return null;
        if(cachedPairs==null || !cachedPairs.Matches(this)) cachedPairs=new ThermalPairCoefficients(this);
        return cachedPairs;
    }
    private void OnValidate() { MaximumFractionPerMinute=ValidFraction; ExteriorFilmCoefficientWPerM2K=ValidExterior; }
}

// Immutable derived table, never stored in play saves. Entries are symmetric, W/(m2 K).
public sealed class ThermalPairCoefficients {
    private readonly double[] h;
    private static double[] Read(ThermalExchangeSettings s)=>new[]{s.AirAir,s.AirWood,s.AirComb,s.AirInsulation,s.WoodWood,s.WoodComb,s.WoodInsulation,s.CombComb,s.CombInsulation,s.InsulationInsulation,s.AirOutside,s.WoodOutside,s.CombOutside,s.InsulationOutside};
    public ThermalPairCoefficients(ThermalExchangeSettings s){h=Read(s);foreach(double x in h)if(!ThermalExchange.Finite(x)||x<0)throw new System.ArgumentOutOfRangeException("Pair coefficient must be finite and non-negative");}
    public bool Matches(ThermalExchangeSettings s)=>h[0]==s.AirAir&&h[1]==s.AirWood&&h[2]==s.AirComb&&h[3]==s.AirInsulation&&h[4]==s.WoodWood&&h[5]==s.WoodComb&&h[6]==s.WoodInsulation&&h[7]==s.CombComb&&h[8]==s.CombInsulation&&h[9]==s.InsulationInsulation&&h[10]==s.AirOutside&&h[11]==s.WoodOutside&&h[12]==s.CombOutside&&h[13]==s.InsulationOutside;
    public double Between(HiveMaterial a,HiveMaterial b){int x=(int)a,y=(int)b;if(x>y){int t=x;x=y;y=t;}return h[x*4-x*(x-1)/2+y-x];}
    public double Outside(HiveMaterial a)=>h[10+(int)a];
    public double Conductance(HiveMaterial a,HiveMaterial b,double area,double halfA,double kA,double halfB,double kB){
        double v=Between(a,b);if(v==0)return 0;
        // Each solid contributes its centre-to-face thickness resistance, including air-solid links.
        // Air contributes no molecular thickness resistance: its mixing/film effect is represented by h.
        return area/(1/v+((a==HiveMaterial.Air?0:halfA/kA)+(b==HiveMaterial.Air?0:halfB/kB)));
    }
    public double Exterior(HiveMaterial a,double area,double half,double k){double v=Outside(a);return v==0?0:area/(1/v+(a==HiveMaterial.Air?0:half/k));}
}
