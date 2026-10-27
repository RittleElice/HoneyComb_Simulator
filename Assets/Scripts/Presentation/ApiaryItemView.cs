
using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(100)]
public class ApiaryItemView : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private ApiaryGridView gridView;
    [SerializeField] private ItemPrefabRegistry prefabRegistry;

    [Header("Fallback Equipment Prefabs")]
    [SerializeField] private GameObject broodBoxPrefab;
    [SerializeField] private GameObject superPrefab;
    [SerializeField] private GameObject lidPrefab;
    [SerializeField] private GameObject framePrefab;
    [SerializeField] private GameObject genericPrefab;

    [Header("Sorting")]
    [SerializeField] private int sortingOrder = 10;

    private sealed class PartVisual
    {
        public GameObject Object;
        public HiveView View;
        public SpriteRenderer Renderer;

        public int GridX;
        public int GridY;
    }

    private struct StackLink
    {
        public SpriteRenderer Lower;
        public SpriteRenderer Upper;
    }

    private readonly Dictionary<ItemData, PartVisual> partViews =
        new Dictionary<ItemData, PartVisual>();

    private readonly Dictionary<ItemSlot, MovableItem> cachedItems =
        new Dictionary<ItemSlot, MovableItem>();

    private readonly HashSet<ItemData> activeParts =
        new HashSet<ItemData>();

    private readonly List<StackLink> stackLinks =
        new List<StackLink>();

    private ApiaryGrid grid;
    private Transform visualRoot;

    private void Start()
    {
        if (gridView == null)
            gridView = GetComponent<ApiaryGridView>();

        if (prefabRegistry == null)
            prefabRegistry =
                FindAnyObjectByType<ItemPrefabRegistry>();

        GameBootstrap bootstrap = GameBootstrap.Instance;

        if (gridView == null ||
            bootstrap == null ||
            bootstrap.GameWorld == null)
        {
            Debug.LogError(
                "[ApiaryItemView] GridView 또는 GameWorld가 없습니다.");

            enabled = false;
            return;
        }

        grid = bootstrap.GameWorld.Apiary.Grid;

        GameObject root = new GameObject("EquipmentVisuals");
        visualRoot = root.transform;

        Refresh();
    }

    private void Update()
    {
        if (grid == null)
            return;

        if (HasChanged())
            Refresh();
    }


    private bool HasChanged()
    {
        if (grid == null)
            return false;

        int count = 0;

        for (int x = 0; x < grid.Width; x++)
        {
            for (int y = 0; y < grid.Height; y++)
            {
                ItemSlot slot = grid.GetItemSlot(x, y);

                // 잠긴 격자는 슬롯이 없으므로 건너뛴다.
                if (slot == null)
                    continue;

                count++;

                if (!cachedItems.TryGetValue(
                        slot, out MovableItem previous))
                {
                    return true;
                }

                if (!ReferenceEquals(previous, slot.Value))
                {
                    return true;
                }
            }
        }

        return count != cachedItems.Count;
    }


    
    public void Refresh()
    {
        if (grid == null)
            return;

        activeParts.Clear();
        stackLinks.Clear();
        cachedItems.Clear();

        for (int x = 0; x < grid.Width; x++)
        {
            for (int y = 0; y < grid.Height; y++)
            {
                ItemSlot slot = grid.GetItemSlot(x, y);

                // 잠긴 격자는 처리하지 않는다.
                if (slot == null)
                    continue;

                cachedItems[slot] = slot.Value;

                if (!slot.IsEmpty)
                {
                    DrawItem(x, y, slot.Value);
                }
            }
        }

        // 양봉장에서 제거된 장비의 이미지 비활성화
        foreach (var pair in partViews)
        {
            if (!activeParts.Contains(pair.Key) &&
                pair.Value.Object != null)
            {
                pair.Value.Object.SetActive(false);
            }
        }
    }


    private void DrawItem(int x, int y, MovableItem item)
    {
        SpriteRenderer previous = null;

        if (item is ItemAssembly assembly)
        {
            foreach (ItemData part in assembly.Parts)
            {
                SpriteRenderer current = DrawPart(x, y, part);

                if (previous != null && current != null)
                {
                    stackLinks.Add(new StackLink
                    {
                        Lower = previous,
                        Upper = current
                    });
                }

                previous = current;
            }
        }
        else if (item is ItemData single)
        {
            DrawPart(x, y, single);
        }
    }

    private SpriteRenderer DrawPart(int x, int y, ItemData item)
    {
        activeParts.Add(item);

        if (!partViews.TryGetValue(item, out PartVisual visual) ||
            visual.Object == null)
        {
            visual = CreatePartVisual(item);

            if (visual == null)
                return null;

            partViews[item] = visual;
        }

        visual.GridX = x;
        visual.GridY = y;

        visual.Object.SetActive(true);

        HiveData hive = item.GetContent<HiveData>();

        if (item.Kind == ItemKind.BroodBox && hive != null)
        {
            visual.View.Initialize(
                hive,
                grid.Width,
                gridView.CellSize
            );
        }

        // 실제 위치는 격자 슬롯 좌표를 사용
        visual.View.ApplyLayout(
            x,
            y,
            grid.Width,
            gridView.CellSize
        );

        ApplyGridSorting(visual.Renderer, y);

        return visual.Renderer;
    }

    
    private PartVisual CreatePartVisual(ItemData item)
    {
        GameObject prefab = GetPrefab(item.Kind);

        if (prefab == null)
        {
            Debug.LogWarning(
                $"[ApiaryItemView] {item.Kind} 프리팹이 없습니다.");
            return null;
        }

        GameObject obj = Instantiate(prefab, visualRoot);

        // Registry의 Apiary 스프라이트 적용
        // HiveView 초기화보다 먼저 실행해야 한다.
        if (prefabRegistry != null)
        {
            prefabRegistry.ApplySprite(
                obj,
                item.Kind,
                ItemViewContext.Apiary);
        }

        HiveView view = obj.GetComponent<HiveView>();
        SpriteRenderer renderer =
            obj.GetComponent<SpriteRenderer>();

        if (view == null ||
            renderer == null ||
            renderer.sprite == null ||
            renderer.sprite.bounds.size.x <= 0f ||
            renderer.sprite.bounds.size.y <= 0f)
        {
            Debug.LogWarning(
                $"[ApiaryItemView] {prefab.name}의 루트에 " +
                "유효한 HiveView와 SpriteRenderer가 필요합니다.");

            obj.SetActive(false);
            Destroy(obj);
            return null;
        }

        obj.name = item.Name;

        HiveData hive = item.GetContent<HiveData>();

        if (item.Kind == ItemKind.BroodBox && hive != null)
        {
            view.Initialize(
                hive,
                grid.Width,
                gridView.CellSize);
        }

        // 기존 Collider 비활성화 유지
        foreach (Collider collider in
                 obj.GetComponentsInChildren<Collider>(true))
        {
            collider.enabled = false;
        }

        foreach (Collider2D collider in
                 obj.GetComponentsInChildren<Collider2D>(true))
        {
            collider.enabled = false;
        }

        return new PartVisual
        {
            Object = obj,
            View = view,
            Renderer = renderer
        };
    }


    public void ApplyGridSorting(
        SpriteRenderer renderer,
        int gridY)
    {
        if (renderer == null)
            return;

        renderer.sortingOrder =
            Mathf.Clamp(sortingOrder - gridY, -32768, 32767);

        GameObject referencePrefab =
            GetPrefab(ItemKind.BroodBox);

        if (referencePrefab != null)
        {
            SpriteRenderer reference =
                referencePrefab.GetComponent<SpriteRenderer>();

            if (reference != null)
                renderer.sortingLayerID = reference.sortingLayerID;
        }
    }

    private GameObject GetPrefab(ItemKind kind)
    {
        if (prefabRegistry != null)
        {
            GameObject registeredPrefab =
                prefabRegistry.GetPrefab(
                    kind,
                    ItemViewContext.Apiary
                );

            if (registeredPrefab != null)
                return registeredPrefab;
        }

        switch (kind)
        {
            case ItemKind.BroodBox:
                return broodBoxPrefab;

            case ItemKind.Super:
                return superPrefab;

            case ItemKind.Lid:
                return lidPrefab;

            case ItemKind.Frame:
                return framePrefab;

            default:
                return genericPrefab;
        }
    }

    // HiveView 회전 이후 조립체 정렬
    private void LateUpdate()
    {
        foreach (StackLink link in stackLinks)
        {
            if (link.Lower == null || link.Upper == null)
                continue;

            Transform lower = link.Lower.transform;
            Transform upper = link.Upper.transform;

            upper.rotation = lower.rotation;

            Bounds lowerBounds = link.Lower.sprite.bounds;
            Bounds upperBounds = link.Upper.sprite.bounds;

            Vector3 top = lower.TransformPoint(new Vector3(
                lowerBounds.center.x,
                lowerBounds.max.y,
                lowerBounds.center.z));

            Vector3 bottom = upper.TransformPoint(new Vector3(
                upperBounds.center.x,
                upperBounds.min.y,
                upperBounds.center.z));

            upper.position += top - bottom;
        }
    }

    // 선택한 격자의 장비 전체 화면 경계 계산
    public bool TryGetEquipmentScreenBounds(
        int gridX,
        int gridY,
        Camera camera,
        out Rect screenBounds)
    {
        screenBounds = default;

        if (camera == null || grid == null ||
            !grid.IsInside(gridX, gridY))
            return false;

        bool found = false;

        float minX = float.PositiveInfinity;
        float minY = float.PositiveInfinity;
        float maxX = float.NegativeInfinity;
        float maxY = float.NegativeInfinity;

        foreach (var pair in partViews)
        {
            PartVisual visual = pair.Value;

            if (!activeParts.Contains(pair.Key) ||
                visual.Object == null ||
                !visual.Object.activeInHierarchy ||
                visual.GridX != gridX ||
                visual.GridY != gridY ||
                visual.Renderer == null ||
                !visual.Renderer.enabled ||
                visual.Renderer.forceRenderingOff ||
                visual.Renderer.sprite == null ||
                (camera.cullingMask &
                 (1 << visual.Object.layer)) == 0)
                continue;

            SpriteRenderer renderer = visual.Renderer;
            Bounds bounds = renderer.localBounds;
            Transform partTransform = renderer.transform;

            Vector3[] corners =
            {
                new Vector3(bounds.min.x, bounds.min.y, bounds.center.z),
                new Vector3(bounds.min.x, bounds.max.y, bounds.center.z),
                new Vector3(bounds.max.x, bounds.min.y, bounds.center.z),
                new Vector3(bounds.max.x, bounds.max.y, bounds.center.z)
            };

            foreach (Vector3 corner in corners)
            {
                Vector3 worldPoint =
                    partTransform.TransformPoint(corner);

                Vector3 screenPoint =
                    camera.WorldToScreenPoint(worldPoint);

                if (screenPoint.z <= 0f)
                    continue;

                minX = Mathf.Min(minX, screenPoint.x);
                minY = Mathf.Min(minY, screenPoint.y);
                maxX = Mathf.Max(maxX, screenPoint.x);
                maxY = Mathf.Max(maxY, screenPoint.y);

                found = true;
            }
        }

        if (!found)
            return false;

        screenBounds = Rect.MinMaxRect(
            minX, minY, maxX, maxY);

        return true;
    }

    // 화면 좌표로 장비를 찾아 해당 격자 좌표 반환
    public bool TryGetEquipmentAtScreenPosition(
        Vector2 screenPosition,
        Camera camera,
        out int gridX,
        out int gridY)
    {
        gridX = -1;
        gridY = -1;

        if (camera == null || grid == null)
            return false;

        bool found = false;

        int bestLayer = int.MinValue;
        int bestOrder = int.MinValue;
        float bestDistance = float.PositiveInfinity;

        foreach (var pair in partViews)
        {
            PartVisual visual = pair.Value;

            if (!activeParts.Contains(pair.Key) ||
                visual.Object == null ||
                !visual.Object.activeInHierarchy ||
                visual.Renderer == null ||
                !visual.Renderer.enabled ||
                visual.Renderer.forceRenderingOff ||
                visual.Renderer.sprite == null ||
                !grid.IsInside(visual.GridX, visual.GridY) ||
                (camera.cullingMask &
                 (1 << visual.Object.layer)) == 0)
                continue;

            SpriteRenderer renderer = visual.Renderer;
            Bounds bounds = renderer.localBounds;
            Transform t = renderer.transform;

            Vector3[] corners =
            {
                new Vector3(bounds.min.x, bounds.min.y, bounds.center.z),
                new Vector3(bounds.min.x, bounds.max.y, bounds.center.z),
                new Vector3(bounds.max.x, bounds.min.y, bounds.center.z),
                new Vector3(bounds.max.x, bounds.max.y, bounds.center.z)
            };

            float minX = float.PositiveInfinity;
            float minY = float.PositiveInfinity;
            float maxX = float.NegativeInfinity;
            float maxY = float.NegativeInfinity;
            float nearestDepth = float.PositiveInfinity;

            bool valid = true;

            foreach (Vector3 corner in corners)
            {
                Vector3 point = camera.WorldToScreenPoint(
                    t.TransformPoint(corner));

                if (point.z < camera.nearClipPlane ||
                    point.z > camera.farClipPlane)
                {
                    valid = false;
                    break;
                }

                minX = Mathf.Min(minX, point.x);
                minY = Mathf.Min(minY, point.y);
                maxX = Mathf.Max(maxX, point.x);
                maxY = Mathf.Max(maxY, point.y);

                nearestDepth = Mathf.Min(
                    nearestDepth, point.z);
            }

            if (!valid ||
                screenPosition.x < minX ||
                screenPosition.x > maxX ||
                screenPosition.y < minY ||
                screenPosition.y > maxY)
                continue;

            int layer = SortingLayer.GetLayerValueFromID(
                renderer.sortingLayerID);

            int order = renderer.sortingOrder;

            bool better =
                !found ||
                layer > bestLayer ||
                (layer == bestLayer && order > bestOrder) ||
                (layer == bestLayer &&
                 order == bestOrder &&
                 nearestDepth < bestDistance);

            if (!better)
                continue;

            found = true;
            bestLayer = layer;
            bestOrder = order;
            bestDistance = nearestDepth;

            gridX = visual.GridX;
            gridY = visual.GridY;
        }

        return found;
    }

    private void OnDestroy()
    {
        partViews.Clear();
        stackLinks.Clear();

        if (visualRoot != null)
            Destroy(visualRoot.gameObject);
    }
}
