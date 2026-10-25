
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class InventoryUI : MonoBehaviour
{
    [Header("슬롯 표시")]
    [SerializeField] private Transform slotContainer;
    [SerializeField] private GameObject slotPrefab;

    [Header("아이템 프리팹 관리")]
    [SerializeField] private ItemPrefabRegistry prefabRegistry;

    [Header("기본 아이템 프리팹")]
    [SerializeField] private ItemIconView itemPrefab;

    [Header("손 UI")]
    [SerializeField] private HandUI handUI;

    [Header("양봉장 선택")]
    [SerializeField]
    private ApiarySelectionController selectionController;

    private InventoryData inventory;
    private HandData hand;

    private readonly List<GameObject> slotObjects =
        new List<GameObject>();

    private readonly List<ItemIconView> itemViews =
        new List<ItemIconView>();

    private readonly List<GameObject> itemPrefabs =
        new List<GameObject>();

    private void Start()
    {
        GameBootstrap bootstrap = GameBootstrap.Instance;

        if (bootstrap == null || bootstrap.GameWorld == null)
        {
            Debug.LogError("[InventoryUI] GameWorld가 없습니다.");
            enabled = false;
            return;
        }

        inventory = bootstrap.GameWorld.Inventory;
        hand = bootstrap.GameWorld.Hand;

        if (selectionController == null)
        {
            selectionController =
                FindAnyObjectByType<ApiarySelectionController>();
        }

        for (int i = 0; i < inventory.Capacity; i++)
        {
            int slotIndex = i;

            GameObject slotObject =
                Instantiate(slotPrefab, slotContainer);

            slotObjects.Add(slotObject);
            itemViews.Add(null);
            itemPrefabs.Add(null);

            Button button = slotObject.GetComponent<Button>();

            if (button != null)
            {
                button.onClick.AddListener(
                    () => OnSlotClicked(slotIndex));
            }
        }

        Refresh();
    }

    private void OnSlotClicked(int index)
    {
        ItemSlot selectedSlot = inventory.GetSlot(index);

        if (selectedSlot == null)
            return;

        // 인벤토리 조작 시 양봉장 선택 해제
        // OnSelectionCleared 이벤트로 행동 메뉴도 닫힌다.
        if (selectionController != null)
            selectionController.ClearSelection();

        // 기존 손-인벤토리 교환 기능 유지
        hand.Swap(selectedSlot);

        Refresh();

        if (handUI != null)
            handUI.Refresh();
    }

    private GameObject GetItemPrefab(MovableItem item)
    {
        ItemKind kind = ItemKind.Generic;

        if (item is ItemData data)
        {
            kind = data.Kind;
        }
        else if (item is ItemAssembly assembly &&
                 assembly.Parts.Count > 0)
        {
            // 조립체는 최하단 장비 종류 사용
            kind = assembly.Parts[0].Kind;
        }

        if (prefabRegistry != null)
        {
            GameObject prefab = prefabRegistry.GetPrefab(
                kind,
                ItemViewContext.Inventory);

            if (prefab != null)
                return prefab;
        }

        return itemPrefab != null
            ? itemPrefab.gameObject
            : null;
    }

    public void Refresh()
    {
        if (inventory == null)
            return;

        for (int i = 0; i < inventory.Capacity; i++)
        {
            ItemSlot slot = inventory.GetSlot(i);
            GameObject slotObject = slotObjects[i];

            TMP_Text slotText =
                slotObject.GetComponentInChildren<TMP_Text>();

            if (slotText != null)
                slotText.text = slot.IsEmpty ? "빈 칸" : "";

            ItemIconView currentView = itemViews[i];

            if (slot.IsEmpty)
            {
                ClearItemView(i);
                continue;
            }

            GameObject desiredPrefab = GetItemPrefab(slot.Value);

            if (desiredPrefab == null)
            {
                ClearItemView(i);
                continue;
            }

            if (currentView != null &&
                ReferenceEquals(currentView.Item, slot.Value) &&
                itemPrefabs[i] == desiredPrefab)
            {
                continue;
            }

            ClearItemView(i);

            GameObject iconObject = Instantiate(
                desiredPrefab,
                slotObject.transform);

            ItemIconView newView =
                iconObject.GetComponent<ItemIconView>();

            if (newView == null)
            {
                Debug.LogWarning(
                    $"[InventoryUI] {desiredPrefab.name}에 " +
                    "ItemIconView가 없습니다.");

                iconObject.SetActive(false);
                Destroy(iconObject);
                continue;
            }

            newView.Initialize(slot.Value);

            RectTransform rect =
                iconObject.GetComponent<RectTransform>();

            if (rect != null)
            {
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = Vector2.zero;
            }

            itemViews[i] = newView;
            itemPrefabs[i] = desiredPrefab;
        }
    }

    private void ClearItemView(int index)
    {
        ItemIconView view = itemViews[index];

        if (view != null)
        {
            view.gameObject.SetActive(false);
            Destroy(view.gameObject);
        }

        itemViews[index] = null;
        itemPrefabs[index] = null;
    }
}
