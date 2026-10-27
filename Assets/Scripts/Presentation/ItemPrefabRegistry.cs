using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Registry owns prefab/sprite assignments; prefab views own sizes/contact points.
public class ItemPrefabRegistry : MonoBehaviour
{
    [SerializeField] private ItemPrefabCatalog catalog;
    public ItemPrefabCatalog Catalog => catalog;
    public IReadOnlyList<ItemPrefabCatalog.PrefabEntry> Entries =>
        catalog != null ? catalog.Entries : Array.Empty<ItemPrefabCatalog.PrefabEntry>();

    public GameObject GetPrefab(ItemKind kind, ItemViewContext context) =>
        catalog != null ? catalog.GetPrefab(kind, context) : null;

    public Sprite GetSprite(ItemKind kind, ItemViewContext context) =>
        catalog != null ? catalog.GetSprite(kind, context) : null;

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
