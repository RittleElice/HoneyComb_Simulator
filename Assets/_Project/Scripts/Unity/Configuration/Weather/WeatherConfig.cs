using System;
using HoneyComb.Simulation.Environment.Weather;
using UnityEngine;
namespace HoneyComb.Unity.Configuration
{
    [CreateAssetMenu(menuName="HoneyComb/Weather Config",fileName="WeatherConfig")]
    public sealed class WeatherConfig : ScriptableObject
    {
        [Tooltip("Ver.2.0 calibrated parameters. Cell keys preserve workbook traceability.")]
        public WeatherParameters parameters;
        [Tooltip("Native Unity baseline asset: 525600 minutes x 8 values.")]
        public WeatherBaselineData baselineData;
        public uint weatherSeed=2026101901;
        public uint windSeed=2026101902;
        public WeatherSimulator CreateSimulator(long startMinute)
        {
            if(parameters==null || baselineData==null)throw new ArgumentException("Missing weather parameters or baseline.");
            return new WeatherSimulator(parameters,baselineData.CreateBaseline(),weatherSeed,windSeed,startMinute);
        }
    }
}
