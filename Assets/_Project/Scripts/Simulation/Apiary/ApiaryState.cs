using System;
using System.Collections.Generic;
using HoneyComb.Simulation.Hive;
namespace HoneyComb.Simulation.Apiary
{
    public sealed class ApiaryState
    {
        private readonly List<HiveState> hives = new List<HiveState>();
        private long nextHiveNumber = 1;
        public string Id { get; }
        public string Name { get; }
        public int HiveCapacity { get; }
        public int HiveCount => hives.Count;
        public IReadOnlyList<HiveState> Hives { get; }
        public ApiaryState(string id, string name, int capacity, int initialHiveCount)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Apiary ID is required.", nameof(id));
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Apiary name is required.", nameof(name));
            if (capacity < 0) throw new ArgumentOutOfRangeException(nameof(capacity));
            if (initialHiveCount < 0 || initialHiveCount > capacity) throw new ArgumentOutOfRangeException(nameof(initialHiveCount));
            Id = id.Trim(); Name = name.Trim(); HiveCapacity = capacity; Hives = hives.AsReadOnly();
            for(int i=0; i<initialHiveCount; i++) TryAddHive(out _);
        }
        public bool TryAddHive(out HiveState hive)
        {
            hive = null;
            if (HiveCount >= HiveCapacity || nextHiveNumber == long.MaxValue) return false;
            hive = new HiveState(Id + "/hive/" + nextHiveNumber++);
            hives.Add(hive); return true;
        }
        public bool RemoveHive(string hiveId)
        {
            int index = hives.FindIndex(hive => hive.Id == hiveId);
            if(index < 0) return false;
            hives.RemoveAt(index); return true;
        }
    }
}
