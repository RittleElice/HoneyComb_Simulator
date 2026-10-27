
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
    private ItemSlot observedSlot;

    private ItemAssemblyView currentView;
    private MovableItem cachedItem;

    private void Start()
    {
        BindHand();
        Refresh();
    }

    private void BindHand()
    {
        GameBootstrap bootstrap = GameBootstrap.Instance;

        if (bootstrap == null ||
            bootstrap.GameWorld == null)
        {
            Debug.LogError(
                "[HandUI] GameWorld가 없습니다.");
            return;
        }

        HandData currentHand = bootstrap.GameWorld.Hand;

        if (currentHand == null)
            return;

        ItemSlot currentSlot = currentHand.Slot;

        if (currentSlot == null)
            return;

        // 이미 같은 슬롯을 구독 중이면 중복 연결 방지
        if (ReferenceEquals(observedSlot, currentSlot))
        {
            hand = currentHand;
            return;
        }

        UnbindHand();

        hand = currentHand;
        observedSlot = currentSlot;

        observedSlot.OnChanged += OnHandSlotChanged;

        if (prefabRegistry == null)
        {
            prefabRegistry =
                FindAnyObjectByType<ItemPrefabRegistry>();
        }
    }

    private void UnbindHand()
    {
        if (observedSlot != null)
            observedSlot.OnChanged -= OnHandSlotChanged;

        observedSlot = null;
        hand = null;
    }

    private void OnHandSlotChanged(ItemSlot slot)
    {
        Refresh();
    }

    public void Refresh()
    {
        // 새 게임 시작 등으로 HandData가 변경된 경우 재연결
        BindHand();

        if (hand == null)
        {
            ClearItemView();
            cachedItem = null;
            return;
        }

        if (handText != null)
        {
            handText.text = hand.IsEmpty
                ? "손: 빈 칸"
                : "손:";
        }

        // 손이 비었으면 이미지 반드시 제거
        if (hand.IsEmpty)
        {
            ClearItemView();
            cachedItem = null;
            return;
        }

        // 같은 아이템의 이미지가 정상적으로 존재하면 재사용
        if (ReferenceEquals(cachedItem, hand.Value) &&
            currentView != null &&
            currentView.gameObject.activeInHierarchy)
        {
            return;
        }

        ClearItemView();

        cachedItem = hand.Value;

        if (itemContainer == null)
        {
            Debug.LogWarning(
                "[HandUI] Item Container가 없습니다.");
            return;
        }

        if (prefabRegistry == null)
        {
            prefabRegistry =
                FindAnyObjectByType<ItemPrefabRegistry>();
        }

        GameObject root = new GameObject(
            "HandItemVisual",
            typeof(RectTransform));

        root.transform.SetParent(
            itemContainer, false);

        ItemAssemblyView view =
            root.AddComponent<ItemAssemblyView>();

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
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
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

    private void OnDestroy()
    {
        UnbindHand();
        ClearItemView();
    }
}
