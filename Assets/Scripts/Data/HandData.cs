public class HandData
{
    public ItemSlot Slot { get; } = new ItemSlot();
    public bool IsEmpty => Slot.IsEmpty;
    public ItemData Item => Slot.Item;
    public MovableItem Value => Slot.Value;
    public void Swap(ItemSlot otherSlot) => Slot.Swap(otherSlot);
}
