using System;
using System.Collections.Generic;

public sealed class ItemAssembly : MovableItem
{
    private readonly List<ItemData> parts;
    public IReadOnlyList<ItemData> Parts { get; }
    public override string Name => string.Join(" + ", parts.ConvertAll(part => part.Name));

    public ItemAssembly(IEnumerable<ItemData> items)
    {
        if (items == null) throw new ArgumentNullException(nameof(items));
        parts = new List<ItemData>(items);
        if (!AssemblyRules.IsValid(parts)) throw new ArgumentException("Invalid assembly.", nameof(items));
        foreach (var part in parts)
            if (part.Owner != null) throw new InvalidOperationException("Part already has an owner.");
        foreach (var part in parts) part.Owner = this;
        Parts = parts.AsReadOnly();
    }

    // Only slot transactions can dismantle an owned assembly.
    internal List<ItemData> ReleaseParts()
    {
        var result = new List<ItemData>(parts);
        foreach (var part in parts) part.Owner = null;
        parts.Clear();
        return result;
    }

    internal static MovableItem Pack(List<ItemData> parts)
        => parts.Count == 1 ? (MovableItem)parts[0] : new ItemAssembly(parts);
}
