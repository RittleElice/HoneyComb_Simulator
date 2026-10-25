
using System.Collections.Generic;
using UnityEngine;

public class ApiarySelectionHighlight : MonoBehaviour
{
    [Header("References")]
    [SerializeField]
    private ApiarySelectionController selectionController;

    [SerializeField]
    private ApiaryGridView gridView;

    [Header("Equipment Highlight")]
    [SerializeField]
    private Color equipmentTint =
        new Color(1f, 0.95f, 0.65f, 1f);

    [SerializeField]
    private Color outlineColor =
        new Color(1f, 0.75f, 0.1f, 1f);

    [SerializeField, Range(1f, 1.3f)]
    private float outlineScale = 1.06f;

    [Header("Grid Highlight")]
    [SerializeField]
    private Color gridColor =
        new Color(1f, 0.8f, 0.15f, 0.25f);

    [SerializeField, Min(0f)]
    private float gridHeight = 0.015f;

    private sealed class HighlightPart
    {
        public SpriteRenderer Source;
        public SpriteRenderer Outline;
        public Color OriginalColor;
    }

    private readonly Dictionary<SpriteRenderer, HighlightPart>
        highlightedParts =
        new Dictionary<SpriteRenderer, HighlightPart>();

    private readonly HashSet<SpriteRenderer> currentRenderers =
        new HashSet<SpriteRenderer>();

    private readonly List<SpriteRenderer> removalBuffer =
        new List<SpriteRenderer>();

    private GameObject gridHighlightObject;
    private Mesh gridMesh;
    private Material gridMaterial;

    private bool hasSelection;
    private int selectedX = -1;
    private int selectedY = -1;

    private void Start()
    {
        if (selectionController == null)
        {
            selectionController =
                FindAnyObjectByType<ApiarySelectionController>();
        }

        if (gridView == null)
        {
            gridView =
                FindAnyObjectByType<ApiaryGridView>();
        }

        if (selectionController == null || gridView == null)
        {
            Debug.LogError(
                "[ApiarySelectionHighlight] " +
                "필요한 참조가 없습니다.");

            enabled = false;
            return;
        }

        CreateGridHighlight();

        selectionController.OnSelectionChanged += OnSelected;
        selectionController.OnSelectionCleared += ClearSelection;

        if (selectionController.HasSelection)
        {
            OnSelected(
                selectionController.SelectedX,
                selectionController.SelectedY);
        }
    }

    private void OnSelected(int x, int y)
    {
        ClearEquipmentHighlight();

        selectedX = x;
        selectedY = y;
        hasSelection = true;

        UpdateGridHighlight();
        UpdateEquipmentHighlight();
    }

    private void LateUpdate()
    {
        if (!hasSelection)
            return;

        // 장비가 이동하거나 조립체가 변경되었을 때도 갱신
        UpdateEquipmentHighlight();
    }

    private void UpdateEquipmentHighlight()
    {
        currentRenderers.Clear();

        // 현재 활성화된 장비만 검색
        HiveView[] views =
            FindObjectsByType<HiveView>(
                FindObjectsSortMode.None);

        foreach (HiveView view in views)
        {
            if (view == null ||
                !view.HasGridPosition ||
                view.GridX != selectedX ||
                view.GridY != selectedY)
                continue;

            SpriteRenderer renderer =
                view.GetComponent<SpriteRenderer>();

            if (renderer == null ||
                !renderer.enabled ||
                renderer.sprite == null ||
                !renderer.gameObject.activeInHierarchy)
                continue;

            currentRenderers.Add(renderer);

            if (!highlightedParts.ContainsKey(renderer))
                AddEquipmentHighlight(renderer);
        }

        removalBuffer.Clear();

        foreach (var pair in highlightedParts)
        {
            if (!currentRenderers.Contains(pair.Key))
                removalBuffer.Add(pair.Key);
        }

        foreach (SpriteRenderer renderer in removalBuffer)
            RemoveEquipmentHighlight(renderer);

        // 기존 장비의 Sprite나 정렬 순서가 변경되면 반영
        foreach (var pair in highlightedParts)
        {
            HighlightPart part = pair.Value;

            if (part.Source == null || part.Outline == null)
                continue;

            part.Outline.sprite = part.Source.sprite;
            part.Outline.sortingLayerID =
                part.Source.sortingLayerID;

            part.Outline.sortingOrder =
                Mathf.Max(-32768,
                    part.Source.sortingOrder - 1);

            part.Outline.transform.localScale =
                Vector3.one * outlineScale;
        }
    }

    private void AddEquipmentHighlight(
        SpriteRenderer renderer)
    {
        GameObject outlineObject =
            new GameObject("SelectionOutline");

        outlineObject.transform.SetParent(
            renderer.transform, false);

        outlineObject.transform.localPosition = Vector3.zero;
        outlineObject.transform.localRotation = Quaternion.identity;
        outlineObject.transform.localScale =
            Vector3.one * outlineScale;

        SpriteRenderer outline =
            outlineObject.AddComponent<SpriteRenderer>();

        outline.sprite = renderer.sprite;
        outline.color = outlineColor;
        outline.sortingLayerID = renderer.sortingLayerID;
        outline.sortingOrder =
            Mathf.Max(-32768, renderer.sortingOrder - 1);

        outline.flipX = renderer.flipX;
        outline.flipY = renderer.flipY;

        HighlightPart part = new HighlightPart
        {
            Source = renderer,
            Outline = outline,
            OriginalColor = renderer.color
        };

        highlightedParts.Add(renderer, part);

        // 원래 이미지의 색상 강조
        renderer.color = new Color(
            part.OriginalColor.r * equipmentTint.r,
            part.OriginalColor.g * equipmentTint.g,
            part.OriginalColor.b * equipmentTint.b,
            part.OriginalColor.a);
    }

    private void RemoveEquipmentHighlight(
        SpriteRenderer renderer)
    {
        if (!highlightedParts.TryGetValue(
            renderer, out HighlightPart part))
            return;

        if (part.Source != null)
            part.Source.color = part.OriginalColor;

        if (part.Outline != null)
        {
            part.Outline.gameObject.SetActive(false);
            Destroy(part.Outline.gameObject);
        }

        highlightedParts.Remove(renderer);
    }

    private void ClearEquipmentHighlight()
    {
        removalBuffer.Clear();

        foreach (var pair in highlightedParts)
            removalBuffer.Add(pair.Key);

        foreach (SpriteRenderer renderer in removalBuffer)
            RemoveEquipmentHighlight(renderer);

        removalBuffer.Clear();
        currentRenderers.Clear();
    }

    private void CreateGridHighlight()
    {
        gridHighlightObject =
            new GameObject("SelectedGridHighlight");

        gridHighlightObject.transform.SetParent(
            gridView.transform, false);

        MeshFilter filter =
            gridHighlightObject.AddComponent<MeshFilter>();

        MeshRenderer renderer =
            gridHighlightObject.AddComponent<MeshRenderer>();

        gridMesh = new Mesh();
        gridMesh.name = "SelectionGridMesh";

        filter.sharedMesh = gridMesh;

        Shader shader = Shader.Find("Sprites/Default");

        if (shader == null)
        {
            Debug.LogError(
                "[ApiarySelectionHighlight] " +
                "Sprites/Default 셰이더를 찾을 수 없습니다.");
            gridHighlightObject.SetActive(false);
            return;
        }

        gridMaterial = new Material(shader);
        gridMaterial.color = gridColor;

        renderer.sharedMaterial = gridMaterial;
        renderer.sortingOrder = 0;

        gridHighlightObject.SetActive(false);
    }

    private void UpdateGridHighlight()
    {
        if (gridHighlightObject == null ||
            gridMesh == null ||
            gridView == null)
            return;

        GameBootstrap bootstrap = GameBootstrap.Instance;

        if (bootstrap == null ||
            bootstrap.GameWorld == null)
            return;

        ApiaryGrid grid =
            bootstrap.GameWorld.Apiary.Grid;

        if (!grid.IsInside(selectedX, selectedY))
            return;

        float cellSize = gridView.CellSize;
        float totalWidth = grid.Width * cellSize;

        float xMin =
            selectedX * cellSize - totalWidth / 2f;

        float xMax = xMin + cellSize;

        float zMin = selectedY * cellSize;
        float zMax = zMin + cellSize;

        Vector3[] vertices =
        {
            new Vector3(xMin, gridHeight, zMin),
            new Vector3(xMin, gridHeight, zMax),
            new Vector3(xMax, gridHeight, zMax),
            new Vector3(xMax, gridHeight, zMin)
        };

        gridMesh.Clear();

        gridMesh.vertices = vertices;
        gridMesh.triangles = new int[]
        {
            0, 1, 2,
            0, 2, 3
        };

        gridMesh.uv = new Vector2[]
        {
            new Vector2(0, 0),
            new Vector2(0, 1),
            new Vector2(1, 1),
            new Vector2(1, 0)
        };

        gridMesh.RecalculateNormals();
        gridMesh.RecalculateBounds();

        gridHighlightObject.SetActive(true);
    }

    public void ClearSelection()
    {
        hasSelection = false;
        selectedX = -1;
        selectedY = -1;

        ClearEquipmentHighlight();

        if (gridHighlightObject != null)
            gridHighlightObject.SetActive(false);
    }

    private void OnDestroy()
    {
        if (selectionController != null)
        {
            selectionController.OnSelectionChanged -= OnSelected;
            selectionController.OnSelectionCleared -= ClearSelection;
        }

        ClearEquipmentHighlight();

        if (gridHighlightObject != null)
            Destroy(gridHighlightObject);

        if (gridMesh != null)
            Destroy(gridMesh);

        if (gridMaterial != null)
            Destroy(gridMaterial);
    }
}
