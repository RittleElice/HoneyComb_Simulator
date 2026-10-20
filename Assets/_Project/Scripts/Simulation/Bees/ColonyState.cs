using System.Collections.Generic;
namespace HoneyComb.Simulation.Bees
{
    public sealed class ColonyState
    {
        private readonly BeeView bees;
        public string Id { get; }
        public string HomeHiveId { get; }
        public IReadOnlyList<BeeState> Bees { get; }
        internal ColonyState(string id,string hiveId,BeeStore store) { Id=id;HomeHiveId=hiveId;bees=new BeeView(store);Bees=bees; }
        internal void Add(BeeState bee) => bees.Add(bee);
    }
}
