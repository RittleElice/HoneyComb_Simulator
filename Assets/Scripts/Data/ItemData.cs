public class ItemData : MovableItem
{
    public int Id { get; }
    public override string Name { get; }
    public object Content { get; }
    public ItemKind Kind { get; }

    // 단상·계상 내부의 범용 장비 슬롯
    public InventoryData InteriorSlots { get; }

    // 기존 코드와의 호환성
    public InventoryData Frames => InteriorSlots;

    public ItemData(int id, string name, object content = null)
        : this(id, name, ItemKind.Generic, content)
    {
    }

    public ItemData(
        int id,
        string name,
        ItemKind kind,
        object content = null)
    {
        Id = id;
        Name = name;
        Kind = kind;
        Content = content;

        if (kind == ItemKind.BroodBox ||
            kind == ItemKind.Super)
        {
            InteriorSlots = new InventoryData(
                10,
                HiveInteriorRules.CanInsert);
        }
    }

    public T GetContent<T>() where T : class
    {
        return Content as T;
    }
}