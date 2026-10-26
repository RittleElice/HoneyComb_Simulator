using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ObservationController : MonoBehaviour
{
    [Header("뒤로 가기")]
    [SerializeField] private Button backButton;

    [Header("관찰 대상")]
    [SerializeField] private TMP_Text targetText;

    private void Start()
    {
        if (backButton == null)
        {
            Debug.LogError(
                "[ObservationController] BackButton이 연결되지 않았습니다.");
            return;
        }

        backButton.onClick.AddListener(GoBack);

        if (targetText != null)
        {
            if (ObservationContext.HasTarget)
            {
                targetText.text =
                    $"관찰 중: ({ObservationContext.GridX}, " +
                    $"{ObservationContext.GridY})";
            }
            else
            {
                targetText.text = "관찰 대상 없음";
            }
        }
    }

    public void GoBack()
    {
        GameBootstrap bootstrap = GameBootstrap.Instance;

        if (bootstrap == null || bootstrap.GameFlow == null)
        {
            Debug.LogError(
                "[ObservationController] GameFlow를 찾을 수 없습니다.");
            return;
        }

        if (bootstrap.GameFlow.IsTransitioning)
            return;

        Debug.Log("[ObservationController] 양봉장으로 복귀");

        bootstrap.GameFlow.OpenApiary();
    }

    private void OnDestroy()
    {
        if (backButton != null)
            backButton.onClick.RemoveListener(GoBack);
    }
}