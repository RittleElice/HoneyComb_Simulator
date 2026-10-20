using HoneyComb.Unity.Bootstrap;
using HoneyComb.Unity.Configuration;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace HoneyComb.Editor
{
    public static class TimeSetup
    {
        [MenuItem("HoneyComb/Setup Time System")]
        public static void Setup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            const string assetPath = "Assets/_Project/Settings/GameMasterData.asset";
            var data = AssetDatabase.LoadAssetAtPath<GameMasterData>(assetPath);
            if (data == null)
            {
                data = ScriptableObject.CreateInstance<GameMasterData>();
                AssetDatabase.CreateAsset(data, assetPath);
            }
            EnvironmentSetup.EnsureConfig(data);
            ApiarySetup.EnsureConfig(data);
            var scene = EditorSceneManager.OpenScene("Assets/_Project/Scenes/Simulation.unity");
            var runner = Object.FindAnyObjectByType<SimulationRunner>();
            if (runner == null) runner = new GameObject("Simulation").AddComponent<SimulationRunner>();
            runner.Configure(data);
            EditorUtility.SetDirty(runner);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Selection.activeObject = data;
            Debug.Log("Time system ready. Edit GameMasterData, then press Play.");
        }
    }
}



