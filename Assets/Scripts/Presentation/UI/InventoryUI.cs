using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class InventoryUI : MonoBehaviour
{
    [Header("슬롯 표시")]
    [SerializeField] private Transform slotContainer;
    [SerializeField] private GameObject slotPrefab;

    [Header("손 UI")]
    [SerializeField] private HandUI handUI;

    private InventoryData inventory;
    private HandData hand;

    private void Start()
    {
        GameWorld world = GameBootstrap.Instance.GameWorld;

        inventory = world.Inventory;
        hand = world.Hand;

        for (int i = 0; i < inventory.Capacity; i++)
        {
            int slotIndex = i;

            GameObject slotObject =
                Instantiate(slotPrefab, slotContainer);

            Button button = slotObject.GetComponent<Button>();

            if (button != null)
            {
                button.onClick.AddListener(
                    () => OnSlotClicked(slotIndex)
                );
            }
        }

        Refresh();
    }

    private void OnSlotClicked(int index)
    {
        ItemSlot selectedSlot = inventory.GetSlot(index);

        if (selectedSlot == null)
            return;

        // 손과 선택한 가방 슬롯의 아이템 교환
        hand.Swap(selectedSlot);

        Refresh();

        if (handUI != null)
            handUI.Refresh();
    }

    public void Refresh()
    {
        for (int i = 0; i < inventory.Capacity; i++)
        {
            Transform slotTransform = slotContainer.GetChild(i);

            TMP_Text slotText =
                slotTransform.GetComponentInChildren<TMP_Text>();

            ItemSlot slot = inventory.GetSlot(i);

            if (slotText != null)
            {
                slotText.text = slot.IsEmpty
                    ? "빈 칸"
                    : slot.Value.Name;
            }
        }
    }
}