
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ApiaryActionUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private ApiarySelectionController selectionController;
    [SerializeField] private Transform buttonContainer;
    [SerializeField] private Button buttonPrefab;

    [Header("Panel Visibility")]
    [SerializeField] private CanvasGroup panelCanvasGroup;

    [Header("Popup Position")]
    [SerializeField] private ApiaryGridView gridView;
    [SerializeField] private Camera targetCamera;
    [SerializeField] private Vector2 popupOffset =
        new Vector2(15f, 0f);

    [Header("Equipment Bounds")]
    [SerializeField] private ApiaryItemView itemView;

    private readonly List<Button> activeButtons =
        new List<Button>();

    private RectTransform panelRect;
    private RectTransform parentRect;
    private Canvas rootCanvas;

    private bool panelVisible;

    private void Awake()
    {
        if (panelCanvasGroup == null)
            panelCanvasGroup = GetComponent<CanvasGroup>();

        panelRect = GetComponent<RectTransform>();

        if (panelRect != null)
            parentRect = panelRect.parent as RectTransform;

        rootCanvas = GetComponentInParent<Canvas>();

        if (rootCanvas != null)
            rootCanvas = rootCanvas.rootCanvas;

        if (targetCamera == null)
            targetCamera = Camera.main;

        SetPanelVisible(false);
    }

    private void Start()
    {
        if (itemView == null)
            itemView = FindAnyObjectByType<ApiaryItemView>();

        if (selectionController == null ||
            buttonContainer == null ||
            buttonPrefab == null ||
            panelCanvasGroup == null ||
            gridView == null ||
            targetCamera == null ||
            panelRect == null ||
            parentRect == null ||
            rootCanvas == null)
        {
            Debug.LogError(
                "[ApiaryActionUI] UI 또는 격자 참조가 설정되지 않았습니다.");

            enabled = false;
            return;
        }

        selectionController.OnSelectionChanged += RefreshActions;
        selectionController.OnSelectionCleared += HidePanel;

        if (selectionController.HasSelection)
        {
            RefreshActions(
                selectionController.SelectedX,
                selectionController.SelectedY);
        }
    }

    private void LateUpdate()
    {
        if (!panelVisible || selectionController == null)
            return;

        if (!selectionController.HasSelection)
        {
            HidePanel();
            return;
        }

        UpdatePopupPosition(
            selectionController.SelectedX,
            selectionController.SelectedY);
    }

    private void OnDestroy()
    {
        if (selectionController != null)
        {
            selectionController.OnSelectionChanged -= RefreshActions;
            selectionController.OnSelectionCleared -= HidePanel;
        }
    }

    private void HidePanel()
    {
        ClearButtons();
        SetPanelVisible(false);
    }

    private void RefreshActions(int x, int y)
    {
        ClearButtons();
        SetPanelVisible(false);

        GameBootstrap bootstrap = GameBootstrap.Instance;

        if (bootstrap == null || bootstrap.GameWorld == null)
            return;

        GameWorld world = bootstrap.GameWorld;
        ApiaryGrid grid = world.Apiary.Grid;

        if (!grid.IsInside(x, y))
            return;
        
        // 해금된 모든 격자에서 관찰 가능
        if (grid.IsUnlocked(x, y))
        {
            CreateButton("관찰", () =>
            {
                GameBootstrap.Instance.GameFlow
                    .OpenObservation(x, y);
            });
        }

        // 잠긴 영역: 부지 해금 메뉴
        if (grid.IsLocked(x, y))
        {
            CreateButton("부지 해금", () =>
            {
                bool success = grid.TryUnlockNextColumn();

                if (!success)
                {
                    Debug.LogWarning(
                        "[ApiaryActionUI] 부지 해금 실패");
                    return;
                }

                Debug.Log(
                    $"[ApiaryActionUI] 부지 해금 완료: " +
                    $"{grid.UnlockedWidth} × {grid.Height}");

                // 해금 후 격자 표시 즉시 갱신
                if (gridView != null)
                    gridView.Rebuild();

                // 선택 및 메뉴 닫기
                if (selectionController != null)
                    selectionController.ClearSelection();

                HidePanel();
            });

            ShowPanel(x, y);
            return;
        }

        // 해금된 일반 격자
        ItemSlot gridSlot = world.Apiary.GetItemSlot(x, y);
        ItemSlot handSlot = world.Hand.Slot;

        if (gridSlot == null || handSlot == null)
            return;

        if (handSlot.IsEmpty)
        {
            if (gridSlot.IsEmpty)
            {
                ShowPanel(x, y);
                return;
            }

            MovableItem item = gridSlot.Value;

            if (item is ItemAssembly assembly)
            {
                // 위층부터 아래층 순서로 버튼 생성
                for (int i = assembly.Parts.Count - 1; i >= 0; i--)
                {
                    ItemData part = assembly.Parts[i];

                    string label = part.Kind == ItemKind.Lid
                        ? "뚜껑 들기"
                        : $"{i + 1}층부터 들기";

                    int index = i;

                    CreateButton(label, () =>
                    {
                        bool success = gridSlot.TrySplitTo(
                            index,
                            handSlot);

                        OnActionCompleted(success, x, y);
                    });
                }
            }
            else
            {
                CreateButton("들기", () =>
                {
                    bool success = gridSlot.TryMoveTo(handSlot);
                    OnActionCompleted(success, x, y);
                });
            }
        }
        else
        {
            // 빈 격자에는 허용된 장비만 배치
            if (gridSlot.IsEmpty &&
                ApiaryPlacementRules.CanPlace(handSlot.Value))
            {
                CreateButton("놓기", () =>
                {
                    bool success = handSlot.TryMoveTo(gridSlot);
                    OnActionCompleted(success, x, y);
                });
            }
            // 기존 장비에 조립 가능한 경우
            else if (!gridSlot.IsEmpty &&
                     gridSlot.CanCombineFrom(handSlot))
            {
                CreateButton("결합하기", () =>
                {
                    bool success =
                        gridSlot.TryCombineFrom(handSlot);

                    OnActionCompleted(success, x, y);
                });
            }
        }

        if (activeButtons.Count == 0)
            return;

        ShowPanel(x, y);
    }

    private void ShowPanel(int x, int y)
    {
        if (activeButtons.Count == 0)
            return;

        SetPanelVisible(true);

        Canvas.ForceUpdateCanvases();
        UpdatePopupPosition(x, y);
    }

    private void OnActionCompleted(bool success, int x, int y)
    {
        if (!success)
        {
            Debug.LogWarning(
                $"[ApiaryActionUI] 아이템 이동 실패: ({x}, {y})");
            return;
        }

        Debug.Log(
            $"[ApiaryActionUI] 아이템 이동 성공: ({x}, {y})");

        // 모든 행동 성공 후 선택 및 메뉴 닫기
        if (selectionController != null)
            selectionController.ClearSelection();

        HidePanel();
    }

    // 선택한 격자의 화면상 경계 계산
    private bool TryGetGridScreenBounds(
        int x,
        int y,
        out Rect screenBounds)
    {
        screenBounds = default;

        GameBootstrap bootstrap = GameBootstrap.Instance;

        if (bootstrap == null ||
            bootstrap.GameWorld == null ||
            gridView == null ||
            targetCamera == null)
            return false;

        ApiaryGrid grid = bootstrap.GameWorld.Apiary.Grid;

        if (!grid.IsInside(x, y))
            return false;

        float cellSize = gridView.CellSize;

        // 초기 8열 기준 고정 원점 사용
        float left =
            gridView.OriginX + x * cellSize;

        float right = left + cellSize;

        float near = y * cellSize;
        float far = near + cellSize;

        Vector3[] corners =
        {
            new Vector3(left, 0f, near),
            new Vector3(left, 0f, far),
            new Vector3(right, 0f, near),
            new Vector3(right, 0f, far)
        };

        float minX = float.PositiveInfinity;
        float minY = float.PositiveInfinity;
        float maxX = float.NegativeInfinity;
        float maxY = float.NegativeInfinity;

        foreach (Vector3 corner in corners)
        {
            Vector3 worldPoint =
                gridView.transform.TransformPoint(corner);

            Vector3 screenPoint =
                targetCamera.WorldToScreenPoint(worldPoint);

            if (screenPoint.z <= 0f)
                return false;

            minX = Mathf.Min(minX, screenPoint.x);
            minY = Mathf.Min(minY, screenPoint.y);
            maxX = Mathf.Max(maxX, screenPoint.x);
            maxY = Mathf.Max(maxY, screenPoint.y);
        }

        screenBounds = Rect.MinMaxRect(
            minX, minY, maxX, maxY);

        return true;
    }

    // 빈 격자: 중앙 / 장비: 전체 이미지 오른쪽 중앙
    private void UpdatePopupPosition(int x, int y)
    {
        if (targetCamera == null ||
            gridView == null ||
            panelRect == null ||
            parentRect == null ||
            rootCanvas == null)
            return;

        if (!TryGetGridScreenBounds(
                x, y, out Rect gridBounds))
        {
            if (panelCanvasGroup != null)
            {
                panelCanvasGroup.alpha = 0f;
                panelCanvasGroup.interactable = false;
                panelCanvasGroup.blocksRaycasts = false;
            }

            return;
        }

        Rect equipmentBounds = default;

        bool hasEquipment =
            itemView != null &&
            itemView.TryGetEquipmentScreenBounds(
                x,
                y,
                targetCamera,
                out equipmentBounds);

        Vector2 screenPosition;

        if (hasEquipment)
        {
            // 장비 전체 이미지의 오른쪽 중앙
            screenPosition = new Vector2(
                equipmentBounds.xMax,
                equipmentBounds.center.y);

            panelRect.pivot = new Vector2(0f, 0.5f);
        }
        else
        {
            // 빈 격자 또는 잠긴 격자의 중앙
            screenPosition = gridBounds.center;

            panelRect.pivot = new Vector2(0.5f, 0.5f);
        }

        // 장비가 있는 경우에만 간격 적용
        Vector2 appliedOffset =
            hasEquipment ? popupOffset : Vector2.zero;

        Vector2 finalScreenPosition =
            screenPosition + appliedOffset;

        Camera uiCamera =
            rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay
                ? null
                : rootCanvas.worldCamera;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                parentRect,
                finalScreenPosition,
                uiCamera,
                out Vector2 localPosition))
            return;

        Rect bounds = parentRect.rect;

        Vector2 anchorReference = new Vector2(
            Mathf.Lerp(
                bounds.xMin,
                bounds.xMax,
                panelRect.anchorMin.x),
            Mathf.Lerp(
                bounds.yMin,
                bounds.yMax,
                panelRect.anchorMin.y));

        panelRect.anchoredPosition =
            localPosition - anchorReference;

        if (panelVisible && panelCanvasGroup != null)
        {
            panelCanvasGroup.alpha = 1f;
            panelCanvasGroup.interactable = true;
            panelCanvasGroup.blocksRaycasts = true;
        }
    }

    private void SetPanelVisible(bool visible)
    {
        panelVisible = visible;

        if (panelCanvasGroup == null)
            return;

        panelCanvasGroup.alpha = visible ? 1f : 0f;
        panelCanvasGroup.interactable = visible;
        panelCanvasGroup.blocksRaycasts = visible;
    }

    private void CreateButton(
        string label,
        UnityEngine.Events.UnityAction action)
    {
        Button button = Instantiate(
            buttonPrefab,
            buttonContainer);

        button.gameObject.SetActive(true);

        TMP_Text text =
            button.GetComponentInChildren<TMP_Text>();

        if (text != null)
            text.text = label;

        button.onClick.AddListener(action);
        activeButtons.Add(button);
    }

    private void ClearButtons()
    {
        foreach (Button button in activeButtons)
        {
            if (button != null)
            {
                button.gameObject.SetActive(false);
                Destroy(button.gameObject);
            }
        }

        activeButtons.Clear();
    }
}
