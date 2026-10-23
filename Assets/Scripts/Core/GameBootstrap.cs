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
        
        ApiaryData apiary = GameWorld.Apiary;

        Debug.Log(
            $"[ApiaryGrid] 크기: {apiary.Grid.Width} x {apiary.Grid.Height}"
        );

        Debug.Log(
            $"[ApiaryGrid] (0,0) 벌통 ID: {apiary.Grid.GetHiveId(0, 0)}"
        );

        Debug.Log(
            $"[ApiaryGrid] (1,0) 빈 칸: {apiary.Grid.IsEmpty(1, 0)}"
        );

        Debug.Log(
            $"[ApiaryGrid] (0,0) 빈 칸: {apiary.Grid.IsEmpty(0, 0)}"
        );


        Debug.Log(
            $"[GameBootstrap] 새 게임 시작: " +
            $"{GameWorld.Year}년 {GameWorld.Month}월 {GameWorld.Day}일"
        );

        GameFlow.OpenApiary();
    }
}