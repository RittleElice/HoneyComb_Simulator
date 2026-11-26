using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class CellWallValidation
{
    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
    static int checks;
    static void Check(bool ok, string name) { checks++; if (!ok) throw new Exception(name); }
    static void Populate(FrameCellMesh graphic, VertexHelper vertices)
        => typeof(FrameCellMesh).GetMethod("OnPopulateMesh", Flags | BindingFlags.DeclaredOnly).Invoke(graphic, new object[] { vertices });
    static void Late(FrameCellPresenter presenter) => typeof(FrameCellPresenter).GetMethod("LateUpdate", Flags).Invoke(presenter, null);
    static T Field<T>(object target, string name) => (T)target.GetType().GetField(name, Flags).GetValue(target);

    [MenuItem("Tools/HoneyComb/검증/Run Cell Wall Validation")]
    public static void Run()
    {
        string folder = "outputs/cell-wall"; Directory.CreateDirectory(folder);
        GameObject root = null;
        try
        {
            var texture = Resources.Load<Texture2D>("CellObservationAtlas");
            Check(texture && texture.width == 256 && texture.height == 128, "source atlas import without resizing");
            var topology = FrameCellMesh.BuildJunctions(25, 12, 40, Vector2.zero);
            Check(topology.Sum(n => n.Cells.Count) == 1800, "all 300 cells contribute six corners");
            Check(topology.All(n => n.Cells.Count <= 3 && n.Cells.Distinct().Count() == n.Cells.Count), "junction deduplication");
            Check(topology.All(n => n.Owner == n.Cells.Min()), "one stable owner per shared junction");
            root = new GameObject("wall test", typeof(RectTransform));
            var graphic = root.AddComponent<FrameCellMesh>();
            graphic.Configure(3, 3, 0, 9, 40, .5f, Vector2.zero, Color.white);
            var surface = new FrameSurface(3, 3);
            graphic.SetSurface(surface);
            using (var vh = new VertexHelper())
            {
                Populate(graphic, vh); Check(vh.currentVertCount == 0, "zero construction is invisible");
                surface.TrySetConstruction(1, 1, 60); graphic.SetSurface(surface); Populate(graphic, vh);
                Check(vh.currentVertCount == 24, "isolated cell uses exactly six two-arm units");
                var vertex = new UIVertex(); vh.PopulateUIVertex(ref vertex, 0);
                Check(Math.Abs(vertex.color.a / 255f - .6f) < .005, "wall alpha follows construction");
                surface.TrySetConstruction(2, 1, 100); graphic.SetSurface(surface); Populate(graphic, vh);
                Check(vh.currentVertCount == 44, "two adjacent cells: ten shared-deduplicated junctions plus one completed fill");
                int triple = 0;
                for (int i = 0; i < vh.currentVertCount; i += 4)
                {
                    vh.PopulateUIVertex(ref vertex, i);
                    if (Math.Abs(vertex.uv0.x - 52f / 256) < .0001)
                    { triple++; Check(vertex.color.a == 255, "shared junction takes maximum, not average construction"); }
                }
                Check(triple == 2, "two shared triple units for adjacent cells");
                surface.TrySetConstruction(2, 1, 60); graphic.SetSurface(surface); Populate(graphic, vh);
                Check(vh.currentVertCount == 44, "historical completion persists at construction 60");
                surface.TrySetConstruction(2, 1, 50); graphic.SetSurface(surface); Populate(graphic, vh);
                Check(vh.currentVertCount == 40, "completion disappears at collapse threshold");
                surface.TrySetConstruction(2, 1, 0); graphic.SetSurface(surface); Populate(graphic, vh);
                Check(vh.currentVertCount == 24, "junction topology returns to closed isolated cell");
                surface.TrySetConstruction(1, 1, 100);
                Check(surface.TryAddNectar(1, 1, 7, .001f, .001f), "tiny source-specific nectar deposit");
                graphic.SetSurface(surface); Populate(graphic, vh);
                Check(vh.currentVertCount == 32, "completed fill and nectar both render");
                UIVertex left = new UIVertex(), right = new UIVertex();
                vh.PopulateUIVertex(ref left, 4); vh.PopulateUIVertex(ref right, 5);
                Check(Math.Abs(Vector3.Distance(left.position, right.position) - 57 * .3f) < .001, "tiny nectar has 30 percent minimum width");
                surface.TryClearContent(1, 1);
                Check(surface.TryAddNectar(1, 1, 8, 0, (float)(CombBalance.LiquidCapacityMl * 1000)), "full liquid deposit");
                graphic.SetSurface(surface); Populate(graphic, vh);
                vh.PopulateUIVertex(ref left, 4); vh.PopulateUIVertex(ref right, 5);
                Check(Math.Abs(Vector3.Distance(left.position, right.position) - 57) < .001, "full nectar reaches full sprite size");
                surface.TryClearContent(1, 1); graphic.SetSurface(surface); Populate(graphic, vh);
                Check(vh.currentVertCount == 28, "clearing content removes only nectar");
                int before = graphic.RefreshCount; graphic.SetSurface(surface);
                Check(graphic.RefreshCount == before, "unchanged state does not dirty mesh");
            }
            UnityEngine.Object.DestroyImmediate(root); root = null;

            var frame = new FrameData();
            root = new GameObject("range test", typeof(RectTransform));
            var meshes = new FrameCellMesh[3];
            for (int i = 0; i < 3; i++)
            {
                var child = new GameObject("range", typeof(RectTransform)); child.transform.SetParent(root.transform);
                meshes[i] = child.AddComponent<FrameCellMesh>();
                meshes[i].ConfigureArt(25, i * 100, (i + 1) * 100, 40, Vector2.zero, topology, 0, 1, .3f);
                meshes[i].SetSurface(frame.Front);
            }
            var baseline = meshes.Select(m => m.RefreshCount).ToArray();
            frame.Front.TrySetConstruction(10, 5, 60);
            foreach (var mesh in meshes) mesh.SetSurface(frame.Front);
            Check(meshes[0].RefreshCount == baseline[0] && meshes[2].RefreshCount == baseline[2] && meshes[1].RefreshCount == baseline[1] + 1,
                "local cell update rebuilds only affected mesh range");
            frame.Front.TrySetConstruction(10, 4, 80);
            foreach (var mesh in meshes) mesh.SetSurface(frame.Front);
            Check(meshes[0].RefreshCount == baseline[0] + 1, "cross-range shared junction invalidates its owner");
            foreach (var mesh in meshes) mesh.SetSurface(frame.Back);
            using (var vh = new VertexHelper()) foreach (var mesh in meshes) { Populate(mesh, vh); Check(vh.currentVertCount == 0, "back side displays its own state"); }
            var watch = System.Diagnostics.Stopwatch.StartNew();
            for (int i = 0; i < 10000; i++) foreach (var mesh in meshes) mesh.SetSurface(frame.Back);
            watch.Stop();
            File.WriteAllText(folder + "/timing.txt", "10,000 unchanged whole-facing-surface comparisons (300 cells plus shared boundary dependencies): " + watch.Elapsed.TotalMilliseconds + " ms. No mesh rebuild. Normal presenter skips comparisons unless revision changes.");
            UnityEngine.Object.DestroyImmediate(root); root = null;
            RenderExample(folder);
            File.WriteAllText(folder + "/result.txt", "PASS " + checks + " cell wall checks. Art/topology/alpha/completion/storage/clear/face/range invalidation/render/prefab binding.\n");
            // Existing hit-coordinate, mirror, corner adjustment, drag-flip, cell-state regressions.
            FrameLayoutTests.Run();
        }
        catch (Exception e)
        {
            File.WriteAllText(folder + "/result.txt", "FAIL " + e); Debug.LogException(e);
            if (Application.isBatchMode) EditorApplication.Exit(1); throw;
        }
        finally { if (root) UnityEngine.Object.DestroyImmediate(root); }
    }

    static void RenderExample(string folder)
    {
        var canvasGo = new GameObject("Preview", typeof(RectTransform), typeof(Canvas));
        var cameraGo = new GameObject("Preview camera", typeof(Camera));
        var camera = cameraGo.GetComponent<Camera>();
        camera.transform.position = new Vector3(0, 0, -100); camera.orthographic = true; camera.orthographicSize = 500;
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.16f, .16f, .16f);
        var rt = new RenderTexture(1800, 1000, 24); camera.targetTexture = rt;
        var canvas = canvasGo.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = camera; canvas.planeDistance = 100;
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Item/InteriorSlot/Frame/PF_Frame_Hand.prefab");
        var frame = new FrameData();
        var view = (UIItemPartView)ItemVisualFactory.Create(new ItemData(987651, "wall preview", ItemKind.Frame, frame), 0, ItemViewContext.Hand, null, prefab, canvasGo.transform);
        var presenter = view.GetComponent<FrameCellPresenter>();
        Check(presenter && presenter.Frame == frame, "existing hand prefab binds new renderer");
        int[,] cells = { {9,6}, {10,6}, {11,6}, {10,7}, {11,7}, {11,8}, {12,8}, {12,5}, {13,5} };
        for (int i = 0; i < cells.GetLength(0); i++) frame.Front.TrySetConstruction(cells[i,0], cells[i,1], 70);
        for (int i = 0; i < 4; i++)
        {
            frame.Front.TrySetConstruction(6 + i * 3, 3, (byte)(25 + 25 * i));
            frame.Front.TrySetConstruction(6 + i * 3, 10, 100);
            frame.Front.TryAddNectar(6 + i * 3, 10, i, 0, (float)(CombBalance.LiquidCapacityMl * 1000 * (i == 0 ? .00001 : i / 3.0)));
        }
        frame.Front.TrySetConstruction(12, 8, 100);
        Canvas.ForceUpdateCanvases(); presenter.Rebuild(); Canvas.ForceUpdateCanvases(); camera.Render();
        var old = RenderTexture.active; RenderTexture.active = rt;
        var capture = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false);
        capture.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0); capture.Apply();
        File.WriteAllBytes(folder + "/unity-preview.png", capture.EncodeToPNG());
        RenderTexture.active = old;
        frame.Flip(); Late(presenter);
        Check(presenter.transform.localScale.x < 0, "existing facing-side mirror retained");
        UnityEngine.Object.DestroyImmediate(capture); UnityEngine.Object.DestroyImmediate(canvasGo);
        UnityEngine.Object.DestroyImmediate(cameraGo); rt.Release(); UnityEngine.Object.DestroyImmediate(rt);
    }
}
