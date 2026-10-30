using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Real scene lifecycle and existing onClick callbacks. Does not simulate physical pointer input.
[InitializeOnLoad]
public static class UnifiedButtonPlayTests
{
    private const string Key = "HoneyComb.UnifiedButtonPlayTests";
    private static double started;
    private static int stage, frames, checks;
    static UnifiedButtonPlayTests()
    {
        if (SessionState.GetBool(Key,false)) { started=EditorApplication.timeSinceStartup; EditorApplication.update+=Tick; }
    }
    public static void BeginBatch()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SessionState.SetBool(Key,true);
        started=EditorApplication.timeSinceStartup;
        EditorApplication.update-=Tick; EditorApplication.update+=Tick;
        EditorApplication.EnterPlaymode();
    }
    private static void Check(bool ok, string message) { checks++; if (!ok) throw new Exception(message); }
    private static Button ButtonFor(ActionButtonId id) => UnityEngine.Object.FindObjectsByType<ActionButtonView>(FindObjectsInactive.Exclude).Single(v=>v.Id==id).GetComponent<Button>();
    private static bool Loaded(string name) => SceneManager.GetSceneByName(name).isLoaded;
    private static void Select(int x,int y)
    {
        var selection=UnityEngine.Object.FindAnyObjectByType<ApiarySelectionController>();
        typeof(ApiarySelectionController).GetMethod("SetSelection", BindingFlags.Instance|BindingFlags.NonPublic).Invoke(selection,new object[]{x,y});
    }
    private static void Tick()
    {
        try
        {
            if (EditorApplication.timeSinceStartup-started>120) throw new Exception("Play test timeout at stage "+stage);
            if (!EditorApplication.isPlaying || EditorApplication.isCompiling) return;
            if (++frames < 10) return;
            frames=0;
            if (stage==0) { new GameObject("Bootstrap",typeof(GameBootstrap)); stage++; return; }
            var boot=GameBootstrap.Instance;
            if (boot==null || boot.GameFlow.IsTransitioning) return;
            if (stage==1 && Loaded("MainMenu"))
            {
                var button=ButtonFor(ActionButtonId.NewGame);
                Check(button.onClick.GetPersistentEventCount()==1,"new game persistent event retained");
                button.onClick.Invoke(); stage++; return;
            }
            if (stage==2 && Loaded("Apiary") && Loaded("SharedUI"))
            {
                var slots=UnityEngine.Object.FindObjectsByType<ActionButtonView>(FindObjectsInactive.Exclude).Where(v=>v.Id==ActionButtonId.InventorySlot).OrderBy(v=>v.transform.GetSiblingIndex()).ToArray();
                Check(slots.Length==12,"12 live inventory buttons");
                var item=boot.GameWorld.Inventory.GetSlot(0).Value;
                slots[0].GetComponent<Button>().onClick.Invoke();
                Check(ReferenceEquals(item,boot.GameWorld.Hand.Slot.Value),"live inventory pickup");
                slots[0].GetComponent<Button>().onClick.Invoke();
                Check(boot.GameWorld.Hand.Slot.IsEmpty,"live inventory return");
                Select(0,0);
                ButtonFor(ActionButtonId.Observe).onClick.Invoke(); stage++; return;
            }
            if (stage==3 && Loaded("Observation"))
            {
                Check(Loaded("SharedUI") && !Loaded("Apiary"),"observation navigation retains shared UI");
                var back=ButtonFor(ActionButtonId.Back);
                Check(back.GetComponent<Canvas>().sortingOrder>100,"back stays above inventory");
                back.onClick.Invoke(); stage++; return;
            }
            if (stage==4 && Loaded("Apiary") && !Loaded("Observation"))
            {
                Check(Loaded("SharedUI"),"back returns to apiary with inventory");
                var grid=boot.GameWorld.Apiary.Grid;
                int width=grid.UnlockedWidth;
                Select(grid.LockedColumnX,0);
                ButtonFor(ActionButtonId.UnlockLand).onClick.Invoke();
                Check(grid.UnlockedWidth==width+1,"registered unlock button preserves unlock action");
                var buttons=UnityEngine.Object.FindObjectsByType<Button>(FindObjectsInactive.Exclude);
                Check(buttons.All(b=>b.GetComponent<ActionButtonView>()!=null),"all live buttons registered");
                Directory.CreateDirectory("outputs/common-buttons");
                File.WriteAllText("outputs/common-buttons/play-tests.txt","PASS: "+checks+" Play Mode checks; real scene transitions and onClick callbacks (not physical pointer simulation).");
                Finish(0);
            }
        }
        catch(Exception e)
        {
            Debug.LogException(e);
            Directory.CreateDirectory("outputs/common-buttons");
            File.WriteAllText("outputs/common-buttons/play-tests.txt","FAIL: "+e);
            Finish(1);
        }
    }
    private static void Finish(int code)
    {
        SessionState.SetBool(Key,false); EditorApplication.update-=Tick; EditorApplication.Exit(code);
    }
}
