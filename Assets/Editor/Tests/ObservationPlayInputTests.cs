using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Batch-only Play Mode smoke test: a real Canvas needs rendered frames to obtain raycast depth.
[InitializeOnLoad]
public static class ObservationPlayInputTests
{
    private const string Key="HoneyComb.ObservationPlayInputTests";
    private static int frame;
    private static Image blocker;
    private static CanvasGroup group;
    private static double started;
    static ObservationPlayInputTests()
    {
        if(SessionState.GetBool(Key,false)) { started=EditorApplication.timeSinceStartup; EditorApplication.update+=Tick; }
    }
    public static void BeginBatch()
    {
        SessionState.SetBool(Key,true);
        started=EditorApplication.timeSinceStartup;
        EditorApplication.update-=Tick; EditorApplication.update+=Tick;
        EditorApplication.EnterPlaymode();
    }
    private static void Tick()
    {
        try
        {
            if(EditorApplication.timeSinceStartup-started>60) throw new Exception("Play input test timed out");
            if(!EditorApplication.isPlaying || EditorApplication.isCompiling) return;
            frame++;
            if(frame==1)
            {
                new GameObject("TestEventSystem",typeof(EventSystem));
                var go=new GameObject("TestCanvas",typeof(RectTransform),typeof(Canvas),typeof(GraphicRaycaster));
                var canvas=go.GetComponent<Canvas>(); canvas.renderMode=RenderMode.ScreenSpaceOverlay; canvas.sortingOrder=1000;
                var child=new GameObject("BlockingUI",typeof(RectTransform),typeof(CanvasRenderer),typeof(Image),typeof(CanvasGroup));
                child.transform.SetParent(go.transform,false);
                var rect=(RectTransform)child.transform; rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero;
                blocker=child.GetComponent<Image>(); group=child.GetComponent<CanvasGroup>();
            }
            if(frame<20) return;
            Canvas.ForceUpdateCanvases();
            var point=new Vector2(Screen.width*.5f,Screen.height*.5f);
            if(!ObservationInputGuard.IsOverUI(point)) throw new Exception("Visible UI did not block input; graphic depth="+blocker.depth);
            group.blocksRaycasts=false;
            if(ObservationInputGuard.IsOverUI(point)) throw new Exception("Hidden panel still blocks input");
            group.blocksRaycasts=true; blocker.raycastTarget=false;
            if(ObservationInputGuard.IsOverUI(point)) throw new Exception("Decorative image blocks input");
            File.WriteAllText("observation-play-result.txt","PASS: 3 real Canvas input checks in Play Mode.");
            Finish(0);
        }
        catch(Exception e)
        {
            Debug.LogException(e); File.WriteAllText("observation-play-result.txt","FAIL: "+e); Finish(1);
        }
    }
    private static void Finish(int code)
    {
        SessionState.SetBool(Key,false); EditorApplication.update-=Tick; EditorApplication.Exit(code);
    }
}
