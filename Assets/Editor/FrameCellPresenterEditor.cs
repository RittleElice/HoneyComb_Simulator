using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(FrameCellPresenter))]
public sealed class FrameCellPresenterEditor : Editor
{
    bool preview = true;
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        serializedObject.Update();
        var area = serializedObject.FindProperty("cellArea").objectReferenceValue as RectTransform;
        if (area != null && BaseSize(area, serializedObject).sqrMagnitude > 0)
        {
            GetCorners(area, serializedObject, out var topLeft, out var bottomRight);
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("배열 모서리 (Cell Area 로컬 좌표)", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            var nextTopLeft = EditorGUILayout.Vector2Field("좌측 상단", topLeft);
            if (EditorGUI.EndChangeCheck())
            {
                nextTopLeft.x = Mathf.Min(nextTopLeft.x, bottomRight.x - BaseSize(area, serializedObject).x * .01f);
                nextTopLeft.y = Mathf.Max(nextTopLeft.y, bottomRight.y + BaseSize(area, serializedObject).y * .01f);
                ApplyCorners(area, serializedObject, nextTopLeft, bottomRight);
                GetCorners(area, serializedObject, out topLeft, out bottomRight);
            }
            EditorGUI.BeginChangeCheck();
            var nextBottomRight = EditorGUILayout.Vector2Field("우측 하단", bottomRight);
            if (EditorGUI.EndChangeCheck())
            {
                nextBottomRight.x = Mathf.Max(nextBottomRight.x, topLeft.x + BaseSize(area, serializedObject).x * .01f);
                nextBottomRight.y = Mathf.Min(nextBottomRight.y, topLeft.y - BaseSize(area, serializedObject).y * .01f);
                ApplyCorners(area, serializedObject, topLeft, nextBottomRight);
            }
        }
        preview = EditorGUILayout.Toggle("Scene 뷰 배열 미리보기", preview);
        if (GUI.changed) SceneView.RepaintAll();
    }

    // Draw an editor-only guide; do not create cells or save temporary objects in a prefab.
    void OnSceneGUI()
    {
        var presenter = (FrameCellPresenter)target;
        if (!preview || Application.isPlaying || presenter.Frame != null) return;
        serializedObject.Update();
        var area = serializedObject.FindProperty("cellArea").objectReferenceValue as RectTransform;
        if (area == null || !serializedObject.FindProperty("showCells").boolValue) return;
        DrawCornerHandles(area);
        var geometry = new CombGeometry();
        float fit = Mathf.Min(area.rect.width / (float)CombGeometry.AreaWidth,
            area.rect.height / (float)CombGeometry.AreaHeight) * serializedObject.FindProperty("areaFill").floatValue;
        var offset = serializedObject.FindProperty("cellOffset").vector2Value;
        var size = serializedObject.FindProperty("cellScale").vector2Value;
        if (serializedObject.FindProperty("flipCellsX").boolValue) size.x = -size.x;
        var old = Handles.color;
        Handles.color = serializedObject.FindProperty("outlineColor").colorValue;
        try
        {
            for (int row = 0; row < geometry.Rows; row++) for (int col = 0; col < geometry.Columns; col++)
            {
                geometry.Center(col,row,out double x,out double y);
                var vertices = new Vector3[7];
                for (int i = 0; i < 7; i++)
                {
                    float angle = (30 + i * 60) * Mathf.Deg2Rad;
                    var p = new Vector2((float)(x-210+geometry.Radius*Mathf.Cos(angle)),
                        (float)(y-100+geometry.Radius*Mathf.Sin(angle))) * fit;
                    vertices[i] = area.TransformPoint(area.rect.center + offset + Vector2.Scale(p,size));
                }
                Handles.DrawAAPolyLine(vertices);
            }
        }
        finally { Handles.color = old; }
    }

    internal static Vector2 BaseSize(RectTransform area, SerializedObject data)
    {
        var geometry = new CombGeometry();
        float fit = Mathf.Min(area.rect.width / (float)CombGeometry.AreaWidth,
            area.rect.height / (float)CombGeometry.AreaHeight) * data.FindProperty("areaFill").floatValue;
        return new Vector2((float)geometry.Width, (float)geometry.Height) * fit;
    }

    internal static void GetCorners(RectTransform area, SerializedObject data, out Vector2 topLeft, out Vector2 bottomRight)
    {
        var center = area.rect.center + data.FindProperty("cellOffset").vector2Value;
        var half = Vector2.Scale(BaseSize(area,data),data.FindProperty("cellScale").vector2Value) * .5f;
        topLeft = center + new Vector2(-half.x,half.y);
        bottomRight = center + new Vector2(half.x,-half.y);
    }

    internal static void ApplyCorners(RectTransform area, SerializedObject data, Vector2 topLeft, Vector2 bottomRight)
    {
        var basis = BaseSize(area,data);
        if (basis.x <= 0 || basis.y <= 0 || bottomRight.x <= topLeft.x || topLeft.y <= bottomRight.y) return;
        Undo.SetCurrentGroupName("벌집 배열 모서리 조정");
        data.FindProperty("cellOffset").vector2Value = (topLeft + bottomRight) * .5f - area.rect.center;
        data.FindProperty("cellScale").vector2Value = new Vector2((bottomRight.x-topLeft.x)/basis.x,(topLeft.y-bottomRight.y)/basis.y);
        data.ApplyModifiedProperties();
        SceneView.RepaintAll();
    }

    void DrawCornerHandles(RectTransform area)
    {
        if (BaseSize(area,serializedObject).sqrMagnitude <= 0) return;
        GetCorners(area,serializedObject,out var topLeft,out var bottomRight);
        var minimum = BaseSize(area,serializedObject) * .01f;
        using (new Handles.DrawingScope(new Color(.25f,.85f,1f),area.localToWorldMatrix))
        {
            Handles.DrawAAPolyLine((Vector3)topLeft,new Vector3(bottomRight.x,topLeft.y),
                (Vector3)bottomRight,new Vector3(topLeft.x,bottomRight.y),(Vector3)topLeft);
            EditorGUI.BeginChangeCheck();
            Vector2 a = Handles.Slider2D((Vector3)topLeft,Vector3.forward,Vector3.right,Vector3.up,
                HandleUtility.GetHandleSize(topLeft)*.07f,Handles.DotHandleCap,Vector2.zero);
            if (EditorGUI.EndChangeCheck())
            {
                a.x=Mathf.Min(a.x,bottomRight.x-minimum.x);a.y=Mathf.Max(a.y,bottomRight.y+minimum.y);
                ApplyCorners(area,serializedObject,a,bottomRight);topLeft=a;
            }
            EditorGUI.BeginChangeCheck();
            Vector2 b = Handles.Slider2D((Vector3)bottomRight,Vector3.forward,Vector3.right,Vector3.up,
                HandleUtility.GetHandleSize(bottomRight)*.07f,Handles.DotHandleCap,Vector2.zero);
            if (EditorGUI.EndChangeCheck())
            {
                b.x=Mathf.Max(b.x,topLeft.x+minimum.x);b.y=Mathf.Min(b.y,topLeft.y-minimum.y);
                ApplyCorners(area,serializedObject,topLeft,b);bottomRight=b;
            }
            Handles.Label(topLeft,"좌측 상단");Handles.Label(bottomRight,"우측 하단");
        }
    }

}
