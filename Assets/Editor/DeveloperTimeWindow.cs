using System;
using UnityEditor;
using UnityEngine;
public sealed class DeveloperTimeWindow : EditorWindow
{
    private long year = 1;
    private int month = 1, day = 1, hour, minute;
    private float speed = 1;
    private string error;
    [MenuItem("HoneyComb/Developer/Time")]
    public static void Open() => GetWindow<DeveloperTimeWindow>("개발 시간 제어");
    private void OnInspectorUpdate() => Repaint();
    private void OnGUI()
    {
        var runtime = GameBootstrap.Instance?.TimeSystem;
        EditorGUILayout.LabelField(runtime != null ? runtime.Clock.ToString() : "Bootstrap에서 Play를 시작하세요.");
        EditorGUILayout.HelpBox("날씨·벌 시뮬레이션은 아직 등록되지 않았습니다. 시간 점프 중에도 전체 월드 체크포인트를 저장합니다.", MessageType.Info);
        using (new EditorGUI.DisabledScope(!Application.isPlaying || runtime == null || !runtime.HasWorld || runtime.Blocked))
        {
            speed = EditorGUILayout.FloatField("배속 (1~1000)", speed);
            if (GUILayout.Button("배속 적용")) Try(() => runtime.SetDeveloperSpeed(speed));
            year = EditorGUILayout.LongField("목표 년차", year);
            month = EditorGUILayout.IntField("월 (1~12)", month);
            day = EditorGUILayout.IntField("일", day);
            hour = EditorGUILayout.IntField("시 (0~23)", hour);
            minute = EditorGUILayout.IntField("분 (0~59)", minute);
            if (GUILayout.Button("모든 1분 틱 계산하여 이동")) Try(() => runtime.JumpTo(year, month, day, hour, minute));
        }
        if (runtime != null) EditorGUILayout.LabelField(runtime.Status);
        if (!string.IsNullOrEmpty(error)) EditorGUILayout.HelpBox(error, MessageType.Error);
    }
    private void Try(Action action) { try { action(); error = null; } catch (Exception e) { error = e.Message; } }
}
