using UnityEngine;

public class GameBootstrap : MonoBehaviour
{
    public static GameBootstrap Instance { get; private set; }

    public GameWorld GameWorld { get; private set; }
    public GameFlowManager GameFlow { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        InitializeGame();
    }

    private void InitializeGame()
    {
        Debug.Log("[GameBootstrap] 시스템 초기화 시작");

        GameFlow = new GameFlowManager();

        Debug.Log("[GameBootstrap] 시스템 초기화 완료");

        GameFlow.OpenMainMenu();
    }

    public void StartNewGame()
    {
        GameWorld = new GameWorld();

        Debug.Log(
            $"[GameBootstrap] 새 게임 시작: " +
            $"{GameWorld.Year}년 {GameWorld.Month}월 {GameWorld.Day}일"
        );

        GameFlow.OpenApiary();
    }
}