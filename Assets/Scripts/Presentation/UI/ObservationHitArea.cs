using UnityEngine;
using UnityEngine.UI;

// Normalized coordinates on the displayed sprite, independent of its pixel size.
[RequireComponent(typeof(Image))]
public class ObservationHitArea : MonoBehaviour
{
    [SerializeField] private Vector2[] polygon = { Vector2.zero, Vector2.right, Vector2.one, Vector2.up };
    public Image Image => GetComponent<Image>();

    public bool Contains(Vector2 screenPoint, Camera camera)
    {
        Image image = Image;
        if (!isActiveAndEnabled || !image.isActiveAndEnabled || image.sprite == null || image.color.a <= 0f) return false;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(image.rectTransform, screenPoint, camera, out Vector2 local)) return false;
        Rect rect = image.rectTransform.rect;
        if (image.preserveAspect && rect.width > 0f && rect.height > 0f)
        {
            float ratio = image.sprite.rect.width / image.sprite.rect.height;
            float width = Mathf.Min(rect.width, rect.height * ratio);
            float height = width / ratio;
            rect = new Rect(rect.xMin + (rect.width-width)*image.rectTransform.pivot.x,
                rect.yMin + (rect.height-height)*image.rectTransform.pivot.y, width, height);
        }
        if (rect.width <= 0f || rect.height <= 0f) return false;
        return ContainsNormalized(new Vector2((local.x-rect.xMin)/rect.width, (local.y-rect.yMin)/rect.height));
    }

    public bool ContainsNormalized(Vector2 point)
    {
        if (polygon == null || polygon.Length < 3) return false;
        bool inside = false;
        for (int i = 0, j = polygon.Length - 1; i < polygon.Length; j = i++)
        {
            Vector2 a = polygon[j], b = polygon[i];
            Vector2 edge = b-a, delta = point-a;
            float cross = edge.x*delta.y-edge.y*delta.x;
            if (Mathf.Abs(cross) < .00001f && Vector2.Dot(delta, edge) >= 0f && Vector2.Dot(delta, edge) <= edge.sqrMagnitude) return true;
            if ((a.y > point.y) != (b.y > point.y) &&
                point.x < (b.x-a.x)*(point.y-a.y)/(b.y-a.y)+a.x) inside = !inside;
        }
        return inside;
    }
}
