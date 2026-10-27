using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

// Deferred until import completes: never edits importers, source images, scenes or prefabs.
[InitializeOnLoad]
public sealed class SpriteAlphaMaskBuilder : AssetPostprocessor, IPreprocessBuildWithReport
{
    public const string AssetPath = "Assets/Resources/SpriteAlphaMasks.asset";
    private static bool scheduled, rebuilding;
    public int callbackOrder => 0;

    static SpriteAlphaMaskBuilder()
    {
        Schedule();
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.ExitingEditMode)
            {
                try { EnsureCurrent(); }
                catch (Exception error) { Debug.LogException(error); EditorApplication.isPlaying = false; }
            }
        };
    }

    private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] oldPaths)
    {
        // Ignore the database's own save to avoid an import loop.
        if (imported.Concat(deleted).Concat(moved).Concat(oldPaths)
            .Any(path => path != AssetPath && !path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)))
            Schedule();
    }

    private static void Schedule()
    {
        if (scheduled) return;
        scheduled = true;
        EditorApplication.delayCall += () =>
        {
            scheduled = false;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) { Schedule(); return; }
            try { EnsureCurrent(); } catch (Exception error) { Debug.LogException(error); }
        };
    }

    [MenuItem("Tools/HoneyComb/Rebuild Sprite Alpha Masks")]
    public static void Rebuild() { EnsureCurrent(true); }

    public void OnPreprocessBuild(BuildReport report) { EnsureCurrent(); }

    public static void EnsureCurrent(bool force = false)
    {
        if (rebuilding) return;
        rebuilding = true;
        try
        {
            var paths = AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets" })
                .Select(AssetDatabase.GUIDToAssetPath).Distinct().OrderBy(p => p, StringComparer.Ordinal)
                .Where(p => AssetImporter.GetAtPath(p) is TextureImporter importer &&
                    importer.textureType == TextureImporterType.Sprite).ToArray();
            var signature = new StringBuilder("SpriteAlphaMasks-v1|").Append(EditorUserBuildSettings.activeBuildTarget);
            foreach (string path in paths)
                signature.Append('|').Append(AssetDatabase.AssetPathToGUID(path)).Append(':')
                    .Append(AssetDatabase.GetAssetDependencyHash(path));
            string fingerprint = Hash128.Compute(signature.ToString()).ToString();
            var database = AssetDatabase.LoadAssetAtPath<SpriteAlphaMaskDatabase>(AssetPath);
            if (!force && database != null && database.SourceFingerprint == fingerprint) return;

            var masks = new List<SpriteAlphaMaskDatabase.Entry>();
            foreach (string path in paths)
            {
                var sprites = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().OrderBy(s => s.name).ToArray();
                if (sprites.Length == 0) continue;
                Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (texture == null) throw new InvalidOperationException("Cannot read sprite texture: " + path);
                Color32[] pixels = ReadPixels(texture);
                foreach (Sprite sprite in sprites)
                {
                    Rect rect = sprite.rect;
                    int width = Mathf.RoundToInt(rect.width), height = Mathf.RoundToInt(rect.height);
                    int x0 = Mathf.RoundToInt(rect.x), y0 = Mathf.RoundToInt(rect.y);
                    if (width <= 0 || height <= 0 || x0 < 0 || y0 < 0 || x0 + width > texture.width || y0 + height > texture.height)
                        throw new InvalidOperationException("Sprite rect outside imported texture: " + path + "/" + sprite.name);
                    var alpha = new byte[width * height];
                    for (int y = 0; y < height; y++)
                        for (int x = 0; x < width; x++)
                            alpha[y * width + x] = pixels[(y0 + y) * texture.width + x0 + x].a;
                    masks.Add(new SpriteAlphaMaskDatabase.Entry { sprite = sprite, width = width, height = height, alpha = alpha });
                }
            }
            // Build all entries successfully before replacing the previous database.
            if (database == null)
            {
                if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets", "Resources");
                database = ScriptableObject.CreateInstance<SpriteAlphaMaskDatabase>();
                database.ReplaceGeneratedData(fingerprint, masks);
                AssetDatabase.CreateAsset(database, AssetPath);
            }
            else
            {
                database.ReplaceGeneratedData(fingerprint, masks);
                EditorUtility.SetDirty(database);
            }
            AssetDatabase.SaveAssetIfDirty(database);
            Debug.Log("Sprite alpha masks updated: " + masks.Count + " sprites.");
        }
        finally { rebuilding = false; }
    }

    private static Color32[] ReadPixels(Texture2D source)
    {
        RenderTexture previous = RenderTexture.active;
        var render = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
        Texture2D readable = null;
        try
        {
            Graphics.Blit(source, render);
            RenderTexture.active = render;
            readable = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false, true);
            readable.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
            readable.Apply();
            return readable.GetPixels32();
        }
        finally
        {
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(render);
            if (readable != null) UnityEngine.Object.DestroyImmediate(readable);
        }
    }
}
