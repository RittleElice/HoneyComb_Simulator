
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class ObservationInteriorContentsView : MonoBehaviour
{
    [Header("Display Scale")]
    [SerializeField, Min(0.01f)]
    private float displayScale = 1f;

    [SerializeField, Min(0.01f)]
    private float widthScale = 1f;

    [SerializeField, Min(0.01f)]
    private float heightScale = 1f;

    [Header("Position")]
    [SerializeField]
    private Vector2 positionOffset = Vector2.zero;

    private ItemData box;
    private ObservationFrameSlotLayout slotLayout;
    private ItemPrefabRegistry registry;
    private UIItemPartView fallbackPrefab;

    private ItemSlotObserver observer;
    private UIItemPartView[] visuals;
    private MovableItem[] displayedItems;

    private RectTransform visualRoot;

    public RectTransform VisualRoot => visualRoot;

    public void Initialize(
        ItemData targetBox,
        ObservationFrameSlotLayout layout,
        ItemPrefabRegistry prefabRegistry,
        UIItemPartView fallback)
    {
        ReleaseVisuals();
        observer?.Dispose();

        box = targetBox;
        slotLayout = layout;
        registry = prefabRegistry;
        fallbackPrefab = fallback;

        if (box == null ||
            box.Frames == null ||
            slotLayout == null)
            return;

        int count = box.Frames.Capacity;

        visuals = new UIItemPartView[count];
        displayedItems = new MovableItem[count];

        CreateVisualRoot();

        observer = new ItemSlotObserver(OnSlotChanged);

        for (int i = 0; i < count; i++)
        {
            ItemSlot slot = box.Frames.GetSlot(i);

            observer.Observe(slot);
            RefreshSlot(i);
        }
    }

    private void CreateVisualRoot()
    {
        GameObject root = new GameObject(
            "InteriorContentsVisuals",
            typeof(RectTransform));

        root.transform.SetParent(transform, false);

        visualRoot = root.GetComponent<RectTransform>();

        visualRoot.anchorMin = new Vector2(0.5f, 0.5f);
        visualRoot.anchorMax = new Vector2(0.5f, 0.5f);
        visualRoot.pivot = new Vector2(0.5f, 0.5f);
        visualRoot.anchoredPosition = Vector2.zero;
        visualRoot.localRotation = Quaternion.identity;
        visualRoot.localScale = Vector3.one;

        // Bind creates the attachment first; this runtime root is created later.
        GetComponent<ObservationTopAttachmentView>()?.UpdateRenderOrder();
    }

    private void OnSlotChanged(ItemSlot changedSlot)
    {
        if (box?.Frames == null)
            return;

        for (int i = 0; i < box.Frames.Capacity; i++)
        {
            if (ReferenceEquals(
                box.Frames.GetSlot(i),
                changedSlot))
            {
                RefreshSlot(i);
                return;
            }
        }
    }

    public void RefreshSlot(int index)
    {
        if (box?.Frames == null ||
            visuals == null ||
            index < 0 ||
            index >= visuals.Length)
            return;

        ItemSlot slot = box.Frames.GetSlot(index);
        MovableItem item = slot?.Value;

        if (ReferenceEquals(displayedItems[index], item))
        {
            if (visuals[index] != null)
                PositionVisual(index);

            return;
        }

        ReleaseSlot(index);

        if (!(item is ItemData part))
            return;

        IItemPartView created = ItemVisualFactory.Create(
            part,
            index,
            ItemViewContext.Observation,
            registry,
            fallbackPrefab != null
                ? fallbackPrefab.gameObject
                : null,
            visualRoot);

        UIItemPartView view = created as UIItemPartView;

        if (view == null)
            return;

        visuals[index] = view;
        displayedItems[index] = item;

        // 내부 소비장은 선택 판정을 방해하지 않음
        foreach (Graphic graphic in
                 view.GetComponentsInChildren<Graphic>(true))
        {
            graphic.raycastTarget = false;
        }

        PositionVisual(index);
    }

    private void PositionVisual(int index)
    {
        UIItemPartView view = visuals[index];

        if (view == null || slotLayout == null)
            return;

        RectTransform rect = view.RectTransform;

        rect.localRotation = Quaternion.identity;

        rect.localScale = new Vector3(
            displayScale * widthScale,
            displayScale * heightScale,
            1f);

        Vector3 targetWorld =
            slotLayout.GetSlotCenterWorld(index);

        // 위치 보정은 상자 로컬 좌표 기준
        targetWorld +=
            slotLayout.transform.TransformVector(
                new Vector3(
                    positionOffset.x,
                    positionOffset.y,
                    0f));

        Image image = view.Image;

        if (image == null)
        {
            rect.position = targetWorld;
            return;
        }

        // 실제 표시 이미지의 중심을 슬롯 중심에 정렬
        Rect visible = view.VisibleRect;

        Vector3 currentCenter =
            image.transform.TransformPoint(
                new Vector3(
                    visible.center.x,
                    visible.center.y,
                    0f));

        rect.position += targetWorld - currentCenter;
    }
    
    public bool TryGetFrameVisual(
        int index,
        ItemData expectedItem,
        out RectTransform rect)
    {
        rect = null;

        if (box == null ||
            box.Frames == null ||
            visuals == null ||
            displayedItems == null ||
            index < 0 ||
            index >= visuals.Length)
            return false;

        ItemSlot slot = box.Frames.GetSlot(index);

        if (slot == null ||
            !ReferenceEquals(slot.Value, expectedItem) ||
            !ReferenceEquals(displayedItems[index], expectedItem) ||
            visuals[index] == null)
            return false;

        rect = visuals[index].RectTransform;
        return rect != null;
    }


    private void ReleaseSlot(int index)
    {
        if (visuals == null ||
            index < 0 ||
            index >= visuals.Length)
            return;

        if (visuals[index] != null)
            ItemVisualFactory.Release(
                visuals[index].gameObject);

        visuals[index] = null;

        if (displayedItems != null)
            displayedItems[index] = null;
    }

    private void ReleaseVisuals()
    {
        if (visuals != null)
        {
            for (int i = 0; i < visuals.Length; i++)
                ReleaseSlot(i);
        }

        visuals = null;
        displayedItems = null;

        if (visualRoot != null)
            ItemVisualFactory.Release(visualRoot.gameObject);

        visualRoot = null;
    }

    private void OnDestroy()
    {
        observer?.Dispose();
        observer = null;

        ReleaseVisuals();
    }
}
