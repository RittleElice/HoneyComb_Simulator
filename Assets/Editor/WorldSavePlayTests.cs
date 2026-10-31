using System;
using System.IO;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[InitializeOnLoad]
public static class WorldSavePlayTests
{
    private const string Key="HoneyComb.WorldSavePlayTests";
    private static int phase;
    private static double started;
    private static long checkpointMinute, target;
    private static GameBootstrap previous;
    private static string directory;
    static WorldSavePlayTests()
    { if(SessionState.GetBool(Key,false)) { started=EditorApplication.timeSinceStartup; EditorApplication.update+=Tick; } }
    public static void BeginBatch()
    {
        if(string.IsNullOrEmpty(Environment.GetEnvironmentVariable("HONEYCOMB_SAVE_TEST_ROOT"))) throw new Exception("An isolated test directory is mandatory.");
        EditorSceneManager.OpenScene("Assets/Scenes/Bootstrap.unity");
        SessionState.SetBool(Key,true); started=EditorApplication.timeSinceStartup;
        EditorApplication.update-=Tick; EditorApplication.update+=Tick; EditorApplication.EnterPlaymode();
    }
    private static void Check(bool ok,string message) { if(!ok) throw new Exception(message); }
    private static void Tick()
    {
        try
        {
            if(EditorApplication.timeSinceStartup-started>180) throw new Exception("Save Play test timeout at phase "+phase);
            if(!EditorApplication.isPlaying || EditorApplication.isCompiling) return;
            var b=GameBootstrap.Instance;
            if(b==null||b.GameFlow==null||b.GameFlow.IsTransitioning) return;
            switch(phase)
            {
                case 0:
                    directory=b.SaveSystem.SaveDirectory;
                    Check(directory==Environment.GetEnvironmentVariable("HONEYCOMB_SAVE_TEST_ROOT"),"isolated persistence");
                    b.StartNewGame(); phase++; break;
                case 1:
                    if(b.SaveSystem.IsSaving || GameTimeRuntime.InputBlocked) return;
                    if(UnityEngine.Object.FindAnyObjectByType<GameSaveUI>()==null) return;
                    Check(b.GameWorld.Inventory.GetSlot(4).TryMoveTo(b.GameWorld.Hand.Slot),"move item before save");
                    b.GameWorld.Hand.Item.TrySetAging(.42f);
                    b.GameWorld.Hand.Item.GetContent<FrameData>().Front.TrySetConstruction(0,0,93);
                    b.SaveSystem.SaveSynchronously(); Check(b.SaveSystem.HasSave,"manual save written");
                    b.TimeSystem.JumpTo(10,1,1,0,0); phase++; break;
                case 2:
                    if(!b.TimeSystem.Scheduler.IsRunning || b.TimeSystem.Clock.CurrentGameMinute<1) return;
                    Check(GameTimeRuntime.InputBlocked,"jump blocks gameplay");
                    b.SaveSystem.SaveSynchronously();
                    var save=new WorldSaveStore(directory).Load(out _);
                    checkpointMinute=save.World.CurrentGameMinute; target=save.TargetMinute;
                    Check(checkpointMinute>0 && checkpointMinute<target && target==GameClock.ToMinute(10,1,1,0,0),"in-progress target checkpoint");
                    previous=b; UnityEngine.Object.DestroyImmediate(b.gameObject);
                    SceneManager.LoadScene("Bootstrap",LoadSceneMode.Single); phase++; break;
                case 3:
                    if(b==previous || b.SaveSystem.IsSaving) return;
                    Check(b.GameWorld==null && b.SaveSystem.HasSave,"new session detects save");
                    var continueButton=Array.Find(UnityEngine.Object.FindObjectsByType<ActionButtonView>(),v=>v.Id==ActionButtonId.ContinueGame);
                    if(continueButton==null) return;
                    Check(continueButton.GetComponent<Button>().interactable,"continue button enabled");
                    b.ContinueGame(); phase++; break;
                case 4:
                    if(b.SaveSystem.IsLoading || b.GameWorld==null || GameTimeRuntime.InputBlocked || b.SaveSystem.IsSaving) return;
                    Check(b.GameWorld.CurrentGameMinute>=target,"remaining jump resumed to target");
                    Check(b.GameWorld.Hand.Item.Id==104 && Math.Abs(b.GameWorld.Hand.Item.Aging-.42f)<.00001f,"hand and aging restored");
                    Check(b.GameWorld.Hand.Item.GetContent<FrameData>().Front.GetCell(0,0).Construction==93,"comb restored");
                    Check(b.GameWorld.Inventory.GetSlot(4).IsEmpty,"no duplicate item");
                    b.SaveSystem.SaveSynchronously();
                    // Seed a closed-application interval against this fully serialized world.
                    var offline=new WorldSaveStore(directory).Load(out _); offline.UtcTicks=DateTime.UtcNow.AddSeconds(-60).Ticks;
                    offline.HighWaterUtcTicks=0; checkpointMinute=offline.World.CurrentGameMinute; offline.TargetMinute=checkpointMinute;
                    new WorldSaveStore(directory).Save(WorldSaveCodec.Encode(offline));
                    previous=b; UnityEngine.Object.DestroyImmediate(b.gameObject); SceneManager.LoadScene("Bootstrap",LoadSceneMode.Single); phase++; break;
                case 5:
                    if(b==previous) return;
                    b.ContinueGame(); phase++; break;
                case 6:
                    if(b.SaveSystem.IsLoading || b.GameWorld==null || GameTimeRuntime.InputBlocked || b.SaveSystem.IsSaving) return;
                    Check(b.GameWorld.CurrentGameMinute>=checkpointMinute+12,"cold-start UTC offline ticks");
                    Check(b.GameWorld.CurrentGameMinute<checkpointMinute+30,"no duplicated catchup window");
                    Check(UnityEngine.Object.FindAnyObjectByType<GameSaveUI>()!=null,"save UI mounted on SharedUI");
                    File.WriteAllText("save-play-tests.txt","PASS: actual scenes; manual/automatic persistence; in-progress jump checkpoint; Bootstrap replacement and Continue UI; whole-state restore; cold-start offline calculation; no duplicate ownership.");
                    SessionState.SetBool(Key,false); EditorApplication.update-=Tick; EditorApplication.Exit(0); break;
            }
        }
        catch(Exception e) { Debug.LogException(e); File.WriteAllText("save-play-tests.txt",e.ToString()); SessionState.SetBool(Key,false); EditorApplication.update-=Tick; EditorApplication.Exit(1); }
    }
}
