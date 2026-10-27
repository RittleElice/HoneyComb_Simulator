using System;

public enum ObservationSelectionKind { None, Ground, EquipmentPart, FrameSlot }

// References the real slot and part; never owns or copies equipment data.
public sealed class ObservationSelection
{
    public ObservationSelectionKind Kind { get; private set; }
    public ItemSlot Slot { get; private set; }
    public ItemData Part { get; private set; }
    public int PartIndex { get; private set; } = -1;
    public int FrameSlotIndex { get; private set; } = -1;

    public void Clear()
    {
        Kind = ObservationSelectionKind.None;
        Slot = null; Part = null; PartIndex = FrameSlotIndex = -1;
    }

    public void SelectGround(ItemSlot slot)
    {
        Clear();
        if (slot == null) return;
        Slot = slot; Kind = ObservationSelectionKind.Ground;
    }

    public bool SelectPart(ItemSlot slot, ItemData part)
    {
        Clear();
        if (slot == null || part == null) return false;
        Slot = slot; Part = part; Kind = ObservationSelectionKind.EquipmentPart;
        return Revalidate(slot);
    }

    public bool Revalidate(ItemSlot currentSlot)
    {
        if (Kind == ObservationSelectionKind.None) return false;
        if (currentSlot == null || !ReferenceEquals(Slot, currentSlot)) { Clear(); return false; }
        if (Kind == ObservationSelectionKind.Ground) return true;
        var parts = ItemVisualFactory.GetParts(currentSlot.Value);
        for (int i = 0; i < parts.Count; i++)
            if (ReferenceEquals(parts[i], Part)) { PartIndex = i; return true; }
        Clear(); return false;
    }
}

public enum ObservationAction { None, Place, Lift, Combine }

public static class ObservationActions
{
    public static ObservationAction Available(ObservationSelection selection, ItemSlot currentSlot, ItemSlot hand)
    {
        if (selection == null || !selection.Revalidate(currentSlot) || hand == null || ReferenceEquals(hand, currentSlot))
            return ObservationAction.None;
        if (selection.Kind == ObservationSelectionKind.Ground)
            return currentSlot.IsEmpty && !hand.IsEmpty && ApiaryPlacementRules.CanPlace(hand.Value)
                ? ObservationAction.Place : ObservationAction.None;
        if (selection.Kind != ObservationSelectionKind.EquipmentPart) return ObservationAction.None;
        if (hand.IsEmpty) return ObservationAction.Lift;
        var parts = ItemVisualFactory.GetParts(currentSlot.Value);
        return selection.PartIndex == parts.Count - 1 && currentSlot.CanCombineFrom(hand)
            ? ObservationAction.Combine : ObservationAction.None;
    }

    public static bool Execute(ObservationAction action, ObservationSelection selection, ItemSlot currentSlot, ItemSlot hand)
    {
        if (action == ObservationAction.None || Available(selection, currentSlot, hand) != action) return false;
        switch (action)
        {
            case ObservationAction.Place: return hand.TryMoveTo(currentSlot);
            case ObservationAction.Combine: return currentSlot.TryCombineFrom(hand);
            case ObservationAction.Lift:
                return currentSlot.Value is ItemAssemblyData
                    ? currentSlot.TrySplitTo(selection.PartIndex, hand) : currentSlot.TryMoveTo(hand);
            default: return false;
        }
    }
}
