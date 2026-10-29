
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

            if (Selection.Kind != ObservationSelectionKind.EquipmentPart ||
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

        Selection.Revalidate(slot);

        bool selectionChanged =
            previousKind != Selection.Kind ||
            !ReferenceEquals(previousPart, Selection.Part) ||
            previousFrameIndex != Selection.FrameSlotIndex;

        if (changed || selectionChanged)
        {
            tracking = false;

            lastSlot = slot;
            lastItem = item;
            lastView = view;

            SelectionChanged?.Invoke();
        }
    }

    public void ClearSelection()
    {
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

        // 1. 최상단에 노출된 상자의 소비장 슬롯 검사
        if (TrySelectFrameSlot(slot, position))
        {
            SelectionChanged?.Invoke();
            return;
        }

        // 2. 기존 장비 선택
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

                // 실제 렌더링 순서 우선
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

    private bool TrySelectFrameSlot(
        ItemSlot observedSlot,
        Vector2 screenPosition)
    {
        if (presenter == null ||
            presenter.CurrentView == null)
            return false;

        // 데이터 기준 최상단 노출 상자
        ItemData exposedBox =
            ObservationSelection.GetExposedBox(observedSlot);

        if (exposedBox == null)
            return false;

        // 현재 화면에서 해당 상자의 실제 View 탐색
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
                tracking = false;
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
                tracking = false;
                return;
            }

            position = Mouse.current.position.ReadValue();

            down = Mouse.current.leftButton.wasPressedThisFrame;
            up = Mouse.current.leftButton.wasReleasedThisFrame;
            held = Mouse.current.leftButton.isPressed;
        }

        if (down && !tracking)
        {
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
        }

        if (!tracking)
            return;

        rejected |=
            Vector2.Distance(start, position) > DragThreshold ||
            IsBlocked(position);

        if (up)
        {
            tracking = false;

            if (!rejected)
                SelectAt(position);
        }
        else if (!held)
        {
            tracking = false;
        }
    }

    private void OnApplicationFocus(bool focused)
    {
        if (!focused)
            tracking = false;
    }

    private void OnDisable()
    {
        tracking = false;

        ClearSelection();

        lastSlot = null;
        lastView = null;
        lastItem = null;
    }
}
