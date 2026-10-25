
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;

public class ApiarySelectionController : MonoBehaviour
{
    [SerializeField] private Camera targetCamera;
    [SerializeField] private ApiaryGridView gridView;
    [SerializeField] private ApiaryItemView itemView;
    private readonly List<RaycastResult> uiHits = new List<RaycastResult>();

    private void Awake()
    {
        if (targetCamera == null) targetCamera = Camera.main;
        if (gridView == null) gridView = FindAnyObjectByType<ApiaryGridView>();
        if (itemView == null) itemView = FindAnyObjectByType<ApiaryItemView>();
    }

    private void OnDisable()
    {
        isPressed = false;
        pressStartedOverUI = false;
    }

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

            pressStartedOverUI = IsPointerOverUI(pointerPosition);
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
            if (pressStartedOverUI || IsPointerOverUI(pointerPosition))
                return;

            SelectAt(pointerPosition);
        }
    }

    private bool IsPointerOverUI(Vector2 screenPosition)
    {
        if (EventSystem.current == null) return false;
        var pointer = new PointerEventData(EventSystem.current) { position = screenPosition };
        uiHits.Clear();
        EventSystem.current.RaycastAll(pointer, uiHits);
        foreach (RaycastResult hit in uiHits)
            if (hit.module is UnityEngine.UI.GraphicRaycaster) return true;
        return false;
    }

    private void SelectAt(Vector2 screenPosition)
    {
        if (targetCamera == null || gridView == null)
            return;

        Ray ray =
            targetCamera.ScreenPointToRay(screenPosition);

        // 장비 이미지를 먼저 선택하고, 맞은 장비가 없을 때 바닥을 선택한다.
        if (TrySelectEquipment(screenPosition))
            return;
        // 격자 선택 시도
        if (TrySelectGridAt(ray))
            return;

        // 격자 바깥을 클릭했다면 선택 해제
        ClearSelection();
    }

    private bool TrySelectEquipment(Vector2 screenPosition)
    {
        if (itemView == null || !itemView.TryGetEquipmentAtScreenPosition(
                screenPosition, targetCamera, out int x, out int y))
            return false;
        SetSelection(x, y);
        return true;
    }

    
    private bool TrySelectGridAt(Ray ray)
    {
        if (GameBootstrap.Instance == null ||
            GameBootstrap.Instance.GameWorld == null ||
            gridView == null)
            return false;

        ApiaryGrid grid =
            GameBootstrap.Instance.GameWorld.Apiary.Grid;

        float cellSize = gridView.CellSize;

        if (cellSize <= 0f)
            return false;

        // 격자가 놓인 바닥 평면
        Plane ground = new Plane(
            gridView.transform.up,
            gridView.transform.position);

        if (!ground.Raycast(ray, out float distance))
            return false;

        // 월드 좌표 계산
        Vector3 worldPosition =
            ray.GetPoint(distance);

        // 월드 좌표를 격자 로컬 좌표로 변환
        Vector3 localPosition =
            gridView.transform.InverseTransformPoint(
                worldPosition);

        // 초기 8열 기준 고정 원점 사용
        int gridX = Mathf.FloorToInt(
            (localPosition.x - gridView.OriginX) / cellSize);

        int gridY = Mathf.FloorToInt(
            localPosition.z / cellSize);

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
