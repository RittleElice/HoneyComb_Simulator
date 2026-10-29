public static class HiveInteriorRules
{
    public static bool CanInsert(MovableItem value)
    {
        if (!(value is ItemData item))
            return false;

        switch (item.Kind)
        {
            case ItemKind.Frame:
                return true;

            // 향후 격리판·단열판 등을 여기에 추가

            default:
                return false;
        }
    }
}