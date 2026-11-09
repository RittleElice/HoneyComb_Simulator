using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

// Preview geometry plus read-only live thermal inspection.
public sealed class HiveSpatialGridDebugWindow : EditorWindow
{
    private HiveSpatialGrid preview;
    private HiveSpatialGrid selectedGrid;
    private List<HiveSpatialGrid> previewAssembly;
    [SerializeField] private bool live=true;
    [SerializeField] private int apiaryX,apiaryY,axis=1,slice=3,displayMode=1;
    [SerializeField] private bool autoRange;
    [SerializeField] private float rangeLow=0,rangeHigh=40;
    [SerializeField] private bool pressureAutoRange=true;
    [SerializeField] private float pressureLow=0,pressureHigh=3000;
    [SerializeField] private bool recordFlow=true;
    private bool showFlowPaths;
    private ThermalWorld flowWorld;
    private EquipmentThermalState flowOwner;
    private int flowIndex=-1;
    private long flowSampleMinute=-1;
    private double nextFlowRead;
    private MoistureFlowSnapshot flow;
    private static readonly string[] FlowDirections={"-X","+X","-Y","+Y","-Z","+Z"};
    private static readonly string[] WaterKinds={"공기","목재","단열재","밀랍","화밀"};
    private Vector2 scroll;
    private HiveTemperatureMonitor monitor;
    private sealed class Pane
    {
        public readonly HiveTemperatureMonitor Monitor=new HiveTemperatureMonitor();
        public readonly HiveVaporPressureMonitor Vapor=new HiveVaporPressureMonitor();
        public bool Ready;
        public string Status;
        public double NextSample;
        public int SliceRevision = -1;
        public ulong YStarts;
    }
    private readonly Dictionary<HiveSpatialGrid,Pane> panes=new Dictionary<HiveSpatialGrid,Pane>();
    private GameWorld sampledWorld;
    private string sampleStatus;
    private bool sampleReady;
    private static readonly Color Cold=new Color(.12f,.35f,.9f),Hot=new Color(.95f,.2f,.08f);
    private static readonly Color SelectedLine=new Color(1,.65f,.15f),OutsideLine=new Color(.2f,.8f,.95f);

    [MenuItem("Tools/HoneyComb/Hive Spatial Grid")]
    public static void Open()
    {
        var window=CreateInstance<HiveSpatialGridDebugWindow>();
        window.Show();
    }
    private void RefreshTitle()
    {
        string title=live?$"Hive Grid ({apiaryX}, {apiaryY})":"Hive Grid 샘플";
        if(titleContent.text!=title) titleContent=new GUIContent(title);
    }
    private void OnEnable() { minSize=new Vector2(500,420);RefreshTitle(); }
    private void OnDisable(){flowWorld?.ObserveMoistureFlow(this,null);}
    private void RefreshFlow()
    {
        var world=live && displayMode==2 && recordFlow?GameBootstrap.Instance?.GameWorld?.Thermal:null;
        EquipmentThermalState owner=null;int index=-1;
        if(world!=null && selectedGrid!=null && panes.TryGetValue(selectedGrid,out var pane) && pane.Monitor.Selected>=0)
        {
            index=pane.Monitor.Selected;
            var source=selectedGrid.GetCell(index).SourceCell;
            owner=source?.Equipment.ThermalState??selectedGrid.Box.ThermalState;
            index=source?.Index??index;
        }
        if(!ReferenceEquals(flowWorld,world)){flowWorld?.ObserveMoistureFlow(this,null);flowWorld=world;flow=null;}
        flowWorld?.ObserveMoistureFlow(this,owner);
        if(!ReferenceEquals(flowOwner,owner) || flowIndex!=index){flow=null;flowSampleMinute=-1;flowOwner=owner;flowIndex=index;}
        var bootstrap=GameBootstrap.Instance;
        long minute=bootstrap?.GameWorld?.CurrentGameMinute??-1;
        if(owner==null){flow=null;return;}
        if(bootstrap.TimeSystem?.Scheduler?.IsInTick==true)return;
        if(flowSampleMinute!=minute || flow==null || EditorApplication.timeSinceStartup>=nextFlowRead)
        {
            nextFlowRead=EditorApplication.timeSinceStartup+.25;
            flowWorld.TryReadMoistureFlow(owner,index,out flow);
            flowSampleMinute=minute;
        }
    }
    private void OnInspectorUpdate() { if(live) Repaint(); }
    // All root parts in placement order. Inserted equipment is already projected into each box grid.
    public static List<HiveSpatialGrid> PlacementGrids(MovableItem value)
    {
        var result=new List<HiveSpatialGrid>();
        if(value is ItemAssemblyData assembly)
        {
            foreach(var part in assembly.Parts) if(part.SpatialGrid!=null) result.Add(part.SpatialGrid);
        }
        else if(value is ItemData item && item.SpatialGrid!=null) result.Add(item.SpatialGrid);
        return result;
    }
    private void Sample(HiveSpatialGrid grid)
    {
        if(!panes.TryGetValue(grid,out var pane)) {pane=new Pane();panes.Add(grid,pane);}
        monitor=pane.Monitor;sampleReady=false;
        var bootstrap=GameBootstrap.Instance;var world=bootstrap?.GameWorld;
        if(!live) {sampleStatus="샘플은 구조 미리보기입니다. 실제 온도는 Play 중인 양봉장에서 확인하세요.";return;}
        if(!EditorApplication.isPlaying || world?.Thermal==null) {sampleStatus="Bootstrap에서 Play를 시작하고 새 게임 또는 이어하기를 실행하세요.";return;}
        if(bootstrap.TimeSystem?.Scheduler?.IsInTick==true || world.Weather==null || world.Weather.Minute!=world.CurrentGameMinute)
        {sampleStatus="계산이 완료된 시점의 온도를 기다리는 중입니다.";return;}
        if(world.Thermal.Failure!=null || bootstrap.TimeSystem?.Failure!=null)
        {sampleStatus="시뮬레이션 오류: "+(world.Thermal.Failure ?? bootstrap.TimeSystem.Failure);return;}
        if(EditorApplication.timeSinceStartup>=pane.NextSample || !monitor.Matches(grid))
        {
            pane.NextSample=EditorApplication.timeSinceStartup+.25;
            pane.Ready=monitor.Capture(grid,world.CurrentGameMinute,world.Weather.Current.TemperatureCelsius,out pane.Status);
            pane.Vapor.Capture(grid,world);

        }
        sampleReady=pane.Ready;sampleStatus=pane.Status;
    }
    internal static ulong ActiveYStarts(HiveSpatialGrid grid)
    {
        ulong mask=0;
        foreach(var cell in grid.ActiveCells())
        {
            grid.Layout.Coordinates(cell.firstIndex,out _,out int y,out _);
            mask |= 1UL << y;
        }
        return mask;
    }
    private int SelectYSlice(List<HiveSpatialGrid> grids)
    {
        ulong starts=0;
        foreach(var grid in grids)
        {
            if(!panes.TryGetValue(grid,out var pane)){pane=new Pane();panes.Add(grid,pane);}
            if(pane.SliceRevision!=grid.Revision)
            {
                pane.YStarts=ActiveYStarts(grid);
                pane.SliceRevision=grid.Revision;
            }
            starts|=pane.YStarts;
        }
        int count=0,selected=0;
        for(int y=0;y<HiveGridLayout.SizeY;y++)
            if((starts & (1UL<<y))!=0){if(y<=slice)selected=count;count++;}
        selected=EditorGUILayout.IntSlider("Y 단면 (조립체 전체 기준)",selected+1,1,count)-1;
        for(int y=0;y<HiveGridLayout.SizeY;y++)
            if((starts & (1UL<<y))!=0 && selected--==0)return y;
        return 0;
    }
    private void OnGUI()
    {
        if(GUILayout.Button("새 격자 창 열기")) Open();
        EditorGUI.BeginChangeCheck();
        live=EditorGUILayout.Toggle("실행 중인 양봉장 보기",live);
        List<HiveSpatialGrid> grids;
        if(live)
        {
            apiaryX=EditorGUILayout.IntField("양봉장 X (0부터)",apiaryX);
            apiaryY=EditorGUILayout.IntField("양봉장 Y (0부터)",apiaryY);
            var world=GameBootstrap.Instance?.GameWorld;
            if(!ReferenceEquals(sampledWorld,world)) {panes.Clear();sampledWorld=world;}
            grids=PlacementGrids(world?.Apiary.Grid.GetItemSlot(apiaryX,apiaryY)?.Value);
        }
        else
        {
            EditorGUILayout.BeginHorizontal();
            if(GUILayout.Button("단상 샘플"))CreatePreview(ItemKind.BroodBox);
            if(GUILayout.Button("계상 샘플"))CreatePreview(ItemKind.Super);
            if(GUILayout.Button("뚜껑 샘플"))CreatePreview(ItemKind.Lid);
            if(GUILayout.Button("조립 샘플"))CreateAssemblyPreview();
            EditorGUILayout.EndHorizontal();grids=previewAssembly ?? (preview==null?new List<HiveSpatialGrid>():new List<HiveSpatialGrid>{preview});
        }
        if(EditorGUI.EndChangeCheck()) {panes.Clear();RefreshTitle();}
        foreach(var stale in panes.Keys.Where(g=>!grids.Contains(g)).ToArray()) panes.Remove(stale);
        RefreshFlow();
        if(grids.Count==0) {EditorGUILayout.HelpBox("선택한 양봉장 칸에 벌통 상자나 뚜껑이 없습니다. Play 중인 양봉장 또는 샘플을 선택하세요.",MessageType.Info);return;}
        displayMode=GUILayout.Toolbar(Math.Min(displayMode,2),new[]{"재질","온도","수증기압"});
        if(displayMode==2)recordFlow=EditorGUILayout.Toggle("선택 셀 수분 이동 기록",recordFlow);
        RefreshFlow();
        axis=GUILayout.Toolbar(axis,new[]{"YZ (X 고정)","XZ (Y 고정)","XY (Z 고정)"});
        int largestZ=grids.Sum(g=>g.Layout.SizeZ);
        slice=axis==1?SelectYSlice(grids):EditorGUILayout.IntSlider("단면 위치 (조립체 전체 기준)",slice,0,(axis==0?HiveGridLayout.SizeX:largestZ)-1);
        double totalHeight=grids.Sum(g=>g.Layout.Z[g.Layout.SizeZ]);
        EditorGUILayout.LabelField($"조립체: 496 × 390 × {totalHeight} mm / 아래에서 위로 연결",EditorStyles.wordWrappedLabel);
        var ready=new HashSet<HiveSpatialGrid>();
        string status=null;
        foreach(var grid in grids)
        {
            Sample(grid);
            if(sampleReady && monitor.Matches(grid)) ready.Add(grid);
            else status=sampleStatus;
        }
        int count=grids.Sum(g=>g.ActiveCellCount);
        if(ready.Count==grids.Count)
            count=count-grids.Sum(g=>panes[g].Monitor.SharedLocalCount)+grids.SelectMany(g=>panes[g].Monitor.SharedIds).Distinct().Count();
        EditorGUILayout.LabelField($"선택 칸 전체: 상자·뚜껑 {grids.Count}개 / {count:N0}개 {(ready.Count==grids.Count?"계산 셀":"부품 공간 셀")}");
        if(status!=null) EditorGUILayout.HelpBox(status,MessageType.Info);
        double low=rangeLow,high=rangeHigh;
        if(displayMode==1)
        {
            autoRange=EditorGUILayout.Toggle("온도 색상 범위 자동 (전체)",autoRange);
            if(autoRange && ready.Count>0)
            {
                low=ready.Min(g=>panes[g].Monitor.Minimum);high=ready.Max(g=>panes[g].Monitor.Maximum);
                if(high-low<1){double mid=(low+high)/2;low=mid-.5;high=mid+.5;}
            }
            if(!autoRange)
            {
                EditorGUILayout.BeginHorizontal();rangeLow=EditorGUILayout.FloatField("최저 °C",rangeLow);rangeHigh=EditorGUILayout.FloatField("최고 °C",rangeHigh);EditorGUILayout.EndHorizontal();low=rangeLow;high=rangeHigh;
            }
            if(!ThermalExchange.Finite(low)||!ThermalExchange.Finite(high)||high<=low)
            {EditorGUILayout.HelpBox("색상 최댓값은 최솟값보다 커야 합니다. 임시로 0~40°C를 표시합니다.",MessageType.Warning);low=0;high=40;}
            DrawLegend(low,high);
        }
        else if(displayMode>=2)
        {
            pressureAutoRange=EditorGUILayout.Toggle("수증기압 색상 범위 자동 (전체)",pressureAutoRange);
            low=pressureLow;high=pressureHigh;
            if(pressureAutoRange)
            {
                var pressures=grids.SelectMany(g=>panes[g].Vapor.Cells ?? Array.Empty<VaporPressureCell>())
                    .Where(c=>c.Available).SelectMany(c=>new[]{c.AirPa,c.MaterialPa})
                    .Where(v=>!double.IsNaN(v) && !double.IsInfinity(v)).ToArray();
                low=pressures.Length>0?Math.Floor(pressures.Min()/100)*100:0;
                high=pressures.Length>0?Math.Max(low+100,Math.Ceiling(pressures.Max()/100)*100):100;
            }
            else
            {
                EditorGUILayout.BeginHorizontal();
                pressureLow=EditorGUILayout.FloatField("최저 Pa",pressureLow);
                pressureHigh=EditorGUILayout.FloatField("최고 Pa",pressureHigh);
                EditorGUILayout.EndHorizontal();low=pressureLow;high=pressureHigh;
            }
            if(!ThermalExchange.Finite(low)||!ThermalExchange.Finite(high)||high<=low)
            {
                EditorGUILayout.HelpBox("유효한 최솟값과 최댓값을 입력하세요. 임시 범위는 0~3000 Pa입니다.",MessageType.Warning);
                low=0;high=3000;
            }
            DrawLegend(low,high,"Pa");
            EditorGUILayout.LabelField("공기·재질을 같은 Pa 범위로 표시 / 둘 다 있으면 왼쪽 공기 · 오른쪽 재질",EditorStyles.wordWrappedLabel);
            EditorGUILayout.LabelField("회색: 수분 저장소 없음 또는 계산 상태 없음");
            if(grids.Any(g=>!panes[g].Vapor.Ready))
                EditorGUILayout.HelpBox("장비 배치 후 완료된 1분 틱의 수분 상태가 필요합니다.",MessageType.Info);
        }
        else EditorGUILayout.LabelField("공기: 파랑 / 목재: 갈색 / 소비: 노랑 / 단열재: 보라 / 격왕판: 자홍");
        scroll=EditorGUILayout.BeginScrollView(scroll);
        const float scale=1.25f;
        double width=axis==0?390:496,height=axis==2?390:totalHeight;
        Rect area=GUILayoutUtility.GetRect((float)width*scale+200,(float)height*scale,GUILayout.ExpandWidth(false));
        double bottom=0;int firstZ=0;
        foreach(var grid in grids)
        {
            double partHeight=grid.Layout.Z[grid.Layout.SizeZ];
            if(axis!=2 || (slice>=firstZ && slice<firstZ+grid.Layout.SizeZ))
            {
                var partArea=new Rect(area.x,axis==2?area.y:area.y+(float)(totalHeight-bottom-partHeight)*scale,(float)width*scale,(float)(axis==2?390:partHeight)*scale);
                DrawGrid(grid,partArea,axis==2?slice-firstZ:slice,ready.Contains(grid),low,high,firstZ);
                GUI.Label(new Rect(partArea.xMax+8,partArea.y,190,40),$"{grid.Box.Name}\nZ {bottom:0}–{bottom+partHeight:0} mm");
            }
            bottom+=partHeight;firstZ+=grid.Layout.SizeZ;
        }
        if(selectedGrid!=null && grids.Contains(selectedGrid) && ready.Contains(selectedGrid))
        {
            monitor=panes[selectedGrid].Monitor;
            EditorGUILayout.LabelField($"선택 부품: {selectedGrid.Box.Name}",EditorStyles.boldLabel);
            DrawSelection(selectedGrid);
            if(displayMode==2)DrawFlowDetails();
        }
        else EditorGUILayout.HelpBox("격자를 클릭하면 해당 부품의 온도 변화 기록을 표시합니다.",MessageType.Info);
        foreach(var grid in grids)
        {
            DrawEquipmentList(grid.Box);
            if(ready.Contains(grid))
            {
                var data=panes[grid].Monitor;
                EditorGUILayout.LabelField($"게임 {data.Minute:N0}분 / 외기 {data.Outside:F2}°C / 최저 {data.Minimum:F2} · 최고 {data.Maximum:F2} · 열용량 가중 평균 {data.WeightedMean:F2}°C",EditorStyles.wordWrappedLabel);
            }
        }
        EditorGUILayout.EndScrollView();
    }
    internal static bool TrySliceCell(HiveSpatialGrid grid,int axis,int slice,int h,int v,out int index,out int layers,out Rect mm)
    {
        int x=axis==0?slice:h, y=axis==0?h:axis==1?slice:v, z=axis==2?slice:v;
        int physical=grid.Layout.Index(x,y,z);
        (index,layers)=grid.ActiveCellAt(physical);
        mm=default;
        // In XZ the slice can pass through any member. In XY/YZ paint only the first.
        if(axis!=1 && physical!=index) return false;
        grid.Layout.Coordinates(index,out x,out y,out z);
        var l=grid.Layout;
        var horizontal=axis==0?l.Y:l.X;
        var vertical=axis==2?l.Y:l.Z;
        int left=axis==0?y:x, bottom=axis==2?y:z;
        int right=left+(axis==0?layers:1), top=bottom+(axis==2?layers:1);
        mm=new Rect((float)horizontal[left],(float)(vertical[vertical.Count-1]-vertical[top]),
            (float)(horizontal[right]-horizontal[left]),(float)(vertical[top]-vertical[bottom]));
        return true;
    }
    private static string YRange(int y,int layers) => layers==1?y.ToString():$"{y}–{y+layers-1}";

    private void DrawGrid(HiveSpatialGrid grid,Rect area,int slice,bool thermal,double low,double high,int firstZ)
    {
        monitor=panes[grid].Monitor;
        var horizontal=axis==0?grid.Layout.Y:grid.Layout.X;var vertical=axis==2?grid.Layout.Y:grid.Layout.Z;
        const float scale=1.25f;
        Rect selectedRect=default;
        for(int v=0;v<vertical.Count-1;v++)for(int h=0;h<horizontal.Count-1;h++)
        {
            if(!TrySliceCell(grid,axis,slice,h,v,out int index,out int layers,out Rect mm))continue;
            grid.Layout.Coordinates(index,out int x,out int y,out int z);
            var cell=grid.GetCell(index);
            Color material=cell.Material==HiveMaterial.Air?new Color(.3f,.55f,.75f):cell.Material==HiveMaterial.Wood?new Color(.5f,.28f,.1f):cell.Material==HiveMaterial.Comb?Color.yellow:new Color(.7f,.3f,.8f);
            Color color=displayMode==0?material:thermal?TemperatureColor(monitor.Temperatures[index],low,high):Color.gray;
            var rect=new Rect(area.x+mm.x*scale,area.y+mm.y*scale,mm.width*scale-1,mm.height*scale-1);
            bool stackAir=thermal && monitor.SharedRows[index]>=0;
            if(stackAir && axis!=2){
                int below=grid.Neighbour(index,4);
                if(below>=0 && monitor.SharedRows[below]==monitor.SharedRows[index])rect.height+=1;
                else if(z==0 && grid.Box.ExistingThermalState.TryGetStackAir(index,out _,out _))rect.height+=1;
            }
            var vapor=panes[grid].Vapor.At(index);
            if(displayMode>=2)
            {
                bool air=vapor.Available && !double.IsNaN(vapor.AirPa);
                bool bound=vapor.Available && !double.IsNaN(vapor.MaterialPa);
                if(air && bound)
                {
                    var left=new Rect(rect.x,rect.y,rect.width*.5f,rect.height);
                    var right=new Rect(left.xMax,rect.y,rect.width-left.width,rect.height);
                    EditorGUI.DrawRect(left,TemperatureColor(vapor.AirPa,low,high));
                    EditorGUI.DrawRect(right,TemperatureColor(vapor.MaterialPa,low,high));
                }
                else
                {
                    double pressure=air?vapor.AirPa:vapor.MaterialPa;
                    EditorGUI.DrawRect(rect,air || bound?TemperatureColor(pressure,low,high):Color.gray);
                }
            }
            else EditorGUI.DrawRect(rect,color);
            string tip=$"{grid.Box.Name} / 전체 ({x}, {YRange(y,layers)}, {firstZ+z}) / 부품 내부 Z {z} {cell.Material}\n{cell.Equipment?.Name ?? "공기"}";
            if(stackAir)tip+="\n인접 공간과 합쳐진 공기 셀";
            if(thermal) tip+=$"\n온도 {monitor.Temperatures[index]:F3} °C\n열용량 {monitor.Capacities[index]:F5} J/K\n열에너지 {monitor.Energies[index]:F5} J";
            else
            {
                double capacity=0;
                for(int k=0;k<layers;k++)capacity+=grid.GetHeatCapacityJPerK(index+k*HiveGridLayout.SizeX);
                tip+=$"\n미리보기 열용량 {capacity:F4} J/K";
            }
            tip+="\n"+VaporDescription(vapor);
            GUI.Label(rect,new GUIContent("",tip));
            if(thermal && selectedGrid==grid && monitor.Selected==index){DrawBorder(rect,Color.white);selectedRect=rect;}
            if(thermal && Event.current.type==EventType.MouseDown && Event.current.button==0 && rect.Contains(Event.current.mousePosition))
            {selectedGrid=grid;monitor.Select(index);Event.current.Use();Repaint();}
        }
        if(displayMode==2 && selectedGrid==grid && selectedRect.width>0 && flow!=null)DrawFlowArrows(selectedRect);
        if(axis!=2 && grid.TopBoundaryEquipment!=null && slice>0 && slice<(axis==0?HiveGridLayout.SizeX-1:HiveGridLayout.SizeY-1))
        {
            float height=(float)(vertical[vertical.Count-1]-grid.Layout.Z[grid.Layout.SizeZ-1])*scale;
            EditorGUI.DrawRect(new Rect(area.x+20*scale,area.y+height-1,(float)(horizontal[horizontal.Count-1]-40)*scale,2),Color.magenta);
        }
    }
    private static void DrawEquipmentList(ItemData box)
    {
        var names=new List<string>();
        if(box.InteriorSlots!=null)
            for(int i=0;i<box.InteriorSlots.Capacity;i++)
            {
                var item=box.InteriorSlots.GetSlot(i).Item;
                if(item!=null) names.Add($"내부 {i+1}: {item.Name} ({item.Kind}, ID {item.Id})");
            }
        if(box.TopSlot?.Item!=null) names.Add($"상단: {box.TopSlot.Item.Name} ({box.TopSlot.Item.Kind}, ID {box.TopSlot.Item.Id})");
        EditorGUILayout.LabelField($"본체: {box.Name} ({box.Kind}, ID {box.Id})",EditorStyles.boldLabel);
        if(names.Count>0) EditorGUILayout.LabelField(string.Join(" / ",names),EditorStyles.wordWrappedLabel);
    }
    private static Color TemperatureColor(double t,double low,double high)
    {float f=Mathf.Clamp01((float)((t-low)/(high-low)));return f<.5f?Color.Lerp(Cold,new Color(.2f,.85f,.65f),2*f):Color.Lerp(new Color(.2f,.85f,.65f),Hot,2*f-1);}
    private static void DrawBorder(Rect r,Color c)
    {EditorGUI.DrawRect(new Rect(r.x,r.y,r.width,2),c);EditorGUI.DrawRect(new Rect(r.x,r.yMax-2,r.width,2),c);EditorGUI.DrawRect(new Rect(r.x,r.y,2,r.height),c);EditorGUI.DrawRect(new Rect(r.xMax-2,r.y,2,r.height),c);}
    private static void DrawLegend(double low,double high,string unit="°C")
    {
        Rect r=GUILayoutUtility.GetRect(100,14,GUILayout.ExpandWidth(true));
        for(int i=0;i<64;i++)EditorGUI.DrawRect(new Rect(r.x+r.width*i/64,r.y,r.width/64+1,r.height),TemperatureColor(low+(high-low)*i/63,low,high));
        EditorGUILayout.BeginHorizontal();GUILayout.Label($"{low:F1} {unit} 이하");GUILayout.FlexibleSpace();GUILayout.Label($"{high:F1} {unit} 이상");EditorGUILayout.EndHorizontal();
    }
    private void DrawSelection(HiveSpatialGrid grid)
    {
        if(monitor.Selected<0){EditorGUILayout.HelpBox("격자를 클릭하면 온도 변화 기록이 시작됩니다.",MessageType.Info);return;}
        int i=monitor.Selected;grid.Layout.Coordinates(i,out int x,out int y,out int z);var cell=grid.GetCell(i);
        EditorGUILayout.Space();EditorGUILayout.LabelField($"선택 계산 셀 ({x}, {YRange(y,grid.ActiveCellAt(i).depthCells)}, {z}) / {cell.Material} / {cell.Equipment?.Name ?? "공기"}",EditorStyles.boldLabel);
        EditorGUILayout.LabelField(VaporDescription(panes[grid].Vapor.At(i)),EditorStyles.wordWrappedLabel);
        var moisture=panes[grid].Vapor.At(i);
        if(moisture.Available)
        {
            if(moisture.Shared)EditorGUILayout.LabelField("수분량은 연결된 공간이 합쳐진 계산 셀 전체의 합계입니다.",EditorStyles.wordWrappedLabel);
            EditorGUILayout.LabelField($"총수분 {moisture.TotalWaterKg*1e6:G8} mg ({moisture.TotalWaterKg:G8} kg)",EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"공기 중 수증기 {moisture.AirWaterKg*1e6:G8} mg / 목재 흡습수 {moisture.WoodWaterKg*1e6:G8} mg",EditorStyles.wordWrappedLabel);
            EditorGUILayout.LabelField($"단열재 흡습수 {moisture.InsulationWaterKg*1e6:G8} mg / 화밀 액상수 {moisture.NectarWaterKg*1e6:G8} mg",EditorStyles.wordWrappedLabel);
        }
        if(displayMode>=2)
        {
            EditorGUILayout.LabelField($"완료된 게임 {panes[grid].Vapor.Minute:N0}분의 수분 상태",EditorStyles.miniLabel);
            return;
        }
        if(monitor.SharedRows[i]>=0)EditorGUILayout.LabelField("공유 공기 셀 · 연결된 공간의 합계");
        EditorGUILayout.LabelField($"온도 {monitor.Temperatures[i]:F3} °C   |   열에너지 {monitor.Energies[i]:F5} J",EditorStyles.wordWrappedLabel);
        EditorGUILayout.LabelField($"열용량 {monitor.Capacities[i]:F5} J/K   (내용물 {monitor.ContentCapacities[i]:F5} J/K)",EditorStyles.wordWrappedLabel);
        if(monitor.TryRate(out double rate,out long elapsed))EditorGUILayout.LabelField($"{(elapsed==1?"직전 1분 변화":"직전 "+elapsed+"분 구간 평균 변화")}  {rate:+0.000;-0.000;0.000} °C/분");
        else EditorGUILayout.LabelField("변화량: 다음 관측 시점부터 표시");
        EditorGUILayout.LabelField("주황: 선택 격자 / 하늘색: 외기. 점선은 관측이 생략된 구간입니다.",EditorStyles.wordWrappedLabel);
        DrawHistory();
        if(GUILayout.Button("선택 격자 기록 초기화"))monitor.ClearHistory();
        EditorGUILayout.HelpBox("최근 240개 관측을 보관합니다. 시간 점프 중 모든 분을 기록하지는 않습니다. 변화량에는 내용물·열용량 변경의 영향도 포함됩니다. 대상·배치가 바뀌면 기록을 초기화합니다.",MessageType.None);
    }
    private void DrawFlowDetails()
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("마지막 완료 틱의 실제 수분 이동",EditorStyles.boldLabel);
        if(!recordFlow){EditorGUILayout.HelpBox("기록을 켜고 1분 틱을 진행하세요.",MessageType.Info);return;}
        if(flow==null){EditorGUILayout.HelpBox("셀을 선택한 뒤 1분 틱을 진행하면 이동 방향과 양이 표시됩니다.",MessageType.Info);return;}
        EditorGUILayout.LabelField($"게임 {flow.Minute-1} → {flow.Minute}분 · 단위 mg/틱 (1틱=1분)");
        EditorGUILayout.LabelField("주황 화살표: 유출 / 하늘색 화살표: 유입. 현재 단면에 수직인 이동은 아래 표로 표시합니다.",EditorStyles.wordWrappedLabel);
        if(flow.Shared)EditorGUILayout.HelpBox("연결된 공간이 하나로 합쳐진 계산 셀의 전체 교환량입니다.",MessageType.Info);
        double incoming=0,outgoing=0;
        for(int direction=0;direction<6;direction++)
        {
            double input=flow.Transfers.Where(t=>t.Direction==direction && t.Incoming).Sum(t=>t.MassKg);
            double output=flow.Transfers.Where(t=>t.Direction==direction && !t.Incoming).Sum(t=>t.MassKg);
            incoming+=input;outgoing+=output;
            EditorGUILayout.LabelField($"{FlowDirections[direction]} 면", $"유입 {input*1e6:G6} / 유출 {output*1e6:G6} mg");
        }
        EditorGUILayout.LabelField($"면 교환 합계: 유입 {incoming*1e6:G6} / 유출 {outgoing*1e6:G6} / 순증가 {(incoming-outgoing)*1e6:G6} mg",EditorStyles.wordWrappedLabel);
        EditorGUILayout.LabelField($"셀 내부 증발: 화밀 → 공기 {flow.EvaporationKg*1e6:G6} mg");
        double exteriorIn=flow.Transfers.Where(t=>t.Exterior && t.Incoming).Sum(t=>t.MassKg);
        double exteriorOut=flow.Transfers.Where(t=>t.Exterior && !t.Incoming).Sum(t=>t.MassKg);
        EditorGUILayout.LabelField($"외기 교환: 유입 {exteriorIn*1e6:G6} / 유출 {exteriorOut*1e6:G6} mg");
        EditorGUILayout.LabelField($"외기 교환계수 적용 풍속: {flow.WindSpeedMps:G4} m/s");
        showFlowPaths=EditorGUILayout.Foldout(showFlowPaths,"저장소별 교환 내역",true);
        if(showFlowPaths)
        {
            foreach(var transfer in flow.Transfers)
            {
                string place=transfer.Exterior?FlowDirections[transfer.Direction]+" 외기":transfer.Direction<0?"같은 셀":FlowDirections[transfer.Direction]+" 이웃 "+transfer.OtherEquipment+" #"+transfer.OtherIndex;
                EditorGUILayout.LabelField($"{place} / {(transfer.Direction<0?"내부 이동":transfer.Incoming?"유입":"유출")} / {WaterKinds[transfer.SourceKind]} → {WaterKinds[transfer.DestinationKind]} / {transfer.MassKg*1e6:G6} mg",EditorStyles.wordWrappedLabel);
            }
            if(flow.Transfers.Count==0)EditorGUILayout.LabelField("교환량 0 mg");
        }
    }
    private void DrawFlowArrows(Rect cell)
    {
        int horizontal=axis==0?1:0,vertical=axis==2?1:2;
        for(int direction=0;direction<6;direction++)
        {
            int worldAxis=direction/2;if(worldAxis!=horizontal && worldAxis!=vertical)continue;
            float sign=direction%2==0?-1:1;
            Vector2 vector=worldAxis==horizontal?new Vector2(sign,0):new Vector2(0,-sign);
            for(int inward=0;inward<2;inward++)
            {
                bool incoming=inward==1;
                double amount=flow.Transfers.Where(t=>t.Direction==direction && t.Incoming==incoming).Sum(t=>t.MassKg);
                if(amount<=0)continue;
                Vector2 offset=new Vector2(-vector.y,vector.x)*(incoming?3:-3);
                Vector2 center=cell.center+offset,edge=center+vector*32;
                Vector2 start=incoming?edge:center,end=incoming?center:edge;
                Vector2 travel=(end-start).normalized,wing=new Vector2(-travel.y,travel.x)*4;
                Handles.BeginGUI();Handles.color=incoming?OutsideLine:SelectedLine;
                Handles.DrawAAPolyLine(2,start,end);
                Handles.DrawAAPolyLine(2,end-travel*7+wing,end,end-travel*7-wing);
                Handles.color=Color.white;Handles.EndGUI();
            }
        }
    }
    private static string VaporDescription(VaporPressureCell cell)
    {
        if(!cell.Available)return "수증기압: 계산 상태 없음";
        string air=double.IsNaN(cell.AirPa)?"공기 저장소 없음":cell.AirPa.ToString("F3")+" Pa";
        string material=double.IsNaN(cell.MaterialPa)?"흡습 저장소 없음":cell.MaterialPa.ToString("F3")+" Pa";
        return $"공기 수증기압: {air} / 재질 평형 수증기압: {material}";
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
    private void CreateAssemblyPreview()
    {
        CreatePreview(ItemKind.BroodBox);
        var slot=new ItemSlot();slot.TryInsert(preview.Box);
        var next=new ItemSlot();next.TryInsert(new ItemData(10,"Super",ItemKind.Super));slot.TryCombineFrom(next);
        next.TryInsert(new ItemData(11,"Lid",ItemKind.Lid));slot.TryCombineFrom(next);
        previewAssembly=PlacementGrids(slot.Value);
    }
    private void CreatePreview(ItemKind kind)
    {
        previewAssembly=null;
        var box=new ItemData(0,"Debug box",kind);
        if(kind==ItemKind.Lid){preview=box.SpatialGrid;return;}
        box.InteriorSlots.GetSlot(0).TryInsert(new ItemData(1,"Frame",ItemKind.Frame,new FrameData()));
        box.InteriorSlots.GetSlot(1).TryInsert(new ItemData(2,"Divider",ItemKind.DividerBoard));
        box.InteriorSlots.GetSlot(2).TryInsert(new ItemData(3,"Insulation",ItemKind.InsulationBoard));
        box.TopSlot.TryInsert(new ItemData(4,"Excluder",ItemKind.QueenExcluder));preview=box.SpatialGrid;
    }
}

