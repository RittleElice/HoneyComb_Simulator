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

        GameFlow = new GameFlowManager(this);

        Debug.Log("[GameBootstrap] 시스템 초기화 완료");

        GameFlow.OpenMainMenu();
    }

    public void StartNewGame()
    {
        if (GameFlow.IsTransitioning)
            return;

        GameWorld = new GameWorld();
        GameFlow.ResetApiaryCamera();

        ApiaryData apiary = GameWorld.Apiary;

        Debug.Log(
            $"[ApiaryGrid] 크기: " +
            $"{apiary.Grid.Width} x {apiary.Grid.Height}");

        Debug.Log(
            $"[GameBootstrap] 새 게임 시작: " +
            $"{GameWorld.Year}년 " +
            $"{GameWorld.Month}월 " +
            $"{GameWorld.Day}일");

        GameFlow.OpenApiary();
    }
}