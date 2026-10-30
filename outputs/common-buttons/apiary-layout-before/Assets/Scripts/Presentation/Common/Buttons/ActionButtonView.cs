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
    private Image icon;
    private TMP_Text label;
    private ActionButtonPositioner positioner;
    public ActionButtonId Id => id;
    public bool HasStyle => manager != null && manager.Catalog != null && manager.Catalog.Find(id) != null;
    public RectTransform RelativeTarget { get => relativeTarget; set => relativeTarget = value; }

    public void Bind(ActionButtonManager owner, ActionButtonId action, string text)
    {
        manager = owner; id = action; contextualLabel = text;
        positioner = GetComponent<ActionButtonPositioner>() ?? gameObject.AddComponent<ActionButtonPositioner>();
        label = GetComponentInChildren<TMP_Text>(true);
        Apply();
    }

    public void SetFollowPoint(Vector3 worldPoint, Camera camera, bool above, float gap)
    {
        positioner?.SetFollowPoint(worldPoint, camera, above, gap);
    }

    public void Apply()
    {
        var entry = manager != null && manager.Catalog != null ? manager.Catalog.Find(id) : null;
        if (entry == null) return; // Legacy presentation remains usable without a catalog entry.
        if (positioner == null) positioner = GetComponent<ActionButtonPositioner>() ?? gameObject.AddComponent<ActionButtonPositioner>();
        if (label == null) label = GetComponentInChildren<TMP_Text>(true);
        var rect = (RectTransform)transform;
        rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, Mathf.Max(1, entry.size.x));
        rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, Mathf.Max(1, entry.size.y));
        var layout = GetComponent<LayoutElement>() ?? gameObject.AddComponent<LayoutElement>();
        layout.ignoreLayout = !entry.useLayoutSlot || entry.placement != ActionButtonPlacement.Relative;
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
            label.text = !string.IsNullOrEmpty(contextualLabel) ? contextualLabel : entry.label;
            label.enableAutoSizing = true; label.fontSizeMin = 12; label.fontSizeMax = 25;
            var r = label.rectTransform; r.anchorMin = Vector2.zero;
            r.anchorMax = new Vector2(1, entry.icon != null ? .34f : 1);
            r.offsetMin = new Vector2(3, 2); r.offsetMax = new Vector2(-3, -2);
        }
        var button = GetComponent<Button>();
        if (button.targetGraphic != null) button.targetGraphic.raycastTarget = true;
        positioner.Apply(entry, relativeTarget);
    }
    private void LateUpdate() => Apply();
    // Claim drags that start on a button; never pass them to a parent/world gesture.
    public void OnBeginDrag(PointerEventData e) { e.eligibleForClick = false; }
    public void OnDrag(PointerEventData e) { e.eligibleForClick = false; }
    public void OnEndDrag(PointerEventData e) { e.eligibleForClick = false; }
}
