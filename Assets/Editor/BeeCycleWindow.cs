using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEditor;
using UnityEngine;

public sealed partial class BeeCycleWindow : EditorWindow
{
    static string TaskLabel(CycleTask task)=>task==CycleTask.Relocate?"자리 이동":task.ToString();

    GameWorld testWorld;
    int x,y,tab,page,frameIndex,side,cellX,cellY;
    long selected;
    Vector2 scroll;
    bool busy;
    string message="",json="";
    BeeCycleSettings draft;
    GameWorld World=>Application.isPlaying?GameBootstrap.Instance?.GameWorld:testWorld;
    [MenuItem("Tools/HoneyComb/벌 시뮬레이션 관찰")]
    public static void Open()
    {var window=GetWindow<BeeCycleWindow>("벌·소비장 관찰");window.minSize=new Vector2(900,600);if(window.position.width<900)window.position=new Rect(120,100,1000,800);window.Show();}
    void OnEnable(){SimulationScheduler.ObservationBoundary+=CaptureBoundary;}
    void OnInspectorUpdate(){Repaint();}
    void OnDisable(){SimulationScheduler.ObservationBoundary-=CaptureBoundary;if(!busy){testWorld?.Thermal?.Dispose();testWorld=null;}}
    void OnGUI()
    {
        EditorGUILayout.LabelField(Application.isPlaying?"실제 플레이 월드":"분리된 시험 월드 — 플레이어 저장과 무관",EditorStyles.boldLabel);
        using(new EditorGUI.DisabledScope(busy||Application.isPlaying))
            if(GUILayout.Button("시험 월드 만들기")){testWorld?.Thermal?.Dispose();testWorld=new GameWorld(WeatherAssetLoader.Create(WeatherSimulator.DefaultSeed));testWorld.PrepareThermal(ThermalMaterials.Load());testWorld.BeeCycle.SeedDemo();draft=null;}
        if(busy || (Application.isPlaying && GameBootstrap.Instance?.TimeSystem?.Scheduler?.IsInTick==true))
        {DrawCompletedView();return;}
        var w=World;if(w==null){EditorGUILayout.HelpBox("Play에서 새 게임을 시작하거나 시험 월드를 만드세요.",MessageType.Info);return;}
        using(new EditorGUI.DisabledScope(busy||Application.isPlaying))
        {EditorGUILayout.BeginHorizontal();foreach(int n in new[]{1,10,60,1440})if(GUILayout.Button(n+"분 진행"))Step(n);EditorGUILayout.EndHorizontal();}
        if(w.BeeCycle.Failure!=null)EditorGUILayout.HelpBox(w.BeeCycle.Failure.Message,MessageType.Error);
        EditorGUILayout.LabelField($"게임 {w.CurrentGameMinute}분 · 벌 계산 {w.BeeCycle.LastMilliseconds:F2} ms · 사망 {w.BeeCycle.Counters.deaths}");
        EditorGUILayout.BeginHorizontal();x=EditorGUILayout.IntSlider("양봉장 X",x,0,w.Apiary.Grid.UnlockedWidth-1);y=EditorGUILayout.IntSlider("Y",y,0,w.Apiary.Grid.Height-1);EditorGUILayout.EndHorizontal();
        for(int row=0;row<w.Apiary.Grid.Height;row++)
        {EditorGUILayout.BeginHorizontal();for(int col=0;col<w.Apiary.Grid.UnlockedWidth;col++){var slot=w.Apiary.GetItemSlot(col,row);var color=GUI.backgroundColor;if(col==x&&row==y)GUI.backgroundColor=Color.yellow;if(GUILayout.Button($"{col},{row}"+(slot.IsEmpty?"":" ●"))){x=col;y=row;page=0;frameIndex=0;}GUI.backgroundColor=color;}EditorGUILayout.EndHorizontal();}
        var root=w.Apiary.GetItemSlot(x,y).Value;var equipment=new List<ItemData>();Collect(root,equipment);var ids=new HashSet<int>(equipment.Select(i=>i.Id));
        EditorGUILayout.LabelField(root==null?"빈 좌표":$"선택 좌표 ({x},{y}) · 장비 {equipment.Count}개");
        tab=GUILayout.Toolbar(tab,new[]{"벌 목록·Task","소비장·소방","수치·수식","집계"});
        scroll=EditorGUILayout.BeginScrollView(scroll);
        if(tab==0)BeeList(w,ids);
        else if(tab==1)Frames(w,equipment);
        else if(tab==2)Parameters(w);
        else
        {
            var c=w.BeeCycle.Counters;
            EditorGUILayout.LabelField($"완료 {c.completed} · 자원 실패 {c.resourceFailed} · 경로 실패 {c.pathFailed} · 중단 {c.interrupted}");
            EditorGUILayout.LabelField($"산란 {c.eggs} · 우화 {c.emerged} · 건설 {c.built:F2} 포인트");
            EditorGUILayout.LabelField($"외부 유입 당분 {c.externalSugar:F3} / 화분 {c.externalPollen:F3} mg");
            EditorGUILayout.LabelField($"임시 제거 수분 {c.discardedWater:F3} mg · 발열 {c.heatJ:F4} J");
            foreach(CycleAction a in Enum.GetValues(typeof(CycleAction)))EditorGUILayout.LabelField(a.ToString(),c.actions[(int)a].ToString());
            EditorGUILayout.Space();EditorGUILayout.LabelField("선택한 벌통의 활성 명령",EditorStyles.boldLabel);
            foreach(var command in w.BeeCycle.Commands)if(ids.Contains(command.target.equipment))EditorGUILayout.LabelField($"{command.task} → {command.target.Grid}",$"필요도 {command.need:F3} · {(command.localOnly?"해당 격자":"봉군 전체")}");
        }
        EditorGUILayout.EndScrollView();if(message!="")EditorGUILayout.HelpBox(message,MessageType.Info);
    }
    static void Collect(MovableItem root,List<ItemData> list)
    {
        if(root is ItemAssemblyData assembly){foreach(var p in assembly.Parts)Collect(p,list);return;}
        if(!(root is ItemData item))return;list.Add(item);
        if(item.InteriorSlots!=null)for(int i=0;i<item.InteriorSlots.Capacity;i++)Collect(item.InteriorSlots.GetSlot(i).Value,list);
        if(item.TopSlot!=null)Collect(item.TopSlot.Value,list);
    }
    void BeeList(GameWorld w,HashSet<int> ids)
    {
        var bees=w.BeeCycle.Bees.Where(b=>w.BeeCycle.BelongsTo(b,ids)||(b.stage==CycleStage.Dead&&ids.Contains(b.homeBox))).ToList();
        EditorGUILayout.LabelField($"전체 {bees.Count} · 성충 {bees.Count(b=>b.stage==CycleStage.Adult)} · 봉충 {bees.Count(b=>b.stage!=CycleStage.Adult&&b.stage!=CycleStage.Dead)} · 외부 {bees.Count(b=>b.outside)}");
        EditorGUILayout.LabelField($"표시 수는 대표 개체 · 일벌·봉충 1개체 = 실제 {w.BeeCycle.Settings.representativeScale:F2}마리 · 여왕은 1마리");
        page=EditorGUILayout.IntSlider("페이지 (100마리)",page,0,Math.Max(0,(bees.Count-1)/100));
        EditorGUILayout.LabelField("ID / 종류·단계 / 발달 / Energy / Health / 당분·화분 / Task → 행동",EditorStyles.boldLabel);
        foreach(var b in bees.Skip(page*100).Take(100))if(GUILayout.Button($"{b.id} {(b.queen?"여왕":"일벌")} {b.stage} | {b.development:F3} | E {b.energy:F1} H {b.health:F1} | {b.cargo.Sugar:F2}/{b.cargo.pollen:F2}mg | {TaskLabel(b.task)} → {b.action}"))selected=b.id;
        var chosen=bees.Find(b=>b.id==selected);if(chosen==null)return;
        EditorGUILayout.Space();EditorGUILayout.LabelField($"벌 {chosen.id} 상세",EditorStyles.boldLabel);
        EditorGUILayout.LabelField("위치",chosen.outside?"외부 채집":w.BeeLocations.TryGet(chosen.id,out var p)?p.ToString():"없음");
        EditorGUILayout.LabelField($"체내 당분 {chosen.reserve:F3} mg · Task 경과 {chosen.elapsed:F1}분 · 이동 진행 {chosen.progress:F2}mm");
        if(chosen.outside)EditorGUILayout.LabelField("채집 밀원",chosen.forageSource>=0?w.Weather.Nectar.Settings.Sources[chosen.forageSource].Name+$" · {(w.BeeCycle.Settings.forageLoadBased!=0?"적재 "+(w.BeeCycle.ForageLoadRatio(chosen,chosen.task==CycleTask.ForagePollen)*100).ToString("F1")+"% · 적재 귀환 확률 "+(w.BeeCycle.ForageReturnProbability(chosen)*100).ToString("F1")+"%/틱":"남은 "+chosen.outsideRemaining.ToString("F1")+"분")}":"기존 저장: 귀환 시 선택");
        EditorGUILayout.LabelField("목표",chosen.hasTarget?chosen.target.Grid.ToString():"없음");
        EditorGUILayout.LabelField("다음 이동",chosen.pathIndex<chosen.path.Count?chosen.path[chosen.pathIndex].Grid.ToString():"없음");
        EditorGUILayout.LabelField("자리 이동 상태 욕구",w.BeeCycle.RelocationWeight(chosen).ToString("F3"));
        foreach(var d in chosen.desires)EditorGUILayout.LabelField($"{d.task} → {d.target.Grid}",d.weight.ToString("F3"));
        EditorGUILayout.LabelField($"운반 용량: 당분 {chosen.cargo.Sugar:F2}/{w.BeeCycle.Settings.carryCapacity*w.BeeCycle.RepresentedBees(chosen):F2} mg · 화분 {chosen.cargo.pollen:F2}/{w.BeeCycle.Settings.pollenCarryCapacity*w.BeeCycle.RepresentedBees(chosen):F2} mg");
        foreach(var s in chosen.cargo.sugars)EditorGUILayout.LabelField($"화원 {s.source} 당분",$"{s.mass:F4}mg");
    }
    void Frames(GameWorld w,List<ItemData> equipment)
    {
        var frames=equipment.Where(i=>i.Kind==ItemKind.Frame).ToArray();if(frames.Length==0){EditorGUILayout.LabelField("이 좌표에 소비장이 없습니다.");return;}
        frameIndex=Math.Min(frameIndex,frames.Length-1);frameIndex=EditorGUILayout.Popup("소비장",frameIndex,frames.Select(f=>FrameLabel(f,equipment)).ToArray());
        side=GUILayout.Toolbar(side,new[]{"앞면","뒷면"});var data=frames[frameIndex].GetContent<FrameData>();var face=side==0?data.Front:data.Back;
        EditorGUILayout.LabelField("25×12 · 빈칸=완성 빈 소방 / 숫자=건설도 / N=화밀 P=화분 / 알·유·번=봉충");
        int complete=0,broodCount=0;double totalSugar=0,totalPollen=0;
        for(int row=0;row<face.Height;row++)for(int col=0;col<face.Width;col++){var c=face.GetCell(col,row);if(c.IsComplete)complete++;if(c.Content==CellContentKind.Brood)broodCount++;totalSugar+=face.GetTotalSugarMassMg(col,row);totalPollen+=face.GetPollenMassMg(col,row);}
        EditorGUILayout.LabelField($"완성 {complete}/300 · 봉충 {broodCount} · 당분 {totalSugar:F2}mg · 화분 {totalPollen:F2}mg");
        var broodById=w.BeeCycle.Bees.Where(b=>b.stage!=CycleStage.Adult&&b.stage!=CycleStage.Dead).ToDictionary(b=>b.id);
        for(int row=face.Height-1;row>=0;row--)
        {
            EditorGUILayout.BeginHorizontal();
            for(int col=0;col<face.Width;col++)
            {
                var c=face.GetCell(col,row);string label=!c.IsComplete?c.Construction.ToString():c.Content==CellContentKind.Nectar?"N":c.Content==CellContentKind.Pollen?"P":c.Content==CellContentKind.Brood?"B":c.Content==CellContentKind.Empty?"·":c.Content.ToString().Substring(0,1);
                if(c.Content==CellContentKind.Brood&&broodById.TryGetValue(face.GetBeeId(col,row),out var occupant))label=occupant.stage==CycleStage.Egg?"알":occupant.stage==CycleStage.Larva?"유":"번";
                var old=GUI.backgroundColor;GUI.backgroundColor=c.Content==CellContentKind.Brood?new Color(1,0.6f,0.4f):c.Content==CellContentKind.Nectar?Color.yellow:c.Content==CellContentKind.Pollen?new Color(0.6f,0.9f,0.4f):Color.white;
                if(col==cellX&&row==cellY)GUI.backgroundColor=Color.cyan;
                if(GUILayout.Button(label,GUILayout.MinWidth(20),GUILayout.Height(24))){cellX=col;cellY=row;}GUI.backgroundColor=old;
            }EditorGUILayout.EndHorizontal();
        }
        var cell=face.GetCell(cellX,cellY);EditorGUILayout.LabelField($"소방 ({cellX},{cellY}) · {cell.Content} · 건설 {cell.Construction}% · 봉개 {cell.CappingProgress}%");
        EditorGUILayout.LabelField($"당분 {face.GetTotalSugarMassMg(cellX,cellY):F4} mg · 수분 {face.GetWaterMassMg(cellX,cellY):F4} mg · 화분 {face.GetPollenMassMg(cellX,cellY):F4} mg");
        long id=face.GetBeeId(cellX,cellY);var brood=w.BeeCycle.Bees.Find(b=>b.id==id);
        if(brood!=null)EditorGUILayout.LabelField($"봉충 ID {id} · {brood.stage} · 발달 {brood.development:F5} · Health {brood.health:F2}");
    }
    static string FrameLabel(ItemData frame,List<ItemData> equipment)
    {
        foreach(var box in equipment)if(box.InteriorSlots!=null)for(int slot=0;slot<box.InteriorSlots.Capacity;slot++)if(ReferenceEquals(box.InteriorSlots.GetSlot(slot).Item,frame))return $"{box.Name} #{box.Id} / 슬롯 {slot} / {frame.Name} #{frame.Id}";
        return frame.Name+" #"+frame.Id;
    }
    void Parameters(GameWorld w)
    {
        if(draft==null)draft=JsonUtility.FromJson<BeeCycleSettings>(JsonUtility.ToJson(w.BeeCycle.Settings));
        EditorGUILayout.HelpBox("임시 모델입니다. 시간 단위는 게임 1분 틱, 질량 mg, 거리 mm, 발열 J입니다. 수식은 + - * / 및 min/max/pow/clamp 등을 지원합니다. 적용은 틱 경계에서만 가능합니다.",MessageType.Info);
        EditorGUILayout.HelpBox("수식 입력: ratio=상태 부족/적재 비율(발육식에서는 온도 계수), need=명령 필요도, time=해당 틱의 사용 시간 비율, amount=요구량·현재량, available=가용량·목표량, distance=이동 거리(산란식에서는 중앙까지 소방 거리), height=높이/기준 높이, temperature=°C, elapsed=Task 경과 분, actionRate=행동별 발달도/틱. 각 식의 기본 형태와 연결은 BeeCycleGuide.txt에 정리되어 있습니다.",MessageType.None);
        BeeParameterPanel.Draw(draft);
        using(new EditorGUI.DisabledScope(busy||(Application.isPlaying&&GameTimeRuntime.InputBlocked)))
        if(GUILayout.Button("검사 후 현재 월드에 적용"))try{var copy=JsonUtility.FromJson<BeeCycleSettings>(JsonUtility.ToJson(draft));w.BeeCycle.ApplySettings(copy);message="수치·수식을 현재 월드에 적용했습니다. 저장 시 함께 보존됩니다.";}catch(Exception e){message=e.Message;}
        if(GUILayout.Button("JSON으로 표시"))json=JsonUtility.ToJson(draft,true);
        json=EditorGUILayout.TextArea(json,GUILayout.MinHeight(100));
        if(GUILayout.Button("JSON 읽기·검사"))try{var copy=JsonUtility.FromJson<BeeCycleSettings>(json);copy.UpgradeForageFormula();copy.UpgradeRelocationFormulas();copy.UpgradeLoadForageFormulas();new BeeCycleRules(copy);draft=copy;message="JSON 검사를 통과했습니다. 적용 버튼으로 반영하세요.";}catch(Exception e){message=e.Message;}
        if(GUILayout.Button("새 게임 기본 파라미터로 저장"))try
        {
            new BeeCycleRules(draft);const string path="Assets/Resources/BeeCycleParameters.asset";
            var asset=AssetDatabase.LoadAssetAtPath<BeeCycleSettingsAsset>(path);
            if(asset==null){asset=CreateInstance<BeeCycleSettingsAsset>();AssetDatabase.CreateAsset(asset,path);}
            Undo.RecordObject(asset,"Bee cycle parameters");asset.values=JsonUtility.FromJson<BeeCycleSettings>(JsonUtility.ToJson(draft));EditorUtility.SetDirty(asset);AssetDatabase.SaveAssets();message="새 게임 기본 파라미터를 저장했습니다. 진행 중 월드는 적용 버튼으로 별도 변경합니다.";
        }catch(Exception e){message=e.Message;}
    }
    async void Step(int minutes)
    {
        if(busy||testWorld==null)return;CaptureCompletedView(testWorld);busy=true;
        try{await GameTimeRuntime.CreateWorldScheduler(testWorld).RunToAsync(testWorld.CurrentGameMinute+minutes,async()=>await System.Threading.Tasks.Task.Yield(),()=>System.Threading.Tasks.Task.CompletedTask,CancellationToken.None);message=$"{minutes}분 진행 완료";}
        catch(Exception e){message=e.ToString();}finally{busy=false;Repaint();}
    }
}
