using HoneyComb.Unity.Configuration;
using UnityEditor;
using UnityEngine;
namespace HoneyComb.Editor
{
    public static class ApiarySetup
    {
        [MenuItem("HoneyComb/Setup Apiary System")]
        public static void Setup()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode) return;
            var master = AssetDatabase.LoadAssetAtPath<GameMasterData>("Assets/_Project/Settings/GameMasterData.asset");
            if(master == null) { Debug.LogError("Run Setup Time System first."); return; }
            EnsureConfig(master);
            Selection.activeObject = master.apiaryConfig;
            Debug.Log("Apiary ready. Edit ApiaryConfig, then start Play.");
        }
        public static void EnsureConfig(GameMasterData master)
        {
            if(master.apiaryConfig != null) return;
            const string path = "Assets/_Project/Settings/ApiaryConfig.asset";
            var config = AssetDatabase.LoadAssetAtPath<ApiaryConfig>(path);
            if(config == null) { config = ScriptableObject.CreateInstance<ApiaryConfig>(); AssetDatabase.CreateAsset(config,path); }
            Undo.RecordObject(master,"Connect apiary master data");
            master.apiaryConfig = config;
            EditorUtility.SetDirty(master); AssetDatabase.SaveAssets();
        }
    }
}
