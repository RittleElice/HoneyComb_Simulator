public class ItemData : MovableItem
{
    public int Id { get; }
    public override string Name { get; }
    public object Content { get; }
    public ItemKind Kind { get; }
    public InventoryData Frames { get; }

    public ItemData(int id, string name, object content = null)
        : this(id, name, ItemKind.Generic, content) { }

    public ItemData(int id, string name, ItemKind kind, object content = null)
    {
        Id = id;
        Name = name;
        Kind = kind;
        Content = content;
        if (kind == ItemKind.BroodBox || kind == ItemKind.Super)
            Frames = new InventoryData(10, value => value is ItemData frame && frame.Kind == ItemKind.Frame);
    }

    public T GetContent<T>() where T : class => Content as T;
}
