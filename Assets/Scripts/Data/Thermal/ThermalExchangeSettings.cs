using UnityEngine;

[CreateAssetMenu(menuName = "HoneyComb/Thermal Exchange Settings")]
public sealed class ThermalExchangeSettings : ScriptableObject
{
    [Range(0.001f, 0.5f)]
    [Tooltip("한 분 동안 주변 온도 차를 반영하는 최대 비율입니다. 0.1은 최대 10%입니다. 게임을 다시 시작하거나 저장을 다시 불러오면 적용됩니다. 실제 물성 기반 속도가 아닌 게임용 제한입니다.")]
    public double MaximumFractionPerMinute = 0.1;
    [Min(0)]
    [Tooltip("외기와 표면 사이의 게임용 전달 계수(W/m²/K). 0이면 외기 교환을 끕니다. 실측 보정값이 아닙니다. 변경 후 게임을 다시 시작하거나 불러오세요.")]
    public double ExteriorFilmCoefficientWPerM2K = 5;
}
