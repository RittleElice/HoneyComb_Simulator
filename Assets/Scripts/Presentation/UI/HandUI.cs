using TMPro;
using UnityEngine;

public class HandUI : MonoBehaviour
{
    [SerializeField] private TMP_Text handText;

    private HandData hand;

    private void Start()
    {
        hand = GameBootstrap.Instance.GameWorld.Hand;
        Refresh();
    }

    public void Refresh()
    {
        if (hand == null || handText == null)
            return;

        handText.text = hand.IsEmpty
            ? "손: 빈 칸"
            : $"손: {hand.Value.Name}";
    }
}