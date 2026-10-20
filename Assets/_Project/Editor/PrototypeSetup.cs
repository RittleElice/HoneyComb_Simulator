using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace HoneyComb.Editor
{
    public static class PrototypeSetup
    {
        private const string Root = "Assets/_Project";
        private const string ScenePath = Root + "/Scenes/Simulation.unity";

        [MenuItem("HoneyComb/Setup Android Prototype")]
        public static void Setup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            string[] folders = {
                "Scenes", "Scripts/Simulation/Model", "Scripts/Simulation/Systems",
                "Scripts/Simulation/Time", "Scripts/Unity/Bootstrap",
                "Scripts/Unity/Configuration", "Scripts/Unity/Presentation",
                "Scripts/Persistence", "Settings", "Prefabs", "Art", "Tests/EditMode"
            };
            foreach (string folder in folders) Directory.CreateDirectory(Root + "/" + folder);
            WriteIfMissing(Root + "/Scripts/Simulation/HoneyComb.Simulation.asmdef",
                "{\n  \"name\": \"HoneyComb.Simulation\",\n  \"rootNamespace\": \"HoneyComb.Simulation\",\n  \"noEngineReferences\": true\n}\n");
            WriteIfMissing(Root + "/Scripts/Unity/HoneyComb.Unity.asmdef",
                "{\n  \"name\": \"HoneyComb.Unity\",\n  \"rootNamespace\": \"HoneyComb.Unity\",\n  \"references\": [\"HoneyComb.Simulation\", \"Unity.ugui\"]\n}\n");
            WriteIfMissing(Root + "/Scripts/Persistence/HoneyComb.Persistence.asmdef",
                "{\n  \"name\": \"HoneyComb.Persistence\",\n  \"rootNamespace\": \"HoneyComb.Persistence\",\n  \"references\": [\"HoneyComb.Simulation\"],\n  \"noEngineReferences\": true\n}\n");
            AssetDatabase.Refresh();

            if (!File.Exists(ScenePath))
            {
                const string source = "Assets/Scenes/SampleScene.unity";
                if (!AssetDatabase.CopyAsset(source, ScenePath))
                {
                    Debug.LogError("HoneyComb: Could not copy SampleScene. Settings were not applied.");
                    return;
                }
            }
            EditorSceneManager.OpenScene(ScenePath);
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.allowedAutorotateToPortrait = true;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = false;
            PlayerSettings.allowedAutorotateToLandscapeRight = false;
            EditorSettings.serializationMode = SerializationMode.ForceText;

            var scenes = EditorBuildSettings.scenes.ToList();
            scenes.RemoveAll(scene => scene.path == ScenePath);
            scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
            foreach (var scene in scenes)
                if (scene.path == "Assets/Scenes/SampleScene.unity") scene.enabled = false;
            EditorBuildSettings.scenes = scenes.ToArray();
            AssetDatabase.SaveAssets();
            Debug.Log("HoneyComb setup complete: portrait orientation, Simulation scene, and assembly boundaries. Set Game view to 9:16 and activate an Android Build Profile.");
        }

        private static void WriteIfMissing(string path, string contents)
        {
            if (!File.Exists(path)) File.WriteAllText(path, contents);
        }
    }
}
