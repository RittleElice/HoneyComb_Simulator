using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class InventoryItemPresenter : MonoBehaviour
{
    [Header("슬롯 표시")]
    [SerializeField] private Transform slotContainer;
    [SerializeField] private GameObject slotPrefab;

    [Header("아이템 프리팹 관리")]
    [SerializeField] private ItemPrefabRegistry prefabRegistry;

    [Header("기본 아이템 프리팹")]
    [SerializeField] private UIItemPartView itemPrefab;

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

    private ItemSlotObserver slotObserver;
    private bool started;
    private bool refreshPending;

    private void Start()
    {
        started = true;
        Refresh();
    }

    private void OnEnable()
    {
        if (started)
            Refresh();
    }

    private void OnDisable()
    {
        slotObserver?.Clear();
        refreshPending = true;
    }

    private void Update()
    {
        GameBootstrap bootstrap = GameBootstrap.Instance;
        InventoryData source = bootstrap != null && bootstrap.GameWorld != null
            ? bootstrap.GameWorld.Inventory
            : null;

        if (refreshPending || !ReferenceEquals(inventory, source))
            Refresh();
    }

    private void OnSlotChanged(ItemSlot slot)
    {
        // Coalesce several slot notifications into one display refresh.
        refreshPending = true;
    }

    private bool BindInventory()
    {
        GameBootstrap bootstrap = GameBootstrap.Instance;
        InventoryData source = bootstrap != null && bootstrap.GameWorld != null
            ? bootstrap.GameWorld.Inventory
            : null;

        if (source == null)
        {
            slotObserver?.Clear();
            if (inventory != null || slotObjects.Count > 0)
                ClearSlots();
            inventory = null;
            hand = null;
            return false;
        }

        if (slotContainer == null || slotPrefab == null)
        {
            Debug.LogError("[InventoryItemPresenter] Slot Container / Slot Prefab을 연결하세요.", this);
            enabled = false;
            return false;
        }

        if (slotObserver == null)
            slotObserver = new ItemSlotObserver(OnSlotChanged);

        if (!ReferenceEquals(inventory, source))
        {
            slotObserver.Clear();
            ClearSlots();
            inventory = source;
        }

        hand = bootstrap.GameWorld.Hand;

        if (prefabRegistry == null)
            prefabRegistry = FindAnyObjectByType<ItemPrefabRegistry>();

        if (slotObjects.Count != inventory.Capacity)
        {
            ClearSlots();
            for (int i = 0; i < inventory.Capacity; i++)
            {
                int index = i;
                GameObject slotObject = Instantiate(slotPrefab, slotContainer);
                slotObjects.Add(slotObject);
                itemViews.Add(null);
                cachedItems.Add(null);

                Button button = slotObject.GetComponent<Button>();
                if (button != null)
                    button.onClick.AddListener(() => OnSlotClicked(index));
            }
        }

        // Observe ignores duplicates, including repeated explicit Refresh calls.
        for (int i = 0; i < inventory.Capacity; i++)
            slotObserver.Observe(inventory.GetSlot(i));

        return true;
    }

    private void OnSlotClicked(int index)
    {
        if (!isActiveAndEnabled || !BindInventory() || hand == null)
            return;

        ItemSlot selectedSlot = inventory.GetSlot(index);
        if (selectedSlot == null)
            return;

        ApiarySelectionController controller =
            FindAnyObjectByType<ApiarySelectionController>();
        if (controller != null)
            controller.ClearSelection();

        hand.Swap(selectedSlot);
        Refresh();

    }

    public void Refresh()
    {
        if (!isActiveAndEnabled)
            return;

        refreshPending = false;
        if (!BindInventory())
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

    private void ClearSlots()
    {
        for (int i = 0; i < itemViews.Count; i++)
            ClearItemView(i);

        foreach (GameObject slotObject in slotObjects)
        {
            if (slotObject == null)
                continue;
            slotObject.SetActive(false);
            Destroy(slotObject);
        }

        slotObjects.Clear();
        itemViews.Clear();
        cachedItems.Clear();
    }

    private void OnDestroy()
    {
        slotObserver?.Dispose();
        ClearSlots();
    }
}
