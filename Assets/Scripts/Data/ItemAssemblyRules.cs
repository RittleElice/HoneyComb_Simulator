using System.Collections.Generic;

public static class ItemAssemblyRules
{
    public const int MaxBoxes = 5;
    public static bool IsValid(IReadOnlyList<ItemData> parts)
    {
        if (parts == null || parts.Count == 0) return false;
        int boxes = 0, brood = 0, lids = 0;
        var seen = new HashSet<ItemData>();
        for (int i = 0; i < parts.Count; i++)
        {
            ItemData part = parts[i];
            if (part == null || !seen.Add(part)) return false;
            switch (part.Kind)
            {
                case ItemKind.BroodBox:
                    if (++brood > 1 || i != 0) return false;
                    boxes++;
                    break;
                case ItemKind.Super: boxes++; break;
                case ItemKind.Lid:
                    if (++lids > 1 || i != parts.Count - 1) return false;
                    break;
                default: return false;
            }
        }
        return boxes <= MaxBoxes;
    }

    public static bool CanCombine(MovableItem lower, MovableItem upper)
    {
        if (lower == null || upper == null || ReferenceEquals(lower, upper)) return false;
        var parts = new List<ItemData>();
        AppendParts(parts, lower);
        AppendParts(parts, upper);
        return IsValid(parts);
    }

    internal static void AppendParts(List<ItemData> parts, MovableItem value)
    {
        if (value is ItemData item) parts.Add(item);
        else if (value is ItemAssemblyData assembly) parts.AddRange(assembly.Parts);
    }
}
