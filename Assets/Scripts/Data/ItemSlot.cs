using System;
using System.Collections.Generic;

public class ItemSlot
{
    private readonly Predicate<MovableItem> accepts;
    public MovableItem Value { get; private set; }
    // Legacy single-part view. An assembly is never disguised as its bottom part.
    public ItemData Item => Value as ItemData;
    public bool IsEmpty => Value == null;
    public ItemSlot() { }
    internal ItemSlot(Predicate<MovableItem> accepts) { this.accepts = accepts; }
    private bool Accepts(MovableItem value) => value == null || accepts == null || accepts(value);

    public bool TryInsert(ItemData item) => TryInsertValue(item);
    public bool TryInsertValue(MovableItem value)
    {
        if (value == null || value.Owner != null || !IsEmpty || !Accepts(value)) return false;
        if (value is ItemAssembly assembly && assembly.Parts.Count == 0) return false;
        Set(value);
        return true;
    }

    private void Set(MovableItem value)
    {
        Value = value;
        if (value != null) value.Owner = this;
    }
    public MovableItem TakeValue()
    {
        var value = Value;
        if (value != null) value.Owner = null;
        Value = null;
        return value;
    }
    public ItemData Take()
    {
        if (Value is ItemAssembly) throw new InvalidOperationException("Use TakeValue for an assembly.");
        return (ItemData)TakeValue();
    }
    public void Swap(ItemSlot other) { TrySwap(other); }
    public bool TrySwap(ItemSlot other)
    {
        if (other == null || other == this || !Accepts(other.Value) || !other.Accepts(Value)) return false;
        var value = TakeValue();
        var otherValue = other.TakeValue();
        Set(otherValue);
        other.Set(value);
        return true;
    }
    public bool TryMoveTo(ItemSlot target)
    {
        if (target == null || target == this || IsEmpty || !target.IsEmpty || !target.Accepts(Value)) return false;
        target.Set(TakeValue());
        return true;
    }
    public bool CanCombineFrom(ItemSlot upper)
    {
        // Restricted slots (e.g. frame storage) cannot contain assemblies.
        return accepts == null && upper != null && upper != this && AssemblyRules.CanCombine(Value, upper.Value);
    }
    public bool TryCombineFrom(ItemSlot upper)
    {
        if (!CanCombineFrom(upper)) return false;
        var parts = Release(TakeValue());
        parts.AddRange(Release(upper.TakeValue()));
        Set(ItemAssembly.Pack(parts));
        return true;
    }
    public bool TrySplitTo(int firstUpperIndex, ItemSlot target)
    {
        if (!(Value is ItemAssembly assembly) || target == null || target == this || !target.IsEmpty) return false;
        if (firstUpperIndex < 0 || firstUpperIndex >= assembly.Parts.Count) return false;
        if (firstUpperIndex == 0) return TryMoveTo(target);
        // A split contains box/lid parts only, so frame-only storage is excluded.
        if (target.accepts != null) return false;
        var parts = Release(TakeValue());
        var upper = parts.GetRange(firstUpperIndex, parts.Count - firstUpperIndex);
        parts.RemoveRange(firstUpperIndex, parts.Count - firstUpperIndex);
        Set(ItemAssembly.Pack(parts));
        target.Set(ItemAssembly.Pack(upper));
        return true;
    }
    private static List<ItemData> Release(MovableItem value)
        => value is ItemAssembly assembly ? assembly.ReleaseParts() : new List<ItemData> { (ItemData)value };
}
