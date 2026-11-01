using System;
using UnityEditor;
using UnityEngine;

// Preview geometry plus read-only live thermal inspection.
public sealed class HiveSpatialGridDebugWindow : EditorWindow
{
    private HiveSpatialGrid preview;
    [SerializeField] private bool live=true;
    [SerializeField] private int apiaryX,apiaryY,boxIndex,axis=1,slice=3,displayMode=1;
    [SerializeField] private bool autoRange;
    [SerializeField] private float rangeLow=0,rangeHigh=40;
    private Vector2 scroll;
    private readonly HiveTemperatureMonitor monitor=new HiveTemperatureMonitor();
    private GameWorld sampledWorld;
    private string sampleStatus;
    private bool sampleReady, lastCaptureReady;
    private double nextSample;
    private static readonly Color Cold=new Color(.12f,.35f,.9f),Hot=new Color(.95f,.2f,.08f);
    private static readonly Color SelectedLine=new Color(1,.65f,.15f),OutsideLine=new Color(.2f,.8f,.95f);

    [MenuItem("Tools/HoneyComb/Hive Spatial Grid")]
    public static void Open() => GetWindow<HiveSpatialGridDebugWindow>("Hive Spatial Grid");
    private void OnEnable() { minSize=new Vector2(500,420); }
    private void OnInspectorUpdate() { if(live) {Sample();Repaint();} }
    private HiveSpatialGrid LiveGrid(GameWorld world)
    {
        var slot=world?.Apiary.Grid.GetItemSlot(apiaryX,apiaryY);ItemData box=slot?.Item;
        if(slot?.Value is ItemAssemblyData assembly)
            box=boxIndex>=0 && boxIndex<assembly.Parts.Count ? assembly.Parts[boxIndex] : null;
        return box?.SpatialGrid;
    }
    private void Sample()
    {
        var bootstrap=GameBootstrap.Instance;var world=bootstrap?.GameWorld;
        if(!ReferenceEquals(sampledWorld,world)) {monitor.Reset();sampledWorld=world;nextSample=0;}
        sampleReady=false;
        if(!EditorApplication.isPlaying || world?.Thermal==null) {sampleStatus="Bootstrap에서 Play를 시작하고 새 게임 또는 이어하기를 실행하세요.";return;}
        var grid=LiveGrid(world);
        if(grid==null) {monitor.Reset();sampleStatus="선택한 위치에 벌통 상자나 뚜껑이 없습니다.";return;}
        if(bootstrap.TimeSystem?.Scheduler?.IsInTick==true || world.Weather==null || world.Weather.Minute!=world.CurrentGameMinute)
        {sampleStatus="계산이 완료된 시점의 온도를 기다리는 중입니다.";return;}
        if(world.Thermal.Failure!=null || bootstrap.TimeSystem?.Failure!=null)
        {sampleStatus="시뮬레이션 오류: "+(world.Thermal.Failure ?? bootstrap.TimeSystem.Failure);return;}
        if(EditorApplication.timeSinceStartup<nextSample && monitor.Matches(grid)) {sampleReady=lastCaptureReady;return;}
        nextSample=EditorApplication.timeSinceStartup+.25;
        sampleReady=lastCaptureReady=monitor.Capture(grid,world.CurrentGameMinute,world.Weather.Current.TemperatureCelsius,out sampleStatus);
    }
    private void OnGUI()
    {
        EditorGUI.BeginChangeCheck();
        live=EditorGUILayout.Toggle("실행 중인 양봉장 보기",live);
        HiveSpatialGrid grid;
        if(live)
        {
            apiaryX=EditorGUILayout.IntField("양봉장 X (0부터)",apiaryX);
            apiaryY=EditorGUILayout.IntField("양봉장 Y (0부터)",apiaryY);
            boxIndex=EditorGUILayout.IntField("조립 부품 번호 (0부터)",boxIndex);
            grid=LiveGrid(GameBootstrap.Instance?.GameWorld);
        }
        else
        {
            EditorGUILayout.BeginHorizontal();
            if(GUILayout.Button("단상 샘플"))CreatePreview(ItemKind.BroodBox);
            if(GUILayout.Button("계상 샘플"))CreatePreview(ItemKind.Super);
            if(GUILayout.Button("뚜껑 샘플"))CreatePreview(ItemKind.Lid);
            EditorGUILayout.EndHorizontal();grid=preview;
        }
        if(EditorGUI.EndChangeCheck()) {monitor.Reset();nextSample=0;}
        if(live) Sample();
        else {sampleReady=false;sampleStatus="샘플은 구조 미리보기입니다. 실제 온도는 Play 중인 양봉장에서 확인하세요.";}
        if(grid==null) {EditorGUILayout.HelpBox(sampleStatus ?? "샘플 또는 양봉장 위치를 선택하세요.",MessageType.Info);return;}
        displayMode=GUILayout.Toolbar(displayMode,new[]{"재질","온도"});
        axis=GUILayout.Toolbar(axis,new[]{"YZ (X 고정)","XZ (Y 고정)","XY (Z 고정)"});
        slice=EditorGUILayout.IntSlider("단면 위치",slice,0,(axis==0?18:axis==1?52:grid.Layout.SizeZ)-1);
        EditorGUILayout.LabelField($"{grid.Box.Name}   |   18 × 52 × {grid.Layout.SizeZ} = {grid.CellCount:N0} 격자");
        EditorGUILayout.LabelField($"496 × 390 × {grid.Layout.Z[grid.Layout.SizeZ]} mm   |   격왕판: {(grid.TopBoundaryEquipment!=null?"있음":"없음")}");
        bool thermal=sampleReady && monitor.Matches(grid);
        if(!thermal)EditorGUILayout.HelpBox(sampleStatus ?? "온도 갱신 대기 중",MessageType.Info);
        else EditorGUILayout.LabelField($"게임 {monitor.Minute:N0}분   외기 {monitor.Outside:F2}°C   최저 {monitor.Minimum:F2} / 최고 {monitor.Maximum:F2}°C   열용량 가중 평균 {monitor.WeightedMean:F2}°C",EditorStyles.wordWrappedLabel);
        double low=rangeLow,high=rangeHigh;
        if(displayMode==1)
        {
            autoRange=EditorGUILayout.Toggle("온도 색상 범위 자동",autoRange);
            if(autoRange && thermal) {low=monitor.Minimum;high=monitor.Maximum;if(high-low<1){double mid=(low+high)/2;low=mid-.5;high=mid+.5;}}
            if(!autoRange)
            {
                EditorGUILayout.BeginHorizontal();rangeLow=EditorGUILayout.FloatField("최저 °C",rangeLow);rangeHigh=EditorGUILayout.FloatField("최고 °C",rangeHigh);EditorGUILayout.EndHorizontal();low=rangeLow;high=rangeHigh;
            }
            if(!ThermalExchange.Finite(low)||!ThermalExchange.Finite(high)||high<=low)
            {EditorGUILayout.HelpBox("색상 최댓값은 최솟값보다 커야 합니다. 임시로 0~40°C를 표시합니다.",MessageType.Warning);low=0;high=40;}
            DrawLegend(low,high);
        }
        else EditorGUILayout.LabelField("공기: 파랑 / 목재: 갈색 / 소비: 노랑 / 단열재: 보라");
        scroll=EditorGUILayout.BeginScrollView(scroll);
        var horizontal=axis==0?grid.Layout.Y:grid.Layout.X;var vertical=axis==2?grid.Layout.Y:grid.Layout.Z;
        const float scale=1.25f;
        Rect area=GUILayoutUtility.GetRect((float)horizontal[horizontal.Count-1]*scale,(float)vertical[vertical.Count-1]*scale,GUILayout.ExpandWidth(false));
        for(int v=0;v<vertical.Count-1;v++)for(int h=0;h<horizontal.Count-1;h++)
        {
            int x=axis==0?slice:h,y=axis==0?h:axis==1?slice:v,z=axis==2?slice:v,index=grid.Layout.Index(x,y,z);
            var cell=grid.GetCell(index);
            Color material=cell.Material==HiveMaterial.Air?new Color(.3f,.55f,.75f):cell.Material==HiveMaterial.Wood?new Color(.5f,.28f,.1f):cell.Material==HiveMaterial.Comb?Color.yellow:new Color(.7f,.3f,.8f);
            Color color=displayMode==0?material:thermal?TemperatureColor(monitor.Temperatures[index],low,high):Color.gray;
            var rect=new Rect(area.x+(float)horizontal[h]*scale,area.y+(float)(vertical[vertical.Count-1]-vertical[v+1])*scale,(float)(horizontal[h+1]-horizontal[h])*scale-1,(float)(vertical[v+1]-vertical[v])*scale-1);
            EditorGUI.DrawRect(rect,color);
            string tip=$"({x}, {y}, {z}) {cell.Material}\n{cell.Equipment?.Name ?? "공기"}";
            if(thermal) tip+=$"\n온도 {monitor.Temperatures[index]:F3} °C\n열용량 {monitor.Capacities[index]:F5} J/K\n열에너지 {monitor.Energies[index]:F5} J";
            else tip+=$"\n미리보기 열용량 {grid.GetHeatCapacityJPerK(index):F4} J/K";
            GUI.Label(rect,new GUIContent("",tip));
            if(thermal && monitor.Selected==index) DrawBorder(rect,Color.white);
            if(thermal && Event.current.type==EventType.MouseDown && Event.current.button==0 && rect.Contains(Event.current.mousePosition))
            {monitor.Select(index);Event.current.Use();Repaint();}
        }
        if(axis!=2 && grid.TopBoundaryEquipment!=null && slice>0 && slice<(axis==0?17:51))
        {
            float height=(float)(vertical[vertical.Count-1]-grid.Layout.Z[grid.Layout.SizeZ-1])*scale;
            EditorGUI.DrawRect(new Rect(area.x+20*scale,area.y+height-1,(float)(horizontal[horizontal.Count-1]-40)*scale,2),Color.magenta);
        }
        if(thermal) DrawSelection(grid);
        EditorGUILayout.EndScrollView();
    }
    private static Color TemperatureColor(double t,double low,double high)
    {float f=Mathf.Clamp01((float)((t-low)/(high-low)));return f<.5f?Color.Lerp(Cold,new Color(.2f,.85f,.65f),2*f):Color.Lerp(new Color(.2f,.85f,.65f),Hot,2*f-1);}
    private static void DrawBorder(Rect r,Color c)
    {EditorGUI.DrawRect(new Rect(r.x,r.y,r.width,2),c);EditorGUI.DrawRect(new Rect(r.x,r.yMax-2,r.width,2),c);EditorGUI.DrawRect(new Rect(r.x,r.y,2,r.height),c);EditorGUI.DrawRect(new Rect(r.xMax-2,r.y,2,r.height),c);}
    private static void DrawLegend(double low,double high)
    {
        Rect r=GUILayoutUtility.GetRect(100,14,GUILayout.ExpandWidth(true));
        for(int i=0;i<64;i++)EditorGUI.DrawRect(new Rect(r.x+r.width*i/64,r.y,r.width/64+1,r.height),TemperatureColor(low+(high-low)*i/63,low,high));
        EditorGUILayout.BeginHorizontal();GUILayout.Label($"{low:F1}°C 이하");GUILayout.FlexibleSpace();GUILayout.Label($"{high:F1}°C 이상");EditorGUILayout.EndHorizontal();
    }
    private void DrawSelection(HiveSpatialGrid grid)
    {
        if(monitor.Selected<0){EditorGUILayout.HelpBox("격자를 클릭하면 온도 변화 기록이 시작됩니다.",MessageType.Info);return;}
        int i=monitor.Selected;grid.Layout.Coordinates(i,out int x,out int y,out int z);var cell=grid.GetCell(i);
        EditorGUILayout.Space();EditorGUILayout.LabelField($"선택 격자 ({x}, {y}, {z}) / {cell.Material} / {cell.Equipment?.Name ?? "공기"}",EditorStyles.boldLabel);
        EditorGUILayout.LabelField($"온도 {monitor.Temperatures[i]:F3} °C   |   열에너지 {monitor.Energies[i]:F5} J",EditorStyles.wordWrappedLabel);
        EditorGUILayout.LabelField($"열용량 {monitor.Capacities[i]:F5} J/K   (내용물 {monitor.ContentCapacities[i]:F5} J/K)",EditorStyles.wordWrappedLabel);
        if(monitor.TryRate(out double rate,out long elapsed))EditorGUILayout.LabelField($"{(elapsed==1?"직전 1분 변화":"직전 "+elapsed+"분 구간 평균 변화")}  {rate:+0.000;-0.000;0.000} °C/분");
        else EditorGUILayout.LabelField("변화량: 다음 관측 시점부터 표시");
        EditorGUILayout.LabelField("주황: 선택 격자 / 하늘색: 외기. 점선은 관측이 생략된 구간입니다.",EditorStyles.wordWrappedLabel);
        DrawHistory();
        if(GUILayout.Button("선택 격자 기록 초기화"))monitor.ClearHistory();
        EditorGUILayout.HelpBox("최근 240개 관측을 보관합니다. 시간 점프 중 모든 분을 기록하지는 않습니다. 변화량에는 내용물·열용량 변경의 영향도 포함됩니다. 대상·배치가 바뀌면 기록을 초기화합니다.",MessageType.None);
    }
    private void DrawHistory()
    {
        var points=monitor.History;Rect full=GUILayoutUtility.GetRect(300,165,GUILayout.ExpandWidth(true));
        EditorGUI.DrawRect(full,new Color(.12f,.12f,.12f));if(points.Count==0)return;
        double low=double.PositiveInfinity,high=double.NegativeInfinity;
        foreach(var p in points){low=Math.Min(low,Math.Min(p.Temperature,p.Outside));high=Math.Max(high,Math.Max(p.Temperature,p.Outside));}
        double pad=Math.Max(.2,(high-low)*.1);low-=pad;high+=pad;
        Rect plot=new Rect(full.x+65,full.y+10,Math.Max(1,full.width-80),full.height-35);
        GUI.Label(new Rect(full.x+3,plot.y,62,20),$"{high:F1}°C",EditorStyles.whiteLabel);
        GUI.Label(new Rect(full.x+3,plot.yMax-18,62,20),$"{low:F1}°C",EditorStyles.whiteLabel);
        long first=points[0].Minute,last=points[points.Count-1].Minute;double span=Math.Max(1,last-first);
        GUI.Label(new Rect(plot.x,plot.yMax+4,120,20),$"{first:N0}분",EditorStyles.whiteLabel);
        GUI.Label(new Rect(plot.xMax-120,plot.yMax+4,120,20),$"{last:N0}분",EditorStyles.whiteLabel);
        Handles.BeginGUI();
        for(int series=0;series<2;series++)
        {
            Handles.color=series==0?SelectedLine:OutsideLine;Vector3 previous=default;
            for(int n=0;n<points.Count;n++)
            {
                var p=points[n];double t=series==0?p.Temperature:p.Outside;
                var position=new Vector3(plot.x+(float)((p.Minute-first)/span)*plot.width,plot.yMax-(float)((t-low)/(high-low))*plot.height,0);
                if(n>0){if(p.Minute-points[n-1].Minute>1)Handles.DrawDottedLine(previous,position,4);else Handles.DrawAAPolyLine(2,previous,position);}
                EditorGUI.DrawRect(new Rect(position.x-2,position.y-2,4,4),series==0?SelectedLine:OutsideLine);previous=position;
            }
        }
        Handles.color=Color.white;Handles.EndGUI();
    }
    private void CreatePreview(ItemKind kind)
    {
        var box=new ItemData(0,"Debug box",kind);
        if(kind==ItemKind.Lid){preview=box.SpatialGrid;return;}
        box.InteriorSlots.GetSlot(0).TryInsert(new ItemData(1,"Frame",ItemKind.Frame,new FrameData()));
        box.InteriorSlots.GetSlot(1).TryInsert(new ItemData(2,"Divider",ItemKind.DividerBoard));
        box.InteriorSlots.GetSlot(2).TryInsert(new ItemData(3,"Insulation",ItemKind.InsulationBoard));
        box.TopSlot.TryInsert(new ItemData(4,"Excluder",ItemKind.QueenExcluder));preview=box.SpatialGrid;
    }
}
