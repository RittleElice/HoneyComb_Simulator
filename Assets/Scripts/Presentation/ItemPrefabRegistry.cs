using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Registry owns prefab/sprite assignments; prefab views own sizes/contact points.
public class ItemPrefabRegistry : MonoBehaviour
{
    [Serializable]
    public class PrefabEntry
    {
        public ItemKind kind;
        public ItemViewContext context;
        public GameObject prefab;
        [Header("장비 이미지")]
        public Sprite sprite;
    }

    [Header("장비별 프리팹")]
    [SerializeField] private List<PrefabEntry> entries = new List<PrefabEntry>();
    [Header("기본 프리팹")]
    [SerializeField] private GameObject defaultApiaryPrefab;
    [SerializeField] private GameObject defaultUIPrefab;

    private readonly Dictionary<(ItemKind, ItemViewContext), PrefabEntry> entryLookup = new();
    private bool initialized;
    public IReadOnlyList<PrefabEntry> Entries => entries;

    private void Awake() => RebuildLookup();
    private void OnValidate() => initialized = false;

    private void RebuildLookup()
    {
        entryLookup.Clear();
        foreach (PrefabEntry entry in entries)
        {
            if (entry == null)
                continue;
            var key = (entry.kind, entry.context);
            if (entryLookup.ContainsKey(key))
                Debug.LogWarning($"[ItemPrefabRegistry] 중복 등록: {entry.kind}, {entry.context}");
            entryLookup[key] = entry;
        }
        initialized = true;
    }

    public GameObject GetPrefab(ItemKind kind, ItemViewContext context)
    {
        if (!initialized)
            RebuildLookup();
        if (entryLookup.TryGetValue((kind, context), out PrefabEntry entry) && entry.prefab != null)
            return entry.prefab;
        return context == ItemViewContext.Apiary ? defaultApiaryPrefab : defaultUIPrefab;
    }

    public Sprite GetSprite(ItemKind kind, ItemViewContext context)
    {
        if (!initialized)
            RebuildLookup();
        return entryLookup.TryGetValue((kind, context), out PrefabEntry entry) ? entry.sprite : null;
    }

    public bool ApplySprite(GameObject instance, ItemKind kind, ItemViewContext context)
    {
        if (instance == null)
            return false;
        Sprite sprite = GetSprite(kind, context);
        // Preserve a default prefab's sprite when no image is registered.
        if (sprite == null)
            return false;
        SpriteRenderer renderer = instance.GetComponent<SpriteRenderer>();
        if (renderer != null)
        {
            renderer.sprite = sprite;
            return true;
        }
        UIItemPartView part = instance.GetComponent<UIItemPartView>();
        Image image = part != null ? part.Image : instance.GetComponent<Image>();
        if (image == null)
            image = instance.GetComponentInChildren<Image>(true);
        if (image == null)
            return false;
        image.sprite = sprite;
        return true;
    }

    public GameObject CreateVisual(ItemKind kind, ItemViewContext context, Transform parent)
    {
        return ItemVisualFactory.InstantiatePrefab(GetPrefab(kind, context), kind, context, this, parent);
    }
}
