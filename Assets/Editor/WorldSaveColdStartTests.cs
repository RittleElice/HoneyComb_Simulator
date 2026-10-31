using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class WorldSaveColdStartTests
{
    private const string Key="HoneyComb.ColdStart";
    private static int phase;
    private static double start;
    private static long minimum;
    static WorldSaveColdStartTests() { if(SessionState.GetBool(Key,false)) { start=EditorApplication.timeSinceStartup; EditorApplication.update+=Tick; } }
    public static void BeginBatch()
    {
        var directory=Environment.GetEnvironmentVariable("HONEYCOMB_SAVE_TEST_ROOT");
        if(string.IsNullOrEmpty(directory)||!new WorldSaveStore(directory).HasFiles) throw new Exception("Existing isolated test save required.");
        EditorSceneManager.OpenScene("Assets/Scenes/Bootstrap.unity"); SessionState.SetBool(Key,true); start=EditorApplication.timeSinceStartup;
        EditorApplication.update-=Tick; EditorApplication.update+=Tick; EditorApplication.EnterPlaymode();
    }
    private static void Tick()
    {
        try
        {
            if(EditorApplication.timeSinceStartup-start>90) throw new Exception("Cold-start timeout");
            if(!EditorApplication.isPlaying||EditorApplication.isCompiling) return;
            var b=GameBootstrap.Instance; if(b==null||b.GameFlow==null||b.GameFlow.IsTransitioning) return;
            if(phase==0)
            {
                var saved=new WorldSaveStore(b.SaveSystem.SaveDirectory).Load(out _);
                minimum=checked(saved.TargetMinute+Math.Max(0,DateTime.UtcNow.Ticks-saved.UtcTicks)/(5*TimeSpan.TicksPerSecond));
                b.ContinueGame(); phase=1; return;
            }
            if(b.SaveSystem.IsLoading||b.GameWorld==null||GameTimeRuntime.InputBlocked||b.SaveSystem.IsSaving) return;
            if(b.GameWorld.CurrentGameMinute<minimum||b.GameWorld.Hand.Item?.Id!=104||Math.Abs(b.GameWorld.Hand.Item.Aging-.42f)>.00001f)
                throw new Exception("Cold-start data or elapsed time mismatch");
            File.WriteAllText("save-cold-start-tests.txt","PASS: a separate Unity process loaded previous process files, restored hand/aging, and completed UTC offline minutes before play.");
            SessionState.SetBool(Key,false); EditorApplication.update-=Tick; EditorApplication.Exit(0);
        }
        catch(Exception e) { Debug.LogException(e); File.WriteAllText("save-cold-start-tests.txt",e.ToString()); SessionState.SetBool(Key,false); EditorApplication.update-=Tick; EditorApplication.Exit(1); }
    }
}
