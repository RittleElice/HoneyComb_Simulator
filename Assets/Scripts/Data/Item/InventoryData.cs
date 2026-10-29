using System;

public class InventoryData
{
    private readonly ItemSlot[] slots;

    public int Capacity => slots.Length;

    public InventoryData(int capacity)
        : this(capacity, null)
    {
    }

    internal InventoryData(
        int capacity,
        Predicate<MovableItem> accepts)
    {
        if (capacity <= 0)
            throw new ArgumentOutOfRangeException(
                nameof(capacity));

        slots = new ItemSlot[capacity];

        for (int i = 0; i < capacity; i++)
        {
            // 인벤토리에서는 단일 장비와 조립체 모두 허용
            slots[i] = new ItemSlot(accepts, true);
        }
    }

    public ItemSlot GetSlot(int index)
    {
        if (index < 0 || index >= slots.Length)
            return null;

        return slots[index];
    }

    public bool TryAdd(ItemData item)
    {
        return TryAddValue(item);
    }

    public bool TryAddValue(MovableItem value)
    {
        foreach (ItemSlot slot in slots)
        {
            if (slot.TryInsertValue(value))
                return true;
        }

        return false;
    }
}