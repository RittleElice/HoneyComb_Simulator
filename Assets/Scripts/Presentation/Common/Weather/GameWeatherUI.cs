using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Read-only common weather view. Opening the window never advances or changes weather.</summary>
public sealed class GameWeatherUI : MonoBehaviour
{
    public Button OpenButton { get; private set; }
    public Button CloseButton { get; private set; }
    public bool IsOpen => panel != null && panel.gameObject.activeSelf;
    public long DisplayedMinute { get; private set; } = -1;
    private RectTransform panel;
    private TMP_Text date;
    private readonly TMP_Text[] values = new TMP_Text[8];
    private GameWorld displayedWorld;
    private static readonly string[] Labels = { "기온", "상대습도", "구름량", "풍속", "이번 분 강수량", "수증기압", "이번 분 일사량", "토양수분 저장량" };

    private void Start()
    {
        OpenButton = GameSaveUI.CreateButton(transform, ActionButtonId.Weather, Toggle);
        OpenButton.name = "WeatherButton";
        var window = new GameObject("WeatherWindow", typeof(RectTransform), typeof(Image));
        window.transform.SetParent(transform, false);
        panel = (RectTransform)window.transform;
        panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(0, 1);
        panel.anchoredPosition = new Vector2(16, -82);
        window.GetComponent<Image>().color = new Color(.075f, .12f, .17f, .98f);
        // The panel receives raycasts so dragging/clicking its contents does not select items behind it.
        Text("Title", "현재 날씨", new Vector2(0, 1), new Vector2(1, 1), new Vector2(24, -58), new Vector2(-112, -14), 28);
        date = Text("WeatherDate", "", new Vector2(0, 1), new Vector2(1, 1), new Vector2(24, -94), new Vector2(-24, -58), 18);
        date.color = new Color(.7f, .8f, .87f);
        var bodyObject = new GameObject("WeatherValues", typeof(RectTransform));
        bodyObject.transform.SetParent(panel, false);
        var body = (RectTransform)bodyObject.transform;
        body.anchorMin = Vector2.zero; body.anchorMax = Vector2.one;
        body.offsetMin = new Vector2(24,16); body.offsetMax = new Vector2(-24,-106);
        for (int i = 0; i < Labels.Length; i++)
        {
            float top = 1 - i / 8f, bottom = 1 - (i + 1) / 8f;
            var row = new GameObject("WeatherRow" + i, typeof(RectTransform));
            row.transform.SetParent(body, false);
            var rect = (RectTransform)row.transform;
            rect.anchorMin = new Vector2(0, bottom); rect.anchorMax = new Vector2(1, top);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            // Rows occupy the body below the title, with heights proportional to the window.
            var label = GameSaveUI.CreateText(row.transform,"Label",Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero);
            label.text = Labels[i]; label.fontSize = 20; label.alignment = TextAlignmentOptions.MidlineLeft;
            label.rectTransform.anchorMax = new Vector2(.63f, 1); label.color = new Color(.8f,.86f,.91f);
            values[i] = GameSaveUI.CreateText(row.transform,"Value",Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero);
            values[i].rectTransform.anchorMin = new Vector2(.63f,0);
            values[i].alignment = TextAlignmentOptions.MidlineRight; values[i].fontSize = 22;
            values[i].enableAutoSizing = true; values[i].fontSizeMin = 16; values[i].fontSizeMax = 22;
            values[i].color = Color.white;
        }
        CloseButton = GameSaveUI.CreateButton(panel, ActionButtonId.CloseWeather, Close);
        CloseButton.name = "CloseWeatherButton";
        panel.gameObject.SetActive(false);
        FitWindow();
    }
    private TMP_Text Text(string name,string content,Vector2 min,Vector2 max,Vector2 offsetMin,Vector2 offsetMax,float size)
    {
        var text=GameSaveUI.CreateText(panel,name,min,max,Vector2.zero,Vector2.zero);
        text.rectTransform.offsetMin=offsetMin;text.rectTransform.offsetMax=offsetMax;
        text.text=content;text.fontSize=size;text.color=Color.white;text.alignment=TextAlignmentOptions.MidlineLeft;
        return text;
    }
    public void Toggle()
    {
        if (GameTimeRuntime.InputBlocked || GameBootstrap.Instance?.GameWorld?.Weather == null) return;
        if (IsOpen) Close();
        else { panel.gameObject.SetActive(true); panel.SetAsLastSibling(); DisplayedMinute=-1; Refresh(); }
    }
    public void Close() { if(panel!=null)panel.gameObject.SetActive(false); }
    private void Update()
    {
        if(OpenButton==null)return;
        var world=GameBootstrap.Instance?.GameWorld;
        bool ready=world?.Weather!=null;
        OpenButton.interactable=ready&&!GameTimeRuntime.InputBlocked;
        CloseButton.interactable=!GameTimeRuntime.InputBlocked;
        if(!ready) { Close();return; }
        if(IsOpen) { FitWindow();Refresh(); }
    }
    private void FitWindow()
    {
        if(panel==null)return;
        var bounds=((RectTransform)transform).rect;
        panel.sizeDelta=new Vector2(Mathf.Min(500,Mathf.Max(280,bounds.width-32)),Mathf.Min(480,Mathf.Max(300,bounds.height-98)));
    }
    private void Refresh()
    {
        var world=GameBootstrap.Instance?.GameWorld;
        if(world?.Weather==null)return;
        var weather=world.Weather.Current;
        if(ReferenceEquals(world,displayedWorld)&&DisplayedMinute==weather.Minute)return;
        displayedWorld=world;DisplayedMinute=weather.Minute;date.text=world.Clock.ToString();
        var culture=CultureInfo.InvariantCulture;
        values[0].text=weather.TemperatureCelsius.ToString("0.0",culture)+" 도 (섭씨)";
        values[1].text=weather.RelativeHumidityPercent.ToString("0.0",culture)+" %";
        values[2].text=(weather.CloudFraction*100).ToString("0.0",culture)+" %";
        values[3].text=weather.WindSpeed.ToString("0.00",culture);
        values[4].text=weather.Precipitation.ToString("0.0000",culture);
        values[5].text=weather.VaporPressureHpa.ToString("0.00",culture)+" hPa";
        values[6].text=weather.SolarEnergy.ToString("0.00000",culture);
        values[7].text=weather.SoilWaterStorage.ToString("0.00",culture);
    }
}
