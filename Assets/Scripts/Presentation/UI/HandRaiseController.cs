using UnityEngine;

[DisallowMultipleComponent, RequireComponent(typeof(HandItemPresenter))]
public class HandRaiseController : MonoBehaviour
{
    private HandItemPresenter presenter;
    private float startY, startProgress;
    public bool IsDragging { get; private set; }
    private void Awake() => presenter = GetComponent<HandItemPresenter>();
    public bool BeginDrag(Vector2 local)
    {
        if (!isActiveAndEnabled || !presenter.IsFrameHeld || presenter.IsTransitioning ||
            presenter.State != HandItemPresenter.HandDisplayState.Lowered) return false;
        startY = local.y;
        startProgress = presenter.HeightProgress;
        return IsDragging = true;
    }
    public void UpdateDrag(Vector2 local)
    {
        if (IsDragging && Mathf.Abs(presenter.HeightRange) > .001f)
            presenter.SetHeightProgress(startProgress + (local.y - startY) / presenter.HeightRange);
    }
    public void EndDrag()
    {
        if (!IsDragging) return;
        IsDragging = false;
        if (presenter.HeightProgress >= .5f) presenter.AnimateToInspecting();
        else presenter.AnimateToLowered();
    }
    public void CancelDrag()
    {
        if (!IsDragging) return;
        IsDragging = false;
        presenter.AnimateToLowered();
    }
    private void OnDisable() => CancelDrag();
}
