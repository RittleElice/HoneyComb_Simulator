using UnityEngine;

public class GameBootstrap : MonoBehaviour
{
    public static GameBootstrap Instance { get; private set; }

    public GameWorld GameWorld { get; private set; }
    public GameFlowManager GameFlow { get; private set; }
    public GameTimeRuntime TimeSystem { get; private set; }
    public GamePersistenceRuntime SaveSystem { get; private set; }

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

        TimeSystem = gameObject.AddComponent<GameTimeRuntime>();
        SaveSystem = gameObject.AddComponent<GamePersistenceRuntime>();
        gameObject.AddComponent<GameTimeUIHost>();
        GameFlow = new GameFlowManager(this);

        Debug.Log("[GameBootstrap] 시스템 초기화 완료");

        GameFlow.OpenMainMenu();
    }

    public async void ContinueGame()
    {
        if (GameFlow.IsTransitioning || GameTimeRuntime.InputBlocked || SaveSystem.IsSaving || GameWorld != null) return;
        try
        {
            var snapshot = await SaveSystem.LoadAsync();
            await TimeSystem.WaitForResumeAsync();
            var resume = GameTimeRuntime.PrepareResume(snapshot);
            // Validate elapsed time before replacing the visible world.
            GameWorld = snapshot.World;
            TimeSystem.BeginWorld(GameWorld);
            GameFlow.ResetApiaryCamera();
            GameFlow.OpenApiary();
            TimeSystem.ApplyResume(resume.offline, resume.minutes);
            SaveSystem.RequestSave();
        }
        catch (System.Exception e) { Debug.LogWarning("[Continue] " + e.Message); }
    }

    public void StartNewGame()
    {
        if (GameFlow.IsTransitioning || GameTimeRuntime.InputBlocked || SaveSystem.IsSaving)
            return;

        GameWorld = new GameWorld(WeatherAssetLoader.Create(WeatherSimulator.DefaultSeed));
        TimeSystem.BeginWorld(GameWorld);
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
        SaveSystem.RequestSave();
    }
}
