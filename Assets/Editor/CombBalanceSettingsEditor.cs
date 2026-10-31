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
        var asset = AssetDatabase.LoadAssetAtPath<CombBalanceSettings>(Path);
        if (asset != null && !asset.TryApply(out string error)) Debug.LogError(error);
    }
    [MenuItem("Tools/HoneyComb/소방 용량·열용량 설정")]
    private static void Open()
    {
        var asset = AssetDatabase.LoadAssetAtPath<CombBalanceSettings>(Path);
        Selection.activeObject = asset; EditorGUIUtility.PingObject(asset);
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
        EditorGUILayout.LabelField("공간 칸의 기본 재질", EditorStyles.boldLabel);
        Field("AirDensityKgPerM3", "공기 밀도 (kg/m³)"); Field("AirSpecificHeatJPerKgK", "공기 비열");
        Field("WoodDensityKgPerM3", "목재 밀도 (kg/m³)"); Field("WoodSpecificHeatJPerKgK", "목재 비열");
        Field("CombEffectiveDensityKgPerM3", "빈 벌집 유효 밀도 (kg/m³)"); Field("CombSpecificHeatJPerKgK", "벌집 비열");
        Field("InsulationDensityKgPerM3", "보온재 밀도 (kg/m³)"); Field("InsulationSpecificHeatJPerKgK", "보온재 비열");
        EditorGUILayout.HelpBox("벌집 유효 밀도는 빈 공간을 포함한 격자 부피 기준입니다. 고체 밀랍의 밀도가 아닙니다. 기본 재질·봉충·당분·화분 값은 조정용 초기값입니다.", MessageType.None);
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
