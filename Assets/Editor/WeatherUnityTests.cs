using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class WeatherUnityTests
{
    private const string PlayKey="HoneyComb.WeatherV2.PlayTest";
    private static int phase,checks;
    private static double started;
    private static long resumeMinimum;
    private static GameBootstrap previous;
    private static WeatherSimulator reference;
    private static string Reports => Environment.GetEnvironmentVariable("HONEYCOMB_WEATHER_REPORT_ROOT") ?? "outputs/weather-v2-tests";
    static WeatherUnityTests()
    { if(SessionState.GetBool(PlayKey,false)) { started=EditorApplication.timeSinceStartup;EditorApplication.update+=Tick; } }
    private static void Check(bool ok,string message) { checks++;if(!ok)throw new Exception(message); }
    private static byte[] Bytes(WeatherSimulator weather)
    { using(var ms=new MemoryStream())using(var w=new BinaryWriter(ms)){weather.WriteSnapshot(w);return ms.ToArray();} }
    private static WorldSaveSnapshot Snapshot(GameWorld world) => new WorldSaveSnapshot {World=world,UtcTicks=DateTime.UtcNow.Ticks,TargetMinute=world.CurrentGameMinute};
    public static async void RunBatch()
    {
        try
        {
            Directory.CreateDirectory(Reports);
            var baseline=WeatherAssetLoader.LoadBaseline();
            var world=new GameWorld(new WeatherSimulator(42,baseline));
            var scheduler=new SimulationScheduler(world.Clock);scheduler.Register(world.Weather);
            await scheduler.RunToAsync(2881,()=>Task.CompletedTask,()=>Task.CompletedTask,CancellationToken.None);
            var snap=Snapshot(world);byte[] encoded=WorldSaveCodec.Encode(snap);var loaded=WorldSaveCodec.Decode(encoded);
            Check(encoded.SequenceEqual(WorldSaveCodec.Encode(loaded)),"world+weather byte-identical roundtrip");
            await WeatherAssetLoader.PrepareLoadedWorldAsync(loaded.World);
            var resumed=new SimulationScheduler(loaded.World.Clock);resumed.Register(loaded.World.Weather);
            await scheduler.RunToAsync(6000,()=>Task.CompletedTask,()=>Task.CompletedTask,CancellationToken.None);
            await resumed.RunToAsync(6000,()=>Task.CompletedTask,()=>Task.CompletedTask,CancellationToken.None);
            Check(Bytes(world.Weather).SequenceEqual(Bytes(loaded.World.Weather)),"weather after world save/resume");
            var legacy=new GameWorld();await new SimulationScheduler(legacy.Clock).RunToAsync(1234,()=>Task.CompletedTask,()=>Task.CompletedTask,CancellationToken.None);
            byte[] legacyBytes=WorldSaveCodec.Encode(Snapshot(legacy));legacyBytes[0]=1;Array.Resize(ref legacyBytes,legacyBytes.Length-1);
            var migrated=WorldSaveCodec.Decode(legacyBytes);Check(migrated.World.Weather==null,"schema 1 readable");
            await WeatherAssetLoader.PrepareLoadedWorldAsync(migrated.World);
            var expected=new WeatherSimulator(WeatherSimulator.DefaultSeed,baseline);while(expected.Minute<1234)expected.Advance(expected.Minute+1);
            Check(Bytes(expected).SequenceEqual(Bytes(migrated.World.Weather)),"legacy migration deterministic replay");
            Check(migrated.World.Inventory.GetSlot(4).Item.Id==104,"legacy equipment preserved");
            var store=new WorldSaveStore(Path.Combine(Reports,"unit-saves"));
            store.Save(WorldSaveCodec.Encode(Snapshot(world)));store.Save(WorldSaveCodec.Encode(Snapshot(loaded.World)));
            File.WriteAllText(Path.Combine(Reports,"unit-saves/world-b.save"),"injected corruption");
            var recovered=store.Load(out bool fallback);Check(fallback && Bytes(recovered.World.Weather).SequenceEqual(Bytes(world.Weather)),"weather recovery from previous generation");
            WorldSaveTests.Run();await GameTimeTests.RunAll();
            File.WriteAllText(Path.Combine(Reports,"unity-tests.txt"),"PASS: "+checks+" weather asset/world save/migration/recovery checks plus existing WorldSaveTests and GameTimeTests.");
            EditorApplication.Exit(0);
        }
        catch(Exception e) { Debug.LogException(e);Directory.CreateDirectory(Reports);File.WriteAllText(Path.Combine(Reports,"unity-tests.txt"),e.ToString());EditorApplication.Exit(1); }
    }
    public static void BeginPlayBatch()
    {
        if(string.IsNullOrEmpty(Environment.GetEnvironmentVariable("HONEYCOMB_SAVE_TEST_ROOT")))throw new Exception("Isolated saves required.");
        SessionState.SetBool(PlayKey+".Cold",false);
        EditorSceneManager.OpenScene("Assets/Scenes/Bootstrap.unity");SessionState.SetBool(PlayKey,true);
        started=EditorApplication.timeSinceStartup;EditorApplication.update-=Tick;EditorApplication.update+=Tick;EditorApplication.EnterPlaymode();
    }
    public static void BeginColdBatch()
    {
        BeginPlayBatch();
        SessionState.SetBool(PlayKey+".Cold",true);
    }
    private static void CompareCurrent(GameWorld world)
    {
        if(reference==null)reference=WeatherAssetLoader.Create(WeatherSimulator.DefaultSeed);
        while(reference.Minute<world.CurrentGameMinute)reference.Advance(reference.Minute+1);
        Check(world.Weather.Minute==world.CurrentGameMinute,"weather clock alignment");
        Check(Bytes(reference).SequenceEqual(Bytes(world.Weather)),"live/jump/offline path matches uninterrupted seed");
    }
    private static void Tick()
    {
        try
        {
            if(EditorApplication.timeSinceStartup-started>150)throw new Exception("Weather Play timeout phase "+phase);
            if(!EditorApplication.isPlaying||EditorApplication.isCompiling)return;
            var b=GameBootstrap.Instance;if(b==null||b.GameFlow==null||b.GameFlow.IsTransitioning)return;
            if(b.TimeSystem.Failure!=null)throw new Exception(b.TimeSystem.Failure);
            switch(phase)
            {
                case 0:
                    if(SessionState.GetBool(PlayKey+".Cold",false))
                    {
                        var saved=new WorldSaveStore(b.SaveSystem.SaveDirectory).Load(out _);
                        resumeMinimum=saved.World.CurrentGameMinute;
                        b.ContinueGame();phase=4;
                    }
                    else { b.StartNewGame();phase++; }
                    break;
                case 1:
                    if(b.GameWorld==null||b.GameWorld.CurrentGameMinute<1||b.SaveSystem.IsSaving||GameTimeRuntime.InputBlocked)return;
                    CompareCurrent(b.GameWorld);Check(b.TimeSystem.Scheduler.ParticipantCount==1,"single weather registration");
                    Check(b.SaveSystem.CanSnapshot,"weather participant supports save");
                    b.TimeSystem.JumpTo(1,1,3,0,1);phase++;break;
                case 2:
                    if(GameTimeRuntime.InputBlocked||b.SaveSystem.IsSaving)return;
                    CompareCurrent(b.GameWorld);Check(b.GameWorld.CurrentGameMinute>=2881,"jump generated every minute");
                    b.SaveSystem.SaveSynchronously();var store=new WorldSaveStore(b.SaveSystem.SaveDirectory);var snap=store.Load(out _);
                    Check(Bytes(snap.World.Weather).SequenceEqual(Bytes(b.GameWorld.Weather)),"actual save includes weather");
                    snap.UtcTicks=DateTime.UtcNow.AddSeconds(-60).Ticks;snap.HighWaterUtcTicks=0;snap.TargetMinute=snap.World.CurrentGameMinute;
                    resumeMinimum=snap.World.CurrentGameMinute+12;store.Save(WorldSaveCodec.Encode(snap));
                    previous=b;UnityEngine.Object.DestroyImmediate(b.gameObject);SceneManager.LoadScene("Bootstrap",LoadSceneMode.Single);phase++;break;
                case 3:if(b==previous)return;b.ContinueGame();phase++;break;
                case 4:
                    if(b.SaveSystem.IsLoading||b.GameWorld==null||GameTimeRuntime.InputBlocked||b.SaveSystem.IsSaving)return;
                    Check(b.GameWorld.CurrentGameMinute>=resumeMinimum,"offline minutes generated after Continue");CompareCurrent(b.GameWorld);
                    Check(b.TimeSystem.Scheduler.ParticipantCount==1,"no duplicate participant after Continue");
                    b.SaveSystem.SaveSynchronously();Directory.CreateDirectory(Reports);
                    bool cold=SessionState.GetBool(PlayKey+".Cold",false);
                    File.WriteAllText(Path.Combine(Reports,cold?"unity-cold-tests.txt":"unity-play-tests.txt"),cold?
                        "PASS: a separate Unity process loaded the previous process save, bound baseline, resumed offline minutes and matched uninterrupted seeded weather. Checks="+checks:
                        "PASS: actual Bootstrap new game, normal 5-second/1-minute tick, 2-day jump, world save, Bootstrap restart, Continue, UTC offline catch-up, seeded weather equality. Checks="+checks);
                    SessionState.SetBool(PlayKey,false);EditorApplication.update-=Tick;EditorApplication.Exit(0);break;
            }
        }
        catch(Exception e)
        { Debug.LogException(e);Directory.CreateDirectory(Reports);File.WriteAllText(Path.Combine(Reports,SessionState.GetBool(PlayKey+".Cold",false)?"unity-cold-tests.txt":"unity-play-tests.txt"),e.ToString());SessionState.SetBool(PlayKey,false);EditorApplication.update-=Tick;EditorApplication.Exit(1); }
    }
}
