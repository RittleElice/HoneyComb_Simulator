using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class ObservationTopAttachmentTests
{
    private static int checks;
    private static void Check(bool ok, string message) { checks++; if (!ok) throw new Exception(message); }
    private static object Field(object o, string name) => o.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(o);
    public static void RunRenderOrderBatch()
    {
        try
        {
            var scene = EditorSceneManager.OpenPreviewScene("Assets/Scenes/SharedUI.unity");
            try
            {
                var registry = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<ItemPrefabRegistry>(true)).Single();
                foreach (var kind in new[] { ItemKind.BroodBox, ItemKind.Super })
                {
                    var box = new ItemData(100, "RenderBox", kind);
                    var queen = new ItemData(101, "RenderQueen", ItemKind.QueenExcluder);
                    var frame = new ItemData(102, "RenderFrame", ItemKind.Frame);
                    Check(box.TopSlot.TryInsertValue(queen), "insert queen");
                    Check(box.Frames.GetSlot(0).TryInsertValue(frame), "insert frame");
                    var ground = new ItemSlot(); var inventory = new ItemSlot();
                    ground.TryInsertValue(box);
                    for (int cycle = 0; cycle < 3; cycle++)
                    {
                        var host = new GameObject("RenderTestCanvas", typeof(RectTransform), typeof(Canvas));
                        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(host, scene);
                        try
                        {
                            var view = ItemVisualFactory.InstantiatePrefab(registry.GetPrefab(kind, ItemViewContext.Observation), kind, ItemViewContext.Observation, registry, host.transform).GetComponent<UIItemPartView>();
                            var top = view.GetComponent<ObservationTopAttachmentView>();
                            typeof(ObservationTopAttachmentView).GetField("prefabRegistry", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(top, registry);
                            view.Bind(box, 0);
                            var contents = view.GetComponent<ObservationInteriorContentsView>();
                            var layout = view.GetComponent<ObservationFrameSlotLayout>();
                            var overlay = new GameObject("SelectionOverlay", typeof(RectTransform));
                            overlay.transform.SetParent(view.transform, false);
                            contents.Initialize(box, layout, registry, null);
                            Transform area = top.CurrentView.transform.parent;
                            Check(contents.VisualRoot.GetSiblingIndex() < area.GetSiblingIndex(), kind + " initial/recreated order");
                            Check(area.GetSiblingIndex() < overlay.transform.GetSiblingIndex(), "overlay remains above attachment");
                            int index = contents.VisualRoot.GetSiblingIndex();
                            top.Refresh(); top.UpdateRenderOrder();
                            Check(index == contents.VisualRoot.GetSiblingIndex(), "repeated refresh stable");
                            var held = new ItemSlot();
                            box.Frames.GetSlot(0).TryMoveTo(held); held.TryMoveTo(box.Frames.GetSlot(0));
                            Check(contents.TryGetFrameVisual(0, frame, out var rect), "frame remove/reinsert visual");
                            Check(contents.VisualRoot.GetSiblingIndex() < area.GetSiblingIndex(), "frame recreation order");
                            var selection = new ObservationSelection();
                            Check(selection.SelectTopAttachment(ground, box), "select queen");
                            Check(ObservationActions.Execute(ObservationAction.TakeTopAttachment, selection, ground, held), "take queen");
                            Check(ReferenceEquals(held.Value, queen), "queen identity");
                            Check(ObservationActions.PlaceTopAttachment(ground, held), "replace queen");
                            Check(contents.VisualRoot.GetSiblingIndex() < area.GetSiblingIndex(), "queen recreation order");
                            // Nested local parent: compare the branches, not incomparable child indices.
                            var wrapper = new GameObject("NestedAttachment", typeof(RectTransform));
                            wrapper.transform.SetParent(view.transform, false);
                            area.SetParent(wrapper.transform, false);
                            contents.VisualRoot.SetAsLastSibling(); top.UpdateRenderOrder();
                            Check(contents.VisualRoot.GetSiblingIndex() < wrapper.transform.GetSiblingIndex(), "nested parent order");
                            // Independent sorting canvases must not silently alter global sorting/UI.
                            var canvas = wrapper.AddComponent<Canvas>(); canvas.overrideSorting = true; canvas.sortingOrder = 25;
                            contents.VisualRoot.SetAsLastSibling(); index = contents.VisualRoot.GetSiblingIndex();
                            top.UpdateRenderOrder();
                            Check(contents.VisualRoot.GetSiblingIndex() == index && canvas.sortingOrder == 25, "independent canvas unchanged");
                            UnityEngine.Object.DestroyImmediate(canvas);
                            area.SetParent(host.transform, false); index = contents.VisualRoot.GetSiblingIndex();
                            top.UpdateRenderOrder();
                            Check(contents.VisualRoot.GetSiblingIndex() == index, "external parent unchanged");
                        }
                        finally
                        {
                            // PreviewScene edit-mode objects do not receive runtime OnDestroy reliably.
                            foreach (var topView in host.GetComponentsInChildren<ObservationTopAttachmentView>(true))
                                topView.BindItem(null);
                            foreach (var interiorView in host.GetComponentsInChildren<ObservationInteriorContentsView>(true))
                                interiorView.Initialize(null, null, null, null);
                            UnityEngine.Object.DestroyImmediate(host);
                        }
                        Check(ground.TryMoveTo(inventory) && inventory.TryMoveTo(ground), "inventory round trip");
                        Check(ReferenceEquals(ground.Value, box) && ReferenceEquals(box.Frames.GetSlot(0).Value, frame) && ReferenceEquals(box.TopSlot.Value, queen), "round trip preserves data");
                    }
                }
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
            File.WriteAllText("render-order-tests-result.txt", "PASS: " + checks + " checks");
            EditorApplication.Exit(0);
        }
        catch (Exception e) { Debug.LogException(e); File.WriteAllText("render-order-tests-result.txt", "FAIL: " + e); EditorApplication.Exit(1); }
    }

    public static void RunAlphaBatch()
    {
        try
        {
            var scene = EditorSceneManager.OpenPreviewScene("Assets/Scenes/Observation.unity");
            var shared = EditorSceneManager.OpenPreviewScene("Assets/Scenes/SharedUI.unity");
            try
            {
                var controller = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<ObservationSelectionController>(true)).Single();
                var presenter = (ObservationItemPresenter)Field(controller, "presenter");
                var registry = shared.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<ItemPrefabRegistry>(true)).Single();
                typeof(ObservationItemPresenter).GetField("prefabRegistry", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(presenter, registry);
                foreach (var kind in new[] { ItemKind.BroodBox, ItemKind.Super })
                {
                    var box = new ItemData(700, "AlphaBox", kind);
                    box.TopSlot.TryInsertValue(new ItemData(701, "AlphaQueen", ItemKind.QueenExcluder));
                    var slot = new ItemSlot(); slot.TryInsertValue(box);
                    presenter.Show(slot.Value);
                    var top = presenter.CurrentView.Parts.Single(p => ReferenceEquals(p.Part, box)).GetComponent<ObservationTopAttachmentView>();
                    typeof(ObservationTopAttachmentView).GetField("prefabRegistry", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(top, registry);
                    top.BindItem(box);
                    // Preview scenes have no rendered camera; use an overlay canvas for screen coordinates.
                    var host = new GameObject("AlphaTestCanvas", typeof(RectTransform), typeof(Canvas));
                    UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(host, scene);
                    host.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
                    presenter.CurrentView.transform.SetParent(host.transform, false);
                    presenter.CurrentView.transform.localScale = Vector3.one;
                    typeof(ObservationSelectionController).GetField("inputArea", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(controller, host.GetComponent<RectTransform>());
                    Canvas.ForceUpdateCanvases();
                    VerifyAttachmentHits(controller, presenter, slot, box);
                }
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); EditorSceneManager.ClosePreviewScene(shared); }
            File.WriteAllText("attachment-alpha-result.txt", "PASS: " + checks + " checks");
            try { ObservationSelectionTests.RunAll(); File.WriteAllText("observation-tests-result.txt", "PASS"); }
            catch (Exception e) { File.WriteAllText("observation-tests-result.txt", "FAIL: " + e); Debug.LogWarning(e); }
            ObservationPlayInputTests.BeginBatch();
        }
        catch (Exception e) { Debug.LogException(e); File.WriteAllText("attachment-alpha-result.txt", "FAIL: " + e); EditorApplication.Exit(1); }
    }

    private static void VerifyAttachmentHits(ObservationSelectionController controller,
        ObservationItemPresenter presenter, ItemSlot slot, ItemData box)
    {
        var part = presenter.CurrentView.Parts.Single(p => ReferenceEquals(p.Part, box));
        var top = part.GetComponent<ObservationTopAttachmentView>();
        var image = top.CurrentView.Image;
        var hit = typeof(ObservationSelectionController).GetMethod("TrySelectTopAttachment", BindingFlags.NonPublic | BindingFlags.Instance);
        var frameHit = typeof(ObservationSelectionController).GetMethod("TrySelectFrameSlot", BindingFlags.NonPublic | BindingFlags.Instance);
        var area = part.GetComponent<ObservationHitArea>();
        Rect rect = SpriteImageHitTest.VisibleRect(image);
        int opaque = 0, transparent = 0, side = 0;
        for (int y = 1; y < 20; y++)
        for (int x = 1; x < 20; x++)
        {
            var uv = new Vector2(x / 20f, y / 20f);
            Check(SpriteAlphaMaskDatabase.TrySample(image.overrideSprite, uv, out float alpha), "queen generated mask available");
            var point = RectTransformUtility.WorldToScreenPoint(controller.UICamera,
                image.rectTransform.TransformPoint(new Vector3(rect.xMin + uv.x * rect.width, rect.yMin + uv.y * rect.height)));
            Check(SpriteImageHitTest.TryGetNormalizedPoint(image, point, controller.UICamera, out var sampledUV), "screen point maps into sprite");
            Check(SpriteAlphaMaskDatabase.TrySample(image.overrideSprite, sampledUV, out alpha), "screen point mask sample");
            controller.Selection.Clear();
            bool selected = (bool)hit.Invoke(controller, new object[] { slot, point });
            if (alpha >= .1f)
            {
                opaque++;
                Check(selected && controller.Selection.Kind == ObservationSelectionKind.TopAttachment, "opaque queen selected");
                Check((bool)hit.Invoke(controller, new object[] { slot, point }) && controller.Selection.Kind == ObservationSelectionKind.None, "repeat opaque click toggles selection");
            }
            else
            {
                transparent++;
                Check(!selected, "transparent queen passes through");
                if (area.Contains(point, controller.UICamera))
                {
                    side++;
                    Check(!(bool)frameHit.Invoke(controller, new object[] { slot, point }), "covered frames remain blocked at transparent point");
                    Check(controller.Selection.SelectPart(slot, box), "underlying equipment remains selectable");
                }
            }
        }
        Check(opaque > 0 && transparent > 0 && side > 0, $"fixture coverage: {opaque} opaque, {transparent} transparent, {side} underlying box");
        Debug.Log($"Attachment alpha regression: {opaque} opaque, {transparent} transparent, {side} underlying box points");
    }

    public static void RunBatch()
    {
        try
        {
            var target = new ItemSlot(); var hand = new ItemSlot();
            var lower = new ItemData(1, "Lower", ItemKind.BroodBox);
            var upper = new ItemData(2, "Upper", ItemKind.Super);
            var queen = new ItemData(3, "Queen", ItemKind.QueenExcluder);
            target.TryInsertValue(new ItemAssemblyData(new[] { lower, upper })); hand.TryInsertValue(queen);
            Check(ObservationActions.CanPlaceTopAttachment(target, hand), "placement without selection");
            Check(ObservationActions.PlaceTopAttachment(target, hand), "placement succeeds");
            Check(ReferenceEquals(upper.TopSlot.Value, queen) && lower.TopSlot.IsEmpty && hand.IsEmpty, "only exposed top receives exact item");
            Check(!ObservationActions.CanPlaceTopAttachment(target, hand), "empty hand rejected");
            var state = new ObservationSelection();
            Check(state.SelectTopAttachment(target, upper), "attachment selection");
            Check(ObservationActions.Available(state, target, hand) == ObservationAction.TakeTopAttachment, "empty hand take");
            hand.TryInsertValue(new ItemData(4, "Frame", ItemKind.Frame));
            Check(!ObservationActions.Execute(ObservationAction.TakeTopAttachment, state, target, hand), "occupied hand rejected");
            hand.TryMoveTo(new ItemSlot());
            Check(ObservationActions.Execute(ObservationAction.TakeTopAttachment, state, target, hand), "take succeeds");
            Check(ReferenceEquals(hand.Value, queen) && upper.TopSlot.IsEmpty, "take preserves identity");
            Check(!state.Revalidate(target), "removed attachment invalidates selection");
            ObservationActions.PlaceTopAttachment(target, hand);
            state.SelectTopAttachment(target, upper);
            upper.TopSlot.TakeValue();
            upper.TopSlot.TryInsertValue(new ItemData(5, "Replacement", ItemKind.QueenExcluder));
            Check(!state.Revalidate(target), "replacement invalidates old selection");
            lower.TopSlot.TryInsertValue(queen);
            Check(!state.SelectTopAttachment(target, lower), "covered lower box rejected");
            var lid = new ItemSlot(); lid.TryInsertValue(new ItemData(6, "Lid", ItemKind.Lid)); target.TryCombineFrom(lid);
            Check(ObservationSelection.GetExposedBox(target) == null, "lid covers boxes");
            Check(!ObservationActions.CanPlaceTopAttachment(target, hand), "covered placement rejected");
            var scene = EditorSceneManager.OpenPreviewScene("Assets/Scenes/Observation.unity");
            var shared = EditorSceneManager.OpenPreviewScene("Assets/Scenes/SharedUI.unity");
            try
            {
                var ui = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<ObservationTopAttachmentActionUI>(true)).Single();
                foreach (string name in new[] { "selection", "presenter", "positioner", "actionLayer", "buttonPrefab" })
                    Check(Field(ui, name) != null, "Inspector reference: " + name);
                var selection = (ObservationSelectionController)Field(ui, "selection");
                var observation = selection.Observation;
                var menu = (ObservationActionUI)Field(ui, "positioner");
                var solo = new ItemSlot(); var box = new ItemData(7, "Box", ItemKind.BroodBox);
                lower.TopSlot.TakeValue(); solo.TryInsertValue(box); Check(box.TopSlot.TryInsertValue(queen), "UI fixture attachment insertion");
                typeof(ObservationController).GetField("observedSlot", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(observation, solo);
                typeof(ObservationActionUI).GetField("handSlot", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(menu, new ItemSlot());
                selection.Selection.SelectTopAttachment(solo, box); menu.RefreshActions();
                var button = (Button)Field(menu, "actionButton");
                Check(button != null && button.GetComponentInChildren<TMPro.TMP_Text>().text == "들기", "selected attachment lift label");
                var previous = GameBootstrap.Instance;
                var go = new GameObject("TopAttachmentTestBootstrap"); go.SetActive(false);
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, scene);
                var bootstrap = go.AddComponent<GameBootstrap>(); var world = new GameWorld();
                typeof(GameBootstrap).GetProperty("GameWorld").SetValue(bootstrap, world);
                typeof(GameBootstrap).GetProperty("Instance").SetValue(null, bootstrap);
                try
                {
                    typeof(ObservationActionUI).GetField("handSlot", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(menu, world.Hand.Slot);
                    menu.RefreshActions();
                    ((Button)Field(menu, "actionButton")).onClick.Invoke();
                    Check(ReferenceEquals(world.Hand.Slot.Value, queen) && box.TopSlot.IsEmpty, "actual lift click");
                    var presenter = (ObservationItemPresenter)Field(ui, "presenter");
                    var registry = shared.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<ItemPrefabRegistry>(true)).Single();
                    typeof(ObservationItemPresenter).GetField("prefabRegistry", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(presenter, registry); presenter.Show(solo.Value);
                    var tick = typeof(ObservationTopAttachmentActionUI).GetMethod("LateUpdate", BindingFlags.NonPublic | BindingFlags.Instance);
                    tick.Invoke(ui, null);
                    var place = (Button)Field(ui, "placeButton");
                    Check(place != null && place.gameObject.activeSelf, "placement button with no selection");
                    var held = world.Hand.Slot.TakeValue();
                    world.Hand.Slot.TryInsertValue(new ItemData(8, "New Queen", ItemKind.QueenExcluder));
                    place.onClick.Invoke();
                    Check(box.TopSlot.IsEmpty, "stale hand click rejected");
                    tick.Invoke(ui, null); place.onClick.Invoke();
                    Check(!box.TopSlot.IsEmpty && world.Hand.Slot.IsEmpty, "refreshed placement click");
                    tick.Invoke(ui, null);
                    Check(!place.gameObject.activeSelf, "OnChanged hides occupied placement");

                    selection.Selection.SelectTopAttachment(solo, box); menu.RefreshActions();
                    var stale = (Button)Field(menu, "actionButton");
                    box.TopSlot.TakeValue(); box.TopSlot.TryInsertValue(held);
                    stale.onClick.Invoke();
                    Check(world.Hand.Slot.IsEmpty && ReferenceEquals(box.TopSlot.Value, held), "stale attachment click rejected");
                }
                finally { typeof(GameBootstrap).GetProperty("Instance").SetValue(null, previous); UnityEngine.Object.DestroyImmediate(go); }
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); EditorSceneManager.ClosePreviewScene(shared); }
            File.WriteAllText("top-attachment-tests-result.txt", "PASS: " + checks + " checks");
            ItemAssemblyTests.RunAll();
            try { ItemVisualTests.RunAll(); File.WriteAllText("visual-regression-result.txt", "PASS"); }
            catch (Exception e) { File.WriteAllText("visual-regression-result.txt", "FAIL: " + e); Debug.LogWarning(e.Message); }
            ObservationSelectionTests.RunAll();
            File.WriteAllText("observation-tests-result.txt", "PASS: ObservationSelectionTests.RunAll");
            ObservationPlayInputTests.BeginBatch();
        }
        catch (Exception e) { Debug.LogException(e); File.WriteAllText("top-attachment-tests-result.txt", "FAIL: " + e); EditorApplication.Exit(1); }
    }
}


