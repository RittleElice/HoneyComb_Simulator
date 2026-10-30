using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class ActionButtonPositioner : MonoBehaviour
{
    private Vector3 followPoint;
    private Camera followCamera;
    private bool hasFollow, above;
    private float gap;
    private readonly Vector3[] corners = new Vector3[4];
    public void SetFollowPoint(Vector3 point, Camera camera, bool placeAbove, float distance)
    { followPoint = point; followCamera = camera; above = placeAbove; gap = distance; hasFollow = true; }

    public void Apply(ActionButtonCatalog.Entry entry, RectTransform relative)
    {
        var rect = (RectTransform)transform;
        var parent = rect.parent as RectTransform;
        var canvas = GetComponentInParent<Canvas>();
        if (parent == null || canvas == null) return;
        canvas = canvas.rootCanvas;
        var root = (RectTransform)canvas.transform;
        Camera camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        Vector3 world;
        if (entry.placement == ActionButtonPlacement.Fixed)
            world = root.TransformPoint(root.rect.min + Vector2.Scale(root.rect.size, entry.anchor));
        else if (entry.placement == ActionButtonPlacement.FollowTarget && hasFollow)
        {
            if (!RectTransformUtility.ScreenPointToWorldPointInRectangle(root,
                RectTransformUtility.WorldToScreenPoint(followCamera, followPoint), camera, out world)) return;
            Vector2 half = entry.size * .5f;
            world += root.TransformVector(above ? new Vector3(0, half.y + gap, 0) : new Vector3(half.x + gap, 0, 0));
        }
        else if (entry.placement == ActionButtonPlacement.Relative && entry.useLayoutSlot && parent.GetComponent<LayoutGroup>() != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(parent);
            world = rect.position;
        }
        else
        {
            var target = relative != null ? relative : parent;
            world = target.TransformPoint(target.rect.min + Vector2.Scale(target.rect.size, entry.anchor));
        }
        rect.position = world + root.TransformVector(entry.offset);
        // LayoutGroup may force-expand a slot; the visible button still uses catalog dimensions.
        rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, Mathf.Max(1, entry.size.x));
        rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, Mathf.Max(1, entry.size.y));
        if (!entry.clampToScreen) return;
        // Clamp actual corners, including non-centred pivots and nested scale, to screen AND root Canvas.
        RectTransformUtility.ScreenPointToLocalPointInRectangle(root, Vector2.zero, camera, out var min);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(root, new Vector2(Screen.width, Screen.height), camera, out var max);
        min = Vector2.Max(min, root.rect.min) + Vector2.one * entry.padding;
        max = Vector2.Min(max, root.rect.max) - Vector2.one * entry.padding;
        rect.GetWorldCorners(corners);
        Vector2 lo = new Vector2(float.PositiveInfinity, float.PositiveInfinity), hi = -lo;
        foreach (var corner in corners) { Vector2 p = root.InverseTransformPoint(corner); lo = Vector2.Min(lo, p); hi = Vector2.Max(hi, p); }
        Vector2 delta = new Vector2(Correction(lo.x, hi.x, min.x, max.x), Correction(lo.y, hi.y, min.y, max.y));
        rect.position += root.TransformVector(delta);
    }
    private static float Correction(float lo, float hi, float min, float max)
    {
        if (hi - lo > max - min) return (min + max - lo - hi) * .5f;
        return lo < min ? min - lo : hi > max ? max - hi : 0;
    }
}
