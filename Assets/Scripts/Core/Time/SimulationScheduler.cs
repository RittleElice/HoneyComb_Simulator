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
public sealed class SimulationScheduler
{
    private readonly GameClock clock;
    private readonly List<IGameMinuteParticipant> participants = new List<IGameMinuteParticipant>();
    private int running;
    public bool IsInTick { get; private set; }
    public int ParticipantCount => participants.Count;
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
    public async Task RunToAsync(long target, Func<Task> yieldFrame, Func<Task> waitUntilForeground, CancellationToken cancellation)
    {
        if (Interlocked.CompareExchange(ref running, 1, 0) != 0) throw new InvalidOperationException("A tick operation is already running.");
        bool tickStarted = false;
        try
        {
            if (Fault != null) throw new InvalidOperationException("World requires full recovery.", Fault);
            if (target < clock.CurrentGameMinute) throw new ArgumentOutOfRangeException(nameof(target), "Rewind requires a complete world snapshot.");
            Completed = 0; Total = target - clock.CurrentGameMinute;
            var budget = Stopwatch.StartNew();
            while (clock.CurrentGameMinute < target)
            {
                cancellation.ThrowIfCancellationRequested();
                await waitUntilForeground();
                cancellation.ThrowIfCancellationRequested();
                long next = checked(clock.CurrentGameMinute + 1);
                tickStarted = true; IsInTick = true;
                foreach (var participant in participants)
                    await participant.CalculateMinuteAsync(next, cancellation);
                clock.Commit(next); // Never publish an unfinished minute.
                tickStarted = false; IsInTick = false;
                Completed++;
                if (CheckpointAsync != null) await CheckpointAsync();
                if (budget.ElapsedMilliseconds >= 4)
                {
                    await yieldFrame();
                    budget.Restart();
                }
            }
        }
        catch (OperationCanceledException e) { if (tickStarted) Fault = e; throw; }
        catch (Exception e) { Fault = e; throw; }
        finally { IsInTick = false; Volatile.Write(ref running, 0); }
    }
}
