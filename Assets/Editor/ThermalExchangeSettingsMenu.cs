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
        EditorGUILayout.LabelField("사각뿔 6개 열교환",EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("각 면에 열용량의 1/6을 배분하고, 면적×재질 조합 계수로 60초간 지수 교환한 뒤 합칩니다. 열전도율·두께 저항·분당 교환 제한은 사용하지 않습니다. 외기는 날씨 온도를 유지하며 내부 공기와 별도 계수를 사용합니다.",MessageType.Info);
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
    private static string HeatTooltip(string pair,bool outside){
        if(pair=="공기 ↔ 외기")return "현재 직접 노출된 공기 셀은 외기 상태로 고정하므로 이 기본 열교환 계수를 사용하지 않습니다. 값을 높이거나 낮춰도 고정 공기 경계에는 영향이 없습니다.";
        return pair+" 접촉면의 단위 면적당 열교환 계수입니다.\n높이면 온도가 높은 쪽에서 낮은 쪽으로 열이 더 빨리 이동하고, 낮추면 느려집니다. 온도 차이가 없으면 열은 이동하지 않습니다.\n"+
            (outside?"0이면 이 기본 외기 열교환을 끕니다. 바람에 의한 추가 열교환은 별도로 남을 수 있습니다.":"0이면 해당 내부 재질 조합의 열교환을 끕니다.");
    }
    private void DrawTransfers(){
        var asset=Resources.Load<PyramidTransferSettings>("Thermal/PyramidTransferSettings");if(asset==null){EditorGUILayout.HelpBox("열교환 계수 설정이 없습니다.",MessageType.Error);return;}
        var so=new SerializedObject(asset);so.Update();
        string[] names={"AirAir","AirWood","AirComb","AirInsulation","WoodWood","WoodComb","WoodInsulation","CombComb","CombInsulation","InsulationInsulation","AirOutside","WoodOutside","CombOutside","InsulationOutside"};
        string[] labels={"공기 ↔ 공기","공기 ↔ 목재","공기 ↔ 소방 복합층","공기 ↔ 단열재","목재 ↔ 목재","목재 ↔ 소방 복합층","목재 ↔ 단열재","소방 ↔ 소방","소방 ↔ 단열재","단열재 ↔ 단열재","공기 ↔ 외기","목재 ↔ 외기","소방 ↔ 외기","단열재 ↔ 외기"};
        bool valid=true;for(int i=0;i<names.Length;i++){if(i==0||i==10){EditorGUILayout.Space();EditorGUILayout.LabelField(i==0?"내부 재질 조합 · W/(m²·K)":"외기 접촉 · W/(m²·K)",EditorStyles.boldLabel);}var p=so.FindProperty(names[i]);p.doubleValue=DragParameter(labels[i],p.doubleValue,0,1000,1,HeatTooltip(labels[i],i>=10));valid &= ThermalExchange.Finite(p.doubleValue)&&p.doubleValue>=0;}
        if(valid)so.ApplyModifiedProperties();else EditorGUILayout.HelpBox("계수는 유한한 0 이상이어야 합니다.",MessageType.Error);
        EditorGUILayout.HelpBox("0은 해당 조합의 교환을 끕니다. 편집은 다음 열 계산부터 반영되며, 아래 저장 버튼으로 프로젝트 기본값에 저장합니다. 초기값은 게임용 시작값이며 실측 보정값이 아닙니다.",MessageType.Info);
    }
    private void DrawMoisture(){
        EditorGUILayout.Space();EditorGUILayout.LabelField("수분 교환",EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("외부에 직접 열린 순수 공기 셀은 외기 온도·수증기압으로 고정합니다. 이 셀과 외기 사이에는 유한한 교환계수를 적용하지 않습니다. 내부 접촉면은 기존 사각뿔 식을 사용하며, 목재·단열재의 외기 교환과 바람 보정은 유지합니다.",MessageType.Info);
        var asset=Resources.Load<PyramidTransferSettings>("Thermal/PyramidTransferSettings");if(asset==null)return;
        var so=new SerializedObject(asset);so.Update();
        string[] names={"MoistureAirPermeability","MoistureWoodPermeability","MoistureAirMobility","MoistureInsulationMobility","MoistureEvaporationMobility","MoistureExteriorAirFilmM","MoistureWindCoefficient"};
        string[] labels={"공기 투습계수 (kg/(m·s·Pa))","목재 투습계수 (kg/(m·s·Pa))","공기 경계 이동 배율","단열재 경계 이동 배율","소방 증발 배율","외기 공기막 두께 (m)","바람 교환 보정계수 (무차원 · 열/수분 공통)"};
        string[] tooltips={
            "공기 사이 수증기 확산과 외기 공기막의 투습 정도입니다. 면적·이동 거리와 함께 사용합니다.\n높이면 수증기압 차이가 빨리 줄고, 낮추면 국소적인 습도 차이가 오래 남습니다. 0이어도 고정 외기 경계와 재질을 통한 교환은 남을 수 있습니다.",
            "목재가 포함된 경로의 수분 전달 정도입니다.\n높이면 흡습·방습과 목재를 통한 전달이 빨라지고, 낮추면 느려집니다. 목재의 저장 용량이나 평형 흡습 곡선은 바꾸지 않습니다.",
            "공기와 단열재 등 사이 경계 교환의 배율입니다. 공기끼리의 확산은 공기 투습계수를 사용합니다.\n높이면 상대 재질이 병목이 아닐 때 교환이 빨라집니다. 낮추면 해당 경계 교환을 제한할 수 있습니다. 상대 재질 배율이 더 작으면 이 값만 높여도 변화가 없을 수 있습니다.",
            "단열재가 참여하는 경계에서 수분이 이동하는 정도입니다.\n높이면 흡습·방습이 빨라지고, 낮추면 느려집니다. 상대 재질의 저항에 제한될 수 있으며 저장 용량은 바뀌지 않습니다.",
            "소방의 화밀 액상수가 수증기로 바뀌는 속도 배율입니다.\n높이면 증발 가능한 조건에서 화밀 건조와 공기 수분 증가가 빨라지고 증발 냉각도 커질 수 있습니다. 낮추면 느려지고 0이면 이 증발을 끕니다. 압력 차이가 없으면 값을 높여도 증발하지 않습니다.",
            "고체 재질의 외기 접촉면에 가정한 정체 공기층 두께로, 수분 이동 저항입니다.\n높이면 외기 교환이 느려지고, 낮추면 빨라집니다. 0보다 커야 합니다. 외기 상태로 고정된 공기 셀에는 적용하지 않습니다.",
            "같은 풍속에서 바람이 외기 교환을 강화하는 정도입니다. 열과 수분에 함께 적용됩니다.\n높이면 외기 온도·수분 상태에 더 빨리 반응하고, 낮추면 바람 영향이 줄어듭니다. 무풍에서는 영향이 없으며 0이면 바람의 추가 기여만 끕니다. 목재 자체의 투습 저항은 남습니다."
        };
        double[] maxima={1e-8,1e-9,10,1,1,.1,1};
        double[] scales={1e-10,1e-12,.1,.001,.01,.001,.01};
        bool valid=true;
        for(int i=0;i<names.Length;i++){
            var value=so.FindProperty(names[i]);value.doubleValue=DragParameter(labels[i],value.doubleValue,i==5?1e-6:0,maxima[i],scales[i],tooltips[i]+"\n수분은 높은 수증기압에서 낮은 쪽으로 이동하므로, 계수 증가가 항상 건조를 뜻하지는 않습니다. 현재 사각뿔 교환 상한에 도달하면 값을 높여도 이동량이 늘지 않을 수 있습니다.");
            valid &= ThermalExchange.Finite(value.doubleValue) && (i==5?value.doubleValue>0:value.doubleValue>=0);
        }
        if(valid)so.ApplyModifiedProperties();else EditorGUILayout.HelpBox("값은 유한한 0 이상이어야 하며, 공기막 두께는 0보다 커야 합니다.",MessageType.Error);
        EditorGUILayout.HelpBox("다음 1분 계산부터 반영됩니다. 아래 저장 버튼으로 프로젝트 기본값에 저장합니다. 사각뿔 면 교환 방식과 마지막 일괄 반영은 유지합니다.",MessageType.Info);
        EditorGUILayout.HelpBox("투습계수는 공기·목재 경로에서 거리와 함께 사용합니다. 경계 이동·증발 배율의 기준 계수는 1×10⁻⁹ kg/(m²·s·Pa)입니다. 공기 경계 배율은 단열재 등과의 경로에 쓰이며 공기끼리의 확산은 투습계수를 사용합니다. 바람 보정은 외기 열교환과 수분 교환에 함께 적용됩니다.",MessageType.None);
        EditorGUILayout.HelpBox("0은 해당 기여를 끕니다. 공기 투습계수가 0이어도 고정 외기 경계와 재질을 통한 교환은 남을 수 있습니다. 계수를 높여도 사각뿔 교환 상한 때문에 실제 이동량이 더 늘지 않을 수 있습니다. 밀랍 흡습은 꺼져 있으며, 목재 흡습 곡선과 저장 용량은 이번 설정 대상이 아닙니다.",MessageType.None);
    }
    private void DrawMaterials(){
        string[] labels={"공기","목재","밀랍","단열재"};
        for(int i=0;i<labels.Length;i++){
            var material=Resources.Load<ThermalMaterialAsset>("Thermal/"+(ThermalMaterialKind)i);
            if(material==null)continue;
            EditorGUILayout.Space();EditorGUILayout.LabelField(labels[i],EditorStyles.boldLabel);
            var so=new SerializedObject(material);so.Update();
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
