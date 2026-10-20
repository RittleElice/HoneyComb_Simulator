using System;
using HoneyComb.Simulation.Hive;
namespace HoneyComb.Simulation.Bees
{
    public enum BeeCaste { Worker, Queen, Drone }
    public enum BeeSex { Female, Male }
    public enum BeeDevelopment { Egg, Larva, Pupa, Adult }
    public struct BeeState
    {
        public long Id { get; }
        public string ColonyId { get; }
        public BeeCaste Caste { get; }
        public BeeSex Sex => Caste==BeeCaste.Drone ? BeeSex.Male : BeeSex.Female;
        private BeeDevelopmentRule rule;
        public double DevelopmentProgress { get; private set; }
        public BeeDevelopment Development => rule.Stage(DevelopmentProgress);
        public double DevelopmentFraction => DevelopmentProgress/rule.AdultStart;
        public bool CanLayEggs { get; private set; }
        public long CreatedHour { get; }
        // Debug-injected larvae/pupae/adults have no known laying date.
        public long? LaidHour { get; }
        public long? AdultSinceHour { get; private set; }
        public float Health { get; }
        public bool Alive => true;
        public BeeLocation Location { get; private set; }
        internal void MoveTo(BeeLocation location) => Location=location;
        internal void SetLaying(bool enabled) => CanLayEggs=enabled;
        internal void Advance(double amount,long hour)
        {
            bool wasAdult=Development==BeeDevelopment.Adult;
            DevelopmentProgress=Math.Min(rule.AdultStart,DevelopmentProgress+amount);
            if(!wasAdult && Development==BeeDevelopment.Adult) AdultSinceHour=hour;
        }
        internal BeeState(long id,string colonyId,BeeCaste caste,BeeDevelopment development,float health,long hour,BeeLocation location,BeeDevelopmentRule rule)
        { this.rule=rule;DevelopmentProgress=rule.Start(development);CanLayEggs=false;Id=id;ColonyId=colonyId;Caste=caste;Health=health;CreatedHour=hour;Location=location;
          LaidHour=development==BeeDevelopment.Egg ? hour : (long?)null;
          AdultSinceHour=development==BeeDevelopment.Adult ? hour : (long?)null; }
    }
    public readonly struct BeeSpawnSettings
    {
        public BeeCaste Caste { get; }
        public BeeDevelopment Development { get; }
        public float Health { get; }
        public BeeSpawnSettings(BeeCaste caste,BeeDevelopment development,float health)
        {
            if(caste<BeeCaste.Worker || caste>BeeCaste.Drone || development<BeeDevelopment.Egg || development>BeeDevelopment.Adult)
                throw new ArgumentOutOfRangeException(nameof(caste));
            if(float.IsNaN(health) || float.IsInfinity(health) || health<0 || health>100) throw new ArgumentOutOfRangeException(nameof(health));
            Caste=caste;Development=development;Health=health;
        }
    }
}
