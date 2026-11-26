using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(FrameCellPresenter))]
public sealed class FrameCellPresenterEditor : Editor
{
    bool preview = true;
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        DrawGroupedInspector();
        serializedObject.ApplyModifiedProperties();
        serializedObject.Update();
        var area = serializedObject.FindProperty("cellArea").objectReferenceValue as RectTransform;
        if (area != null && BaseSize(area, serializedObject).sqrMagnitude > 0)
        {
            GetCorners(area, serializedObject, out var topLeft, out var bottomRight);
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("배열 모서리 (소방 영역 내부 좌표)", EditorStyles.boldLabel);
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
        preview = EditorGUILayout.Toggle("씬 화면 배열 미리보기", preview);
        if (GUI.changed) SceneView.RepaintAll();
    }

    // Only the editor layout changes; serialized paths and prefab references stay intact.
    void DrawGroupedInspector()
    {
        using(new EditorGUI.DisabledScope(true))
            EditorGUILayout.PropertyField(serializedObject.FindProperty("m_Script"),new GUIContent("스크립트"));
        Group("기본 표시와 배열",
            "cellArea", "소방 표시 영역", "showCells", "소방 상태 표시",
            "areaFill", "영역 채움 비율", "cellOffset", "배열 위치",
            "cellScale", "배열 크기", "flipCellsX", "배열 좌우 반전");
        Group("소방 벽",
            "artwork.wallTwoArm", "두 방향 벽 이미지", "artwork.wallThreeArm", "세 방향 벽 이미지",
            "wallMinimumAlpha", "최소 불투명도", "wallMaximumAlpha", "최대 불투명도");
        Group("완성 소방", "artwork.completeCell", "이미지", "completeCellOpacity", "불투명도");
        Group("화밀",
            "artwork.rapeSeedNectar", "유채 이미지", "artwork.cherryBlossomNectar", "벚꽃 이미지",
            "artwork.blackLocustNectar", "아까시 이미지", "artwork.chestnutNectar", "밤 이미지",
            "artwork.wildflowerNectar", "잡화 이미지", "representativeNectarPercent", "대표 밀원 기준 (%)",
            "nectarMinimumScale", "최소 크기 (최대 대비)", "nectarMaximumScale", "최대 크기 배율",
            "dryNectarAlpha", "수분 0% 불투명도", "wetNectarAlpha", "수분 100% 불투명도");
        Group("화분",
            "artwork.pollen", "이미지", "pollenMinimumScale", "최소 크기 (최대 대비)",
            "pollenMaximumScale", "최대 크기 배율", "artwork.pollenColorA", "기본색 범위 시작",
            "artwork.pollenColorB", "기본색 범위 끝", "artwork.pollenTexture", "질감 텍스처",
            "artwork.pollenTextureScale", "질감 크기 배율", "artwork.pollenTextureOpacity", "질감 불투명도",
            "artwork.pollenTextureBlend", "질감 합성 방식");
        Group("알", "artwork.egg", "이미지", "eggRotationRange", "회전 범위 (도)");
        Group("애벌레", "artwork.larva", "이미지", "larvaMinimumScale", "최소 크기 (최대 대비)",
            "larvaMaximumScale", "최대 크기 배율");
        Group("번데기 봉개", "artwork.cappedPupa", "이미지", "pupaStartColor", "발달 시작 색", "pupaEndColor", "발달 완료 색");
        Group("배열 미리보기와 성능", "outlineColor", "미리보기 선 색", "outlineWidth", "선 두께", "cellsPerMesh", "묶음당 소방 수");
    }

    void Group(string title,params string[] fields)
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField(title,EditorStyles.boldLabel);
        for(int i=0;i<fields.Length;i+=2)Field(fields[i],fields[i+1]);
    }

    void Field(string path,string label)
    {
        var property=serializedObject.FindProperty(path);
        var content=new GUIContent(label,property.tooltip);
        // These fields have legacy Header decorators. Draw their controls directly
        // to avoid duplicate English headings, retaining prefab override and Undo UI.
        bool legacyHeader=path=="cellArea"||path=="showCells"||path=="cellOffset"||path=="outlineColor"||path=="cellsPerMesh";
        if(!legacyHeader){EditorGUILayout.PropertyField(property,content,true);return;}
        var rect=EditorGUILayout.GetControlRect(true,EditorGUI.GetPropertyHeight(property.propertyType,content));
        EditorGUI.BeginProperty(rect,content,property);
        EditorGUI.BeginChangeCheck();
        if(path=="cellArea")
        {
            var value=EditorGUI.ObjectField(rect,content,property.objectReferenceValue,typeof(RectTransform),true);
            if(EditorGUI.EndChangeCheck())property.objectReferenceValue=value;
        }
        else if(path=="showCells")
        {
            bool value=EditorGUI.Toggle(rect,content,property.boolValue);
            if(EditorGUI.EndChangeCheck())property.boolValue=value;
        }
        else if(path=="cellOffset")
        {
            var value=EditorGUI.Vector2Field(rect,content,property.vector2Value);
            if(EditorGUI.EndChangeCheck())property.vector2Value=value;
        }
        else if(path=="outlineColor")
        {
            var value=EditorGUI.ColorField(rect,content,property.colorValue);
            if(EditorGUI.EndChangeCheck())property.colorValue=value;
        }
        else
        {
            int value=EditorGUI.IntSlider(rect,content,property.intValue,50,500);
            if(EditorGUI.EndChangeCheck())property.intValue=value;
        }
        EditorGUI.EndProperty();
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
