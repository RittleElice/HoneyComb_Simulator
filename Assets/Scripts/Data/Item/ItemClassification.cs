public static class ItemClassification
{
    public static SlotCategory GetCategory(ItemKind kind)
    {
        switch (kind)
        {
            case ItemKind.BroodBox:
            case ItemKind.Super:
            case ItemKind.Lid:
                return SlotCategory.Assembly;

            case ItemKind.Frame:
                return SlotCategory.InteriorSlot;
            
            case ItemKind.QueenExcluder:
                return SlotCategory.TopSlot;

            default:
                return SlotCategory.General;
        }
    }
}