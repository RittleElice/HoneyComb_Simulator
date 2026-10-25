
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

    [Header("조립체 표시 크기")]
    [SerializeField, Range(0.1f, 1f)]
    private float slotFillRatio = 0.9f;

    private InventoryData inventory;
    private HandData hand;

    private readonly List<GameObject> slotObjects =
        new List<GameObject>();

    private readonly List<ItemAssemblyView> itemViews =
        new List<ItemAssemblyView>();

    private readonly List<MovableItem> cachedItems =
        new List<MovableItem>();

    private void Start()
    {
        GameBootstrap bootstrap = GameBootstrap.Instance;

        if (bootstrap == null ||
            bootstrap.GameWorld == null)
        {
            Debug.LogError(
                "[InventoryUI] GameWorld가 없습니다.");
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

        if (prefabRegistry == null)
        {
            prefabRegistry =
                FindAnyObjectByType<ItemPrefabRegistry>();
        }

        for (int i = 0; i < inventory.Capacity; i++)
        {
            int index = i;

            GameObject slotObject =
                Instantiate(slotPrefab, slotContainer);

            slotObjects.Add(slotObject);
            itemViews.Add(null);
            cachedItems.Add(null);

            Button button =
                slotObject.GetComponent<Button>();

            if (button != null)
            {
                button.onClick.AddListener(
                    () => OnSlotClicked(index));
            }
        }

        Refresh();
    }

    private void OnSlotClicked(int index)
    {
        ItemSlot selectedSlot = inventory.GetSlot(index);

        if (selectedSlot == null)
            return;

        if (selectionController != null)
            selectionController.ClearSelection();

        // 성공 여부를 직접 확인
        bool success =
            hand.Slot.TrySwap(selectedSlot);

        if (!success)
        {
            Debug.LogWarning(
                $"[InventoryUI] 슬롯 {index} 교환 실패");
            return;
        }

        Refresh();

        if (handUI != null)
            handUI.Refresh();
    }

    public void Refresh()
    {
        if (inventory == null)
            return;

        for (int i = 0; i < inventory.Capacity; i++)
        {
            ItemSlot slot = inventory.GetSlot(i);
            GameObject slotObject = slotObjects[i];

            // 아이콘 내부 텍스트를 잘못 수정하지 않도록
            // 슬롯의 직접 자식에서만 텍스트 조회
            TMP_Text slotText = null;

            foreach (Transform child in slotObject.transform)
            {
                if (child.GetComponent<ItemAssemblyView>() != null)
                    continue;

                slotText = child.GetComponent<TMP_Text>();

                if (slotText != null)
                    break;
            }

            if (slotText != null)
            {
                slotText.text =
                    slot.IsEmpty ? "빈 칸" : "";
            }

            if (ReferenceEquals(
                    cachedItems[i], slot.Value) &&
                (slot.IsEmpty || itemViews[i] != null))
            {
                continue;
            }

            ClearItemView(i);

            cachedItems[i] = slot.Value;

            if (slot.IsEmpty)
                continue;

            GameObject root =
                new GameObject(
                    "ItemVisual",
                    typeof(RectTransform));

            root.transform.SetParent(
                slotObject.transform, false);

            ItemAssemblyView view =
                root.AddComponent<ItemAssemblyView>();

            RectTransform slotRect =
                slotObject.GetComponent<RectTransform>();

            float width = slotRect != null
                ? slotRect.rect.width * slotFillRatio
                : 100f;

            float height = slotRect != null
                ? slotRect.rect.height * slotFillRatio
                : 100f;

            view.Initialize(
                slot.Value,
                prefabRegistry,
                ItemViewContext.Inventory,
                itemPrefab,
                width,
                height);

            itemViews[i] = view;
        }
    }

    private void ClearItemView(int index)
    {
        ItemAssemblyView view = itemViews[index];

        if (view != null)
        {
            view.gameObject.SetActive(false);
            Destroy(view.gameObject);
        }

        itemViews[index] = null;
    }
}
