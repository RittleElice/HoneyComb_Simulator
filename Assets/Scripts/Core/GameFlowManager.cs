using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameFlowManager
{
    private const string MainMenuScene = "MainMenu";
    private const string SharedUIScene = "SharedUI";
    private const string ApiaryScene = "Apiary";
    private const string ObservationScene = "Observation";

    private readonly MonoBehaviour runner;
    private readonly ApiaryCameraState apiaryCameraState =
        new ApiaryCameraState();

    private bool isTransitioning;

    public bool IsTransitioning => isTransitioning;

    public GameFlowManager(MonoBehaviour coroutineRunner)
    {
        runner = coroutineRunner;
    }

    public void OpenMainMenu()
    {
        if (isTransitioning || GameTimeRuntime.InputBlocked)
            return;

        runner.StartCoroutine(OpenMainMenuRoutine());
    }

    public void OpenApiary()
    {
        if (isTransitioning || GameTimeRuntime.InputBlocked)
            return;

        runner.StartCoroutine(OpenApiaryRoutine());
    }

    public void OpenObservation(int gridX, int gridY)
    {
        if (isTransitioning || GameTimeRuntime.InputBlocked)
            return;

        runner.StartCoroutine(
            OpenObservationRoutine(gridX, gridY));
    }

    private IEnumerator OpenMainMenuRoutine()
    {
        isTransitioning = true;

        yield return SceneManager.LoadSceneAsync(
            MainMenuScene,
            LoadSceneMode.Single);

        isTransitioning = false;
    }

    private IEnumerator OpenApiaryRoutine()
    {
        isTransitioning = true;

        if (GameBootstrap.Instance == null ||
            GameBootstrap.Instance.GameWorld == null)
        {
            Debug.LogError("[GameFlow] GameWorld가 없습니다.");
            isTransitioning = false;
            yield break;
        }

        // 공통 UI 유지
        if (!IsSceneLoaded(SharedUIScene))
        {
            yield return SceneManager.LoadSceneAsync(
                SharedUIScene,
                LoadSceneMode.Additive);
        }

        // 관찰 카메라 참조
        Camera oldCamera = FindSceneCamera(ObservationScene);

        // 양봉장 씬을 먼저 로드
        if (!IsSceneLoaded(ApiaryScene))
        {
            yield return SceneManager.LoadSceneAsync(
                ApiaryScene,
                LoadSceneMode.Additive);
        }

        Scene apiaryScene =
            SceneManager.GetSceneByName(ApiaryScene);

        if (apiaryScene.IsValid() && apiaryScene.isLoaded)
            SceneManager.SetActiveScene(apiaryScene);

        Camera newCamera = FindSceneCamera(ApiaryScene);

        // 저장된 카메라 상태 복원
        if (newCamera != null &&
            apiaryCameraState.HasSavedState)
        {
            apiaryCameraState.Restore(newCamera);
        }

        // 이전 카메라 비활성화
        if (oldCamera != null)
            SetCameraActive(oldCamera, false);

        // 새 카메라 활성화
        if (newCamera != null)
            SetCameraActive(newCamera, true);
        
        // 양봉장 카메라가 준비된 후 메인 메뉴 제거
        yield return UnloadIfLoaded(MainMenuScene);

        // 관찰 씬 제거
        yield return UnloadIfLoaded(ObservationScene);

        isTransitioning = false;
    }

    private IEnumerator OpenObservationRoutine(
        int gridX,
        int gridY)
    {
        isTransitioning = true;

        ObservationContext.GridX = gridX;
        ObservationContext.GridY = gridY;

        // 양봉장 카메라 상태 저장
        Camera oldCamera = FindSceneCamera(ApiaryScene);

        if (oldCamera != null)
        {
            apiaryCameraState.Save(oldCamera);
        }

        // 관찰 씬을 먼저 로드
        if (!IsSceneLoaded(ObservationScene))
        {
            yield return SceneManager.LoadSceneAsync(
                ObservationScene,
                LoadSceneMode.Additive);
        }

        Scene observationScene =
            SceneManager.GetSceneByName(ObservationScene);

        if (observationScene.IsValid() &&
            observationScene.isLoaded)
        {
            SceneManager.SetActiveScene(observationScene);
        }

        Camera newCamera =
            FindSceneCamera(ObservationScene);

        // 이전 카메라 비활성화
        if (oldCamera != null)
            SetCameraActive(oldCamera, false);

        // 새 카메라 활성화
        if (newCamera != null)
            SetCameraActive(newCamera, true);

        // 양봉장 씬 제거
        yield return UnloadIfLoaded(ApiaryScene);

        isTransitioning = false;
    }

    private static Camera FindSceneCamera(string sceneName)
    {
        Scene scene =
            SceneManager.GetSceneByName(sceneName);

        if (!scene.IsValid() || !scene.isLoaded)
            return null;

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            Camera camera =
                root.GetComponentInChildren<Camera>(true);

            if (camera != null)
                return camera;
        }

        return null;
    }

    private static void SetCameraActive(
        Camera camera,
        bool active)
    {
        if (camera == null)
            return;

        camera.enabled = active;

        AudioListener listener =
            camera.GetComponent<AudioListener>();

        if (listener != null)
            listener.enabled = active;
    }

    private static bool IsSceneLoaded(string sceneName)
    {
        Scene scene =
            SceneManager.GetSceneByName(sceneName);

        return scene.IsValid() && scene.isLoaded;
    }

    private static IEnumerator UnloadIfLoaded(string sceneName)
    {
        Scene scene =
            SceneManager.GetSceneByName(sceneName);

        if (scene.IsValid() && scene.isLoaded)
        {
            yield return SceneManager.UnloadSceneAsync(scene);
        }
    }

    public void ResetApiaryCamera()
    {
        apiaryCameraState.Clear();
    }
}
