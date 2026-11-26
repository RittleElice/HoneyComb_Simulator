
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

    [Header("벌집 배열")]
    [Tooltip("소방 배열의 위치 보정. Cell Area 로컬 좌표 기준입니다.")]
    [SerializeField] private Vector2 cellOffset = Vector2.zero;
    [Tooltip("소방 배열의 가로·세로 배율. 1, 1이면 기존 크기입니다.")]
    [SerializeField] private Vector2 cellScale = Vector2.one;
    [Tooltip("소방 배열만 좌우 반전합니다. 뒷면 이미지 반전과 별도로 적용됩니다.")]
    [SerializeField] private bool flipCellsX;

    [Header("Appearance")]
    [SerializeField] private Color outlineColor =
        new Color(0.45f, 0.25f, 0.1f, 0.85f);

    [SerializeField, Min(0.01f)]
    private float outlineWidth = 0.5f;

    [Header("소방 상태 이미지")]
    [SerializeField] private FrameCellArtwork artwork = new FrameCellArtwork();
    [Tooltip("전체 당분량 중 이 비율을 초과한 밀원을 표시합니다. 같거나 대표 밀원이 없으면 잡화로 표시합니다. 물은 제외하고 잡화 4종은 합산합니다.")]
    [SerializeField, Range(50f, 100f)] private float representativeNectarPercent = 50f;
    [Tooltip("수분 비율 0%일 때 화밀 이미지의 불투명도")]
    [SerializeField, Range(0f, 1f)] private float dryNectarAlpha = 1f;
    [Tooltip("수분 비율 100%일 때 화밀 이미지의 불투명도")]
    [SerializeField, Range(0f, 1f)] private float wetNectarAlpha = .3f;
    [Tooltip("건설 중인 소방벽의 최소 투명도. 건설도 0인 소방만 접한 곳은 숨깁니다.")]
    [SerializeField, Range(0f, 1f)] private float wallMinimumAlpha = 0f;
    [Tooltip("건설도 100일 때 소방벽의 투명도. 공유 유닛은 인접 소방의 최대 건설도를 사용합니다.")]
    [SerializeField, Range(0f, 1f)] private float wallMaximumAlpha = 1f;
    [Tooltip("화밀이 조금이라도 있을 때 표시할 최소 가로·세로 크기. 최대 크기에 대한 비율입니다.")]
    [SerializeField, Range(0.01f, 1f)] private float nectarMinimumScale = 0.3f;
    [Tooltip("화분이 조금이라도 있을 때 표시할 최소 크기. 저장량에 따라 최대 크기까지 증가합니다.")]
    [SerializeField, Range(0.01f, 1f)] private float pollenMinimumScale = 0.3f;

    [Tooltip("화밀이 가득 찼을 때 이미지 크기 배율. 1은 기존 최대 크기입니다. 최소 크기는 이 값에 대한 비율입니다.")]
    [SerializeField, Min(.01f)] private float nectarMaximumScale = 1f;
    [Tooltip("화분이 가득 찼을 때 이미지 크기 배율. 1은 기존 최대 크기입니다. 최소 크기는 이 값에 대한 비율입니다.")]
    [SerializeField, Min(.01f)] private float pollenMaximumScale = 1f;

    [Tooltip("부화 직후 애벌레의 크기. 최대 크기에 대한 비율입니다.")]
    [SerializeField, Range(.01f,1f)] private float larvaMinimumScale = .3f;
    [Tooltip("번데기 전환 직전 애벌레 크기 배율. 1은 기존 이미지 크기입니다.")]
    [SerializeField, Min(.01f)] private float larvaMaximumScale = 1f;
    [Tooltip("번데기 단계 시작 시 봉개 이미지에 곱할 색입니다.")]
    [SerializeField, ColorUsage(false)] private Color pupaStartColor = Color.white;
    [Tooltip("우화 직전 봉개 이미지에 곱할 색입니다.")]
    [SerializeField, ColorUsage(false)] private Color pupaEndColor = new Color(179f/255,138f/255,92f/255,1);

    [Tooltip("완성 소방 바탕 이미지의 불투명도. 0은 숨김, 1은 원본입니다. 벽과 내용물에는 영향을 주지 않습니다.")]
    [SerializeField, Range(0f,1f)] private float completeCellOpacity = 1f;
    [Tooltip("알의 고정 랜덤 회전 범위(도). X는 최소, Y는 최대이며 같은 값이면 고정 각도입니다.")]
    [SerializeField] private Vector2 eggRotationRange = new Vector2(0f,360f);

    [Header("Performance")]
    [SerializeField, Range(50, 500)]
    private int cellsPerMesh = 400;

    private FrameData frame;
    private ItemData boundItem;
    private FrameSide lastSide;
    private int lastRevision = -1;
    private int lastBalanceRevision = -1;
    private BeeCycleRuntime broodRuntime;
    private long lastBroodMinute = -1;
    public void BindBroodRuntime(BeeCycleRuntime runtime)
    {
        broodRuntime = runtime;
        lastBroodMinute = -1;
        foreach (var mesh in meshes) mesh.SetSurface(frame?.GetFacingSurface(), broodRuntime);
    }
    private float displayScale;
    private RectTransform visualRoot;

    private readonly List<FrameCellMesh> meshes =
        new List<FrameCellMesh>();

    private Vector2 lastAreaSize;
    private float lastFill;
    private Vector2 lastOffset, lastCellScale;
    private bool lastFlipCellsX;

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

        var activeRuntime = GameBootstrap.Instance?.GameWorld?.BeeCycle ?? broodRuntime;
        bool runtimeChanged = activeRuntime != broodRuntime;
        broodRuntime = activeRuntime;
        long minute = broodRuntime?.LastMinute ?? -1;
        if (runtimeChanged || lastBroodMinute != minute || lastSide != frame.FacingSide || lastRevision != frame.GetFacingSurface().Revision || lastBalanceRevision != CombBalance.Revision)
        {
            ApplyFacingSide();
            foreach (var mesh in meshes) mesh.SetSurface(frame.GetFacingSurface(), broodRuntime);
            lastSide = frame.FacingSide;
            lastRevision = frame.GetFacingSurface().Revision;
            lastBalanceRevision = CombBalance.Revision;
            lastBroodMinute = minute;
        }
        Vector2 size = cellArea.rect.size;

        if (size != lastAreaSize ||
            !Mathf.Approximately(lastFill, areaFill) || lastOffset != cellOffset ||
            lastCellScale != cellScale || lastFlipCellsX != flipCellsX)
        {
            Rebuild();
        }
    }

    public void Rebuild()
    {
        broodRuntime = GameBootstrap.Instance?.GameWorld?.BeeCycle ?? broodRuntime;
        ClearMeshes();
        ApplyFacingSide();

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

        visualRoot.anchorMin = new Vector2(0.5f, 0.5f);
        visualRoot.anchorMax = new Vector2(0.5f, 0.5f);
        visualRoot.pivot = new Vector2(0.5f, 0.5f);
        visualRoot.sizeDelta = area.size;
        visualRoot.anchoredPosition = cellOffset;
        visualRoot.localRotation = Quaternion.identity;
        visualRoot.localScale = new Vector3(cellScale.x * (flipCellsX ? -1f : 1f), cellScale.y, 1f);

        int total = columns * rows;
        int chunkSize = Mathf.Clamp(cellsPerMesh, 50, 500);
        var topology = FrameCellMesh.BuildJunctions(columns, rows, radius, origin);

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

            mesh.ConfigureArt(columns, start, end, radius, origin, topology,
                wallMinimumAlpha, wallMaximumAlpha, nectarMinimumScale, artwork, representativeNectarPercent, dryNectarAlpha, wetNectarAlpha, pollenMinimumScale, nectarMaximumScale, pollenMaximumScale, larvaMinimumScale, larvaMaximumScale, pupaStartColor, pupaEndColor, completeCellOpacity, eggRotationRange.x, eggRotationRange.y);

            mesh.SetSurface(frame.GetFacingSurface(), broodRuntime);
            meshes.Add(mesh);
        }

        lastAreaSize = area.size;
        lastFill = areaFill;
        lastOffset = cellOffset; lastCellScale = cellScale; lastFlipCellsX = flipCellsX;
        lastSide = frame.FacingSide; lastRevision = frame.GetFacingSurface().Revision;
        lastBalanceRevision = CombBalance.Revision;
        lastBroodMinute = broodRuntime?.LastMinute ?? -1;
    }

    private void ApplyFacingSide()
    {
        if (frame == null) return;
        var scale = transform.localScale;
        scale.x = Mathf.Abs(scale.x) * (frame.FacingSide == FrameSide.Back ? -1f : 1f);
        transform.localScale = scale;
    }

    // Caller supplies a point local to cellArea; no per-cell raycast objects.
    public bool TryGetCellAtLocalPoint(Vector2 point, out CombCellId id)
    {
        id = default;
        if (!showCells || frame == null || cellArea == null || visualRoot == null || displayScale <= 0 || cellScale.x <= 0 || cellScale.y <= 0) return false;
        point = visualRoot.InverseTransformPoint(cellArea.TransformPoint(point));
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
        completeCellOpacity = Mathf.Clamp01(completeCellOpacity);
        if(eggRotationRange.x>eggRotationRange.y)eggRotationRange=new Vector2(eggRotationRange.y,eggRotationRange.x);
        areaFill = Mathf.Clamp(areaFill, 0.1f, 1f);
        cellScale.x = Mathf.Max(0.01f, cellScale.x);
        cellScale.y = Mathf.Max(0.01f, cellScale.y);
        outlineWidth = Mathf.Max(0.01f, outlineWidth);
        wallMinimumAlpha = Mathf.Clamp01(wallMinimumAlpha);
        wallMaximumAlpha = Mathf.Clamp(wallMaximumAlpha, wallMinimumAlpha, 1f);
        nectarMinimumScale = Mathf.Clamp(nectarMinimumScale, .01f, 1f);
        pollenMinimumScale = Mathf.Clamp(pollenMinimumScale, .01f, 1f);
        larvaMinimumScale = Mathf.Clamp(larvaMinimumScale,.01f,1f);
        larvaMaximumScale = Mathf.Max(.01f,larvaMaximumScale);
        nectarMaximumScale = Mathf.Max(.01f,nectarMaximumScale);
        pollenMaximumScale = Mathf.Max(.01f,pollenMaximumScale);
        representativeNectarPercent = Mathf.Clamp(representativeNectarPercent, 50f, 100f);
        dryNectarAlpha = Mathf.Clamp01(dryNectarAlpha); wetNectarAlpha = Mathf.Clamp01(wetNectarAlpha);

        if (Application.isPlaying && isActiveAndEnabled)
            Rebuild();
    }

    private void OnDestroy()
    {
        ClearMeshes();
    }
}
