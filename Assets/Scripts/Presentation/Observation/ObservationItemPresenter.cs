
using UnityEngine;

public class ObservationItemPresenter : MonoBehaviour
{
    [Header("Equipment Container")]
    [SerializeField] private RectTransform itemContainer;

    [Header("Ground Reference")]
    [SerializeField] private RectTransform groundImage;

    [Header("Prefab Registry")]
    [SerializeField] private ItemPrefabRegistry prefabRegistry;

    [Header("Fallback")]
    [SerializeField] private UIItemPartView fallbackPrefab;

    [Header("Display Size")]
    [SerializeField, Min(1f)]
    private float maxWidth = 600f;

    [SerializeField, Min(1f)]
    private float maxHeight = 800f;

    [Header("Interior Equipment Display")]
    [SerializeField, Min(0.01f)]
    private float interiorDisplayScale = 1f;

    private ItemAssemblyView currentView;

    public ItemAssemblyView CurrentView => currentView;

    private void ResolveRegistry()
    {
        if (prefabRegistry == null)
            prefabRegistry =
                FindAnyObjectByType<ItemPrefabRegistry>();
    }

    public void Show(MovableItem item)
    {
        Clear();

        if (item == null || itemContainer == null)
            return;

        ResolveRegistry();

        GameObject root = new GameObject(
            "ObservedEquipment",
            typeof(RectTransform));

        root.transform.SetParent(itemContainer, false);

        currentView = root.AddComponent<ItemAssemblyView>();

        currentView.Initialize(
            item,
            prefabRegistry,
            ItemViewContext.Observation,
            fallbackPrefab,
            maxWidth,
            maxHeight,
            AssemblyAlignment.BaseAnchor,
            AssemblyScaleMode.Fixed);

        InitializeInteriorViews();

        if (groundImage == null)
        {
            Debug.LogWarning(
                "[ObservationItemPresenter] Ground Image를 연결하세요.",
                this);
        }

        AlignToGround();
    }

    private void InitializeInteriorViews()
    {
        if (currentView == null)
            return;

        foreach (UIItemPartView partView in currentView.Parts)
        {
            ItemData part = partView.Part;

            if (part == null ||
                part.Frames == null)
                continue;

            ObservationFrameSlotLayout layout =
                partView.GetComponent<ObservationFrameSlotLayout>();

            if (layout == null)
            {
                Debug.LogWarning(
                    $"[ObservationItemPresenter] " +
                    $"{part.Name}에 ObservationFrameSlotLayout이 없습니다.",
                    partView);
                continue;
            }

            ObservationInteriorContentsView contents =
                partView.GetComponent<ObservationInteriorContentsView>();

            if (contents == null)
            {
                contents =
                    partView.gameObject.AddComponent<
                        ObservationInteriorContentsView>();
            }

            contents.Initialize(
                part,
                layout,
                prefabRegistry,
                fallbackPrefab);
        }
    }

    private void LateUpdate()
    {
        AlignToGround();
    }

    private void AlignToGround()
    {
        if (currentView == null ||
            groundImage == null)
            return;

        Vector2 center = groundImage.rect.center;

        currentView.AlignBaseTo(
            groundImage.TransformPoint(
                new Vector3(
                    center.x,
                    center.y,
                    0f)));
    }

    public void Clear()
    {
        if (currentView != null)
            ItemVisualFactory.Release(
                currentView.gameObject);

        currentView = null;
    }

    private void OnDestroy()
    {
        Clear();
    }
}
