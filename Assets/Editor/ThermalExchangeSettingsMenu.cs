using UnityEditor;
using UnityEngine;
public sealed class ThermalExchangeSettingsMenu : EditorWindow {
    private ThermalExchangeSettings settings;
    private double fraction, exterior;
    [MenuItem("Tools/HoneyComb/격자 열교환 설정")]
    [MenuItem("HoneyComb/Developer/Thermal Exchange")]
    public static void Open(){GetWindow<ThermalExchangeSettingsMenu>("열교환 설정");}
    private void OnEnable(){Reload();minSize=new Vector2(430,330);}
    private void Reload(){settings=Resources.Load<ThermalExchangeSettings>("Thermal/ExchangeSettings");if(settings!=null){fraction=settings.ValidFraction;exterior=settings.ValidExterior;}}
    private bool Valid()=>!double.IsNaN(fraction)&&!double.IsInfinity(fraction)&&fraction>=.001&&fraction<=.5&&!double.IsNaN(exterior)&&!double.IsInfinity(exterior)&&exterior>=0;
    private void Apply(){Undo.RecordObject(settings,"열교환 설정 변경");settings.MaximumFractionPerMinute=fraction;settings.ExteriorFilmCoefficientWPerM2K=exterior;EditorUtility.SetDirty(settings);}
    private void OnGUI(){
        if(settings==null){EditorGUILayout.HelpBox("열교환 설정 파일이 없습니다.",MessageType.Error);return;}
        EditorGUILayout.LabelField("격자 열교환",EditorStyles.boldLabel);
        EditorGUILayout.LabelField("현재 적용값",$"분당 제한 {settings.ValidFraction:P1} / 외기 계수 {settings.ValidExterior:G5}");
        EditorGUILayout.Space();
        fraction=EditorGUILayout.Slider(new GUIContent("분당 교환 제한 (%)","주변 온도 차를 반영하는 최대 비율. 각 연결의 실제 비율은 더 작을 수 있습니다."),(float)(fraction*100),.1f,50f)/100d;
        exterior=EditorGUILayout.DoubleField(new GUIContent("외기 열전달계수 (W/m²·K)","0이면 외기 열교환이 꺼집니다. 내부 교환은 유지됩니다."),exterior);
        EditorGUILayout.HelpBox("적용하면 실행 중인 게임도 다음 열 계산부터 변경됩니다. 기존 열에너지는 유지됩니다. 재생이 일시정지 상태라면 열 계산이 재개될 때 반영됩니다.",MessageType.Info);
        EditorGUILayout.HelpBox("분당 제한을 높이면 내부·외기 교환의 상한이 함께 올라갑니다. 외기 계수는 외부 노출면에 작용합니다. 환기·대류·벌 발열을 추가하는 설정은 아닙니다. 현재 60초 계산의 안정성 범위로 제한합니다.",MessageType.None);
        using(new EditorGUI.DisabledScope(!Valid())){
            if(GUILayout.Button("현재 값 적용"))Apply();
            if(GUILayout.Button("적용하고 프로젝트 기본값 저장")){Apply();AssetDatabase.SaveAssetIfDirty(settings);}
        }
        if(!Valid())EditorGUILayout.HelpBox("제한은 0.1~50%, 외기 계수는 유한한 0 이상이어야 합니다.",MessageType.Error);
        EditorGUILayout.BeginHorizontal();
        if(GUILayout.Button("현재 값 다시 읽기"))Reload();
        if(GUILayout.Button("기본값 입력 (10%, 5)")){fraction=.1;exterior=5;}
        if(GUILayout.Button("설정 파일 선택"))Selection.activeObject=settings;
        EditorGUILayout.EndHorizontal();
    }
}
