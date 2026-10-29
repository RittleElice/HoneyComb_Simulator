using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent, DefaultExecutionOrder(-100)]
[RequireComponent(typeof(HandItemPresenter), typeof(HandRaiseController), typeof(HandInspectionController))]
public class HandGestureController : MonoBehaviour
{
    [SerializeField, Min(1f)] private float gestureThreshold = 15f;
    private HandItemPresenter presenter;
    private HandRaiseController raise;
    private HandInspectionController inspection;
    private enum Route { Pending, Raise, Inspect, Ignore }
    private Route route;
    private Vector2 origin;
    private MovableItem item;
    private RectTransform visual;
    private bool captured;
    private int releasedFrame = -1;
    private TouchControl touch;
    private int touchId;
    private Mouse mouse;
    public bool IsDragging => captured;
    public bool BlocksObservationInput => captured || releasedFrame == Time.frameCount;

    private void Awake()
    {
        presenter = GetComponent<HandItemPresenter>();
        raise = GetComponent<HandRaiseController>();
        inspection = GetComponent<HandInspectionController>();
    }
    private void OnEnable() => presenter.VisualInvalidated += CancelDrag;
    private void OnDisable()
    {
        presenter.VisualInvalidated -= CancelDrag;
        CancelDrag();
    }
    private void OnApplicationFocus(bool focused) { if (!focused) CancelDrag(); }
    private void OnApplicationPause(bool paused) { if (paused) CancelDrag(); }
    private bool CanInteract => presenter.isActiveAndEnabled && presenter.IsFrameHeld &&
        presenter.InspectionVisual != null && SceneManager.GetActiveScene().name == "Observation";

    private void Update()
    {
        if (!CanInteract) { CancelDrag(); return; }
        if (captured)
        {
            if (!ReferenceEquals(item, presenter.CurrentItem) || visual != presenter.InspectionVisual ||
                (route == Route.Raise && !raise.isActiveAndEnabled) ||
                (route == Route.Inspect && !inspection.isActiveAndEnabled))
            { CancelDrag(); return; }
            if (touch != null)
            {
                if (!touch.device.added || touch.phase.ReadValue() == UnityEngine.InputSystem.TouchPhase.Canceled ||
                    (touch.press.isPressed && touch.touchId.ReadValue() != touchId))
                { CancelDrag(); return; }
                Move(touch.position.ReadValue());
                if (!touch.press.isPressed) EndDrag();
            }
            else if (mouse != null && mouse.added)
            {
                Move(mouse.position.ReadValue());
                if (!mouse.leftButton.isPressed) EndDrag();
            }
            else CancelDrag();
            return;
        }
        if (presenter.IsTransitioning || presenter.State == HandItemPresenter.HandDisplayState.Pickup ||
            presenter.IsInspectionInputCaptured) return;
        var screen = Touchscreen.current;
        if (screen != null)
        {
            bool anyTouch = false;
            foreach (var candidate in screen.touches)
            {
                anyTouch |= candidate.press.isPressed || candidate.press.wasReleasedThisFrame;
                if (candidate.press.wasPressedThisFrame && Begin(candidate.position.ReadValue()))
                { touch = candidate; touchId = candidate.touchId.ReadValue(); return; }
            }
            if (anyTouch) return;
        }
        var candidateMouse = Mouse.current;
        if (candidateMouse != null && candidateMouse.leftButton.wasPressedThisFrame &&
            Begin(candidateMouse.position.ReadValue())) mouse = candidateMouse;
    }
    private bool Begin(Vector2 screen)
    {
        if (ObservationInputGuard.IsOverUI(screen) || !presenter.IsPointerOnHand(screen) ||
            !presenter.TryScreenToLocal(screen, out origin)) return false;
        item = presenter.CurrentItem;
        visual = presenter.InspectionVisual;
        route = Route.Pending;
        captured = true;
        presenter.SetInspectionInputCaptured(true);
        return true;
    }
    private void Move(Vector2 screen)
    {
        if (!presenter.TryScreenToLocal(screen, out var local)) { CancelDrag(); return; }
        var delta = local - origin;
        if (route == Route.Pending)
        {
            if (delta.magnitude < gestureThreshold) return;
            bool horizontal = Mathf.Abs(delta.x) > Mathf.Abs(delta.y);
            route = Route.Ignore; // Direction and recipient remain locked until release.
            if (presenter.State == HandItemPresenter.HandDisplayState.Lowered)
            {
                if (!horizontal && delta.y > 0f && raise.BeginDrag(origin)) route = Route.Raise;
            }
            else if (horizontal || delta.y < 0f)
            {
                if (inspection.BeginDrag(horizontal, origin)) route = Route.Inspect;
            }
        }
        if (route == Route.Raise) raise.UpdateDrag(local);
        else if (route == Route.Inspect) inspection.UpdateDrag(local);
    }
    private void EndDrag()
    {
        if (!captured) return;
        if (route == Route.Raise) raise.EndDrag();
        else if (route == Route.Inspect) inspection.EndDrag();
        Release();
    }
    public void CancelDrag()
    {
        if (raise != null) raise.CancelDrag();
        if (inspection != null) inspection.CancelDrag();
        Release();
    }
    private void Release()
    {
        if (captured) releasedFrame = Time.frameCount;
        captured = false;
        touch = null;
        mouse = null;
        item = null;
        visual = null;
        route = Route.Pending;
        if (presenter != null) presenter.SetInspectionInputCaptured(false);
    }
}
