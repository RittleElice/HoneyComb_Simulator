using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public static class ButtonRefreshTests
{
    private static int checks;
    private static void Check(bool value, string message) { checks++; if (!value) throw new Exception(message); }
    private static void Tick(ActionButtonView view) => typeof(ActionButtonView).GetMethod("LateUpdate", BindingFlags.Instance|BindingFlags.NonPublic).Invoke(view,null);
    public static void RunBatch()
    {
        GameObject canvasObject=null;
        ActionButtonCatalog catalog=null;
        try
        {
            catalog=UnityEngine.Object.Instantiate(Resources.Load<ActionButtonCatalog>("ActionButtonCatalog"));
            for(int i=0;i<100;i++) catalog.Find(ActionButtonId.ObservationLift);
            long allocatedBefore=GC.GetAllocatedBytesForCurrentThread();
            for(int i=0;i<10000;i++) catalog.Find(ActionButtonId.ObservationLift);
            Check(GC.GetAllocatedBytesForCurrentThread()==allocatedBefore,"10000 catalog lookups allocate no managed memory");
            canvasObject=new GameObject("RefreshTests",typeof(RectTransform),typeof(Canvas),typeof(ActionButtonManager));
            var canvas=canvasObject.GetComponent<Canvas>(); canvas.renderMode=RenderMode.ScreenSpaceOverlay;
            canvasObject.GetComponent<ActionButtonManager>().Catalog=catalog;
            var root=(RectTransform)canvasObject.transform;
            var button=ActionButtonManager.Create(null,root,ActionButtonId.ObservationLift);
            var view=button.GetComponent<ActionButtonView>();
            var positioner=button.GetComponent<ActionButtonPositioner>();
            var style=catalog.Find(ActionButtonId.ObservationLift);
            style.clampToScreen=false; style.showLabel=true; view.Apply();
            Canvas.ForceUpdateCanvases();
            int styles=view.StyleApplicationCount, positions=positioner.PositionApplicationCount;
            for(int i=0;i<120;i++)
            {
                view.SetScreenAnchor(new Vector2(200+i,200),Vector2.right,16);
                view.SetScreenAnchor(new Vector2(220+i,200),Vector2.right,16);
                Check(positioner.PositionApplicationCount==positions+i,"anchors do not synchronously apply position");
                Tick(view);
            }
            Check(view.StyleApplicationCount==styles,"120 moving frames cause zero appearance reapplications");
            Check(positioner.PositionApplicationCount==positions+120,"one position application per moving tick");
            Check(Mathf.Abs(button.transform.position.x-(220+119+style.size.x*.5f+style.followGap))<.1f,"latest anchor wins without a frame of lag");
            style.label="수정"; Tick(view);
            Check(view.StyleApplicationCount==styles+1 && button.GetComponentInChildren<TMP_Text>(true).text=="수정","direct catalog label edit applies once");
            Tick(view); Check(view.StyleApplicationCount==styles+1,"unchanged edited label not reapplied");
            style.stackLabel="{level}층"; view.SetContext(floor:2);
            Check(button.GetComponentInChildren<TMP_Text>(true).text=="2층","context updates label immediately");
            styles=view.StyleApplicationCount; view.SetContext(floor:2); Tick(view);
            Check(view.StyleApplicationCount==styles,"identical context does not dirty style");
            var originalIcon=style.icon; style.icon=null; style.showLabel=false; Tick(view);
            Check(button.GetComponentInChildren<TMP_Text>(true).gameObject.activeSelf,"missing icon still shows text");
            style.icon=originalIcon; Tick(view);
            Check(!button.GetComponentInChildren<TMP_Text>(true).gameObject.activeSelf,"restoring icon reapplies label visibility");
            style.size=new Vector2(140,100); Tick(view);
            Check(((RectTransform)button.transform).rect.size==style.size,"live size edit retained");
            style.placement=ActionButtonPlacement.Fixed; style.anchor=new Vector2(.5f,.5f); Tick(view);
            styles=view.StyleApplicationCount; positions=positioner.PositionApplicationCount;
            for(int i=0;i<120;i++) Tick(view);
            Check(view.StyleApplicationCount==styles && positioner.PositionApplicationCount==positions,"stable fixed button has zero style and position applications");
            var before=button.transform.position; style.offset+=new Vector2(20,10); Tick(view);
            Check(Vector2.Distance((Vector2)(button.transform.position-before),new Vector2(20,10))<.1f,"fixed offset edit invalidates placement");
            Check(view.StyleApplicationCount==styles,"placement-only edit does not dirty appearance");

            var parent=new GameObject("Relative",typeof(RectTransform)); parent.transform.SetParent(root,false);
            button.transform.SetParent(parent.transform,false);
            style.placement=ActionButtonPlacement.Relative; style.offset=Vector2.zero; Tick(view);
            before=button.transform.position; parent.transform.localPosition+=new Vector3(25,30,0); Tick(view);
            Check(Vector2.Distance((Vector2)(button.transform.position-before),new Vector2(25,30))<.1f,"relative parent motion retained");
            var layout=parent.AddComponent<VerticalLayoutGroup>();
            // Reparent invalidates component cache after adding the layout dynamically.
            button.transform.SetParent(root,false); button.transform.SetParent(parent.transform,false);
            style.useLayoutSlot=true; style.offset=new Vector2(12,8); Tick(view);
            before=button.transform.position; int rebuilds=positioner.LayoutRebuildCount;
            for(int i=0;i<30;i++) Tick(view);
            Check(positioner.LayoutRebuildCount==rebuilds,"stable layout slot is not forcibly rebuilt every frame");
            Check(Vector3.Distance(before,button.transform.position)<.01f,"cached layout offsets do not accumulate");
            ((RectTransform)parent.transform).sizeDelta+=new Vector2(80,50); Tick(view);
            Check(positioner.LayoutRebuildCount==rebuilds+1,"parent resize invalidates layout placement");
            button.gameObject.SetActive(false); style.label="재활성"; button.gameObject.SetActive(true); view.SetContext(); Tick(view);
            Check(button.GetComponentInChildren<TMP_Text>(true).text=="재활성","disabled style edit applied on reenable");
            var audit=ActionButtonAudit.Run(); Check(audit.Errors==0,audit.Text);
            Directory.CreateDirectory("outputs/button-refresh");
            File.WriteAllText("outputs/button-refresh/tests.txt","PASS: "+checks+" checks. Moving 120 ticks: 0 style reapplications, 120 position applications. Fixed 120 ticks: 0 style/position applications. Stable layout 30 ticks: 0 forced rebuilds.\n"+audit.Text);
            UnityEngine.Object.DestroyImmediate(canvasObject); UnityEngine.Object.DestroyImmediate(catalog);
            EditorApplication.Exit(0);
        }
        catch(Exception e)
        {
            Debug.LogException(e); Directory.CreateDirectory("outputs/button-refresh"); File.WriteAllText("outputs/button-refresh/tests.txt","FAIL: "+e);
            if(canvasObject!=null)UnityEngine.Object.DestroyImmediate(canvasObject);
            if(catalog!=null)UnityEngine.Object.DestroyImmediate(catalog);
            EditorApplication.Exit(1);
        }
    }
}
