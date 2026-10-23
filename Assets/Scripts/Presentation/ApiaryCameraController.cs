
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;

public class ApiaryCameraController : MonoBehaviour
{
    [SerializeField] private Camera targetCamera;
    [SerializeField] private float dragSensitivity = 1f;

    private Vector2 lastPointerPosition;
    private bool isDragging;

    // UI에서 시작한 드래그인지 저장
    private bool dragStartedOverUI;

    private readonly List<RaycastResult> uiRaycastResults =
        new List<RaycastResult>();

    private void Awake()
    {
        if (targetCamera == null)
            targetCamera = Camera.main;
    }

    private void Update()
    {
        if (targetCamera == null)
            return;

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

        // 드래그 시작 시 UI 위인지 확인
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

        // UI에서 시작한 입력은 카메라 이동 차단
        if (dragStartedOverUI || !isDragging)
            return;

        float distance = 12f;

        float unitsPerPixel =
            2f * distance *
            Mathf.Tan(
                targetCamera.fieldOfView *
                0.5f *
                Mathf.Deg2Rad
            ) / targetCamera.pixelHeight;

        float deltaX =
            (pointerPosition.x - lastPointerPosition.x) *
            unitsPerPixel *
            dragSensitivity;

        targetCamera.transform.position -=
            new Vector3(deltaX, 0f, 0f);

        lastPointerPosition = pointerPosition;
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
            uiRaycastResults
        );

        return uiRaycastResults.Count > 0;
    }
}
