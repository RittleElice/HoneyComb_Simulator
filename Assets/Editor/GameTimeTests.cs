using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

public static class GameTimeTests
{
    private static int checks;
    private static void Check(bool ok, string message) { checks++; if (!ok) throw new Exception(message); }
    private sealed class Participant : IGameMinuteParticipant
    {
        public Func<long, Task> Calculate;
        public Task CalculateMinuteAsync(long minute, CancellationToken cancellation) => Calculate(minute);
    }
    public static async Task RunAll()
    {
        var clock = new GameClock();
        Check(clock.Year == 1 && clock.Month == 1 && clock.Day == 1 && clock.Hour == 0 && clock.Minute == 0, "initial time");
        var scheduler = new SimulationScheduler(clock);
        long calls = 0;
        scheduler.Register(new Participant { Calculate = m => { Check(m == clock.CurrentGameMinute + 1, "pre-commit minute"); calls++; return Task.CompletedTask; } });
        await scheduler.RunToAsync(GameClock.ToMinute(4, 60, 0, 0), () => Task.CompletedTask, () => Task.CompletedTask, CancellationToken.None);
        Check(clock.Year == 4 && clock.Month == 3 && clock.Day == 1, "year 4 is not leap year");
        Check(calls == clock.CurrentGameMinute && scheduler.Completed == scheduler.Total, "all 1.6m minute ticks execute");
        Check(GameClock.ToMinute(2,1,0,0) == 525600, "365 days");
        Check(GameClock.ToMinute(1,365,23,59) == 525599, "year end");
        foreach (var invalid in new Action[] { () => GameClock.ToMinute(0,1,0,0), () => GameClock.ToMinute(1,366,0,0), () => GameClock.ToMinute(1,1,24,0), () => GameClock.ToMinute(long.MaxValue,1,0,0) })
        { bool failed = false; try { invalid(); } catch (Exception) { failed = true; } Check(failed, "invalid date rejected"); }
        var slowClock = new GameClock(); var slow = new SimulationScheduler(slowClock); var wait = new TaskCompletionSource<bool>();
        slow.Register(new Participant { Calculate = _ => wait.Task });
        var run = slow.RunToAsync(1, () => Task.CompletedTask, () => Task.CompletedTask, CancellationToken.None);
        Check(slowClock.CurrentGameMinute == 0 && slow.IsRunning, "async tick not committed early");
        bool overlapRejected = false;
        try { await slow.RunToAsync(1, () => Task.CompletedTask, () => Task.CompletedTask, CancellationToken.None); } catch (InvalidOperationException) { overlapRejected = true; }
        Check(overlapRejected && slow.IsRunning, "overlap rejected without releasing first operation");
        wait.SetResult(true); await run; Check(slowClock.CurrentGameMinute == 1 && !slow.IsRunning, "commit after async completion");
        var brokenClock = new GameClock(); var broken = new SimulationScheduler(brokenClock);
        broken.Register(new Participant { Calculate = _ => throw new Exception("injected failure") });
        try { await broken.RunToAsync(2, () => Task.CompletedTask, () => Task.CompletedTask, CancellationToken.None); } catch { }
        Check(brokenClock.CurrentGameMinute == 0 && broken.Fault != null && !broken.IsRunning, "failed tick cannot commit");
        var cancel = new CancellationTokenSource(); var cancelClock = new GameClock(); var cancelled = new SimulationScheduler(cancelClock);
        cancelled.Register(new Participant { Calculate = _ => { cancel.Cancel(); return Task.CompletedTask; } });
        try { await cancelled.RunToAsync(10, () => Task.CompletedTask, () => Task.CompletedTask, cancel.Token); } catch (OperationCanceledException) { }
        Check(cancelClock.CurrentGameMinute == 1 && cancelled.Fault == null, "boundary cancellation preserves completed tick");
        var utc = new DateTime(2026,1,1,0,0,0,DateTimeKind.Utc); var offline = new OfflineTimeManager();
        offline.Suspend(utc); offline.Suspend(utc.AddSeconds(1)); Check(offline.Resume(utc.AddSeconds(12)) == 2, "duplicate pause coalesced");
        Check(offline.Resume(utc.AddSeconds(20)) == 0, "duplicate resume ignored");
        offline.Suspend(utc.AddSeconds(20)); Check(offline.Resume(utc.AddSeconds(23)) == 1, "fraction carried");
        offline = new OfflineTimeManager(); offline.Suspend(utc); Check(offline.Resume(utc.AddYears(10)) > 60000000, "offline duration uncapped");
        offline = new OfflineTimeManager();
        offline.Suspend(utc); Check(offline.Resume(utc.AddHours(-1)) == 0 && offline.ClockMovedBackwards, "UTC rollback clamped");
        Check(offline.Resume(utc.AddSeconds(5)) == 0, "rollback resume is closed");
        offline.Suspend(utc.AddSeconds(10)); Check(offline.Resume(utc.AddSeconds(15)) == 1, "online gap after rollback is not counted");
        offline.Suspend(utc.AddSeconds(5)); Check(offline.Resume(utc.AddSeconds(20)) == 1, "high-water prevents replaying elapsed UTC");
        var policy = new CheckpointPolicy(); for (int i=0;i<100;i++) policy.ObserveWriteCost(100);
        Check(policy.IntervalSeconds <= 120 && policy.IntervalSeconds > 119, "adaptive upper bound");
        for (int i=0;i<100;i++) policy.ObserveWriteCost(0);
        Check(policy.IntervalSeconds >= 10 && policy.IntervalSeconds < 11, "adaptive lower bound");
        Debug.Log("GameTimeTests PASS: " + checks + " checks");
    }
    public static async void RunBatch()
    {
        try
        {
            GameTimeAssetSetup.CreateMissing();
            await RunAll();
            ItemAssemblyTests.RunAll(); HiveSpatialGridTests.RunAll(); CellStateTests.RunAll(); CombGeometryTests.RunAll(); EquipmentCellDataTests.RunAll(); CombThermalTests.RunAll(); ObservationSelectionTests.RunAll();
            var audit = ActionButtonAudit.Run(); Check(audit.Errors == 0, audit.Text);
            Check(UnityEngine.Resources.Load<GameTimeUI>("GameTimeUI") != null, "common prefab loaded");
            var profile = UnityEngine.Resources.Load<GameTimeUIProfile>("GameTimeUIProfile");
            Check(profile.Find("MainMenu") != null && profile.Find("Apiary") != null && profile.Find("Observation") != null, "all screen profiles");
            File.WriteAllText("time-tests.txt", "PASS " + checks + " time/assertion checks; assembly, spatial, cell, geometry, equipment, thermal, observation selection and button audit passed.");
            UnifiedButtonTests.RunBatch();
        }
        catch (Exception e) { Debug.LogException(e); File.WriteAllText("time-tests.txt",e.ToString()); EditorApplication.Exit(1); }
    }
}
