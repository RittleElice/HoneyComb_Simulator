using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class WorldItemPartView : MonoBehaviour
{
    private SpriteRenderer spriteRenderer;

    [Header("격자 크기 대비 벌통 크기")]
    [SerializeField, Min(0.01f)]
    private float widthRatio = 0.8f;

    [SerializeField, Min(0.01f)]
    private float heightRatio = 1.2f;
    [Header("격자 칸 내부 위치")]
    [SerializeField, Range(0f, 1f)]
    private float positionX = 0.5f;

    [SerializeField, Range(0f, 1f)]
    private float positionZ = 0.5f;

    public HiveData Data { get; private set; }
    public int GridX { get; private set; }
    public int GridY { get; private set; }
    public bool HasGridPosition { get; private set; }

    // Collider 없이 현재 회전/크기가 반영된 Sprite 사각형을 판정한다.
    public bool TryGetGridHit(
        Ray ray, Camera camera, out float distance,
        out int layerOrder, out int spriteOrder)
    {
        distance = 0f;
        layerOrder = 0;
        spriteOrder = 0;
        if (!HasGridPosition || !isActiveAndEnabled || camera == null)
            return false;

        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer == null || !spriteRenderer.enabled ||
            spriteRenderer.forceRenderingOff || spriteRenderer.sprite == null ||
            (camera.cullingMask & (1 << gameObject.layer)) == 0)
            return false;

        Bounds bounds = spriteRenderer.localBounds;
        Plane plane = new Plane(
            transform.forward, transform.TransformPoint(bounds.center));
        if (!plane.Raycast(ray, out distance))
            return false;

        Vector3 point = ray.GetPoint(distance);
        float cameraDepth = camera.WorldToViewportPoint(point).z;
        if (cameraDepth < camera.nearClipPlane ||
            cameraDepth > camera.farClipPlane)
            return false;

        Vector3 local = transform.InverseTransformPoint(point);
        if (local.x < bounds.min.x || local.x > bounds.max.x ||
            local.y < bounds.min.y || local.y > bounds.max.y)
            return false;

        layerOrder = SortingLayer.GetLayerValueFromID(spriteRenderer.sortingLayerID);
        spriteOrder = spriteRenderer.sortingOrder;
        return true;
    }

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    public void Initialize(
        HiveData data,
        int gridWidth,
        float cellSize)
    {
        Data = data;

        ApplyLayout(data.X, data.Y, gridWidth, cellSize);
        gameObject.name = $"Hive_{data.Id}";
    }

    // 데이터가 없는 소지품 장비도 같은 프리팹 설정으로 배치한다.
    public void ApplyLayout(int x, int y, int gridWidth, float cellSize)
    {
        GridX = x;
        GridY = y;
        HasGridPosition = true;
        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();

        float originX =
            -ApiaryGrid.InitialUnlockedWidth * cellSize * 0.5f;

        float worldX =
            originX + (x + positionX) * cellSize;

        float worldZ =
            (y + positionZ) * cellSize;

        // 원본 Sprite의 크기 (Unity 월드 단위)
        Vector2 spriteSize = spriteRenderer.sprite.bounds.size;

        float targetWidth = cellSize * widthRatio;
        float targetHeight = cellSize * heightRatio;

        // 원본 Sprite 크기를 고려해 목표 크기로 변환
        transform.localScale = new Vector3(
            targetWidth / spriteSize.x,
            targetHeight / spriteSize.y,
            1f
        );

        // Sprite Pivot 위치에 따라 바닥 높이 보정
        float bottomOffset =
            spriteRenderer.sprite.bounds.min.y *
            transform.localScale.y;

        transform.position = new Vector3(
            worldX,
            -bottomOffset,
            worldZ
        );


    }
    
    [Header("이미지 방향")]
    [SerializeField] private bool faceCamera = true;

    private Camera targetCamera;

    private void LateUpdate()
    {
        if (!faceCamera)
            return;

        if (targetCamera == null)
            targetCamera = Camera.main;

        if (targetCamera == null)
            return;

        // 카메라와 동일한 방향으로 이미지 회전
        transform.rotation = targetCamera.transform.rotation;
    }

}