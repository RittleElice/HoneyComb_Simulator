public class ItemSlot
{
    public ItemData Item { get; private set; }

    public bool IsEmpty => Item == null;

    public bool TryInsert(ItemData item)
    {
        if (item == null || !IsEmpty)
            return false;

        Item = item;
        return true;
    }

    public ItemData Take()
    {
        ItemData item = Item;
        Item = null;

        return item;
    }

    public void Swap(ItemSlot other)
    {
        if (other == null || other == this)
            return;

        ItemData temp = Item;
        Item = other.Item;
        other.Item = temp;
    }
}