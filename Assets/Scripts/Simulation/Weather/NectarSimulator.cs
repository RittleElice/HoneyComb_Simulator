using System;
using System.IO;


public sealed class NectarSimulator
{
    [Serializable] public sealed class State
    {
        public double gdd, unopened = 1, bloom, solar, sugar, water, pollen, developmentSum;
        public double[] development;
        public int count, cursor;
        public double yieldFactor;
        public double Total => sugar + water;
    }
    public NectarParameters Settings { get; private set; }
    public State[] Sources { get; private set; }
    public NectarSimulator(NectarParameters parameters=null)
    {
        Settings = parameters ?? new NectarParameters();

        Sources = new State[Settings.Sources.Length];
    }
    static double Clamp(double x) => Math.Max(0,Math.Min(1,x));
    static double Rise(double x,double lo,double hi) => Clamp((x-lo)/(hi-lo));
    static double Dome(double x,double center,double width) => Math.Max(0,1-Math.Pow((x-center)/width,2));
    static double PerMinute(double rate) => 1-Math.Pow(1-Clamp(rate),1.0/60);
    // Year one uses the workbook cached coefficient. Later years use a reproducible normal draw.
    static double AnnualFactor(ulong seed,long year,int source)
    {
        ulong bits=unchecked(seed ^ ((ulong)year*11400714819323198485UL) ^ ((ulong)(source+1)*7046029254386353131UL));
        double Uniform() {
            unchecked { bits+=11400714819323198485UL; ulong z=bits; z=(z^(z>>30))*13787848793156543929UL; z=(z^(z>>27))*10723151780598845931UL; return (((z^(z>>31))>>11)+0.5)/9007199254740992.0; }
        }
        double normal=Math.Sqrt(-2*Math.Log(Uniform()))*Math.Cos(2*Math.PI*Uniform());
        return Math.Min(2,Math.Max(.5,1+.3*normal));
    }
    public void Advance(WeatherSnapshot w,ulong seed)
    {
        bool first = w.Minute % 525600 == 0;
        for(int i=0;i<Sources.Length;i++)
        {
            var p=Settings.Sources[i];
            if(first) Sources[i]=new State { yieldFactor=w.Minute==0?p.AnnualYieldFactor:AnnualFactor(seed,w.Minute/525600,i), development=new double[Math.Max(1,(int)Math.Round(60*p.PollenDevelopmentHours))] };
            var s=Sources[i];
            double t=w.TemperatureCelsius, soil=w.SoilWaterStorage;
            s.gdd+=Math.Max(0,t-p.BaseGrowthTemperature)/1440;
            double opening=s.unopened*PerMinute(p.BaseBloomRate*Clamp((s.gdd-p.BloomStartGDD)/p.BloomActivationGDDRange)*Dome(t,p.OptimalBloomTemperature,p.BloomTemperatureRange));
            double wilting=s.bloom*PerMinute(p.BaseWiltingRate*(1+Math.Pow(Math.Abs(t-p.OptimalFlowerMaintenanceTemperature)/(t<p.OptimalFlowerMaintenanceTemperature?p.ColdWiltingTemperatureRange:p.HeatWiltingTemperatureRange),p.WiltingTemperatureExponent)));
            double previous=s.bloom;
            s.unopened-=opening; s.bloom+=opening-wilting;
            double effective=s.bloom<=p.MinimumBloomingFlowerRatio?0:s.bloom;
            double site=Settings.SiteArea*Settings.SourceFactor*p.BaseNectarYieldRatio*s.yieldFactor;
            double light=Math.Max(0,w.SolarEnergy)*60;
            s.solar=first?light:s.solar+(1-Math.Exp(-1/(60*p.CarbonResponseTime)))*(light-s.solar);
            double carbon=p.MinimumCarbonFactor+(1-p.MinimumCarbonFactor)*s.solar/(s.solar+p.CarbonHalfSaturation);
            s.sugar+=site*effective*p.NectarProductionFactor*Dome(t,p.OptimalNectarTemperature,p.NectarTemperatureRange)*carbon*Rise(soil,p.SoilWaterZeroProduction,p.SoilWaterFullProduction)/60;
            s.water+=site*effective*p.WaterProductionFactor*Rise(t,p.MinimumWaterProductionTemperature,p.FullWaterProductionTemperature)*(1-Rise(t,p.HeatWaterProductionTemperature,p.MaximumWaterProductionTemperature))*Math.Pow(Rise(soil,p.WaterProductionSoilMinimum,p.WaterProductionSoilFull),p.WaterProductionSoilExponent)/60;
            double solute=18.01528/p.NectarSugarMolarMass*s.sugar;
            double fraction=s.water+solute==0?0:solute/(s.water+solute);
            double activity=(1-fraction)*Math.Exp(-p.NectarNorrishConstant*fraction*fraction);
            s.water*=Math.Exp(-p.NectarEvaporationRate/60*(1+p.NectarEvaporationWindFactor*Math.Sqrt(w.WindSpeed))*0.61078*Math.Exp(17.27*t/(t+237.3))*Math.Max(0,activity-Clamp(w.RelativeHumidityPercent/100)));
            double capacity=site*effective*p.NectarStorageFactor;
            if(s.Total>capacity) { double scale=capacity/s.Total; s.sugar*=scale; s.water*=scale; }
            double recent=s.count==0?1:s.developmentSum/s.count;
            double retained=effective==0||previous==0?0:Math.Max(0,1-wilting/previous);
            s.pollen=(s.pollen*retained+(effective==0?0:site*p.PollenProductionFactor*opening*recent))*Math.Exp(-p.PollenRainLossCoefficient*w.Precipitation);
            double dev=(1-Rise(t,p.PollenHeatStartTemperature,p.PollenHeatZeroTemperature))*Rise(soil,p.PollenSoilMinimum,p.PollenSoilFull);
            s.developmentSum+=dev-s.development[s.cursor]; s.development[s.cursor]=dev;
            s.cursor=(s.cursor+1)%s.development.Length; s.count=Math.Min(s.count+1,s.development.Length);
        }
    }
    public void Write(BinaryWriter w)
    {
        Settings.Write(w);
        foreach(var s in Sources)
        {
            foreach(double x in new[]{s.yieldFactor,s.gdd,s.unopened,s.bloom,s.solar,s.sugar,s.water,s.pollen,s.developmentSum}) w.Write(x);
            w.Write(s.cursor); w.Write(s.count); w.Write(s.development.Length);
            foreach(double x in s.development) w.Write(x);
        }
    }
    public void Read(BinaryReader r)
    {
        Settings=NectarParameters.Read(r);
        for(int i=0;i<Sources.Length;i++)
        {
            var s=new State { yieldFactor=r.ReadDouble(),gdd=r.ReadDouble(),unopened=r.ReadDouble(),bloom=r.ReadDouble(),solar=r.ReadDouble(),sugar=r.ReadDouble(),water=r.ReadDouble(),pollen=r.ReadDouble(),developmentSum=r.ReadDouble(),cursor=r.ReadInt32(),count=r.ReadInt32() };
            int length=r.ReadInt32();
            if(length<=0||length>525600||s.cursor<0||s.cursor>=length||s.count<0||s.count>length) throw new InvalidDataException("Invalid nectar state");
            s.development=new double[length]; for(int j=0;j<length;j++) s.development[j]=r.ReadDouble(); Sources[i]=s;
        }
    }
}


