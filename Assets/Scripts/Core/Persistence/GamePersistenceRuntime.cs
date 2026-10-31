using System;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;

[DefaultExecutionOrder(-900)]
public sealed class GamePersistenceRuntime : MonoBehaviour
{
    private WorldSaveStore store;
    private Task write;
    private double started, lastWrite, retryAfter;
    private bool requested;
    private readonly CheckpointPolicy policy = new CheckpointPolicy();
    public bool IsLoading { get; private set; }
    public bool IsSaving => write != null;
    public bool HasSave => store != null && store.HasFiles;
    public string Status { get; private set; } = "아직 저장되지 않았습니다.";
    public string SaveDirectory => store.DirectoryPath;
    public bool CanSnapshot
    {
        get
        {
            var b=GameBootstrap.Instance; var time=b?.TimeSystem;
            return !IsLoading && b != null && ReferenceEquals(b.SaveSystem, this) && b.GameWorld != null && time != null && time.HasWorld && ReferenceEquals(b.GameWorld.Clock, time.Clock) && time.Failure == null && time.Scheduler.Fault == null &&
                time.CanCaptureWorld(b.GameWorld);
        }
    }
    private void Awake()
    {
        string directory=Path.Combine(Application.persistentDataPath,"Saves");
#if UNITY_EDITOR
        // Batch tests use an isolated location and must never touch the player's save files.
        string testDirectory=Environment.GetEnvironmentVariable("HONEYCOMB_SAVE_TEST_ROOT");
        if(!string.IsNullOrEmpty(testDirectory)) directory=testDirectory;
#endif
        store=new WorldSaveStore(directory); lastWrite=Time.realtimeSinceStartupAsDouble;
        if (store.HasFiles) Status="저장된 게임을 이어갈 수 있습니다.";
    }
    public void RequestSave()
    {
        requested=true;
        if(GameBootstrap.Instance?.GameWorld != null && !GameBootstrap.Instance.TimeSystem.CanCaptureWorld(GameBootstrap.Instance.GameWorld))
            Status="저장 불가: 등록된 시뮬레이션의 상태 저장 구현이 필요합니다.";
    }
    private void Update()
    {
        if(write!=null && write.IsCompleted) Complete(write);
        if(!CanSnapshot || write!=null || Time.realtimeSinceStartupAsDouble<retryAfter) return;
        if(requested || policy.IsDue(Time.realtimeSinceStartupAsDouble-lastWrite)) _ = SaveBoundaryAsync(false);
    }
    public async Task SaveBoundaryAsync(bool force)
    {
        while(write!=null) { var existing=write; try { await existing; } catch { } Complete(existing); }
        if(!CanSnapshot)
        {
            if(force) { requested=true; Status="저장 대기 중 — 안전한 계산 경계가 필요합니다."; }
            return;
        }
        if(Time.realtimeSinceStartupAsDouble<retryAfter || (!force && !requested && !policy.IsDue(Time.realtimeSinceStartupAsDouble-lastWrite))) return;
        try
        {
            // Capture on the main thread at a complete minute boundary. IO uses only detached bytes.
            started=Time.realtimeSinceStartupAsDouble;
            byte[] payload=WorldSaveCodec.Encode(GameBootstrap.Instance.TimeSystem.CaptureTimeline(GameBootstrap.Instance.GameWorld));
            requested=false; Status="저장 중…";
            write=Task.Run(()=>store.Save(payload)); var task=write;
            try { await task; } catch { } Complete(task);
        }
        catch(Exception e) { Failed(e); }
    }
    private void Complete(Task task)
    {
        if(!ReferenceEquals(write,task)) return;
        write=null;
        try
        {
            task.GetAwaiter().GetResult(); lastWrite=Time.realtimeSinceStartupAsDouble;
            policy.ObserveWriteCost(Math.Max(0,lastWrite-started));
            Status="저장 완료 "+DateTime.Now.ToString("HH:mm:ss");
        }
        catch(Exception e) { Failed(e); }
    }
    private void Failed(Exception e)
    {
        requested=true; retryAfter=Time.realtimeSinceStartupAsDouble+10;
        Status="저장 실패: "+e.Message; Debug.LogWarning("[Save] "+e);
    }
    public void SaveSynchronously()
    {
        // OS shutdown callbacks are best effort. Previously flushed generations remain recoverable.
        if(write!=null) { var task=write; try { task.GetAwaiter().GetResult(); } catch { } Complete(task); }
        if(!CanSnapshot) { requested=true; return; }
        try
        {
            double start=Time.realtimeSinceStartupAsDouble;
            var bytes=WorldSaveCodec.Encode(GameBootstrap.Instance.TimeSystem.CaptureTimeline(GameBootstrap.Instance.GameWorld));
            store.Save(bytes); lastWrite=Time.realtimeSinceStartupAsDouble; policy.ObserveWriteCost(lastWrite-start);
            requested=false; Status="저장 완료 "+DateTime.Now.ToString("HH:mm:ss");
        }
        catch(Exception e) { Failed(e); }
    }
    public async Task<WorldSaveSnapshot> LoadAsync()
    {
        if(IsLoading || IsSaving) throw new InvalidOperationException("저장 작업이 진행 중입니다.");
        IsLoading=true; Status="저장 불러오는 중…";
        try
        {
            bool recovered=false; var snapshot=await Task.Run(()=>store.Load(out recovered));
            await WeatherAssetLoader.PrepareLoadedWorldAsync(snapshot.World);
            Status=recovered?"이전 정상 저장본으로 복구했습니다.":"저장을 불러왔습니다.";
            return snapshot;
        }
        catch(Exception e) { Status="불러오기 실패: "+e.Message; throw; }
        finally { IsLoading=false; }
    }
    private void OnApplicationQuit() => SaveSynchronously();
}
