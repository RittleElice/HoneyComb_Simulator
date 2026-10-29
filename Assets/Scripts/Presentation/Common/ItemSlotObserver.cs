using System;
using System.Collections.Generic;

// A reference-only subscription helper. It never owns or modifies item data.
// This is a plain C# class, not an Inspector component.
public sealed class ItemSlotObserver : IDisposable
{
    private readonly HashSet<ItemSlot> slots = new HashSet<ItemSlot>();
    private Action<ItemSlot> callback;
    private bool disposed;

    public int Count => slots.Count;

    public ItemSlotObserver(Action<ItemSlot> onChanged)
    {
        callback = onChanged ?? throw new ArgumentNullException(nameof(onChanged));
    }

    public void Observe(ItemSlot slot)
    {
        if (disposed)
            throw new ObjectDisposedException(nameof(ItemSlotObserver));

        if (slot != null && slots.Add(slot))
            slot.OnChanged += HandleChanged;
    }

    public void Unobserve(ItemSlot slot)
    {
        if (slot != null && slots.Remove(slot))
            slot.OnChanged -= HandleChanged;
    }

    public void Clear()
    {
        foreach (ItemSlot slot in slots)
            slot.OnChanged -= HandleChanged;

        slots.Clear();
    }

    private void HandleChanged(ItemSlot slot)
    {
        if (!disposed && slots.Contains(slot))
            callback?.Invoke(slot);
    }

    public void Dispose()
    {
        if (disposed)
            return;

        Clear();
        callback = null;
        disposed = true;
    }
}
