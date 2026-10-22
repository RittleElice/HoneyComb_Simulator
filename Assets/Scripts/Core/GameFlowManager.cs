using UnityEngine.SceneManagement;

public class GameFlowManager
{
    public void OpenMainMenu()
    {
        SceneManager.LoadScene("MainMenu");
    }

    public void OpenApiary()
    {
        SceneManager.LoadScene("Apiary");
    }
}