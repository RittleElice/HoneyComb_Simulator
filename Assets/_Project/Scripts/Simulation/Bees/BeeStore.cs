using System;
using System.Collections;
using System.Collections.Generic;
namespace HoneyComb.Simulation.Bees
{
    // Canonical value storage. Views retain only IDs and resolve a fresh snapshot on read.
    public sealed class BeeStore
    {
        private BeeState[] values = Array.Empty<BeeState>();
        private readonly Dictionary<long,int> positions = new Dictionary<long,int>();
        public int Count { get; private set; }
        public bool TryGet(long id,out BeeState bee)
        { if(positions.TryGetValue(id,out int index)) { bee=values[index];return true; } bee=default;return false; }
        internal void Add(BeeState bee)
        {
            if(Count==values.Length) Array.Resize(ref values,Math.Max(4,checked(Count*2)));
            positions.Add(bee.Id,Count);values[Count++]=bee;
        }
        internal void Update(BeeState bee) => values[positions[bee.Id]]=bee;
    }
    internal sealed class BeeView : IReadOnlyList<BeeState>
    {
        private readonly List<long> ids=new List<long>();
        private readonly BeeStore store;
        public BeeView(BeeStore store) { this.store=store; }
        public int Count => ids.Count;
        public BeeState this[int index] { get { store.TryGet(ids[index],out var bee);return bee; } }
        internal void Add(BeeState bee) => ids.Add(bee.Id);
        internal void Remove(BeeState bee) => ids.Remove(bee.Id);
        public IEnumerator<BeeState> GetEnumerator() { foreach(long id in ids) { store.TryGet(id,out var bee);yield return bee; } }
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
