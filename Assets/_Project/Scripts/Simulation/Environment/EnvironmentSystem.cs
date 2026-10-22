using System;
namespace HoneyComb.Simulation.Environment
{
    public sealed class EnvironmentSystem
    {
        private readonly EnvironmentState initialConditions;
        public EnvironmentState Current { get; private set; }
        public long LastUpdatedHour { get; private set; }
        public long ProcessedTicks { get; private set; }
        public EnvironmentSystem(EnvironmentState initialConditions, long initialHour)
        {
            if (initialHour < 0) throw new ArgumentOutOfRangeException(nameof(initialHour));
            this.initialConditions = initialConditions;
            Current = initialConditions;
            LastUpdatedHour = initialHour;
        }
        public void TickHour(long hour)
        {
            if (LastUpdatedHour == long.MaxValue || hour != LastUpdatedHour + 1)
                throw new ArgumentOutOfRangeException(nameof(hour), "Environment must receive each hour exactly once, in order.");
            // Fixed external input for v0.1. A future weather model replaces this calculation.
            Current = initialConditions;
            LastUpdatedHour = hour;
            ProcessedTicks++;
        }
    }
}
