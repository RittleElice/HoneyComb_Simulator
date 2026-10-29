using UnityEngine;

// Shared contract for one visual part. Item ownership remains in ItemSlot.
public interface IItemPartView
{
    Transform PartTransform { get; }
    ItemData Part { get; }
    int PartIndex { get; }
    Vector3 BottomWorld { get; }
    Vector3 TopWorld { get; }
    void Bind(ItemData part, int partIndex);
    Bounds GetBoundsIn(Transform space);
}
