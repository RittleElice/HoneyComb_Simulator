using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.EventSystems;

[DefaultExecutionOrder(-1000)]
public sealed class GameTimeRuntime : MonoBehaviour
{
    public GameClock Clock { get; private set; } = new GameClock();
    public SimulationScheduler Scheduler { get; private set; }
    public static bool InputBlocked => GameBootstrap.Instance != null && GameBootstrap.Instance.TimeSystem != null && GameBootstrap.Instance.TimeSystem.Blocked;
    public bool Blocked => suspended || operation || pendingMinutes > 0 || failure != null || (GameBootstrap.Instance?.SaveSystem?.IsLoading ?? false);
    public bool HasWorld { get; private set; }
    public bool DurableRecoveryAvailable => GameBootstrap.Instance?.SaveSystem?.CanSnapshot ?? false;
    public string Failure => failure;
    public string Status => (GameBootstrap.Instance?.SaveSystem?.IsLoading ?? false) ? "저장 불러오는 중…" : failure != null ? "시간 계산 실패 — 재시작 필요" : operation ? $"시간 계산 중 {Scheduler.Completed:N0} / {Scheduler.Total:N0}분" : "";
    private OfflineTimeManager offline = new OfflineTimeManager();
    private CancellationTokenSource lifetime = new CancellationTokenSource();
    private TaskCompletionSource<bool> foreground;
    private readonly List<TaskCompletionSource<bool>> frameWaits = new List<TaskCompletionSource<bool>>();
    private bool suspended, paused, unfocused, operation;
    private long pendingMinutes, operationTarget;
    private double nextTick;
    private string failure;
    private float speed = 1;
    private readonly Dictionary<EventSystem, bool> lockedEvents = new Dictionary<EventSystem, bool>();

    private void Awake() { Scheduler = new SimulationScheduler(Clock); nextTick = Time.realtimeSinceStartupAsDouble + 5; }
    public void BeginWorld(GameClock clock)
    {
        if (Blocked || Scheduler.IsRunning) throw new InvalidOperationException("Cannot replace a calculating world.");
        Clock = clock; Scheduler = new SimulationScheduler(clock); offline = new OfflineTimeManager();
        Scheduler.CheckpointAsync = () => GameBootstrap.Instance?.SaveSystem?.SaveBoundaryAsync(false) ?? Task.CompletedTask;
        pendingMinutes = 0; operationTarget = 0; HasWorld = true; nextTick = Time.realtimeSinceStartupAsDouble + 5 / speed;
    }
    private void Update()
    {
        RefreshInputLock();
        if (!HasWorld || suspended || operation || failure != null) return;
        if (pendingMinutes > 0)
        {
            long count = pendingMinutes; pendingMinutes = 0;
            try { StartOperation(checked(Clock.CurrentGameMinute + count), true); }
            catch (Exception e) { Fail(e); }
        }
        else if (Time.realtimeSinceStartupAsDouble >= nextTick)
        {
            try { StartOperation(checked(Clock.CurrentGameMinute + 1)); }
            catch (Exception e) { Fail(e); }
        }
    }
    private async void StartOperation(long target, bool showProgress = false)
    {
        if (operation) return;
        operation = true; operationTarget = target;
        try
        {
            // Bulk work paints progress first. Instant live ticks must not flash an overlay or cancel a gesture.
            if (showProgress)
            {
                RefreshInputLock(); await NextFrame();
                if (GameBootstrap.Instance?.SaveSystem != null) await GameBootstrap.Instance.SaveSystem.SaveBoundaryAsync(true);
            }
            Task work = Scheduler.RunToAsync(target, NextFrame, WaitForForeground, lifetime.Token);
            if (!work.IsCompleted) RefreshInputLock();
            await work;
            if (showProgress && GameBootstrap.Instance?.SaveSystem != null) await GameBootstrap.Instance.SaveSystem.SaveBoundaryAsync(true);
        }
        catch (OperationCanceledException) { if (!lifetime.IsCancellationRequested) Fail(new Exception("Unexpected cancellation")); }
        catch (Exception e) { Fail(e); }
        finally
        {
            operation = false; operationTarget = 0;
            // No backlog: a full fresh interval after completion, independent of Unity timeScale.
            nextTick = Time.realtimeSinceStartupAsDouble + 5 / speed;
            RefreshInputLock();
        }
    }
    private void Fail(Exception e) { failure = e.ToString(); Debug.LogException(e); RefreshInputLock(); }
    private Task NextFrame()
    {
        var completion = new TaskCompletionSource<bool>();
        frameWaits.Add(completion);
        StartCoroutine(NextFrameRoutine(completion));
        return completion.Task;
    }
    private IEnumerator NextFrameRoutine(TaskCompletionSource<bool> completion)
    {
        yield return null;
        frameWaits.Remove(completion);
        if (lifetime.IsCancellationRequested) completion.TrySetCanceled(); else completion.TrySetResult(true);
    }
    private Task WaitForForeground() => suspended ? foreground.Task : Task.CompletedTask;
    private void OnApplicationPause(bool value) { paused = value; RefreshSuspension(); }
    private void OnApplicationFocus(bool value)
    {
#if !UNITY_EDITOR
        // Editor utility windows must remain usable without pausing the game.
        unfocused = !value; RefreshSuspension();
#endif
    }
    private void RefreshSuspension()
    {
        bool value = paused || unfocused;
        if (value == suspended) return;
        suspended = value;
        if (value)
        {
            foreground = new TaskCompletionSource<bool>();
            if (HasWorld) offline.Suspend(DateTime.UtcNow);
            GameBootstrap.Instance?.SaveSystem?.SaveSynchronously();
        }
        else
        {
            try { if (HasWorld) pendingMinutes = checked(pendingMinutes + offline.Resume(DateTime.UtcNow)); }
            catch (Exception e) { Fail(e); }
            nextTick = Time.realtimeSinceStartupAsDouble + 5 / speed;
            foreground?.TrySetResult(true);
        }
        RefreshInputLock();
    }
    internal WorldSaveSnapshot CaptureTimeline(GameWorld world)
    {
        if (!ReferenceEquals(world.Clock, Clock) || Scheduler.IsInTick || Scheduler.Fault != null || failure != null || Scheduler.ParticipantCount != 0)
            throw new InvalidOperationException("전체 상태 저장을 지원하지 않는 시뮬레이션이 있거나 계산 중입니다.");
        return new WorldSaveSnapshot {
            World = world,
            UtcTicks = Math.Max(offline.SuspendedUtcTicks ?? DateTime.UtcNow.Ticks, offline.HighWaterUtcTicks),
            RemainderTicks = offline.RemainderTicks, HighWaterUtcTicks = offline.HighWaterUtcTicks,
            TargetMinute = checked(Math.Max(Clock.CurrentGameMinute, operationTarget) + pendingMinutes)
        };
    }
    internal Task WaitForResumeAsync() => WaitForForeground();
    internal static (OfflineTimeManager offline, long minutes) PrepareResume(WorldSaveSnapshot snapshot)
    {
        var restored = OfflineTimeManager.Restore(snapshot.RemainderTicks, snapshot.HighWaterUtcTicks);
        restored.Suspend(new DateTime(snapshot.UtcTicks, DateTimeKind.Utc));
        long elapsed = restored.Resume(DateTime.UtcNow);
        long minutes = checked(snapshot.TargetMinute - snapshot.World.CurrentGameMinute + elapsed);
        checked { long finalMinute = snapshot.World.CurrentGameMinute + minutes; }
        return (restored, minutes);
    }
    internal void ApplyResume(OfflineTimeManager restored, long minutes)
    {
        offline = restored; pendingMinutes = minutes; RefreshInputLock();
    }
    private void RefreshInputLock()
    {
        // Lock every loaded EventSystem: additive scene loading can temporarily create more than one.
        // Covers keyboard Submit as well as pointers without altering catalog button state.
        if (!Blocked) { RestoreEvents(); return; }
        foreach (var system in FindObjectsByType<EventSystem>())
        {
            if (lockedEvents.ContainsKey(system)) continue;
            lockedEvents.Add(system, system.enabled);
            system.enabled = false;
        }
    }
    private void RestoreEvents()
    {
        foreach (var entry in lockedEvents)
            if (entry.Key != null) entry.Key.enabled = entry.Value;
        lockedEvents.Clear();
    }
    private void OnDestroy()
    {
        lifetime.Cancel(); foreground?.TrySetCanceled();
        foreach (var wait in frameWaits.ToArray()) wait.TrySetCanceled();
        frameWaits.Clear(); RestoreEvents();
    }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    public float DeveloperSpeed => speed;
    public void SetDeveloperSpeed(float value)
    {
        if (float.IsNaN(value) || float.IsInfinity(value) || value < 1 || value > 1000) throw new ArgumentOutOfRangeException(nameof(value));
        speed = value; nextTick = Time.realtimeSinceStartupAsDouble + 5 / speed;
    }
    public void JumpTo(long year, int month, int day, int hour, int minute)
        => JumpToMinute(GameClock.ToMinute(year, month, day, hour, minute));
    public void JumpTo(long year, int day, int hour, int minute)
        => JumpToMinute(GameClock.ToMinute(year, day, hour, minute));
    public void JumpBy(long years, long days, long hours, long minutes)
        => JumpToMinute(GameClock.AddDuration(Clock.CurrentGameMinute, years, days, hours, minutes));
    private void JumpToMinute(long target)
    {
        if (!HasWorld || Blocked || pendingMinutes > 0) throw new InvalidOperationException("Time system is busy or no game is active.");
        if (target < Clock.CurrentGameMinute) throw new ArgumentException("과거로 이동하려면 전체 상태 복원이 필요합니다.");
        StartOperation(target, true);
    }
#endif
}
