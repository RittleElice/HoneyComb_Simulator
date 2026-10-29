public static class ObservationFrameMoveAction
{
    public static bool TryMove(
        ItemSlot observedSlot,
        ItemData box,
        int sourceIndex,
        int targetIndex)
    {
        if (observedSlot == null ||
            box == null ||
            box.InteriorSlots == null)
            return false;

        // 현재 최상단에 노출된 상자만 조작 가능
        if (!ReferenceEquals(
                ObservationSelection.GetExposedBox(observedSlot),
                box))
            return false;

        // 격왕판 등 상단 장비가 있으면 이동 불가
        if (box.TopSlot != null &&
            !box.TopSlot.IsEmpty)
            return false;

        InventoryData slots = box.InteriorSlots;

        if (sourceIndex < 0 ||
            sourceIndex >= slots.Capacity ||
            targetIndex < 0 ||
            targetIndex >= slots.Capacity ||
            sourceIndex == targetIndex)
            return false;

        ItemSlot source = slots.GetSlot(sourceIndex);
        ItemSlot target = slots.GetSlot(targetIndex);

        if (source == null ||
            target == null ||
            source.IsEmpty ||
            !target.IsEmpty)
            return false;

        return source.TryMoveTo(target);
    }
}