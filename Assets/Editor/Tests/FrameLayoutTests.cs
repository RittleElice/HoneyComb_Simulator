using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class FrameLayoutTests
{
    static int checks;
    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
    static void Check(bool ok, string label) { checks++; if (!ok) throw new Exception(label); }
    static void Set(object o,string field,object value) => o.GetType().GetField(field,Flags).SetValue(o,value);
    static T Get<T>(object o,string field) => (T)o.GetType().GetField(field,Flags).GetValue(o);
    static void Call(object o,string method) => o.GetType().GetMethod(method,Flags).Invoke(o,null);
    [MenuItem("Tools/HoneyComb/검증/Run Frame Layout Tests")]
    public static void Run()
    {
        string folder="outputs/frame-layout";Directory.CreateDirectory(folder);
        GameObject canvasGo=null,gestureGo=null,cameraGo=null;RenderTexture rt=null;
        try
        {
            canvasGo=new GameObject("Layout test canvas",typeof(RectTransform),typeof(Canvas));
            var canvas=canvasGo.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceCamera;
            cameraGo=new GameObject("Layout test camera",typeof(Camera));var camera=cameraGo.GetComponent<Camera>();
            camera.transform.position=new Vector3(0,0,-100);camera.orthographic=true;camera.orthographicSize=550;
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.15f,.17f,.2f);
            rt=new RenderTexture(1200,1100,24);camera.targetTexture=rt;canvas.worldCamera=camera;canvas.planeDistance=100;
            var wrapper=new GameObject("Gesture wrapper",typeof(RectTransform));wrapper.transform.SetParent(canvasGo.transform,false);
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Item/InteriorSlot/Frame/PF_Frame_Hand.prefab");
            var view=(UIItemPartView)ItemVisualFactory.Create(new ItemData(987650,"layout",ItemKind.Frame,new FrameData()),0,ItemViewContext.Hand,null,prefab,wrapper.transform);
            var presenter=view.GetComponent<FrameCellPresenter>();var f=presenter.Frame;
            f.Front.SetAllConstruction(100);f.Back.SetAllConstruction(80);
            f.Front.TryAddNectar(0,0,0,100,100);f.Back.TryAddPollen(24,11,100);
            var sprite=view.Image.sprite;Canvas.ForceUpdateCanvases();
            var area=Get<RectTransform>(presenter,"cellArea");
            foreach(bool flip in new[]{false,true})foreach(var side in new[]{FrameSide.Front,FrameSide.Back})
            {
                f.SetFacingSide(side);Set(presenter,"cellOffset",new Vector2(31,-17));Set(presenter,"cellScale",new Vector2(.73f,1.13f));Set(presenter,"flipCellsX",flip);
                presenter.Rebuild();Canvas.ForceUpdateCanvases();
                Check(Mathf.Sign(view.transform.localScale.x)==(side==FrameSide.Back?-1:1),"face mirrors image");
                Check(view.Image.sprite==sprite,"original sprite reused without asset edit");
                var visual=Get<RectTransform>(presenter,"visualRoot");float scale=Get<float>(presenter,"displayScale");var geometry=new CombGeometry();
                for(int y=0;y<f.Height;y++)for(int x=0;x<f.Width;x++)
                {
                    geometry.Center(x,y,out double cx,out double cz);
                    // Independently calculate where configured geometry should be displayed.
                    Vector2 expected=area.rect.center+new Vector2(31+(float)(cx-210)*scale*.73f*(flip?-1:1),-17+(float)(cz-100)*scale*1.13f);
                    Check(presenter.TryGetCellAtLocalPoint(expected,out var id)&&id.Column==x&&id.Row==y&&id.Side==side,"configured cell hit identity");
                    Vector3 world=area.TransformPoint(expected);
                    Vector2 screen=RectTransformUtility.WorldToScreenPoint(camera,world);
                    Check(RectTransformUtility.ScreenPointToLocalPointInRectangle(area,screen,camera,out var local)&&presenter.TryGetCellAtLocalPoint(local,out id)&&id.Column==x&&id.Row==y,"mirrored screen hit identity");
                }
                Check(!presenter.TryGetCellAtLocalPoint(new Vector2(10000,10000),out _),"outside rejected");
            }
            // Re-created / rebound back-side frames must mirror without starting a drag.
            f.SetFacingSide(FrameSide.Back);presenter.BindItem(view.Part);Check(view.transform.localScale.x<0,"back binding mirror");
            Set(presenter,"cellOffset",Vector2.zero);Set(presenter,"cellScale",Vector2.one);Set(presenter,"flipCellsX",false);
            foreach(var side in new[]{FrameSide.Front,FrameSide.Back})
            {
                f.SetFacingSide(side);presenter.Rebuild();Canvas.ForceUpdateCanvases();camera.Render();RenderTexture.active=rt;
                var png=new Texture2D(rt.width,rt.height,TextureFormat.RGB24,false);png.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0);png.Apply();
                File.WriteAllBytes(folder+"/"+side+".png",png.EncodeToPNG());UnityEngine.Object.DestroyImmediate(png);RenderTexture.active=null;
            }
            gestureGo=new GameObject("Flip action test",typeof(RectTransform));var action=gestureGo.AddComponent<FrameFlipAction>();
            Set(action,"visual",(RectTransform)wrapper.transform);Set(action,"currentFrame",f);Set(action,"baseScale",Vector3.one);
            foreach(float angle in new[]{0f,60f,120f,180f,240f,360f,-120f,-180f})
            {
                Set(action,"angle",angle);Call(action,"ApplyAngle");Call(presenter,"LateUpdate");
                bool back=Mathf.Cos(angle*Mathf.Deg2Rad)<0;
                Check(f.FacingSide==(back?FrameSide.Back:FrameSide.Front),"edge-on content switch");
                Check(wrapper.transform.localScale.x>=0,"gesture never double-mirrors");
                Check(view.transform.localScale.x*(back?-1:1)>0,"prefab face sign during gesture");
            }
            Set(action,"dragStartSide",FrameSide.Front);Set(action,"dragging",true);action.CancelDrag();Call(presenter,"LateUpdate");
            Check(f.FacingSide==FrameSide.Front&&view.transform.localScale.x>0,"cancel restores starting side");
            Set(action,"angle",180f);Set(action,"dragging",true);Set(action,"snapDuration",0f);action.EndDrag();Call(presenter,"LateUpdate");
            Check(f.FacingSide==FrameSide.Back&&wrapper.transform.localScale.x>0&&view.transform.localScale.x<0,"flip end mirrors once");
            CombGeometryTests.RunAll();CellStateTests.RunAll();
            File.WriteAllText(folder+"/result.txt","PASS "+checks+" checks: prefab binding, front/back sprite reflection, offset/nonuniform scale/local mirror, all 600 cell identities and screen hit tests per configuration, gesture direction/edge switch/cancel/end. Geometry and cell-state regressions passed.");
            Debug.Log("FRAME LAYOUT PASS "+checks);
        }
        catch(Exception e){File.WriteAllText(folder+"/result.txt",e.ToString());Debug.LogException(e);if(Application.isBatchMode)EditorApplication.Exit(1);throw;}
        finally
        {
            if(gestureGo)UnityEngine.Object.DestroyImmediate(gestureGo);
            if(canvasGo)UnityEngine.Object.DestroyImmediate(canvasGo);
            if(cameraGo)UnityEngine.Object.DestroyImmediate(cameraGo);
            if(rt){rt.Release();UnityEngine.Object.DestroyImmediate(rt);}
        }
        if(Application.isBatchMode)EditorApplication.Exit(0);
    }
}
