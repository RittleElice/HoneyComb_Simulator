
using UnityEngine;
using UnityEngine.InputSystem;

public class ApiarySelectionController : MonoBehaviour
{
    [SerializeField] private Camera targetCamera;
    [SerializeField] private ApiaryGridView gridView;

    private Vector2 pointerStart;
    private bool isPressed;

    private const float DragThreshold = 15f;

    private void Update()
    {
        Vector2 pointerPosition;
        bool pressed;
        bool released;

        if (Touchscreen.current != null &&
            Touchscreen.current.primaryTouch.press.isPressed)
        {
            pointerPosition =
                Touchscreen.current.primaryTouch.position.ReadValue();

            pressed = !isPressed;
            released = false;
        }
        else if (isPressed &&
                 Touchscreen.current != null &&
                 Touchscreen.current.primaryTouch.press.wasReleasedThisFrame)
        {
            pointerPosition =
                Touchscreen.current.primaryTouch.position.ReadValue();

            pressed = false;
            released = true;
        }
        else if (Mouse.current != null)
        {
            pointerPosition = Mouse.current.position.ReadValue();
            pressed = Mouse.current.leftButton.wasPressedThisFrame;
            released = Mouse.current.leftButton.wasReleasedThisFrame;
        }
        else
        {
            return;
        }

        if (pressed)
        {
            pointerStart = pointerPosition;
            isPressed = true;
        }

        if (released && isPressed)
        {
            isPressed = false;

            if (Vector2.Distance(pointerStart, pointerPosition)
                <= DragThreshold)
            {
                SelectAt(pointerPosition);
            }
        }
    }

    private void SelectAt(Vector2 screenPosition)
    {
        if (targetCamera == null || gridView == null)
            return;

        Ray ray = targetCamera.ScreenPointToRay(screenPosition);

        // 1. 벌통 이미지를 먼저 검사
        if (Physics.Raycast(ray, out RaycastHit hit))
        {
            HiveView hiveView = hit.collider.GetComponentInParent<HiveView>();

            if (hiveView != null && hiveView.Data != null)
            {
                HiveData hive = hiveView.Data;

                Debug.Log(
                    $"벌통 선택: ID={hive.Id}, 좌표=({hive.X}, {hive.Y})"
                );

                return;
            }
        }

        // 2. 벌통을 클릭하지 않았다면 기존 격자 선택
        SelectGridAt(ray);
    }
    private void SelectGridAt(Ray ray)
{
    ApiaryData apiary =
        GameBootstrap.Instance.GameWorld.Apiary;

    ApiaryGrid grid = apiary.Grid;
    float cellSize = gridView.CellSize;

    Plane ground = new Plane(Vector3.up, Vector3.zero);

    if (!ground.Raycast(ray, out float distance))
        return;

    Vector3 worldPosition = ray.GetPoint(distance);
    float totalWidth = grid.Width * cellSize;

    int gridX = Mathf.FloorToInt(
        (worldPosition.x + totalWidth / 2f) / cellSize
    );

    int gridY = Mathf.FloorToInt(
        worldPosition.z / cellSize
    );

    if (!grid.IsInside(gridX, gridY))
        return;

    int hiveId = grid.GetHiveId(gridX, gridY);

    if (hiveId > 0)
    {
        Debug.Log(
            $"벌통 선택: ID={hiveId}, 좌표=({gridX}, {gridY})"
        );
    }
    else
    {
        Debug.Log(
            $"빈 격자 선택: ({gridX}, {gridY})"
        );
    }
}
}
