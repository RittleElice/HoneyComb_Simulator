using TMPro;
using UnityEngine;
public sealed class GameTimeUI : MonoBehaviour
{
    public TMP_Text label;
    private long lastMinute = -1;
    public void Refresh(GameClock clock)
    {
        if (lastMinute == clock.CurrentGameMinute) return;
        lastMinute = clock.CurrentGameMinute;
        label.text = clock.ToString();
    }
    public void Place(GameTimeUIProfile.Placement placement)
    {
        var rect = (RectTransform)transform;
        rect.anchorMin = rect.anchorMax = placement.anchor;
        rect.pivot = placement.pivot;
        rect.anchoredPosition = placement.position; rect.sizeDelta = placement.size;
    }
}
