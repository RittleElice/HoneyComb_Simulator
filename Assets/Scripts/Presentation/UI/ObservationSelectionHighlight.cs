using UnityEngine;
using UnityEngine.UI;

[DefaultExecutionOrder(110)]
public class ObservationSelectionHighlight : MonoBehaviour
{
    [SerializeField] private ObservationSelectionController selection;
    [SerializeField] private Color color = new Color(1f,.8f,.1f,1f);
    [SerializeField] private Vector2 width = new Vector2(4f,4f);
    private Image target;
    private Outline outline;
    private void LateUpdate()
    {
        Image next = selection != null ? selection.SelectedImage : null;
        if (next == target && outline != null) return;
        Clear(); target = next;
        if (target == null) return;
        outline = target.gameObject.AddComponent<Outline>();
        outline.effectColor = color; outline.effectDistance = width;
        outline.useGraphicAlpha = true;
    }
    private void Clear()
    {
        if (outline != null)
        {
            outline.enabled = false;
            if (Application.isPlaying) Destroy(outline); else DestroyImmediate(outline);
        }
        outline = null; target = null;
    }
    private void OnDisable() => Clear();
}
