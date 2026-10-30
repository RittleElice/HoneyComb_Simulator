using UnityEngine;
using UnityEngine.InputSystem;

[DefaultExecutionOrder(110)]
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

#pragma warning disable CS0414 // Retain old serialized data without applying the obsolete center offset.
    [SerializeField, HideInInspector]
    private float initialVerticalOffset = 0f;
#pragma warning restore CS0414

    [SerializeField, Min(0f)]
    private float initialBottomPadding = 160f;

    [Header("Vertical Pan")]
    [SerializeField, Min(0.01f)]
    private float panSensitivity = 1f;

    [SerializeField, Min(0f)]
    private float dragThreshold = 15f;

    [SerializeField, Min(0f)]
    private float verticalPanMargin = 300f;

    [Tooltip("Height of the object top kept above the viewport bottom when panning down, in UI units.")]
    [SerializeField, Min(0f)]
    private float minimumVisibleHeight = 80f;

    private float fitScale = 1f;
    private float zoomRatio = 1f;

    private Bounds contentBounds;
    private bool hasBounds;
    private float equipmentTop;

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
    private ItemSlot lastTargetSlot;
    private int lastTargetX = int.MinValue;
    private int lastTargetY = int.MinValue;

    private readonly Vector3[] corners = new Vector3[4];

    private void Start()
    {
        zoomRatio = Mathf.Clamp(
            initialZoom,
            minZoom,
            maxZoom);

        // The first LateUpdate runs after every presenter's Start has created its equipment.
    }

    private void LateUpdate()
    {
        CheckContentChanges();
        HandleZoom();
        HandlePan();
    }

    private void CheckContentChanges()
    {
        // A rebuilt view (combine/split) is still the same observed location.
        ItemSlot targetSlot = selection != null && selection.Observation != null
            ? selection.Observation.ObservedSlot : null;
        if (!ReferenceEquals(lastTargetSlot, targetSlot) ||
            lastTargetX != ObservationContext.GridX || lastTargetY != ObservationContext.GridY)
        {
            lastTargetSlot = targetSlot;
            lastTargetX = ObservationContext.GridX;
            lastTargetY = ObservationContext.GridY;
            initialCameraApplied = false;
            ResetPointer();
        }

        ItemAssemblyView view =
            presenter != null ? presenter.CurrentView : null;

        int count = view != null ? view.Parts.Count : 0;

        // Do not lock a ground-only fit while a non-empty target is still rendering.
        if (!initialCameraApplied && targetSlot != null &&
            targetSlot.Value != null && count == 0)
            return;

        if (!initialCameraApplied || view != lastView || count != lastPartCount)
        {
            lastView = view;
            lastPartCount = count;

            // Resolve the first Canvas layout before measuring the initial fit.
            if (!initialCameraApplied)
                Canvas.ForceUpdateCanvases();

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

        // Capture equipment separately so a large ground image cannot hide its loss of visibility.
        bool hasEquipment = hasBounds;
        equipmentTop = hasEquipment ? contentBounds.max.y : 0f;
        EncapsulateRect(groundImage);
        if (!hasEquipment && hasBounds)
            equipmentTop = contentBounds.max.y;

        world.localScale = previousScale;

        if (!hasBounds)
            return;

        // Keep the initial fit as the zoom baseline for this observation target.
        // Re-clamping a compensated ratio against a new fit can also cause jumps.
        if (initialCameraApplied)
        {
            ClampPosition();
            return;
        }

        if (viewport.rect.width <= 0f || viewport.rect.height <= 0f)
        {
            hasBounds = false;
            return;
        }

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

        position.y = GetBottomAlignedPosition();

        world.anchoredPosition = position;

        ClampPosition();

        initialCameraApplied = true;
    }

    private float GetBottomAlignedPosition()
    {
        return GetBottomAlignedPosition(initialBottomPadding);
    }

    private float GetBottomAlignedPosition(float bottomPadding)
    {
        var image = groundImage.GetComponent<UnityEngine.UI.Image>();
        Rect rect = image != null ? SpriteImageHitTest.VisibleRect(image) : groundImage.rect;
        float bottom = 0f;
        if (image != null && image.overrideSprite != null)
            SpriteAlphaMaskDatabase.TryGetOpaqueBottom(image.overrideSprite, out bottom);
        Vector3 groundPoint = groundImage.TransformPoint(new Vector3(rect.center.x,
            rect.yMin + rect.height * bottom));
        Vector3 target = viewport.TransformPoint(new Vector3(viewport.rect.center.x,
            viewport.rect.yMin + bottomPadding));
        // Compare in the anchored transform's parent space, including arbitrary anchors/pivots.
        return world.anchoredPosition.y + world.parent.InverseTransformPoint(target).y
            - world.parent.InverseTransformPoint(groundPoint).y;
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
        // 선택된 소비장 위에서 시작한 포인터 제스처는 카메라에 전달하지 않는다.
        // 드래그가 끝나는 프레임에도 카메라가 움직이지 않도록 아래에서
        // pointerRejected를 유지하고 release 시에만 초기화한다.
        if (selection != null && selection.BlocksCameraPan)
        {
            pointerRejected = true;
            dragging = false;
        }
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
                IsInputBlocked(position) ||
                (selection != null && selection.CanCaptureFrameDragAt(position));
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

        float bottomAligned = GetBottomAlignedPosition();

        if (contentHeight <= viewportHeight)
        {
            position.y = Mathf.Clamp(
                position.y,
                Mathf.Min(centerY, bottomAligned) - verticalPanMargin,
                Mathf.Max(centerY, bottomAligned) + verticalPanMargin);
        }
        else
        {
            float minimum =
                viewMax - maxY - verticalPanMargin;

            float maximum =
                viewMin - minY + verticalPanMargin;

            position.y = Mathf.Clamp(
                position.y,
                Mathf.Min(minimum, bottomAligned - verticalPanMargin),
                Mathf.Max(maximum, bottomAligned + verticalPanMargin));
        }

        // Allow the base below the screen, but retain a strip of the equipment top.
        float visibleHeight = Mathf.Clamp(minimumVisibleHeight, 0f, viewportHeight);
        float lowestPosition = viewMin + visibleHeight - equipmentTop * scale;
        position.y = Mathf.Max(position.y, lowestPosition);

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
        initialCameraApplied = false;
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
        minimumVisibleHeight = Mathf.Max(0f, minimumVisibleHeight);
        fitPadding = Mathf.Max(0f, fitPadding);
        initialBottomPadding = Mathf.Max(0f, initialBottomPadding);
    }
}
