
using UnityEngine;

public class ObservationItemView : MonoBehaviour
{
    [Header("Equipment Container")]
    [SerializeField] private RectTransform itemContainer;

    [Header("Prefab Registry")]
    [SerializeField] private ItemPrefabRegistry prefabRegistry;

    [Header("Fallback")]
    [SerializeField] private ItemIconView fallbackPrefab;

    [Header("Display Size")]
    [SerializeField, Min(1f)]
    private float maxWidth = 600f;

    [SerializeField, Min(1f)]
    private float maxHeight = 800f;

    private ItemAssemblyView currentView;

    private void Start()
    {
        if (prefabRegistry == null)
        {
            prefabRegistry =
                FindAnyObjectByType<ItemPrefabRegistry>();
        }
    }

    public void Show(MovableItem item)
    {
        Clear();

        if (item == null || itemContainer == null)
            return;

        if (prefabRegistry == null)
        {
            prefabRegistry =
                FindAnyObjectByType<ItemPrefabRegistry>();
        }

        GameObject root = new GameObject(
            "ObservedEquipment",
            typeof(RectTransform));

        root.transform.SetParent(itemContainer, false);

        currentView =
            root.AddComponent<ItemAssemblyView>();

        currentView.Initialize(
            item,
            prefabRegistry,
            ItemViewContext.Observation,
            fallbackPrefab,
            maxWidth,
            maxHeight,
            AssemblyAlignment.Center);

        RectTransform rect =
            root.GetComponent<RectTransform>();

        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
    }

    public void Clear()
    {
        if (currentView != null)
        {
            currentView.gameObject.SetActive(false);
            Destroy(currentView.gameObject);
            currentView = null;
        }
    }

    private void OnDestroy()
    {
        Clear();
    }
}
