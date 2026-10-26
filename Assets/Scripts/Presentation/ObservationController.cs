
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
    [SerializeField] private ObservationItemView itemView;

    private ItemSlot observedSlot;

    private void Start()
    {
        if (backButton != null)
            backButton.onClick.AddListener(GoBack);

        RefreshObservation();
    }

    public void RefreshObservation()
    {
        GameBootstrap bootstrap = GameBootstrap.Instance;

        if (bootstrap == null ||
            bootstrap.GameWorld == null ||
            !ObservationContext.HasTarget)
        {
            ShowEmpty("관찰 대상 없음");
            return;
        }

        ApiaryGrid grid =
            bootstrap.GameWorld.Apiary.Grid;

        int x = ObservationContext.GridX;
        int y = ObservationContext.GridY;

        if (!grid.IsInside(x, y))
        {
            ShowEmpty("유효하지 않은 격자");
            return;
        }

        if (!grid.IsUnlocked(x, y))
        {
            ShowEmpty("잠긴 부지");
            return;
        }

        observedSlot = grid.GetItemSlot(x, y);

        if (targetText != null)
            targetText.text = $"관찰 위치: ({x}, {y})";

        if (observedSlot == null || observedSlot.IsEmpty)
        {
            if (targetInfoText != null)
                targetInfoText.text = "비어 있는 격자";

            if (itemView != null)
                itemView.Clear();

            return;
        }

        MovableItem item = observedSlot.Value;

        if (targetInfoText != null)
            targetInfoText.text = BuildItemInfo(item);

        if (itemView != null)
            itemView.Show(item);
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

        if (item is ItemAssembly assembly)
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
        observedSlot = null;

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
        if (backButton != null)
            backButton.onClick.RemoveListener(GoBack);
    }
}
