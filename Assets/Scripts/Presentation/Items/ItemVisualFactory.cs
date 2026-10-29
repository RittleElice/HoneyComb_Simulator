using System.Collections.Generic;
using UnityEngine;

public static class ItemVisualFactory
{
    public static IReadOnlyList<ItemData> GetParts(MovableItem item)
    {
        if (item is ItemAssemblyData assembly)
            return assembly.Parts;
        if (item is ItemData single)
            return new[] { single };
        return System.Array.Empty<ItemData>();
    }

    public static IItemPartView Create(
        ItemData part, int partIndex, ItemViewContext context,
        ItemPrefabRegistry registry, GameObject fallback, Transform parent)
    {
        if (part == null)
            return null;

        GameObject prefab = registry != null ? registry.GetPrefab(part.Kind, context) : null;
        if (prefab == null)
            prefab = fallback;
        if (prefab == null)
        {
            Debug.LogWarning($"[ItemVisualFactory] {part.Kind}/{context} 프리팹 없음");
            return null;
        }

        GameObject instance = InstantiatePrefab(prefab, part.Kind, context, registry, parent);
        IItemPartView view;
        if (context == ItemViewContext.Apiary)
        {
            WorldItemPartView world = instance.GetComponent<WorldItemPartView>();
            SpriteRenderer renderer = instance.GetComponent<SpriteRenderer>();
            if (world == null || renderer == null || renderer.sprite == null ||
                renderer.sprite.bounds.size.x <= 0f || renderer.sprite.bounds.size.y <= 0f)
            {
                Debug.LogWarning($"[ItemVisualFactory] {prefab.name}: WorldItemPartView/SpriteRenderer 설정 확인");
                Release(instance);
                return null;
            }
            foreach (Collider collider in instance.GetComponentsInChildren<Collider>(true))
                collider.enabled = false;
            foreach (Collider2D collider in instance.GetComponentsInChildren<Collider2D>(true))
                collider.enabled = false;
            view = world;
        }
        else
        {
            UIItemPartView ui = instance.GetComponent<UIItemPartView>();
            if (ui == null || ui.GetComponent<RectTransform>() == null ||
                ui.Image == null || ui.Image.sprite == null)
            {
                Debug.LogWarning($"[ItemVisualFactory] {prefab.name}: UIItemPartView/Image 설정 확인");
                Release(instance);
                return null;
            }
            // Preserve each prefab's pivot. Only its parent-relative placement is reset.
            RectTransform rect = ui.RectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition3D = Vector3.zero;
            rect.localRotation = Quaternion.identity;
            rect.localScale = Vector3.one;
            foreach (UnityEngine.UI.Graphic graphic in instance.GetComponentsInChildren<UnityEngine.UI.Graphic>(true))
                graphic.raycastTarget = false;
            view = ui;
        }

        instance.name = part.Name;
        view.Bind(part, partIndex);
        return view;
    }

    public static void Release(GameObject instance)
    {
        if (instance == null)
            return;
        instance.SetActive(false);
        if (Application.isPlaying)
            Object.Destroy(instance);
        else
            Object.DestroyImmediate(instance);
    }

    public static GameObject InstantiatePrefab(GameObject prefab, ItemKind kind,
        ItemViewContext context, ItemPrefabRegistry registry, Transform parent)
    {
        if (prefab == null)
            return null;
        GameObject instance = Object.Instantiate(prefab, parent, false);
        // Registry is authoritative; apply before measuring geometry or binding data.
        if (registry != null)
            registry.ApplySprite(instance, kind, context);
        return instance;
    }
}
