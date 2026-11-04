using UnityEditor;
using UnityEngine;
public sealed class ThermalExchangeSettingsMenu : EditorWindow {
    private Vector2 scroll;
    [MenuItem("Tools/HoneyComb/열 파라미터 설정", false, 10)]
    public static void Open(){GetWindow<ThermalExchangeSettingsMenu>("열 파라미터 설정");}
    private void OnEnable(){minSize=new Vector2(560,650);}
    private void OnGUI(){
        scroll=EditorGUILayout.BeginScrollView(scroll);
        DrawSettings();
        EditorGUILayout.EndScrollView();
    }
    private void DrawSettings(){
        EditorGUILayout.LabelField("사각뿔 6개 열교환",EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("각 면은 열용량의 1/6을 사용해 인접 면과 60초간 지수 교환한 뒤 합쳐집니다. 내부·외기는 재질 열전도율과 중심에서 면까지의 거리로 계산합니다. 외기는 날씨 온도를 유지하는 공기이며, 외부 거리도 내부의 중심→면 거리와 같습니다. 별도 교환 제한이나 재질 조합 계수는 없습니다.",MessageType.Info);
        var comb=Resources.Load<CombBalanceSettings>("CombBalanceSettings");
        if(comb==null){EditorGUILayout.HelpBox("소방 설정 파일이 없습니다.",MessageType.Error);return;}
        DrawMaterials();
        EditorGUILayout.Space();EditorGUILayout.LabelField("소방 내용물 열용량",EditorStyles.boldLabel);
        var so=new SerializedObject(comb);so.Update();var values=so.FindProperty("values");
        string[] names={"WaterSpecificHeatJPerKgK","SugarSpecificHeatJPerKgK","PollenSpecificHeatJPerKgK","BroodHeatCapacityJPerK"};
        string[] labels={"수분 비열 (J/kg·K)","당분 비열 (J/kg·K)","화분 비열 (J/kg·K)","봉충 소방 열용량 (J/K)"};
        for(int i=0;i<names.Length;i++)EditorGUILayout.PropertyField(values.FindPropertyRelative(names[i]),new GUIContent(labels[i]));
        if(so.ApplyModifiedProperties()&&!comb.TryApply(out string error))Debug.LogError(error);
        if(GUILayout.Button("소방 용량 등 전체 설정 열기"))Selection.activeObject=comb;
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
    private void DrawMaterials(){
        string[] labels={"공기 (외기에도 적용)","목재","밀랍","단열재"};
        for(int i=0;i<labels.Length;i++){
            var material=Resources.Load<ThermalMaterialAsset>("Thermal/"+(ThermalMaterialKind)i);
            if(material==null)continue;
            EditorGUILayout.Space();EditorGUILayout.LabelField(labels[i],EditorStyles.boldLabel);
            var so=new SerializedObject(material);so.Update();
            var cv=so.FindProperty("EffectiveVolumetricHeatCapacityJPerM3K");var conductivity=so.FindProperty("ConductivityWPerMK");
            double nextC=DragParameter("유효 체적 열용량 (J/m³·K)",cv.floatValue,0,10000000,1000);
            double nextK=DragParameter("열전도율 (W/m·K)",conductivity.floatValue,.000001,10,.01);
            if(ThermalExchange.Finite(nextC)&&nextC>=0&&nextC<=float.MaxValue&&ThermalExchange.Finite(nextK)&&nextK>0&&nextK<=float.MaxValue){cv.floatValue=(float)nextC;conductivity.floatValue=(float)nextK;if(so.ApplyModifiedProperties())ThermalMaterials.NotifyAssetsChanged();}
            else EditorGUILayout.HelpBox("열용량은 유한한 0 이상, 열전도율은 유한한 양수여야 합니다.",MessageType.Error);
        }
        EditorGUILayout.HelpBox("편집은 다음 열 계산부터 반영됩니다. 열용량 0은 밀도×비열을 사용합니다. 열용량 변경 시 에너지를 유지하므로 온도가 달라질 수 있습니다. 공기 열전도율은 내부 공기와 외기 양쪽에 적용됩니다.",MessageType.Info);
    }
    // Keep exact typed values, including those outside the drag range, until the slider is actually moved.
    public static double DragParameter(string label,double value,double minimum,double maximum,double scale){
        double result=EditorGUILayout.DelayedDoubleField(new GUIContent(label,"숫자를 직접 입력하거나 아래 막대를 드래그하세요. 작은 값은 세밀하게, 큰 값은 넓게 조절됩니다."),value);
        double shown=ThermalExchange.Finite(result)?System.Math.Clamp(result,minimum,maximum):minimum;
        double lo=System.Math.Log(1+minimum/scale),hi=System.Math.Log(1+maximum/scale);
        float position=(float)((System.Math.Log(1+shown/scale)-lo)/(hi-lo));
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label(new GUIContent("드래그",$"드래그 범위: {minimum:G5} ~ {maximum:G5}. 범위 밖의 값은 숫자로 입력할 수 있습니다."),GUILayout.Width(48));
        EditorGUI.BeginChangeCheck();
        float next=GUILayout.HorizontalSlider(position,0f,1f);
        if(EditorGUI.EndChangeCheck()){
            double adjusted=next<=0?minimum:next>=1?maximum:scale*(System.Math.Exp(lo+next*(hi-lo))-1);
            result=double.Parse(adjusted.ToString("G6",System.Globalization.CultureInfo.InvariantCulture),System.Globalization.CultureInfo.InvariantCulture);
        }
        EditorGUILayout.EndHorizontal();
        return result;
    }
    private void DrawHeatingPanels(){
        EditorGUILayout.Space();EditorGUILayout.LabelField("테스트용 벌통 가온판",EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("가온판 본체에 설정한 총 출력×60초의 열을 공급합니다. 벌통 내부 슬롯에 설치됐을 때만 작동하며, 소지품·손에서는 작동하지 않습니다. 출력과 켜짐 상태는 게임 저장에 포함됩니다.",MessageType.Info);
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
