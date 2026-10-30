using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class ButtonAnchorTests
{
    private static int checks;
    private static void Check(bool ok,string message) { checks++; if(!ok)throw new Exception(message); }
    private static void Near(Vector3 value,Vector2 expected,string message) => Check(Vector2.Distance(value,expected)<.1f,message);
    public static void RunBatch()
    {
        GameObject go=null;
        ActionButtonCatalog catalog=null;
        try
        {
            catalog=UnityEngine.Object.Instantiate(Resources.Load<ActionButtonCatalog>("ActionButtonCatalog"));
            go=new GameObject("AnchorTest",typeof(RectTransform),typeof(Canvas),typeof(ActionButtonManager));
            go.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;
            go.GetComponent<ActionButtonManager>().Catalog=catalog;
            var button=ActionButtonManager.Create(null,go.transform,ActionButtonId.Combine);
            var view=button.GetComponent<ActionButtonView>();
            var style=catalog.Find(ActionButtonId.Combine);
            style.size=new Vector2(80,60); style.followGap=15; style.clampToScreen=false; style.offset=Vector2.zero;
            style.followAnchor=ActionButtonFollowAnchor.Automatic;
            var bounds=new Rect(100,200,300,100);
            view.SetScreenBounds(bounds,new Vector2(400,250),Vector2.right,15);
            Canvas.ForceUpdateCanvases(); view.Apply();
            Near(button.transform.position,new Vector2(455,250),"automatic right-side position preserved");
            int styles=view.StyleApplicationCount;
            var modes=new[]{ActionButtonFollowAnchor.Top,ActionButtonFollowAnchor.Bottom,ActionButtonFollowAnchor.Left,ActionButtonFollowAnchor.Right,ActionButtonFollowAnchor.Center};
            var expected=new[]{new Vector2(250,345),new Vector2(250,155),new Vector2(45,250),new Vector2(455,250),new Vector2(250,250)};
            for(int i=0;i<modes.Length;i++)
            {
                style.followAnchor=modes[i]; view.Apply(); Near(button.transform.position,expected[i],"catalog direction "+modes[i]);
            }
            Check(view.StyleApplicationCount==styles,"direction edits do not reapply appearance");
            style.followAnchor=ActionButtonFollowAnchor.Top; style.offset=new Vector2(3,7); view.Apply();
            Near(button.transform.position,new Vector2(253,352),"offset is applied after direction and gap");
            style.offset=Vector2.zero;
            bounds=new Rect(100,200,300,300);
            view.SetScreenBounds(bounds,new Vector2(400,350),Vector2.right,15); view.Apply();
            Near(button.transform.position,new Vector2(250,545),"top follows changed target height");
            view.SetScreenBounds(bounds,bounds.center,Vector2.left,7.5f,.5f); style.followAnchor=ActionButtonFollowAnchor.Automatic; view.Apply();
            Near(button.transform.position,new Vector2(202.5f,350),"paired automatic half-gap preserved");
            style.followAnchor=ActionButtonFollowAnchor.Top; view.Apply();
            Near(button.transform.position,new Vector2(250,545),"explicit direction uses full configured gap");
            style.followAnchor=ActionButtonFollowAnchor.Automatic;
            view.SetFollowPoint(new Vector3(200,200,0),null,true,16); view.Apply();
            Near(button.transform.position,new Vector2(200,245),"legacy world point API remains usable");
            style.followAnchor=ActionButtonFollowAnchor.Right; view.SetScreenAnchor(new Vector2(200,200),Vector2.up,16); view.Apply();
            Near(button.transform.position,new Vector2(255,200),"point-only call clears stale bounds");

            // Exercise the real Apiary controller, not only the positioning primitive.
            var controller=go.AddComponent<ApiaryActionUI>();
            typeof(ApiaryActionUI).GetField("combineButton",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(controller,button);
            var update=typeof(ApiaryActionUI).GetMethod("UpdateIconPositions",BindingFlags.Instance|BindingFlags.NonPublic);
            style.followAnchor=ActionButtonFollowAnchor.Top;
            update.Invoke(controller,new object[]{0,0,new Rect(100,200,300,100)}); view.Apply();
            Near(button.transform.position,new Vector2(250,345),"Apiary passes full target bounds to Combine");
            style.clampToScreen=true;
            view.SetScreenBounds(new Rect(-10000,-10000,20,20),Vector2.zero,Vector2.right,15);view.Apply();
            var corners=new Vector3[4]; ((RectTransform)button.transform).GetWorldCorners(corners);
            Check(corners[0].x>=style.padding-.1f && corners[0].y>=style.padding-.1f,"clamp retained with custom anchor");
            var liftStyle=catalog.Find(ActionButtonId.Lift);
            Check(liftStyle.overrideLidFollowAnchor && liftStyle.lidFollowAnchor==ActionButtonFollowAnchor.Top,"saved Apiary lid override enabled at Top");
            liftStyle.size=new Vector2(80,60); liftStyle.followGap=15; liftStyle.offset=Vector2.zero; liftStyle.clampToScreen=false;
            liftStyle.followAnchor=ActionButtonFollowAnchor.Right;
            var lidButton=ActionButtonManager.Create(null,go.transform,ActionButtonId.Lift);
            var lidView=lidButton.GetComponent<ActionButtonView>();
            var boxButton=ActionButtonManager.Create(null,go.transform,ActionButtonId.Lift);
            var boxView=boxButton.GetComponent<ActionButtonView>();
            var partBounds=new Rect(100,200,300,100);
            lidView.SetScreenBounds(partBounds,new Vector2(400,250),Vector2.right,15);
            boxView.SetScreenBounds(partBounds,new Vector2(400,250),Vector2.right,15);
            lidView.SetContext(lid:true,floor:6); boxView.SetContext(floor:5); lidView.Apply(); boxView.Apply();
            Near(lidButton.transform.position,new Vector2(250,345),"lid-only top placement");
            Near(boxButton.transform.position,new Vector2(455,250),"other lift buttons retain right placement");
            int lidStyles=lidView.StyleApplicationCount;
            liftStyle.lidFollowAnchor=ActionButtonFollowAnchor.Bottom; lidView.Apply();
            Near(lidButton.transform.position,new Vector2(250,155),"lid anchor edited live");
            Check(lidView.StyleApplicationCount==lidStyles,"lid placement edit does not reapply appearance");
            liftStyle.overrideLidFollowAnchor=false; lidView.Apply();
            Near(lidButton.transform.position,new Vector2(455,250),"disabling override inherits shared placement");
            liftStyle.overrideLidFollowAnchor=true; liftStyle.lidFollowAnchor=ActionButtonFollowAnchor.Top;
            lidView.SetScreenBounds(new Rect(100,200,300,300),new Vector2(400,350),Vector2.right,15); lidView.Apply();
            Near(lidButton.transform.position,new Vector2(250,545),"lid anchor follows changed bounds");
            lidView.SetContext(lid:false,floor:2); lidView.Apply();
            Near(lidButton.transform.position,new Vector2(455,350),"context change restores ordinary lift placement");
            var audit=ActionButtonAudit.Run(); Check(audit.Errors==0,audit.Text);
            Directory.CreateDirectory("outputs/button-anchor");
            File.WriteAllText("outputs/button-anchor/tests.txt","PASS: "+checks+" anchor checks.\n"+audit.Text);
            UnityEngine.Object.DestroyImmediate(go);UnityEngine.Object.DestroyImmediate(catalog);EditorApplication.Exit(0);
        }
        catch(Exception e)
        {
            Debug.LogException(e);Directory.CreateDirectory("outputs/button-anchor");File.WriteAllText("outputs/button-anchor/tests.txt","FAIL: "+e);
            if(go!=null)UnityEngine.Object.DestroyImmediate(go);if(catalog!=null)UnityEngine.Object.DestroyImmediate(catalog);EditorApplication.Exit(1);
        }
    }
}
