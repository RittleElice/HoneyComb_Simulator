using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(CombBalanceSettings))]
public sealed class CombBalanceSettingsEditor : Editor
{
    private const string Path = "Assets/Resources/CombBalanceSettings.asset";
    [InitializeOnLoadMethod]
    private static void Initialize()
    {
        EditorApplication.delayCall += ApplyCanonical;
        Undo.undoRedoPerformed += ApplyCanonical;
    }
    private static void ApplyCanonical()
    {
        ThermalMaterials.NotifyAssetsChanged();
        var asset = AssetDatabase.LoadAssetAtPath<CombBalanceSettings>(Path);
        if (asset != null && !asset.TryApply(out string error)) Debug.LogError(error);
    }
    private void Field(string name, string label)
    { EditorGUILayout.PropertyField(serializedObject.FindProperty("values").FindPropertyRelative(name), new GUIContent(label)); }
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        EditorGUILayout.HelpBox("기본 설정 에셋의 변경은 현재 실행에도 적용됩니다. 용량을 낮춰도 기존 내용물은 삭제하지 않으며 추가 투입만 제한합니다. 현재 모든 소방 종류에 동일한 용량을 적용합니다.", MessageType.Info);
        EditorGUILayout.LabelField("소방 하나의 저장 한도", EditorStyles.boldLabel);
        Field("CellLiquidCapacityMl", "화밀·물 용적 (mL)");
        Field("CellPollenCapacityMg", "화분 질량 (mg)");
        EditorGUILayout.LabelField("내용물의 비열 — J/(kg·K)", EditorStyles.boldLabel);
        Field("WaterSpecificHeatJPerKgK", "수분 비열"); Field("SugarSpecificHeatJPerKgK", "당분 비열");
        Field("PollenSpecificHeatJPerKgK", "화분 비열");
        Field("BroodHeatCapacityJPerK", "봉충 소방 1개의 열용량 (J/K)");
        EditorGUILayout.HelpBox("봉충은 벌 질량 모델 연결 전의 고정 대푯값입니다. 구형 FillRatio만 있는 소방은 질량을 추정하지 않아 내용물 열용량에서 제외됩니다.", MessageType.None);
        EditorGUILayout.LabelField("격자 재질 — 유효 체적 열용량", EditorStyles.boldLabel);
        string[] materialNames = { "공기", "목재", "밀랍", "보온재", "설탕 재질", "물 재질", "화분 재질" };
        for (int i = 0; i < materialNames.Length; i++)
        {
            var material = Resources.Load<ThermalMaterialAsset>("Thermal/" + (ThermalMaterialKind)i);
            if (material == null) { EditorGUILayout.HelpBox(materialNames[i] + " 재질 설정이 없습니다.", MessageType.Error); continue; }
            var materialObject = new SerializedObject(material);
            materialObject.Update();
            var property = materialObject.FindProperty("EffectiveVolumetricHeatCapacityJPerM3K");
            EditorGUILayout.LabelField(materialNames[i], EditorStyles.boldLabel);
            float value = (float)ThermalExchangeSettingsMenu.DragParameter("유효 체적 열용량 (J/m³·K)",property.floatValue,0,10000000,1000);
            if (value != property.floatValue)
            {
                if (ThermalMath.Finite(value) && value >= 0)
                { property.floatValue = value; materialObject.ApplyModifiedProperties(); SceneView.RepaintAll(); }
                else Debug.LogWarning("유효 체적 열용량은 0 이상의 유한한 값이어야 합니다.");
            }
            EditorGUILayout.Space(4);
        }
        EditorGUILayout.HelpBox("실제 격자 계산에 쓰는 재질 설정입니다. 0은 기존 밀도×비열 값을 사용합니다. 변경은 실행 중에도 다음 열 상태 갱신에 반영됩니다. 저장된 열에너지는 유지하므로 열용량 변경 시 온도도 달라집니다. 위 내용물의 비열 설정은 별도로 적용됩니다.", MessageType.Info);
        EditorGUILayout.LabelField("부피 계산용 밀도 — g/mL", EditorStyles.boldLabel);
        Field("WaterDensityGPerMl", "수분 밀도");
        Field("SugarEffectiveDensityGPerMl", "당분 유효 밀도");
        EditorGUILayout.HelpBox("화밀 부피 = 당분 질량 ÷ 당분 유효 밀도 + 수분 질량 ÷ 수분 밀도. 질량은 g로 환산해 계산합니다.", MessageType.None);
        bool edited = serializedObject.ApplyModifiedProperties();
        if (AssetDatabase.GetAssetPath(target) != Path)
        { EditorGUILayout.HelpBox("게임은 Assets/Resources/CombBalanceSettings.asset을 사용합니다.", MessageType.Warning); return; }
        if (edited || GUILayout.Button("설정 다시 적용"))
        {
            if (!((CombBalanceSettings)target).TryApply(out string error)) Debug.LogError("설정 미적용: " + error);
            SceneView.RepaintAll();
        }
    }
}
