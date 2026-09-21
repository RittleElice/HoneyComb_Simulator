using HoneyComb.Unity.Configuration;
using UnityEditor;
using UnityEngine;
namespace HoneyComb.Editor
{
    public static class EnvironmentSetup
    {
        [MenuItem("HoneyComb/Setup Environment System")]
        public static void Setup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            const string path = "Assets/_Project/Settings/GameMasterData.asset";
            var master = AssetDatabase.LoadAssetAtPath<GameMasterData>(path);
            if (master == null)
            {
                Debug.LogError("Run HoneyComb > Setup Time System first.");
                return;
            }
            EnsureConfig(master);
            Selection.activeObject = master.environmentConfig;
            Debug.Log("Environment ready. Edit EnvironmentConfig and restart Play. Each clock tick updates the environment once.");
        }
        public static void EnsureConfig(GameMasterData master)
        {
            if (master.environmentConfig != null) return;
            const string path = "Assets/_Project/Settings/EnvironmentConfig.asset";
            var config = AssetDatabase.LoadAssetAtPath<EnvironmentConfig>(path);
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<EnvironmentConfig>();
                AssetDatabase.CreateAsset(config, path);
            }
            Undo.RecordObject(master, "Connect environment master data");
            master.environmentConfig = config;
            EditorUtility.SetDirty(master);
            AssetDatabase.SaveAssets();
        }
    }
}
