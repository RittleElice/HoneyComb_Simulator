using UnityEngine;

public class ApiaryGridView : MonoBehaviour
{
    [SerializeField] private Material lineMaterial;
    [SerializeField] private float cellSize = 1f;
    public float CellSize => cellSize;

    private void Start()
    {
        GameBootstrap bootstrap = GameBootstrap.Instance;

        if (bootstrap == null || bootstrap.GameWorld == null)
        {
            Debug.LogError("[ApiaryGridView] GameWorld 없음");
            return;
        }

        ApiaryGrid grid = bootstrap.GameWorld.Apiary.Grid;

        DrawGrid(grid.Width, grid.Height);
    }

    private void DrawGrid(int width, int height)
    {
        float totalWidth = width * cellSize;
        float totalHeight = height * cellSize;

        // X 방향 격자선
        for (int x = 0; x <= width; x++)
        {
            float worldX = x * cellSize - totalWidth / 2f;

            CreateLine(
                new Vector3(worldX, 0f, 0f),
                new Vector3(worldX, 0f, totalHeight)
            );
        }

        // Z 방향 격자선
        for (int y = 0; y <= height; y++)
        {
            float worldZ = y * cellSize;

            CreateLine(
                new Vector3(-totalWidth / 2f, 0f, worldZ),
                new Vector3(totalWidth / 2f, 0f, worldZ)
            );
        }
    }

    private void CreateLine(Vector3 start, Vector3 end)
    {
        GameObject lineObject = new GameObject("GridLine");
        lineObject.transform.SetParent(transform, false);

        LineRenderer line =
            lineObject.AddComponent<LineRenderer>();

        line.useWorldSpace = false;
        line.positionCount = 2;

        line.SetPosition(0, start);
        line.SetPosition(1, end);

        line.startWidth = 0.025f;
        line.endWidth = 0.025f;

        line.material = lineMaterial;
        line.startColor = Color.white;
        line.endColor = Color.white;
    }
}