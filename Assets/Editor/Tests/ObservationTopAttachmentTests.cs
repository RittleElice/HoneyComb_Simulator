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


