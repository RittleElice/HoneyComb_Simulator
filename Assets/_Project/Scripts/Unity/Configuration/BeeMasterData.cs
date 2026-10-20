using HoneyComb.Simulation.Bees;
using UnityEngine;
namespace HoneyComb.Unity.Configuration
{
    [System.Serializable]
    public sealed class BeeMasterData
    {
        public BeeCaste defaultCaste=BeeCaste.Worker;
        public BeeDevelopment defaultDevelopment=BeeDevelopment.Adult;
        [Range(0,100)] public float initialHealth=100;
        [Header("Development duration (hours) / copied on Play")]
        public DevelopmentMasterData worker=new DevelopmentMasterData(72,132,300);
        public DevelopmentMasterData queen=new DevelopmentMasterData(72,120,192);
        public DevelopmentMasterData drone=new DevelopmentMasterData(72,156,348);
        [Min(0)] public double developmentPerHour=1;
        [Tooltip("Prototype laying only: ignores food, season and mating. Enable a queen in the debug panel as well.")]
        public bool automaticQueenLaying=false;
        [Min(0)] public int eggsPerQueenHour=1;
        public BeeLifecycleSettings CreateLifecycle()
        {
            if(worker==null || queen==null || drone==null) throw new System.ArgumentException("Bee development master data is missing.");
            return new BeeLifecycleSettings(worker.Create(),queen.Create(),drone.Create(),developmentPerHour,automaticQueenLaying,eggsPerQueenHour,initialHealth);
        }
        public BeeSpawnSettings CreateSettings() => new BeeSpawnSettings(defaultCaste,defaultDevelopment,initialHealth);
    }
    [System.Serializable]
    public sealed class DevelopmentMasterData
    {
        [Min(0.01f)] public double eggHours,larvaHours,pupaHours;
        public DevelopmentMasterData(double egg,double larva,double pupa) { eggHours=egg;larvaHours=larva;pupaHours=pupa; }
        public BeeDevelopmentRule Create() => new BeeDevelopmentRule(eggHours,larvaHours,pupaHours);
    }
}
