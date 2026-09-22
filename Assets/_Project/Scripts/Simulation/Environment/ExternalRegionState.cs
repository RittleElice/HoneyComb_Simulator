using System;
namespace HoneyComb.Simulation.Environment
{
    // A logical destination, separate from weather conditions. No resource simulation yet.
    public sealed class ExternalRegionState
    {
        public string Id { get; }
        public string Name { get; }
        public ExternalRegionState(string id,string name)
        {
            if(string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(name)) throw new ArgumentException("External region ID/name are required.");
            Id=id;Name=name;
        }
    }
}
