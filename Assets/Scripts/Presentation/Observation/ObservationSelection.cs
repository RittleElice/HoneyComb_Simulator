
using System;

public enum ObservationSelectionKind
{
    None,
    Ground,
    EquipmentPart,
    FrameSlot
}

// 실제 장비와 슬롯을 참조하며 데이터를 복제하지 않음
public sealed class ObservationSelection
{
    public ObservationSelectionKind Kind { get; private set; }

    // 관찰 중인 양봉장 격자의 슬롯
    public ItemSlot Slot { get; private set; }

    // 선택한 실제 장비
    public ItemData Part { get; private set; }

    // 조립체 내부 장비 인덱스
    public int PartIndex { get; private set; } = -1;

    // 소비장 슬롯 인덱스 (0~9)
    public int FrameSlotIndex { get; private set; } = -1;

    public void Clear()
    {
        Kind = ObservationSelectionKind.None;
        Slot = null;
        Part = null;
        PartIndex = -1;
        FrameSlotIndex = -1;
    }

    public void SelectGround(ItemSlot slot)
    {
        Clear();

        if (slot == null)
            return;

        Slot = slot;
        Kind = ObservationSelectionKind.Ground;
    }

    public bool SelectPart(ItemSlot slot, ItemData part)
    {
        Clear();

        if (slot == null || part == null)
            return false;

        Slot = slot;
        Part = part;
        Kind = ObservationSelectionKind.EquipmentPart;

        return Revalidate(slot);
    }

    public bool SelectFrameSlot(
        ItemSlot slot,
        ItemData box,
        int index)
    {
        Clear();

        if (slot == null ||
            box == null ||
            box.Frames == null ||
            index < 0 ||
            index >= 10)
            return false;

        Slot = slot;
        Part = box;
        FrameSlotIndex = index;
        Kind = ObservationSelectionKind.FrameSlot;

        return Revalidate(slot);
    }

    // 현재 접근 가능한 최상단 상자
    public static ItemData GetExposedBox(ItemSlot slot)
    {
        if (slot == null || slot.IsEmpty)
            return null;

        var parts = ItemVisualFactory.GetParts(slot.Value);

        if (parts == null || parts.Count == 0)
            return null;

        ItemData top = parts[parts.Count - 1];

        if (top == null)
            return null;

        if (top.Kind != ItemKind.BroodBox &&
            top.Kind != ItemKind.Super)
            return null;

        return top.Frames != null ? top : null;
    }

    public ItemSlot GetSelectedFrameSlot()
    {
        if (Kind != ObservationSelectionKind.FrameSlot)
            return null;

        if (!Revalidate(Slot))
            return null;

        return Part.Frames.GetSlot(FrameSlotIndex);
    }

    public bool Revalidate(ItemSlot currentSlot)
    {
        if (Kind == ObservationSelectionKind.None)
            return false;

        if (currentSlot == null ||
            !ReferenceEquals(Slot, currentSlot))
        {
            Clear();
            return false;
        }

        if (Kind == ObservationSelectionKind.Ground)
            return true;

        var parts = ItemVisualFactory.GetParts(currentSlot.Value);

        PartIndex = -1;

        for (int i = 0; i < parts.Count; i++)
        {
            if (ReferenceEquals(parts[i], Part))
            {
                PartIndex = i;
                break;
            }
        }

        if (PartIndex < 0)
        {
            Clear();
            return false;
        }

        if (Kind == ObservationSelectionKind.EquipmentPart)
            return true;

        if (Kind == ObservationSelectionKind.FrameSlot)
        {
            if (Part.Frames == null ||
                (Part.Kind != ItemKind.BroodBox &&
                 Part.Kind != ItemKind.Super) ||
                FrameSlotIndex < 0 ||
                FrameSlotIndex >= 10 ||
                FrameSlotIndex >= Part.Frames.Capacity ||
                Part.Frames.GetSlot(FrameSlotIndex) == null ||
                !ReferenceEquals(GetExposedBox(currentSlot), Part))
            {
                Clear();
                return false;
            }

            return true;
        }

        Clear();
        return false;
    }
}

public enum ObservationAction
{
    None,
    Place,
    Lift,
    Combine,
    InsertFrame,
    TakeFrame
}

public static class ObservationActions
{
    public static ObservationAction Available(
        ObservationSelection selection,
        ItemSlot currentSlot,
        ItemSlot hand)
    {
        if (selection == null ||
            !selection.Revalidate(currentSlot) ||
            hand == null ||
            ReferenceEquals(hand, currentSlot))
        {
            return ObservationAction.None;
        }

        if (selection.Kind == ObservationSelectionKind.Ground)
        {
            return currentSlot.IsEmpty &&
                   !hand.IsEmpty &&
                   ApiaryPlacementRules.CanPlace(hand.Value)
                ? ObservationAction.Place
                : ObservationAction.None;
        }

        if (selection.Kind == ObservationSelectionKind.FrameSlot)
        {
            ItemSlot frameSlot =
                selection.GetSelectedFrameSlot();

            if (frameSlot == null ||
                ReferenceEquals(frameSlot, hand))
                return ObservationAction.None;

            // 빈 슬롯에 손의 소비장 넣기
            if (frameSlot.IsEmpty)
            {
                return HiveInteriorRules.CanInsert(hand.Value)
                    ? ObservationAction.InsertFrame
                    : ObservationAction.None;
            }

            // 소비장이 있는 슬롯에서 빈손으로 꺼내기
            return hand.IsEmpty
                ? ObservationAction.TakeFrame
                : ObservationAction.None;
        }

        if (selection.Kind != ObservationSelectionKind.EquipmentPart)
            return ObservationAction.None;

        if (hand.IsEmpty)
            return ObservationAction.Lift;

        var parts = ItemVisualFactory.GetParts(currentSlot.Value);

        return selection.PartIndex == parts.Count - 1 &&
               currentSlot.CanCombineFrom(hand)
            ? ObservationAction.Combine
            : ObservationAction.None;
    }

    public static bool Execute(
        ObservationAction action,
        ObservationSelection selection,
        ItemSlot currentSlot,
        ItemSlot hand)
    {
        if (action == ObservationAction.None ||
            Available(selection, currentSlot, hand) != action)
            return false;

        switch (action)
        {
            case ObservationAction.Place:
                return hand.TryMoveTo(currentSlot);

            case ObservationAction.Combine:
                return currentSlot.TryCombineFrom(hand);

            case ObservationAction.Lift:
                return currentSlot.Value is ItemAssemblyData
                    ? currentSlot.TrySplitTo(selection.PartIndex, hand)
                    : currentSlot.TryMoveTo(hand);

            case ObservationAction.InsertFrame:
            {
                ItemSlot frameSlot =
                    selection.GetSelectedFrameSlot();

                return frameSlot != null &&
                       hand.TryMoveTo(frameSlot);
            }

            case ObservationAction.TakeFrame:
            {
                ItemSlot frameSlot =
                    selection.GetSelectedFrameSlot();

                return frameSlot != null &&
                       frameSlot.TryMoveTo(hand);
            }

            default:
                return false;
        }
    }
}
