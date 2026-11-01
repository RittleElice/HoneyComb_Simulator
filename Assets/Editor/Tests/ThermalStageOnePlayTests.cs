using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class ThermalStageOnePlayTests
{
    const string Key="HoneyComb.ThermalStageOnePlay";
    static int phase;
    static double started;
    static int checks;
    static System.Threading.Tasks.Task<WorldSaveSnapshot> load;
    static ThermalStageOnePlayTests()
    {
        EditorApplication.update += Tick;
        EditorApplication.playModeStateChanged += mode => {
            if(mode==PlayModeStateChange.EnteredEditMode && SessionState.GetBool(Key,false))
            {
                Environment.SetEnvironmentVariable("HONEYCOMB_SAVE_TEST_ROOT",SessionState.GetString(Key+"env", ""));
                SessionState.SetBool(Key,false);
            }
        };
        started=EditorApplication.timeSinceStartup;
    }
    static void Check(bool b,string message) { checks++; if(!b) throw new Exception(message); }
    static void Finish(string message)
    {
        File.WriteAllText("outputs/thermal-stage-one/play-tests.txt",message);
        EditorApplication.ExitPlaymode();
    }
    static void Tick()
    {
        const string trigger="Temp/run-thermal-stage-one-play";
        if(!SessionState.GetBool(Key,false))
        {
            if(!File.Exists(trigger)||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode) return;
            File.Delete(trigger);
            if(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name!="Bootstrap") { File.WriteAllText("outputs/thermal-stage-one/play-tests.txt","SKIP: open Bootstrap first"); return; }
            SessionState.SetString(Key+"env",Environment.GetEnvironmentVariable("HONEYCOMB_SAVE_TEST_ROOT")??"");
            string root=Path.GetFullPath("outputs/thermal-stage-one/play-save-"+DateTime.UtcNow.ToString("yyyyMMddHHmmss"));
            Environment.SetEnvironmentVariable("HONEYCOMB_SAVE_TEST_ROOT",root);
            SessionState.SetBool(Key,true); started=EditorApplication.timeSinceStartup; phase=0; checks=0;
            EditorApplication.EnterPlaymode(); return;
        }
        try
        {
            if(EditorApplication.timeSinceStartup-started>120) throw new Exception("Thermal Play timeout phase "+phase);
            if(!EditorApplication.isPlaying || EditorApplication.isCompiling) return;
            var b=GameBootstrap.Instance;
            if(b==null||b.GameFlow==null||b.GameFlow.IsTransitioning) return;
            Check(b.SaveSystem.SaveDirectory==Environment.GetEnvironmentVariable("HONEYCOMB_SAVE_TEST_ROOT"),"isolated save root");
            if(phase==0) { b.StartNewGame(); phase=1; return; }
            if(b.GameWorld==null||b.SaveSystem.IsSaving||GameTimeRuntime.InputBlocked) return;
            if(phase==1)
            {
                Check(b.GameWorld.Thermal!=null,"BeginWorld initialized thermal");
                Check(b.TimeSystem.CanCaptureWorld(b.GameWorld),"scheduler save eligibility unchanged");
                var frame=b.GameWorld.Inventory.GetSlot(4).Item;
                frame.ThermalState.RestoreEnergy(0,321.5f);
                frame.GetContent<FrameData>().Front.TrySetConstruction(0,0,100);
                b.SaveSystem.SaveSynchronously(); Check(b.SaveSystem.HasSave,"actual save wrote");
                load=b.SaveSystem.LoadAsync(); phase=2; return;
            }
            if(phase==2)
            {
                if(!load.IsCompleted) return;
                var loaded=load.GetAwaiter().GetResult().World; loaded.PrepareThermal(ThermalMaterials.Load());
                Check(loaded.Inventory.GetSlot(4).Item.ThermalState.EnergyJ[0]==321.5f,"actual store/async load preserves float energy");
                Check(loaded.Inventory.GetSlot(4).Item.GetContent<FrameData>().Front.GetCell(0,0).Construction==100,"comb saved together");
                b.TimeSystem.JumpTo(1,1,1,1,0); phase=3; return;
            }
            if(phase==3)
            {
                Check(b.GameWorld.CurrentGameMinute>=60,"live scheduler advanced one hour");
                Check(b.GameWorld.Inventory.GetSlot(4).Item.ThermalState.EnergyJ[0]==321.5f,"no heat exchange during actual game time");
                Check(b.TimeSystem.Scheduler.ParticipantCount==1,"weather remains only participant");
                Check(b.TimeSystem.Failure==null,"runtime no fault");
                Finish("PASS "+checks+" checks: original Bootstrap/StartNewGame; runtime thermal initialization; isolated actual save/load; one hour jump; energy preserved; no solver.\n"); phase=4;
            }
        }
        catch(Exception e) { Debug.LogException(e); Finish("FAIL "+e); phase=4; }
    }
}
