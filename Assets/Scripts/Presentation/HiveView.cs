using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class HiveView : MonoBehaviour
{
    [Header("벌통 이미지")]
    [SerializeField] private SpriteRenderer spriteRenderer;

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

    private void Awake()
    {
        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();
    }

    public void Initialize(
        HiveData data,
        int gridWidth,
        float cellSize)
    {
        Data = data;

        float totalWidth = gridWidth * cellSize;

        float worldX =
            (data.X + positionX) * cellSize
            - totalWidth / 2f;

        float worldZ =
            (data.Y + positionZ) * cellSize;

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

        gameObject.name = $"Hive_{data.Id}";
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