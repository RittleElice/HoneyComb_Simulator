using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;

// Runs against a copied project: real Bootstrap/SharedUI/Apiary/Observation scenes.
[InitializeOnLoad]
public static class GameTimePlayTests
{
    private const string Key = "HoneyComb.GameTimePlayTests";
    private static int phase;
    private static double started, phaseStart;
    private static GameTimeRuntime runtime;
    private static SlowParticipant slow;
    private static long expected;
    private static bool sawProgress;
    private sealed class SlowParticipant : IGameMinuteParticipant
    {
        public TaskCompletionSource<bool> gate;
        public int calls;
        public Task CalculateMinuteAsync(long minute, CancellationToken token)
        {
            calls++;
            return gate?.Task ?? Task.CompletedTask;
        }
    }
    static GameTimePlayTests()
    {
        if (SessionState.GetBool(Key, false)) { started = EditorApplication.timeSinceStartup; EditorApplication.update += Tick; }
    }
    public static void BeginBatch()
    {
        GameTimeAssetSetup.CreateMissing();
        EditorSceneManager.OpenScene("Assets/Scenes/Bootstrap.unity");
        SessionState.SetBool(Key,true); started=EditorApplication.timeSinceStartup;
        EditorApplication.update -= Tick; EditorApplication.update += Tick; EditorApplication.EnterPlaymode();
    }
    private static void Check(bool value,string message) { if (!value) throw new Exception(message); }
    private static void Advance() { phase++; phaseStart = EditorApplication.timeSinceStartup; }
    private static void Tick()
    {
        try
        {
            if (EditorApplication.timeSinceStartup - started > 180) throw new Exception("Play test timeout phase " + phase);
            if (!EditorApplication.isPlaying || EditorApplication.isCompiling) return;
            double elapsed = EditorApplication.timeSinceStartup - phaseStart;
            var bootstrap = GameBootstrap.Instance;
            if (bootstrap == null || bootstrap.GameFlow == null || bootstrap.GameFlow.IsTransitioning) return;
            switch (phase)
            {
                case 0:
                    runtime = bootstrap.TimeSystem;
                    // Batch editor has no foreground window; simulate a focused running application.
                    runtime.SendMessage("OnApplicationFocus", true);
                    Check(runtime.Clock.CurrentGameMinute == 0, "menu initial minute");
                    bootstrap.StartNewGame(); Advance(); break;
                case 1:
                    if (UnityEngine.Object.FindAnyObjectByType<GameTimeUI>() == null) return;
                    Check(bootstrap.GameWorld != null && ReferenceEquals(bootstrap.GameWorld.Clock,runtime.Clock), "world shares clock");
                    slow = new SlowParticipant { gate = new TaskCompletionSource<bool>() }; runtime.Scheduler.Register(slow);
                    runtime.SetDeveloperSpeed(1000); Advance(); break;
                case 2:
                    if (slow.calls == 0) return;
                    Check(runtime.Clock.CurrentGameMinute == 0 && GameTimeRuntime.InputBlocked, "slow tick blocks and does not commit");
                    runtime.SetDeveloperSpeed(1); Advance(); break;
                case 3:
                    if (elapsed < 6) return;
                    Check(slow.calls == 1 && runtime.Clock.CurrentGameMinute == 0, "six-second tick has no overlap");
                    slow.gate.SetResult(true); slow.gate = null; Advance(); break;
                case 4:
                    if (runtime.Blocked) return;
                    Check(runtime.Clock.CurrentGameMinute == 1, "one slow tick committed"); Advance(); break;
                case 5:
                    if (elapsed < 2) return;
                    Check(runtime.Clock.CurrentGameMinute == 1 && slow.calls == 1, "no backlog catch-up");
                    runtime.JumpTo(2, 1, 0, 0); Advance(); break;
                case 6:
                    if (runtime.Blocked)
                    {
                        sawProgress = true;
                        var systems = UnityEngine.Object.FindObjectsByType<EventSystem>();
                        foreach (var system in systems) Check(!system.enabled, "event input disabled during jump");
                        return;
                    }
                    Check(sawProgress && runtime.Clock.CurrentGameMinute == 525600 && slow.calls == 525600, "full year sequential jump and responsive frames");
                    Check(UnityEngine.Object.FindAnyObjectByType<GameTimeUI>().label.text.Contains("2년차"), "clock UI updated");
                    expected = runtime.Clock.CurrentGameMinute;
                    bootstrap.GameFlow.OpenObservation(0,0); Advance(); break;
                case 7:
                    Check(ReferenceEquals(runtime,bootstrap.TimeSystem) && runtime.Clock.CurrentGameMinute == expected, "clock survives Observation transition");
                    Check(UnityEngine.Object.FindAnyObjectByType<GameTimeUI>() != null,"common UI retained");
                    runtime.SendMessage("OnApplicationPause", true); Advance(); break;
                case 8:
                    if (elapsed < 6) return;
                    Check(runtime.Clock.CurrentGameMinute == expected, "background does not run live ticks");
                    runtime.SendMessage("OnApplicationPause", false); Advance(); break;
                case 9:
                    if (runtime.Blocked) return;
                    Check(runtime.Clock.CurrentGameMinute == expected + 1,"UTC resume runs offline minute");
                    bootstrap.GameFlow.OpenApiary(); Advance(); break;
                case 10:
                    Check(UnityEngine.Object.FindAnyObjectByType<GameTimeUI>() != null,"Apiary return UI");
                    File.WriteAllText("time-play-tests.txt", "PASS: actual Bootstrap/MainMenu/SharedUI/Apiary/Observation; slow tick >5s; no overlap or backlog; full-year sequential jump with frame progress and input lock; scene continuity; UTC pause/resume.");
                    SessionState.SetBool(Key,false); EditorApplication.update -= Tick; EditorApplication.Exit(0); break;
            }
        }
        catch (Exception e)
        {
            Debug.LogException(e); File.WriteAllText("time-play-tests.txt",e.ToString()); SessionState.SetBool(Key,false); EditorApplication.update -= Tick; EditorApplication.Exit(1);
        }
    }
}

