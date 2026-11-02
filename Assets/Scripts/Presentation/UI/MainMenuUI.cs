using System.Threading;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MainMenuUI : MonoBehaviour
{
    private Button resume, start;
    private TMP_Text status, message, preparation;
    private TMP_InputField year, month, day;
    private GameObject confirmation;
    private CancellationTokenSource preparing;
    private void Start()
    {
        var canvas=GetComponentInParent<Canvas>();
        if(canvas==null) foreach(var c in FindObjectsByType<Canvas>()) if(c.gameObject.scene==gameObject.scene) { canvas=c; break; }
        if(canvas==null) return;
        resume=GameSaveUI.CreateButton(canvas.transform,ActionButtonId.ContinueGame,()=>GameBootstrap.Instance?.ContinueGame());
        status=GameSaveUI.CreateText(canvas.transform,"SaveInfo",new Vector2(.5f,.5f),new Vector2(.5f,.5f),new Vector2(0,-75),new Vector2(720,70));
        status.alignment=TextAlignmentOptions.Center;
        confirmation=new GameObject("NewGameDate",typeof(RectTransform),typeof(Image));confirmation.transform.SetParent(canvas.transform,false);
        var r=(RectTransform)confirmation.transform;r.anchorMin=Vector2.zero;r.anchorMax=Vector2.one;r.offsetMin=r.offsetMax=Vector2.zero;
        confirmation.GetComponent<Image>().color=new Color(0,0,0,.95f);
        message=Label("Message",new Vector2(0,180),new Vector2(680,100),"새 게임 시작 날짜");
        year=Input("Year",-200,"년차","1");month=Input("Month",0,"월","1");day=Input("Day",200,"일","1");
        Label("Explanation",new Vector2(0,-25),new Vector2(680,75),"선택한 날짜 00:00에 시작합니다.\n그 날짜까지 날씨만 계산하며 장비의 열교환은 진행하지 않습니다.");
        preparation=Label("Preparation",new Vector2(0,-105),new Vector2(680,70),"");
        start=GameSaveUI.CreateButton(confirmation.transform,ActionButtonId.ConfirmNewGame,StartSelected);
        Place(start,-160,-190);SetButtonText(start,"새 게임 시작");
        var cancel=GameSaveUI.CreateButton(confirmation.transform,ActionButtonId.CancelNewGame,Cancel);
        Place(cancel,160,-190);SetButtonText(cancel,"취소");
        confirmation.SetActive(false);
    }
    private TMP_Text Label(string name,Vector2 pos,Vector2 size,string value)
    {var t=GameSaveUI.CreateText(confirmation.transform,name,new Vector2(.5f,.5f),new Vector2(.5f,.5f),pos,size);t.alignment=TextAlignmentOptions.Center;t.text=value;return t;}
    private TMP_InputField Input(string name,float x,string label,string initial)
    {
        Label(name+"Label",new Vector2(x,115),new Vector2(180,35),label);
        var go=new GameObject(name,typeof(RectTransform),typeof(Image),typeof(TMP_InputField));go.transform.SetParent(confirmation.transform,false);
        var rect=(RectTransform)go.transform;rect.anchorMin=rect.anchorMax=new Vector2(.5f,.5f);rect.sizeDelta=new Vector2(170,48);rect.anchoredPosition=new Vector2(x,70);
        go.GetComponent<Image>().color=new Color(.18f,.18f,.18f,1);
        var viewport=new GameObject("Viewport",typeof(RectTransform),typeof(RectMask2D));viewport.transform.SetParent(go.transform,false);
        var vr=(RectTransform)viewport.transform;vr.anchorMin=Vector2.zero;vr.anchorMax=Vector2.one;vr.offsetMin=new Vector2(8,3);vr.offsetMax=new Vector2(-8,-3);
        var text=GameSaveUI.CreateText(viewport.transform,"Text",Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero);text.alignment=TextAlignmentOptions.Center;text.color=Color.white;
        var input=go.GetComponent<TMP_InputField>();input.targetGraphic=go.GetComponent<Image>();input.textViewport=vr;input.textComponent=(TextMeshProUGUI)text;input.contentType=TMP_InputField.ContentType.IntegerNumber;input.characterLimit=8;input.text=initial;return input;
    }
    private static void Place(Button b,float x,float y){var r=(RectTransform)b.transform;r.anchorMin=r.anchorMax=new Vector2(.5f,.5f);r.anchoredPosition=new Vector2(x,y);r.sizeDelta=new Vector2(260,56);}
    private static void SetButtonText(Button b,string text){var t=b.GetComponentInChildren<TMP_Text>(true);if(t!=null)t.text=text;}
    private bool TryDate(out long y,out int m,out int d,out string error)
    {
        y=0;m=d=0;error="년차·월·일을 숫자로 입력하세요.";
        if(!long.TryParse(year.text,out y)||!int.TryParse(month.text,out m)||!int.TryParse(day.text,out d))return false;
        try{GameClock.ToMinute(y,m,d,0,0);error="";return true;}catch(System.Exception e){error=e.Message;return false;}
    }
    private async void StartSelected()
    {
        var bootstrap=GameBootstrap.Instance;if(bootstrap==null||preparing!=null)return;
        if(!TryDate(out long y,out int m,out int d,out string error)){preparation.text=error;return;}
        var token=new CancellationTokenSource();preparing=token;
        bool success=await bootstrap.StartNewGameAsync(y,m,d,token.Token);
        if(this!=null){preparing=null;if(success)confirmation.SetActive(false);else preparation.text=bootstrap.NewGameStatus;}
        token.Dispose();
    }
    private void Cancel(){if(preparing!=null)preparing.Cancel();else confirmation.SetActive(false);}
    private void OnDestroy(){preparing?.Cancel();}
    private void Update()
    {
        var bootstrap=GameBootstrap.Instance;var system=bootstrap?.SaveSystem;
        if(system==null||resume==null)return;
        bool busy=bootstrap.IsPreparingNewGame;
        resume.interactable=system.HasSave&&!system.IsLoading&&!system.IsSaving&&!GameTimeRuntime.InputBlocked&&!busy;
        status.text=system.HasSave?system.Status:"저장된 게임이 없습니다.";
        if(confirmation.activeSelf){year.interactable=month.interactable=day.interactable=!busy;start.interactable=!busy&&!system.IsSaving&&!system.IsLoading&&!GameTimeRuntime.InputBlocked;if(busy)preparation.text=bootstrap.NewGameStatus;}
    }
    public void OnNewGameClicked()
    {
        var b=GameBootstrap.Instance;if(b==null||b.IsPreparingNewGame||GameTimeRuntime.InputBlocked||b.SaveSystem.IsSaving||b.SaveSystem.IsLoading)return;
        if(confirmation==null)return;
        message.text=b.SaveSystem.HasSave?"새 게임 시작 날짜\n시작하면 현재 저장을 새 게임으로 바꿉니다.":"새 게임 시작 날짜";
        preparation.text="";confirmation.SetActive(true);confirmation.transform.SetAsLastSibling();
    }
}
