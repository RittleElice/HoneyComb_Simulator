using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[CreateAssetMenu(menuName = "HoneyComb/UI/Action Button Catalog")]
public sealed class ActionButtonCatalog : ScriptableObject
{
    [Serializable]
    public sealed class Entry
    {
        public ActionButtonId id;
        [Tooltip("동적 버튼의 생성 원본. 씬에 배치된 버튼은 기존 오브젝트를 유지합니다.")]
        public Button prefab;
        public Sprite icon;
        public float iconRotation;
        public Vector2 size = new Vector2(96, 96);
        [Tooltip("Grid 이외의 배치에서 적용하는 Canvas 좌표 오프셋")]
        public Vector2 offset;
        public ActionButtonPlacement placement = ActionButtonPlacement.FollowTarget;
        public Vector2 anchor = new Vector2(.5f, .5f);
        [Tooltip("추적 대상과의 간격. 음수는 기존 Controller의 간격을 사용합니다.")]
        public float followGap = -1;
        [Tooltip("FollowTarget 전용. 기존 배치는 Controller의 원래 기준점을 유지합니다.")]
        public ActionButtonFollowAnchor followAnchor = ActionButtonFollowAnchor.Automatic;
        [Tooltip("들기 버튼에서 뚜껑일 때만 추적 위치를 별도로 사용합니다.")]
        public bool overrideLidFollowAnchor;
        public ActionButtonFollowAnchor lidFollowAnchor = ActionButtonFollowAnchor.Top;
        public bool clampToScreen = true;
        [Min(0)] public float padding = 16;
        public bool showLabel = true;
        public string label;
        public string lidLabel;
        [Tooltip("{level}은 선택한 층 번호로 치환됩니다.")]
        public string stackLabel;
        public string occupiedLabel;
        public bool useLayoutSlot;
        public Vector2 gridSpacing = new Vector2(8, 8);
        [Min(1)] public int gridColumns = 6;

        public ActionButtonFollowAnchor ResolveFollowAnchor(bool isLid)
        {
            bool lift = id == ActionButtonId.Lift || id == ActionButtonId.ObservationLift;
            return lift && isLid && overrideLidFollowAnchor ? lidFollowAnchor : followAnchor;
        }

        public string ResolveLabel(string fallback, bool lid, int level, bool occupied)
        {
            if (id == ActionButtonId.InventorySlot && occupied) return occupiedLabel ?? "";
            string value = lid && !string.IsNullOrEmpty(lidLabel) ? lidLabel :
                level > 0 && !string.IsNullOrEmpty(stackLabel) ? stackLabel : label;
            return (value ?? fallback ?? "").Replace("{level}", level.ToString());
        }
    }
    public List<Entry> entries = new List<Entry>();
    // Called by active views each frame; avoid a capturing predicate allocation.
    public Entry Find(ActionButtonId id)
    {
        for (int i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            if (entry != null && entry.id == id) return entry;
        }
        return null;
    }
}

public enum ActionButtonFollowAnchor
{
    [InspectorName("기존 배치")] Automatic = 0,
    [InspectorName("위쪽 중앙")] Top = 1,
    [InspectorName("아래쪽 중앙")] Bottom = 2,
    [InspectorName("왼쪽 중앙")] Left = 3,
    [InspectorName("오른쪽 중앙")] Right = 4,
    [InspectorName("대상 중앙")] Center = 5
}
