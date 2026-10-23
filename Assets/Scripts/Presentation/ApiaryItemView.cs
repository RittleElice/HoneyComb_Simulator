
using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(100)]
public class ApiaryItemView : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private ApiaryGridView gridView;

    [Header("Equipment Prefabs")]
    [SerializeField] private GameObject broodBoxPrefab;
    [SerializeField] private GameObject superPrefab;
    [SerializeField] private GameObject lidPrefab;
    [SerializeField] private GameObject framePrefab;
    [SerializeField] private GameObject genericPrefab;

    [Header("Sorting")]
    [SerializeField] private int sortingOrder = 10;

    private readonly Dictionary<ItemSlot, MovableItem> cachedItems =
        new Dictionary<ItemSlot, MovableItem>();

    private readonly List<GameObject> visualObjects =
        new List<GameObject>();

    private struct StackLink
    {
        public SpriteRenderer Lower;
        public SpriteRenderer Upper;
    }

    private readonly List<StackLink> stackLinks = new List<StackLink>();
    private SpriteRenderer previousPart;

    private void RegisterPart(SpriteRenderer renderer)
    {
        if (previousPart != null)
            stackLinks.Add(new StackLink { Lower = previousPart, Upper = renderer });
        previousPart = renderer;
    }

    // HiveView.LateUpdate의 회전이 끝난 뒤 실제 Sprite 경계를 맞춘다.
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
                lowerBounds.center.x, lowerBounds.max.y, lowerBounds.center.z));
            Vector3 bottom = upper.TransformPoint(new Vector3(
                upperBounds.center.x, upperBounds.min.y, upperBounds.center.z));

            upper.position += top - bottom;
        }
    }

    private ApiaryUI apiaryUI;
    private ApiaryGrid grid;
    private Transform visualRoot;

    private void Start()
    {
        if (gridView == null)
            gridView = GetComponent<ApiaryGridView>();

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

        apiaryUI = FindAnyObjectByType<ApiaryUI>();
        if (apiaryUI == null)
        {
            Debug.LogError("[ApiaryItemView] ApiaryUI가 없습니다.");
            enabled = false;
            return;
        }

        grid = bootstrap.GameWorld.Apiary.Grid;

        GameObject root = new GameObject("EquipmentVisuals");
        visualRoot = root.transform;
        // 월드 루트에 두어 기존 Hive_1과 같은 좌표 및 스케일 기준을 사용한다.

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
        int count = 0;

        for (int x = 0; x < grid.Width; x++)
        {
            for (int y = 0; y < grid.Height; y++)
            {
                ItemSlot slot = grid.GetItemSlot(x, y);
                count++;

                if (!cachedItems.TryGetValue(
                        slot, out MovableItem previous))
                    return true;

                if (!ReferenceEquals(previous, slot.Value))
                    return true;
            }
        }

        return count != cachedItems.Count;
    }

    public void Refresh()
    {
        if (grid == null)
            return;

        apiaryUI.EnsureHiveViews();
        apiaryUI.HideHiveViews();
        ClearVisuals();
        cachedItems.Clear();

        for (int x = 0; x < grid.Width; x++)
        {
            for (int y = 0; y < grid.Height; y++)
            {
                ItemSlot slot = grid.GetItemSlot(x, y);

                cachedItems[slot] = slot.Value;

                if (!slot.IsEmpty)
                    DrawItem(x, y, slot.Value);
            }
        }
    }

    private void DrawItem(int x, int y, MovableItem item)
    {
        previousPart = null;

        if (item is ItemAssembly assembly)
        {
            foreach (ItemData part in assembly.Parts)
                DrawPart(x, y, part);
        }
        else if (item is ItemData single)
        {
            DrawPart(x, y, single);
        }
    }

    private void DrawPart(int x, int y, ItemData item)
    {
        HiveData hive = item.GetContent<HiveData>();
        if (item.Kind == ItemKind.BroodBox &&
            hive != null &&
            apiaryUI.TryGetHiveView(hive, out HiveView existing))
        {
            hive.SetPosition(x, y);
            existing.gameObject.SetActive(true);
            existing.Initialize(hive, grid.Width, gridView.CellSize);
            SpriteRenderer existingRenderer = existing.GetComponent<SpriteRenderer>();
            ApplyGridSorting(existingRenderer, y);
            RegisterPart(existingRenderer);
            return;
        }

        GameObject prefab = GetPrefab(item.Kind);
        if (prefab == null)
        {
            Debug.LogWarning($"[ApiaryItemView] {item.Kind} 프리팹이 없습니다.");
            previousPart = null;
            return;
        }

        GameObject obj = Instantiate(prefab, visualRoot);
        obj.name = item.Name;

        SpriteRenderer renderer = obj.GetComponent<SpriteRenderer>();
        HiveView view = obj.GetComponent<HiveView>();
        if (renderer == null || renderer.sprite == null || view == null ||
            renderer.sprite.bounds.size.x <= 0f ||
            renderer.sprite.bounds.size.y <= 0f)
        {
            Debug.LogWarning(
                $"[ApiaryItemView] {prefab.name}의 루트에 HiveView와 유효한 SpriteRenderer가 필요합니다.");
            obj.SetActive(false);
            Destroy(obj);
            previousPart = null;
            return;
        }

        // 기존 Hive_1과 동일한 배치 함수. 장비마다 프리팹 설정을 사용한다.
        view.ApplyLayout(x, y, grid.Width, gridView.CellSize);
        ApplyGridSorting(renderer, y);

        foreach (Collider collider in obj.GetComponentsInChildren<Collider>(true))
            collider.enabled = false;
        foreach (Collider2D collider in obj.GetComponentsInChildren<Collider2D>(true))
            collider.enabled = false;

        visualObjects.Add(obj);
        RegisterPart(renderer);
    }
    public void ApplyGridSorting(SpriteRenderer renderer, int gridY)
    {
        // 현재 카메라에서는 Y=0이 앞줄이다. 높이나 생성 순서 대신 소속 행으로 정한다.
        // 같은 격자의 모든 부품에 같은 순서를 부여해 뒤쪽 장비가 끼어들지 않게 한다.
        renderer.sortingOrder = Mathf.Clamp(sortingOrder - gridY, -32768, 32767);

        // Sorting Layer도 단상 프리팹과 통일한다.
        if (broodBoxPrefab != null)
        {
            SpriteRenderer reference = broodBoxPrefab.GetComponent<SpriteRenderer>();
            if (reference != null)
                renderer.sortingLayerID = reference.sortingLayerID;
        }
    }

    private GameObject GetPrefab(ItemKind kind)
    {
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

    private void ClearVisuals()
    {
        stackLinks.Clear();
        previousPart = null;
        foreach (GameObject obj in visualObjects)
        {
            if (obj == null)
                continue;

            obj.SetActive(false);
            Destroy(obj);
        }

        visualObjects.Clear();
    }

    private void OnDestroy()
    {
        ClearVisuals();
        if (visualRoot != null)
            Destroy(visualRoot.gameObject);
    }
}
