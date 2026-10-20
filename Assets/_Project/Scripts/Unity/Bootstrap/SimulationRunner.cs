using System;
using System.Globalization;
using HoneyComb.Simulation.Time;
using HoneyComb.Simulation.Apiary;
using HoneyComb.Simulation.Environment;
using HoneyComb.Unity.Configuration;
using UnityEngine;
namespace HoneyComb.Unity.Bootstrap
{
    public sealed class SimulationRunner : MonoBehaviour
    {
        [SerializeField] private GameMasterData masterData;
        public SimulationClock Clock { get; private set; }
        public EnvironmentSystem Environment { get; private set; }
        public ApiaryState Apiary { get; private set; }
        private Vector2 debugScroll;
        private int budget, maxAdvance;
        private bool showPanel, suspended;
        private string input, error;
        private double lastTime;
        public void Configure(GameMasterData data) => masterData = data;
        private void Awake()
        {
            if (masterData == null || !masterData.IsValid)
            {
                Debug.LogError("Missing or invalid GameMasterData.", this); enabled = false; return;
            }
            if (masterData.environmentConfig == null)
            {
                Debug.LogError("Missing EnvironmentConfig. Run HoneyComb > Setup Environment System.", this);
                enabled = false; return;
            }
            try
            {
                Environment = new EnvironmentSystem(masterData.environmentConfig.CreateSnapshot(), masterData.initialElapsedHours);
            }
            catch (ArgumentOutOfRangeException exception)
            {
                Debug.LogError("Invalid environment master data: " + exception.Message, this);
                enabled = false; return;
            }
            if (masterData.apiaryConfig == null)
            {
                Debug.LogError("Missing ApiaryConfig. Run HoneyComb > Setup Apiary System.", this);
                enabled = false; return;
            }
            try { Apiary = masterData.apiaryConfig.CreateState(); }
            catch (ArgumentException exception)
            {
                Debug.LogError("Invalid apiary master data: " + exception.Message, this);
                enabled = false; return;
            }
            Clock = new SimulationClock(masterData.initialElapsedHours, masterData.realSecondsPerTick, masterData.startAutomatic);
            budget = masterData.maxTicksPerFrame; maxAdvance = masterData.maxAdvanceHours;
            showPanel = masterData.showTimeDebugPanel;
            input = masterData.defaultAdvanceHours.ToString(CultureInfo.InvariantCulture);
            lastTime = UnityEngine.Time.realtimeSinceStartupAsDouble;
        }
        private void OnEnable()
        {
            if (Clock == null) return;
            Clock.Tick += HandleHourTick;
            lastTime = UnityEngine.Time.realtimeSinceStartupAsDouble;
        }
        private void OnDisable()
        {
            if (Clock != null) Clock.Tick -= HandleHourTick;
        }
        private void HandleHourTick(long hour)
        {
            // One integration point: future systems get explicit order here,
            // rather than relying on event subscription order between objects.
            Environment.TickHour(hour);
        }
        private void Update()
        {
            double now = UnityEngine.Time.realtimeSinceStartupAsDouble;
            double elapsed = now - lastTime; lastTime = now;
            if (!suspended) Clock.Update(elapsed, budget);
        }
        private void OnApplicationPause(bool paused)
        {
            suspended = paused;
            lastTime = UnityEngine.Time.realtimeSinceStartupAsDouble;
        }
        private void OnApplicationFocus(bool focused)
        {
            suspended = !focused;
            lastTime = UnityEngine.Time.realtimeSinceStartupAsDouble;
        }
        public void AdvanceOneHour() => Queue(1);
        private void Queue(int hours)
        {
            try { Clock.QueueTicks(hours); error = ""; }
            catch (ArgumentOutOfRangeException) { error = "Requested time is too large."; }
        }
        private void OnGUI()
        {
            if (Clock == null || !showPanel) return;
            // Temporary development panel. Screen.safeArea excludes phone cutouts.
            Rect safe = Screen.safeArea;
            float scale = Mathf.Max(0.1f, Mathf.Min(safe.width / 600f, safe.height / 1040f));
            Matrix4x4 previous = GUI.matrix;
            GUI.matrix = Matrix4x4.TRS(new Vector3(safe.x, Screen.height - safe.yMax, 0), Quaternion.identity, Vector3.one * scale);
            var label = new GUIStyle(GUI.skin.label) { fontSize = 24, wordWrap = true };
            var button = new GUIStyle(GUI.skin.button) { fontSize = 24 };
            var field = new GUIStyle(GUI.skin.textField) { fontSize = 28 };
            GUILayout.BeginArea(new Rect(20, 20, 560, safe.height / scale - 40), GUI.skin.box);
            debugScroll = GUILayout.BeginScrollView(debugScroll, false, true);
            long total = Clock.TotalHours;
            GUILayout.Label("HONEYCOMB / TIME DEBUG", label);
            GUILayout.Space(12);
            GUILayout.Label($"Year {total / 8760 + 1} / Day {total / 24 % 365 + 1} / {total % 24:00}:00", label);
            GUILayout.Label($"Elapsed: {total:N0} hours\n1 tick = {Clock.SecondsPerTick:0.###} real seconds\nQueued: {Clock.PendingTicks:N0} hours", label);
            if (GUILayout.Button(Clock.Automatic ? "Pause automatic time" : "Resume automatic time", button, GUILayout.Height(64))) Clock.Automatic = !Clock.Automatic;
            GUI.enabled = Clock.PendingTicks == 0;
            if (GUILayout.Button("+1 hour (1 tick)", button, GUILayout.Height(64))) AdvanceOneHour();
            GUILayout.Label("Hours to advance (positive whole number)", label);
            input = GUILayout.TextField(input, 12, field, GUILayout.Height(56));
            if (GUILayout.Button("Advance entered hours", button, GUILayout.Height(64)))
            {
                if (int.TryParse(input, NumberStyles.None, CultureInfo.InvariantCulture, out int hours) && hours > 0 && hours <= maxAdvance) Queue(hours);
                else error = $"Enter a whole number from 1 to {maxAdvance:N0}.";
            }
            GUI.enabled = true;
            if (!string.IsNullOrEmpty(error)) GUILayout.Label(error, label);
            GUILayout.Space(20);
            GUILayout.Label("ENVIRONMENT DEBUG", label);
            var weather = Environment.Current;
            GUILayout.Label($"Temperature     {weather.TemperatureCelsius:0.0} °C\nHumidity           {weather.HumidityPercent:0.0} %\nPrecipitation     {weather.PrecipitationMmPerHour:0.0} mm/h\nWind                 {weather.WindSpeedMetersPerSecond:0.0} m/s\nDaylight             {weather.Daylight:0.00}", label);
            GUILayout.Label($"Environment ticks: {Environment.ProcessedTicks:N0}\nLast updated hour: {Environment.LastUpdatedHour:N0}", label);
            GUILayout.Space(20);
            GUILayout.Label("APIARY DEBUG", label);
            GUILayout.Label($"Apiary: {Apiary.Name}\nID: {Apiary.Id}\nHive capacity: {Apiary.HiveCapacity}\nHives: {Apiary.HiveCount}", label);
            GUI.enabled = Apiary.HiveCount < Apiary.HiveCapacity;
            if (GUILayout.Button("Add Hive", button, GUILayout.Height(64))) Apiary.TryAddHive(out _);
            GUI.enabled = Apiary.HiveCount > 0;
            if (GUILayout.Button("Remove Last Hive", button, GUILayout.Height(64)))
                Apiary.RemoveHive(Apiary.Hives[Apiary.HiveCount - 1].Id);
            GUI.enabled = true;
            for (int i = 0; i < Apiary.Hives.Count; i++)
            {
                var hive = Apiary.Hives[i];
                GUILayout.Label($"HIVE {hive.Id}\nStatus: {hive.Status}", label);
            }
            GUILayout.EndScrollView();
            GUILayout.EndArea(); GUI.matrix = previous;
        }
    }
}


