using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class ApiaryIconLayoutTests
{
    static int checks;
    static void Check(bool ok, string message) { checks++; if (!ok) throw new Exception(message); }
    static FieldInfo Field(string name) => typeof(ApiaryItemPresenter).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);
    public static void RunBatch()
    {
        var objects = new List<UnityEngine.Object>();
        try
        {
            var cameraGO = new GameObject("Camera", typeof(Camera)); objects.Add(cameraGO);
            var camera = cameraGO.GetComponent<Camera>(); camera.orthographic = true; camera.orthographicSize = 10;
            camera.transform.position = new Vector3(0, 0, -10);
            var presenterGO = new GameObject("Presenter", typeof(ApiaryItemPresenter)); objects.Add(presenterGO);
            var presenter = presenterGO.GetComponent<ApiaryItemPresenter>();
            Field("grid").SetValue(presenter, new ApiaryGrid(8, 1));
            var texture = new Texture2D(10,10); objects.Add(texture);
            var sprite = Sprite.Create(texture, new Rect(0,0,10,10), Vector2.one*.5f,10); objects.Add(sprite);
            var data = new[] {new ItemData(1,"lower",ItemKind.BroodBox),new ItemData(2,"upper",ItemKind.Super)};
            var map = (IDictionary)Field("partViews").GetValue(presenter);
            var active = (HashSet<ItemData>)Field("activeParts").GetValue(presenter);
            var visualType = typeof(ApiaryItemPresenter).GetNestedType("PartVisual", BindingFlags.NonPublic);
            for(int i=0;i<2;i++)
            {
                var go = new GameObject("Part",typeof(SpriteRenderer)); objects.Add(go);
                go.transform.position = new Vector3(0,i*2,0);
                var renderer = go.GetComponent<SpriteRenderer>(); renderer.sprite=sprite;
                var visual = Activator.CreateInstance(visualType,true);
                visualType.GetField("Object").SetValue(visual,go);
                visualType.GetField("Renderer").SetValue(visual,renderer);
                map.Add(data[i],visual); active.Add(data[i]);
            }
            Check(presenter.TryGetEquipmentScreenBounds(0,0,camera,out var lower,data[0]),"lower bounds available");
            Check(presenter.TryGetEquipmentScreenBounds(0,0,camera,out var upper,data[1]),"upper bounds available");
            Check(upper.center.y>lower.center.y,"each layer has independent height");
            Check(presenter.TryGetEquipmentScreenBounds(0,0,camera,out var entire),"whole equipment bounds available");
            Check(entire.yMin==lower.yMin && entire.yMax==upper.yMax,"observe bounds cover entire image");
            Check(!presenter.TryGetEquipmentScreenBounds(0,0,camera,out _,new ItemData(3,"missing",ItemKind.Lid)),"missing layer rejected");
            var canvasGO=new GameObject("Canvas",typeof(RectTransform),typeof(Canvas)); objects.Add(canvasGO);
            canvasGO.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;
            var buttonGO=new GameObject("Button",typeof(RectTransform),typeof(Image),typeof(Button)); buttonGO.transform.SetParent(canvasGO.transform,false);
            var button=buttonGO.GetComponent<Button>(); button.targetGraphic=buttonGO.GetComponent<Image>();
            var view=ActionButtonManager.Adopt(button,ActionButtonId.Observe);
            Canvas.ForceUpdateCanvases();
            var positioner=button.GetComponent<ActionButtonPositioner>();
            var style=new ActionButtonCatalog.Entry {placement=ActionButtonPlacement.FollowTarget,size=new Vector2(80,60),clampToScreen=false};
            positioner.SetScreenAnchor(new Vector2(200,200),Vector2.zero,0); positioner.Apply(style,null);
            Check(Vector2.Distance(button.transform.position,new Vector2(200,200))<.1f,"observe exactly centred");
            positioner.SetScreenAnchor(new Vector2(200,200),Vector2.right,15); positioner.Apply(style,null);
            Check(Vector2.Distance(button.transform.position,new Vector2(255,200))<.1f,"lift outside right edge with gap");
            var controllerGO=new GameObject("Controller",typeof(ObservationController)); objects.Add(controllerGO);
            var controller=controllerGO.GetComponent<ObservationController>();
            typeof(ObservationController).GetField("backButton",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(controller,button);
            typeof(ObservationController).GetMethod("Start",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(controller,null);
            Check(button.GetComponent<Canvas>().overrideSorting && button.GetComponent<Canvas>().sortingOrder>100,"back draws over inventory");
            Check(button.GetComponent<GraphicRaycaster>()!=null,"back retains raycaster on sorting canvas");
            File.WriteAllText("outputs/common-buttons/apiary-icon-tests.txt","PASS: "+checks+" layout/navigation checks");
            foreach(var o in objects) if(o!=null) UnityEngine.Object.DestroyImmediate(o);
            EditorApplication.Exit(0);
        }
        catch(Exception e) { Debug.LogException(e); File.WriteAllText("outputs/common-buttons/apiary-icon-tests.txt","FAIL: "+e); EditorApplication.Exit(1); }
    }
}
