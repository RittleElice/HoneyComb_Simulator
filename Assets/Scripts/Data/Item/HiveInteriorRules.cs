public static class HiveInteriorRules
{
    public static bool CanInsert(MovableItem value)
    {
        return value is ItemData item &&
               item.Category == SlotCategory.InteriorSlot;
    }
}