
using UnityEngine;
using UnityEngine.InputSystem;

public class ObservationCameraController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private RectTransform viewport;
    [SerializeField] private RectTransform world;
    [SerializeField] private RectTransform groundImage;
    [SerializeField] private ObservationItemPresenter presenter;
    [SerializeField] private ObservationSelectionController selection;

    [Header("Zoom")]
    [SerializeField, Range(0.1f, 1f)]
    private float minZoom = 0.5f;

    [SerializeField, Min(1f)]
    private float maxZoom = 3f;

    [SerializeField, Min(0.01f)]
    private float zoomSpeed = 0.15f;

    [SerializeField, Min(0f)]
    private float fitPadding = 40f;

    [Header("Initial Camera")]
    [SerializeField, Min(0.01f)]
    private float initialZoom = 1f;

    [SerializeField]
    private float initialVerticalOffset = 0f;

    [Header("Vertical Pan")]
    [SerializeField, Min(0.01f)]
    private float panSensitivity = 1f;

    [SerializeField, Min(0f)]
    private float dragThreshold = 15f;

    [SerializeField, Min(0f)]
    private float verticalPanMargin = 300f;

    private float fitScale = 1f;
    private float zoomRatio = 1f;

    private Bounds contentBounds;
    private bool hasBounds;

    private bool dragging;
    private bool pointerTracking;
    private bool pointerRejected;
    private bool trackingTouch;

    private int trackingTouchId = -1;

    private Vector2 startPointer;
    private Vector2 lastPointer;

    private ItemAssemblyView lastView;
    private int lastPartCount = -1;

    private bool initialCameraApplied;

    private readonly Vector3[] corners = new Vector3[4];

    private void Start()
    {
        zoomRatio = Mathf.Clamp(
            initialZoom,
            minZoom,
            maxZoom);

        RecalculateBounds();

        ApplyInitialCamera();
    }

    private void LateUpdate()
    {
        CheckContentChanges();
        HandleZoom();
        HandlePan();
    }

    private void CheckContentChanges()
    {
        ItemAssemblyView view =
            presenter != null ? presenter.CurrentView : null;

        int count = view != null ? view.Parts.Count : 0;

        if (view != lastView || count != lastPartCount)
        {
            lastView = view;
            lastPartCount = count;

            RecalculateBounds();

            // 장비 표시가 처음 완료된 경우
            // 초기 카메라 구도를 다시 적용
            if (!initialCameraApplied)
                ApplyInitialCamera();
        }
    }

    public void RecalculateBounds()
    {
        if (viewport == null ||
            world == null ||
            groundImage == null)
            return;

        hasBounds = false;

        Vector3 previousScale = world.localScale;
        world.localScale = Vector3.one;

        EncapsulateRect(groundImage);

        if (presenter != null &&
            presenter.CurrentView != null)
        {
            foreach (var part in presenter.CurrentView.Parts)
            {
                if (part == null)
                    continue;

                RectTransform rect =
                    part.GetComponent<RectTransform>();

                EncapsulateRect(rect);
            }
        }

        world.localScale = previousScale;

        if (!hasBounds)
            return;

        float availableWidth = Mathf.Max(
            1f,
            viewport.rect.width - fitPadding * 2f);

        float availableHeight = Mathf.Max(
            1f,
            viewport.rect.height - fitPadding * 2f);

        float widthScale =
            availableWidth /
            Mathf.Max(1f, contentBounds.size.x);

        float heightScale =
            availableHeight /
            Mathf.Max(1f, contentBounds.size.y);

        fitScale = Mathf.Min(
            widthScale,
            heightScale);

        zoomRatio = Mathf.Clamp(
            zoomRatio,
            minZoom,
            maxZoom);

        ApplyCamera();
    }

    private void EncapsulateRect(RectTransform rect)
    {
        if (rect == null)
            return;

        rect.GetWorldCorners(corners);

        foreach (Vector3 corner in corners)
        {
            Vector3 local =
                world.InverseTransformPoint(corner);

            if (!hasBounds)
            {
                contentBounds = new Bounds(
                    local,
                    Vector3.zero);

                hasBounds = true;
            }
            else
            {
                contentBounds.Encapsulate(local);
            }
        }
    }

    private void ApplyInitialCamera()
    {
        if (!hasBounds ||
            viewport == null ||
            world == null)
            return;

        zoomRatio = Mathf.Clamp(
            initialZoom,
            minZoom,
            maxZoom);

        ApplyCamera();

        Vector2 position = world.anchoredPosition;

        position.y =
            viewport.rect.center.y -
            contentBounds.center.y * world.localScale.y +
            initialVerticalOffset;

        world.anchoredPosition = position;

        ClampPosition();

        initialCameraApplied = true;
    }

    private void HandleZoom()
    {
        // PC 마우스 휠
        if (Mouse.current != null)
        {
            Vector2 mousePosition =
                Mouse.current.position.ReadValue();

            float scroll =
                Mouse.current.scroll.ReadValue().y;

            if (Mathf.Abs(scroll) > 0.01f &&
                IsInsideViewport(mousePosition) &&
                !IsInputBlocked(mousePosition))
            {
                float multiplier =
                    scroll > 0f
                        ? 1f + zoomSpeed
                        : 1f / (1f + zoomSpeed);

                zoomRatio *= multiplier;

                zoomRatio = Mathf.Clamp(
                    zoomRatio,
                    minZoom,
                    maxZoom);

                ApplyCamera();
            }
        }

        // 모바일 두 손가락 핀치
        Touchscreen touchscreen = Touchscreen.current;

        if (touchscreen == null)
            return;

        var touches = touchscreen.touches;

        int first = -1;
        int second = -1;

        for (int i = 0; i < touches.Count; i++)
        {
            if (!touches[i].press.isPressed)
                continue;

            if (first < 0)
                first = i;
            else
            {
                second = i;
                break;
            }
        }

        if (first < 0 || second < 0)
            return;

        Vector2 p1 = touches[first].position.ReadValue();
        Vector2 p2 = touches[second].position.ReadValue();

        if (!IsInsideViewport(p1) ||
            !IsInsideViewport(p2) ||
            IsInputBlocked(p1) ||
            IsInputBlocked(p2))
            return;

        pointerRejected = true;
        dragging = false;

        Vector2 d1 = touches[first].delta.ReadValue();
        Vector2 d2 = touches[second].delta.ReadValue();

        float previousDistance =
            Vector2.Distance(p1 - d1, p2 - d2);

        float currentDistance =
            Vector2.Distance(p1, p2);

        if (previousDistance <= 0.01f)
            return;

        zoomRatio *=
            currentDistance / previousDistance;

        zoomRatio = Mathf.Clamp(
            zoomRatio,
            minZoom,
            maxZoom);

        ApplyCamera();
    }

    private void HandlePan()
    {
        Vector2 position = Vector2.zero;
        bool pressed = false;
        bool down = false;
        bool up = false;

        Touchscreen touchscreen = Touchscreen.current;

        bool useTouch =
            pointerTracking
                ? trackingTouch
                : touchscreen != null &&
                  touchscreen.primaryTouch.press.wasPressedThisFrame;

        if (useTouch)
        {
            if (touchscreen == null)
            {
                ResetPointer();
                return;
            }

            var touch = touchscreen.primaryTouch;

            position = touch.position.ReadValue();
            pressed = touch.press.isPressed;
            down = touch.press.wasPressedThisFrame;
            up = touch.press.wasReleasedThisFrame;

            if (pointerTracking &&
                touch.touchId.ReadValue() != trackingTouchId &&
                !up)
            {
                pointerRejected = true;
            }

            int activeTouches = 0;

            foreach (var finger in touchscreen.touches)
            {
                if (finger.press.isPressed)
                    activeTouches++;
            }

            if (activeTouches > 1)
                pointerRejected = true;
        }
        else
        {
            if (Mouse.current == null)
            {
                ResetPointer();
                return;
            }

            var mouse = Mouse.current;

            position = mouse.position.ReadValue();
            pressed = mouse.leftButton.isPressed;
            down = mouse.leftButton.wasPressedThisFrame;
            up = mouse.leftButton.wasReleasedThisFrame;
        }

        if (down && !pointerTracking)
        {
            pointerTracking = true;
            trackingTouch = useTouch;

            trackingTouchId = useTouch
                ? touchscreen.primaryTouch.touchId.ReadValue()
                : -1;

            startPointer = position;
            lastPointer = position;

            dragging = false;

            pointerRejected =
                !IsInsideViewport(position) ||
                IsInputBlocked(position);
        }

        if (!pointerTracking)
            return;

        if (pointerRejected)
        {
            if (up || !pressed)
                ResetPointer();

            return;
        }

        if (pressed)
        {
            if (!dragging &&
                Vector2.Distance(
                    startPointer,
                    position) > dragThreshold)
            {
                dragging = true;
            }

            if (dragging)
            {
                float scaleFactor = GetCanvasScaleFactor();

                float deltaY =
                    (position.y - lastPointer.y) /
                    scaleFactor;

                Vector2 anchored =
                    world.anchoredPosition;

                anchored.y +=
                    deltaY * panSensitivity;

                world.anchoredPosition = anchored;

                ClampPosition();
            }

            lastPointer = position;
        }

        if (up || !pressed)
            ResetPointer();
    }

    private float GetCanvasScaleFactor()
    {
        if (viewport == null)
            return 1f;

        Canvas canvas =
            viewport.GetComponentInParent<Canvas>();

        if (canvas == null)
            return 1f;

        return Mathf.Max(
            0.001f,
            canvas.rootCanvas.scaleFactor);
    }

    private bool IsInsideViewport(Vector2 position)
    {
        if (viewport == null)
            return false;

        return RectTransformUtility
            .RectangleContainsScreenPoint(
                viewport,
                position,
                GetUICamera());
    }

    private bool IsInputBlocked(Vector2 position)
    {
        return selection != null &&
               selection.IsBlocked(position);
    }

    private Camera GetUICamera()
    {
        if (viewport == null)
            return null;

        Canvas canvas =
            viewport.GetComponentInParent<Canvas>();

        if (canvas == null ||
            canvas.rootCanvas.renderMode ==
            RenderMode.ScreenSpaceOverlay)
            return null;

        return canvas.rootCanvas.worldCamera;
    }

    private void ApplyCamera()
    {
        if (world == null || !hasBounds)
            return;

        float scale = fitScale * zoomRatio;

        world.localScale =
            Vector3.one * scale;

        Vector2 anchored =
            world.anchoredPosition;

        anchored.x =
            -contentBounds.center.x * scale;

        world.anchoredPosition = anchored;

        ClampPosition();
    }

    private void ClampPosition()
    {
        if (viewport == null ||
            world == null ||
            !hasBounds)
            return;

        float scale = world.localScale.y;

        float minY =
            contentBounds.min.y * scale;

        float maxY =
            contentBounds.max.y * scale;

        float viewMin =
            viewport.rect.yMin;

        float viewMax =
            viewport.rect.yMax;

        Vector2 position =
            world.anchoredPosition;

        float contentHeight =
            maxY - minY;

        float viewportHeight =
            viewMax - viewMin;

        float centerY =
            viewport.rect.center.y -
            contentBounds.center.y * scale;

        if (contentHeight <= viewportHeight)
        {
            position.y = Mathf.Clamp(
                position.y,
                centerY - verticalPanMargin,
                centerY + verticalPanMargin);
        }
        else
        {
            float minimum =
                viewMax - maxY - verticalPanMargin;

            float maximum =
                viewMin - minY + verticalPanMargin;

            position.y = Mathf.Clamp(
                position.y,
                minimum,
                maximum);
        }

        position.x =
            -contentBounds.center.x * scale;

        world.anchoredPosition = position;
    }

    private void ResetPointer()
    {
        pointerTracking = false;
        pointerRejected = false;
        trackingTouch = false;

        trackingTouchId = -1;
        dragging = false;
    }

    private void OnDisable()
    {
        ResetPointer();
    }

    private void OnValidate()
    {
        minZoom = Mathf.Clamp(minZoom, 0.1f, 1f);
        maxZoom = Mathf.Max(1f, maxZoom);

        initialZoom = Mathf.Clamp(
            initialZoom,
            minZoom,
            maxZoom);

        zoomSpeed = Mathf.Max(0.01f, zoomSpeed);
        panSensitivity = Mathf.Max(0.01f, panSensitivity);

        dragThreshold = Mathf.Max(0f, dragThreshold);
        verticalPanMargin = Mathf.Max(0f, verticalPanMargin);
        fitPadding = Mathf.Max(0f, fitPadding);
    }
}
