
using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;

public class ApiarySelectionController : MonoBehaviour
{
    [SerializeField] private Camera targetCamera;
    [SerializeField] private ApiaryGridView gridView;

    private Vector2 pointerStart;
    private bool isPressed;
    private bool pressStartedOverUI;

    private const float DragThreshold = 15f;

    public int SelectedX { get; private set; } = -1;
    public int SelectedY { get; private set; } = -1;

    public bool HasSelection =>
        SelectedX >= 0 && SelectedY >= 0;

    public event Action<int, int> OnSelectionChanged;
    public event Action OnSelectionCleared;

    public ItemSlot SelectedSlot
    {
        get
        {
            if (!HasSelection ||
                GameBootstrap.Instance == null ||
                GameBootstrap.Instance.GameWorld == null)
                return null;

            return GameBootstrap.Instance.GameWorld
                .Apiary.GetItemSlot(SelectedX, SelectedY);
        }
    }

    private void Update()
    {
        Vector2 pointerPosition;
        bool pressed;
        bool released;

        bool touchActive =
            Touchscreen.current != null &&
            Touchscreen.current.primaryTouch.press.isPressed;

        if (touchActive)
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
            pointerPosition =
                Mouse.current.position.ReadValue();

            pressed =
                Mouse.current.leftButton.wasPressedThisFrame;

            released =
                Mouse.current.leftButton.wasReleasedThisFrame;
        }
        else
        {
            return;
        }

        if (pressed)
        {
            pointerStart = pointerPosition;
            isPressed = true;

            pressStartedOverUI = IsPointerOverUI();
        }

        if (released && isPressed)
        {
            isPressed = false;

            if (Vector2.Distance(
                pointerStart,
                pointerPosition
            ) > DragThreshold)
                return;

            // UI 버튼을 누른 경우 선택을 유지
            if (pressStartedOverUI || IsPointerOverUI())
                return;

            SelectAt(pointerPosition);
        }
    }

    private bool IsPointerOverUI()
    {
        if (EventSystem.current == null)
            return false;

        if (Touchscreen.current != null &&
            Touchscreen.current.primaryTouch.press.isPressed)
        {
            int touchId =
                Touchscreen.current.primaryTouch.touchId.ReadValue();

            return EventSystem.current
                .IsPointerOverGameObject(touchId);
        }

        return EventSystem.current.IsPointerOverGameObject();
    }

    private void SelectAt(Vector2 screenPosition)
    {
        if (targetCamera == null || gridView == null)
            return;

        Ray ray =
            targetCamera.ScreenPointToRay(screenPosition);

        // 장비 이미지를 먼저 선택하고, 맞은 장비가 없을 때 바닥을 선택한다.
        if (TrySelectEquipment(ray))
            return;
        // 격자 선택 시도
        if (TrySelectGridAt(ray))
            return;

        // 격자 바깥을 클릭했다면 선택 해제
        ClearSelection();
    }

    private bool TrySelectEquipment(Ray ray)
    {
        if (GameBootstrap.Instance == null ||
            GameBootstrap.Instance.GameWorld == null)
            return false;

        ApiaryGrid grid = GameBootstrap.Instance.GameWorld.Apiary.Grid;
        HiveView selected = null;
        int bestLayer = int.MinValue;
        int bestOrder = int.MinValue;
        float bestDistance = float.PositiveInfinity;

        // 클릭 시에만 조회한다. 비활성화된 벌통과 파괴 대기 객체는 제외된다.
        foreach (HiveView view in FindObjectsByType<HiveView>(
            FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (!grid.IsInside(view.GridX, view.GridY) ||
                !view.TryGetGridHit(ray, targetCamera,
                    out float distance, out int layer, out int order))
                continue;

            // 겹친 이미지에서는 Sorting Layer, Order, 거리 순서로 선택한다.
            bool better = selected == null ||
                layer > bestLayer ||
                (layer == bestLayer && order > bestOrder) ||
                (layer == bestLayer && order == bestOrder &&
                    distance < bestDistance);
            if (!better)
                continue;

            selected = view;
            bestLayer = layer;
            bestOrder = order;
            bestDistance = distance;
        }

        if (selected == null)
            return false;

        SetSelection(selected.GridX, selected.GridY);
        return true;
    }

    private bool TrySelectGridAt(Ray ray)
    {
        if (GameBootstrap.Instance == null ||
            GameBootstrap.Instance.GameWorld == null)
            return false;

        ApiaryGrid grid =
            GameBootstrap.Instance.GameWorld.Apiary.Grid;

        float cellSize = gridView.CellSize;

        if (cellSize <= 0f)
            return false;

        Plane ground =
            new Plane(Vector3.up, Vector3.zero);

        if (!ground.Raycast(ray, out float distance))
            return false;

        Vector3 worldPosition =
            ray.GetPoint(distance);

        float totalWidth =
            grid.Width * cellSize;

        int gridX = Mathf.FloorToInt(
            (worldPosition.x + totalWidth / 2f) / cellSize
        );

        int gridY = Mathf.FloorToInt(
            worldPosition.z / cellSize
        );

        if (!grid.IsInside(gridX, gridY))
            return false;

        SetSelection(gridX, gridY);
        return true;
    }

    private void SetSelection(int x, int y)
    {
        if (GameBootstrap.Instance == null ||
            GameBootstrap.Instance.GameWorld == null)
            return;

        ApiaryData apiary =
            GameBootstrap.Instance.GameWorld.Apiary;

        if (!apiary.Grid.IsInside(x, y))
            return;

        SelectedX = x;
        SelectedY = y;

        ItemSlot slot =
            apiary.GetItemSlot(x, y);

        int hiveId =
            apiary.Grid.GetHiveId(x, y);

        if (hiveId > 0)
        {
            Debug.Log(
                $"기존 벌통 선택: ID={hiveId}, 좌표=({x}, {y})"
            );
        }
        else if (slot != null && !slot.IsEmpty)
        {
            Debug.Log(
                $"아이템 선택: {slot.Value.Name}, 좌표=({x}, {y})"
            );
        }
        else
        {
            Debug.Log(
                $"빈 격자 선택: ({x}, {y})"
            );
        }

        OnSelectionChanged?.Invoke(x, y);
    }

    public void ClearSelection()
    {
        if (!HasSelection)
            return;

        SelectedX = -1;
        SelectedY = -1;

        Debug.Log("격자 선택 해제");

        OnSelectionCleared?.Invoke();
    }
}
