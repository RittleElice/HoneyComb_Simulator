using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "HoneyComb/Item Prefab Catalog", fileName = "ItemPrefabCatalog")]
public class ItemPrefabCatalog : ScriptableObject
{
    [Serializable]
    public class PrefabEntry
    {
        public ItemKind kind;
        public ItemViewContext context;
        public GameObject prefab;
        public Sprite sprite;
    }

    [SerializeField] private List<PrefabEntry> entries = new List<PrefabEntry>();
    [SerializeField] private GameObject defaultApiaryPrefab;
    [SerializeField] private GameObject defaultUIPrefab;
    public IReadOnlyList<PrefabEntry> Entries => entries;

    // The small catalog is read directly, so asset edits cannot leave a stale cache.
    private PrefabEntry Find(ItemKind kind, ItemViewContext context)
    {
        for (int i = entries.Count - 1; i >= 0; i--)
            if (entries[i] != null && entries[i].kind == kind && entries[i].context == context)
                return entries[i];
        return null;
    }

    public GameObject GetPrefab(ItemKind kind, ItemViewContext context)
    {
        var entry = Find(kind, context);
        return entry != null && entry.prefab != null ? entry.prefab :
            context == ItemViewContext.Apiary ? defaultApiaryPrefab : defaultUIPrefab;
    }

    public Sprite GetSprite(ItemKind kind, ItemViewContext context) => Find(kind, context)?.sprite;

    private void OnValidate()
    {
        var keys = new HashSet<(ItemKind, ItemViewContext)>();
        foreach (var entry in entries)
            if (entry != null && !keys.Add((entry.kind, entry.context)))
                Debug.LogWarning($"[ItemPrefabCatalog] 중복 등록: {entry.kind}, {entry.context}", this);
    }
}
