using System;
using System.IO;

internal sealed class WeatherWindows
{
    // Chronological last four samples. Sum in Excel's chronological order, including the current sample.
    private readonly double[] emission=new double[4], pass1=new double[4];
    public int Count { get; private set; }
    public double MeanEmission(double current) => Mean(emission,current);
    public double MeanPass1(double current) => Mean(pass1,current);
    private double Mean(double[] values,double current)
    { double sum=0; for(int i=0;i<Count;i++) sum+=values[i]; return (sum+current)/5d; }
    public void Commit(double a,double b)
    {
        if(Count==4) { Array.Copy(emission,1,emission,0,3); Array.Copy(pass1,1,pass1,0,3); }
        else Count++;
        emission[Count-1]=a; pass1[Count-1]=b;
    }
    public void Write(BinaryWriter w)
    { w.Write(Count); for(int i=0;i<Count;i++) { w.Write(emission[i]); w.Write(pass1[i]); } }
    public static WeatherWindows Read(BinaryReader r,long minute)
    {
        var result=new WeatherWindows(); int count=r.ReadInt32();
        if(count!=Math.Min(4,minute+1)) throw new InvalidDataException("Invalid rain history length.");
        for(int i=0;i<count;i++)
        {
            double a=WeatherBinary.Finite(r.ReadDouble()),b=WeatherBinary.Finite(r.ReadDouble());
            if(a<0||b<0) throw new InvalidDataException("Invalid rain history.");
            result.Commit(a,b);
        }
        return result;
    }
}
