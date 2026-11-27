using System;

using System.Collections.Generic;

using System.Linq;

using UnityEditor;

using UnityEngine;



public sealed partial class BeeCycleWindow

{

    GameWorld completedWorld;

    string[] completedLines;

    long completedMinute;

    double nextCapture;

    Vector2 completedScroll;

    void CaptureBoundary(GameClock clock)

    {

        var w=World;

        if(w==null || !ReferenceEquals(w.Clock,clock))return;

        if(ReferenceEquals(completedWorld,w) && EditorApplication.timeSinceStartup<nextCapture)return;

        CaptureCompletedView(w);Repaint();

    }

    // Completed text and a rendered image remain stable while the next tick changes the world.

    void CaptureCompletedView(GameWorld w)

    {

        var lines=new List<string>();

        var equipment=new List<ItemData>();

        Collect(w.Apiary.Grid.GetItemSlot(x,y)?.Value,equipment);

        var ids=new HashSet<int>(equipment.Select(i=>i.Id));

        lines.Add($"양봉장 ({x}, {y}) · 벌 계산 {w.BeeCycle.LastMilliseconds:F2} ms · 사망 {w.BeeCycle.Counters.deaths}");

        lines.AddRange(w.BeeCycle.DemandObservation(ids));

        lines.Add(new[]{"벌 목록·Task","소비장·소방","수치·수식","집계"}[tab]);

        if(tab==0)

        {

            lines.Add($"표시 수는 대표 개체 · 일벌·봉충 1개체 = 실제 {w.BeeCycle.Settings.representativeScale:F2}마리 · 여왕은 1마리");

            int count=0,adult=0,brood=0,outside=0;

            foreach(var b in w.BeeCycle.Bees)

            {

                if(!w.BeeCycle.BelongsTo(b,ids) && !(b.stage==CycleStage.Dead&&ids.Contains(b.homeBox)))continue;

                if(b.stage==CycleStage.Adult)adult++;else if(b.stage!=CycleStage.Dead)brood++;

                if(b.outside)outside++;

                if(count>=page*100 && count<page*100+100)

                    lines.Add($"{b.id} {(b.queen?"여왕":"일벌")} {b.stage} | {b.development:F3} | E {b.energy:F1} H {b.health:F1} | {b.cargo.Sugar:F2}/{b.cargo.pollen:F2}mg | {TaskLabel(b.task)} → {b.action}");

                count++;

                if(b.id==selected)

                {

                    lines.Add($"선택 벌 {b.id} · 위치 {(w.BeeCycle.TryGetApiaryPosition(b.id,out int ax,out int ay)?$"양봉장 ({ax},{ay})":b.outside?"외부 채집":w.BeeLocations.TryGet(b.id,out var p)?p.ToString():"없음")} · 체내 당분 {b.reserve:F3} · Task {b.elapsed:F1}분 · 이동 {b.progress:F2}mm");

                    lines.Add($"목표 {(b.hasTarget?b.target.Grid.ToString():"없음")} · 다음 이동 {(b.hasWaypoint?b.waypoint.Grid.ToString():b.pathIndex<b.path.Count?b.path[b.pathIndex].Grid.ToString():"없음")}");

                    lines.Add($"자리 이동 상태 욕구 {w.BeeCycle.RelocationWeight(b):F3}");

                    if(b.outside&&w.BeeCycle.Settings.forageLoadBased!=0)lines.Add($"외부 채집 적재 {(w.BeeCycle.ForageLoadRatio(b,b.task==CycleTask.ForagePollen)*100):F1}% · 적재 귀환 확률 {(w.BeeCycle.ForageReturnProbability(b)*100):F1}%/틱");

                    foreach(var d in b.desires)lines.Add($"욕구 {d.task} → {d.target.Grid} : {d.weight:F3}");

                }

            }

            lines.Insert(2,$"전체 {count} · 성충 {adult} · 봉충 {brood} · 외부 {outside} · 페이지 {page}");

        }

        else if(tab==1)

        {

            var frames=equipment.Where(i=>i.Kind==ItemKind.Frame).ToArray();

            if(frames.Length==0){lines.Add("이 좌표에 소비장이 없습니다.");frameImage?.Dispose();frameImage=null;}

            else

            {

                var frame=frames[Math.Min(frameIndex,frames.Length-1)];

                var data=frame.GetContent<FrameData>();var face=side==0?data.Front:data.Back;
                cellX=Mathf.Clamp(cellX,0,face.Width-1);cellY=Mathf.Clamp(cellY,0,face.Height-1);CaptureFrameImage(w,frame,side);

                lines.Add(FrameLabel(frame,equipment)+(side==0?" 앞면":" 뒷면"));

                if(showCellTextGrid)for(int row=face.Height-1;row>=0;row--)

                {

                    var cells=new string[face.Width];

                    for(int col=0;col<face.Width;col++)

                    {var c=face.GetCell(col,row);cells[col]=!c.IsComplete?c.Construction.ToString():c.Content==CellContentKind.Nectar?"N":c.Content==CellContentKind.Pollen?"P":c.Content==CellContentKind.Brood?"B":"·";}

                    lines.Add(string.Join("  ",cells));

                }

                var cell=face.GetCell(cellX,cellY);

                lines.Add($"소방 ({cellX},{cellY}) · {cell.Content} · 건설 {cell.Construction}% · 봉개 {cell.CappingProgress}%");

                lines.Add($"당분 {face.GetTotalSugarMassMg(cellX,cellY):F4} mg · 수분 {face.GetWaterMassMg(cellX,cellY):F4} mg · 화분 {face.GetPollenMassMg(cellX,cellY):F4} mg");

            }

        }

        else if(tab==2)lines.Add(JsonUtility.ToJson(w.BeeCycle.Settings,true));

        else

        {

            var c=w.BeeCycle.Counters;

            lines.Add($"완료 {c.completed} · 자원 실패 {c.resourceFailed} · 경로 실패 {c.pathFailed} · 중단 {c.interrupted}");

            lines.Add($"산란 {c.eggs} · 우화 {c.emerged} · 건설 {c.built:F2} · 발열 {c.heatJ:F4} J");

            lines.Add($"외부 유입 당분 {c.externalSugar:F3} / 화분 {c.externalPollen:F3} mg · 제거 수분 {c.discardedWater:F3} mg");

            foreach(CycleAction a in Enum.GetValues(typeof(CycleAction)))lines.Add($"{a}: {c.actions[(int)a]}");

            foreach(var c2 in w.BeeCycle.Commands)if(ids.Contains(c2.target.equipment))lines.Add($"{c2.task} → {c2.target.Grid} · 필요도 {c2.need:F3}");

        }

        completedLines=lines.ToArray();completedWorld=w;completedMinute=w.CurrentGameMinute;

        nextCapture=EditorApplication.timeSinceStartup+.25;

    }

    void DrawCompletedView()

    {

        if(!ReferenceEquals(completedWorld,World) || completedLines==null)

        {EditorGUILayout.HelpBox("계산 중 · 첫 완료 상태를 기다리는 중입니다.",MessageType.Info);return;}

        EditorGUILayout.HelpBox($"계산 중 · 표시: 완료된 {completedMinute}분 상태 (선택 변경·수정은 계산 완료 후)",MessageType.Info);

        completedScroll=EditorGUILayout.BeginScrollView(completedScroll);

        if(tab==1)DrawFrameImage(false);
        foreach(var line in completedLines)EditorGUILayout.LabelField(line,EditorStyles.wordWrappedLabel);

        EditorGUILayout.EndScrollView();

    }

}

