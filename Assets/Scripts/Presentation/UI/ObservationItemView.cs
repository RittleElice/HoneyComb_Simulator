using UnityEngine;

public class ObservationItemView : MonoBehaviour
{
    [Header("Equipment Container")]
    [SerializeField] private RectTransform itemContainer;

    [Header("Ground Reference")]
    [SerializeField] private RectTransform groundImage;

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
    private RectTransform currentRoot;
    private RectTransform basePartRect;

    private void Start()
    {
        ResolveRegistry();
    }

    private void LateUpdate()
    {
        // 화면 크기나 UI 배치가 변경되어도 기준 위치를 유지한다.
        AlignBasePartToGround();
    }

    private void ResolveRegistry()
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

        ResolveRegistry();

        GameObject root = new GameObject(
            "ObservedEquipment",
            typeof(RectTransform));

        root.transform.SetParent(itemContainer, false);

        currentRoot = root.GetComponent<RectTransform>();
        currentView = root.AddComponent<ItemAssemblyView>();

        // 기존 표시·쌓기·크기 제한 로직을 그대로 사용한다.
        currentView.Initialize(
            item,
            prefabRegistry,
            ItemViewContext.Observation,
            fallbackPrefab,
            maxWidth,
            maxHeight,
            AssemblyAlignment.Center);

        currentRoot.anchorMin = new Vector2(0.5f, 0.5f);
        currentRoot.anchorMax = new Vector2(0.5f, 0.5f);
        currentRoot.pivot = new Vector2(0.5f, 0.5f);
        currentRoot.anchoredPosition = Vector2.zero;

        ItemData basePart = GetBasePart(item);
        basePartRect = FindPartRect(basePart);

        if (groundImage == null)
        {
            Debug.LogWarning(
                "[ObservationItemView] Ground Image를 연결하세요.",
                this);
        }
        else if (basePartRect == null)
        {
            Debug.LogWarning(
                "[ObservationItemView] 맨 아래 부품의 표시를 찾지 못했습니다. " +
                "관찰용 프리팹과 ItemIconView 연결을 확인하세요.",
                this);
        }

        AlignBasePartToGround();
    }

    private static ItemData GetBasePart(MovableItem item)
    {
        if (item is ItemAssembly assembly)
        {
            return assembly.Parts.Count > 0
                ? assembly.Parts[0]
                : null;
        }

        return item as ItemData;
    }

    private RectTransform FindPartRect(ItemData part)
    {
        if (part == null || currentRoot == null)
            return null;

        // 자식 순서가 아닌 실제 데이터 참조로 찾는다.
        // 프리팹이 누락되어도 다른 층을 단상으로 오인하지 않는다.
        for (int i = 0; i < currentRoot.childCount; i++)
        {
            Transform child = currentRoot.GetChild(i);

            if (!child.gameObject.activeSelf)
                continue;

            ItemIconView icon = child.GetComponent<ItemIconView>();

            if (icon != null && ReferenceEquals(icon.Item, part))
                return child as RectTransform;
        }

        return null;
    }

    private void AlignBasePartToGround()
    {
        if (currentRoot == null ||
            basePartRect == null ||
            groundImage == null)
        {
            return;
        }

        // 바닥 RectTransform의 중앙을 월드 좌표로 변환한다.
        // 바닥 자체의 Pivot 값과 관계없이 중앙을 구한다.
        Vector2 center = groundImage.rect.center;

        Vector3 groundCenter = groundImage.TransformPoint(
            new Vector3(center.x, center.y, 0f));

        // RectTransform.position은 해당 부품의 피벗 위치다.
        // 조립체 전체를 옮겨 부품 간 상대 위치는 보존한다.
        Vector3 correction = groundCenter - basePartRect.position;

        currentRoot.position += correction;
    }

    public void Clear()
    {
        basePartRect = null;
        currentRoot = null;

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