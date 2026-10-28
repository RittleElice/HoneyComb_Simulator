
using UnityEngine;
using UnityEditor;
using UnityEngine.UI;

[CustomEditor(typeof(ObservationFrameSlotLayout))]
public class ObservationFrameSlotLayoutEditor : Editor
{
    private static readonly string[] CornerNames =
    {
        "Near Left",
        "Near Right",
        "Far Left",
        "Far Right"
    };

    private static readonly string[] PropertyNames =
    {
        "nearLeft",
        "nearRight",
        "farLeft",
        "farRight"
    };

    private SerializedProperty[] corners;
    private SerializedProperty slotCount;

    private ObservationFrameSlotLayout layout;
    private RectTransform rectTransform;

    private readonly Vector3[] points = new Vector3[4];

    private void OnEnable()
    {
        layout = (ObservationFrameSlotLayout)target;
        rectTransform = layout.GetComponent<RectTransform>();

        corners = new SerializedProperty[4];

        for (int i = 0; i < 4; i++)
        {
            corners[i] =
                serializedObject.FindProperty(PropertyNames[i]);
        }

        slotCount =
            serializedObject.FindProperty("slotCount");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.HelpBox(
            "Scene 뷰에서 네 꼭짓점을 드래그하여 " +
            "소비장 슬롯 영역을 설정하세요.\n" +
            "파란색 선은 10개 슬롯의 경계입니다.",
            MessageType.Info);

        for (int i = 0; i < corners.Length; i++)
        {
            EditorGUILayout.PropertyField(
                corners[i],
                new GUIContent(CornerNames[i]));
        }

        EditorGUILayout.PropertyField(
            slotCount,
            new GUIContent("Slot Count"));
        
        
        if (GUILayout.Button("Make Parallelogram (Adjust Near Left)"))
        {
            // 나머지 세 꼭짓점은 유지
            Vector2 nearRight = corners[1].vector2Value;
            Vector2 farLeft = corners[2].vector2Value;
            Vector2 farRight = corners[3].vector2Value;

            // 평행사변형의 네 번째 꼭짓점 계산
            Vector2 nearLeft = nearRight + farLeft - farRight;

            // Near Left만 변경
            corners[0].vector2Value = nearLeft;

            serializedObject.ApplyModifiedProperties();

            EditorUtility.SetDirty(layout);

            SceneView.RepaintAll();
        }


        if (GUILayout.Button("Reset Corners"))
        {
            corners[0].vector2Value =
                new Vector2(0f, 0.67f);

            corners[1].vector2Value =
                new Vector2(0.67f, 0.67f);

            corners[2].vector2Value =
                new Vector2(0.33f, 1f);

            corners[3].vector2Value =
                new Vector2(1f, 1f);
        }

        serializedObject.ApplyModifiedProperties();
    }

    private void OnSceneGUI()
    {
        if (rectTransform == null)
            return;

        serializedObject.Update();

        Rect rect = rectTransform.rect;

        for (int i = 0; i < 4; i++)
        {
            Vector2 normalized = corners[i].vector2Value;

            Vector3 local = new Vector3(
                rect.xMin + normalized.x * rect.width,
                rect.yMin + normalized.y * rect.height,
                0f);

            points[i] =
                rectTransform.TransformPoint(local);
        }

        // 상단 평행사변형 테두리
        Handles.color = Color.yellow;

        Handles.DrawAAPolyLine(
            3f,
            points[0],
            points[1],
            points[3],
            points[2],
            points[0]);

        // 소비장 슬롯 경계선
        Handles.color = Color.cyan;

        int count = Mathf.Max(
            1,
            slotCount.intValue);

        for (int i = 1; i < count; i++)
        {
            float t = (float)i / count;

            Vector3 left = Vector3.Lerp(points[0], points[1], t);
            Vector3 right = Vector3.Lerp(points[2], points[3], t);

            Handles.DrawAAPolyLine(
                2f,
                left,
                right);
        }

        // 꼭짓점 이동 핸들
        for (int i = 0; i < 4; i++)
        {
            Handles.color = Color.yellow;

            float size =
                HandleUtility.GetHandleSize(points[i]) * 0.08f;

            EditorGUI.BeginChangeCheck();

            Vector3 newWorldPosition =
                Handles.FreeMoveHandle(
                    points[i],
                    size,
                    Vector3.zero,
                    Handles.DotHandleCap);

            if (EditorGUI.EndChangeCheck())
            {
                Vector3 local =
                    rectTransform.InverseTransformPoint(
                        newWorldPosition);

                Vector2 normalized = new Vector2(
                    Mathf.InverseLerp(
                        rect.xMin,
                        rect.xMax,
                        local.x),
                    Mathf.InverseLerp(
                        rect.yMin,
                        rect.yMax,
                        local.y));

                corners[i].vector2Value = normalized;

                serializedObject.ApplyModifiedProperties();

                EditorUtility.SetDirty(layout);
            }

            Handles.Label(
                points[i] + Vector3.up * size * 2f,
                CornerNames[i]);
        }
    }
}
