using UnityEditor;
using UnityEngine;
public sealed class ThermalExchangeSettingsMenu : EditorWindow {
    private ThermalExchangeSettings settings;
    private double fraction, exterior;
    private Vector2 scroll;
    private Editor combEditor;
    [MenuItem("Tools/HoneyComb/열 파라미터 설정", false, 10)]
    public static void Open(){GetWindow<ThermalExchangeSettingsMenu>("열 파라미터 설정");}
    private void OnEnable(){Reload();minSize=new Vector2(560,650);}
    private void Reload(){settings=Resources.Load<ThermalExchangeSettings>("Thermal/ExchangeSettings");if(settings!=null){fraction=settings.ValidFraction;exterior=settings.ValidExterior;}}
    private bool Valid()=>!double.IsNaN(fraction)&&!double.IsInfinity(fraction)&&fraction>=.001&&fraction<=.5&&!double.IsNaN(exterior)&&!double.IsInfinity(exterior)&&exterior>=0;
    private void Apply(){Undo.RecordObject(settings,"열교환 설정 변경");settings.MaximumFractionPerMinute=fraction;settings.ExteriorFilmCoefficientWPerM2K=exterior;EditorUtility.SetDirty(settings);}
    private void OnDisable(){if(combEditor!=null)DestroyImmediate(combEditor);}
    private void OnGUI(){
        scroll=EditorGUILayout.BeginScrollView(scroll);
        DrawSettings();
        EditorGUILayout.EndScrollView();
    }
    private void DrawSettings(){
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
        if(GUILayout.Button("설정 파일 선택"))Selection.activeObject=settings;
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.Space(16);
        var comb=Resources.Load<CombBalanceSettings>("CombBalanceSettings");
        if(comb==null){EditorGUILayout.HelpBox("소방 설정 파일이 없습니다.",MessageType.Error);return;}
        Editor.CreateCachedEditor(comb,typeof(CombBalanceSettingsEditor),ref combEditor);
        combEditor.OnInspectorGUI();
        DrawHeatingPanels();
        EditorGUILayout.Space();
        if(GUILayout.Button("재질·소방 설정을 프로젝트 기본값으로 저장")){
            AssetDatabase.SaveAssetIfDirty(comb);
            foreach(ThermalMaterialKind kind in System.Enum.GetValues(typeof(ThermalMaterialKind))){
                var material=Resources.Load<ThermalMaterialAsset>("Thermal/"+kind);
                if(material!=null)AssetDatabase.SaveAssetIfDirty(material);
            }
        }
    }
    private void DrawHeatingPanels(){
        EditorGUILayout.Space();EditorGUILayout.LabelField("테스트용 벌통 가온판",EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("단열재 본체에 설정한 총 출력×60초의 열을 공급합니다. 벌통 내부 슬롯에 설치됐을 때만 작동하며, 소지품·손에서는 작동하지 않습니다. 출력과 켜짐 상태는 게임 저장에 포함됩니다.",MessageType.Info);
        var world=GameBootstrap.Instance?.GameWorld;
        if(world==null){EditorGUILayout.HelpBox("게임 실행 후 가온판을 추가하거나 출력을 조정할 수 있습니다. 새 게임에는 가온판 1개가 지급됩니다.",MessageType.None);return;}
        using(new EditorGUI.DisabledScope(GameTimeRuntime.InputBlocked)){
            if(GUILayout.Button("소지품에 테스트 가온판 추가")){
                int id=0;foreach(var item in ThermalWorld.Items(world))id=System.Math.Max(id,item.Id);
                if(id<int.MaxValue&&!world.Inventory.TryAdd(new ItemData(id+1,"벌통 가온판",ItemKind.HeatingPanel)))Debug.LogWarning("소지품 공간이 부족합니다.");
            }
            foreach(var item in ThermalWorld.Items(world))if(item.Kind==ItemKind.HeatingPanel){
                var heater=item.GetContent<HeatingPanelData>();if(heater==null)continue;
                EditorGUILayout.LabelField(item.Name+" #"+item.Id,EditorStyles.boldLabel);
                bool enabled=EditorGUILayout.Toggle("전원",heater.Enabled);
                float watts=EditorGUILayout.DelayedFloatField("총 출력 (W, 0~1000)",heater.OutputWatts);
                if(enabled!=heater.Enabled||watts!=heater.OutputWatts)if(!heater.TryConfigure(enabled,watts))Debug.LogWarning("출력은 유한한 0~1000 W여야 합니다.");
                EditorGUILayout.LabelField("지난 1분 공급 열량",heater.LastSuppliedJoules.ToString("F2")+" J");
            }
        }
    }

}
