
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(CanvasRenderer))]
public class FrameCellMesh : MaskableGraphic
{
    private int columns;
    private int rows;
    private int firstIndex;
    private int lastIndex;

    private float radius;
    private float lineWidth;
    private Vector2 origin;

    public void Configure(
        int columns,
        int rows,
        int firstIndex,
        int lastIndex,
        float radius,
        float lineWidth,
        Vector2 origin,
        Color outlineColor)
    {
        this.columns = columns;
        this.rows = rows;
        this.firstIndex = firstIndex;
        this.lastIndex = lastIndex;
        this.radius = radius;
        this.lineWidth = lineWidth;
        this.origin = origin;

        color = outlineColor;
        raycastTarget = false;

        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();

        if (columns <= 0 || rows <= 0 ||
            radius <= 0f || lineWidth <= 0f)
            return;

        float stepX = Mathf.Sqrt(3f) * radius;
        float stepY = 1.5f * radius;

        int total = columns * rows;
        int end = Mathf.Min(lastIndex, total);

        for (int index = firstIndex; index < end; index++)
        {
            int x = index % columns;
            int y = index / columns;

            float shift = (y & 1) == 1
                ? stepX * 0.5f
                : 0f;

            Vector2 center = origin + new Vector2(
                radius * Mathf.Sqrt(3f) * 0.5f
                    + x * stepX + shift,
                radius + y * stepY);

            DrawHexagon(vh, center);
        }
    }

    private void DrawHexagon(VertexHelper vh, Vector2 center)
    {
        for (int i = 0; i < 6; i++)
        {
            float a0 = Mathf.Deg2Rad * (60f * i + 30f);
            float a1 = Mathf.Deg2Rad * (60f * (i + 1) + 30f);

            Vector2 p0 = center + new Vector2(
                Mathf.Cos(a0), Mathf.Sin(a0)) * radius;

            Vector2 p1 = center + new Vector2(
                Mathf.Cos(a1), Mathf.Sin(a1)) * radius;

            DrawLine(vh, p0, p1);
        }
    }

    private void DrawLine(VertexHelper vh, Vector2 a, Vector2 b)
    {
        Vector2 direction = (b - a).normalized;

        Vector2 normal = new Vector2(
            -direction.y, direction.x) * (lineWidth * 0.5f);

        int index = vh.currentVertCount;

        AddVertex(vh, a - normal);
        AddVertex(vh, a + normal);
        AddVertex(vh, b + normal);
        AddVertex(vh, b - normal);

        vh.AddTriangle(index, index + 1, index + 2);
        vh.AddTriangle(index, index + 2, index + 3);
    }

    private void AddVertex(VertexHelper vh, Vector2 position)
    {
        UIVertex vertex = UIVertex.simpleVert;
        vertex.position = position;
        vertex.color = color;
        vertex.uv0 = Vector2.zero;

        vh.AddVert(vertex);
    }
}
