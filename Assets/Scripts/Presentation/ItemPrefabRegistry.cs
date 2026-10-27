
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

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

    [Header("장비별 프리팹 및 이미지")]
    [SerializeField]
    private List<PrefabEntry> entries =
        new List<PrefabEntry>();

    [Header("기본 프리팹")]
    [SerializeField]
    private GameObject defaultApiaryPrefab;

    [SerializeField]
    private GameObject defaultUIPrefab;

    private readonly Dictionary<
        (ItemKind, ItemViewContext), PrefabEntry>
        entryLookup = new();

    private void Awake()
    {
        RebuildLookup();
    }

    private void RebuildLookup()
    {
        entryLookup.Clear();

        foreach (PrefabEntry entry in entries)
        {
            if (entry == null)
                continue;

            var key = (entry.kind, entry.context);

            if (entryLookup.ContainsKey(key))
            {
                Debug.LogWarning(
                    "[ItemPrefabRegistry] 중복 등록: " +
                    $"{entry.kind}, {entry.context}");
            }

            entryLookup[key] = entry;
        }
    }

    // 기존 프리팹 조회 기능 유지
    public GameObject GetPrefab(
        ItemKind kind,
        ItemViewContext context)
    {
        if (entryLookup.TryGetValue(
            (kind, context), out PrefabEntry entry))
        {
            if (entry.prefab != null)
                return entry.prefab;
        }

        return context == ItemViewContext.Apiary
            ? defaultApiaryPrefab
            : defaultUIPrefab;
    }

    // 등록된 스프라이트 조회
    public Sprite GetSprite(
        ItemKind kind,
        ItemViewContext context)
    {
        if (entryLookup.TryGetValue(
            (kind, context), out PrefabEntry entry))
        {
            return entry.sprite;
        }

        return null;
    }

    // 생성된 프리팹 인스턴스에 스프라이트 적용
    public bool ApplySprite(
        GameObject instance,
        ItemKind kind,
        ItemViewContext context)
    {
        if (instance == null)
            return false;

        Sprite sprite = GetSprite(kind, context);

        if (sprite == null)
            return false;

        // 월드 공간 SpriteRenderer
        SpriteRenderer spriteRenderer =
            instance.GetComponent<SpriteRenderer>();

        if (spriteRenderer != null)
        {
            spriteRenderer.sprite = sprite;
            return true;
        }

        // UI Image
        Image image = instance.GetComponent<Image>();

        if (image != null)
        {
            image.sprite = sprite;
            return true;
        }

        Debug.LogWarning(
            "[ItemPrefabRegistry] 이미지 컴포넌트 없음: " +
            instance.name);

        return false;
    }

    // 프리팹 생성과 이미지 적용을 함께 처리
    public GameObject CreateVisual(
        ItemKind kind,
        ItemViewContext context,
        Transform parent)
    {
        GameObject prefab = GetPrefab(kind, context);

        if (prefab == null)
        {
            Debug.LogWarning(
                "[ItemPrefabRegistry] 프리팹 없음: " +
                $"{kind}, {context}");

            return null;
        }

        GameObject instance =
            Instantiate(prefab, parent);

        ApplySprite(instance, kind, context);

        return instance;
    }
}
