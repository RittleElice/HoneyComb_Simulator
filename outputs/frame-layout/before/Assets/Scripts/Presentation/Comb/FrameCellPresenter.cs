
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
public class FrameCellPresenter : MonoBehaviour, IItemDataBound
{
    [Header("References")]
    [SerializeField] private RectTransform cellArea;

    [Header("Display")]
    [SerializeField] private bool showCells = true;

    [SerializeField, Range(0.1f, 1f)]
    private float areaFill = 1f;

    [Header("Appearance")]
    [SerializeField] private Color outlineColor =
        new Color(0.45f, 0.25f, 0.1f, 0.85f);

    [SerializeField, Min(0.01f)]
    private float outlineWidth = 0.5f;

    [Header("Performance")]
    [SerializeField, Range(50, 500)]
    private int cellsPerMesh = 400;

    private FrameData frame;
    private ItemData boundItem;
    private FrameSide lastSide;
    private int lastRevision = -1;
    private float displayScale;
    private RectTransform visualRoot;

    private readonly List<FrameCellMesh> meshes =
        new List<FrameCellMesh>();

    private Vector2 lastAreaSize;
    private float lastFill;

    public FrameData Frame => frame;

    public void BindItem(ItemData item)
    {
        boundItem = item;
        frame = item != null && item.Kind == ItemKind.Frame
            ? item.GetContent<FrameData>()
            : null;

        Rebuild();
    }

    private void LateUpdate()
    {
        if (frame == null || cellArea == null)
            return;

        if (lastSide != frame.FacingSide || lastRevision != frame.GetFacingSurface().Revision)
        {
            foreach (var mesh in meshes) mesh.SetSurface(frame.GetFacingSurface());
            lastSide = frame.FacingSide;
            lastRevision = frame.GetFacingSurface().Revision;
        }
        Vector2 size = cellArea.rect.size;

        if (size != lastAreaSize ||
            !Mathf.Approximately(lastFill, areaFill))
        {
            Rebuild();
        }
    }

    public void Rebuild()
    {
        ClearMeshes();

        if (!showCells || frame == null || cellArea == null)
            return;

        Rect area = cellArea.rect;

        if (area.width <= 0f || area.height <= 0f)
            return;

        int columns = frame.Width;
        int rows = frame.Height;

        var geometry = new CombGeometry(columns, rows);
        displayScale = Mathf.Min(area.width / (float)CombGeometry.AreaWidth,
            area.height / (float)CombGeometry.AreaHeight) * areaFill;
        float radius = (float)geometry.Radius * displayScale;
        Vector2 origin = new Vector2(-(float)geometry.Width * displayScale / 2,
            -(float)geometry.Height * displayScale / 2);

        GameObject rootObject = new GameObject(
            "FrameCellVisuals",
            typeof(RectTransform));

        rootObject.transform.SetParent(cellArea, false);

        visualRoot = rootObject.GetComponent<RectTransform>();

        visualRoot.anchorMin = Vector2.zero;
        visualRoot.anchorMax = Vector2.one;
        visualRoot.offsetMin = Vector2.zero;
        visualRoot.offsetMax = Vector2.zero;
        visualRoot.localRotation = Quaternion.identity;
        visualRoot.localScale = Vector3.one;

        int total = columns * rows;
        int chunkSize = Mathf.Clamp(cellsPerMesh, 50, 500);

        for (int start = 0; start < total; start += chunkSize)
        {
            int end = Mathf.Min(start + chunkSize, total);

            GameObject go = new GameObject(
                $"Cells_{start}_{end - 1}",
                typeof(RectTransform),
                typeof(CanvasRenderer));

            go.transform.SetParent(visualRoot, false);

            RectTransform rect =
                go.GetComponent<RectTransform>();

            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.localRotation = Quaternion.identity;
            rect.localScale = Vector3.one;

            FrameCellMesh mesh = go.AddComponent<FrameCellMesh>();

            mesh.Configure(
                columns,
                rows,
                start,
                end,
                radius,
                outlineWidth,
                origin,
                outlineColor);

            mesh.SetSurface(frame.GetFacingSurface());
            meshes.Add(mesh);
        }

        lastAreaSize = area.size;
        lastFill = areaFill;
    }

    // Caller supplies a point local to cellArea; no per-cell raycast objects.
    public bool TryGetCellAtLocalPoint(Vector2 point, out CombCellId id)
    {
        id = default;
        if (!showCells || frame == null || cellArea == null || displayScale <= 0) return false;
        point -= cellArea.rect.center;
        var geometry = new CombGeometry(frame.Width, frame.Height);
        if (!geometry.TryFind(point.x / displayScale + CombGeometry.AreaWidth / 2,
            point.y / displayScale + CombGeometry.AreaHeight / 2, out int c, out int r)) return false;
        id = new CombCellId(boundItem, frame.FacingSide, c, r);
        return true;
    }

    private void ClearMeshes()
    {
        if (visualRoot != null)
        {
            visualRoot.gameObject.SetActive(false);

            if (Application.isPlaying)
                Destroy(visualRoot.gameObject);
            else
                DestroyImmediate(visualRoot.gameObject);
        }

        visualRoot = null;
        meshes.Clear();
    }

    private void OnValidate()
    {
        areaFill = Mathf.Clamp(areaFill, 0.1f, 1f);
        outlineWidth = Mathf.Max(0.01f, outlineWidth);

        if (Application.isPlaying && isActiveAndEnabled)
            Rebuild();
    }

    private void OnDestroy()
    {
        ClearMeshes();
    }
}
