using HoneyComb.Simulation.Apiary;
using UnityEngine;
namespace HoneyComb.Unity.Configuration
{
    [CreateAssetMenu(menuName = "HoneyComb/Apiary Config", fileName = "ApiaryConfig")]
    public sealed class ApiaryConfig : ScriptableObject
    {
        [Tooltip("Stable unique ID. Future apiaries must each have a different ID.")]
        public string apiaryId = "apiary-1";
        public string apiaryName = "Test Apiary";
        [Min(0)] public int hiveCapacity = 10;
        [Min(0)] public int initialHiveCount = 1;
        public ApiaryState CreateState() => new ApiaryState(apiaryId, apiaryName, hiveCapacity, initialHiveCount);
    }
}
