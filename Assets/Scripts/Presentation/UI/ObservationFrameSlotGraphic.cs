
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasRenderer))]
public class ObservationFrameSlotGraphic : MaskableGraphic
{
    private Vector2[] polygon;

    [SerializeField, Min(0f)]
    private float outlineWidth = 3f;

    [SerializeField]
    private Color outlineColor = Color.yellow;

    public void SetPolygon(Vector2[] points)
    {
        polygon = points;
        raycastTarget = false;
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();

        if (polygon == null || polygon.Length < 3)
            return;

        Vector2 center = Vector2.zero;

        foreach (Vector2 p in polygon)
            center += p;

        center /= polygon.Length;

        // 내부 반투명 면
        int centerIndex = AddVertex(
            vh, center, color);

        for (int i = 0; i < polygon.Length; i++)
        {
            int next = (i + 1) % polygon.Length;

            int a = AddVertex(vh, polygon[i], color);
            int b = AddVertex(vh, polygon[next], color);

            vh.AddTriangle(centerIndex, a, b);
        }

        // 테두리
        for (int i = 0; i < polygon.Length; i++)
        {
            Vector2 a = polygon[i];
            Vector2 b = polygon[(i + 1) % polygon.Length];

            Vector2 direction = (b - a).normalized;
            Vector2 normal = new Vector2(
                -direction.y,
                direction.x) * outlineWidth * 0.5f;

            int v0 = AddVertex(
                vh, a - normal, outlineColor);

            int v1 = AddVertex(
                vh, a + normal, outlineColor);

            int v2 = AddVertex(
                vh, b + normal, outlineColor);

            int v3 = AddVertex(
                vh, b - normal, outlineColor);

            vh.AddTriangle(v0, v1, v2);
            vh.AddTriangle(v0, v2, v3);
        }
    }

    private static int AddVertex(
        VertexHelper vh,
        Vector2 position,
        Color vertexColor)
    {
        int index = vh.currentVertCount;

        UIVertex vertex = UIVertex.simpleVert;
        vertex.position = position;
        vertex.color = vertexColor;

        vh.AddVert(vertex);

        return index;
    }
}
