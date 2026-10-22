using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Toolbars;
using UnityEngine;
using UnityEngine.SceneManagement;

// Editor-only: keep this file in Assets/Editor.
[InitializeOnLoad]
public static class SceneSwitcher
{
    private const string Root = "Assets/Scenes";
    private const string ToolbarPath = "HoneyComb/씬 바꾸기";

    static SceneSwitcher()
    {
        EditorApplication.projectChanged += Refresh;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
        EditorSceneManager.activeSceneChangedInEditMode += OnActiveSceneChanged;
        EditorSceneManager.sceneOpened += OnSceneOpened;
        EditorSceneManager.sceneClosed += OnSceneChanged;
        EditorSceneManager.sceneSaved += OnSceneChanged;
        EditorSceneManager.sceneDirtied += OnSceneChanged;
        EditorSceneManager.newSceneCreated += OnNewScene;
        EditorApplication.delayCall += Refresh;
    }

    private static bool CanSwitch => !EditorApplication.isPlayingOrWillChangePlaymode
                                     && !EditorApplication.isPlaying;

    [MainToolbarElement(ToolbarPath, defaultDockPosition = MainToolbarDockPosition.Left)]
    public static MainToolbarElement CreateToolbar()
    {
        var scene = SceneManager.GetActiveScene();
        var name = string.IsNullOrEmpty(scene.name) ? "새 씬" : scene.name;
        return new MainToolbarDropdown(
            new MainToolbarContent($"씬 바꾸기({name})",
                CanSwitch ? "Assets/Scenes의 씬 열기" : "Play 모드에서는 씬을 변경할 수 없습니다."),
            rect => CreateMenu().DropDown(rect)) { enabled = CanSwitch };
    }

    // Query on each click: additions, deletions and renames never leave a stale cache.
    private static GenericMenu CreateMenu()
    {
        var menu = new GenericMenu();
        if (!CanSwitch)
        {
            menu.AddDisabledItem(new GUIContent("Play 모드에서는 씬을 변경할 수 없습니다."));
            return menu;
        }

        var paths = AssetDatabase.IsValidFolder(Root)
            ? AssetDatabase.FindAssets("t:Scene", new[] { Root })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(IsScenePath).OrderBy(path => path, StringComparer.OrdinalIgnoreCase).ToArray()
            : Array.Empty<string>();
        var activePath = SceneManager.GetActiveScene().path;
        foreach (var path in paths)
        {
            // Relative subfolders distinguish scenes with identical names.
            var label = path.Substring(Root.Length + 1);
            label = label.Substring(0, label.Length - ".unity".Length);
            var guid = AssetDatabase.AssetPathToGUID(path);
            menu.AddItem(new GUIContent(label), path == activePath,
                () => OpenScenePath(AssetDatabase.GUIDToAssetPath(guid)));
        }
        if (paths.Length == 0)
            menu.AddDisabledItem(new GUIContent("Assets/Scenes에 씬이 없습니다."));
        return menu;
    }

    private static bool IsScenePath(string path) =>
        !string.IsNullOrEmpty(path) && path.StartsWith(Root + "/", StringComparison.Ordinal)
        && path.EndsWith(".unity", StringComparison.OrdinalIgnoreCase);

    private static bool SceneExists(string path) => IsScenePath(path)
        && AssetDatabase.LoadAssetAtPath<SceneAsset>(path) != null;

    private static void OpenScenePath(string path)
    {
        if (!CanSwitch) return;
        if (!SceneExists(path))
        {
            Debug.LogWarning($"씬이 존재하지 않거나 Assets/Scenes 밖으로 이동했습니다: {path}");
            Refresh();
            return;
        }
        // Clicking the active scene must not close other additively opened scenes.
        if (SceneManager.GetActiveScene().path == path) return;
        var guid = AssetDatabase.AssetPathToGUID(path);
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        // Recheck after the modal dialog, including moves/deletions during a save callback.
        path = AssetDatabase.GUIDToAssetPath(guid);
        if (!CanSwitch || !SceneExists(path)) return;
        try
        {
            EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
        }
        Refresh();
    }

    [MenuItem("Tools/씬 바꾸기/씬 목록...", false, 0)]
    public static void ShowSceneMenu() => CreateMenu().ShowAsContext();

    // Preserve existing menu paths and public entry points; use the same guarded opener.
    [MenuItem("Tools/씬 바꾸기/Bootstrap", false, 20)]
    public static void OpenBootstrap() => OpenScenePath(Root + "/Bootstrap.unity");
    [MenuItem("Tools/씬 바꾸기/MainMenu", false, 21)]
    public static void OpenMainMenu() => OpenScenePath(Root + "/MainMenu.unity");
    [MenuItem("Tools/씬 바꾸기/Apiary", false, 22)]
    public static void OpenApiary() => OpenScenePath(Root + "/Apiary.unity");
    [MenuItem("Tools/씬 바꾸기/HiveManagement", false, 23)]
    public static void OpenHiveManagement() => OpenScenePath(Root + "/HiveManagement.unity");

    [MenuItem("Tools/씬 바꾸기/씬 목록...", true)]
    private static bool ValidateList() => CanSwitch;
    [MenuItem("Tools/씬 바꾸기/Bootstrap", true)]
    private static bool ValidateBootstrap() => CanSwitch && SceneExists(Root + "/Bootstrap.unity");
    [MenuItem("Tools/씬 바꾸기/MainMenu", true)]
    private static bool ValidateMainMenu() => CanSwitch && SceneExists(Root + "/MainMenu.unity");
    [MenuItem("Tools/씬 바꾸기/Apiary", true)]
    private static bool ValidateApiary() => CanSwitch && SceneExists(Root + "/Apiary.unity");
    [MenuItem("Tools/씬 바꾸기/HiveManagement", true)]
    private static bool ValidateHiveManagement() => CanSwitch && SceneExists(Root + "/HiveManagement.unity");

    private static void Refresh() => MainToolbar.Refresh(ToolbarPath);
    private static void OnPlayModeChanged(PlayModeStateChange state) => Refresh();
    private static void OnActiveSceneChanged(Scene previous, Scene next) => Refresh();
    private static void OnSceneOpened(Scene scene, OpenSceneMode mode) => Refresh();
    private static void OnSceneChanged(Scene scene) => Refresh();
    private static void OnNewScene(Scene scene, NewSceneSetup setup, NewSceneMode mode) => Refresh();
}
