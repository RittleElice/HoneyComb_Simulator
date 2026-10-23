using UnityEngine;
using UnityEngine.InputSystem;

public class ApiaryCameraController : MonoBehaviour
{
    [SerializeField] private Camera targetCamera;
    [SerializeField] private float dragSensitivity = 1f;

    private Vector2 lastPointerPosition;
    private bool isDragging;

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
            return;
        }

        if (pressedThisFrame)
        {
            lastPointerPosition = pointerPosition;
            isDragging = true;
        }

        if (!pressed)
        {
            isDragging = false;
            return;
        }

        if (!isDragging)
            return;

        float distance = 12f;

        float unitsPerPixel =
            2f * distance *
            Mathf.Tan(targetCamera.fieldOfView * 0.5f * Mathf.Deg2Rad)
            / targetCamera.pixelHeight;

        float deltaX =
            (pointerPosition.x - lastPointerPosition.x)
            * unitsPerPixel
            * dragSensitivity;

        targetCamera.transform.position -=
            new Vector3(deltaX, 0f, 0f);

        lastPointerPosition = pointerPosition;
    }
}