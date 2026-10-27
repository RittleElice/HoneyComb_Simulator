using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;

// Explicit menu tests. Never open/save scenes, exit the Editor, or change existing source artwork.
public static class ObservationAlphaMaskTests
{
    private static int checks;
    private static void Check(bool value, string message)
    {
        if (!value) throw new Exception("Alpha selection: " + message);
        checks++;
    }
    private static T Get<T>(object o, string field) => (T)o.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(o);
    private static object Call(object o, string method, params object[] args) => o.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(o, args);
    private static Vector2 ScreenPoint(Image image, Vector2 uv, Camera camera)
    {
        Rect r = SpriteImageHitTest.VisibleRect(image);
        return RectTransformUtility.WorldToScreenPoint(camera, image.rectTransform.TransformPoint(
            new Vector3(r.xMin + r.width * uv.x, r.yMin + r.height * uv.y)));
    }

    [MenuItem("Tools/HoneyComb/Test Alpha Selection in Play %&h")]
    public static void RunPlay()
    {
        if (!EditorApplication.isPlaying) throw new InvalidOperationException("Enter Observation in Play Mode with a placed brood box first.");
        var selection = UnityEngine.Object.FindAnyObjectByType<ObservationSelectionController>();
        var camera = UnityEngine.Object.FindAnyObjectByType<ObservationCameraController>();
        if (selection == null || camera == null) throw new InvalidOperationException("Observation scene is required.");
        var presenter = Get<ObservationItemPresenter>(selection, "presenter");
        if (presenter.CurrentView == null || presenter.CurrentView.Parts.Count == 0)
            throw new InvalidOperationException("Place equipment first.");
        var part = presenter.CurrentView.Parts[0];
        var world = Get<RectTransform>(camera, "world");
        var viewport = Get<RectTransform>(camera, "viewport");
        Vector2 savedPosition = world.anchoredPosition;
        Vector3 savedScale = world.localScale;
        float savedZoom = Get<float>(camera, "zoomRatio");
        var previousMouse = Mouse.current;
        Mouse mouse = null;
        checks = 0;
        try
        {
            mouse = InputSystem.AddDevice<Mouse>();
            Vector2 blank = RectTransformUtility.WorldToScreenPoint(selection.UICamera,
                viewport.TransformPoint(new Vector3(viewport.rect.xMin + 25, viewport.rect.center.y)));
            Check(!selection.IsBlocked(blank), "camera test origin is not blocked by UI/hand");
            VerifySelection(selection, part);
            InputSystem.QueueStateEvent(mouse, new MouseState { position = blank, scroll = new Vector2(0, 120) });
            InputSystem.Update();
            Call(camera, "HandleZoom");
            Check(world.localScale.x > savedScale.x, "wheel zoom enlarges world");
            VerifySelection(selection, part);
            InputSystem.QueueStateEvent(mouse, new MouseState { position = blank, scroll = new Vector2(0, -120) });
            InputSystem.Update();
            Call(camera, "HandleZoom");
            Check(Mathf.Abs(world.localScale.x - savedScale.x) < .0001f, "wheel zoom out restores scale");
            VerifySelection(selection, part);

            Call(camera, "ResetPointer");
            InputSystem.QueueStateEvent(mouse, new MouseState { position = blank }.WithButton(MouseButton.Left));
            InputSystem.Update(); Call(camera, "HandlePan");
            Vector2 beforePan = world.anchoredPosition;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = blank + Vector2.up * 400 }.WithButton(MouseButton.Left));
            InputSystem.Update(); Call(camera, "HandlePan");
            Check(world.anchoredPosition.y > beforePan.y + 100, "held mouse movement pans camera vertically");
            InputSystem.QueueStateEvent(mouse, new MouseState { position = blank + Vector2.up * 400 });
            InputSystem.Update(); Call(camera, "HandlePan");
            VerifySelection(selection, part);

            var hand = UnityEngine.Object.FindAnyObjectByType<HandItemPresenter>();
            var handView = hand != null ? Get<ItemAssemblyView>(hand, "currentView") : null;
            if (handView == null) throw new InvalidOperationException("Hold a frame to also verify hand/camera sharing.");
            Image handImage = handView.GetComponentsInChildren<Image>().First(i => i.sprite != null);
            Camera handCamera = (Camera)Call(hand, "GetUICamera");
            Rect whole = handImage.rectTransform.rect;
            Vector2 letterbox = RectTransformUtility.WorldToScreenPoint(handCamera,
                handImage.rectTransform.TransformPoint(new Vector3(whole.center.x, whole.yMax - 5)));
            Check(RectTransformUtility.RectangleContainsScreenPoint(handImage.rectTransform, letterbox, handCamera), "regression point inside old hand rectangle");
            Check(!SpriteImageHitTest.Contains(handImage, letterbox, handCamera), "hand letterbox is not an opaque hit");
            Check(!hand.BlocksObservationInput(letterbox), "hand letterbox releases equipment selection");
            Check(!(bool)Call(camera, "IsInputBlocked", letterbox), "hand letterbox releases camera input");
            Vector2 opaque = ScreenPoint(handImage, new Vector2(.5f, .5f), handCamera);
            Check(hand.BlocksObservationInput(opaque), "opaque hand still captures input");
            Check((bool)Call(camera, "IsInputBlocked", opaque), "camera respects opaque hand");
            Debug.Log("PASS Alpha Play: " + checks + " checks; zoom in/out, input-driven vertical pan, inside/outside selection, hand/camera shared alpha.");
        }
        catch (Exception error) { Debug.LogException(error); }
        finally
        {
            if (mouse != null) InputSystem.RemoveDevice(mouse);
            if (previousMouse != null && previousMouse.added) previousMouse.MakeCurrent();
            typeof(ObservationCameraController).GetField("zoomRatio", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(camera, savedZoom);
            world.localScale = savedScale; world.anchoredPosition = savedPosition;
            Call(camera, "ResetPointer"); selection.ClearSelection();
        }
    }

    private static void VerifySelection(ObservationSelectionController selection, UIItemPartView part)
    {
        Canvas.ForceUpdateCanvases();
        var area = part.GetComponent<ObservationHitArea>();
        bool selected = false;
        for (int y = 9; y >= 1 && !selected; y--)
            for (int x = 1; x < 10 && !selected; x++)
            {
                Vector2 point = ScreenPoint(part.Image, new Vector2(x / 10f, y / 10f), selection.UICamera);
                if (!area.Contains(point, selection.UICamera) || selection.IsBlocked(point)) continue;
                selection.SelectAt(point);
                selected = ReferenceEquals(selection.Selection.Part, part.Part);
            }
        Check(selected, "visible equipment selects at current camera transform");
        Vector2 outside = ScreenPoint(part.Image, new Vector2(-.05f, .5f), selection.UICamera);
        Check(!area.Contains(outside, selection.UICamera), "outside current image rejects at current transform");
    }

    private static string scratch, png;
    private static int importStage;
    private static double deadline;

    [MenuItem("Tools/HoneyComb/Test Automatic Alpha Refresh %&j")]
    public static void RunImportRefresh()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || scratch != null)
            throw new InvalidOperationException("Run outside Play Mode and wait for the previous test.");
        string folder = "AlphaMaskTest_" + Guid.NewGuid().ToString("N");
        scratch = "Assets/" + folder;
        AssetDatabase.CreateFolder("Assets", folder);
        png = scratch + "/changing.png";
        try
        {
            WriteTestImage(8, 4, false);
            AssetDatabase.ImportAsset(png, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(png);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.filterMode = FilterMode.Point;
            importer.isReadable = false;
            importer.SaveAndReimport();
            importStage = 0; deadline = EditorApplication.timeSinceStartup + 45;
            EditorApplication.update += WaitForRefresh;
        }
        catch { Cleanup(); throw; }
    }

    private static void WriteTestImage(int width, int height, bool right)
    {
        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        try
        {
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                    texture.SetPixel(x, y, (x >= width / 2) == right ? Color.white : Color.clear);
            texture.Apply(); File.WriteAllBytes(png, texture.EncodeToPNG());
        }
        finally { UnityEngine.Object.DestroyImmediate(texture); }
    }

    private static void WaitForRefresh()
    {
        try
        {
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Automatic mask refresh timed out");
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(png);
            if (sprite == null || !SpriteAlphaMaskDatabase.TrySample(sprite, new Vector2(.25f, .5f), out float left) ||
                !SpriteAlphaMaskDatabase.TrySample(sprite, new Vector2(.75f, .5f), out float right)) return;
            if (importStage == 0 && left > .9f && right < .1f)
            {
                WriteTestImage(4, 8, true);
                importStage = 1;
                AssetDatabase.ImportAsset(png, ImportAssetOptions.ForceSynchronousImport);
            }
            else if (importStage == 1 && left < .1f && right > .9f && sprite.rect.width == 4 && sprite.rect.height == 8)
            {
                if (sprite.texture.isReadable) throw new Exception("Source unexpectedly became readable");
                Debug.Log("PASS Alpha Import: automatic mask regeneration after image pixels AND aspect ratio replacement; Read/Write remains disabled.");
                Cleanup();
            }
        }
        catch (Exception error) { Debug.LogException(error); Cleanup(); }
    }

    private static void Cleanup()
    {
        EditorApplication.update -= WaitForRefresh;
        if (scratch != null) AssetDatabase.DeleteAsset(scratch);
        scratch = null; png = null;
    }
}
