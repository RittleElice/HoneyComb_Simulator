using UnityEditor;
using UnityEngine;
public sealed class ThermalExchangeSettingsMenu : EditorWindow {
    private Vector2 scroll;
    [MenuItem("Tools/HoneyComb/재질 파라미터 설정", false, 10)]
    public static void Open(){GetWindow<ThermalExchangeSettingsMenu>("재질 파라미터 설정");}
    private void OnEnable(){titleContent=new GUIContent("재질 파라미터 설정");minSize=new Vector2(560,650);}
    private void OnGUI(){
        scroll=EditorGUILayout.BeginScrollView(scroll);
        using(new EditorGUI.DisabledScope(GameTimeRuntime.InputBlocked)){DrawSettings();}
        EditorGUILayout.EndScrollView();
    }
    private void DrawSettings(){
        EditorGUILayout.LabelField("격자별 열·수분 수지",EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("내부 열은 면적·거리·열전도율로 계산합니다. 전체 격자의 다음 상태를 함께 풀며, 내부 공기를 외기 상태로 강제 고정하지 않습니다.",MessageType.Info);
        var comb=Resources.Load<CombBalanceSettings>("CombBalanceSettings");
        if(comb==null){EditorGUILayout.HelpBox("소방 설정 파일이 없습니다.",MessageType.Error);return;}
        DrawTransfers();
        DrawMoisture();
        DrawMaterials();
        EditorGUILayout.Space();EditorGUILayout.LabelField("소방 내용물 열용량",EditorStyles.boldLabel);
        var so=new SerializedObject(comb);so.Update();var values=so.FindProperty("values");
        string[] names={"WaterSpecificHeatJPerKgK","SugarSpecificHeatJPerKgK","PollenSpecificHeatJPerKgK","BroodHeatCapacityJPerK"};
        string[] labels={"수분 비열 (J/kg·K)","당분 비열 (J/kg·K)","화분 비열 (J/kg·K)","봉충 소방 열용량 (J/K)"};
        for(int i=0;i<names.Length;i++)EditorGUILayout.PropertyField(values.FindPropertyRelative(names[i]),new GUIContent(labels[i],i==3?"봉충이 있는 소방의 열용량입니다.\n높이면 같은 열을 받아도 온도가 천천히 변하고, 낮추면 빠르게 변합니다. 봉충의 자체 발열 출력은 아닙니다.":"해당 내용물 1kg의 온도를 1°C 바꾸는 데 필요한 열량입니다.\n높이면 같은 질량과 열량에서 온도 변화가 작아지고, 낮추면 커집니다. 수분 비열은 액상수에 해당하며 공기 수증기 비열과는 별개입니다."));
        if(so.ApplyModifiedProperties()&&!comb.TryApply(out string error))Debug.LogError(error);
        if(GUILayout.Button("소방 용량 등 전체 설정 열기"))Selection.activeObject=comb;
        DrawHeatingPanels();
        EditorGUILayout.Space();
        if(GUILayout.Button("재질 설정을 프로젝트 기본값으로 저장")){
            AssetDatabase.SaveAssetIfDirty(comb);
            var transfer=Resources.Load<PyramidTransferSettings>("Thermal/PyramidTransferSettings");if(transfer!=null)AssetDatabase.SaveAssetIfDirty(transfer);
            foreach(ThermalMaterialKind kind in System.Enum.GetValues(typeof(ThermalMaterialKind))){
                var material=Resources.Load<ThermalMaterialAsset>("Thermal/"+kind);
                if(material!=null)AssetDatabase.SaveAssetIfDirty(material);
            }
        }
    }
    private void DrawTransfers(){DrawBalanceFields("외부 열교환",new[]{"AirOutside","WoodOutside","CombOutside","InsulationOutside","HeatWindCoefficient"},new[]{"공기 경계 (W/m²·K)","목재 경계 (W/m²·K)","소비장 경계 (W/m²·K)","단열재 경계 (W/m²·K)","풍속당 열교환 증가 (W/m²·K)/(m/s)"});}
    private void DrawMoisture(){DrawBalanceFields("수분 이동과 풀이 정확도",new[]{"MoistureAirPermeability","MoistureWoodPermeability","MoistureAirMobility","MoistureInsulationMobility","MoistureEvaporationMobility","MoistureExteriorAirFilmM","MoistureWindCoefficient","BalanceRelativeTolerance","BalanceMassTolerance","BalanceMassAbsoluteToleranceKg","BalanceLinearIterations","BalanceNonlinearIterations"},new[]{"공기 투습계수 (kg/m·s·Pa)","목재 투습계수 (kg/m·s·Pa)","혼합 경계 공기 이동 배율","단열재 이동 배율","화밀 증발 배율","외부 공기막 두께 (m)","풍속 수분 교환 계수","선형 상대 오차","수분 수지 상대 오차","수분 수지 절대 오차 (kg)","선형 최대 반복","비선형 최대 반복"});}
    private void DrawBalanceFields(string title,string[] names,string[] labels){EditorGUILayout.Space();EditorGUILayout.LabelField(title,EditorStyles.boldLabel);var asset=Resources.Load<PyramidTransferSettings>("Thermal/PyramidTransferSettings");if(asset==null)return;var so=new SerializedObject(asset);so.Update();for(int i=0;i<names.Length;i++)EditorGUILayout.PropertyField(so.FindProperty(names[i]),new GUIContent(labels[i]));if(so.ApplyModifiedProperties()){try{asset.Read();}catch(System.Exception e){Debug.LogError(e.Message);}}}
    private void DrawMaterials(){
        string[] labels={"공기","목재","밀랍","단열재"};
        for(int i=0;i<labels.Length;i++){
            var material=Resources.Load<ThermalMaterialAsset>("Thermal/"+(ThermalMaterialKind)i);
            if(material==null)continue;
            EditorGUILayout.Space();EditorGUILayout.LabelField(labels[i],EditorStyles.boldLabel);
            var so=new SerializedObject(material);so.Update();
            EditorGUILayout.PropertyField(so.FindProperty("DensityKgPerM3"),new GUIContent("밀도 (kg/m³)"));
            EditorGUILayout.PropertyField(so.FindProperty("SpecificHeatJPerKgK"),new GUIContent("비열 (J/kg·K)"));
            EditorGUILayout.PropertyField(so.FindProperty("ConductivityWPerMK"),new GUIContent("열전도율 (W/m·K)"));
            var cv=so.FindProperty("EffectiveVolumetricHeatCapacityJPerM3K");
            double nextC=DragParameter("유효 체적 열용량 (J/m³·K)",cv.floatValue,0,10000000,1000,labels[i]+" 1m³의 온도를 1°C 바꾸는 데 필요한 열량입니다.\n높이면 같은 열량에서 온도 변화가 작아지고, 낮추면 커집니다. 0은 열용량 제거가 아니라 밀도×비열 사용입니다. 변경 시 에너지를 유지하므로 현재 온도도 달라질 수 있습니다.");
            if(ThermalExchange.Finite(nextC)&&nextC>=0&&nextC<=float.MaxValue){cv.floatValue=(float)nextC;if(so.ApplyModifiedProperties())ThermalMaterials.NotifyAssetsChanged();}
            else EditorGUILayout.HelpBox("열용량은 유한한 0 이상이어야 합니다.",MessageType.Error);
        }
        EditorGUILayout.HelpBox("편집은 다음 열 계산부터 반영됩니다. 열용량 0은 밀도×비열을 사용합니다. 열용량 변경 시 에너지를 유지하므로 온도가 달라질 수 있습니다.",MessageType.Info);
    }
    // Keep exact typed values, including those outside the drag range, until the slider is actually moved.
    public static double DragParameter(string label,double value,double minimum,double maximum,double scale,string tooltip=null){
        double result=EditorGUILayout.DelayedDoubleField(new GUIContent(label,tooltip??"숫자를 직접 입력하거나 아래 막대를 드래그하세요. 작은 값은 세밀하게, 큰 값은 넓게 조절됩니다."),value);
        if(!string.IsNullOrEmpty(tooltip))GUI.Label(GUILayoutUtility.GetLastRect(),new GUIContent("",tooltip),GUIStyle.none);
        double shown=ThermalExchange.Finite(result)?System.Math.Clamp(result,minimum,maximum):minimum;
        double lo=System.Math.Log(1+minimum/scale),hi=System.Math.Log(1+maximum/scale);
        float position=(float)((System.Math.Log(1+shown/scale)-lo)/(hi-lo));
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label(new GUIContent("드래그",(tooltip==null?"":tooltip+"\n\n")+$"드래그 범위: {minimum:G5} ~ {maximum:G5}. 범위 밖의 값은 숫자로 입력할 수 있습니다."),GUILayout.Width(48));
        EditorGUI.BeginChangeCheck();
        float next=GUILayout.HorizontalSlider(position,0f,1f);
        if(!string.IsNullOrEmpty(tooltip))GUI.Label(GUILayoutUtility.GetLastRect(),new GUIContent("",tooltip),GUIStyle.none);
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
                bool enabled=EditorGUILayout.Toggle(new GUIContent("전원","켜면 설치된 가온판에 매분 열을 공급하고, 끄면 공급을 멈춥니다. 소지품·손에 있을 때는 켜져 있어도 가열하지 않습니다."),heater.Enabled);
                float watts=EditorGUILayout.DelayedFloatField(new GUIContent("총 출력 (W, 0~1000)","가온판의 초당 열 공급량입니다.\n높이면 분당 공급 열량(출력×60초)이 커지고, 낮추면 작아집니다. 0이면 열을 공급하지 않습니다."),heater.OutputWatts);
                if(enabled!=heater.Enabled||watts!=heater.OutputWatts)if(!heater.TryConfigure(enabled,watts))Debug.LogWarning("출력은 유한한 0~1000 W여야 합니다.");
                EditorGUILayout.LabelField(new GUIContent("지난 1분 공급 열량","직전 틱에 가온판이 실제로 공급한 열량입니다. 읽기 전용이며 출력·전원·설치 여부에 따라 달라집니다."),heater.LastSuppliedJoules.ToString("F2")+" J");
            }
        }
    }

}
