using System;
namespace HoneyComb.Simulation.Time
{
    public sealed class SimulationClock
    {
        public const int HoursPerDay = 24;
        public const int DaysPerYear = 365;
        public long TotalHours { get; private set; }
        public double AccumulatedSeconds { get; private set; }
        public long PendingTicks { get; private set; }
        public bool Automatic { get; set; }
        public double SecondsPerTick { get; }
        public event Action<long> Tick;
        public SimulationClock(long initialHour, double secondsPerTick, bool automatic)
        {
            if (initialHour < 0) throw new ArgumentOutOfRangeException(nameof(initialHour));
            if (double.IsNaN(secondsPerTick) || double.IsInfinity(secondsPerTick) || secondsPerTick <= 0)
                throw new ArgumentOutOfRangeException(nameof(secondsPerTick));
            TotalHours = initialHour; SecondsPerTick = secondsPerTick; Automatic = automatic;
        }
        public void QueueTicks(long count)
        {
            if (count <= 0 || count > long.MaxValue - TotalHours - PendingTicks)
                throw new ArgumentOutOfRangeException(nameof(count));
            PendingTicks += count;
        }
        public int Update(double elapsedSeconds, int budget)
        {
            if (elapsedSeconds < 0 || double.IsNaN(elapsedSeconds) || double.IsInfinity(elapsedSeconds))
                throw new ArgumentOutOfRangeException(nameof(elapsedSeconds));
            if (budget <= 0) throw new ArgumentOutOfRangeException(nameof(budget));
            // Manual fast-forward uses its own queue. It does not accumulate automatic time.
            bool fastForwarding = PendingTicks > 0;
            if (Automatic && !fastForwarding) AccumulatedSeconds += elapsedSeconds;
            int processed = 0;
            while (processed < budget)
            {
                if (PendingTicks > 0) PendingTicks--;
                else if (!fastForwarding && Automatic && AccumulatedSeconds >= SecondsPerTick)
                    AccumulatedSeconds -= SecondsPerTick;
                else break;
                TotalHours = checked(TotalHours + 1);
                Tick?.Invoke(TotalHours);
                processed++;
            }
            return processed;
        }
    }
}
