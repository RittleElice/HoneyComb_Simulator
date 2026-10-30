
using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// 관찰 화면 갱신 이후 선택 입력 처리
[DefaultExecutionOrder(100)]
public class ObservationSelectionController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private ObservationFrameDragPreview dragPreview;
    [SerializeField] private ObservationController observation;
    [SerializeField] private ObservationItemPresenter presenter;
    [SerializeField] private ObservationHitArea ground;
    [SerializeField] private RectTransform inputArea;

    public ObservationSelection Selection { get; } =
        new ObservationSelection();

    public ObservationController Observation => observation;

    public event Action SelectionChanged;

    private HandItemPresenter hand;

    private ItemSlot lastSlot;
    private MovableItem lastItem;
    private ItemAssemblyView lastView;

    private bool tracking;
    private bool rejected;
    private bool touchGesture;

    private int touchId;
    private Vector2 start;

    private const float DragThreshold = 15f;

    private bool frameDragging;
    private ItemSlot dragObservedSlot;
    private MovableItem dragObservedItem;
    private ItemData dragBox;
    private ItemSlot dragSourceSlot;
    private ItemData dragFrame;
    private int dragSourceIndex = -1;
    private int dragReachableIndex = -1;

    // 카메라 컨트롤러가 소비장 이동 입력을 구분하는 데 사용
    public bool IsFrameDragging => frameDragging;

    public bool BlocksCameraPan =>
        tracking && !rejected && dragBox != null;

    // 카메라가 포인터를 누른 첫 프레임에도
    // 소비장 이동 가능성을 판정할 수 있도록 제공
    public bool CanCaptureFrameDragAt(Vector2 position)
    {
        ItemSlot slot = observation != null
            ? observation.ObservedSlot
            : null;

        if (Selection.Kind != ObservationSelectionKind.FrameSlot ||
            !ReferenceEquals(Selection.Slot, slot) ||
            !TryGetFrameSlotAt(
                slot,
                position,
                out ItemData box,
                out int index) ||
            !ReferenceEquals(Selection.Part, box) ||
            Selection.FrameSlotIndex != index)
        {
            return false;
        }

        ItemData frame = box.Frames.GetSlot(index)?.Item;

        return frame != null &&
               frame.Kind == ItemKind.Frame;
    }

    private void ResetPointer()
    {
        if (dragPreview != null)
            dragPreview.Cancel();

        tracking = false;
        rejected = false;
        frameDragging = false;

        dragObservedSlot = null;
        dragObservedItem = null;
        dragBox = null;
        dragSourceSlot = null;
        dragFrame = null;

        dragSourceIndex = -1;
        dragReachableIndex = -1;
    }

    private bool TryGetFrameSlotAt(
        ItemSlot observedSlot,
        Vector2 position,
        out ItemData box,
        out int index)
    {
        box = ObservationSelection.GetExposedBox(observedSlot);
        index = -1;

        if (box == null ||
            box.Frames == null ||
            (box.TopSlot != null && !box.TopSlot.IsEmpty) ||
            presenter == null ||
            presenter.CurrentView == null)
        {
            return false;
        }

        foreach (var part in presenter.CurrentView.Parts)
        {
            if (!ReferenceEquals(part.Part, box))
                continue;

            ObservationFrameSlotLayout layout =
                part.GetComponent<ObservationFrameSlotLayout>();

            if (layout == null || !layout.isActiveAndEnabled)
                return false;

            index = layout.GetSlotAtScreenPoint(
                position,
                UICamera);

            return index >= 0 &&
                   index < box.Frames.Capacity;
        }

        return false;
    }

    private void CaptureFrameDrag(Vector2 position)
    {
        ItemSlot observedSlot = observation != null
            ? observation.ObservedSlot
            : null;

        if (Selection.Kind != ObservationSelectionKind.FrameSlot ||
            !TryGetFrameSlotAt(
                observedSlot,
                position,
                out ItemData box,
                out int index) ||
            !ReferenceEquals(Selection.Slot, observedSlot) ||
            !ReferenceEquals(Selection.Part, box) ||
            Selection.FrameSlotIndex != index)
        {
            return;
        }

        ItemSlot source = box.Frames.GetSlot(index);
        ItemData frame = source?.Item;

        if (frame == null || frame.Kind != ItemKind.Frame)
            return;

        dragObservedSlot = observedSlot;
        dragObservedItem = observedSlot.Value;
        dragBox = box;

        dragSourceIndex = index;
        dragReachableIndex = index;

        dragSourceSlot = source;
        dragFrame = frame;
    }

    private bool IsFrameDragValid()
    {
        return dragBox != null &&
               observation != null &&
               ReferenceEquals(
                   observation.ObservedSlot,
                   dragObservedSlot) &&
               ReferenceEquals(
                   dragObservedSlot.Value,
                   dragObservedItem) &&
               ReferenceEquals(
                   ObservationSelection.GetExposedBox(
                       dragObservedSlot),
                   dragBox) &&
               (dragBox.TopSlot == null ||
                dragBox.TopSlot.IsEmpty) &&
               dragBox.Frames != null &&
               dragSourceIndex >= 0 &&
               dragSourceIndex < dragBox.Frames.Capacity &&
               ReferenceEquals(
                   dragBox.Frames.GetSlot(dragSourceIndex),
                   dragSourceSlot) &&
               ReferenceEquals(
                   dragSourceSlot.Value,
                   dragFrame) &&
               Selection.Kind ==
                   ObservationSelectionKind.FrameSlot &&
               ReferenceEquals(
                   Selection.Slot,
                   dragObservedSlot) &&
               ReferenceEquals(
                   Selection.Part,
                   dragBox) &&
               Selection.FrameSlotIndex == dragSourceIndex;
    }

   
    private void CompleteFrameDrag(Vector2 position)
    {
        if (!IsFrameDragValid() ||
            dragReachableIndex < 0 ||
            dragReachableIndex == dragSourceIndex)
            return;

        // 마지막으로 미리보기가 도달한 슬롯
        int targetIndex = dragReachableIndex;

        // 드롭 직전 실제 데이터와 이동 경로 재검증
        int reachable =
            ObservationFrameMoveAction.GetReachableIndex(
                dragBox,
                dragSourceIndex,
                targetIndex);

        if (reachable != targetIndex)
            return;

        ItemSlot observedSlot = dragObservedSlot;
        ItemData box = dragBox;
        int sourceIndex = dragSourceIndex;

        // 실제 데이터는 여기서만 이동
        if (ObservationFrameMoveAction.TryMove(
                observedSlot,
                box,
                sourceIndex,
                targetIndex))
        {
            // 이동 후 목적지 슬롯 선택 유지
            Selection.SelectFrameSlot(
                observedSlot,
                box,
                targetIndex);

            SelectionChanged?.Invoke();
        }
    }
    public Camera UICamera
    {
        get
        {
            Canvas canvas = inputArea != null
                ? inputArea.GetComponentInParent<Canvas>()
                : null;

            return canvas == null ||
                   canvas.rootCanvas.renderMode ==
                   RenderMode.ScreenSpaceOverlay
                ? null
                : canvas.rootCanvas.worldCamera;
        }
    }

    public Image GroundImage =>
        ground != null ? ground.Image : null;

    public Image SelectedImage
    {
        get
        {
            if (Selection.Kind ==
                ObservationSelectionKind.Ground)
            {
                return GroundImage;
            }

            if (Selection.Kind ==
                ObservationSelectionKind.TopAttachment)
            {
                return GetTopAttachmentImage();
            }

            if (Selection.Kind !=
                    ObservationSelectionKind.EquipmentPart ||
                presenter == null ||
                presenter.CurrentView == null)
            {
                return null;
            }

            foreach (var part in presenter.CurrentView.Parts)
            {
                if (ReferenceEquals(part.Part, Selection.Part))
                    return part.Image;
            }

            return null;
        }
    }

    private Image GetTopAttachmentImage()
    {
        ItemSlot slot = observation != null
            ? observation.ObservedSlot
            : null;

        ItemData box =
            ObservationSelection.GetExposedBox(slot);

        if (box == null ||
            presenter == null ||
            presenter.CurrentView == null ||
            !ReferenceEquals(
                Selection.SelectedTopAttachmentSlot,
                box.TopSlot))
        {
            return null;
        }

        foreach (var part in presenter.CurrentView.Parts)
        {
            if (!ReferenceEquals(part.Part, box))
                continue;

            ObservationTopAttachmentView topView =
                part.GetComponent<ObservationTopAttachmentView>();

            if (topView == null ||
                topView.CurrentView == null ||
                !ReferenceEquals(
                    topView.CurrentView.Part,
                    Selection.Part))
            {
                return null;
            }

            return topView.CurrentView.Image;
        }

        return null;
    }

    private void LateUpdate()
    {
        Synchronize();
        HandlePointer();
    }

    public void Synchronize()
    {
        ItemSlot slot = observation != null
            ? observation.ObservedSlot
            : null;

        MovableItem item = slot?.Value;

        ItemAssemblyView view = presenter != null
            ? presenter.CurrentView
            : null;

        bool changed =
            !ReferenceEquals(slot, lastSlot) ||
            !ReferenceEquals(item, lastItem) ||
            view != lastView;

        ObservationSelectionKind previousKind =
            Selection.Kind;

        ItemData previousPart = Selection.Part;

        int previousFrameIndex =
            Selection.FrameSlotIndex;

        ItemSlot previousTopSlot =
            Selection.SelectedTopAttachmentSlot;

        Selection.Revalidate(slot);

        bool selectionChanged =
            previousKind != Selection.Kind ||
            !ReferenceEquals(previousPart, Selection.Part) ||
            previousFrameIndex != Selection.FrameSlotIndex ||
            !ReferenceEquals(
                previousTopSlot,
                Selection.SelectedTopAttachmentSlot);

        if (changed || selectionChanged)
        {
            ResetPointer();

            lastSlot = slot;
            lastItem = item;
            lastView = view;

            SelectionChanged?.Invoke();
        }
    }

    public void ClearSelection()
    {
        ResetPointer();
        Selection.Clear();
        SelectionChanged?.Invoke();
    }

    public void SelectAt(Vector2 position)
    {
        Synchronize();

        if (inputArea == null ||
            !RectTransformUtility.RectangleContainsScreenPoint(
                inputArea,
                position,
                UICamera) ||
            IsBlocked(position))
        {
            return;
        }

        ItemSlot slot = observation != null
            ? observation.ObservedSlot
            : null;

        if (slot == null)
        {
            ClearSelection();
            return;
        }

        // 1. 설치된 상단 장비 우선 선택
        if (TrySelectTopAttachment(slot, position))
        {
            SelectionChanged?.Invoke();
            return;
        }

        // 2. 소비장 슬롯 선택
        if (TrySelectFrameSlot(slot, position))
        {
            SelectionChanged?.Invoke();
            return;
        }

        // 3. 기존 장비 선택
        UIItemPartView best = null;

        if (presenter != null &&
            presenter.CurrentView != null)
        {
            foreach (var part in presenter.CurrentView.Parts)
            {
                ObservationHitArea area =
                    part.GetComponent<ObservationHitArea>();

                if (area == null ||
                    !area.Contains(position, UICamera))
                {
                    continue;
                }

                if (best == null ||
                    part.transform.GetSiblingIndex() >
                    best.transform.GetSiblingIndex())
                {
                    best = part;
                }
            }
        }

        if (best != null)
        {
            Selection.SelectPart(slot, best.Part);
        }
        else if (ground != null &&
                 ground.Contains(position, UICamera))
        {
            Selection.SelectGround(slot);
        }
        else
        {
            Selection.Clear();
        }

        SelectionChanged?.Invoke();
    }

    private bool TrySelectTopAttachment(
        ItemSlot observedSlot,
        Vector2 screenPosition)
    {
        if (presenter == null ||
            presenter.CurrentView == null)
        {
            return false;
        }

        ItemData exposedBox =
            ObservationSelection.GetExposedBox(observedSlot);

        if (exposedBox == null ||
            exposedBox.TopSlot == null ||
            exposedBox.TopSlot.IsEmpty)
        {
            return false;
        }

        foreach (var part in presenter.CurrentView.Parts)
        {
            if (!ReferenceEquals(part.Part, exposedBox))
                continue;

            ObservationTopAttachmentView topView =
                part.GetComponent<ObservationTopAttachmentView>();

            if (topView == null ||
                !topView.isActiveAndEnabled ||
                topView.CurrentView == null)
            {
                return false;
            }

            Image image = topView.CurrentView.Image;

            if (image == null ||
                !image.gameObject.activeInHierarchy)
            {
                return false;
            }

            // Match the rendered sprite, including transparent pixels and aspect padding.
            if (!SpriteImageHitTest.Contains(
                    image,
                    screenPosition,
                    UICamera))
            {
                return false;
            }

            // 동일 상단 장비 재클릭 시 선택 해제
            bool alreadySelected =
                Selection.Kind ==
                    ObservationSelectionKind.TopAttachment &&
                ReferenceEquals(
                    Selection.SelectedTopAttachmentSlot,
                    exposedBox.TopSlot) &&
                ReferenceEquals(
                    Selection.Part,
                    exposedBox.TopSlot.Item);

            if (alreadySelected)
            {
                Selection.Clear();
                return true;
            }

            return Selection.SelectTopAttachment(
                observedSlot,
                exposedBox);
        }

        return false;
    }

    private bool TrySelectFrameSlot(
        ItemSlot observedSlot,
        Vector2 screenPosition)
    {
        if (presenter == null ||
            presenter.CurrentView == null)
        {
            return false;
        }

        ItemData exposedBox =
            ObservationSelection.GetExposedBox(observedSlot);

        if (exposedBox == null)
            return false;

        // 격왕판 등 상단 장비가 설치된 경우
        // 아래 소비장 슬롯 선택 차단
        if (exposedBox.TopSlot != null &&
            !exposedBox.TopSlot.IsEmpty)
        {
            return false;
        }

        foreach (var part in presenter.CurrentView.Parts)
        {
            if (!ReferenceEquals(part.Part, exposedBox))
                continue;

            ObservationFrameSlotLayout layout =
                part.GetComponent<ObservationFrameSlotLayout>();

            if (layout == null ||
                !layout.isActiveAndEnabled)
            {
                return false;
            }

            int slotIndex =
                layout.GetSlotAtScreenPoint(
                    screenPosition,
                    UICamera);

            if (slotIndex < 0 ||
                slotIndex >= exposedBox.Frames.Capacity)
            {
                return false;
            }

            // 동일 소비장 슬롯 재클릭 시 선택 해제
            bool alreadySelected =
                Selection.Kind ==
                    ObservationSelectionKind.FrameSlot &&
                ReferenceEquals(
                    Selection.Slot,
                    observedSlot) &&
                ReferenceEquals(
                    Selection.Part,
                    exposedBox) &&
                Selection.FrameSlotIndex == slotIndex;

            if (alreadySelected)
            {
                Selection.Clear();
                return true;
            }

            bool selected = Selection.SelectFrameSlot(
                observedSlot,
                exposedBox,
                slotIndex);

            if (selected)
            {
                Debug.Log(
                    $"[Observation] 소비장 슬롯 선택: " +
                    $"{Selection.PartIndex + 1}층, " +
                    $"{Selection.FrameSlotIndex + 1}번");
            }

            return selected;
        }

        return false;
    }

    private ObservationFrameSlotLayout GetDragLayout()
    {
        if (presenter == null ||
            presenter.CurrentView == null ||
            dragBox == null)
        {
            return null;
        }

        foreach (var part in presenter.CurrentView.Parts)
        {
            if (ReferenceEquals(part.Part, dragBox))
            {
                return part.GetComponent<
                    ObservationFrameSlotLayout>();
            }
        }

        return null;
    }

    private bool TryGetDragVisual(
        out RectTransform visual)
    {
        visual = null;

        if (presenter == null ||
            presenter.CurrentView == null ||
            dragBox == null)
        {
            return false;
        }

        foreach (var part in presenter.CurrentView.Parts)
        {
            if (!ReferenceEquals(part.Part, dragBox))
                continue;

            ObservationInteriorContentsView contents =
                part.GetComponent<
                    ObservationInteriorContentsView>();

            return contents != null &&
                   contents.TryGetFrameVisual(
                       dragSourceIndex,
                       dragFrame,
                       out visual);
        }

        return false;
    }

    public bool IsBlocked(Vector2 position)
    {
        if (ObservationInputGuard.IsOverUI(position))
            return true;

        if (hand == null)
        {
            hand =
                FindAnyObjectByType<HandItemPresenter>();
        }

        return hand != null &&
               hand.BlocksObservationInput(position);
    }

private void HandlePointer()
{
    Vector2 position;
    bool down, up, held;

    Touchscreen screen = Touchscreen.current;

    bool useTouch =
        tracking
            ? touchGesture
            : screen != null &&
              (screen.primaryTouch.press.isPressed ||
               screen.primaryTouch.press.wasPressedThisFrame);

    if (useTouch)
    {
        if (screen == null)
        {
            ResetPointer();
            return;
        }

        int count = 0;

        foreach (var finger in screen.touches)
        {
            if (finger.press.isPressed)
                count++;
        }

        var touch = screen.primaryTouch;

        position = touch.position.ReadValue();
        down = touch.press.wasPressedThisFrame;
        up = touch.press.wasReleasedThisFrame;
        held = touch.press.isPressed;

        if (count > 1 ||
            (tracking &&
             touch.touchId.ReadValue() != touchId &&
             !up))
        {
            rejected = true;
        }
    }
    else
    {
        if (Mouse.current == null)
        {
            ResetPointer();
            return;
        }

        position = Mouse.current.position.ReadValue();

        down = Mouse.current.leftButton.wasPressedThisFrame;
        up = Mouse.current.leftButton.wasReleasedThisFrame;
        held = Mouse.current.leftButton.isPressed;
    }

    if (down && !tracking)
    {
        ResetPointer();

        tracking = true;
        touchGesture = useTouch;
        start = position;

        touchId = useTouch
            ? screen.primaryTouch.touchId.ReadValue()
            : -1;

        // 최초 누름 위치에서는 입력 차단 검사 유지
        rejected =
            IsBlocked(position) ||
            inputArea == null ||
            !RectTransformUtility.RectangleContainsScreenPoint(
                inputArea,
                position,
                UICamera);

        if (useTouch)
        {
            int count = 0;

            foreach (var finger in screen.touches)
            {
                if (finger.press.isPressed)
                    count++;
            }

            rejected |= count > 1;
        }

        if (!rejected)
            CaptureFrameDrag(position);
    }

    if (!tracking)
        return;

    // 소비장 드래그 후보가 없을 때만
    // 이동 중 입력 차단 영역을 검사한다.
    if (dragBox == null)
    {
        rejected |= IsBlocked(position);
    }

    // 명시적인 취소 조건은 유지
    if (useTouch &&
        (screen.primaryTouch.phase.ReadValue() ==
            UnityEngine.InputSystem.TouchPhase.Canceled ||
         screen.primaryTouch.touchId.ReadValue() != touchId))
    {
        rejected = true;
    }

    if (Keyboard.current != null &&
        Keyboard.current.escapeKey.wasPressedThisFrame)
    {
        rejected = true;
    }

    if (dragBox != null && !IsFrameDragValid())
        rejected = true;

    Vector2 delta = position - start;

    // X축 이동량만으로 드래그 시작
    // Y축 이동량은 무시한다.
    if (!frameDragging &&
        !rejected &&
        dragBox != null &&
        Mathf.Abs(delta.x) > DragThreshold)
    {
        if (TryGetDragVisual(out RectTransform visual) &&
            dragPreview != null)
        {
            frameDragging = true;

            dragPreview.Begin(
                visual,
                start,
                UICamera);
        }
        else
        {
            rejected = true;
        }
    }

    // 슬롯 단위 이미지 스냅
    if (frameDragging &&
        !rejected &&
        dragPreview != null)
    {
        ObservationFrameSlotLayout layout =
            GetDragLayout();

        if (layout != null)
        {
            // 드래그 시작 높이로 Y좌표 고정
            Vector2 projectedPosition =
                new Vector2(position.x, start.y);

            int requestedIndex =
                layout.GetSlotAtScreenPoint(
                    projectedPosition,
                    UICamera);

            if (requestedIndex >= 0 &&
                requestedIndex < dragBox.Frames.Capacity)
            {
                int reachable =
                    ObservationFrameMoveAction.GetReachableIndex(
                        dragBox,
                        dragSourceIndex,
                        requestedIndex);

                if (reachable >= 0)
                {
                    dragReachableIndex = reachable;

                    dragPreview.SnapTo(
                        layout.GetSlotCenterWorld(reachable),
                        layout.GetSlotCenterWorld(
                            dragSourceIndex));
                }
            }
        }
    }

    if (up)
    {
        bool click = !rejected && !frameDragging;

        if (!rejected && frameDragging)
        {
            // 데이터 이동 전에 시각 효과 종료
            if (dragPreview != null)
                dragPreview.End();

            // 마지막으로 미리보기가 도달한 슬롯에 확정
            CompleteFrameDrag(position);
        }

        ResetPointer();

        if (click)
            SelectAt(position);
    }
    else if (!held)
    {
        ResetPointer();
    }
}


private void OnApplicationFocus(bool focused)
{
    if (!focused)
        ResetPointer();
}

private void OnDisable()
{
    ResetPointer();
    ClearSelection();

    lastSlot = null;
    lastView = null;
    lastItem = null;
}
}
