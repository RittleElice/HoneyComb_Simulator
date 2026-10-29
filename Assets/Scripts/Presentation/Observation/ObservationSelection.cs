
using System;

public enum ObservationSelectionKind
{
    None,
    Ground,
    EquipmentPart,
    FrameSlot,
    TopAttachment
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

    // 내부 장비 슬롯 인덱스 (0~9)
    public int FrameSlotIndex { get; private set; } = -1;

    // 선택한 상단 부착 장비가 들어 있는 실제 슬롯
    public ItemSlot SelectedTopAttachmentSlot
    {
        get;
        private set;
    }

    public void Clear()
    {
        Kind = ObservationSelectionKind.None;
        Slot = null;
        Part = null;
        PartIndex = -1;
        FrameSlotIndex = -1;
        SelectedTopAttachmentSlot = null;
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
            index >= box.Frames.Capacity)
            return false;

        Slot = slot;
        Part = box;
        FrameSlotIndex = index;
        Kind = ObservationSelectionKind.FrameSlot;

        return Revalidate(slot);
    }

    // 설치된 상단 장비 자체를 선택
    public bool SelectTopAttachment(
        ItemSlot observedSlot,
        ItemData box)
    {
        Clear();

        if (observedSlot == null ||
            box == null ||
            box.TopSlot == null ||
            box.TopSlot.IsEmpty)
            return false;

        if (!ReferenceEquals(
            GetExposedBox(observedSlot), box))
            return false;

        ItemData attachment = box.TopSlot.Item;

        if (attachment == null ||
            attachment.Category != SlotCategory.TopSlot)
            return false;

        Slot = observedSlot;
        Part = attachment;
        SelectedTopAttachmentSlot = box.TopSlot;
        Kind = ObservationSelectionKind.TopAttachment;

        return Revalidate(observedSlot);
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

    public ItemSlot GetSelectedTopAttachmentSlot()
    {
        if (Kind != ObservationSelectionKind.TopAttachment)
            return null;

        if (!Revalidate(Slot))
            return null;

        return SelectedTopAttachmentSlot;
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

        // 상단 장비는 조립체 Parts에 포함되지 않으므로
        // 일반 EquipmentPart와 별도로 검증
        if (Kind == ObservationSelectionKind.TopAttachment)
        {
            ItemData exposedBox = GetExposedBox(currentSlot);

            bool valid =
                exposedBox != null &&
                exposedBox.TopSlot != null &&
                ReferenceEquals(
                    SelectedTopAttachmentSlot,
                    exposedBox.TopSlot) &&
                ReferenceEquals(
                    SelectedTopAttachmentSlot.Value,
                    Part) &&
                Part != null &&
                Part.Category == SlotCategory.TopSlot;

            if (!valid)
            {
                Clear();
                return false;
            }

            PartIndex = -1;
            return true;
        }

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
            // 격왕판 등 상단 장비가 설치되어 있으면
            // 내부 소비장 슬롯에 접근할 수 없다.
            bool topAttachmentBlocksInterior =
                Part.TopSlot != null &&
                !Part.TopSlot.IsEmpty;

            if (Part.Frames == null ||
                (Part.Kind != ItemKind.BroodBox &&
                 Part.Kind != ItemKind.Super) ||
                topAttachmentBlocksInterior ||
                FrameSlotIndex < 0 ||
                FrameSlotIndex >= Part.Frames.Capacity ||
                Part.Frames.GetSlot(FrameSlotIndex) == null ||
                !ReferenceEquals(
                    GetExposedBox(currentSlot), Part))
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
    TakeFrame,
    PlaceTopAttachment,
    TakeTopAttachment
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

        // 설치된 상단 장비 선택 후 빈손으로 들기
        if (selection.Kind ==
            ObservationSelectionKind.TopAttachment)
        {
            ItemSlot attachmentSlot =
                selection.GetSelectedTopAttachmentSlot();

            return hand.IsEmpty &&
                   attachmentSlot != null &&
                   !attachmentSlot.IsEmpty &&
                   !ReferenceEquals(attachmentSlot, hand)
                ? ObservationAction.TakeTopAttachment
                : ObservationAction.None;
        }

        if (selection.Kind == ObservationSelectionKind.FrameSlot)
        {
            ItemSlot frameSlot =
                selection.GetSelectedFrameSlot();

            if (frameSlot == null ||
                ReferenceEquals(frameSlot, hand))
                return ObservationAction.None;

            if (frameSlot.IsEmpty)
            {
                return HiveInteriorRules.CanInsert(hand.Value)
                    ? ObservationAction.InsertFrame
                    : ObservationAction.None;
            }

            return hand.IsEmpty
                ? ObservationAction.TakeFrame
                : ObservationAction.None;
        }

        if (selection.Kind !=
            ObservationSelectionKind.EquipmentPart)
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
                    ? currentSlot.TrySplitTo(
                        selection.PartIndex, hand)
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

            case ObservationAction.TakeTopAttachment:
            {
                ItemSlot attachmentSlot =
                    selection.GetSelectedTopAttachmentSlot();

                return attachmentSlot != null &&
                       attachmentSlot.TryMoveTo(hand);
            }

            default:
                return false;
        }
    }

    // 선택 상태와 무관하게 상단 장비 설치 가능 여부 확인
    public static bool CanPlaceTopAttachment(
        ItemSlot observedSlot,
        ItemSlot hand)
    {
        if (observedSlot == null ||
            hand == null ||
            ReferenceEquals(observedSlot, hand) ||
            hand.IsEmpty)
            return false;

        ItemData box =
            ObservationSelection.GetExposedBox(observedSlot);

        return box != null &&
               box.TopSlot != null &&
               box.TopSlot.IsEmpty &&
               !ReferenceEquals(box.TopSlot, hand) &&
               HiveTopAttachmentRules.CanInsert(hand.Value);
    }

    // 상단 장비 설치 실행
    public static bool PlaceTopAttachment(
        ItemSlot observedSlot,
        ItemSlot hand)
    {
        if (!CanPlaceTopAttachment(observedSlot, hand))
            return false;

        ItemData box =
            ObservationSelection.GetExposedBox(observedSlot);

        return box != null &&
               hand.TryMoveTo(box.TopSlot);
    }
}
