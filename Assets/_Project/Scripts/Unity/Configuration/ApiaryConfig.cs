using HoneyComb.Simulation.Apiary;
using UnityEngine;
namespace HoneyComb.Unity.Configuration
{
    [CreateAssetMenu(menuName="HoneyComb/Apiary Config",fileName="ApiaryConfig")]
    public sealed class ApiaryConfig : ScriptableObject
    {
        public string apiaryId="apiary-1";
        public string apiaryName="Test Apiary";
        [Min(1)] public int width=5;
        [Min(1)] public int height=4;
        [Tooltip("Placed from (0,0), left to right, bottom to top. Cannot exceed Width x Height.")]
        [Min(0)] public int initialHiveCount=1;
        [Header("Frame cells per face (copied when Play starts)")]
        [Min(1)] public int frameCellWidth=90;
        [Min(1)] public int frameCellHeight=40;
        public ApiaryState CreateState() => new ApiaryState(apiaryId,apiaryName,width,height,initialHiveCount,frameCellWidth,frameCellHeight);
    }
}
