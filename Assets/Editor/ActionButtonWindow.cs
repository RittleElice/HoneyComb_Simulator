using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

public sealed class ActionButtonWindow : EditorWindow
{
    private ActionButtonCatalog catalog;
    private SerializedObject serialized;
    private Vector2 scroll;
    private string filter = "";
    private int selected;
    private string report;

    [MenuItem("Tools/HoneyComb/Button Manager")]
    public static void Open() => GetWindow<ActionButtonWindow>("버튼 관리");
    private void OnEnable() => Load();
    private void Load()
    {
        catalog = AssetDatabase.LoadAssetAtPath<ActionButtonCatalog>(ActionButtonSetup.CatalogPath);
        serialized = catalog != null ? new SerializedObject(catalog) : null;
    }
    private void OnGUI()
    {
        if (catalog == null || serialized == null) Load();
        EditorGUILayout.LabelField("모든 UI 버튼", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("설정은 하나의 Catalog에 저장됩니다. 클릭 행동과 표시 조건은 각 Controller가 유지합니다. 프리팹 변경은 다음 생성부터 적용됩니다.", MessageType.Info);
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Catalog 선택")) Selection.activeObject = catalog;
            if (GUILayout.Button("연결 검사")) report = ActionButtonAudit.Run().Text;
            if (GUILayout.Button("누락 항목 설치")) { ActionButtonSetup.Install(); Load(); }
            if (GUILayout.Button("저장") && catalog != null) AssetDatabase.SaveAssetIfDirty(catalog);
        }
        if (catalog == null) return;
        filter = EditorGUILayout.TextField("씬 / ID / 문구 검색", filter);
        scroll = EditorGUILayout.BeginScrollView(scroll);
        serialized.Update();
        var entries = serialized.FindProperty("entries");
        for (int i = 0; i < catalog.entries.Count; i++)
        {
            var e = catalog.entries[i];
            if (e == null) continue;
            var usage = ActionButtonAudit.Usages.FirstOrDefault(u => u.Id == e.id);
            string title = (usage?.Name ?? e.id.ToString()) + "  —  " + (usage?.Scene ?? "미등록") + "  [" + e.id + "]";
            if (!string.IsNullOrEmpty(filter) && (title + e.label).IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0) continue;
            bool expanded = EditorGUILayout.Foldout(selected == i, title, true);
            if (expanded) selected = i;
            else if (selected == i) selected = -1;
            if (selected != i) continue;
            EditorGUI.indentLevel++;
            if (usage != null)
            {
                EditorGUILayout.LabelField("생성 / 연결", usage.Owner);
                EditorGUILayout.LabelField("클릭 행동", usage.Action);
                EditorGUILayout.LabelField("추적 기준점", usage.Target);
                if (GUILayout.Button("담당 코드 열기")) AssetDatabase.OpenAsset(AssetDatabase.LoadAssetAtPath<MonoScript>(usage.Source));
                if (GUILayout.Button("씬 에셋 선택")) Selection.activeObject = AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/Scenes/" + usage.Scene + ".unity");
            }
            var entry = entries.GetArrayElementAtIndex(i);
            // IDs are stable serialized keys; edit presentation without accidentally changing identity.
            EditorGUILayout.PropertyField(entry.FindPropertyRelative("prefab"));
            if (e.id == ActionButtonId.Back || e.id == ActionButtonId.NewGame)
                EditorGUILayout.HelpBox("씬 직접 배치 버튼입니다. 기존 오브젝트와 클릭 이벤트를 유지하므로 Prefab은 생성에 사용되지 않습니다.", MessageType.None);
            foreach (string field in new[] { "icon", "iconRotation", "showLabel", "label", "size", "placement" })
                EditorGUILayout.PropertyField(entry.FindPropertyRelative(field));
            if ((ActionButtonPlacement)entry.FindPropertyRelative("placement").enumValueIndex == ActionButtonPlacement.FollowTarget)
            {
                EditorGUILayout.PropertyField(entry.FindPropertyRelative("followAnchor"), new GUIContent("Follow Anchor / 추적 위치"));
                EditorGUILayout.HelpBox("기존 배치는 원래 위치를 유지합니다. 방향을 선택하면 대상의 화면 경계를 기준으로 배치하며 Follow Gap과 Offset을 추가 적용합니다.", MessageType.None);
            }
            if (e.id == ActionButtonId.Lift || e.id == ActionButtonId.ObservationLift)
            {
                if ((ActionButtonPlacement)entry.FindPropertyRelative("placement").enumValueIndex == ActionButtonPlacement.FollowTarget)
                {
                    EditorGUILayout.PropertyField(entry.FindPropertyRelative("overrideLidFollowAnchor"), new GUIContent("뚜껑 추적 위치 별도 지정"));
                    if (entry.FindPropertyRelative("overrideLidFollowAnchor").boolValue)
                        EditorGUILayout.PropertyField(entry.FindPropertyRelative("lidFollowAnchor"), new GUIContent("뚜껑 추적 위치"));
                }
                EditorGUILayout.PropertyField(entry.FindPropertyRelative("lidLabel"));
                EditorGUILayout.PropertyField(entry.FindPropertyRelative("stackLabel"));
            }
            if (e.id == ActionButtonId.InventorySlot) EditorGUILayout.PropertyField(entry.FindPropertyRelative("occupiedLabel"));
            if ((ActionButtonPlacement)entry.FindPropertyRelative("placement").enumValueIndex == ActionButtonPlacement.Grid)
            {
                EditorGUILayout.PropertyField(entry.FindPropertyRelative("gridSpacing"));
                EditorGUILayout.PropertyField(entry.FindPropertyRelative("gridColumns"));
                EditorGUILayout.HelpBox("격자 모드에서는 GridLayoutGroup이 셀 위치를 관리합니다. 개별 오프셋과 화면 경계 보정은 적용하지 않습니다.", MessageType.None);
            }
            else
                foreach (string field in new[] { "anchor", "offset", "followGap", "useLayoutSlot", "clampToScreen", "padding" })
                    EditorGUILayout.PropertyField(entry.FindPropertyRelative(field));
            EditorGUI.indentLevel--;
        }
        if (serialized.ApplyModifiedProperties()) Repaint();
        EditorGUILayout.Space();
        EditorGUILayout.HelpBox("손 올리기 / 뒤집기 / 내리기: SharedUI의 HandGestureController가 처리하는 제스처입니다. HandSlot의 사용하지 않는 Button은 정리되었습니다.", MessageType.None);
        if (!string.IsNullOrEmpty(report)) EditorGUILayout.TextArea(report, GUILayout.MinHeight(160));
        EditorGUILayout.EndScrollView();
    }
}
