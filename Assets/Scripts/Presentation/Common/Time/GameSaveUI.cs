using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class GameSaveUI : MonoBehaviour
{
    private Button save;
    private TMP_Text status;
    public static Button CreateButton(Transform parent, ActionButtonId id, UnityEngine.Events.UnityAction action)
    {
        var button=ActionButtonManager.Create(null,parent,id);
        button.onClick.AddListener(action);
        return button;
    }
    public static TMP_Text CreateText(Transform parent,string name,Vector2 min,Vector2 max,Vector2 position,Vector2 size)
    {
        var go=new GameObject(name,typeof(RectTransform),typeof(TextMeshProUGUI)); go.transform.SetParent(parent,false);
        var text=go.GetComponent<TMP_Text>(); text.font=TMP_Settings.defaultFontAsset; text.fontSize=20; text.raycastTarget=false;
        var r=text.rectTransform; r.anchorMin=min; r.anchorMax=max; r.pivot=new Vector2(.5f,.5f); r.anchoredPosition=position; r.sizeDelta=size;
        return text;
    }
    private void Start()
    {
        save=CreateButton(transform,ActionButtonId.SaveGame,()=>GameBootstrap.Instance?.SaveSystem?.RequestSave());
        status=CreateText(transform,"SaveStatus",Vector2.one,Vector2.one,new Vector2(-230,-72),new Vector2(440,40));
        status.alignment=TextAlignmentOptions.Right; status.enableAutoSizing=true; status.fontSizeMin=12; status.fontSizeMax=20;
    }
    private void Update()
    {
        var system=GameBootstrap.Instance?.SaveSystem;
        if(system==null) return;
        save.interactable=!GameTimeRuntime.InputBlocked && !system.IsSaving;
        status.text=system.Status;
    }
}
