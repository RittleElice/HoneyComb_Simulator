
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public enum AssemblyAlignment
{
    Center,
    Bottom
}

[RequireComponent(typeof(RectTransform))]
public class ItemAssemblyView : MonoBehaviour
{
    private readonly List<GameObject> visuals =
        new List<GameObject>();

    private struct PartLayout
    {
        public RectTransform Rect;
        public Rect ImageBounds;
    }

    public MovableItem Item { get; private set; }

    public void Initialize(
        MovableItem item,
        ItemPrefabRegistry registry,
        ItemViewContext context,
        ItemIconView fallbackPrefab,
        float maxWidth,
        float maxHeight,
        AssemblyAlignment alignment = AssemblyAlignment.Center)
    {
        Clear();

        Item = item;

        if (item == null)
            return;

        List<ItemData> parts = new List<ItemData>();

        if (item is ItemAssembly assembly)
        {
            foreach (ItemData part in assembly.Parts)
                parts.Add(part);
        }
        else if (item is ItemData single)
        {
            parts.Add(single);
        }

        if (parts.Count == 0)
            return;

        RectTransform root = GetComponent<RectTransform>();

        root.anchorMin = new Vector2(0.5f, 0.5f);
        root.anchorMax = new Vector2(0.5f, 0.5f);
        root.pivot = new Vector2(0.5f, 0.5f);
        root.anchoredPosition = Vector2.zero;
        root.localRotation = Quaternion.identity;
        root.localScale = Vector3.one;

        List<PartLayout> layouts = new List<PartLayout>();

        foreach (ItemData part in parts)
        {
            GameObject prefab = null;

            if (registry != null)
                prefab = registry.GetPrefab(part.Kind, context);

            if (prefab == null && fallbackPrefab != null)
                prefab = fallbackPrefab.gameObject;

            if (prefab == null)
            {
                Debug.LogWarning(
                    $"[ItemAssemblyView] {part.Kind} 프리팹 없음");
                continue;
            }

            GameObject obj = Instantiate(
                prefab, transform, false);

            ItemIconView icon =
                obj.GetComponent<ItemIconView>();

            RectTransform rect =
                obj.GetComponent<RectTransform>();

            if (icon == null || rect == null)
            {
                Debug.LogWarning(
                    $"[ItemAssemblyView] {prefab.name}에 " +
                    "ItemIconView 또는 RectTransform이 없습니다.");

                obj.SetActive(false);
                Destroy(obj);
                continue;
            }

            icon.Initialize(part);
            
            // Registry에 등록된 Sprite를 적용
            if (registry != null)
            {
                registry.ApplySprite(
                    obj,
                    part.Kind,
                    context);
            }

            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.localRotation = Quaternion.identity;
            rect.localScale = Vector3.one;

            foreach (Graphic graphic in
                obj.GetComponentsInChildren<Graphic>(true))
            {
                graphic.raycastTarget = false;
            }

            Image image = obj.GetComponent<Image>();

            if (image == null)
                image = obj.GetComponentInChildren<Image>();

            Rect imageBounds = GetImageBounds(rect, image);

            layouts.Add(new PartLayout
            {
                Rect = rect,
                ImageBounds = imageBounds
            });

            visuals.Add(obj);
        }

        if (layouts.Count == 0)
            return;

        // 아래 부품부터 위로 쌓기
        float currentTop = 0f;

        float minX = float.PositiveInfinity;
        float minY = float.PositiveInfinity;
        float maxX = float.NegativeInfinity;
        float maxY = float.NegativeInfinity;

        foreach (PartLayout layout in layouts)
        {
            Rect bounds = layout.ImageBounds;

            float positionX = -bounds.center.x;
            float positionY = currentTop - bounds.yMin;

            layout.Rect.anchoredPosition =
                new Vector2(positionX, positionY);

            float left = positionX + bounds.xMin;
            float right = positionX + bounds.xMax;
            float bottom = positionY + bounds.yMin;
            float top = positionY + bounds.yMax;

            minX = Mathf.Min(minX, left);
            minY = Mathf.Min(minY, bottom);
            maxX = Mathf.Max(maxX, right);
            maxY = Mathf.Max(maxY, top);

            currentTop = top;
        }

        float totalWidth = maxX - minX;
        float totalHeight = maxY - minY;

        // Center: 전체 중앙을 원점으로
        // Bottom: 전체 하단 중앙을 원점으로
        Vector2 offset;

        if (alignment == AssemblyAlignment.Bottom)
        {
            offset = new Vector2(
                (minX + maxX) * 0.5f,
                minY);
        }
        else
        {
            offset = new Vector2(
                (minX + maxX) * 0.5f,
                (minY + maxY) * 0.5f);
        }

        foreach (PartLayout layout in layouts)
        {
            layout.Rect.anchoredPosition -= offset;
        }

        root.sizeDelta = new Vector2(
            totalWidth, totalHeight);

        // 조립체 전체 비율 유지 축소
        // 손에 든 장비는 층수와 관계없이 고정 크기로 표시
        if (context == ItemViewContext.Hand)
        {
            root.localScale = Vector3.one;
        }
        else
        {
            // 소지품 등에서는 기존 자동 축소 유지
            float scale = 1f;

            if (totalWidth > 0f && maxWidth > 0f)
                scale = Mathf.Min(
                    scale, maxWidth / totalWidth);

            if (totalHeight > 0f && maxHeight > 0f)
                scale = Mathf.Min(
                    scale, maxHeight / totalHeight);

            root.localScale =
                Vector3.one * Mathf.Clamp01(scale);
        }
    }

    private static Rect GetImageBounds(
        RectTransform root,
        Image image)
    {
        if (image == null)
            return root.rect;

        RectTransform imageRect = image.rectTransform;
        Rect rect = imageRect.rect;

        float width = rect.width;
        float height = rect.height;

        if (image.preserveAspect &&
            image.sprite != null &&
            width > 0f &&
            height > 0f)
        {
            float spriteWidth = image.sprite.rect.width;
            float spriteHeight = image.sprite.rect.height;

            if (spriteWidth > 0f && spriteHeight > 0f)
            {
                float spriteRatio =
                    spriteWidth / spriteHeight;

                float rectRatio = width / height;

                if (spriteRatio > rectRatio)
                    height = width / spriteRatio;
                else
                    width = height * spriteRatio;
            }
        }

        Vector2 center = rect.center;

        Rect visibleRect = new Rect(
            center.x - width * 0.5f,
            center.y - height * 0.5f,
            width,
            height);

        Vector3[] corners =
        {
            new Vector3(visibleRect.xMin, visibleRect.yMin, 0f),
            new Vector3(visibleRect.xMin, visibleRect.yMax, 0f),
            new Vector3(visibleRect.xMax, visibleRect.yMin, 0f),
            new Vector3(visibleRect.xMax, visibleRect.yMax, 0f)
        };

        Matrix4x4 toRoot =
            root.worldToLocalMatrix *
            imageRect.localToWorldMatrix;

        float minX = float.PositiveInfinity;
        float minY = float.PositiveInfinity;
        float maxX = float.NegativeInfinity;
        float maxY = float.NegativeInfinity;

        foreach (Vector3 corner in corners)
        {
            Vector3 point =
                toRoot.MultiplyPoint3x4(corner);

            minX = Mathf.Min(minX, point.x);
            minY = Mathf.Min(minY, point.y);
            maxX = Mathf.Max(maxX, point.x);
            maxY = Mathf.Max(maxY, point.y);
        }

        return Rect.MinMaxRect(minX, minY, maxX, maxY);
    }

    private void Clear()
    {
        foreach (GameObject obj in visuals)
        {
            if (obj != null)
            {
                obj.SetActive(false);
                Destroy(obj);
            }
        }

        visuals.Clear();
        Item = null;
    }

    private void OnDestroy()
    {
        Clear();
    }
}
