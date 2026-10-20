using HoneyComb.Simulation.Environment.Weather;
using UnityEngine;
namespace HoneyComb.Unity.Presentation
{
    public sealed class WeatherDebugPanel
    {
        private Vector2 scroll;
        public void Draw(WeatherSample value, float width, float height)
        {
            var heading=new GUIStyle(GUI.skin.label){fontSize=22,fontStyle=FontStyle.Bold,wordWrap=true};
            var label=new GUIStyle(GUI.skin.label){fontSize=18,wordWrap=true};
            GUILayout.BeginArea(new Rect(0,0,width,height));
            scroll=GUILayout.BeginScrollView(scroll);
            GUILayout.Label("날씨 생성값",heading);
            long minute=value.Minute;
            GUILayout.Label($"Year {minute/525600+1} / Day {(minute%525600)/1440+1}  {minute/60%24:00}:{minute%60:00}",label);
            GUILayout.Label("게임 시간 1분마다 갱신됩니다.",label);
            GUILayout.Space(10);
            Row("운량", $"{value.CloudCover*100:0.00}%  ({value.CloudCover*10:0.000}/10)",label);
            Row("풍속", $"{value.WindSpeedMetersPerSecond:0.000} m/s",label);
            Row("기온", $"{value.TemperatureCelsius:0.000} °C",label);
            Row("상대습도", $"{value.RelativeHumidityPercent:0.000}%",label);
            Row("수증기압", $"{value.VaporPressureHpa:0.000} hPa",label);
            GUILayout.Space(10);
            Row("강수량 · 1분 구간", $"{value.PrecipitationMm:0.######} mm",label);
            Row("강수 강도 · 시간당 환산", $"{value.PrecipitationMmPerHour:0.######} mm/h",label);
            Row("액체 강수량 · 1분 구간", $"{value.LiquidRainMm:0.######} mm",label);
            Row("강설 수량 · 물로 환산 / 1분", $"{value.SnowfallWaterMm:0.######} mm",label);
            Row("융설 수량 · 1분 구간", $"{value.SnowMeltMm:0.######} mm",label);
            Row("적설 깊이", $"{value.SnowDepthCm:0.######} cm",label);
            GUILayout.Space(10);
            Row("일사 에너지 · 1분 구간", $"{value.SolarEnergyMJPerM2:0.########} MJ/m²",label);
            Row("일사 강도 · 구간 평균 환산", $"{value.SolarEnergyMJPerM2*1000000/60:0.000} W/m²",label);
            Row("화면용 밝기", $"{value.Daylight:0.0000} / 1",label);
            Row("토양 수분 저장량", $"{value.SoilWaterMm:0.000} mm",label);
            GUILayout.Space(10);
            GUILayout.Label("강수량은 비와 눈의 수량 합계입니다. 강설 수량과 적설 깊이는 서로 다른 값입니다. 토양 수분은 저장량이며 백분율이 아닙니다.",label);
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }
        private static void Row(string name,string value,GUIStyle style)
        {
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label(name,style);
            GUILayout.Label(value,style);
            GUILayout.EndVertical();
        }
    }
}
