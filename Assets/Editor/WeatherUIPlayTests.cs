using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;

[InitializeOnLoad]
public static class WeatherUIPlayTests
{
    private const string Key="HoneyComb.WeatherUI.Play";
    private static int phase,checks;
    private static double start;
    private static long minute;
    private static GameWeatherUI view;
    private static string Root=>Environment.GetEnvironmentVariable("HONEYCOMB_WEATHER_REPORT_ROOT");
    static WeatherUIPlayTests() {if(SessionState.GetBool(Key,false)){start=EditorApplication.timeSinceStartup;EditorApplication.update+=Tick;}}
    public static void BeginBatch()
    {
        if(string.IsNullOrEmpty(Root)||string.IsNullOrEmpty(Environment.GetEnvironmentVariable("HONEYCOMB_SAVE_TEST_ROOT")))throw new Exception("Isolated outputs required.");
        EditorSceneManager.OpenScene("Assets/Scenes/Bootstrap.unity");SessionState.SetBool(Key,true);start=EditorApplication.timeSinceStartup;
        EditorApplication.update-=Tick;EditorApplication.update+=Tick;EditorApplication.EnterPlaymode();
    }
    private static void Check(bool ok,string message){checks++;if(!ok)throw new Exception(message);}
    private static byte[] State(GameWorld world){using(var m=new MemoryStream())using(var w=new BinaryWriter(m)){world.Weather.WriteSnapshot(w);return m.ToArray();}}
    private static void Click(GameObject go){ExecuteEvents.Execute(go,new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Left},ExecuteEvents.pointerClickHandler);}
    private static void Tick()
    {
        try
        {
            if(EditorApplication.timeSinceStartup-start>100)throw new Exception("Weather UI timeout phase "+phase);
            if(!EditorApplication.isPlaying||EditorApplication.isCompiling)return;
            var b=GameBootstrap.Instance;if(b==null||b.GameFlow==null||b.GameFlow.IsTransitioning)return;
            if(b.TimeSystem.Failure!=null)throw new Exception(b.TimeSystem.Failure);
            switch(phase)
            {
                case 0:b.StartNewGame();phase++;break;
                case 1:
                    view=UnityEngine.Object.FindAnyObjectByType<GameWeatherUI>();
                    if(view==null||view.OpenButton==null||GameTimeRuntime.InputBlocked||b.SaveSystem.IsSaving)return;
                    Check(!view.IsOpen,"window starts closed");
                    var catalog=Resources.Load<ActionButtonCatalog>("ActionButtonCatalog");
                    Check(catalog.entries.Count(e=>e.id==ActionButtonId.Weather)==1&&catalog.entries.Count(e=>e.id==ActionButtonId.CloseWeather)==1,"catalog entries unique");
                    var before=State(b.GameWorld);Click(view.OpenButton.gameObject);
                    Check(view.IsOpen&&State(b.GameWorld).SequenceEqual(before),"button opens without altering weather/RNG");
                    Check(view.DisplayedMinute==b.GameWorld.CurrentGameMinute,"initial current weather displayed");phase++;break;
                case 2:
                    Canvas.ForceUpdateCanvases();
                    var panel=(RectTransform)view.transform.Find("WeatherWindow");var points=new Vector3[4];panel.GetWorldCorners(points);
                    Check(points[0].x>=0&&points[0].y>=0&&points[2].x<=Screen.width&&points[2].y<=Screen.height,"window fits screen");
                    foreach(var text in panel.GetComponentsInChildren<TMP_Text>()){text.ForceMeshUpdate();Check(!text.isTextOverflowing,"text fits: "+text.name);}
                    var rect=(RectTransform)view.OpenButton.transform;rect.GetWorldCorners(points);
                    Check(points[0].x<Screen.width*.25f&&points[2].y>Screen.height*.8f,"top-left button placement");
                    var pointer=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(null,rect.position)};
                    var hits=new System.Collections.Generic.List<RaycastResult>();EventSystem.current.RaycastAll(pointer,hits);
                    Check(hits.Count>0&&(hits[0].gameObject==view.OpenButton.gameObject||hits[0].gameObject.transform.IsChildOf(view.OpenButton.transform)),"button receives pointer raycast");
                    phase++;break;
                case 3:
                    minute=b.GameWorld.CurrentGameMinute;b.TimeSystem.JumpBy(0,0,0,1);phase++;break;
                case 4:
                    if(GameTimeRuntime.InputBlocked||view.DisplayedMinute<=minute||!view.CloseButton.interactable||!view.OpenButton.interactable)return;
                    Check(view.DisplayedMinute==b.GameWorld.CurrentGameMinute,"open window updates on minute");
                    Click(view.CloseButton.gameObject);Check(!view.IsOpen,"close button");
                    Click(view.OpenButton.gameObject);Click(view.OpenButton.gameObject);Check(!view.IsOpen,"toggle closes");
                    Click(view.OpenButton.gameObject);b.GameFlow.OpenObservation(0,0);phase++;break;
                case 5:
                    Check(UnityEngine.Object.FindObjectsByType<GameWeatherUI>().Length==1,"one shared weather view after scene transition");
                    Check(view.IsOpen,"view survives Observation transition");
                    File.WriteAllText(Path.Combine(Root,"weather-ui-tests.txt"),"PASS "+checks+" checks: actual button clicks/raycast, open/close/toggle, all labels fit, current-minute refresh, no weather mutation, shared view across Apiary/Observation.");
                    SessionState.SetBool(Key,false);EditorApplication.update-=Tick;EditorApplication.Exit(0);break;
            }
        }
        catch(Exception e){Debug.LogException(e);File.WriteAllText(Path.Combine(Root,"weather-ui-tests.txt"),e.ToString());SessionState.SetBool(Key,false);EditorApplication.update-=Tick;EditorApplication.Exit(1);}
    }
}
