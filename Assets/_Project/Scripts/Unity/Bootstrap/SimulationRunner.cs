using System;
using System.Globalization;
using HoneyComb.Simulation.Time;
using HoneyComb.Simulation.Bees;
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
        public BeePopulation Population { get; private set; }
        private BeeSpawnSettings beeDefaults;

        private readonly HoneyComb.Unity.Presentation.ObservationPanel observation = new HoneyComb.Unity.Presentation.ObservationPanel();
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
            try
            {
                if(masterData.beeDefaults==null) throw new ArgumentException("Bee defaults are missing.");
                beeDefaults=masterData.beeDefaults.CreateSettings();
                Population=new BeePopulation(Apiary,masterData.apiaryConfig.CreateExternalRegion(),masterData.beeDefaults.CreateLifecycle(),masterData.initialElapsedHours);
            }
            catch(ArgumentException exception) { Debug.LogError(exception.Message,this);enabled=false;return; }
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
            Population.TickHour(hour);
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
            if(Clock==null || !showPanel) return;
            Rect safe=Screen.safeArea;
            float scale=Mathf.Max(0.1f,Mathf.Min(safe.width/600f,safe.height/1040f));
            Matrix4x4 previous=GUI.matrix;
            GUI.matrix=Matrix4x4.TRS(new Vector3(safe.x,Screen.height-safe.yMax,0),Quaternion.identity,Vector3.one*scale);
            float height=safe.height/scale;
            Color tint=GUI.color;GUI.color=new Color(.075f,.11f,.15f);GUI.DrawTexture(new Rect(0,0,600,height),Texture2D.whiteTexture);GUI.color=tint;
            var title=new GUIStyle(GUI.skin.label){fontSize=25,fontStyle=FontStyle.Bold};
            var text=new GUIStyle(GUI.skin.label){fontSize=18,wordWrap=true};
            var button=new GUIStyle(GUI.skin.button){fontSize=20};
            var field=new GUIStyle(GUI.skin.textField){fontSize=22};
            GUILayout.BeginArea(new Rect(20,12,560,218));
            long hour=Clock.TotalHours;
            GUILayout.Label($"HONEYCOMB   |   Day {hour/24+1}   {hour%24:00}:00",title);
            GUILayout.BeginHorizontal();
            if(GUILayout.Button(Clock.Automatic ? "Pause" : "Play",button,GUILayout.Height(45))) Clock.Automatic=!Clock.Automatic;
            GUI.enabled=Clock.PendingTicks==0;
            if(GUILayout.Button("+1 hour",button,GUILayout.Height(45))) Queue(1);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            input=GUILayout.TextField(input,12,field,GUILayout.Width(180),GUILayout.Height(42));
            if(GUILayout.Button("Advance hours",button,GUILayout.Height(42)))
            {
                if(int.TryParse(input,NumberStyles.None,CultureInfo.InvariantCulture,out int hours)&&hours>0&&hours<=maxAdvance) Queue(hours);
                else error=$"Enter 1..{maxAdvance} whole hours.";
            }
            GUILayout.EndHorizontal();GUI.enabled=true;
            var weather=Environment.Current;
            GUILayout.Label($"{weather.TemperatureCelsius:0.#} C  |  RH {weather.HumidityPercent:0.#}%  |  Queued {Clock.PendingTicks:N0}h",text);
            if(!string.IsNullOrEmpty(error)) GUILayout.Label(error,text);
            GUILayout.EndArea();
            GUI.BeginGroup(new Rect(20,235,560,height-250));
            observation.Draw(Apiary,Population,Clock.TotalHours,beeDefaults,560,height-250);
            GUI.EndGroup();GUI.matrix=previous;
        }
    }
}
