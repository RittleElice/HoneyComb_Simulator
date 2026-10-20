using System;
namespace HoneyComb.Simulation.Environment.Weather
{
    public sealed class WeatherBaseline
    {
        public const int MinutesPerYear=525600, Columns=8;
        private readonly double[] values;
        // Little endian doubles, minute-major. See source.json for provenance and column units.
        public WeatherBaseline(byte[] bytes)
        {
            if(bytes==null || bytes.Length!=MinutesPerYear*Columns*8)
                throw new ArgumentException("Expected 525600 rows of eight baseline values.");
            values=new double[MinutesPerYear*Columns];
            if(BitConverter.IsLittleEndian) Buffer.BlockCopy(bytes,0,values,0,bytes.Length);
            else for(int i=0;i<values.Length;i++){var b=new byte[8];Array.Copy(bytes,i*8,b,0,8);Array.Reverse(b);values[i]=BitConverter.ToDouble(b,0);}
            for(int i=0;i<values.Length;i++)
                if(double.IsNaN(values[i])||double.IsInfinity(values[i])) throw new ArgumentException("Non-finite baseline.");
            for(int i=0;i<MinutesPerYear;i++)
                if(values[i*8+4]<=0 || values[i*8+3]<0 || values[i*8+6]<0 || values[i*8+7]<0)
                    throw new ArgumentException("Invalid baseline physical range.");
        }
        public WeatherBaseline(double[] source)
        {
            if(source==null || source.Length!=MinutesPerYear*Columns)
                throw new ArgumentException("Expected 525600 rows of eight baseline values.");
            values=(double[])source.Clone();
            for(int i=0;i<values.Length;i++)
                if(double.IsNaN(values[i])||double.IsInfinity(values[i])) throw new ArgumentException("Non-finite baseline.");
            for(int i=0;i<MinutesPerYear;i++)
                if(values[i*8+4]<=0 || values[i*8+3]<0 || values[i*8+6]<0 || values[i*8+7]<0)
                    throw new ArgumentException("Invalid baseline physical range.");
        }
        public double Get(long minute,int column)
        {
            if(minute<0 || column<0 || column>=Columns) throw new ArgumentOutOfRangeException();
            return values[(int)(minute%MinutesPerYear)*Columns+column];
        }
    }
}
