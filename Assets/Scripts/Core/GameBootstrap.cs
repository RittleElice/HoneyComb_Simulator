using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

public class GameBootstrap : MonoBehaviour
{
    public static GameBootstrap Instance { get; private set; }

    public bool IsPreparingNewGame { get; private set; }
    public string NewGameStatus { get; private set; } = "";
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
        if (IsPreparingNewGame || GameFlow.IsTransitioning || GameTimeRuntime.InputBlocked || SaveSystem.IsSaving || GameWorld != null) return;
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

    public async void StartNewGame() => await StartNewGameAsync(1, 1, 1, CancellationToken.None);

    public async Task<bool> StartNewGameAsync(long year, int month, int day, CancellationToken cancellation)
    {
        if (IsPreparingNewGame || GameFlow.IsTransitioning || GameTimeRuntime.InputBlocked || SaveSystem.IsSaving) return false;
        try
        {
            long target = GameClock.ToMinute(year, month, day, 0, 0);
            IsPreparingNewGame = true;
            NewGameStatus = "시작 날짜의 날씨를 준비하고 있습니다…";
            await TimeSystem.NextFrame();
            var candidate = await NewGameWeatherPreparation.CreateAsync(target,
                WeatherAssetLoader.Create(WeatherSimulator.DefaultSeed), TimeSystem.NextFrame,
                TimeSystem.WaitForResumeAsync, cancellation,
                (done, total) => NewGameStatus = total == 0 ? "새 게임 준비 완료" : $"날씨 준비 {100d * done / total:F0}%");
            cancellation.ThrowIfCancellationRequested();
            await TimeSystem.WaitForResumeAsync();
            cancellation.ThrowIfCancellationRequested();
            // Publish only a completed candidate. BeginWorld initializes equipment at the selected weather.
            IsPreparingNewGame = false;
            TimeSystem.BeginWorld(candidate);
            GameWorld = candidate;
            GameFlow.ResetApiaryCamera();
            GameFlow.OpenApiary();
            SaveSystem.RequestSave();
            NewGameStatus = "";
            return true;
        }
        catch (OperationCanceledException) { NewGameStatus = "새 게임 준비를 취소했습니다."; return false; }
        catch (Exception e) { NewGameStatus = "새 게임 준비 실패: " + e.Message; Debug.LogWarning(NewGameStatus); return false; }
        finally { IsPreparingNewGame = false; }
    }
}

// The provisional world is not published or saved until weather replay succeeds.
public static class NewGameWeatherPreparation
{
    public static async Task<GameWorld> CreateAsync(long target, WeatherSimulator weather,
        Func<Task> yieldFrame, Func<Task> waitUntilForeground, CancellationToken cancellation,
        Action<long,long> progress = null)
    {
        if (target < 0 || weather == null || weather.Minute != 0) throw new ArgumentOutOfRangeException(nameof(target));
        cancellation.ThrowIfCancellationRequested();
        var world = new GameWorld(weather);
        var scheduler = new SimulationScheduler(world.Clock);
        scheduler.Register(weather); // Deliberately no thermal/equipment or other simulation participants.
        progress?.Invoke(0, target);
        await scheduler.RunToAsync(target, async () => {
            progress?.Invoke(scheduler.Completed, scheduler.Total);
            await yieldFrame();
        }, waitUntilForeground, cancellation);
        cancellation.ThrowIfCancellationRequested();
        progress?.Invoke(target, target);
        return world;
    }
}
