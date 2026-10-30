using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class ActionButtonSetup
{
    public const string CatalogPath = "Assets/Resources/ActionButtonCatalog.asset";
    private static Sprite Icon(string name) => AssetDatabase.LoadAllAssetsAtPath("Assets/Art/UI/" + name + ".png").OfType<Sprite>().FirstOrDefault();

    [MenuItem("Tools/HoneyComb/Install Common Action Buttons")]
    public static void Install()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var setup = EditorSceneManager.GetSceneManagerSetup();
        try
        {
            var catalog = AssetDatabase.LoadAssetAtPath<ActionButtonCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<ActionButtonCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }
            var actionPrefab = AssetDatabase.LoadAssetAtPath<Button>("Assets/Prefabs/ActionButton.prefab");
            var slotPrefab = AssetDatabase.LoadAssetAtPath<Button>("Assets/Prefabs/InventorySlot.prefab");
            string[] labels = { "관찰", "놓기", "결합하기", "들기", "소비장 넣기", "소비장 꺼내기", "격왕판 배치하기", "뒤로가기",
                "새 게임", "부지 해금", "빈 칸", "놓기", "결합하기", "들기", "들기" };
            foreach (ActionButtonId id in Enum.GetValues(typeof(ActionButtonId)))
            {
                var e = catalog.Find(id);
                if (e == null)
                {
                    ActionButtonId? copy = id == ActionButtonId.ObservationPlace ? ActionButtonId.Place :
                        id == ActionButtonId.ObservationCombine ? ActionButtonId.Combine :
                        id == ActionButtonId.ObservationLift || id == ActionButtonId.TakeTopAttachment ? ActionButtonId.Lift : (ActionButtonId?)null;
                    e = copy.HasValue && catalog.Find(copy.Value) != null
                        ? JsonUtility.FromJson<ActionButtonCatalog.Entry>(JsonUtility.ToJson(catalog.Find(copy.Value)))
                        : new ActionButtonCatalog.Entry { size = new Vector2(128,96), showLabel = false,
                            icon = Icon(id == ActionButtonId.Observe ? "UI_Observe" : id == ActionButtonId.Lift || id == ActionButtonId.TakeFrame || id == ActionButtonId.Back ? "UI_UP" : "UI_Down") };
                    e.id = id; e.label = labels[(int)id];
                    if ((int)id >= 11) e.followGap = 16;
                    if (id == ActionButtonId.ObservationLift) e.stackLabel = "{level}층부터 위 모두 들기";
                    if (id == ActionButtonId.Back) { e.placement = ActionButtonPlacement.Fixed; e.anchor = new Vector2(0,1); e.offset = new Vector2(80,-225); e.iconRotation = 90; }
                    if (id == ActionButtonId.NewGame) { e.placement = ActionButtonPlacement.Fixed; e.size = new Vector2(160,30); e.icon = null; e.showLabel = true; }
                    if (id == ActionButtonId.UnlockLand) { e.placement = ActionButtonPlacement.Relative; e.useLayoutSlot = true; e.size = new Vector2(220,40); e.icon = null; e.showLabel = true; e.clampToScreen = false; }
                    if (id == ActionButtonId.InventorySlot) { e.placement = ActionButtonPlacement.Grid; e.size = new Vector2(80,80); e.icon = null; e.showLabel = true; e.clampToScreen = false; }
                    catalog.entries.Add(e);
                }
                if (e.prefab == null && id != ActionButtonId.NewGame && id != ActionButtonId.Back)
                    e.prefab = id == ActionButtonId.InventorySlot ? slotPrefab : actionPrefab;
                if (e.followGap < 0) e.followGap = (int)id <= 3 ? 15 : 16;
                if (id == ActionButtonId.Lift || id == ActionButtonId.ObservationLift)
                {
                    if (string.IsNullOrEmpty(e.lidLabel)) e.lidLabel = "뚜껑 들기";
                    if (string.IsNullOrEmpty(e.stackLabel)) e.stackLabel = id == ActionButtonId.Lift ? "{level}층부터 들기" : "{level}층부터 위 모두 들기";
                }
            }
            EditorUtility.SetDirty(catalog);
            foreach (string name in new[] { "MainMenu", "Apiary", "Observation", "SharedUI" })
            {
                var scene = EditorSceneManager.OpenScene("Assets/Scenes/" + name + ".unity");
                var roots = scene.GetRootGameObjects();
                foreach (var canvas in roots.SelectMany(g => g.GetComponentsInChildren<Canvas>(true)).Where(c => c.isRootCanvas))
                {
                    var manager = canvas.GetComponent<ActionButtonManager>() ?? canvas.gameObject.AddComponent<ActionButtonManager>();
                    manager.Catalog = catalog;
                }
                foreach (var button in roots.SelectMany(g => g.GetComponentsInChildren<Button>(true)))
                {
                    if (name == "SharedUI" && button.name == "HandSlot")
                    {
                        if (button.onClick.GetPersistentEventCount() != 0) throw new Exception("HandSlot has click listeners; cannot remove.");
                        foreach (var component in roots.SelectMany(g => g.GetComponentsInChildren<Component>(true)))
                        {
                            if (component == null || component is Transform || component == button) continue;
                            var iterator = new SerializedObject(component).GetIterator();
                            while (iterator.Next(true))
                                if (iterator.propertyType == SerializedPropertyType.ObjectReference && iterator.objectReferenceValue == button)
                                    throw new Exception("HandSlot Button is referenced by " + component.GetType().Name);
                        }
                        UnityEngine.Object.DestroyImmediate(button);
                    }
                    else if ((name == "MainMenu" && button.name == "NewGameButton") || (name == "Observation" && button.name == "BackButton"))
                    {
                        var binding = button.GetComponent<ActionButtonBinding>() ?? button.gameObject.AddComponent<ActionButtonBinding>();
                        binding.Id = name == "MainMenu" ? ActionButtonId.NewGame : ActionButtonId.Back;
                    }
                }
                EditorSceneManager.SaveScene(scene);
            }
            AssetDatabase.SaveAssets();
        }
        finally
        {
            if (!Application.isBatchMode && setup.Length > 0 && setup.All(s => !string.IsNullOrEmpty(s.path)))
                EditorSceneManager.RestoreSceneManagerSetup(setup);
        }
    }

    public static void InstallBatch()
    {
        try
        {
            Install();
            var report = ActionButtonAudit.Run();
            Directory.CreateDirectory("outputs/common-buttons");
            File.WriteAllText("outputs/common-buttons/connections.txt", report.Text);
            if (report.Errors != 0) throw new Exception(report.Text);
            EditorApplication.Exit(0);
        }
        catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
    }
}
