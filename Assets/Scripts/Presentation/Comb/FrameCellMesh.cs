
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(CanvasRenderer))]
public class FrameCellMesh : MaskableGraphic
{
    private FrameSurface surface;
    private Color cellColor;
    public void SetSurface(FrameSurface value) { surface = value; SetVerticesDirty(); }
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

            var cell = surface != null ? surface.GetCell(x, y) : default;
            cellColor = cell.Type == CombCellType.Drone ? new Color(0.35f, 0.2f, 0.65f, color.a) : color;
            cellColor.a *= Mathf.Lerp(0.25f, 1f, cell.Construction / 100f);
            if (cell.Content != CellContentKind.Empty) DrawContent(vh, center, cell.Content);
            DrawHexagon(vh, center);
        }
    }

    private void DrawContent(VertexHelper vh, Vector2 center, CellContentKind content)
    {
        Color saved = cellColor;
        switch (content)
        {
            case CellContentKind.Honey: cellColor = new Color(1f, 0.65f, 0.05f, 0.7f); break;
            case CellContentKind.Pollen: cellColor = new Color(0.9f, 0.35f, 0.08f, 0.7f); break;
            case CellContentKind.Nectar: cellColor = new Color(0.4f, 0.8f, 1f, 0.6f); break;
            case CellContentKind.Egg: cellColor = new Color(1f, 1f, 1f, 0.8f); break;
            case CellContentKind.Larva: cellColor = new Color(1f, 0.9f, 0.65f, 0.8f); break;
            default: cellColor = new Color(0.65f, 0.45f, 0.25f, 0.8f); break;
        }
        int start = vh.currentVertCount;
        AddVertex(vh, center);
        for (int i = 0; i < 6; i++)
        {
            float angle = (30 + 60 * i) * Mathf.Deg2Rad;
            AddVertex(vh, center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius * 0.8f);
        }
        for (int i = 0; i < 6; i++) vh.AddTriangle(start, start + 1 + i, start + 1 + (i + 1) % 6);
        cellColor = saved;
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
        vertex.color = cellColor;
        vertex.uv0 = Vector2.zero;

        vh.AddVert(vertex);
    }
}
