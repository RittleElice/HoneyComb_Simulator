using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// One batched graphic per range. Shared junctions have exactly one owner, including
// junctions crossing a range boundary. The source art uses a hex radius of 40 px.
[DisallowMultipleComponent]
[RequireComponent(typeof(CanvasRenderer))]
public class FrameCellMesh : MaskableGraphic
{
    public sealed class Junction
    {
        public Vector2 Position;
        public readonly List<int> Cells = new List<int>(3);
        public readonly List<int> Corners = new List<int>(3);
        public int Owner => Cells[0];
    }

    public static List<Junction> BuildJunctions(int columns, int rows, float radius, Vector2 origin)
    {
        var result = new List<Junction>();
        var lookup = new Dictionary<Vector2Int, Junction>();
        int[] dx = { 1, 0, -1, -1, 0, 1 };
        int[] dy = { 1, 2, 1, -1, -2, -1 };
        for (int row = 0; row < rows; row++)
        for (int col = 0; col < columns; col++)
        for (int corner = 0; corner < 6; corner++)
        {
            var key = new Vector2Int(2 * col + (row & 1) + 1 + dx[corner], 3 * row + 2 + dy[corner]);
            if (!lookup.TryGetValue(key, out var node))
            {
                node = new Junction { Position = origin + new Vector2(key.x * Mathf.Sqrt(3f) * radius / 2, key.y * radius / 2) };
                lookup.Add(key, node); result.Add(node);
            }
            node.Cells.Add(row * columns + col); node.Corners.Add(corner);
        }
        return result;
    }

    private struct State
    {
        public byte Construction;
        public bool Complete;
        public float Nectar;
        public bool Same(State other) => Construction == other.Construction && Complete == other.Complete && Nectar == other.Nectar;
    }
    private FrameSurface surface;
    private int columns, firstIndex, lastIndex;
    private float radius, alphaMin, alphaMax = 1f, nectarMinimum = .3f;
    private Vector2 origin;
    private Texture2D atlas;
    private readonly List<Junction> nodes = new List<Junction>();
    private readonly List<int> dependencies = new List<int>();
    private State[] states;
    public int RefreshCount { get; private set; }
    public override Texture mainTexture => atlas != null ? atlas : Texture2D.whiteTexture;

    // Retains the existing entry point for callers and editor geometry tests.
    public void Configure(int columns, int rows, int firstIndex, int lastIndex,
        float radius, float lineWidth, Vector2 origin, Color outlineColor)
    {
        ConfigureArt(columns, firstIndex, lastIndex, radius, origin,
            BuildJunctions(columns, rows, radius, origin), 0, 1, .3f);
    }

    public void ConfigureArt(int columns, int firstIndex, int lastIndex, float radius,
        Vector2 origin, List<Junction> topology, float minimumAlpha, float maximumAlpha, float minimumNectar)
    {
        this.columns = columns; this.firstIndex = firstIndex; this.lastIndex = lastIndex;
        this.radius = radius; this.origin = origin;
        alphaMin = Mathf.Clamp01(minimumAlpha); alphaMax = Mathf.Clamp(maximumAlpha, alphaMin, 1);
        nectarMinimum = Mathf.Clamp01(minimumNectar);
        atlas = Resources.Load<Texture2D>("CellObservationAtlas");
        raycastTarget = false; color = Color.white;
        nodes.Clear(); dependencies.Clear();
        var needed = new HashSet<int>();
        for (int i = firstIndex; i < lastIndex; i++) needed.Add(i);
        foreach (var node in topology)
        {
            if (node.Owner < firstIndex || node.Owner >= lastIndex) continue;
            nodes.Add(node);
            foreach (int index in node.Cells) needed.Add(index);
        }
        dependencies.AddRange(needed); dependencies.Sort();
        states = new State[dependencies.Count];
        surface = null;
        SetMaterialDirty(); SetVerticesDirty();
    }

    private State Read(int index)
    {
        var cell = surface.GetCell(index % columns, index / columns);
        bool nectar = cell.Content == CellContentKind.Nectar || cell.Content == CellContentKind.Honey;
        float fill = nectar ? (float)surface.GetCapacityFillRatio(index % columns, index / columns) : 0;
        // A nonempty quantitative nectar cell may consist of sugar alone or water alone.
        bool present = nectar && (surface.HasQuantifiedContent(index % columns, index / columns)
            ? surface.GetTotalSugarMassMg(index % columns, index / columns) > 0 || surface.GetWaterMassMg(index % columns, index / columns) > 0
            : cell.FillRatio > 0);
        return new State { Construction = cell.Construction, Complete = cell.IsComplete,
            Nectar = present ? Mathf.Lerp(nectarMinimum, 1, Mathf.Clamp01(fill)) : 0 };
    }

    public void SetSurface(FrameSurface value)
    {
        bool changed = surface != value;
        surface = value;
        if (states == null) return;
        for (int i = 0; i < dependencies.Count; i++)
        {
            var next = surface == null ? default : Read(dependencies[i]);
            if (!states[i].Same(next)) changed = true;
            states[i] = next;
        }
        if (changed) { RefreshCount++; SetVerticesDirty(); }
    }

    private State Cached(int index) => states[dependencies.BinarySearch(index)];
    public static float WallAlpha(byte maximumConstruction, float minimum, float maximum)
        => Mathf.Lerp(minimum, maximum, maximumConstruction / 100f);

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        if (surface == null || atlas == null || radius <= 0) return;
        float scale = radius / 40f;
        for (int index = firstIndex; index < lastIndex; index++)
        {
            int x = index % columns, y = index / columns;
            var center = origin + new Vector2(Mathf.Sqrt(3f) * radius * (x + .5f + (y & 1) * .5f), radius + y * 1.5f * radius);
            var cell = Cached(index);
            if (cell.Complete) Quad(vh, center, new Rect(4, 48, 60, 69), new Vector2(30, 34.5f), scale, 0, 1);
            if (cell.Nectar > 0) Quad(vh, center, new Rect(76, 48, 57, 63), new Vector2(28.5f, 31.5f), scale * cell.Nectar, 0, 1);
        }
        foreach (var node in nodes)
        {
            int active = 0, corner = 0; byte maximum = 0;
            for (int i = 0; i < node.Cells.Count; i++)
            {
                byte construction = Cached(node.Cells[i]).Construction;
                if (construction > 0) { active++; corner = node.Corners[i]; }
                if (construction > maximum) maximum = construction;
            }
            if (active == 0) continue;
            float angle = 60 * corner - 60;
            if (active == 1)
                Quad(vh, node.Position, new Rect(4, 4, 40, 18), new Vector2(20, 4), scale, angle, WallAlpha(maximum, alphaMin, alphaMax));
            else
                Quad(vh, node.Position, new Rect(52, 4, 40, 34), new Vector2(20, 20), scale, angle, WallAlpha(maximum, alphaMin, alphaMax));
        }
    }

    // Pixels/pivots use top-left origin, while Unity UI uses bottom-left origin.
    private void Quad(VertexHelper vh, Vector2 anchor, Rect pixels, Vector2 pivot, float scale, float angle, float alpha)
    {
        int start = vh.currentVertCount;
        float radians = angle * Mathf.Deg2Rad, cos = Mathf.Cos(radians), sin = Mathf.Sin(radians);
        for (int i = 0; i < 4; i++)
        {
            float x = (i == 1 || i == 2) ? pixels.width : 0;
            float y = i >= 2 ? pixels.height : 0;
            Vector2 p = new Vector2(x - pivot.x, pivot.y - y) * scale;
            var vertex = UIVertex.simpleVert;
            vertex.position = anchor + new Vector2(p.x * cos - p.y * sin, p.x * sin + p.y * cos);
            vertex.color = new Color(1, 1, 1, alpha);
            vertex.uv0 = new Vector2((pixels.x + x) / atlas.width, 1 - (pixels.y + y) / atlas.height);
            vh.AddVert(vertex);
        }
        vh.AddTriangle(start, start + 1, start + 2); vh.AddTriangle(start, start + 2, start + 3);
    }
}
