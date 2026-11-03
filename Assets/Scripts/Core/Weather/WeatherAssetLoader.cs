using System;
using System.Diagnostics;
using System.Threading.Tasks;
using UnityEngine;

public static class WeatherAssetLoader
{
    private static WeatherBaseline cached;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetCache() { cached=null; }
    public static WeatherBaseline LoadBaseline()
    {
        // Only called on Unity's main thread. Background save decode does not touch Unity APIs.
        if(cached!=null) return cached;
        var asset=Resources.Load<WeatherBaselineAsset>("Weather/WeatherBaselineV2");
        if(asset==null) throw new InvalidOperationException("날씨 기준표 에셋을 찾을 수 없습니다.");
        cached=asset.LoadData();
        return cached;
    }
    public static WeatherSimulator Create(ulong seed) {
        var asset=Resources.Load<NectarSettings>("NectarV2");
        if(asset) asset.ValidateSettings();
        var parameters=asset ? JsonUtility.FromJson<NectarParameters>(JsonUtility.ToJson(asset.parameters)) : new NectarParameters();
        if(parameters.Sources?.Length!=8) throw new InvalidOperationException("밀원 8종 설정을 확인하세요.");
        byte[] seedBytes=new byte[sizeof(ulong)];
        using(var random=System.Security.Cryptography.RandomNumberGenerator.Create())
            random.GetBytes(seedBytes);
        return new WeatherSimulator(seed,LoadBaseline(),parameters,BitConverter.ToUInt64(seedBytes,0));
    }
    public static async Task PrepareLoadedWorldAsync(GameWorld world)
    {
        var data=LoadBaseline();
        if(world.Weather!=null) { world.Weather.BindBaseline(data); await world.Weather.RestoreLegacyNectarAsync(data); return; }
        // Schema 1 saves predate weather. Replay a fixed migration seed without changing any existing world data.
        var weather=new WeatherSimulator(WeatherSimulator.DefaultSeed,data);
        weather.BindApiaryArea(() => world.Apiary.Grid.UnlockedArea);
        var budget=Stopwatch.StartNew();
        while(weather.Minute<world.CurrentGameMinute)
        {
            weather.Advance(weather.Minute+1);
            if(budget.ElapsedMilliseconds>=4) { await Task.Yield(); budget.Restart(); }
        }
        world.AttachWeather(weather);
    }
}



