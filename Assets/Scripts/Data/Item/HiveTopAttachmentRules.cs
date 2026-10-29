public static class HiveTopAttachmentRules
{
    // TopSlot 카테고리에 속하는 장비만 허용
    public static bool CanInsert(MovableItem value)
    {
        return value is ItemData item &&
               item.Category == SlotCategory.TopSlot;
    }

    public static bool HasTopSlot(ItemData item)
    {
        return item != null &&
               item.TopSlot != null;
    }

    // 상자 상단이 실제로 노출되어 있을 때만 접근 가능
    public static bool CanAccess(
        ItemData box,
        bool isTopExposed)
    {
        return HasTopSlot(box) && isTopExposed;
    }
}