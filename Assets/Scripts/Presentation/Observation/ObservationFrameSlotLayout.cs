
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(RectTransform))]
public class ObservationFrameSlotLayout : MonoBehaviour
{
    [Header("Top Surface Corners - Normalized")]
    [SerializeField] private Vector2 nearLeft =
        new Vector2(0f, 0.58f);

    [SerializeField] private Vector2 nearRight =
        new Vector2(0.68f, 0.58f);

    [SerializeField] private Vector2 farLeft =
        new Vector2(0.32f, 1f);

    [SerializeField] private Vector2 farRight =
        new Vector2(1f, 1f);

    [Header("Slots")]
    [SerializeField, Range(1, 10)]
    private int slotCount = 10;

    public int SlotCount => slotCount;

    private RectTransform RectTransform =>
        (RectTransform)transform;

    
    public Vector2[] GetSlotPolygon(int index)
    {
        if (index < 0 || index >= slotCount)
            return null;

        float t0 = (float)index / slotCount;
        float t1 = (float)(index + 1) / slotCount;

        // 앞쪽 변을 좌우로 분할
        Vector2 near0 = Vector2.Lerp(nearLeft, nearRight, t0);
        Vector2 near1 = Vector2.Lerp(nearLeft, nearRight, t1);

        // 뒤쪽 변도 같은 비율로 분할
        Vector2 far0 = Vector2.Lerp(farLeft, farRight, t0);
        Vector2 far1 = Vector2.Lerp(farLeft, farRight, t1);

        return new[]
        {
            near0,
            near1,
            far1,
            far0
        };
    }


    public Vector2 GetSlotCenter(int index)
    {
        Vector2[] polygon = GetSlotPolygon(index);

        if (polygon == null)
            return Vector2.zero;

        Vector2 sum = Vector2.zero;

        foreach (Vector2 point in polygon)
            sum += point;

        return sum / polygon.Length;
    }

    public Vector3 NormalizedToWorld(Vector2 normalized)
    {
        Rect rect = RectTransform.rect;

        Vector3 local = new Vector3(
            rect.xMin + normalized.x * rect.width,
            rect.yMin + normalized.y * rect.height,
            0f);

        return RectTransform.TransformPoint(local);
    }

    public Vector3 GetSlotCenterWorld(int index)
    {
        return NormalizedToWorld(GetSlotCenter(index));
    }

    public Vector3[] GetSlotWorldPolygon(int index)
    {
        Vector2[] polygon = GetSlotPolygon(index);

        if (polygon == null)
            return null;

        Vector3[] result = new Vector3[polygon.Length];

        for (int i = 0; i < polygon.Length; i++)
            result[i] = NormalizedToWorld(polygon[i]);

        return result;
    }

    public int GetSlotAtScreenPoint(
        Vector2 screenPoint,
        Camera camera)
    {
        if (!RectTransformUtility
            .ScreenPointToLocalPointInRectangle(
                RectTransform,
                screenPoint,
                camera,
                out Vector2 local))
        {
            return -1;
        }

        Rect rect = RectTransform.rect;

        if (rect.width <= 0f || rect.height <= 0f)
            return -1;

        Vector2 normalized = new Vector2(
            (local.x - rect.xMin) / rect.width,
            (local.y - rect.yMin) / rect.height);

        // 가까운 슬롯부터 검사
        for (int i = 0; i < slotCount; i++)
        {
            if (ContainsPolygon(
                    GetSlotPolygon(i),
                    normalized))
            {
                return i;
            }
        }

        return -1;
    }

    private static bool ContainsPolygon(
        Vector2[] polygon,
        Vector2 point)
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

    private void OnValidate()
    {
        slotCount = Mathf.Clamp(slotCount, 1, 10);
    }
}
