using System;
using Unity.Burst;

// Arrays remain pinned only for the duration of each synchronous kernel call.
[BurstCompile]
public static unsafe class HiveClimateKernels
{
    // Reference isotherm near 20 C, dry-mass basis. Rounded Wood Handbook EMC
    // points; not species-specific, no hysteresis or temperature-dependent EMC yet.
    // RH=1 endpoint is a modelling bound for sorbed water, not liquid saturation.
    // EMC reference: USDA Wood Handbook ch.4; OSU EM 8600.
    // Permeability range reference (beech, not a softwood calibration):
    // https://doi.org/10.1007/s10570-023-05093-z
    // 2e-12 is an explicit representative assumption within the reported range;
    // grain orientation, RH dependence and coatings are not represented.
    public const double WoodCapacityRatio = .30;
    public const double WoodReferenceMoisture = .12;
    public const double WoodPermeability = 2e-12; // kg/(m s Pa), isotropic reference
    public const double AirPermeability = 1.85e-10; // still air near 20 C
    static readonly double[] WoodRh = {0,.1,.2,.3,.4,.5,.6,.7,.8,.9,.95,1};
    static readonly double[] WoodMc = {0,.025,.045,.062,.078,.094,.111,.133,.162,.207,.241,.30};
    public static double WoodMoistureRatio(double rh)
    {
        int j=1;while(j<WoodRh.Length-1 && rh>WoodRh[j])j++;
        return WoodMc[j-1]+(rh-WoodRh[j-1])*(WoodMc[j]-WoodMc[j-1])/(WoodRh[j]-WoodRh[j-1]);
    }
    public static double WoodActivity(double water,double capacity)
    {
        if(capacity<=0)return 0;
        double mc=water*WoodCapacityRatio/capacity;
        int j=1;while(j<WoodMc.Length-1 && mc>WoodMc[j])j++;
        // Extrapolate legacy over-capacity water; never discard saved mass.
        return WoodRh[j-1]+(mc-WoodMc[j-1])*(WoodRh[j]-WoodRh[j-1])/(WoodMc[j]-WoodMc[j-1]);
    }

    public struct Contact
    {
        public int a,b;
        public double conductance,fractionA,fractionB;
    }

    public static void State(double[] mass,double[] dry,double[] freeVolume,double[] volume,double[] energy,
        double[] phase,double[] capacity,double[] temperature,double density,double airCv,double liquidCp)
    {
        fixed(double* m=mass,d=dry,f=freeVolume,v=volume,e=energy,h=phase,c=capacity,t=temperature)
            RefreshState(energy.Length,m,d,f,v,e,h,c,t,density,airCv,liquidCp);
    }

    [BurstCompile(CompileSynchronously=true, FloatMode=FloatMode.Strict)]
    static void RefreshState(int n,double* m,double* dry,double* freeVolume,double* volume,double* energy,
        double* phase,double* capacity,double* temperature,double density,double airCv,double liquidCp)
    {
        for(int i=0;i<n;i++,m+=5)
        {
            double v=freeVolume[i]-m[4]/density;
            volume[i]=v;
            double c=dry[i]+v*airCv+m[0]*1850+m[1]*4180+m[2]*4180+m[3]*4180+m[4]*liquidCp;
            double h=m[0]*2501000-m[1]*120000-m[2]*50000-m[3]*20000;
            phase[i]=h;capacity[i]=c;temperature[i]=(energy[i]-h)/c;
        }
    }

    public static void PressureAndSlope(double[] mass,double[] limit,double[] volume,
        double[] temperature,double[] pressure,double[] slope)
    {
        fixed(double* m=mass,l=limit,v=volume,t=temperature,p=pressure,d=slope)
            PreparePressure(temperature.Length,m,l,v,t,p,d);
    }

    [BurstCompile(CompileSynchronously=true,FloatMode=FloatMode.Strict)]
    static void PreparePressure(int count,double* mass,double* limit,double* volume,
        double* temperature,double* pressure,double* slope)
    {
        for(int i=0;i<count;i++,mass+=5,limit+=5,pressure+=5,slope+=5)
        {
            double t=temperature[i];
            double sat=610.94*Math.Exp(17.625*t/(243.04+t));
            slope[0]=volume[i]>0?461.5*(273.15+t)/volume[i]:0;
            pressure[0]=mass[0]*slope[0];
            for(int s=1;s<=3;s++)
            {
                slope[s]=limit[s]>0?sat/limit[s]:0;
                pressure[s]=mass[s]*slope[s];
                if(s==1 && limit[s]>0)
                {
                    pressure[s]=sat*WoodActivity(mass[s],limit[s]);
                    // Global upper dp/dm bound (minimum dMC/dRH = .16).
                    // Keeps all pyramid updates inside initial pressure extrema even
                    // when one minute crosses an isotherm knot, without substeps.
                    slope[s]=sat*WoodCapacityRatio/(.16*limit[s]);
                }
            }
        }
    }

    public static void RefreshChangedRows(int[] rows,int count,double[] mass,double[] dry,double[] freeVolume,
        double[] volume,double[] energy,double[] phase,double[] capacity,double[] temperature,double density,double airCv,double liquidCp)
    {
        fixed(int* indices=rows)
        fixed(double* m=mass,d=dry,f=freeVolume,v=volume,e=energy,h=phase,c=capacity,t=temperature)
            RefreshRows(indices,count,m,d,f,v,e,h,c,t,density,airCv,liquidCp);
    }

    [BurstCompile(CompileSynchronously=true,FloatMode=FloatMode.Strict)]
    static void RefreshRows(int* rows,int count,double* mass,double* dry,double* freeVolume,double* volume,
        double* energy,double* phase,double* capacity,double* temperature,double density,double airCv,double liquidCp)
    {
        for(int j=0;j<count;j++)
        {
            int i=rows[j];
            RefreshState(1,mass+5*i,dry+i,freeVolume+i,volume+i,energy+i,phase+i,capacity+i,temperature+i,density,airCv,liquidCp);
        }
    }

    public static void PairFlux(Contact[] contacts,double[] slope,double[] pressure,double[] mass,
        double[] limit,double[] delta,double[] energyDelta,double[] temperature,double[] transfers=null)
    {
        fixed(Contact* c=contacts)
        fixed(double* s=slope,p=pressure,m=mass,l=limit,d=delta,h=energyDelta,t=temperature)
        fixed(double* trace=transfers)
            ExchangePairs(c,contacts.Length,s,p,m,l,d,h,t,trace);
    }

    [BurstCompile(CompileSynchronously=true,FloatMode=FloatMode.Strict)]
    static void ExchangePairs(Contact* contacts,int count,double* slope,double* pressure,double* mass,
        double* limit,double* delta,double* energyDelta,double* temperature,double* trace)
    {
        const int batchSize=32;
        double* amounts=stackalloc double[batchSize];
        int* donors=stackalloc int[batchSize];
        int* receivers=stackalloc int[batchSize];

        for(int start=0;start<count;start+=batchSize)
        {
            int length=Math.Min(batchSize,count-start);
            // All transfers read the tick-start state, including the last partial batch.
            for(int k=0;k<length;k++)
            {
                Contact c=contacts[start+k];
                bool forwards=pressure[c.a]>=pressure[c.b];
                int donor=forwards?c.a:c.b, receiver=forwards?c.b:c.a;
                donors[k]=-1;
                if(receiver%5==4 || mass[donor]<=0 || c.fractionA<=0 || c.fractionB<=0)
                    continue;
                // Subvolume and sorption capacity both scale with the pyramid share.
                double sensitivity=slope[c.a]/c.fractionA+slope[c.b]/c.fractionB;
                double x=60*c.conductance*sensitivity;
                double response=x<1e-5 ? 60*c.conductance*(1-x/2+x*x/6) : (1-Math.Exp(-x))/sensitivity;
                double moved=(pressure[donor]-pressure[receiver])*response;
                double available=mass[donor]*(forwards?c.fractionA:c.fractionB);
                if(receiver%5!=0)
                    available=Math.Min(available,Math.Max(0,limit[receiver]-mass[receiver])*(forwards?c.fractionB:c.fractionA));
                moved=Math.Min(moved,available*(1-1e-14));
                amounts[k]=moved;
                donors[k]=donor;
                receivers[k]=receiver;
            }

            // Preserve contact order and accumulate into deltas until the final tick commit.
            for(int k=0;k<length;k++)
            {
                int donor=donors[k];
                if(trace!=null)
                    trace[start+k]=donor<0?0:(donor==contacts[start+k].a?amounts[k]:-amounts[k]);
                if(donor<0)continue;
                int receiver=receivers[k];
                double moved=amounts[k];
                delta[donor]-=moved;
                delta[receiver]+=moved;
                double heat=moved*(2501000+1850*temperature[donor/5]);
                energyDelta[donor/5]-=heat;
                energyDelta[receiver/5]+=heat;
            }
        }
    }

    public struct Face
    {
        public int a,b;
        public double fa,fb,area,heat,halfA,halfB;
    }

    public static void ExchangeHeat(Face[] faces, double[] capacity, bool[] changed,
        double[] coefficients, double[] temperature, double[] energyDelta)
    {
        fixed(Face* f=faces)
        fixed(double* c=capacity, g=coefficients, t=temperature, e=energyDelta)
        fixed(bool* dirty=changed)
            Heat(f,faces.Length,c,dirty,g,t,e);
    }

    [BurstCompile(CompileSynchronously=true, FloatMode=FloatMode.Strict)]
    static void Heat(Face* faces,int count,double* capacity,bool* dirty,double* g,double* t,double* e)
    {
        for(int j=0;j<count;j++)
            Conduct(faces[j],j,capacity,dirty,g,t,e);
    }

    static void Conduct(Face f,int j,double* capacity,bool* dirty,double* g,double* t,double* e)
    {
        if(dirty[f.a] || dirty[f.b])
        {
            double a=capacity[f.a]*f.fa,b=capacity[f.b]*f.fb,reduced=a*b/(a+b);
            g[j]=reduced*(1-Math.Exp(-60*f.heat/reduced));
        }
        double heat=g[j]*(t[f.a]-t[f.b]);
        e[f.a]-=heat;
        e[f.b]+=heat;
    }
}
