using System;
using static System.Math;
namespace HoneyComb.Simulation.Environment.Weather
{
    // Minute recurrence port of the verified Ver.2.0 model. All calculations use double.
    public sealed class WeatherSimulator
    {
        private readonly WeatherParameters p;
        private readonly WeatherBaseline baseline;
        private WeatherState s;
        public WeatherSample Current { get; private set; }
        public long NextMinute => s.Minute;
        public WeatherSimulator(WeatherParameters parameters,WeatherBaseline baseline,uint seed=2026101901,uint windSeed=2026101902,long startMinute=0)
        {
            if(startMinute<0) throw new ArgumentOutOfRangeException(nameof(startMinute));
            p=parameters?.Copy()??throw new ArgumentNullException(nameof(parameters));
            p.Validate();this.baseline=baseline??throw new ArgumentNullException(nameof(baseline));
            s=new WeatherState{Minute=startMinute,RandomState=seed,WindRandomState=windSeed,
                Reservoir=p.C8,WindDay=p.B20,WindFast=p.B22,GroundEnergy=p.G2,SurfaceEnergy=p.G8,
                AirEnergy=p.G16*p.G18,SnowWater=p.D16*p.D12/100,SnowDepth=p.D16,SoilWater=p.H4,
                RainQueue=new double[(int)p.C50],PassQueue=new double[(int)p.C50]};
            s.Slow=p.A4*Normal();s.Fast=p.A8*Normal();s.Supply=p.C4*Normal();
            s.VaporNoise=p.E4*Normal();s.IntensityNoise=p.C40*Normal();s.SmoothIntensity=s.IntensityNoise;
            first=true;
        }
        private bool first;
        public WeatherState CaptureState()=>s.Copy();
        // Restore includes the first-step flag through an explicit snapshot container.
        public WeatherSnapshot Capture()=>new WeatherSnapshot{State=s.Copy(),First=first,Current=Current};
        public void Restore(WeatherSnapshot snapshot)
        {
            if(snapshot==null || snapshot.State==null || snapshot.State.Minute<0 ||
                snapshot.State.RainQueue==null || snapshot.State.PassQueue==null ||
                snapshot.State.RainQueue.Length!=(int)p.C50 || snapshot.State.PassQueue.Length!=(int)p.C50)
                throw new ArgumentException("Invalid weather snapshot.");
            s=snapshot.State.Copy();first=snapshot.First;Current=snapshot.Current;
        }
        private static double Clip(double x)=>Max(0,Min(1,x));
        private double Uniform(bool wind=false)
        {
            unchecked{
                uint state=(wind?s.WindRandomState:s.RandomState)+0x6D2B79F5u;
                if(wind)s.WindRandomState=state;else s.RandomState=state;
                uint t=(state^(state>>15))*(state|1u);
                t^=t+(t^(t>>7))*(t|61u);
                return (t^(t>>14))/4294967296.0;
            }
        }
        private double Normal(bool wind=false)
        {
            if(wind?s.WindHasSpare:s.HasSpare){
                if(wind){s.WindHasSpare=false;return s.WindSpare;}
                s.HasSpare=false;return s.Spare;
            }
            double a=Sqrt(-2*Log(Max(1e-16,Uniform(wind)))),b=2*PI*Uniform(wind),spare=a*Sin(b);
            if(wind){s.WindHasSpare=true;s.WindSpare=spare;}else{s.HasSpare=true;s.Spare=spare;}
            return a*Cos(b);
        }
        private double Noise(double previous,double rho,double sigma)=>rho*previous+sigma*Sqrt(1-rho*rho)*Normal();
        public WeatherSample Step(long minute)
        {
            if(minute!=s.Minute || minute==long.MaxValue) throw new ArgumentOutOfRangeException(nameof(minute),"Weather requires consecutive minutes.");
            double B=baseline.Get(minute,4);
            if(!first){
                s.Slow=Noise(s.Slow,p.A2,p.A4);s.Fast=Noise(s.Fast,p.A6,p.A8);
                s.Supply=Noise(s.Supply,p.C2,p.C4);s.VaporNoise=Noise(s.VaporNoise,p.E2,p.E4);
                s.IntensityNoise=Noise(s.IntensityNoise,p.C38,p.C40);
                s.SmoothIntensity=p.C42*s.SmoothIntensity+(1-p.C42)*s.IntensityNoise;
            }
            double cb=baseline.Get(minute,0)+s.Slow+s.Fast,c=Pow(Clip(cb+(first?0:s.Overshoot*p.A10)),p.A12);
            s.Overshoot=cb<0?cb:cb>1?cb-1:0;
            s.Reservoir+=baseline.Get(minute,3)*Exp(s.Supply-p.C4*p.C4/2)-s.RawRain;
            double start=p.C10-p.C12*c,stop=p.C14-p.C16*c;
            double a=Clip((s.Age-60+p.C36/2)/p.C36),d=Clip((s.Age-120+p.C36/2)/p.C36);
            a=a*a*(3-2*a);d=d*d*(3-2*d);
            double potential=Min(s.Reservoir,Exp(s.SmoothIntensity-p.C48/2)*c*
                ((1-a)*(s.Reservoir-start)*p.C18+(a-d)*(s.Reservoir-stop)*p.C20+d*(s.Reservoir-stop)*p.C22));
            if(potential<p.C24)s.Age=0;
            else if(s.Age==0){double q=p.C26*Pow(c,p.C28);q=1-Pow(1-q,1.0/60);s.Age=Uniform()<q?1:0;}
            else {double q=(s.Age>=120?p.C34:p.C30)*Pow(c,p.C32);s.Age=Uniform()<Pow(q,1.0/60)?Min(s.Age+1,Max(121,Ceiling(120+p.C36/2)+1)):0;}
            s.RawRain=s.Age>0?potential:0;
            int index=(int)(minute%s.RainQueue.Length);
            s.RainQueue[index]=s.RawRain;
            double pass=0,rain=0;foreach(double x in s.RainQueue)pass+=x;
            s.PassQueue[index]=pass/s.RainQueue.Length;
            foreach(double x in s.PassQueue)rain+=x;rain/=s.PassQueue.Length;
            double g=Pow(10/B,p.E8),ac=B*Exp((s.VaporNoise+s.Supply*p.E6)*g-
                .5*(Pow(p.E6*p.C4,2)+p.E4*p.E4)*g*g+p.E10*(c-baseline.Get(minute,5)));
            double target=rain/(rain+p.E16);
            s.Wet=first?target:s.Wet+(target-s.Wet)*(1-(target>s.Wet?p.E20:p.E18));
            if(!first)s.PreWet=p.E32*s.PreWet+(1-p.E32)*p.E26*Clip((s.Reservoir-start+p.E28)/p.E28)*
                Clip((c-p.E30)/(1-p.E30))*Clip(.5+.5*s.Supply/p.C4)*(1-s.Wet);
            double vp=p.E24*(ac+p.E12*Max(s.Wet,s.PreWet)*Max(0,B+p.E14-ac));
            s.Vapor=first?vp:p.E22*s.Vapor+(1-p.E22)*vp;
            double mass=baseline.Get(minute,7),u=Max(0,(1.7*s.Vapor-.1)/10)*mass;
            double solar=baseline.Get(minute,6)*(1-p.F2*Pow(c,p.F4))*Exp(-p.F6*Pow(rain*60,p.F8))*
                (1-2.4959*u/(Pow(1+79.034*u,.6828)+6.385*u));
            double theta=2*PI*((minute%WeatherBaseline.MinutesPerYear)+.5)/WeatherBaseline.MinutesPerYear;
            solar*=Exp(p.F10+p.F12*Cos(theta)+p.F14*Sin(theta)+p.F16*Cos(2*theta)+p.F18*Sin(2*theta))*Pow(Max(1,mass),p.F20);
            if(!first){if(minute%1440==0)s.WindDay=p.B2*s.WindDay+p.B4*Normal(true);s.WindFast=p.B6*s.WindFast+p.B8*Normal(true);}
            double wind=Max(0,Exp(baseline.Get(minute,1)+p.B10+p.B12*baseline.Get(minute,2)+p.B14*s.WindDay+p.B16*s.WindFast)-p.B18);
            Current=ThermalAndSoil(minute,c,rain,s.Vapor,solar,B,wind);
            s.Minute++;first=false;
            return Current;
        }
        private WeatherSample ThermalAndSoil(long minute,double c,double r,double v,double solar,double B,double wind)
        {
            double gt=s.GroundEnergy/p.G4,st=s.SurfaceEnergy/p.G10,at=s.AirEnergy/p.G16,ct=at-273.15;
            double frac=Clip((p.D4-ct)/(p.D4-p.D2)),rain=r*(1-frac),snow=r*frac;
            double emiss=(1-.84*c)*1.24*Pow(v/at,1.0/7)+.84*c,gs=p.G20*(st-gt),sa=p.G22*(st-at),ass=p.G28*solar;
            double sr=p.G30*.0036/60*5.670374419e-8*(Pow(st,4)-Pow(p.G32,4))*(1-emiss);
            double ar=p.G34*.0036/60*5.670374419e-8*(Pow(at,4)-Pow(p.G36,4))*(1-emiss)*Exp(-p.G38*Log(v/B))/p.G40;
            double logv=Log(v/6.112),dew=243.5*logv/(17.67-logv),rh=v/(6.112*Exp(17.67*ct/(ct+243.5)));
            double cool=p.G16*Min(Max(0,ct-dew),p.G42*rain/(rain+p.G44)*Max(0,1-Min(1,rh)));
            double rawAir=s.AirEnergy+sa+ass-ar-cool,adj=Max(0,p.G16*(dew+273.15)-rawAir);
            double na=rawAir+adj,temp=na/p.G16-273.15;
            double melt=first?0:Min(s.SnowWater,p.D6*Max(ct-p.D8,0)+p.D22*solar*Clip((ct+5)/5));
            double ns=s.SnowWater+snow-melt,oldDepth=s.SnowWater>0?s.SnowDepth*(1-melt/s.SnowWater):0;
            double minDepth=100*Max(0,s.SnowWater-melt)/p.D18;
            double settled=minDepth+Max(0,oldDepth-minDepth)*Exp(-p.D20),depth=settled+100*snow/p.D12;
            double cover=Clip(depth/p.D10),ss=p.G24*(1-p.G26*cover)*solar;
            s.GroundEnergy+=gs;s.SurfaceEnergy+=-gs-sa+ss-sr;s.AirEnergy=na;s.SnowWater=ns;s.SnowDepth=depth;
            double humidity=Min(100,100*v/(6.112*Exp(17.67*temp/(temp+243.5))));
            double eref=.6108*Exp(17.27*p.H10/(p.H10+237.3)),sref=4098*eref/Pow(p.H10+237.3,2);
            double es=.6108*Exp(17.27*temp/(temp+237.3)),vpd=Max(0,es*(1-p.H8*humidity/100));
            double slope=4098*es/Pow(temp+237.3,2),response=(slope/(slope+p.H12))/(sref/(sref+p.H12));
            double demand=Max(0,p.H20*(p.H14*solar*response+p.H16*vpd/eref*(1+p.H18*wind)));
            double et=Min(s.SoilWater,demand*Min(1,s.SoilWater/(p.H2*p.H22)));
            s.SoilWater=Max(0,Min(p.H2,s.SoilWater+(rain+melt)*p.H6-et));
            if(double.IsNaN(temp)||double.IsInfinity(temp)||r<0||solar<0||ns< -1e-9)
                throw new InvalidOperationException("Invalid weather state at minute "+minute);
            return new WeatherSample(minute,c,wind,v,r,solar,temp,humidity,depth,s.SoilWater,rain,snow,melt);
        }
    }
    public sealed class WeatherSnapshot
    {
        public WeatherState State;
        public bool First;
        public WeatherSample Current;
    }
}
