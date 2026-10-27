using System;
using System.Collections.Generic;

// Runs both in Unity (-executeMethod ItemAssemblyTests.RunAll) and a plain C# harness.
public static class ItemAssemblyTests
{
    private static int checks;
    private static ItemData Part(ItemKind kind) => new ItemData(1, kind.ToString(), kind);
    private static void Check(bool condition, string message)
    {
        checks++;
        if (!condition) throw new Exception("Assembly test failed: " + message);
    }
    private static ItemSlot Slot(MovableItem value)
    {
        var slot = new ItemSlot();
        Check(slot.TryInsertValue(value), "fixture insert");
        return slot;
    }
    private static bool Valid(params ItemKind[] kinds)
    {
        var parts = new List<ItemData>();
        foreach (var kind in kinds) parts.Add(Part(kind));
        return ItemAssemblyRules.IsValid(parts);
    }

#if UNITY_EDITOR
    [UnityEditor.MenuItem("Tools/HoneyComb/Run Item Assembly Tests")]
#endif
    public static void RunAll()
    {
        checks = 0;
        Rules();
        ExhaustiveRules();
        TransfersAndSplit();
        FailuresAreAtomic();
        LegacyCompatibility();
        string message = "ItemAssemblyData tests passed: " + checks + " checks.";
#if UNITY_EDITOR
        UnityEngine.Debug.Log(message);
#else
        Console.WriteLine(message);
#endif
    }

    private static void Rules()
    {
        Check(!Valid(), "empty invalid");
        Check(Valid(ItemKind.BroodBox), "single brood");
        Check(Valid(ItemKind.Super), "single super");
        Check(Valid(ItemKind.Lid), "single lid");
        Check(Valid(ItemKind.Super, ItemKind.Super), "base optional");
        Check(Valid(ItemKind.BroodBox, ItemKind.Super, ItemKind.Super, ItemKind.Super, ItemKind.Super, ItemKind.Lid), "five boxes plus lid");
        Check(!Valid(ItemKind.BroodBox, ItemKind.Super, ItemKind.Super, ItemKind.Super, ItemKind.Super, ItemKind.Super), "six boxes");
        Check(!Valid(ItemKind.BroodBox, ItemKind.BroodBox), "two brood");
        Check(!Valid(ItemKind.Super, ItemKind.BroodBox), "brood above super");
        Check(!Valid(ItemKind.Lid, ItemKind.Super), "lid blocks stacking");
        Check(!Valid(ItemKind.Lid, ItemKind.Lid), "two lids");
        Check(!Valid(ItemKind.Frame), "frames are contents");
        Check(!Valid(ItemKind.Generic), "generic not stackable");
        var part = Part(ItemKind.Super);
        Check(!ItemAssemblyRules.IsValid(new[] { part, part }), "duplicate part");
        Check(!ItemAssemblyRules.IsValid(new ItemData[] { null }), "null part");
    }

    private static void ExhaustiveRules()
    {
        // Independent oracle: optional B, up to five boxes total, optional terminal L.
        // Enumerate all sequences of length 1..7 over B/S/L.
        for (int length = 1; length <= 7; length++)
        {
            int count = (int)Math.Pow(3, length);
            for (int code = 0; code < count; code++)
            {
                int value = code;
                var kinds = new ItemKind[length];
                var word = "";
                for (int i = 0; i < length; i++)
                {
                    int digit = value % 3; value /= 3;
                    kinds[i] = digit == 0 ? ItemKind.BroodBox : digit == 1 ? ItemKind.Super : ItemKind.Lid;
                    word += "BSL"[digit];
                }
                bool expected = System.Text.RegularExpressions.Regex.IsMatch(word, @"\AB?S*L?\z")
                    && word.Replace("L", "").Length <= 5;
                Check(Valid(kinds) == expected, "rules " + word);
            }
        }
    }

    private static void TransfersAndSplit()
    {
        var brood = Part(ItemKind.BroodBox);
        var super = Part(ItemKind.Super);
        var lid = Part(ItemKind.Lid);
        Check(brood.Frames.Capacity == 10 && super.Frames.Capacity == 10, "ten frame slots each");
        for (int i = 0; i < 10; i++)
        {
            Check(brood.Frames.GetSlot(i).TryInsert(Part(ItemKind.Frame)), "fill brood");
            Check(super.Frames.GetSlot(i).TryInsert(Part(ItemKind.Frame)), "fill super");
        }
        Check(!super.Frames.TryAdd(Part(ItemKind.Frame)), "eleventh frame rejected");
        var originalFrame = super.Frames.GetSlot(7).Item;
        var ground = Slot(brood);
        var upper = Slot(new ItemAssemblyData(new[] { super, lid }));
        Check(ground.TryCombineFrom(upper) && upper.IsEmpty, "combine consumes upper slot");
        var hand = new HandData();
        Check(ground.TryMoveTo(hand.Slot), "whole assembly to hand");
        var held = hand.Value;
        Check(ground.IsEmpty && ((ItemAssemblyData)held).Parts.Count == 3, "one hand slot");
        var bag = new InventoryData(2);
        hand.Swap(bag.GetSlot(0));
        Check(hand.IsEmpty && ReferenceEquals(held, bag.GetSlot(0).Value), "swap preserves whole object");
        Check(bag.GetSlot(0).TrySplitTo(1, hand.Slot), "split middle with upper parts");
        Check(ReferenceEquals(brood, bag.GetSlot(0).Item), "bottom becomes individual");
        var top = (ItemAssemblyData)hand.Value;
        Check(ReferenceEquals(top.Parts[0], super) && ReferenceEquals(top.Parts[1], lid), "upper order retained");
        Check(ReferenceEquals(super.Frames.GetSlot(7).Item, originalFrame), "contents retained after transfer and split");
        Check(hand.Slot.TrySplitTo(1, ground), "lift lid first");
        var extra = Slot(Part(ItemKind.Super));
        Check(hand.Slot.TryCombineFrom(extra), "super may be added after lid lifted");
        Check(hand.Slot.TryCombineFrom(ground), "replace lid");
        Check(hand.Slot.TrySplitTo(0, ground), "index zero moves all");
        Check(hand.IsEmpty && ((ItemAssemblyData)ground.Value).Parts.Count == 3, "whole split count");
        Check(!new ItemSlot().TryInsertValue(held), "consumed assembly cannot be reused");
        Check(!new ItemSlot().TryInsert(originalFrame), "frame cannot occupy two slots");
    }

    private static void FailuresAreAtomic()
    {
        var bottom = Part(ItemKind.BroodBox);
        var lower = Slot(new ItemAssemblyData(new[] { bottom, Part(ItemKind.Lid) }));
        var upper = Slot(Part(ItemKind.Super));
        var originalLower = lower.Value;
        var originalUpper = upper.Value;
        Check(!lower.TryCombineFrom(upper), "closed lid blocks addition");
        Check(ReferenceEquals(lower.Value, originalLower) && ReferenceEquals(upper.Value, originalUpper), "failed join changes nothing");
        Check(!lower.TrySplitTo(1, upper), "occupied split target");
        Check(!lower.TrySplitTo(-1, new ItemSlot()), "negative split");
        Check(!lower.TrySplitTo(2, new ItemSlot()), "past end split");
        Check(!lower.TrySplitTo(1, lower), "self split");
        Check(!lower.TryCombineFrom(lower), "self join");
        Check(!lower.TryMoveTo(upper), "occupied move");
        Check(!lower.TryMoveTo(null), "null move");
        Check(!lower.TrySplitTo(1, bottom.Frames.GetSlot(0)), "lid not frame");
        Check(!bottom.Frames.GetSlot(0).TryInsert(Part(ItemKind.Super)), "box not frame");
        Check(!lower.TrySwap(bottom.Frames.GetSlot(0)), "swap respects frame filter");
        Check(ReferenceEquals(lower.Value, originalLower), "failed operations preserve source");
        var five = Slot(new ItemAssemblyData(new[] { Part(ItemKind.Super), Part(ItemKind.Super), Part(ItemKind.Super), Part(ItemKind.Super), Part(ItemKind.Super) }));
        Check(!five.TryCombineFrom(upper), "sixth rejected transactionally");
        Check(ReferenceEquals(upper.Value, originalUpper), "rejected sixth retained");
        var individual = Part(ItemKind.Super);
        var owner = Slot(individual);
        bool threw = false;
        try { new ItemAssemblyData(new[] { individual }); } catch (InvalidOperationException) { threw = true; }
        Check(threw && ReferenceEquals(owner.Item, individual), "constructor cannot steal slotted part");
        threw = false;
        try { lower.Take(); } catch (InvalidOperationException) { threw = true; }
        Check(threw && ReferenceEquals(lower.Value, originalLower), "legacy take cannot lose assembly");
    }

    private static void LegacyCompatibility()
    {
        var hive = new HiveData(3, 2, 4);
        var item = new ItemData(9, "legacy", hive);
        Check(ReferenceEquals(item.GetContent<HiveData>(), hive), "legacy content");
        Check(hive.Id == 3 && hive.X == 2 && hive.Y == 4 && hive.Slot.IsEmpty, "hive coordinate facade");
        var inventory = new InventoryData(1);
        Check(inventory.TryAdd(item), "legacy add");
        Check(ReferenceEquals(inventory.GetSlot(0).Take(), item), "legacy take");
        Check(inventory.GetSlot(0).IsEmpty, "legacy empty");
        Check(inventory.GetSlot(-1) == null && inventory.GetSlot(1) == null, "slot bounds");
        Check(item.Kind == ItemKind.Generic && item.Frames == null, "legacy is not guessed from name or ID");
        Check(Part(ItemKind.Frame).Frames == null && Part(ItemKind.Lid).Frames == null, "only boxes own frame storage");
    }
}
