using System;
public class InventoryData
{
    private readonly ItemSlot[] slots;
    public int Capacity => slots.Length;
    public InventoryData(int capacity) : this(capacity, null) { }
    internal InventoryData(int capacity, Predicate<MovableItem> accepts)
    {
        if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
        slots = new ItemSlot[capacity];
        for (int i = 0; i < capacity; i++) slots[i] = new ItemSlot(accepts);
    }
    public ItemSlot GetSlot(int index) => index < 0 || index >= slots.Length ? null : slots[index];
    public bool TryAdd(ItemData item) => TryAddValue(item);
    public bool TryAddValue(MovableItem value)
    {
        foreach (var slot in slots) if (slot.TryInsertValue(value)) return true;
        return false;
    }
}
