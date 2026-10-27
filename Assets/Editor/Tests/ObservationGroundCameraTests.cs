using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;

public static class ObservationGroundCameraTests
{
    static readonly BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
    static T Get<T>(object o, string f) => (T)o.GetType().GetField(f, Flags).GetValue(o);
    static void Set(object o, string f, object v) => o.GetType().GetField(f, Flags).SetValue(o, v);
    static void Call(object o, string m) => o.GetType().GetMethod(m, Flags).Invoke(o, null);
    static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }

    [MenuItem("Tools/HoneyComb/Test Ground Camera in Play %&k")]
    public static void Run()
    {
        if (!EditorApplication.isPlaying) throw new Exception("Enter Observation through Bootstrap first.");
        var camera = UnityEngine.Object.FindAnyObjectByType<ObservationCameraController>();
        var selection = UnityEngine.Object.FindAnyObjectByType<ObservationSelectionController>();
        if (camera == null || selection == null) throw new Exception("Observation required.");
        var presenter = Get<ObservationItemPresenter>(camera, "presenter");
        var observation = selection.Observation;
        var originalSlot = Get<ItemSlot>(observation, "observedSlot");
        var world = Get<RectTransform>(camera, "world");
        var viewport = Get<RectTransform>(camera, "viewport");
        var ground = Get<RectTransform>(camera, "groundImage");
        var position = world.anchoredPosition; var scale = world.localScale;
        float zoom = Get<float>(camera, "zoomRatio");
        var previousMouse = Mouse.current;
        Mouse mouse = null;
        try
        {
            mouse = InputSystem.AddDevice<Mouse>();
            float firstBottom = float.NaN;
            foreach (int count in new[] { 1, 3, 5 })
            {
                var parts = new List<ItemData> { new ItemData(9000, "Test brood", ItemKind.BroodBox) };
                for (int i = 1; i < count; i++) parts.Add(new ItemData(9000+i, "Test super", ItemKind.Super));
                var slot = new ItemSlot();
                slot.TryInsertValue(count == 1 ? (MovableItem)parts[0] : new ItemAssemblyData(parts));
                Set(observation, "observedSlot", slot);
                presenter.Show(slot.Value); Canvas.ForceUpdateCanvases(); Call(presenter, "AlignToGround");
                selection.Synchronize();
                Set(camera, "initialCameraApplied", false); Set(camera, "lastPartCount", -1);
                Call(camera, "CheckContentChanges");
                var image = ground.GetComponent<Image>();
                Check(SpriteAlphaMaskDatabase.TryGetOpaqueBottom(image.overrideSprite, out float bottom), "Ground mask missing");
                Rect rect = SpriteImageHitTest.VisibleRect(image);
                Vector3 screenGround = viewport.InverseTransformPoint(ground.TransformPoint(new Vector3(rect.center.x, rect.yMin + rect.height * bottom)));
                float gap = screenGround.y - viewport.rect.yMin;
                Check(Mathf.Abs(gap - Get<float>(camera, "initialBottomPadding")) < .05f, "Bottom padding mismatch");
                if (!float.IsNaN(firstBottom)) Check(Mathf.Abs(gap-firstBottom) < .05f, "Height changes bottom position");
                firstBottom = gap;
                Verify(selection, presenter.CurrentView.Parts[0]);
                Vector2 blank = RectTransformUtility.WorldToScreenPoint(selection.UICamera, viewport.TransformPoint(new Vector3(viewport.rect.xMin+25, viewport.rect.center.y)));
                Check(!selection.IsBlocked(blank), "Test input origin blocked");
                float baseScale = world.localScale.x;
                foreach (float wheel in new[] {120f, -120f})
                {
                    InputSystem.QueueStateEvent(mouse, new MouseState {position=blank, scroll=new Vector2(0,wheel)});
                    InputSystem.Update(); Call(camera,"HandleZoom");
                    if (wheel > 0) Check(world.localScale.x > baseScale, "Zoom in failed");
                    else Check(Mathf.Abs(world.localScale.x-baseScale)<.0001f,"Zoom out failed");
                    Verify(selection,presenter.CurrentView.Parts[0]);
                }
                Call(camera,"ResetPointer");
                InputSystem.QueueStateEvent(mouse,new MouseState {position=blank}.WithButton(MouseButton.Left));
                InputSystem.Update(); Call(camera,"HandlePan");
                float before=world.anchoredPosition.y;
                InputSystem.QueueStateEvent(mouse,new MouseState {position=blank+Vector2.up*200}.WithButton(MouseButton.Left));
                InputSystem.Update(); Call(camera,"HandlePan");
                Check(world.anchoredPosition.y>before+10,"Pan failed");
                InputSystem.QueueStateEvent(mouse,new MouseState {position=blank+Vector2.up*200});
                InputSystem.Update(); Call(camera,"HandlePan");
                Verify(selection,presenter.CurrentView.Parts[0]);
                Debug.Log($"PASS Ground Camera: {count} layers, bottom gap={gap:F3}; initial/zoom in/zoom out/pan selection.");
            }
        }
        catch(Exception e) { Debug.LogException(e); }
        finally
        {
            if(mouse!=null) InputSystem.RemoveDevice(mouse);
            if(previousMouse!=null && previousMouse.added) previousMouse.MakeCurrent();
            Set(observation,"observedSlot",originalSlot); presenter.Show(originalSlot?.Value);
            Canvas.ForceUpdateCanvases(); Call(presenter,"AlignToGround");
            selection.Synchronize(); selection.ClearSelection(); camera.RecalculateBounds();
            Set(camera,"zoomRatio",zoom); world.localScale=scale; world.anchoredPosition=position;
            Call(camera,"ResetPointer");
        }
    }
    static void Verify(ObservationSelectionController selection, UIItemPartView part)
    {
        typeof(ObservationAlphaMaskTests).GetMethod("VerifySelection",BindingFlags.NonPublic|BindingFlags.Static)
            .Invoke(null,new object[]{selection,part});
    }
}
