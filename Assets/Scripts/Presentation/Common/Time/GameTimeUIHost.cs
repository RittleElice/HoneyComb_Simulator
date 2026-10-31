using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Mounts one common prefab in the existing MainMenu/SharedUI canvas.</summary>
public sealed class GameTimeUIHost : MonoBehaviour
{
    private GameTimeUI view;
    private GameTimeUIProfile profile;
    private string placedScene;
    private GameObject blocker;
    private TMP_Text progress;
    private void Start()
    {
        profile = Resources.Load<GameTimeUIProfile>("GameTimeUIProfile");
        var canvasObject = new GameObject("TimeCalculationOverlay", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);
        var canvas = canvasObject.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 32760;
        var scaler = canvasObject.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920,1080);
        blocker = new GameObject("InputBlocker", typeof(RectTransform), typeof(Image)); blocker.transform.SetParent(canvasObject.transform, false);
        var rect = (RectTransform)blocker.transform; rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
        blocker.GetComponent<Image>().color = new Color(0,0,0,.78f);
        var text = new GameObject("Progress", typeof(RectTransform), typeof(TextMeshProUGUI)); text.transform.SetParent(blocker.transform, false);
        progress = text.GetComponent<TMP_Text>(); progress.alignment = TextAlignmentOptions.Center; progress.fontSize = 30; progress.raycastTarget = false;
        progress.rectTransform.anchorMin = new Vector2(.1f,.35f); progress.rectTransform.anchorMax = new Vector2(.9f,.65f); progress.rectTransform.offsetMin = progress.rectTransform.offsetMax = Vector2.zero;
        blocker.SetActive(false);
    }
    private void LateUpdate()
    {
        var runtime = GameBootstrap.Instance?.TimeSystem;
        if (runtime == null) return;
        string scene = SceneManager.GetActiveScene().name;
        if (view == null)
        {
            string uiScene = scene == "MainMenu" ? "MainMenu" : "SharedUI";
            foreach (var canvas in FindObjectsByType<Canvas>())
            {
                if (!canvas.isRootCanvas || canvas.gameObject.scene.name != uiScene) continue;
                var prefab = Resources.Load<GameTimeUI>("GameTimeUI");
                if (prefab != null) view = Instantiate(prefab, canvas.transform, false);
                if (uiScene == "SharedUI" && canvas.GetComponentInChildren<GameSaveUI>() == null)
                {
                    var saveUI = new GameObject("GameSaveUI", typeof(RectTransform), typeof(GameSaveUI));
                    saveUI.transform.SetParent(canvas.transform, false);
                    var rect = (RectTransform)saveUI.transform; rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
                }
                placedScene = null; break;
            }
        }
        if (view != null)
        {
            if (placedScene != scene && profile != null)
            {
                var placement = profile.Find(scene);
                if (placement != null) { view.Place(placement); placedScene = scene; }
            }
            view.Refresh(runtime.Clock);
        }
        if (blocker != null)
        {
            blocker.SetActive(runtime.Blocked);
            if (runtime.Blocked)
            {
                var scheduler = runtime.Scheduler;
                double fraction = scheduler.Total == 0 ? 0 : (double)scheduler.Completed / scheduler.Total;
                progress.text = runtime.Status + (runtime.Failure == null ? $"\n{fraction:P1}" : "\n재시작 후 정상 저장본이 있으면 이어갈 수 있습니다.");
            }
        }
    }
}
