using System;
using UnityEngine;

/// <summary>Native Unity asset pointing at the lossless, versioned baseline. No spreadsheet runtime dependency.</summary>
public sealed class WeatherBaselineAsset : ScriptableObject
{
    [SerializeField] private TextAsset data;
    public WeatherBaseline LoadData()
    {
        if(data==null) throw new InvalidOperationException("날씨 기준표 데이터가 연결되지 않았습니다.");
        return WeatherBaseline.FromBytes(data.bytes);
    }
}
