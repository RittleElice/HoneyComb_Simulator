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
    [SerializeField, Min(1f)] private float maxWidth = 600f;
    [SerializeField, Min(1f)] private float maxHeight = 800f;

    private ItemAssemblyView currentView;
    public ItemAssemblyView CurrentView => currentView;

    private void ResolveRegistry()
    {
        if (prefabRegistry == null)
            prefabRegistry = FindAnyObjectByType<ItemPrefabRegistry>();
    }

    public void Show(MovableItem item)
    {
        Clear();
        if (item == null || itemContainer == null)
            return;
        ResolveRegistry();

        GameObject root = new GameObject("ObservedEquipment", typeof(RectTransform));
        root.transform.SetParent(itemContainer, false);
        currentView = root.AddComponent<ItemAssemblyView>();
        currentView.Initialize(item, prefabRegistry, ItemViewContext.Observation,
            fallbackPrefab, maxWidth, maxHeight,
            AssemblyAlignment.BaseAnchor, AssemblyScaleMode.FitWithinBounds);

        if (groundImage == null)
            Debug.LogWarning("[ObservationItemPresenter] Ground Image를 연결하세요.", this);
        AlignToGround();
    }

    private void LateUpdate() => AlignToGround();

    private void AlignToGround()
    {
        if (currentView == null || groundImage == null)
            return;
        Vector2 center = groundImage.rect.center;
        currentView.AlignBaseTo(groundImage.TransformPoint(new Vector3(center.x, center.y, 0f)));
    }

    public void Clear()
    {
        if (currentView != null)
            ItemVisualFactory.Release(currentView.gameObject);
        currentView = null;
    }

    private void OnDestroy() => Clear();
}
