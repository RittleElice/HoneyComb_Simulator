using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public static class UnifiedButtonTests
{
    private static int checks;
    private static void Check(bool ok, string message) { checks++; if (!ok) throw new Exception(message); }
    private static void Set(object owner, string field, object value) => owner.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(owner, value);

    public static void RunBatch()
    {
        try
        {
            Directory.CreateDirectory("outputs/common-buttons");
            var audit = ActionButtonAudit.Run();
            Check(audit.Errors == 0, audit.Text);
            File.WriteAllText("outputs/common-buttons/connections.txt", audit.Text);
            TestStylesAndGrid();
            TestInventoryClick();
            ObservationSelectionTests.RunAll();
            ItemAssemblyTests.RunAll();
            File.WriteAllText("outputs/common-buttons/unified-tests.txt", "PASS: " + checks + " unified button checks; ObservationSelectionTests and ItemAssemblyTests passed.");
            EditorApplication.Exit(0);
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            File.WriteAllText("outputs/common-buttons/unified-tests.txt", "FAIL: " + e);
            EditorApplication.Exit(1);
        }
    }
    private static void TestStylesAndGrid()
    {
        var catalog = UnityEngine.Object.Instantiate(Resources.Load<ActionButtonCatalog>("ActionButtonCatalog"));
        var canvasObject = new GameObject("UnifiedTests", typeof(RectTransform), typeof(Canvas), typeof(ActionButtonManager));
        canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        canvasObject.GetComponent<ActionButtonManager>().Catalog = catalog;
        try
        {
            var root = canvasObject.transform;
            Check(catalog.entries.Count == 15, "15 distinct button settings");
            var originalLift = catalog.Find(ActionButtonId.Lift);
            var observationLift = catalog.Find(ActionButtonId.ObservationLift);
            originalLift.label = "양봉장 설정";
            Check(observationLift.label != originalLift.label, "scene styles independent");
            foreach (var entry in catalog.entries)
            {
                if (entry.id == ActionButtonId.InventorySlot) continue;
                var fallback = AssetDatabase.LoadAssetAtPath<Button>("Assets/Prefabs/ActionButton.prefab");
                var button = ActionButtonManager.Create(fallback, root, entry.id, "legacy text");
                var view = button.GetComponent<ActionButtonView>();
                int clicked = 0;
                button.onClick.AddListener(() => clicked++);
                ActionButtonManager.Adopt(button, entry.id, "legacy text");
                button.onClick.Invoke();
                Check(clicked == 1, "single original listener " + entry.id);
                entry.showLabel = true; entry.label = "수정 " + entry.id;
                view.Apply();
                Check(button.GetComponentInChildren<TMP_Text>(true).text == entry.label, "catalog label wins " + entry.id);
                UnityEngine.Object.DestroyImmediate(button.gameObject);
            }
            var lift = ActionButtonManager.Create(originalLift.prefab, root, ActionButtonId.ObservationLift);
            var liftView = lift.GetComponent<ActionButtonView>();
            observationLift.stackLabel = "{level}층 전체"; observationLift.lidLabel = "덮개";
            liftView.SetContext(floor: 3);
            Check(lift.GetComponentInChildren<TMP_Text>(true).text == "3층 전체", "floor template");
            liftView.SetContext(lid: true, floor: 4);
            Check(lift.GetComponentInChildren<TMP_Text>(true).text == "덮개", "lid template precedence");
            observationLift.followGap = 42; observationLift.clampToScreen = false;
            observationLift.size = new Vector2(80,60);
            liftView.SetScreenAnchor(new Vector2(200,200), Vector2.right, 15);
            liftView.Apply();
            Check(Mathf.Abs(lift.transform.position.x - 282) < .1f, "catalog gap overrides controller gap");

            var gridObject = new GameObject("Grid", typeof(RectTransform), typeof(GridLayoutGroup));
            gridObject.transform.SetParent(root, false);
            var grid = gridObject.GetComponent<GridLayoutGroup>();
            var slotStyle = catalog.Find(ActionButtonId.InventorySlot);
            var slots = Enumerable.Range(0,12).Select(_ => ActionButtonManager.Create(slotStyle.prefab, gridObject.transform, ActionButtonId.InventorySlot)).ToArray();
            Canvas.ForceUpdateCanvases(); LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)gridObject.transform);
            var before = slots.Select(b => b.transform.localPosition).ToArray();
            for (int pass=0; pass<5; pass++)
            {
                foreach (var slot in slots) slot.GetComponent<ActionButtonView>().Apply();
                LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)gridObject.transform);
            }
            Check(slots.Select((b,i) => Vector3.Distance(b.transform.localPosition,before[i]) < .01f).All(x=>x), "grid has no per-frame drift");
            Check(grid.constraintCount == 6 && grid.cellSize == new Vector2(80,80), "original six-column grid retained");
            Check(slots.All(b=>!b.GetComponent<LayoutElement>().ignoreLayout), "all slots participate in layout");
            Check(slots[0].transform.localPosition.x != slots[1].transform.localPosition.x && slots[0].transform.localPosition.y != slots[6].transform.localPosition.y, "grid rows and columns distinct");
            slotStyle.size = new Vector2(100,90); slotStyle.gridSpacing = new Vector2(12,14); slotStyle.gridColumns = 4;
            foreach (var slot in slots) slot.GetComponent<ActionButtonView>().Apply();
            LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)gridObject.transform);
            Check(grid.cellSize == slotStyle.size && grid.spacing == slotStyle.gridSpacing && grid.constraintCount == 4, "grid catalog edits applied");
            var first = slots[0].GetComponent<ActionButtonView>(); slotStyle.label = "비었음"; slotStyle.occupiedLabel = "";
            first.SetContext(filled:false);
            first.Apply(); // Flush the deferred Catalog edit as LateUpdate does at runtime.
            Check(slots[0].GetComponentInChildren<TMP_Text>(true).text == "비었음", "empty label configurable");
            first.SetContext(filled:true);
            Check(slots[0].GetComponentInChildren<TMP_Text>(true).text == "", "occupied slot label remains empty");

            // Use a different prefab to prove Catalog (not the caller's fallback) owns creation.
            originalLift.prefab = slotStyle.prefab;
            var overridden = ActionButtonManager.Create(observationLift.prefab, root, ActionButtonId.Lift);
            Check(overridden.name.StartsWith("InventorySlot"), "catalog prefab overrides legacy reference");
            originalLift.label = "";
            overridden.GetComponent<ActionButtonView>().Apply();
            Check(overridden.GetComponentInChildren<TMP_Text>(true).text == "", "explicit empty text is respected");
        }
        finally { UnityEngine.Object.DestroyImmediate(canvasObject); UnityEngine.Object.DestroyImmediate(catalog); }
    }
    private static void TestInventoryClick()
    {
        var scene = EditorSceneManager.OpenPreviewScene("Assets/Scenes/SharedUI.unity");
        var bootObject = new GameObject("TestBootstrap");
        bootObject.SetActive(false);
        var boot = bootObject.AddComponent<GameBootstrap>();
        var instance = typeof(GameBootstrap).GetField("<Instance>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic);
        var previous = instance.GetValue(null);
        instance.SetValue(null, boot);
        var world = new GameWorld();
        Set(boot,"<GameWorld>k__BackingField",world);
        try
        {
            var presenter = scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<InventoryItemPresenter>(true)).Single();
            presenter.Refresh();
            var buttons = presenter.GetComponentsInChildren<Button>(true);
            Check(buttons.Length == world.Inventory.Capacity, "one registered button per inventory slot");
            Check(buttons.All(b=>b.GetComponent<ActionButtonView>()?.Id == ActionButtonId.InventorySlot), "all inventory instances registered");
            var item = world.Inventory.GetSlot(0).Value;
            Check(item != null && world.Hand.Slot.IsEmpty, "inventory initial conditions");
            buttons[0].onClick.Invoke();
            Check(ReferenceEquals(world.Hand.Slot.Value,item) && world.Inventory.GetSlot(0).IsEmpty, "inventory click still picks up item");
            buttons[0].onClick.Invoke();
            Check(world.Hand.Slot.IsEmpty && ReferenceEquals(world.Inventory.GetSlot(0).Value,item), "inventory second click returns item");
            Check(scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<HandGestureController>(true)).Single().GetComponent<Button>() == null, "hand gesture retained without unused Button");
        }
        finally
        {
            instance.SetValue(null, previous);
            UnityEngine.Object.DestroyImmediate(bootObject);
            EditorSceneManager.ClosePreviewScene(scene);
        }
    }
}
