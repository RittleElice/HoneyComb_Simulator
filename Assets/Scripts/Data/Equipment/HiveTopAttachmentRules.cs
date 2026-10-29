public static class HiveTopAttachmentRules
{
    public static bool CanInsert(MovableItem value)
    {
        return value is ItemData item &&
               item.Kind == ItemKind.QueenExcluder;
    }

    public static bool HasTopSlot(ItemData item)
    {
        return item != null &&
               item.TopSlot != null;
    }
}