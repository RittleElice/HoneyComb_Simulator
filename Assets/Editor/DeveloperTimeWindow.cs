using System;
using UnityEditor;
using UnityEngine;
public sealed class DeveloperTimeWindow : EditorWindow
{
    private long year = 1;
    private int month = 1, day = 1, hour, minute;
    private int jumpMode;
    private long durationYears, durationDays = 1, durationHours, durationMinutes;
    private static readonly string[] JumpModes = { "지정 날짜로 이동", "기간만큼 이동" };
    private float speed = 1;
    private string error;
    [MenuItem("HoneyComb/Developer/Time")]
    public static void Open() => GetWindow<DeveloperTimeWindow>("개발 시간 제어");
    private void OnInspectorUpdate() => Repaint();
    private void OnGUI()
    {
        var runtime = GameBootstrap.Instance?.TimeSystem;
        EditorGUILayout.LabelField(runtime != null ? runtime.Clock.ToString() : "Bootstrap에서 Play를 시작하세요.");
        EditorGUILayout.HelpBox("날씨는 게임 시간 1분마다 생성됩니다. 시간 점프 중에도 날씨 상태를 포함한 전체 월드 체크포인트를 저장합니다.", MessageType.Info);
        using (new EditorGUI.DisabledScope(!Application.isPlaying || runtime == null || !runtime.HasWorld || runtime.Blocked))
        {
            speed = EditorGUILayout.FloatField("배속 (1~1000)", speed);
            if (GUILayout.Button("배속 적용")) Try(() => runtime.SetDeveloperSpeed(speed));
            EditorGUILayout.Space();
            EditorGUI.BeginChangeCheck();
            jumpMode = GUILayout.Toolbar(jumpMode, JumpModes);
            if (EditorGUI.EndChangeCheck()) error = null;
            if (jumpMode == 0)
            {
                year = EditorGUILayout.LongField("목표 년차", year);
                month = EditorGUILayout.IntField("월 (1~12)", month);
                day = EditorGUILayout.IntField("일", day);
                hour = EditorGUILayout.IntField("시 (0~23)", hour);
                minute = EditorGUILayout.IntField("분 (0~59)", minute);
                if (GUILayout.Button("입력한 날짜까지 이동")) Try(() => runtime.JumpTo(year, month, day, hour, minute));
            }
            else
            {
                EditorGUILayout.LabelField("현재 시각부터 진행합니다. 1년 = 365일");
                durationYears = EditorGUILayout.LongField("년", durationYears);
                durationDays = EditorGUILayout.LongField("일", durationDays);
                durationHours = EditorGUILayout.LongField("시간", durationHours);
                durationMinutes = EditorGUILayout.LongField("분", durationMinutes);
                if (GUILayout.Button("입력한 기간만큼 이동")) Try(() => runtime.JumpBy(durationYears, durationDays, durationHours, durationMinutes));
            }
        }
        if (runtime != null) EditorGUILayout.LabelField(runtime.Status);
        if (!string.IsNullOrEmpty(error)) EditorGUILayout.HelpBox(error, MessageType.Error);
    }
    private void Try(Action action) { try { action(); error = null; } catch (OverflowException) { error = "입력한 날짜 또는 기간이 지원하는 시간 범위를 초과합니다."; } catch (Exception e) { error = e.Message; } }
}
