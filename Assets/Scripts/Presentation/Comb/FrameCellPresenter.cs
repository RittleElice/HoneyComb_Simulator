
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
    private RectTransform visualRoot;

    private readonly List<FrameCellMesh> meshes =
        new List<FrameCellMesh>();

    private Vector2 lastAreaSize;
    private float lastFill;

    public FrameData Frame => frame;

    public void BindItem(ItemData item)
    {
        frame = item != null && item.Kind == ItemKind.Frame
            ? item.GetContent<FrameData>()
            : null;

        Rebuild();
    }

    private void LateUpdate()
    {
        if (frame == null || cellArea == null)
            return;

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

        // Pointy-top 정육각형의 전체 배열 크기
        // 홀수 행은 가로로 반 칸 이동
        float widthUnits =
            Mathf.Sqrt(3f) *
            (columns + (rows > 1 ? 0.5f : 0f));

        float heightUnits =
            1.5f * (rows - 1) + 2f;

        float availableWidth = area.width * areaFill;
        float availableHeight = area.height * areaFill;

        float radius = Mathf.Min(
            availableWidth / widthUnits,
            availableHeight / heightUnits);

        if (radius <= 0f)
            return;

        float totalWidth = widthUnits * radius;
        float totalHeight = heightUnits * radius;

        Vector2 origin = new Vector2(
            -totalWidth * 0.5f,
            -totalHeight * 0.5f);

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
        int chunkSize = Mathf.Max(1, cellsPerMesh);

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

            meshes.Add(mesh);
        }

        lastAreaSize = area.size;
        lastFill = areaFill;
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
