
using System.Collections.Generic;
using UnityEngine;

public class ApiaryGridView : MonoBehaviour
{
    [Header("Grid")]
    [SerializeField] private Material lineMaterial;
    [SerializeField, Min(0.01f)] private float cellSize = 1f;

    [Header("Locked Area")]
    [SerializeField] private Color lockedColor =
        new Color(0.12f, 0.14f, 0.18f, 0.65f);

    [SerializeField] private Material lockedMaterial;

    public float CellSize => cellSize;

    // 최초 8열의 중심을 원점으로 고정
    public float OriginX =>
        -ApiaryGrid.InitialUnlockedWidth * cellSize * 0.5f;

    private ApiaryGrid grid;
    private Transform gridRoot;
    private MeshRenderer lockedRenderer;
    private MeshFilter lockedFilter;
    private Mesh lockedMesh;
    private Material runtimeLockedMaterial;

    private int cachedWidth = -1;
    private int cachedHeight = -1;
    private int cachedUnlockedWidth = -1;

    private void Start()
    {
        GameBootstrap bootstrap = GameBootstrap.Instance;

        if (bootstrap == null || bootstrap.GameWorld == null)
        {
            Debug.LogError("[ApiaryGridView] GameWorld 없음");
            enabled = false;
            return;
        }

        grid = bootstrap.GameWorld.Apiary.Grid;
        Rebuild();
    }

    private void Update()
    {
        if (grid == null)
            return;

        if (grid.Width != cachedWidth ||
            grid.Height != cachedHeight ||
            grid.UnlockedWidth != cachedUnlockedWidth)
        {
            Rebuild();
        }
    }

    public Vector3 GetCellCenterLocal(int x, int y)
    {
        return new Vector3(
            OriginX + (x + 0.5f) * cellSize,
            0f,
            (y + 0.5f) * cellSize);
    }

    public Vector3 GetCellCenterWorld(int x, int y)
    {
        return transform.TransformPoint(
            GetCellCenterLocal(x, y));
    }

    public void Rebuild()
    {
        if (grid == null)
            return;

        if (gridRoot != null)
        {
            gridRoot.gameObject.SetActive(false);
            Destroy(gridRoot.gameObject);
        }

        GameObject root = new GameObject("GeneratedGrid");
        root.transform.SetParent(transform, false);
        gridRoot = root.transform;

        float left = OriginX;
        float right = OriginX + grid.Width * cellSize;
        float depth = grid.Height * cellSize;

        for (int x = 0; x <= grid.Width; x++)
        {
            float worldX = left + x * cellSize;

            CreateLine(
                new Vector3(worldX, 0f, 0f),
                new Vector3(worldX, 0f, depth));
        }

        for (int y = 0; y <= grid.Height; y++)
        {
            float worldZ = y * cellSize;

            CreateLine(
                new Vector3(left, 0f, worldZ),
                new Vector3(right, 0f, worldZ));
        }

        CreateLockedArea();

        cachedWidth = grid.Width;
        cachedHeight = grid.Height;
        cachedUnlockedWidth = grid.UnlockedWidth;
    }

    private void CreateLine(Vector3 start, Vector3 end)
    {
        GameObject lineObject = new GameObject("GridLine");
        lineObject.transform.SetParent(gridRoot, false);

        LineRenderer line =
            lineObject.AddComponent<LineRenderer>();

        line.useWorldSpace = false;
        line.positionCount = 2;
        line.SetPosition(0, start);
        line.SetPosition(1, end);

        line.startWidth = 0.025f;
        line.endWidth = 0.025f;

        if (lineMaterial != null)
            line.sharedMaterial = lineMaterial;

        line.startColor = Color.white;
        line.endColor = Color.white;
    }

    private void CreateLockedArea()
    {
        GameObject obj = new GameObject("LockedArea");
        obj.transform.SetParent(gridRoot, false);

        lockedFilter = obj.AddComponent<MeshFilter>();
        lockedRenderer = obj.AddComponent<MeshRenderer>();

        lockedMesh = new Mesh();
        lockedMesh.name = "LockedAreaMesh";

        float xMin =
            OriginX + grid.UnlockedWidth * cellSize;

        float xMax = xMin + cellSize;
        float zMin = 0f;
        float zMax = grid.Height * cellSize;

        lockedMesh.vertices = new Vector3[]
        {
            new Vector3(xMin, 0.005f, zMin),
            new Vector3(xMin, 0.005f, zMax),
            new Vector3(xMax, 0.005f, zMax),
            new Vector3(xMax, 0.005f, zMin)
        };

        lockedMesh.triangles = new int[]
        {
            0, 1, 2,
            0, 2, 3
        };

        lockedMesh.uv = new Vector2[]
        {
            new Vector2(0, 0),
            new Vector2(0, 1),
            new Vector2(1, 1),
            new Vector2(1, 0)
        };

        lockedMesh.RecalculateNormals();
        lockedMesh.RecalculateBounds();

        lockedFilter.sharedMesh = lockedMesh;

        if (lockedMaterial != null)
        {
            lockedRenderer.sharedMaterial = lockedMaterial;
        }
        else
        {
            Shader shader = Shader.Find("Sprites/Default");

            if (shader != null)
            {
                runtimeLockedMaterial = new Material(shader);
                runtimeLockedMaterial.color = lockedColor;
                lockedRenderer.sharedMaterial = runtimeLockedMaterial;
            }
        }
    }

    private void OnDestroy()
    {
        if (lockedMesh != null)
            Destroy(lockedMesh);

        if (runtimeLockedMaterial != null)
            Destroy(runtimeLockedMaterial);
    }
}
