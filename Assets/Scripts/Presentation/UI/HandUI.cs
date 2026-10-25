
using TMPro;
using UnityEngine;

public class HandUI : MonoBehaviour
{
    [Header("손 텍스트")]
    [SerializeField] private TMP_Text handText;

    [Header("손 아이템 표시")]
    [SerializeField] private RectTransform itemContainer;

    [Header("프리팹 관리")]
    [SerializeField] private ItemPrefabRegistry prefabRegistry;

    [Header("기본 아이템 프리팹")]
    [SerializeField] private ItemIconView itemPrefab;

    [Header("손 장비 표시 크기")]
    [SerializeField, Min(1f)]
    private float displayWidth = 800f;

    [SerializeField, Min(1f)]
    private float displayHeight = 1200f;

    [Header("손 장비 하단 위치")]
    [SerializeField]
    private Vector2 bottomOffset = Vector2.zero;

    private HandData hand;
    private ItemAssemblyView currentView;
    private MovableItem cachedItem;

    private void Start()
    {
        GameBootstrap bootstrap = GameBootstrap.Instance;

        if (bootstrap == null || bootstrap.GameWorld == null)
        {
            Debug.LogError(
                "[HandUI] GameWorld가 없습니다.");

            enabled = false;
            return;
        }

        hand = bootstrap.GameWorld.Hand;

        if (prefabRegistry == null)
        {
            prefabRegistry =
                FindAnyObjectByType<ItemPrefabRegistry>();
        }

        Refresh();
    }

    public void Refresh()
    {
        if (hand == null)
            return;

        if (handText != null)
        {
            handText.text = hand.IsEmpty
                ? "손: 빈 칸"
                : "손:";
        }

        if (ReferenceEquals(cachedItem, hand.Value) &&
            (hand.IsEmpty || currentView != null))
        {
            return;
        }

        ClearItemView();
        cachedItem = hand.Value;

        if (hand.IsEmpty)
            return;

        if (itemContainer == null)
        {
            Debug.LogWarning(
                "[HandUI] Item Container가 없습니다.");
            return;
        }

        GameObject root = new GameObject(
            "HandItemVisual",
            typeof(RectTransform));

        root.transform.SetParent(
            itemContainer, false);

        ItemAssemblyView view =
            root.AddComponent<ItemAssemblyView>();

        // 손에서는 하단 고정 정렬 사용
        view.Initialize(
            hand.Value,
            prefabRegistry,
            ItemViewContext.Hand,
            itemPrefab,
            displayWidth,
            displayHeight,
            AssemblyAlignment.Bottom);

        RectTransform rect =
            root.GetComponent<RectTransform>();

        if (rect != null)
        {
            // ItemContainer의 아래쪽 중앙을 기준으로 고정
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            // Bottom alignment already places the visible bottom at the root origin.
            // Preserve the center pivot used by ItemAssemblyView child anchors.
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = bottomOffset;
        }

        currentView = view;
    }

    private void ClearItemView()
    {
        if (currentView != null)
        {
            currentView.gameObject.SetActive(false);
            Destroy(currentView.gameObject);
        }

        currentView = null;
    }
}
