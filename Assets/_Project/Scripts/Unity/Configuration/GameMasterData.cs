using UnityEngine;
namespace HoneyComb.Unity.Configuration
{
    [CreateAssetMenu(menuName = "HoneyComb/Game Master Data", fileName = "GameMasterData")]
    public sealed class GameMasterData : ScriptableObject
    {
        [Header("Time — initial values (applied when Play starts)")]
        [Tooltip("Real seconds for one game minute. Default: 5 seconds.")]
        [Min(0.001f)] public float realSecondsPerMinute = 5f;
        [Min(0)] public int initialElapsedHours = 0;
        [Range(0, 59)] public int initialMinuteOfHour = 0;
        public long InitialElapsedMinutes => (long)initialElapsedHours * 60 + initialMinuteOfHour;
        public bool startAutomatic = true;
        [Header("Environment master data")]
        public EnvironmentConfig environmentConfig;
        public WeatherConfig weatherConfig;
        [Header("Apiary master data")]
        public ApiaryConfig apiaryConfig;
        [Header("Bee creation defaults")]
        public BeeMasterData beeDefaults=new BeeMasterData();
        [Header("Development controls")]
        [Min(1)] public int defaultAdvanceHours = 24;
        [Min(1)] public int maxAdvanceHours = 876000;
        [Tooltip("Work limit per rendered frame; remaining ticks stay queued.")]
        [Min(1)] public int maxTicksPerFrame = 240;
        public bool showTimeDebugPanel = true;
        public bool IsValid => realSecondsPerMinute > 0 && !float.IsNaN(realSecondsPerMinute)
            && !float.IsInfinity(realSecondsPerMinute) && initialElapsedHours >= 0 && initialMinuteOfHour >= 0 && initialMinuteOfHour < 60
            && defaultAdvanceHours > 0 && maxAdvanceHours >= defaultAdvanceHours && maxTicksPerFrame > 0;
    }
}
