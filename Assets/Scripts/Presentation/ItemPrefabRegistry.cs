using System;
using System.Collections.Generic;
using UnityEngine;

public class ItemPrefabRegistry : MonoBehaviour
{
    [Serializable]
    public class PrefabEntry
    {
        public ItemKind kind;
        public ItemViewContext context;
        public GameObject prefab;
    }

    [Header("장비별 프리팹")]
    [SerializeField]
    private List<PrefabEntry> entries =
        new List<PrefabEntry>();

    [Header("기본 프리팹")]
    [SerializeField]
    private GameObject defaultApiaryPrefab;

    [SerializeField]
    private GameObject defaultUIPrefab;

    private readonly Dictionary<(ItemKind, ItemViewContext), GameObject>
        prefabLookup = new();

    private void Awake()
    {
        prefabLookup.Clear();

        foreach (PrefabEntry entry in entries)
        {
            if (entry.prefab == null)
                continue;

            var key = (entry.kind, entry.context);

            if (prefabLookup.ContainsKey(key))
            {
                Debug.LogWarning(
                    $"[ItemPrefabRegistry] 중복 등록: " +
                    $"{entry.kind}, {entry.context}");
            }

            prefabLookup[key] = entry.prefab;
        }
    }

    public GameObject GetPrefab(
        ItemKind kind,
        ItemViewContext context)
    {
        if (prefabLookup.TryGetValue(
                (kind, context), out GameObject prefab))
        {
            return prefab;
        }

        return context == ItemViewContext.Apiary
            ? defaultApiaryPrefab
            : defaultUIPrefab;
    }
}