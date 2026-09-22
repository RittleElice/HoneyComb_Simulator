using System;
namespace HoneyComb.Simulation.Bees
{
    public sealed class BeeDevelopmentRule
    {
        public double EggEnd { get; }
        public double LarvaEnd { get; }
        public double AdultStart { get; }
        public BeeDevelopmentRule(double eggHours,double larvaHours,double pupaHours)
        {
            if(!Positive(eggHours)||!Positive(larvaHours)||!Positive(pupaHours)||!Positive(eggHours+larvaHours+pupaHours))
                throw new ArgumentOutOfRangeException(nameof(eggHours),"Development durations must be finite and positive.");
            EggEnd=eggHours;LarvaEnd=eggHours+larvaHours;AdultStart=LarvaEnd+pupaHours;
        }
        private static bool Positive(double value) => value>0 && !double.IsInfinity(value) && !double.IsNaN(value);
        public BeeDevelopment Stage(double progress) => progress<EggEnd ? BeeDevelopment.Egg : progress<LarvaEnd ? BeeDevelopment.Larva : progress<AdultStart ? BeeDevelopment.Pupa : BeeDevelopment.Adult;
        public double Start(BeeDevelopment stage) => stage==BeeDevelopment.Egg ? 0 : stage==BeeDevelopment.Larva ? EggEnd : stage==BeeDevelopment.Pupa ? LarvaEnd : AdultStart;
    }
    public sealed class BeeLifecycleSettings
    {
        private readonly BeeDevelopmentRule worker,queen,drone;
        public double ProgressPerHour { get; }
        public int EggsPerQueenHour { get; }
        public bool AutomaticLaying { get; }
        public float NewbornHealth { get; }
        public BeeLifecycleSettings(BeeDevelopmentRule worker,BeeDevelopmentRule queen,BeeDevelopmentRule drone,
            double progressPerHour=1,bool automaticLaying=false,int eggsPerQueenHour=1,float newbornHealth=100)
        {
            this.worker=worker??throw new ArgumentNullException(nameof(worker));this.queen=queen??throw new ArgumentNullException(nameof(queen));this.drone=drone??throw new ArgumentNullException(nameof(drone));
            if(double.IsNaN(progressPerHour)||double.IsInfinity(progressPerHour)||progressPerHour<0) throw new ArgumentOutOfRangeException(nameof(progressPerHour));
            if(eggsPerQueenHour<0) throw new ArgumentOutOfRangeException(nameof(eggsPerQueenHour));
            if(float.IsNaN(newbornHealth)||float.IsInfinity(newbornHealth)||newbornHealth<0||newbornHealth>100) throw new ArgumentOutOfRangeException(nameof(newbornHealth));
            ProgressPerHour=progressPerHour;AutomaticLaying=automaticLaying;EggsPerQueenHour=eggsPerQueenHour;NewbornHealth=newbornHealth;
        }
        public BeeDevelopmentRule For(BeeCaste caste) => caste==BeeCaste.Worker ? worker : caste==BeeCaste.Queen ? queen : caste==BeeCaste.Drone ? drone : throw new ArgumentOutOfRangeException(nameof(caste));
        // Compatibility defaults for pure model callers. Unity supplies master data explicitly.
        public static BeeLifecycleSettings Default => new BeeLifecycleSettings(new BeeDevelopmentRule(72,132,300),new BeeDevelopmentRule(72,120,192),new BeeDevelopmentRule(72,156,348));
    }
}
