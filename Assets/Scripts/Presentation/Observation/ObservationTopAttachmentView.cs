
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
public class ObservationTopAttachmentView :
    MonoBehaviour, IItemDataBound
{
    [Header("References")]
    [SerializeField]
    private RectTransform attachmentArea;

    [SerializeField]
    private ItemPrefabRegistry prefabRegistry;

    [SerializeField]
    private UIItemPartView fallbackPrefab;

    [Header("Position")]
    [SerializeField]
    private Vector2 positionOffset = Vector2.zero;

    [Header("Scale")]
    [SerializeField, Min(0.01f)]
    private float displayScale = 1f;

    [SerializeField, Min(0.01f)]
    private float widthScale = 1f;

    [SerializeField, Min(0.01f)]
    private float heightScale = 1f;

    private ItemData box;
    private ItemSlot topSlot;

    private UIItemPartView currentView;
    private MovableItem displayedItem;

    private ItemSlotObserver observer;

    public ItemData Box => box;
    public ItemSlot TopSlot => topSlot;
    public UIItemPartView CurrentView => currentView;

    // UIItemPartView.Bind()에서 자동 호출
    public void BindItem(ItemData item)
    {
        ReleaseVisual();

        observer?.Dispose();
        observer = null;

        box = item;
        topSlot = box != null ? box.TopSlot : null;

        if (topSlot == null)
            return;

        ResolveRegistry();

        observer = new ItemSlotObserver(_ => Refresh());
        observer.Observe(topSlot);

        Refresh();
    }

    private void ResolveRegistry()
    {
        if (prefabRegistry == null)
            prefabRegistry =
                FindAnyObjectByType<ItemPrefabRegistry>();
    }

    
    public void Refresh()
    {
        if (topSlot == null)
        {
            ReleaseVisual();
            return;
        }

        MovableItem item = topSlot.Value;

        if (ReferenceEquals(displayedItem, item))
        {
            PositionVisual();
            UpdateRenderOrder();
            return;
        }

        ReleaseVisual();

        if (!(item is ItemData part))
            return;

        ResolveRegistry();

        Transform parent =
            attachmentArea != null
                ? attachmentArea
                : transform;

        IItemPartView created = ItemVisualFactory.Create(
            part,
            0,
            ItemViewContext.Observation,
            prefabRegistry,
            fallbackPrefab != null
                ? fallbackPrefab.gameObject
                : null,
            parent);

        currentView = created as UIItemPartView;

        if (currentView == null)
            return;

        displayedItem = item;

        foreach (Graphic graphic in
                 currentView.GetComponentsInChildren<Graphic>(true))
        {
            graphic.raycastTarget = false;
        }

        PositionVisual();
        UpdateRenderOrder();
    }
    
    public void UpdateRenderOrder()
    {
        var contents = GetComponent<ObservationInteriorContentsView>();
        Transform interior = contents != null ? contents.VisualRoot : null;
        if (currentView == null || interior == null || interior.parent != transform)
            return;

        // Stay inside this box: never reorder the assembly or selection UI roots.
        Transform attachment = currentView.transform;
        if (!attachment.IsChildOf(transform) ||
            SortingCanvas(attachment) != SortingCanvas(interior))
            return;

        // The attachment can be nested under a local display container.
        while (attachment.parent != transform)
            attachment = attachment.parent;

        if (attachment != interior &&
            interior.GetSiblingIndex() > attachment.GetSiblingIndex())
            interior.SetSiblingIndex(attachment.GetSiblingIndex());
    }

    private static Canvas SortingCanvas(Transform visual)
    {
        for (Transform parent = visual; parent != null; parent = parent.parent)
        {
            Canvas canvas = parent.GetComponent<Canvas>();
            if (canvas != null && (canvas.overrideSorting || canvas.isRootCanvas))
                return canvas;
        }
        return null;
    }

    private void PositionVisual()
    {
        if (currentView == null)
            return;

        RectTransform rect = currentView.RectTransform;

        rect.localRotation = Quaternion.identity;
        rect.localScale = new Vector3(
            displayScale * widthScale,
            displayScale * heightScale,
            1f);

        RectTransform area =
            attachmentArea != null
                ? attachmentArea
                : (RectTransform)transform;

        Vector3 targetWorld = area.TransformPoint(
            new Vector3(
                area.rect.center.x + positionOffset.x,
                area.rect.center.y + positionOffset.y,
                0f));

        Image image = currentView.Image;

        if (image == null)
        {
            rect.position = targetWorld;
            return;
        }

        Rect visible = currentView.VisibleRect;

        Vector3 currentCenter =
            image.transform.TransformPoint(
                new Vector3(
                    visible.center.x,
                    visible.center.y,
                    0f));

        rect.position += targetWorld - currentCenter;
    }

    private void ReleaseVisual()
    {
        if (currentView != null)
            ItemVisualFactory.Release(currentView.gameObject);

        currentView = null;
        displayedItem = null;
    }

    private void OnDestroy()
    {
        observer?.Dispose();
        observer = null;

        ReleaseVisual();
    }
}
