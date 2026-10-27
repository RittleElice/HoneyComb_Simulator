using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

[CustomEditor(typeof(ItemPrefabCatalog)), CanEditMultipleObjects]
public class ItemPrefabCatalogEditor : Editor
{
    private readonly HashSet<ItemPrefabCatalog> pending = new HashSet<ItemPrefabCatalog>();

    private void OnEnable()
    {
        Undo.undoRedoPerformed += QueueTargets;
        QueueTargets();
    }

    private void OnDisable()
    {
        Undo.undoRedoPerformed -= QueueTargets;
        EditorApplication.delayCall -= Flush;
    }

    public override void OnInspectorGUI()
    {
        EditorGUI.BeginChangeCheck();
        DrawDefaultInspector();
        if (EditorGUI.EndChangeCheck())
            QueueTargets();

        EditorGUILayout.HelpBox(
            "Catalog의 Sprite 변경은 프리팹에도 자동 반영됩니다. " +
            "서로 다른 이미지는 별도 프리팹에 등록하세요. " +
            "이미지를 비우면 프리팹의 기존 이미지를 유지합니다.", MessageType.Info);
        if (GUILayout.Button("프리팹 이미지 동기화"))
            QueueTargets();
    }

    private void QueueTargets()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;
        foreach (Object obj in targets)
            if (obj is ItemPrefabCatalog registry)
                pending.Add(registry);
        EditorApplication.delayCall -= Flush;
        EditorApplication.delayCall += Flush;
    }

    private void Flush()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            pending.Clear();
            return;
        }
        foreach (ItemPrefabCatalog registry in pending)
            if (registry != null)
                SyncPrefabs(registry);
        pending.Clear();
    }

    public static int SyncPrefabs(ItemPrefabCatalog registry)
    {
        var sprites = new Dictionary<GameObject, Sprite>();
        var conflicts = new HashSet<GameObject>();
        foreach (var entry in registry.Entries)
        {
            if (entry == null || entry.prefab == null || entry.sprite == null)
                continue;
            if (sprites.TryGetValue(entry.prefab, out Sprite existing) && existing != entry.sprite)
                conflicts.Add(entry.prefab);
            else
                sprites[entry.prefab] = entry.sprite;
        }

        int changed = 0;
        foreach (var pair in sprites)
        {
            GameObject prefab = pair.Key;
            if (conflicts.Contains(prefab))
            {
                Debug.LogWarning($"[ItemPrefabCatalog] {prefab.name}: 서로 다른 Sprite 등록 충돌. 프리팹을 분리하세요.", registry);
                continue;
            }
            if (!PrefabUtility.IsPartOfPrefabAsset(prefab))
            {
                Debug.LogWarning($"[ItemPrefabCatalog] {prefab.name}: Project 창의 프리팹 에셋을 연결하세요.", registry);
                continue;
            }
            Component component = prefab.GetComponent<SpriteRenderer>();
            if (component == null)
            {
                UIItemPartView part = prefab.GetComponent<UIItemPartView>();
                component = part != null ? part.Image : prefab.GetComponentInChildren<Image>(true);
            }
            if (component == null)
                continue;

            // SerializedObject records Undo and preserves all unrelated prefab fields.
            var serialized = new SerializedObject(component);
            SerializedProperty spriteProperty = serialized.FindProperty("m_Sprite");
            if (spriteProperty == null || spriteProperty.objectReferenceValue == pair.Value)
                continue;
            spriteProperty.objectReferenceValue = pair.Value;
            serialized.ApplyModifiedProperties();
            PrefabUtility.SavePrefabAsset(prefab);
            changed++;
        }
        return changed;
    }
}
