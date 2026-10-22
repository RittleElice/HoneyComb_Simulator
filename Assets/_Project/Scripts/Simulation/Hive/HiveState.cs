using System;
namespace HoneyComb.Simulation.Hive
{
    public enum HiveStatus { Active }
    public sealed class HiveState
    {
        public string Id { get; }
        public HiveStatus Status { get; } = HiveStatus.Active;
        // Active denotes an installed hive, not a living colony.
        internal HiveState(string id) { Id = id; }
    }
}
