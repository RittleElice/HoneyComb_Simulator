using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

/// <summary>Immutable, full-precision 365-day baseline. Repeats annually; stochastic states never reset.</summary>
public sealed class WeatherBaseline
{
    public const int MinutesPerYear = 525600;
    private readonly double[] values;
    private WeatherBaseline(double[] values) { this.values=values; }
    public static WeatherBaseline FromBytes(byte[] bytes)
    {
        if (bytes == null || bytes.Length != 16 + MinutesPerYear * 8 * 8)
            throw new InvalidDataException("날씨 기준표 크기가 맞지 않습니다.");
        using (var sha = SHA256.Create())
        {
            string hash=BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-","").ToLowerInvariant();
            if (hash != WeatherModelVersion.BaselineSha256) throw new InvalidDataException("날씨 기준표 버전/해시가 맞지 않습니다.");
        }
        using (var reader=new BinaryReader(new MemoryStream(bytes,false)))
        {
            if (Encoding.ASCII.GetString(reader.ReadBytes(8))!="HCBWV200" || reader.ReadInt32()!=MinutesPerYear || reader.ReadInt32()!=8)
                throw new InvalidDataException("날씨 기준표 형식이 맞지 않습니다.");
            var values=new double[MinutesPerYear*8];
            for(int i=0;i<values.Length;i++) values[i]=WeatherBinary.Finite(reader.ReadDouble());
            return new WeatherBaseline(values);
        }
    }
    internal void Fill(long minute, WeatherValues target)
    {
        int i=checked((int)(minute % MinutesPerYear)*8);
        target.Cloud_Baseline=values[i]; target.Wind_Baseline=values[i+1];
        target.Wind_Season_Baseline=values[i+2]; target.Rain_Supply_Baseline=values[i+3];
        target.Vapor_Baseline=values[i+4]; target.Cloud_Avg=values[i+5];
        target.Solar_Baseline=values[i+6]; target.AirMass=values[i+7];
    }
}
internal static class WeatherBinary
{
    public static double Finite(double value)
    {
        if (double.IsNaN(value)||double.IsInfinity(value)) throw new InvalidDataException("Non-finite weather state.");
        return value;
    }
}
