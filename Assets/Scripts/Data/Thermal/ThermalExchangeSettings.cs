using UnityEngine;

[CreateAssetMenu(menuName = "HoneyComb/Thermal Exchange Settings")]
public sealed class ThermalExchangeSettings : ScriptableObject
{
    [Range(0.001f, 0.5f)]
    [Tooltip("한 분 동안 주변 온도 차를 반영하는 최대 비율입니다. 0.1은 최대 10%입니다. 실행 중 변경하면 다음 열 계산부터 적용됩니다. 실제 물성 기반 속도가 아닌 게임용 제한입니다.")]
    public double MaximumFractionPerMinute = 0.1;
    [Min(0)]
    [Tooltip("외기와 표면 사이의 게임용 전달 계수(W/m²/K). 0이면 외기 교환을 끕니다. 실측 보정값이 아닙니다. 변경하면 다음 열 계산부터 적용됩니다.")]
    public double ExteriorFilmCoefficientWPerM2K = 5;
    public double ValidFraction => double.IsNaN(MaximumFractionPerMinute) || double.IsInfinity(MaximumFractionPerMinute) ? 0.1 : System.Math.Clamp(MaximumFractionPerMinute,0.001,0.5);
    public double ValidExterior => double.IsNaN(ExteriorFilmCoefficientWPerM2K) || double.IsInfinity(ExteriorFilmCoefficientWPerM2K) ? 5 : System.Math.Max(0,ExteriorFilmCoefficientWPerM2K);
    private void OnValidate() { MaximumFractionPerMinute=ValidFraction; ExteriorFilmCoefficientWPerM2K=ValidExterior; }
}
