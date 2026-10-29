using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(HandLowerAction), typeof(FrameFlipAction))]
public class HandInspectionController : MonoBehaviour
{
    [SerializeField] private HandItemPresenter handPresenter;
    [SerializeField] private FrameFlipAction frameFlip;
    [SerializeField] private HandLowerAction handLower;
    private enum Action { None, Lower, Flip }
    private Action active;
    public bool IsDragging => active != Action.None;
    private void Awake()
    {
        if (handPresenter == null) handPresenter = GetComponent<HandItemPresenter>();
        if (frameFlip == null) frameFlip = GetComponent<FrameFlipAction>();
        if (handLower == null) handLower = GetComponent<HandLowerAction>();
    }
    // Positions are Canvas-local, supplied only by HandGestureController.
    public bool BeginDrag(bool horizontal, Vector2 origin)
    {
        if (!isActiveAndEnabled || IsDragging || handPresenter == null ||
            !handPresenter.IsFrameHeld || !handPresenter.IsAtInspectionHeight) return false;
        if (horizontal)
        {
            if (frameFlip == null || !frameFlip.isActiveAndEnabled) return false;
            frameFlip.BeginDrag(origin);
            if (!frameFlip.IsDragging) return false;
            active = Action.Flip;
        }
        else
        {
            if (handLower == null || !handLower.BeginDrag(origin)) return false;
            active = Action.Lower;
        }
        return true;
    }
    public void UpdateDrag(Vector2 local)
    {
        if (active == Action.Flip) frameFlip.UpdateDrag(local);
        else if (active == Action.Lower) handLower.UpdateDrag(local);
    }
    public void EndDrag()
    {
        var previous = active;
        active = Action.None;
        if (previous == Action.Flip) frameFlip.EndDrag();
        else if (previous == Action.Lower) handLower.EndDrag();
    }
    public void CancelDrag()
    {
        active = Action.None;
        if (frameFlip != null) frameFlip.CancelDrag();
        if (handLower != null) handLower.CancelDrag();
    }
    private void OnDisable() => CancelDrag();
}
