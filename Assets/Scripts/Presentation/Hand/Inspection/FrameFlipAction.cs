
using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(HandItemPresenter))]
public class FrameFlipAction : MonoBehaviour
{
    [Header("References")]
    [SerializeField]
    private HandItemPresenter handPresenter;

    [Header("Flip Settings")]
    [SerializeField, Min(1f)]
    private float dragDistance = 300f;

    [SerializeField, Min(0f)]
    private float snapDuration = 0.25f;

    private RectTransform visual;
    private Coroutine snapRoutine;

    private MovableItem currentItem;
    private FrameData currentFrame;

    private float angle;
    private float startAngle;
    private float startPointerX;

    private bool dragging;
    private Vector3 baseScale;

    public float Angle => angle;
    public bool IsDragging => dragging;

    public FrameSide CurrentSide =>
        currentFrame != null
            ? currentFrame.FacingSide
            : FrameSide.Front;

    private void Awake()
    {
        if (handPresenter == null)
            handPresenter = GetComponent<HandItemPresenter>();
    }

    public void BeginDrag(Vector2 position)
    {
        if (!isActiveAndEnabled ||
            handPresenter == null ||
            !handPresenter.IsAtInspectionHeight ||
            !handPresenter.IsFrameHeld)
            return;

        RectTransform nextVisual =
            handPresenter.InspectionVisual;

        if (nextVisual == null)
            return;

        ItemData item =
            handPresenter.CurrentItem as ItemData;

        if (item == null ||
            item.Kind != ItemKind.Frame)
            return;

        FrameData frame =
            item.GetContent<FrameData>();

        if (frame == null)
            return;

        StopSnap();

        if (!ReferenceEquals(currentItem, item) ||
            visual != nextVisual)
        {
            currentItem = item;
            currentFrame = frame;
            visual = nextVisual;

            baseScale = visual.localScale;

            angle = frame.FacingSide == FrameSide.Front
                ? 0f
                : 180f;

            ApplyAngle();
        }

        startAngle = angle;
        startPointerX = position.x;
        dragging = true;
    }

    public void UpdateDrag(Vector2 position)
    {
        if (!dragging || visual == null)
            return;

        float deltaX =
            position.x - startPointerX;

        angle = startAngle +
            Mathf.Clamp(
                deltaX / Mathf.Max(1f, dragDistance),
                -1f,
                1f) * 180f;

        ApplyAngle();
    }

    public void EndDrag()
    {
        if (!dragging)
            return;

        dragging = false;

        float target =
            Mathf.Round(angle / 180f) * 180f;

        StopSnap();

        if (snapDuration <= 0f)
        {
            angle = target;
            ApplyAngle();
            CommitSide();
            return;
        }

        snapRoutine =
            StartCoroutine(SnapTo(target));
    }

    public void CancelDrag()
    {
        dragging = false;
        StopSnap();

        if (currentFrame != null)
        {
            angle =
                currentFrame.FacingSide == FrameSide.Front
                    ? 0f
                    : 180f;
        }

        ApplyAngle();
    }

    private IEnumerator SnapTo(float target)
    {
        float start = angle;
        float elapsed = 0f;

        while (elapsed < snapDuration)
        {
            elapsed += Time.unscaledDeltaTime;

            float t = Mathf.Clamp01(
                elapsed / snapDuration);

            t = t * t * (3f - 2f * t);

            angle = Mathf.Lerp(start, target, t);
            ApplyAngle();

            yield return null;
        }

        angle = target;
        ApplyAngle();

        CommitSide();

        snapRoutine = null;
    }

    private void CommitSide()
    {
        if (currentFrame == null)
            return;

        // 180도마다 앞면/뒷면 교대
        int turns =
            Mathf.RoundToInt(angle / 180f);

        FrameSide side =
            Mathf.Abs(turns % 2) == 0
                ? FrameSide.Front
                : FrameSide.Back;

        currentFrame.SetFacingSide(side);
    }

    private void ApplyAngle()
    {
        if (visual == null)
            return;

        float widthFactor =
            Mathf.Cos(angle * Mathf.Deg2Rad);

        visual.localScale = new Vector3(
            baseScale.x * widthFactor,
            baseScale.y,
            baseScale.z);
    }

    private void StopSnap()
    {
        if (snapRoutine == null)
            return;

        StopCoroutine(snapRoutine);
        snapRoutine = null;
    }

    private void OnDisable()
    {
        CancelDrag();
    }
}
