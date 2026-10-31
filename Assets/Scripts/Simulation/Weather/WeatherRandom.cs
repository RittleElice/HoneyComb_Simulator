using System;
using System.IO;

internal enum WeatherStream { CloudSlow, CloudFast, WindDay, WindMinute, RainSupply, RainIntensity, RainInitialSmooth, RainDecision, VaporVariation }
internal interface IWeatherRandom
{
    double Uniform(WeatherStream stream);
    double Normal(WeatherStream stream);
}

// Versioned PCG32 streams, no dependency on UnityEngine.Random/System.Random or wall time.
internal sealed class WeatherRandom : IWeatherRandom
{
    private readonly ulong[] states = new ulong[9];
    public WeatherRandom(ulong seed)
    {
        unchecked
        {
            for (int i = 0; i < states.Length; i++)
            {
                Next(i);
                seed += 0x9e3779b97f4a7c15UL;
                ulong z = seed;
                z = (z ^ (z >> 30)) * 0xbf58476d1ce4e5b9UL;
                z = (z ^ (z >> 27)) * 0x94d049bb133111ebUL;
                states[i] += z ^ (z >> 31);
                Next(i);
            }
        }
    }
    private uint Next(int stream)
    {
        unchecked
        {
            ulong old = states[stream];
            states[stream] = old * 6364136223846793005UL + (ulong)(stream * 2 + 1);
            uint x = (uint)(((old >> 18) ^ old) >> 27);
            int rotation = (int)(old >> 59);
            return (x >> rotation) | (x << ((-rotation) & 31));
        }
    }
    public double Uniform(WeatherStream stream) => (Next((int)stream) + 0.5d) / 4294967296d;
    public double Normal(WeatherStream stream) => InverseNormal(Uniform(stream));

    // Acklam rational inverse-normal approximation. One uniform per Gaussian, no spare cache.
    internal static double InverseNormal(double p)
    {
        if (!(p > 0 && p < 1)) throw new ArgumentOutOfRangeException(nameof(p));
        if (p < .02425 || p > .97575)
        {
            double q = Math.Sqrt(-2 * Math.Log(p < .02425 ? p : 1 - p));
            double value = (((((-7.784894002430293e-3*q-3.223964580411365e-1)*q-2.400758277161838)*q-2.549732539343734)*q+4.374664141464968)*q+2.938163982698783) /
                ((((7.784695709041462e-3*q+3.224671290700398e-1)*q+2.445134137142996)*q+3.754408661907416)*q+1);
            return p < .02425 ? value : -value;
        }
        double x = p - .5, r = x*x;
        return (((((-3.969683028665376e1*r+2.209460984245205e2)*r-2.759285104469687e2)*r+1.383577518672690e2)*r-3.066479806614716e1)*r+2.506628277459239)*x /
            (((((-5.447609879822406e1*r+1.615858368580409e2)*r-1.556989798598866e2)*r+6.680131188771972e1)*r-1.328068155288572e1)*r+1);
    }
    public void Write(BinaryWriter w) { foreach (ulong state in states) w.Write(state); }
    public static WeatherRandom Read(BinaryReader r, ulong seed)
    {
        var result = new WeatherRandom(seed);
        for (int i=0;i<result.states.Length;i++) result.states[i]=r.ReadUInt64();
        return result;
    }
}
