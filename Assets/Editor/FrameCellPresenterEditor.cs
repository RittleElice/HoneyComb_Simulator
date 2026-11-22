using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(FrameCellPresenter))]
public sealed class FrameCellPresenterEditor : Editor
{
    bool preview = true;
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
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
}
