
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

    private HandData hand;

    private ItemIconView currentView;
    private GameObject currentPrefab;

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

        Refresh();
    }

    // 아이템 종류 확인
    private ItemKind GetItemKind(MovableItem item)
    {
        if (item is ItemData data)
            return data.Kind;

        if (item is ItemAssembly assembly &&
            assembly.Parts.Count > 0)
        {
            // 조립체는 우선 최하단 장비의 종류 사용
            return assembly.Parts[0].Kind;
        }

        return ItemKind.Generic;
    }

    // 손에 사용할 프리팹 선택
    private GameObject GetItemPrefab(MovableItem item)
    {
        ItemKind kind = GetItemKind(item);

        if (prefabRegistry != null)
        {
            GameObject prefab = prefabRegistry.GetPrefab(
                kind,
                ItemViewContext.Hand);

            if (prefab != null)
                return prefab;
        }

        return itemPrefab != null
            ? itemPrefab.gameObject
            : null;
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

        // 손이 비어 있으면 아이콘 제거
        if (hand.IsEmpty)
        {
            ClearItemView();
            return;
        }

        GameObject desiredPrefab =
            GetItemPrefab(hand.Value);

        if (desiredPrefab == null)
        {
            ClearItemView();

            Debug.LogWarning(
                "[HandUI] 표시할 아이템 프리팹이 없습니다.");

            return;
        }

        // 같은 아이템과 같은 프리팹이면 재생성하지 않음
        if (currentView != null &&
            ReferenceEquals(currentView.Item, hand.Value) &&
            currentPrefab == desiredPrefab)
        {
            return;
        }

        ClearItemView();

        if (itemContainer == null)
        {
            Debug.LogWarning(
                "[HandUI] Item Container가 설정되지 않았습니다.");

            return;
        }

        GameObject iconObject = Instantiate(
            desiredPrefab,
            itemContainer);

        ItemIconView newView =
            iconObject.GetComponent<ItemIconView>();

        if (newView == null)
        {
            Debug.LogWarning(
                $"[HandUI] {desiredPrefab.name}에 " +
                "ItemIconView가 없습니다.");

            Destroy(iconObject);
            return;
        }

        newView.Initialize(hand.Value);

        RectTransform rect =
            iconObject.GetComponent<RectTransform>();

        if (rect != null)
        {
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
        }

        currentView = newView;
        currentPrefab = desiredPrefab;
    }

    private void ClearItemView()
    {
        if (currentView != null)
        {
            currentView.gameObject.SetActive(false);
            Destroy(currentView.gameObject);
        }

        currentView = null;
        currentPrefab = null;
    }
}
