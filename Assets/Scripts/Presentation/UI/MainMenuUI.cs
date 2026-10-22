using UnityEngine;

public class MainMenuUI : MonoBehaviour
{
    public void OnNewGameClicked()
    {
        GameBootstrap.Instance.StartNewGame();
    }
}