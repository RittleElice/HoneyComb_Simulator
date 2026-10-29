using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public static class ObservationInputGuard
{
    private static readonly List<RaycastResult> hits = new List<RaycastResult>();
    public static bool IsOverUI(Vector2 position)
    {
        if (EventSystem.current == null) return false;
        hits.Clear();
        EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = position }, hits);
        foreach (var hit in hits)
            if (hit.module is GraphicRaycaster) return true;
        return false;
    }
}
