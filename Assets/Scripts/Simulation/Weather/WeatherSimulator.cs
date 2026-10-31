using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

/// <summary>Authoritative minute weather, including all recurrence and random state. Never draws UI.</summary>
public sealed class WeatherSimulator : IGameMinuteParticipant
{
    public const ulong DefaultSeed=20261031UL;
    public ulong Seed { get; private set; }
    public long Minute { get; private set; }
    public WeatherSnapshot Current => new WeatherSnapshot(Minute,current);
    private WeatherBaseline baseline;
    private WeatherParameters parameters;
    private WeatherRandom random;
    private WeatherWindows windows=new WeatherWindows();
    private WeatherValues current=new WeatherValues(), scratch=new WeatherValues();
    private bool faulted;
    private WeatherSimulator() { }
    public WeatherSimulator(ulong seed,WeatherBaseline baseline)
    {
        Seed=seed; this.baseline=baseline ?? throw new ArgumentNullException(nameof(baseline));
        random=new WeatherRandom(seed); parameters=WeatherParameters.Create(random);
        Minute=-1; Advance(0); // At clock minute zero expose the workbook's first calculated row, exactly once.
    }
    public void BindBaseline(WeatherBaseline data) { baseline=data ?? throw new ArgumentNullException(nameof(data)); }
    public Task CalculateMinuteAsync(long nextMinute,CancellationToken cancellation)
    { cancellation.ThrowIfCancellationRequested(); Advance(nextMinute); return Task.CompletedTask; }
    public void Advance(long nextMinute)
    {
        if(faulted) throw new InvalidOperationException("Weather requires recovery from a complete snapshot.");
        if(baseline==null) throw new InvalidOperationException("Weather baseline must be bound before simulation.");
        if(nextMinute!=checked(Minute+1)) throw new ArgumentOutOfRangeException(nameof(nextMinute),"Weather advances sequentially, one minute at a time.");
        try
        {
            baseline.Fill(nextMinute,scratch);
            WeatherEquations.Evaluate(nextMinute,parameters,current,scratch,windows,random);
            scratch.Validate();
            ValidateBounds(nextMinute,scratch);
            windows.Commit(scratch.Rain_Emission,scratch.Rain_Edge_Pass1);
            var old=current; current=scratch; scratch=old; Minute=nextMinute;
        }
        catch { faulted=true; throw; }
    }
    private static void ValidateBounds(long minute,WeatherValues v)
    {
        if(v.Total_Time!=minute || v.Cloud_Cover<0 || v.Cloud_Cover>1 || v.Wind_Cover<0 || v.Precipitation<0 || v.Vapor_Cover<=0 ||
           v.Solar_Cover<0 || v.RelativeHumidity<0 || v.RelativeHumidity>100 || v.SoilWater_Cover<0 || v.SoilWater_Cover>100 ||
           v.Rain_State<0 || v.Rain_State>136 || v.Rain_State!=Math.Floor(v.Rain_State) || v.Snow_Storage<0 || v.Snow_Depth<0)
            throw new InvalidDataException("Invalid weather state.");
    }
    public void WriteSnapshot(BinaryWriter writer)
    {
        if(faulted || Minute<0) throw new InvalidDataException("Incomplete weather state cannot be saved.");
        writer.Write(WeatherModelVersion.StateVersion);
        writer.Write(WeatherModelVersion.Equations); writer.Write(WeatherModelVersion.BaselineSha256);
        writer.Write(Seed); writer.Write(Minute); current.Write(writer); windows.Write(writer); random.Write(writer);
    }
    public static WeatherSimulator ReadSnapshot(BinaryReader reader,long expectedMinute)
    {
        if(reader.ReadInt32()!=WeatherModelVersion.StateVersion || reader.ReadString()!=WeatherModelVersion.Equations || reader.ReadString()!=WeatherModelVersion.BaselineSha256)
            throw new InvalidDataException("지원하지 않는 날씨 모델 또는 기준표 버전입니다.");
        var result=new WeatherSimulator { Seed=reader.ReadUInt64(),Minute=reader.ReadInt64() };
        if(result.Minute<0 || result.Minute!=expectedMinute) throw new InvalidDataException("날씨와 게임 시간의 저장 시점이 다릅니다.");
        result.current=WeatherValues.Read(reader); ValidateBounds(result.Minute,result.current);
        result.windows=WeatherWindows.Read(reader,result.Minute); result.random=WeatherRandom.Read(reader,result.Seed);
        // Derived initialization is reproducible; rebuilding parameters must not consume the restored streams.
        result.parameters=WeatherParameters.Create(new WeatherRandom(result.Seed));
        return result;
    }
}
