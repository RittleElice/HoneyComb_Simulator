using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MainMenuUI : MonoBehaviour
{
    private Button resume;
    private TMP_Text status;
    private GameObject confirmation;
    private void Start()
    {
        var canvas=GetComponentInParent<Canvas>();
        if(canvas==null) foreach(var c in FindObjectsByType<Canvas>()) if(c.gameObject.scene==gameObject.scene) { canvas=c; break; }
        if(canvas==null) return;
        resume=GameSaveUI.CreateButton(canvas.transform,ActionButtonId.ContinueGame,()=>GameBootstrap.Instance?.ContinueGame());
        status=GameSaveUI.CreateText(canvas.transform,"SaveInfo",new Vector2(.5f,.5f),new Vector2(.5f,.5f),new Vector2(0,-75),new Vector2(720,70));
        status.alignment=TextAlignmentOptions.Center;
        confirmation=new GameObject("ConfirmNewGame",typeof(RectTransform),typeof(Image)); confirmation.transform.SetParent(canvas.transform,false);
        var r=(RectTransform)confirmation.transform; r.anchorMin=Vector2.zero; r.anchorMax=Vector2.one; r.offsetMin=r.offsetMax=Vector2.zero;
        confirmation.GetComponent<Image>().color=new Color(0,0,0,.93f);
        var text=GameSaveUI.CreateText(confirmation.transform,"Message",new Vector2(.5f,.5f),new Vector2(.5f,.5f),new Vector2(0,35),new Vector2(700,100));
        text.alignment=TextAlignmentOptions.Center; text.text="새 게임을 시작하면 현재 저장을 새 게임으로 바꿉니다.";
        GameSaveUI.CreateButton(confirmation.transform,ActionButtonId.ConfirmNewGame,()=>{ confirmation.SetActive(false); GameBootstrap.Instance.StartNewGame(); });
        GameSaveUI.CreateButton(confirmation.transform,ActionButtonId.CancelNewGame,()=>confirmation.SetActive(false));
        confirmation.SetActive(false);
    }
    private void Update()
    {
        var system=GameBootstrap.Instance?.SaveSystem;
        if(system==null||resume==null) return;
        resume.interactable=system.HasSave && !system.IsLoading && !system.IsSaving && !GameTimeRuntime.InputBlocked;
        status.text=system.HasSave?system.Status:"저장된 게임이 없습니다.";
    }
    public void OnNewGameClicked()
    {
        var bootstrap=GameBootstrap.Instance;
        if(bootstrap==null||GameTimeRuntime.InputBlocked||bootstrap.SaveSystem.IsSaving) return;
        if(bootstrap.SaveSystem.HasSave && confirmation!=null) confirmation.SetActive(true);
        else bootstrap.StartNewGame();
    }
}
