using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(RectTransform))]
public class ItemAssemblyView : MonoBehaviour
{
    private readonly List<UIItemPartView> parts = new List<UIItemPartView>();
    public MovableItem Item { get; private set; }
    public IReadOnlyList<UIItemPartView> Parts => parts;

    public void Initialize(MovableItem item, ItemPrefabRegistry registry,
        ItemViewContext context, UIItemPartView fallbackPrefab,
        float maxWidth, float maxHeight,
        AssemblyAlignment alignment = AssemblyAlignment.Center,
        AssemblyScaleMode scaleMode = AssemblyScaleMode.FitWithinBounds)
    {
        Clear();
        Item = item;
        RectTransform root = (RectTransform)transform;
        root.anchorMin = root.anchorMax = root.pivot = new Vector2(0.5f, 0.5f);
        root.anchoredPosition3D = Vector3.zero;
        root.localRotation = Quaternion.identity;
        root.localScale = Vector3.one;

        IReadOnlyList<ItemData> data = ItemVisualFactory.GetParts(item);
        for (int i = 0; i < data.Count; i++)
        {
            IItemPartView visual = ItemVisualFactory.Create(data[i], i, context, registry,
                fallbackPrefab != null ? fallbackPrefab.gameObject : null, transform);
            if (!(visual is UIItemPartView part))
            {
                // Never show a partial stack with shifted/misleading part indices.
                if (visual != null)
                    ItemVisualFactory.Release(visual.PartTransform.gameObject);
                Clear();
                return;
            }
            parts.Add(part);
        }
        ItemAssemblyLayout.ArrangeUI(parts, root, alignment, scaleMode, maxWidth, maxHeight);
    }

    public void AlignBaseTo(Vector3 targetWorld)
    {
        if (parts.Count > 0)
            transform.position += targetWorld - parts[0].BottomWorld;
    }

    public void Clear()
    {
        foreach (UIItemPartView part in parts)
            if (part != null)
                ItemVisualFactory.Release(part.gameObject);
        parts.Clear();
        Item = null;
    }

    private void OnDestroy() => Clear();
}
