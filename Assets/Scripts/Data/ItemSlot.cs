
using System;
using System.Collections.Generic;

public class ItemSlot
{
    private readonly Predicate<MovableItem> accepts;
    private readonly bool allowAssembly;

    public MovableItem Value { get; private set; }

    public ItemData Item => Value as ItemData;
    public bool IsEmpty => Value == null;

    // 슬롯의 내용물이 변경되었을 때 발생
    public event Action<ItemSlot> OnChanged;

    public ItemSlot() : this(null, true)
    {
    }

    internal ItemSlot(Predicate<MovableItem> accepts)
        : this(accepts, false)
    {
    }

    internal ItemSlot(
        Predicate<MovableItem> accepts,
        bool allowAssembly)
    {
        this.accepts = accepts;
        this.allowAssembly = allowAssembly;
    }

    private bool Accepts(MovableItem value)
    {
        if (value == null)
            return true;

        if (value is ItemAssemblyData && !allowAssembly)
            return false;

        return accepts == null || accepts(value);
    }

    private void NotifyChanged()
    {
        OnChanged?.Invoke(this);
    }

    // 내부 전용: 이벤트를 발생시키지 않는다.
    private void SetWithoutNotify(MovableItem value)
    {
        Value = value;

        if (value != null)
            value.Owner = this;
    }

    // 내부 전용: 이벤트를 발생시키지 않는다.
    private MovableItem TakeWithoutNotify()
    {
        MovableItem value = Value;

        if (value != null)
            value.Owner = null;

        Value = null;
        return value;
    }

    public bool TryInsert(ItemData item)
    {
        return TryInsertValue(item);
    }

    public bool TryInsertValue(MovableItem value)
    {
        if (value == null ||
            value.Owner != null ||
            !IsEmpty ||
            !Accepts(value))
            return false;

        if (value is ItemAssemblyData assembly &&
            assembly.Parts.Count == 0)
            return false;

        SetWithoutNotify(value);

        NotifyChanged();
        return true;
    }

    public MovableItem TakeValue()
    {
        if (IsEmpty)
            return null;

        MovableItem value = TakeWithoutNotify();

        NotifyChanged();
        return value;
    }

    public ItemData Take()
    {
        if (Value is ItemAssemblyData)
        {
            throw new InvalidOperationException(
                "Use TakeValue for an assembly.");
        }

        return (ItemData)TakeValue();
    }

    public void Swap(ItemSlot other)
    {
        TrySwap(other);
    }

    public bool TrySwap(ItemSlot other)
    {
        if (other == null ||
            other == this ||
            !Accepts(other.Value) ||
            !other.Accepts(Value))
            return false;

        MovableItem value = TakeWithoutNotify();
        MovableItem otherValue = other.TakeWithoutNotify();

        SetWithoutNotify(otherValue);
        other.SetWithoutNotify(value);

        NotifyChanged();
        other.NotifyChanged();

        return true;
    }

    public bool TryMoveTo(ItemSlot target)
    {
        if (target == null ||
            target == this ||
            IsEmpty ||
            !target.IsEmpty ||
            !target.Accepts(Value))
            return false;

        MovableItem value = TakeWithoutNotify();

        target.SetWithoutNotify(value);

        NotifyChanged();
        target.NotifyChanged();

        return true;
    }

    public bool CanCombineFrom(ItemSlot upper)
    {
        if (!allowAssembly ||
            upper == null ||
            upper == this ||
            IsEmpty ||
            upper.IsEmpty)
            return false;

        if (!ItemAssemblyRules.CanCombine(Value, upper.Value))
            return false;

        if (accepts != null)
        {
            if (!AllPartsAccepted(Value) ||
                !AllPartsAccepted(upper.Value))
                return false;
        }

        return true;
    }

    private bool AllPartsAccepted(MovableItem value)
    {
        if (value is ItemData item)
            return Accepts(item);

        if (value is ItemAssemblyData assembly)
        {
            foreach (ItemData part in assembly.Parts)
            {
                if (!Accepts(part))
                    return false;
            }

            return true;
        }

        return false;
    }

    public bool TryCombineFrom(ItemSlot upper)
    {
        if (!CanCombineFrom(upper))
            return false;

        List<ItemData> parts =
            Release(TakeWithoutNotify());

        parts.AddRange(
            Release(upper.TakeWithoutNotify()));

        SetWithoutNotify(ItemAssemblyData.Pack(parts));

        NotifyChanged();
        upper.NotifyChanged();

        return true;
    }

    public bool TrySplitTo(
        int firstUpperIndex,
        ItemSlot target)
    {
        if (!(Value is ItemAssemblyData assembly) ||
            target == null ||
            target == this ||
            !target.IsEmpty)
            return false;

        if (firstUpperIndex < 0 ||
            firstUpperIndex >= assembly.Parts.Count)
            return false;

        if (firstUpperIndex == 0)
            return TryMoveTo(target);

        if (!target.allowAssembly)
            return false;

        for (int i = firstUpperIndex;
             i < assembly.Parts.Count;
             i++)
        {
            if (!target.Accepts(assembly.Parts[i]))
                return false;
        }

        List<ItemData> parts =
            Release(TakeWithoutNotify());

        List<ItemData> upper = parts.GetRange(
            firstUpperIndex,
            parts.Count - firstUpperIndex);

        parts.RemoveRange(
            firstUpperIndex,
            parts.Count - firstUpperIndex);

        SetWithoutNotify(ItemAssemblyData.Pack(parts));
        target.SetWithoutNotify(ItemAssemblyData.Pack(upper));

        NotifyChanged();
        target.NotifyChanged();

        return true;
    }

    private static List<ItemData> Release(MovableItem value)
    {
        return value is ItemAssemblyData assembly
            ? assembly.ReleaseParts()
            : new List<ItemData> { (ItemData)value };
    }
}
