
public static class ObservationFrameMoveAction
{
    // 출발 슬롯을 제외하고 목적지까지
    // 모든 슬롯이 비어 있는지 검사
    public static bool IsPathClear(
        ItemData box,
        int sourceIndex,
        int targetIndex)
    {
        if (box == null ||
            box.InteriorSlots == null)
            return false;

        InventoryData slots = box.InteriorSlots;

        if (sourceIndex < 0 ||
            sourceIndex >= slots.Capacity ||
            targetIndex < 0 ||
            targetIndex >= slots.Capacity)
            return false;

        if (sourceIndex == targetIndex)
            return true;

        int direction =
            targetIndex > sourceIndex ? 1 : -1;

        for (int i = sourceIndex + direction;
             i != targetIndex + direction;
             i += direction)
        {
            ItemSlot slot = slots.GetSlot(i);

            if (slot == null || !slot.IsEmpty)
                return false;
        }

        return true;
    }

    // 포인터가 가리키는 목적지까지 이동하되
    // 장애물이 있으면 직전 빈 슬롯에서 멈춘다.
    public static int GetReachableIndex(
        ItemData box,
        int sourceIndex,
        int requestedIndex)
    {
        if (box == null ||
            box.InteriorSlots == null)
            return -1;

        InventoryData slots = box.InteriorSlots;

        if (sourceIndex < 0 ||
            sourceIndex >= slots.Capacity ||
            requestedIndex < 0 ||
            requestedIndex >= slots.Capacity)
            return -1;

        int direction =
            requestedIndex > sourceIndex ? 1 : -1;

        int reachable = sourceIndex;

        for (int i = sourceIndex + direction;
             direction > 0
                 ? i <= requestedIndex
                 : i >= requestedIndex;
             i += direction)
        {
            ItemSlot slot = slots.GetSlot(i);

            if (slot == null || !slot.IsEmpty)
                break;

            reachable = i;
        }

        return reachable;
    }

    // 실제 데이터 이동은 드롭 시에만 실행
    public static bool TryMove(
        ItemSlot observedSlot,
        ItemData box,
        int sourceIndex,
        int targetIndex)
    {
        if (observedSlot == null ||
            box == null ||
            box.InteriorSlots == null ||
            sourceIndex == targetIndex)
            return false;

        if (!ReferenceEquals(
            ObservationSelection.GetExposedBox(observedSlot),
            box))
            return false;

        // 상단 장비가 있으면 내부 이동 불가
        if (box.TopSlot != null &&
            !box.TopSlot.IsEmpty)
            return false;

        InventoryData slots = box.InteriorSlots;

        if (sourceIndex < 0 ||
            sourceIndex >= slots.Capacity ||
            targetIndex < 0 ||
            targetIndex >= slots.Capacity)
            return false;

        ItemSlot source = slots.GetSlot(sourceIndex);
        ItemSlot target = slots.GetSlot(targetIndex);

        if (source == null ||
            target == null ||
            source.IsEmpty ||
            !target.IsEmpty)
            return false;

        if (!(source.Value is ItemData item) ||
            item.Kind != ItemKind.Frame)
            return false;

        // 중간에 장비가 있으면 이동 거부
        if (!IsPathClear(box, sourceIndex, targetIndex))
            return false;

        return source.TryMoveTo(target);
    }
}
