
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;

public class ApiaryCameraController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Camera targetCamera;
    [SerializeField] private ApiaryGridView gridView;

    [Header("Drag")]
    [SerializeField, Min(0.01f)]
    private float dragSensitivity = 1f;

    [Header("Camera Bounds")]
    [SerializeField]
    private bool limitCameraMovement = true;

    [SerializeField, Min(0f)]
    private float boundaryPadding = 0.5f;

    [SerializeField, Min(0.01f)]
    private float referenceDistance = 12f;

    private Vector2 lastPointerPosition;
    private bool isDragging;
    private bool dragStartedOverUI;

    private readonly List<RaycastResult> uiRaycastResults =
        new List<RaycastResult>();

    private void Awake()
    {
        if (targetCamera == null)
            targetCamera = Camera.main;

        if (gridView == null)
            gridView = FindAnyObjectByType<ApiaryGridView>();
    }

    private void Update()
    {
        if (targetCamera == null)
            return;

        // 해금이나 화면 크기 변경 시에도 경계 유지
        if (limitCameraMovement)
            ClampCameraPosition();

        Vector2 pointerPosition;
        bool pressed;
        bool pressedThisFrame;

        if (Touchscreen.current != null &&
            Touchscreen.current.primaryTouch.press.isPressed)
        {
            var touch = Touchscreen.current.primaryTouch;

            pointerPosition = touch.position.ReadValue();
            pressed = touch.press.isPressed;
            pressedThisFrame = touch.press.wasPressedThisFrame;
        }
        else if (Mouse.current != null)
        {
            var mouse = Mouse.current;

            pointerPosition = mouse.position.ReadValue();
            pressed = mouse.leftButton.isPressed;
            pressedThisFrame = mouse.leftButton.wasPressedThisFrame;
        }
        else
        {
            isDragging = false;
            dragStartedOverUI = false;
            return;
        }

        if (pressedThisFrame)
        {
            lastPointerPosition = pointerPosition;

            dragStartedOverUI =
                IsPointerOverUI(pointerPosition);

            isDragging = !dragStartedOverUI;
        }

        if (!pressed)
        {
            isDragging = false;
            dragStartedOverUI = false;
            return;
        }

        if (dragStartedOverUI || !isDragging)
            return;

        float unitsPerPixel;

        if (targetCamera.orthographic)
        {
            unitsPerPixel =
                2f * targetCamera.orthographicSize /
                Mathf.Max(1, targetCamera.pixelHeight);
        }
        else
        {
            unitsPerPixel =
                2f * referenceDistance *
                Mathf.Tan(
                    targetCamera.fieldOfView *
                    0.5f *
                    Mathf.Deg2Rad) /
                Mathf.Max(1, targetCamera.pixelHeight);
        }

        float deltaX =
            (pointerPosition.x - lastPointerPosition.x) *
            unitsPerPixel *
            dragSensitivity;

        targetCamera.transform.position -=
            new Vector3(deltaX, 0f, 0f);

        if (limitCameraMovement)
            ClampCameraPosition();

        lastPointerPosition = pointerPosition;
    }

    private void ClampCameraPosition()
    {
        if (gridView == null ||
            targetCamera == null ||
            GameBootstrap.Instance == null ||
            GameBootstrap.Instance.GameWorld == null)
            return;

        ApiaryGrid grid =
            GameBootstrap.Instance.GameWorld.Apiary.Grid;

        float cellSize = gridView.CellSize;

        // 양봉장의 실제 월드 좌우 경계
        float leftBoundary =
            gridView.transform.TransformPoint(
                new Vector3(
                    gridView.OriginX,
                    0f,
                    0f)).x;

        float rightBoundary =
            gridView.transform.TransformPoint(
                new Vector3(
                    gridView.OriginX + grid.Width * cellSize,
                    0f,
                    0f)).x;

        leftBoundary -= boundaryPadding;
        rightBoundary += boundaryPadding;

        // 카메라 화면의 좌우 가장자리가
        // 바닥 평면과 만나는 위치를 계산
        Plane ground = new Plane(
            gridView.transform.up,
            gridView.transform.position);

        Ray leftRay =
            targetCamera.ViewportPointToRay(
                new Vector3(0f, 0.5f, 0f));

        Ray rightRay =
            targetCamera.ViewportPointToRay(
                new Vector3(1f, 0.5f, 0f));

        if (!ground.Raycast(leftRay, out float leftDistance) ||
            !ground.Raycast(rightRay, out float rightDistance))
            return;

        float visibleLeft =
            leftRay.GetPoint(leftDistance).x;

        float visibleRight =
            rightRay.GetPoint(rightDistance).x;

        float visibleWidth =
            visibleRight - visibleLeft;

        float availableWidth =
            rightBoundary - leftBoundary;

        Vector3 position =
            targetCamera.transform.position;

        // 화면이 양봉장보다 넓으면 중앙에 고정
        if (visibleWidth >= availableWidth)
        {
            float visibleCenter =
                (visibleLeft + visibleRight) * 0.5f;

            float targetCenter =
                (leftBoundary + rightBoundary) * 0.5f;

            position.x += targetCenter - visibleCenter;
        }
        else
        {
            if (visibleLeft < leftBoundary)
                position.x += leftBoundary - visibleLeft;

            if (visibleRight > rightBoundary)
                position.x -= visibleRight - rightBoundary;
        }

        targetCamera.transform.position = position;
    }

    private bool IsPointerOverUI(Vector2 screenPosition)
    {
        if (EventSystem.current == null)
            return false;

        PointerEventData pointerData =
            new PointerEventData(EventSystem.current);

        pointerData.position = screenPosition;

        uiRaycastResults.Clear();

        EventSystem.current.RaycastAll(
            pointerData,
            uiRaycastResults);

        return uiRaycastResults.Count > 0;
    }
}
