using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class ActionButtonPositioner : MonoBehaviour
{
    private Vector3 followPoint;
    private Camera followCamera;
    private bool hasFollow, above;
    private float gap, gapScale = 1;
    private bool screenAnchor, hasBounds;
    private Rect screenBounds;
    private Vector2 screenPoint, edge;
    public void SetScreenAnchor(Vector2 point, Vector2 direction, float distance, float scale = 1)
    { hasBounds = false; screenPoint = point; edge = direction; gap = distance; gapScale = scale; screenAnchor = true; hasFollow = true; }
    public void SetScreenBounds(Rect bounds, Vector2 defaultPoint, Vector2 defaultDirection, float distance, float scale = 1)
    {
        SetScreenAnchor(defaultPoint, defaultDirection, distance, scale);
        screenBounds = bounds; hasBounds = true;
    }
    private RectTransform cachedRect, cachedParent;
    private Canvas cachedCanvas;
    private GridLayoutGroup cachedGrid;
    private LayoutGroup cachedLayout;
    private bool hasPlacement;
    private PlacementState lastPlacement;
#if UNITY_EDITOR
    public int PositionApplicationCount { get; private set; }
    public int LayoutRebuildCount { get; private set; }
#endif
    private void OnEnable() => hasPlacement = false;
    private void OnTransformParentChanged() { cachedCanvas = null; hasPlacement = false; }
    private void OnCanvasHierarchyChanged() { cachedCanvas = null; hasPlacement = false; }
    private readonly Vector3[] corners = new Vector3[4];
    public void SetFollowPoint(Vector3 point, Camera camera, bool placeAbove, float distance)
    { hasBounds = false; gapScale = 1; screenAnchor = false; followPoint = point; followCamera = camera; above = placeAbove; gap = distance; hasFollow = true; }

    public void Apply(ActionButtonCatalog.Entry entry, RectTransform relative, bool isLid = false)
    {
        if (cachedRect == null) cachedRect = (RectTransform)transform;
        var rect = cachedRect;
        var parent = rect.parent as RectTransform;
        if (cachedCanvas == null || cachedParent != parent)
        {
            cachedParent = parent;
            cachedCanvas = GetComponentInParent<Canvas>();
            cachedGrid = parent != null ? parent.GetComponent<GridLayoutGroup>() : null;
            cachedLayout = parent != null ? parent.GetComponent<LayoutGroup>() : null;
            hasPlacement = false;
        }
        var canvas = cachedCanvas;
        if (parent == null || canvas == null) return;
        if (entry.placement == ActionButtonPlacement.Grid)
        {
            if (cachedGrid == null) cachedGrid = parent.GetComponent<GridLayoutGroup>();
            var grid = cachedGrid;
            if (grid != null)
            {
                if (grid.cellSize != entry.size) grid.cellSize = entry.size;
                if (grid.spacing != entry.gridSpacing) grid.spacing = entry.gridSpacing;
                if (grid.constraint != GridLayoutGroup.Constraint.FixedColumnCount) grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
                int columns = Mathf.Max(1, entry.gridColumns);
                if (grid.constraintCount != columns) grid.constraintCount = columns;
            }
            return; // GridLayoutGroup exclusively owns each slot's position.
        }
        float distance = entry.followGap >= 0 ? entry.followGap * gapScale : gap;
        canvas = canvas.rootCanvas;
        var root = (RectTransform)canvas.transform;
        Camera camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        var target = relative != null ? relative : parent;
        if (entry.placement == ActionButtonPlacement.Relative && entry.useLayoutSlot && cachedLayout == null)
        {
            cachedLayout = parent.GetComponent<LayoutGroup>();
            if (cachedLayout != null) hasPlacement = false;
        }
        bool cachePlacement = entry.placement != ActionButtonPlacement.FollowTarget;
        if (cachePlacement && hasPlacement && lastPlacement.Matches(new PlacementState(entry, rect, parent, root, target, camera))) return;
#if UNITY_EDITOR
        PositionApplicationCount++;
#endif
        Vector3 world;
        if (entry.placement == ActionButtonPlacement.Fixed)
            world = root.TransformPoint(root.rect.min + Vector2.Scale(root.rect.size, entry.anchor));
        else if (entry.placement == ActionButtonPlacement.FollowTarget && hasFollow)
        {
            Vector2 point = screenAnchor ? screenPoint : RectTransformUtility.WorldToScreenPoint(followCamera, followPoint);
            Vector2 direction = screenAnchor ? edge : above ? Vector2.up : Vector2.right;
            var followAnchor = entry.ResolveFollowAnchor(isLid);
            if (followAnchor != ActionButtonFollowAnchor.Automatic)
            {
                switch (followAnchor)
                {
                    case ActionButtonFollowAnchor.Top: direction = Vector2.up; break;
                    case ActionButtonFollowAnchor.Bottom: direction = Vector2.down; break;
                    case ActionButtonFollowAnchor.Left: direction = Vector2.left; break;
                    case ActionButtonFollowAnchor.Right: direction = Vector2.right; break;
                    default: direction = Vector2.zero; break;
                }
                if (hasBounds) point = screenBounds.center + Vector2.Scale(screenBounds.size * .5f, direction);
                // Paired buttons use half-gap only in their original automatic layout.
                if (entry.followGap >= 0) distance = entry.followGap;
            }
            if (!RectTransformUtility.ScreenPointToWorldPointInRectangle(root, point, camera, out world)) return;
            Vector2 half = entry.size * .5f;
            world += root.TransformVector(Vector2.Scale(direction, half + Vector2.one * distance));
        }
        else if (entry.placement == ActionButtonPlacement.Relative && entry.useLayoutSlot && cachedLayout != null)
        {
#if UNITY_EDITOR
            LayoutRebuildCount++;
#endif
            LayoutRebuilder.ForceRebuildLayoutImmediate(parent);
            world = rect.position;
        }
        else
        {
            world = target.TransformPoint(target.rect.min + Vector2.Scale(target.rect.size, entry.anchor));
        }
        rect.position = world + root.TransformVector(entry.offset);
        // LayoutGroup may force-expand a slot; the visible button still uses catalog dimensions.
        if (rect.rect.width != Mathf.Max(1, entry.size.x)) rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, Mathf.Max(1, entry.size.x));
        if (rect.rect.height != Mathf.Max(1, entry.size.y)) rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, Mathf.Max(1, entry.size.y));
        if (!entry.clampToScreen)
        {
            if (cachePlacement) { lastPlacement = new PlacementState(entry, rect, parent, root, target, camera); hasPlacement = true; }
            return;
        }
        // Clamp actual corners, including non-centred pivots and nested scale, to screen AND root Canvas.
        RectTransformUtility.ScreenPointToLocalPointInRectangle(root, Vector2.zero, camera, out var min);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(root, new Vector2(Screen.width, Screen.height), camera, out var max);
        min = Vector2.Max(min, root.rect.min) + Vector2.one * entry.padding;
        max = Vector2.Min(max, root.rect.max) - Vector2.one * entry.padding;
        rect.GetWorldCorners(corners);
        Vector2 lo = new Vector2(float.PositiveInfinity, float.PositiveInfinity), hi = -lo;
        foreach (var corner in corners) { Vector2 p = root.InverseTransformPoint(corner); lo = Vector2.Min(lo, p); hi = Vector2.Max(hi, p); }
        Vector2 delta = new Vector2(Correction(lo.x, hi.x, min.x, max.x), Correction(lo.y, hi.y, min.y, max.y));
        if (delta != Vector2.zero) rect.position += root.TransformVector(delta);
        if (cachePlacement) { lastPlacement = new PlacementState(entry, rect, parent, root, target, camera); hasPlacement = true; }
    }
    private struct PlacementState
    {
        private ActionButtonPlacement mode;
        private Vector2 size;
        private Vector2 offset;
        private Vector2 anchor;
        private float padding;
        private bool clamp;
        private bool slot;
        private Rect rootRect;
        private Rect targetRect;
        private Rect selfRect;
        private Matrix4x4 rootMatrix;
        private Matrix4x4 parentMatrix;
        private Matrix4x4 targetMatrix;
        private Matrix4x4 selfMatrix;
        private Matrix4x4 cameraMatrix;
        private Matrix4x4 projection;
        private Rect pixelRect;
        private Vector2Int screen;
        public PlacementState(ActionButtonCatalog.Entry e, RectTransform self, RectTransform parent, RectTransform root, RectTransform target, Camera camera)
        {
            mode = e.placement;
            size = e.size;
            offset = e.offset;
            anchor = e.anchor;
            padding = e.padding;
            clamp = e.clampToScreen;
            slot = e.useLayoutSlot;
            rootRect = root.rect;
            targetRect = target.rect;
            selfRect = self.rect;
            rootMatrix = root.localToWorldMatrix;
            parentMatrix = parent.localToWorldMatrix;
            targetMatrix = target.localToWorldMatrix;
            selfMatrix = self.localToWorldMatrix;
            cameraMatrix = camera != null ? camera.worldToCameraMatrix : Matrix4x4.identity;
            projection = camera != null ? camera.projectionMatrix : Matrix4x4.identity;
            pixelRect = camera != null ? camera.pixelRect : default;
            screen = new Vector2Int(Screen.width, Screen.height);
        }
        public bool Matches(PlacementState other) =>
            mode == other.mode &&
            size == other.size &&
            offset == other.offset &&
            anchor == other.anchor &&
            padding == other.padding &&
            clamp == other.clamp &&
            slot == other.slot &&
            rootRect == other.rootRect &&
            targetRect == other.targetRect &&
            selfRect == other.selfRect &&
            rootMatrix == other.rootMatrix &&
            parentMatrix == other.parentMatrix &&
            targetMatrix == other.targetMatrix &&
            selfMatrix == other.selfMatrix &&
            cameraMatrix == other.cameraMatrix &&
            projection == other.projection &&
            pixelRect == other.pixelRect &&
            screen == other.screen;
    }
    private static float Correction(float lo, float hi, float min, float max)
    {
        if (hi - lo > max - min) return (min + max - lo - hi) * .5f;
        return lo < min ? min - lo : hi > max ? max - hi : 0;
    }
}
