using System;
using UnityEngine;
[CreateAssetMenu(menuName="HoneyComb/Nectar V2 Settings")]
public sealed class NectarSettings : ScriptableObject
{
    public NectarParameters parameters = new NectarParameters();

    [ContextMenu("화밀/엑셀 기본값으로 초기화")]
    private void ResetDefaults()
    {
#if UNITY_EDITOR
        UnityEditor.Undo.RecordObject(this,"Reset nectar parameters");
#endif
        parameters = new NectarParameters();
#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(this);
#endif
    }

    [ContextMenu("화밀/파라미터 검증")]
    public void ValidateSettings()
    {
        if(parameters?.Sources?.Length!=8 || !(parameters.SourceFactor>=0))
            throw new InvalidOperationException("면적, 밀원 배율 및 밀원 8종 설정을 확인하세요.");
        foreach(var p in parameters.Sources)
        {
            if(p==null) throw new InvalidOperationException("밀원 파라미터가 비어 있습니다.");
            foreach(var field in typeof(NectarParameters.Source).GetFields())
                if(field.FieldType==typeof(double)) {
                    double value=(double)field.GetValue(p);
                    if(double.IsNaN(value)||double.IsInfinity(value)||value<0)
                        throw new InvalidOperationException(p.Name+" / "+field.Name);
                }
            if(p.BloomActivationGDDRange<=0 || p.BloomTemperatureRange<=0 || p.ColdWiltingTemperatureRange<=0 ||
               p.HeatWiltingTemperatureRange<=0 || p.NectarTemperatureRange<=0 || p.CarbonResponseTime<=0 ||
               p.CarbonHalfSaturation<=0 || p.SoilWaterFullProduction<=p.SoilWaterZeroProduction ||
               p.FullWaterProductionTemperature<=p.MinimumWaterProductionTemperature ||
               p.MaximumWaterProductionTemperature<=p.HeatWaterProductionTemperature ||
               p.WaterProductionSoilFull<=p.WaterProductionSoilMinimum || p.PollenSoilFull<=p.PollenSoilMinimum ||
               p.PollenHeatZeroTemperature<=p.PollenHeatStartTemperature || p.PollenDevelopmentHours<=0 ||
               p.PollenDevelopmentHours>8760 || p.NectarSugarMolarMass<=0)
                throw new InvalidOperationException(p.Name+"의 범위 또는 시간 설정을 확인하세요.");
        }
    }
}
