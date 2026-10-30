using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ObservationController : MonoBehaviour
{
    [Header("Navigation")]
    [SerializeField] private Button backButton;

    [Header("Target Information")]
    [SerializeField] private TMP_Text targetText;
    [SerializeField] private TMP_Text targetInfoText;

    [Header("Equipment Display")]
    [SerializeField] private ObservationItemPresenter itemView;

    private ItemSlot observedSlot;
    private ItemSlotObserver slotObserver;
    private ApiaryGrid sourceGrid;
    private int sourceX;
    private int sourceY;
    private bool sourceHasTarget;
    private bool started;
    private bool refreshPending;

    // Always resolves through the current slot; never retains an old assembly.
    public ItemSlot ObservedSlot => observedSlot;
    public MovableItem ObservedItem => observedSlot != null ? observedSlot.Value : null;

    private void Start()
    {
        started = true;
        if (targetInfoText != null) targetInfoText.gameObject.SetActive(false);
        ActionButtonManager.Adopt(backButton, ActionButtonId.Back);
        if (backButton != null)
        {
            // SharedUI inventory Canvas uses order 100. Keep only navigation above it.
            var navigationCanvas = backButton.GetComponent<Canvas>();
            if (navigationCanvas == null) navigationCanvas = backButton.gameObject.AddComponent<Canvas>();
            navigationCanvas.overrideSorting = true;
            navigationCanvas.sortingOrder = 101;
            if (backButton.GetComponent<GraphicRaycaster>() == null) backButton.gameObject.AddComponent<GraphicRaycaster>();
        }
        if (backButton != null)
            backButton.onClick.AddListener(GoBack);

        RefreshObservation();
    }

    private void OnEnable()
    {
        if (started)
            RefreshObservation();
    }

    private void OnDisable()
    {
        BindSlot(null);
        refreshPending = true;
    }

    private void Update()
    {
        GameBootstrap bootstrap = GameBootstrap.Instance;
        ApiaryGrid currentGrid = bootstrap != null && bootstrap.GameWorld != null
            ? bootstrap.GameWorld.Apiary.Grid
            : null;

        bool hasTarget = ObservationContext.HasTarget;
        int x = ObservationContext.GridX;
        int y = ObservationContext.GridY;
        ItemSlot targetSlot = hasTarget && currentGrid != null
            ? currentGrid.GetItemSlot(x, y)
            : null;

        if (refreshPending || !ReferenceEquals(sourceGrid, currentGrid) ||
            sourceHasTarget != hasTarget || sourceX != x || sourceY != y ||
            !ReferenceEquals(observedSlot, targetSlot))
        {
            RefreshObservation();
        }
    }

    private void BindSlot(ItemSlot slot)
    {
        if (ReferenceEquals(observedSlot, slot))
            return;

        if (slotObserver == null)
            slotObserver = new ItemSlotObserver(OnObservedSlotChanged);

        slotObserver.Clear();
        observedSlot = slot;
        slotObserver.Observe(slot);
    }

    private void OnObservedSlotChanged(ItemSlot slot)
    {
        refreshPending = true;
    }

    public void RefreshObservation()
    {
        if (!isActiveAndEnabled)
            return;

        refreshPending = false;
        GameBootstrap bootstrap = GameBootstrap.Instance;
        sourceGrid = bootstrap != null && bootstrap.GameWorld != null
            ? bootstrap.GameWorld.Apiary.Grid
            : null;
        sourceHasTarget = ObservationContext.HasTarget;
        sourceX = ObservationContext.GridX;
        sourceY = ObservationContext.GridY;

        if (sourceGrid == null || !sourceHasTarget)
        {
            ShowEmpty("관찰 대상 없음");
            return;
        }

        if (!sourceGrid.IsInside(sourceX, sourceY))
        {
            ShowEmpty("유효하지 않은 격자");
            return;
        }

        if (!sourceGrid.IsUnlocked(sourceX, sourceY))
        {
            ShowEmpty("잠긴 부지");
            return;
        }

        BindSlot(sourceGrid.GetItemSlot(sourceX, sourceY));

        if (targetText != null)
            targetText.text = $"관찰 위치: ({sourceX}, {sourceY})";

        MovableItem item = ObservedItem;
        if (targetInfoText != null)
            targetInfoText.text = BuildItemInfo(item);

        if (itemView != null)
        {
            if (item == null)
                itemView.Clear();
            else
                itemView.Show(item);
        }
    }

    private string BuildItemInfo(MovableItem item)
    {
        if (item == null)
            return "비어 있는 격자";

        if (item is ItemData single)
        {
            return $"장비: {single.Name}\n" +
                   $"종류: {single.Kind}";
        }

        if (item is ItemAssemblyData assembly)
        {
            StringBuilder builder = new StringBuilder();

            builder.AppendLine($"조립체: {item.Name}");
            builder.AppendLine(
                $"구성 부품: {assembly.Parts.Count}개");

            for (int i = 0; i < assembly.Parts.Count; i++)
            {
                ItemData part = assembly.Parts[i];

                builder.AppendLine(
                    $"{i + 1}. {part.Name} ({part.Kind})");
            }

            return builder.ToString();
        }

        return item.Name;
    }

    private void ShowEmpty(string message)
    {
        BindSlot(null);

        if (targetText != null)
            targetText.text = message;
        if (targetInfoText != null)
            targetInfoText.text = "";
        if (itemView != null)
            itemView.Clear();
    }

    public void GoBack()
    {
        GameBootstrap bootstrap = GameBootstrap.Instance;

        if (bootstrap == null ||
            bootstrap.GameFlow == null ||
            bootstrap.GameFlow.IsTransitioning)
            return;

        bootstrap.GameFlow.OpenApiary();
    }

    private void OnDestroy()
    {
        slotObserver?.Dispose();
        observedSlot = null;
        if (backButton != null)
            backButton.onClick.RemoveListener(GoBack);
    }
}
