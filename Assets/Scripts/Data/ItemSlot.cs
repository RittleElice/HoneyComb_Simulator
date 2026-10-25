
using System;
using System.Collections.Generic;

public class ItemSlot
{
    private readonly Predicate<MovableItem> accepts;
    private readonly bool allowAssembly;

    public MovableItem Value { get; private set; }

    // 조립체를 단일 부품으로 취급하지 않는다.
    public ItemData Item => Value as ItemData;

    public bool IsEmpty => Value == null;

    // 일반 슬롯: 조립 허용
    public ItemSlot() : this(null, true)
    {
    }

    // 제한 슬롯: 기본적으로 조립 불허
    internal ItemSlot(Predicate<MovableItem> accepts)
        : this(accepts, false)
    {
    }

    // 슬롯별 배치 제한과 조립 허용 여부를 별도로 지정
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

        if (value is ItemAssembly && !allowAssembly)
            return false;

        return accepts == null || accepts(value);
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

        if (value is ItemAssembly assembly &&
            assembly.Parts.Count == 0)
            return false;

        Set(value);
        return true;
    }

    private void Set(MovableItem value)
    {
        Value = value;

        if (value != null)
            value.Owner = this;
    }

    public MovableItem TakeValue()
    {
        MovableItem value = Value;

        if (value != null)
            value.Owner = null;

        Value = null;
        return value;
    }

    public ItemData Take()
    {
        if (Value is ItemAssembly)
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

        MovableItem value = TakeValue();
        MovableItem otherValue = other.TakeValue();

        Set(otherValue);
        other.Set(value);

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

        target.Set(TakeValue());
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

        if (!AssemblyRules.CanCombine(Value, upper.Value))
            return false;

        // 결합 결과의 부품이 목적지 배치 규칙을 만족하는지 검사
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

        if (value is ItemAssembly assembly)
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

        List<ItemData> parts = Release(TakeValue());
        parts.AddRange(Release(upper.TakeValue()));

        Set(ItemAssembly.Pack(parts));
        return true;
    }

    public bool TrySplitTo(int firstUpperIndex, ItemSlot target)
    {
        if (!(Value is ItemAssembly assembly) ||
            target == null ||
            target == this ||
            !target.IsEmpty)
            return false;

        if (firstUpperIndex < 0 ||
            firstUpperIndex >= assembly.Parts.Count)
            return false;

        if (firstUpperIndex == 0)
            return TryMoveTo(target);

        // 조립체를 받을 수 없는 슬롯에는 분리 불가
        if (!target.allowAssembly)
            return false;

        // 분리된 상단 부품의 배치 가능 여부 검사
        for (int i = firstUpperIndex;
             i < assembly.Parts.Count;
             i++)
        {
            if (!target.Accepts(assembly.Parts[i]))
                return false;
        }

        List<ItemData> parts = Release(TakeValue());

        List<ItemData> upper = parts.GetRange(
            firstUpperIndex,
            parts.Count - firstUpperIndex);

        parts.RemoveRange(
            firstUpperIndex,
            parts.Count - firstUpperIndex);

        Set(ItemAssembly.Pack(parts));
        target.Set(ItemAssembly.Pack(upper));

        return true;
    }

    private static List<ItemData> Release(MovableItem value)
    {
        return value is ItemAssembly assembly
            ? assembly.ReleaseParts()
            : new List<ItemData> { (ItemData)value };
    }
}
