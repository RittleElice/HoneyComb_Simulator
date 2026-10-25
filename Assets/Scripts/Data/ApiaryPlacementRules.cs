public static class ApiaryPlacementRules
{
    // 양봉장 격자에 직접 배치 가능한 장비
    public static bool CanPlace(MovableItem item)
    {
        if (item == null)
            return true;

        if (item is ItemData data)
            return IsAllowedKind(data.Kind);

        if (item is ItemAssembly assembly)
        {
            if (assembly.Parts.Count == 0)
                return false;

            // 조립체를 구성하는 모든 부품 검사
            foreach (ItemData part in assembly.Parts)
            {
                if (!IsAllowedKind(part.Kind))
                    return false;
            }

            return true;
        }

        return false;
    }

    private static bool IsAllowedKind(ItemKind kind)
    {
        switch (kind)
        {
            case ItemKind.BroodBox:
            case ItemKind.Super:
            case ItemKind.Lid:
                return true;

            default:
                return false;
        }
    }
}