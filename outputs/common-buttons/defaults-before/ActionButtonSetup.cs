using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class ActionButtonSetup
{
    private static Sprite Icon(string name) => AssetDatabase.LoadAllAssetsAtPath("Assets/Art/UI/" + name + ".png").OfType<Sprite>().FirstOrDefault();
    [MenuItem("Tools/HoneyComb/Install Common Action Buttons")]
    public static void Install()
    {
        const string path = "Assets/Resources/ActionButtonCatalog.asset";
        if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets", "Resources");
        var catalog = AssetDatabase.LoadAssetAtPath<ActionButtonCatalog>(path);
        if (catalog == null)
        {
            catalog = ScriptableObject.CreateInstance<ActionButtonCatalog>();
            string[] names = { "관찰", "놓기", "결합하기", "들기", "소비장 넣기", "소비장 꺼내기", "격왕판 배치하기", "뒤로가기" };
            foreach (ActionButtonId id in Enum.GetValues(typeof(ActionButtonId)))
            {
                bool layout = id == ActionButtonId.Observe || id == ActionButtonId.Lift || id == ActionButtonId.InsertFrame || id == ActionButtonId.TakeFrame;
                var entry = new ActionButtonCatalog.Entry { id = id, label = names[(int)id],
                    icon = Icon(id == ActionButtonId.Observe ? "UI_Observe" : id == ActionButtonId.Lift || id == ActionButtonId.TakeFrame || id == ActionButtonId.Back ? "UI_UP" : "UI_Down"),
                    placement = id == ActionButtonId.Back ? ActionButtonPlacement.Fixed : layout ? ActionButtonPlacement.Relative : ActionButtonPlacement.FollowTarget,
                    useLayoutSlot = layout, size = new Vector2(128, 96), showLabel = true };
                if (id == ActionButtonId.Back) { entry.anchor = new Vector2(0, 1); entry.offset = new Vector2(80, -225); entry.iconRotation = 90; }
                catalog.entries.Add(entry);
            }
            AssetDatabase.CreateAsset(catalog, path);
        }
        foreach (var name in new[] { "Observation", "Apiary" })
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/" + name + ".unity");
            foreach (var canvas in scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Canvas>(true)).Where(c => c.isRootCanvas))
            {
                var manager = canvas.GetComponent<ActionButtonManager>() ?? canvas.gameObject.AddComponent<ActionButtonManager>();
                manager.Catalog = catalog; EditorUtility.SetDirty(manager);
            }
            EditorSceneManager.SaveScene(scene);
        }
        AssetDatabase.SaveAssets();
        File.WriteAllText("outputs/common-buttons/install.txt", "PASS: catalog and Observation/Apiary Canvas managers installed.");
    }
    public static void InstallBatch()
    {
        try { Install(); EditorApplication.Exit(0); }
        catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
    }
}
