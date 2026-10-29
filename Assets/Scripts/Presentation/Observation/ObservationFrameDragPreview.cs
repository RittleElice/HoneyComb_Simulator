using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class ObservationFrameDragPreview : MonoBehaviour
{
    [Header("Origin Ghost")]
    [SerializeField] private bool showOriginGhost = true;
    [SerializeField, Range(0f, 1f)] private float originGhostOpacity = 0.3f;

    private RectTransform draggedRect;
    private GameObject ghostObject;
    private Vector3 originalWorldPosition;
    private bool dragging;

    public bool IsDragging => dragging;

    // Keep existing call signature for compatibility with the input controller.
    public void Begin(RectTransform visual, Vector2 pointerPosition, Camera camera)
    {
        Cancel();
        if (visual == null) return;

        draggedRect = visual;
        originalWorldPosition = visual.position;
        dragging = true;

        if (showOriginGhost) CreateGhost();
    }

    public void SnapTo(Vector3 targetWorldPosition, Vector3 sourceWorldPosition)
    {
        if (!dragging || draggedRect == null) return;
        draggedRect.position = originalWorldPosition +
                               (targetWorldPosition - sourceWorldPosition);
    }

    public void End() => Cancel();

    public void Cancel()
    {
        if (draggedRect != null)
            draggedRect.position = originalWorldPosition;

        ReleaseGhost();
        draggedRect = null;
        dragging = false;
    }

    private void CreateGhost()
    {
        UIItemPartView partView = draggedRect.GetComponent<UIItemPartView>();
        Image source = partView != null ? partView.Image : draggedRect.GetComponent<Image>();
        if (source == null || source.sprite == null) return;

        RectTransform sourceRect = source.rectTransform;
        RectTransform parent = draggedRect.parent as RectTransform;
        if (parent == null) return;

        ghostObject = new GameObject("FrameDragOriginGhost",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        RectTransform ghost = ghostObject.GetComponent<RectTransform>();
        ghost.SetParent(parent, false); // sibling of dragged object, not its child
        ghost.anchorMin = ghost.anchorMax = new Vector2(0.5f, 0.5f);
        ghost.pivot = sourceRect.pivot;
        ghost.sizeDelta = sourceRect.rect.size;
        ghost.position = sourceRect.position;
        ghost.rotation = sourceRect.rotation;

        Vector3 sourceScale = sourceRect.lossyScale;
        Vector3 parentScale = parent.lossyScale;
        ghost.localScale = new Vector3(
            sourceScale.x / Mathf.Max(0.0001f, Mathf.Abs(parentScale.x)),
            sourceScale.y / Mathf.Max(0.0001f, Mathf.Abs(parentScale.y)),
            1f);

        Image image = ghostObject.GetComponent<Image>();
        image.sprite = source.sprite;
        image.type = source.type;
        image.preserveAspect = source.preserveAspect;
        image.material = source.material;
        image.fillAmount = source.fillAmount;
        image.fillMethod = source.fillMethod;
        image.fillOrigin = source.fillOrigin;
        image.fillClockwise = source.fillClockwise;
        Color color = source.color;
        color.a *= originGhostOpacity;
        image.color = color;
        image.raycastTarget = false;
        ghost.SetSiblingIndex(draggedRect.GetSiblingIndex());
    }

    private void ReleaseGhost()
    {
        if (ghostObject == null) return;
        ghostObject.SetActive(false);
        if (Application.isPlaying) Destroy(ghostObject);
        else DestroyImmediate(ghostObject);
        ghostObject = null;
    }

    private void OnDisable() => Cancel();
}
