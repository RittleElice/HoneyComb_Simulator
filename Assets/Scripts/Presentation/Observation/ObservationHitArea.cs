
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Image))]
public class ObservationHitArea : MonoBehaviour
{
    [Header("Selection Polygon")]
    [SerializeField]
    private Vector2[] polygon =
    {
        Vector2.zero,
        Vector2.right,
        Vector2.one,
        Vector2.up
    };

    [Header("Alpha Hit Test")]
    [SerializeField, Range(0f, 1f)]
    private float alphaThreshold = 0.1f;

    [SerializeField]
    private bool useAlphaHitTest = true;

    public Image Image => GetComponent<Image>();

    // Optional authored restriction; off by default so replacement artwork is not clipped by an old polygon.
    [SerializeField] private bool restrictAlphaToPolygon;

    public bool Contains(Vector2 screenPoint, Camera camera)
    {
        if (!isActiveAndEnabled ||
            !SpriteImageHitTest.TryGetNormalizedPoint(Image, screenPoint, camera, out Vector2 normalized))
            return false;
        if (!useAlphaHitTest) return ContainsNormalized(normalized);
        if (restrictAlphaToPolygon && !ContainsNormalized(normalized)) return false;
        return SpriteImageHitTest.ContainsNormalized(Image, normalized, alphaThreshold);
    }
    public bool ContainsNormalized(Vector2 point)
    {
        if (polygon == null || polygon.Length < 3)
            return false;

        bool inside = false;

        for (int i = 0, j = polygon.Length - 1;
             i < polygon.Length;
             j = i++)
        {
            Vector2 a = polygon[j];
            Vector2 b = polygon[i];

            Vector2 edge = b - a;
            Vector2 delta = point - a;

            float cross =
                edge.x * delta.y -
                edge.y * delta.x;

            if (Mathf.Abs(cross) < 0.00001f &&
                Vector2.Dot(delta, edge) >= 0f &&
                Vector2.Dot(delta, edge) <=
                edge.sqrMagnitude)
                return true;

            if ((a.y > point.y) != (b.y > point.y) &&
                point.x <
                (b.x - a.x) *
                (point.y - a.y) /
                (b.y - a.y) + a.x)
            {
                inside = !inside;
            }
        }

        return inside;
    }
}
