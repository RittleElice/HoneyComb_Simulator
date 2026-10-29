
using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// 관찰 화면 갱신 이후 선택 입력 처리
[DefaultExecutionOrder(100)]
public class ObservationSelectionController : MonoBehaviour
{
    [Header("References")]
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

    private void ResetPointer()
    {
        tracking = false;
        rejected = false;
        frameDragging = false;
        dragObservedSlot = null;
        dragObservedItem = null;
        dragBox = null;
        dragSourceSlot = null;
        dragFrame = null;
        dragSourceIndex = -1;
    }

    private bool TryGetFrameSlotAt(
        ItemSlot observedSlot, Vector2 position,
        out ItemData box, out int index)
    {
        box = ObservationSelection.GetExposedBox(observedSlot);
        index = -1;
        if (box == null || box.Frames == null ||
            (box.TopSlot != null && !box.TopSlot.IsEmpty) ||
            presenter == null || presenter.CurrentView == null)
            return false;

        foreach (var part in presenter.CurrentView.Parts)
        {
            if (!ReferenceEquals(part.Part, box))
                continue;
            var layout = part.GetComponent<ObservationFrameSlotLayout>();
            if (layout == null || !layout.isActiveAndEnabled)
                return false;
            index = layout.GetSlotAtScreenPoint(position, UICamera);
            return index >= 0 && index < box.Frames.Capacity;
        }
        return false;
    }

    private void CaptureFrameDrag(Vector2 position)
    {
        ItemSlot observedSlot = observation != null
            ? observation.ObservedSlot : null;
        if (Selection.Kind != ObservationSelectionKind.FrameSlot ||
            !TryGetFrameSlotAt(observedSlot, position, out var box, out int index) ||
            !ReferenceEquals(Selection.Slot, observedSlot) ||
            !ReferenceEquals(Selection.Part, box) ||
            Selection.FrameSlotIndex != index)
            return;

        ItemSlot source = box.Frames.GetSlot(index);
        ItemData frame = source?.Item;
        if (frame == null || frame.Kind != ItemKind.Frame)
            return;

        dragObservedSlot = observedSlot;
        dragObservedItem = observedSlot.Value;
        dragBox = box;
        dragSourceIndex = index;
        dragSourceSlot = source;
        dragFrame = frame;
    }

    private bool IsFrameDragValid()
    {
        return dragBox != null && observation != null &&
            ReferenceEquals(observation.ObservedSlot, dragObservedSlot) &&
            ReferenceEquals(dragObservedSlot.Value, dragObservedItem) &&
            ReferenceEquals(ObservationSelection.GetExposedBox(dragObservedSlot), dragBox) &&
            (dragBox.TopSlot == null || dragBox.TopSlot.IsEmpty) &&
            dragBox.Frames != null &&
            dragSourceIndex >= 0 && dragSourceIndex < dragBox.Frames.Capacity &&
            ReferenceEquals(dragBox.Frames.GetSlot(dragSourceIndex), dragSourceSlot) &&
            ReferenceEquals(dragSourceSlot.Value, dragFrame) &&
            Selection.Kind == ObservationSelectionKind.FrameSlot &&
            ReferenceEquals(Selection.Slot, dragObservedSlot) &&
            ReferenceEquals(Selection.Part, dragBox) &&
            Selection.FrameSlotIndex == dragSourceIndex;
    }

    private void CompleteFrameDrag(Vector2 position)
    {
        if (!IsFrameDragValid() ||
            !TryGetFrameSlotAt(dragObservedSlot, position, out var box, out int index) ||
            !ReferenceEquals(box, dragBox) || index == dragSourceIndex)
            return;

        ItemSlot target = box.Frames.GetSlot(index);
        if (target == null || !target.IsEmpty)
            return;

        // 이동 이벤트가 동기적으로 선택/입력 상태를 갱신할 수 있으므로 참조 보관.
        ItemSlot observedSlot = dragObservedSlot;
        int sourceIndex = dragSourceIndex;
        if (ObservationFrameMoveAction.TryMove(observedSlot, box, sourceIndex, index))
        {
            Selection.SelectFrameSlot(observedSlot, box, index);
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
            if (Selection.Kind == ObservationSelectionKind.Ground)
                return GroundImage;

            if (Selection.Kind ==
                ObservationSelectionKind.TopAttachment)
            {
                return GetTopAttachmentImage();
            }

            if (Selection.Kind !=
                    ObservationSelectionKind.EquipmentPart ||
                presenter == null ||
                presenter.CurrentView == null)
                return null;

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
            return null;

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
                return null;

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

        ObservationSelectionKind previousKind = Selection.Kind;
        ItemData previousPart = Selection.Part;
        int previousFrameIndex = Selection.FrameSlotIndex;

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
                inputArea, position, UICamera) ||
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

        // 2. 소비장 슬롯 선택 (동일 슬롯 재클릭 시 해제)
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
                    continue;

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
            // 빈 공간 클릭 시 기존 선택 해제
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
            return false;

        ItemData exposedBox =
            ObservationSelection.GetExposedBox(observedSlot);

        if (exposedBox == null ||
            exposedBox.TopSlot == null ||
            exposedBox.TopSlot.IsEmpty)
            return false;

        foreach (var part in presenter.CurrentView.Parts)
        {
            if (!ReferenceEquals(part.Part, exposedBox))
                continue;

            ObservationTopAttachmentView topView =
                part.GetComponent<ObservationTopAttachmentView>();

            if (topView == null ||
                !topView.isActiveAndEnabled ||
                topView.CurrentView == null)
                return false;

            Image image = topView.CurrentView.Image;

            if (image == null ||
                !image.gameObject.activeInHierarchy)
                return false;

            if (!RectTransformUtility.RectangleContainsScreenPoint(
                    image.rectTransform,
                    screenPosition,
                    UICamera))
                return false;

            // 동일 격왕판을 다시 클릭하면 선택 해제
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
            return false;

        ItemData exposedBox =
            ObservationSelection.GetExposedBox(observedSlot);

        if (exposedBox == null)
            return false;

        // 격왕판 등 상단 장비가 설치되어 있으면
        // 아래 소비장 슬롯 선택을 차단
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
                return false;

            int slotIndex =
                layout.GetSlotAtScreenPoint(
                    screenPosition,
                    UICamera);

            if (slotIndex < 0 ||
                slotIndex >= exposedBox.Frames.Capacity)
                return false;

            // 같은 소비장 슬롯을 다시 클릭하면 선택 해제
            bool alreadySelected =
                Selection.Kind ==
                    ObservationSelectionKind.FrameSlot &&
                ReferenceEquals(Selection.Slot, observedSlot) &&
                ReferenceEquals(Selection.Part, exposedBox) &&
                Selection.FrameSlotIndex == slotIndex;

            if (alreadySelected)
            {
                Selection.Clear();
                return true;
            }

            // 다른 슬롯이면 기존 선택을 교체
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

    public bool IsBlocked(Vector2 position)
    {
        if (ObservationInputGuard.IsOverUI(position))
            return true;

        if (hand == null)
            hand = FindAnyObjectByType<HandItemPresenter>();

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

            if (useTouch &&
                screen.primaryTouch.phase.ReadValue() ==
                    UnityEngine.InputSystem.TouchPhase.Canceled)
            {
                rejected = true;
            }
        }

        if (!tracking)
            return;

        rejected |= IsBlocked(position) ||
            inputArea == null ||
            !RectTransformUtility.RectangleContainsScreenPoint(
                inputArea, position, UICamera);

        if (useTouch &&
            (screen.primaryTouch.phase.ReadValue() ==
                UnityEngine.InputSystem.TouchPhase.Canceled ||
             screen.primaryTouch.touchId.ReadValue() != touchId))
            rejected = true;

        if (Keyboard.current != null &&
            Keyboard.current.escapeKey.wasPressedThisFrame)
            rejected = true;

        if (dragBox != null && !IsFrameDragValid())
            rejected = true;

        Vector2 delta = position - start;
        if (delta.sqrMagnitude > DragThreshold * DragThreshold)
        {
            // 수직 방향으로 임계 거리를 넘으면 이 제스처 전체를 취소한다.
            bool horizontal = Mathf.Abs(delta.x) > Mathf.Abs(delta.y);
            if (!horizontal || dragBox == null)
                rejected = true;
            else if (!rejected)
                frameDragging = true;
        }

        if (up)
        {
            bool click = !rejected && !frameDragging;
            if (!rejected && frameDragging &&
                Mathf.Abs(delta.x) > DragThreshold &&
                Mathf.Abs(delta.x) > Mathf.Abs(delta.y))
                CompleteFrameDrag(position);

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
