using System.Collections.Generic;
using UnityEngine;

public enum AssemblyAlignment { Center, Bottom, BaseAnchor }
public enum AssemblyScaleMode { FitWithinBounds, Fixed }

// Both world and UI presenters use the same contact-point rule.
public static class ItemAssemblyLayout
{
    public static void AlignBottom(IItemPartView part, Vector3 targetWorld)
    {
        part.PartTransform.position += targetWorld - part.BottomWorld;
    }

    public static void Stack(IItemPartView lower, IItemPartView upper, bool matchRotation = false)
    {
        if (matchRotation)
            upper.PartTransform.rotation = lower.PartTransform.rotation;
        AlignBottom(upper, lower.TopWorld);
    }

    public static Bounds TransformBounds(Bounds bounds, Transform from, Transform to)
    {
        Matrix4x4 matrix = to.worldToLocalMatrix * from.localToWorldMatrix;
        Bounds result = new Bounds(matrix.MultiplyPoint3x4(bounds.min), Vector3.zero);
        for (int x = 0; x < 2; x++)
            for (int y = 0; y < 2; y++)
                for (int z = 0; z < 2; z++)
                    result.Encapsulate(matrix.MultiplyPoint3x4(new Vector3(
                        x == 0 ? bounds.min.x : bounds.max.x,
                        y == 0 ? bounds.min.y : bounds.max.y,
                        z == 0 ? bounds.min.z : bounds.max.z)));
        return result;
    }

    public static Bounds GetBounds<T>(IReadOnlyList<T> parts, Transform space) where T : IItemPartView
    {
        if (parts.Count == 0)
            return new Bounds(Vector3.zero, Vector3.zero);
        Bounds bounds = parts[0].GetBoundsIn(space);
        for (int i = 1; i < parts.Count; i++)
            bounds.Encapsulate(parts[i].GetBoundsIn(space));
        return bounds;
    }

    public static float FitScale(Vector2 size, float maxWidth, float maxHeight)
    {
        float scale = 1f;
        if (size.x > 0f && maxWidth > 0f)
            scale = Mathf.Min(scale, maxWidth / size.x);
        if (size.y > 0f && maxHeight > 0f)
            scale = Mathf.Min(scale, maxHeight / size.y);
        return Mathf.Clamp01(scale);
    }

    public static void ArrangeUI<T>(IReadOnlyList<T> parts, RectTransform root,
        AssemblyAlignment alignment, AssemblyScaleMode scaleMode, float maxWidth, float maxHeight)
        where T : IItemPartView
    {
        root.localScale = Vector3.one;
        if (parts.Count == 0)
        {
            root.sizeDelta = Vector2.zero;
            return;
        }

        AlignBottom(parts[0], root.position);
        for (int i = 1; i < parts.Count; i++)
            Stack(parts[i - 1], parts[i]);

        Bounds bounds = GetBounds(parts, root);
        Vector3 offset = alignment == AssemblyAlignment.Center
            ? new Vector3(bounds.center.x, bounds.center.y, 0f)
            : alignment == AssemblyAlignment.Bottom
                ? new Vector3(bounds.center.x, bounds.min.y, 0f)
                : root.InverseTransformPoint(parts[0].BottomWorld);
        Vector3 worldOffset = root.TransformVector(offset);
        for (int i = 0; i < parts.Count; i++)
            parts[i].PartTransform.position -= worldOffset;

        root.sizeDelta = new Vector2(bounds.size.x, bounds.size.y);
        float scale = scaleMode == AssemblyScaleMode.Fixed ? 1f
            : FitScale(new Vector2(bounds.size.x, bounds.size.y), maxWidth, maxHeight);
        root.localScale = Vector3.one * scale;
    }
}
