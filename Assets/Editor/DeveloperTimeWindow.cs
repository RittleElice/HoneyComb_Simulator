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
        if(runtime != null && runtime.HasWorld)
        {
            EditorGUILayout.LabelField("미처리 예약 시간", FormatDuration(runtime.DeveloperRemainingMinutes));
            EditorGUILayout.LabelField("진행 중 작업의 남은 시간", FormatDuration(runtime.DeveloperActiveRemainingMinutes));
            EditorGUILayout.LabelField("추가 대기 시간", FormatDuration(runtime.DeveloperPendingMinutes));
            EditorGUILayout.LabelField("예약 완료 시각", runtime.DeveloperScheduledTargetTime);
            EditorGUILayout.HelpBox("예약 시간은 게임 시간입니다. 진행 중인 이동의 남은 시간과 복귀·불러오기로 추가된 대기 시간을 합산합니다. 실제 계산에 걸릴 시간의 예측값은 아닙니다.", MessageType.None);
        }
        EditorGUILayout.HelpBox("날씨는 게임 시간 1분마다 생성됩니다. 시간 점프 중에도 날씨 상태를 포함한 전체 월드 체크포인트를 저장합니다.", MessageType.Info);
        using (new EditorGUI.DisabledScope(!Application.isPlaying || runtime == null || !runtime.HasWorld || runtime.Blocked))
        {
            if(runtime != null) speed = runtime.DeveloperSpeed;
            EditorGUI.BeginChangeCheck();
            speed = EditorGUILayout.Slider("배속 (1~1000)", speed, 1, 1000);
            if (EditorGUI.EndChangeCheck()) Try(() => runtime.SetDeveloperSpeed(speed));
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
    private static string FormatDuration(long minutes)
    {
        if(minutes==0) return "없음 (0분)";
        long years=minutes/GameClock.MinutesPerYear;
        long days=minutes%GameClock.MinutesPerYear/GameClock.MinutesPerDay;
        long hours=minutes%GameClock.MinutesPerDay/60;
        long remainder=minutes%60;
        return $"{years:N0}년 {days}일 {hours}시간 {remainder}분 (총 {minutes:N0}분)";
    }
    private void Try(Action action) { try { action(); error = null; } catch (OverflowException) { error = "입력한 날짜 또는 기간이 지원하는 시간 범위를 초과합니다."; } catch (Exception e) { error = e.Message; } }
}
