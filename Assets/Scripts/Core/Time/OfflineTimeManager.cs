using System;

/// <summary>UTC accounting included only in complete world checkpoints.</summary>
public sealed class OfflineTimeManager
{
    private DateTime? suspendedUtc;
    private long remainderTicks;
    private long highWaterUtcTicks;
    public long RemainderTicks => remainderTicks;
    public long HighWaterUtcTicks => highWaterUtcTicks;
    public long? SuspendedUtcTicks => suspendedUtc?.Ticks;
    internal static OfflineTimeManager Restore(long remainder, long highWater)
    {
        if (remainder < 0 || remainder >= 5 * TimeSpan.TicksPerSecond || highWater < 0 || highWater > DateTime.MaxValue.Ticks)
            throw new ArgumentOutOfRangeException();
        return new OfflineTimeManager { remainderTicks = remainder, highWaterUtcTicks = highWater };
    }
    public bool ClockMovedBackwards { get; private set; }
    public void Suspend(DateTime utc)
    {
        RequireUtc(utc);
        if (!suspendedUtc.HasValue)
            suspendedUtc = new DateTime(Math.Max(utc.Ticks, highWaterUtcTicks), DateTimeKind.Utc);
    }
    public long Resume(DateTime utc)
    {
        RequireUtc(utc);
        if (!suspendedUtc.HasValue) return 0;
        long anchor = suspendedUtc.Value.Ticks;
        long elapsed = utc.Ticks - anchor;
        suspendedUtc = null; // Always close this interval; never accidentally count subsequent online play.
        highWaterUtcTicks = Math.Max(highWaterUtcTicks, Math.Max(anchor, utc.Ticks));
        if (elapsed < 0) { ClockMovedBackwards = true; return 0; }
        long ticks = checked(elapsed + remainderTicks);
        long perMinute = 5 * TimeSpan.TicksPerSecond;
        remainderTicks = ticks % perMinute;
        return ticks / perMinute;
    }
    private static void RequireUtc(DateTime utc) { if (utc.Kind != DateTimeKind.Utc) throw new ArgumentException("UTC required."); }
}

/// <summary>Adaptive interval policy used by the complete-world checkpoint writer.</summary>
public sealed class CheckpointPolicy
{
    public double IntervalSeconds { get; private set; } = 30;
    public void ObserveWriteCost(double seconds)
    {
        if (double.IsNaN(seconds) || double.IsInfinity(seconds) || seconds < 0) throw new ArgumentOutOfRangeException(nameof(seconds));
        // Target <= 5% write overhead, bounded 10..120 seconds; smooth disk-cost changes.
        double target = Math.Max(10, Math.Min(120, seconds * 20));
        IntervalSeconds = IntervalSeconds * .75 + target * .25;
    }
    public bool IsDue(double secondsSinceLastSuccessfulWrite) => secondsSinceLastSuccessfulWrite >= IntervalSeconds;
}
