using UnityEngine;
namespace HoneyComb.Unity.Configuration
{
    [CreateAssetMenu(menuName = "HoneyComb/Game Master Data", fileName = "GameMasterData")]
    public sealed class GameMasterData : ScriptableObject
    {
        [Header("Time — initial values (applied when Play starts)")]
        [Tooltip("Real seconds for one game hour. 82.19178 is approximately 30 real days per game year.")]
        [Min(0.001f)] public float realSecondsPerTick = 1f;
        [Min(0)] public int initialElapsedHours = 0;
        public bool startAutomatic = true;
        [Header("Environment master data")]
        public EnvironmentConfig environmentConfig;
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
        public bool IsValid => realSecondsPerTick > 0 && !float.IsNaN(realSecondsPerTick)
            && !float.IsInfinity(realSecondsPerTick) && initialElapsedHours >= 0
            && defaultAdvanceHours > 0 && maxAdvanceHours >= defaultAdvanceHours && maxTicksPerFrame > 0;
    }
}
