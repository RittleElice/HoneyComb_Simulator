using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent, RequireComponent(typeof(Button)), DefaultExecutionOrder(220)]
public sealed class ActionButtonView : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [SerializeField] private ActionButtonId id;
    [SerializeField] private ActionButtonManager manager;
    [SerializeField] private RectTransform relativeTarget;
    private string contextualLabel;
    private bool isLid, occupied;
    private int level;
    public void SetContext(bool lid = false, int floor = 0, bool filled = false)
    {
        if (isLid == lid && level == floor && occupied == filled) return;
        isLid = lid; level = floor; occupied = filled; styleDirty = true; Apply();
    }
    private Image icon;
    private TMP_Text label;
    private ActionButtonPositioner positioner;
    private RectTransform rect;
    private LayoutElement layout;
    private Button button;
    private bool initialized, styleDirty = true, hasAppearance;
    private Appearance appearance;
#if UNITY_EDITOR
    public int StyleApplicationCount { get; private set; }
#endif
    private void CacheComponents()
    {
        if (initialized) return;
        rect = (RectTransform)transform;
        button = GetComponent<Button>();
        layout = GetComponent<LayoutElement>() ?? gameObject.AddComponent<LayoutElement>();
        positioner = GetComponent<ActionButtonPositioner>() ?? gameObject.AddComponent<ActionButtonPositioner>();
        label = GetComponentInChildren<TMP_Text>(true);
        initialized = true;
    }
    private void OnEnable() { styleDirty = true; }

    public ActionButtonId Id => id;
    public bool HasStyle => manager != null && manager.Catalog != null && manager.Catalog.Find(id) != null;
    public RectTransform RelativeTarget { get => relativeTarget; set => relativeTarget = value; }

    public void Bind(ActionButtonManager owner, ActionButtonId action, string text)
    {
        styleDirty |= manager != owner || id != action || contextualLabel != text;
        manager = owner; id = action; contextualLabel = text;
        CacheComponents();
        Apply();
    }

    public void SetFollowPoint(Vector3 worldPoint, Camera camera, bool above, float gap)
    {
        CacheComponents();
        positioner.SetFollowPoint(worldPoint, camera, above, gap);
    }

    public void SetScreenAnchor(Vector2 point, Vector2 direction, float gap, float gapScale = 1)
    {
        CacheComponents();
        positioner.SetScreenAnchor(point, direction, gap, gapScale);
        // Controllers only publish anchors. LateUpdate flushes the latest one once.
    }

    public void SetScreenBounds(Rect bounds, Vector2 defaultPoint, Vector2 defaultDirection, float gap, float gapScale = 1)
    {
        CacheComponents();
        positioner.SetScreenBounds(bounds, defaultPoint, defaultDirection, gap, gapScale);
    }

    public void Apply()
    {
        var entry = manager != null && manager.Catalog != null ? manager.Catalog.Find(id) : null;
        if (entry == null) return; // Legacy presentation remains usable without a catalog entry.
        CacheComponents();
        var current = new Appearance(entry);
        if (styleDirty || !hasAppearance || !appearance.Matches(current))
        {
            ApplyAppearance(entry);
            appearance = current; hasAppearance = true; styleDirty = false;
        }
        positioner.Apply(entry, relativeTarget, isLid);
    }

    private void ApplyAppearance(ActionButtonCatalog.Entry entry)
    {
#if UNITY_EDITOR
        StyleApplicationCount++;
#endif
        rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, Mathf.Max(1, entry.size.x));
        rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, Mathf.Max(1, entry.size.y));
        layout.ignoreLayout = entry.placement != ActionButtonPlacement.Grid &&
            (!entry.useLayoutSlot || entry.placement != ActionButtonPlacement.Relative);
        layout.preferredWidth = entry.size.x; layout.preferredHeight = entry.size.y;
        layout.minWidth = entry.size.x; layout.minHeight = entry.size.y;
        layout.flexibleWidth = layout.flexibleHeight = 0;
        if (icon == null && entry.icon != null)
        {
            var go = new GameObject("ActionIcon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(transform, false);
            icon = go.GetComponent<Image>(); icon.raycastTarget = false; icon.preserveAspect = true;
        }
        bool showText = entry.showLabel || entry.icon == null;
        if (icon != null)
        {
            icon.sprite = entry.icon; icon.enabled = entry.icon != null;
            icon.rectTransform.localRotation = Quaternion.Euler(0, 0, entry.iconRotation);
            var r = icon.rectTransform;
            r.anchorMin = new Vector2(.12f, showText ? .34f : .12f);
            r.anchorMax = new Vector2(.88f, .9f); r.offsetMin = r.offsetMax = Vector2.zero;
        }
        if (label != null)
        {
            label.gameObject.SetActive(showText); label.raycastTarget = false;
            label.text = entry.ResolveLabel(contextualLabel, isLid, level, occupied);
            label.enableAutoSizing = true; label.fontSizeMin = 12; label.fontSizeMax = 25;
            var r = label.rectTransform; r.anchorMin = Vector2.zero;
            r.anchorMax = new Vector2(1, entry.icon != null ? .34f : 1);
            r.offsetMin = new Vector2(3, 2); r.offsetMax = new Vector2(-3, -2);
        }
        if (button.targetGraphic != null) button.targetGraphic.raycastTarget = true;
    }
    private void LateUpdate() => Apply();
    // Value snapshot: direct runtime field edits and Inspector edits are both detected.
    // No text formatting, allocation or UI setters on unchanged frames.
    private struct Appearance
    {
        private Sprite icon;
        private float iconRotation;
        private Vector2 size;
        private ActionButtonPlacement placement;
        private bool useLayoutSlot;
        private bool showLabel;
        private string label;
        private string lidLabel;
        private string stackLabel;
        private string occupiedLabel;
        public Appearance(ActionButtonCatalog.Entry e)
        {
            icon = e.icon;
            iconRotation = e.iconRotation;
            size = e.size;
            placement = e.placement;
            useLayoutSlot = e.useLayoutSlot;
            showLabel = e.showLabel;
            label = e.label;
            lidLabel = e.lidLabel;
            stackLabel = e.stackLabel;
            occupiedLabel = e.occupiedLabel;
        }
        public bool Matches(Appearance other) =>
            icon == other.icon &&
            iconRotation == other.iconRotation &&
            size == other.size &&
            placement == other.placement &&
            useLayoutSlot == other.useLayoutSlot &&
            showLabel == other.showLabel &&
            label == other.label &&
            lidLabel == other.lidLabel &&
            stackLabel == other.stackLabel &&
            occupiedLabel == other.occupiedLabel;
    }
    // Claim drags that start on a button; never pass them to a parent/world gesture.
    public void OnBeginDrag(PointerEventData e) { e.eligibleForClick = false; }
    public void OnDrag(PointerEventData e) { e.eligibleForClick = false; }
    public void OnEndDrag(PointerEventData e) { e.eligibleForClick = false; }
}
