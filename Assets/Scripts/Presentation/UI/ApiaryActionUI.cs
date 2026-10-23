
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

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
    [SerializeField] private Vector2 popupOffset = new Vector2(20f, 20f);

    [Header("UI Refresh")]
    [SerializeField] private HandUI handUI;
    [SerializeField] private InventoryUI inventoryUI;

    private readonly List<Button> activeButtons = new List<Button>();

    private RectTransform panelRect;
    private RectTransform parentRect;
    private Canvas rootCanvas;

    private bool panelVisible;

    private void Awake()
    {
        if (panelCanvasGroup == null)
            panelCanvasGroup = GetComponent<CanvasGroup>();

        panelRect = GetComponent<RectTransform>();
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
                "[ApiaryActionUI] UI 또는 격자 참조가 설정되지 않았습니다."
            );
            return;
        }

        selectionController.OnSelectionChanged += RefreshActions;
        selectionController.OnSelectionCleared += HidePanel;

        if (selectionController.HasSelection)
        {
            RefreshActions(
                selectionController.SelectedX,
                selectionController.SelectedY
            );
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
            selectionController.SelectedY
        );
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

        ItemSlot gridSlot = world.Apiary.GetItemSlot(x, y);
        ItemSlot handSlot = world.Hand.Slot;

        if (gridSlot == null || handSlot == null)
            return;

        // 기존 HiveData는 아직 새 ItemSlot에 등록되지 않음
        if (world.Apiary.Grid.GetHiveId(x, y) > 0)
            return;

        if (handSlot.IsEmpty)
        {
            if (gridSlot.IsEmpty)
                return;

            MovableItem item = gridSlot.Value;

            if (item is ItemAssembly assembly)
            {
                for (int i = 0; i < assembly.Parts.Count; i++)
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
                            handSlot
                        );

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
            // 빈 격자: 손의 아이템 배치
            if (gridSlot.IsEmpty)
            {
                CreateButton("놓기", () =>
                {
                    bool success = handSlot.TryMoveTo(gridSlot);
                    OnActionCompleted(success, x, y);
                });
            }
            // 장비가 있는 격자: 조립 가능한 경우 결합
            else if (gridSlot.CanCombineFrom(handSlot))
            {
                CreateButton("결합하기", () =>
                {
                    bool success = gridSlot.TryCombineFrom(handSlot);
                    OnActionCompleted(success, x, y);
                });
            }
        }

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
                $"[ApiaryActionUI] 아이템 이동 실패: ({x}, {y})"
            );
            return;
        }

        Debug.Log(
            $"[ApiaryActionUI] 아이템 이동 성공: ({x}, {y})"
        );

        // 손 UI 갱신
        if (handUI != null)
            handUI.Refresh();

        // 소지품 UI 갱신
        if (inventoryUI != null)
            inventoryUI.Refresh();

        // 현재 격자의 행동 메뉴 재구성
        RefreshActions(x, y);
    }

    private void UpdatePopupPosition(int x, int y)
    {
        if (targetCamera == null ||
            gridView == null ||
            panelRect == null ||
            parentRect == null ||
            rootCanvas == null)
            return;

        GameBootstrap bootstrap = GameBootstrap.Instance;

        if (bootstrap == null || bootstrap.GameWorld == null)
            return;

        float cellSize = gridView.CellSize;

        int gridWidth =
            bootstrap.GameWorld.Apiary.Grid.Width;

        float totalWidth = gridWidth * cellSize;

        Vector3 localCellCenter = new Vector3(
            (x + 0.5f) * cellSize - totalWidth / 2f,
            0f,
            (y + 0.5f) * cellSize
        );

        Vector3 worldPosition =
            gridView.transform.TransformPoint(localCellCenter);

        Vector3 screenPosition =
            targetCamera.WorldToScreenPoint(worldPosition);

        if (screenPosition.z <= 0f)
        {
            panelCanvasGroup.alpha = 0f;
            panelCanvasGroup.interactable = false;
            panelCanvasGroup.blocksRaycasts = false;
            return;
        }

        panelCanvasGroup.alpha = 1f;
        panelCanvasGroup.interactable = true;
        panelCanvasGroup.blocksRaycasts = true;

        Camera uiCamera =
            rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay
                ? null
                : rootCanvas.worldCamera;

        Vector2 localPosition;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                parentRect,
                screenPosition,
                uiCamera,
                out localPosition))
            return;

        Vector2 desiredPosition = localPosition + popupOffset;

        Rect bounds = parentRect.rect;

        Vector2 anchorReference = new Vector2(
            Mathf.Lerp(
                bounds.xMin,
                bounds.xMax,
                panelRect.anchorMin.x
            ),
            Mathf.Lerp(
                bounds.yMin,
                bounds.yMax,
                panelRect.anchorMin.y
            )
        );

        // 화면 경계 제한 없이 격자를 따라 이동
        panelRect.anchoredPosition =
            desiredPosition - anchorReference;
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
        Button button = Instantiate(buttonPrefab, buttonContainer);

        button.gameObject.SetActive(true);

        TMP_Text text = button.GetComponentInChildren<TMP_Text>();

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
