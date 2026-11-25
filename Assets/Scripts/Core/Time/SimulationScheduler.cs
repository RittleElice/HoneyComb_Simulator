using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

/// <summary>Participants run in registration order on the calling context (Unity main thread).
/// Each participant must yield/chunk expensive work. A failed partially-mutating tick is terminal:
/// no retry, no clock commit, no saving until the complete world can be restored.</summary>
public interface IGameMinuteParticipant
{
    Task CalculateMinuteAsync(long nextMinute, CancellationToken cancellation);
}
public interface ISlicedGameMinuteParticipant : IGameMinuteParticipant
{
    Task CalculateMinuteSlicedAsync(long nextMinute, CancellationToken cancellation, SimulationSlice slice);
}

// Cooperatively yields on the caller context. Never reorders or retries simulation work.
public sealed class SimulationSlice
{
    readonly Func<Task> yieldFrame, waitUntilForeground;
    readonly CancellationToken cancellation;
    readonly double budget;
    readonly Stopwatch watch = Stopwatch.StartNew();
    public double WaitMilliseconds { get; private set; }
    public SimulationSlice(Func<Task> yieldFrame, Func<Task> waitUntilForeground, CancellationToken cancellation, double budget)
    { this.yieldFrame=yieldFrame; this.waitUntilForeground=waitUntilForeground; this.cancellation=cancellation; this.budget=budget; }
    public Task CheckpointAsync()
    {
        cancellation.ThrowIfCancellationRequested();
        return watch.Elapsed.TotalMilliseconds >= budget ? YieldAsync() : Task.CompletedTask;
    }
    async Task YieldAsync()
    {
        long start=Stopwatch.GetTimestamp();
        try { await yieldFrame(); await waitUntilForeground(); cancellation.ThrowIfCancellationRequested(); }
        finally { WaitMilliseconds+=(Stopwatch.GetTimestamp()-start)*1000.0/Stopwatch.Frequency; watch.Restart(); }
    }

}

public sealed class SimulationScheduler
{
#if UNITY_EDITOR
    // Observation only: never serialize, yield, or expose an unfinished minute.
    public static event Action<GameClock> ObservationBoundary;
    void PublishObservation()
    {
        var handlers=ObservationBoundary;
        if(handlers==null)return;
        foreach(Action<GameClock> observer in handlers.GetInvocationList())
            try { observer(clock); } catch(Exception e) { UnityEngine.Debug.LogException(e); }
    }
#endif
    private readonly GameClock clock;
    private readonly List<IGameMinuteParticipant> participants = new List<IGameMinuteParticipant>();
    private int running;
    public bool IsInTick { get; private set; }
    public int ParticipantCount => participants.Count;
    public bool ContainsOnly(IGameMinuteParticipant participant)
        => participant != null && participants.Count == 1 && ReferenceEquals(participants[0], participant);
    public bool ContainsThermalAndWeather(IGameMinuteParticipant thermal, IGameMinuteParticipant weather)
        => thermal != null && weather != null && participants.Count == 2 && ReferenceEquals(participants[0], thermal) && ReferenceEquals(participants[1], weather);
    public bool ContainsCycle(IGameMinuteParticipant thermal, IGameMinuteParticipant weather, IGameMinuteParticipant bees)
        => participants.Count == 3 && ReferenceEquals(participants[0], thermal) && ReferenceEquals(participants[1], weather) && ReferenceEquals(participants[2], bees);
    public double SliceBudgetMilliseconds { get; set; } = 2;
    public Func<Task> CheckpointAsync { get; set; }
    public bool IsRunning => Volatile.Read(ref running) != 0;
    public Exception Fault { get; private set; }
    public long Completed { get; private set; }
    public long Total { get; private set; }
    public SimulationScheduler(GameClock clock) { this.clock = clock ?? throw new ArgumentNullException(nameof(clock)); }
    public void Register(IGameMinuteParticipant participant)
    {
        if (IsRunning || Fault != null) throw new InvalidOperationException("Cannot change participants during calculation or after failure.");
        if (participant == null || participants.Contains(participant)) throw new ArgumentException(nameof(participant));
        participants.Add(participant);
    }
    public async Task RunToAsync(long target, Func<Task> yieldFrame, Func<Task> waitUntilForeground, CancellationToken cancellation, Func<bool> stopBeforeNextTick = null)
    {
        if (Interlocked.CompareExchange(ref running, 1, 0) != 0) throw new InvalidOperationException("A tick operation is already running.");
        bool tickStarted = false;
        try
        {
            if (Fault != null) throw new InvalidOperationException("World requires full recovery.", Fault);
            if (target < clock.CurrentGameMinute) throw new ArgumentOutOfRangeException(nameof(target), "Rewind requires a complete world snapshot.");
            Completed = 0; Total = target - clock.CurrentGameMinute;

            if (!(SliceBudgetMilliseconds > 0) || double.IsInfinity(SliceBudgetMilliseconds)) throw new InvalidOperationException("Invalid slice budget.");
#if UNITY_EDITOR
            PublishObservation();
#endif
            var slice = new SimulationSlice(yieldFrame, waitUntilForeground, cancellation, SliceBudgetMilliseconds);
            while (clock.CurrentGameMinute < target)
            {
                if (stopBeforeNextTick?.Invoke() == true) break;
                cancellation.ThrowIfCancellationRequested();
                await waitUntilForeground();
                cancellation.ThrowIfCancellationRequested();
                if (stopBeforeNextTick?.Invoke() == true) break;
                long next = checked(clock.CurrentGameMinute + 1);
                tickStarted = true; IsInTick = true;
                foreach (var participant in participants)
                {
                    await slice.CheckpointAsync();
                    if (participant is ISlicedGameMinuteParticipant sliced)
                        await sliced.CalculateMinuteSlicedAsync(next, cancellation, slice);
                    else await participant.CalculateMinuteAsync(next, cancellation);
                }
                clock.Commit(next); // Never publish an unfinished minute.
                tickStarted = false; IsInTick = false;
                Completed++;
#if UNITY_EDITOR
                PublishObservation();
#endif
                if (CheckpointAsync != null) await CheckpointAsync();
                await slice.CheckpointAsync();
            }
        }
        catch (OperationCanceledException e) { if (tickStarted) Fault = e; throw; }
        catch (Exception e) { Fault = e; throw; }
        finally { IsInTick = false; Volatile.Write(ref running, 0); }
    }
}
