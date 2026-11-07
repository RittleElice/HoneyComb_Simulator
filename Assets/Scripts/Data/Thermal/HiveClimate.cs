using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;

using System.Threading.Tasks;
using Contact = HiveClimateKernels.Contact;
using Link = HiveClimateKernels.Face;

public static partial class HiveClimate
{
    const int Air=0, Wood=1, Insulation=2, Wax=3, Nectar=4, Kinds=5;
    const double Rv=461.5, VaporCp=1850, LiquidCp=4180, Latent=2501000;
    static readonly double[] Offset={Latent,-120000,-50000,-20000,0};
    // Wood uses a reference nonlinear isotherm; other materials retain their provisional model.
    static readonly double[] Sorption={0,HiveClimateKernels.WoodCapacityRatio,.04,0,0};
    static readonly double[] Mobility={1,.015,.008,0,.04};
    static double Psat(double t)=>610.94*Math.Exp(17.625*t/(243.04+t));

    public sealed partial class Model
    {
        readonly int n;
        readonly Link[] links;
        readonly double[] mass,limit,pressure,delta,energy,dE,temperature,volume,dry,sugar;
        readonly double[] contentFreeVolume,capacity,heatCapacityAtLastExchange,heatCoefficients,phaseEnergy;
        readonly int[] nectarRows;
        bool stateReady;
        readonly bool[] capacityChanged;
        readonly double waterDensity, airCv, woodDryDensity;
        readonly double liquidCp;
        public Model(ThermalExchange ex, Dictionary<EquipmentThermalState,ItemData> owners,ThermalMaterials materials,bool warmGradient=false)
            : this(new ClimatePartition(ex),owners,materials,warmGradient,null) {}
        internal Model(ClimatePartition ex, Dictionary<EquipmentThermalState,ItemData> owners,ThermalMaterials materials,
            bool warmGradient,double? initialRh)
        {
            n=ex.CellCount;
            phaseEnergy=new double[n];capacity=new double[n];heatCapacityAtLastExchange=new double[n];capacityChanged=new bool[n];
            heatCoefficients=new double[ex.FaceCount];slope=new double[n*Kinds];
            links=new Link[ex.FaceCount];
            mass=new double[n*Kinds];limit=new double[mass.Length];pressure=new double[mass.Length];delta=new double[mass.Length];
            energy=new double[n];dE=new double[n];temperature=new double[n];volume=new double[n];dry=new double[n];sugar=new double[n];contentFreeVolume=new double[n];
            var air=materials[ThermalMaterialKind.Air];
            airCv=air.EffectiveVolumetricHeatCapacityJPerM3K>0?air.EffectiveVolumetricHeatCapacityJPerM3K:air.Density*air.SpecificHeat;
            woodDryDensity=materials[ThermalMaterialKind.Wood].Density/(1+HiveClimateKernels.WoodReferenceMoisture);
            var balance=CombBalance.Snapshot();
            waterDensity=1000*balance.WaterDensityGPerMl;
            liquidCp=balance.WaterSpecificHeatJPerKgK;
            for(int i=0;i<n;i++)
            {
                var c=ex.GetCell(i);
                for(int k=0;k<c.MemberCount;k++)
                {
                    var o=c.MemberOwner(k);int j=c.MemberIndex(k);var g=owners[o].EquipmentGrid;
                    var r=g.GetContentThermalMass(j);
                    int x=j%g.SizeX,y=j/g.SizeX%g.SizeY,z=j/(g.SizeX*g.SizeY);
                    double v=(g.X[x+1]-g.X[x])*(g.Y[y+1]-g.Y[y])*(g.Z[z+1]-g.Z[z])*1e-9;
                    var material=o.ExchangeMaterial(j);
                    foreach(int s in new[]{Wood,Insulation})
                        if(material==(s==Wood?HiveMaterial.Wood:HiveMaterial.Insulation))
                            limit[i*Kinds+s]+=v*g.SolidFraction(y)*materials[s==Wood?ThermalMaterialKind.Wood:ThermalMaterialKind.Insulation].Density*Sorption[s]/(s==Wood?1+HiveClimateKernels.WoodReferenceMoisture:1);
                    sugar[i]+=r.SugarMg*1e-6;
                    mass[i*Kinds+Nectar]+=r.WaterMg*1e-6;
                    // Pollen bulk density is a prototype assumption of 1000 kg/m3.
                    double excluded=CombBalance.NectarVolumeMl(r.SugarMg,r.WaterMg)*1e-6+r.PollenMg*1e-9;
                    volume[i]+=o.AirVolumeM3[j]-excluded;
                    dry[i]+=o.HeatCapacityJPerK[j]-o.AirVolumeM3[j]*airCv-r.WaterMg*1e-6*liquidCp;
                }
                if(volume[i]<-1e-12)throw new InvalidOperationException("Contents exceed available volume");
                volume[i]=Math.Max(0,volume[i]);
                contentFreeVolume[i]=volume[i]+mass[i*Kinds+Nectar]/waterDensity;
                double t=c.EnergyJ/c.HeatCapacityJPerK,rh=initialRh ?? (.4+.3*(i%2));
                if(warmGradient)
                {
                    t=12+18*(.5+.5*Math.Sin(i*.17));
                    rh=.20+.65*(.5+.5*Math.Cos(i*.37));
                }
                mass[i*Kinds]=rh*Psat(t)*volume[i]/(Rv*(273.15+t));
                for(int s=Wood;s<=Insulation;s++)mass[i*Kinds+s]=limit[i*Kinds+s]*(s==Wood?HiveClimateKernels.WoodMoistureRatio(rh)/HiveClimateKernels.WoodCapacityRatio:rh);
                if(initialRh.HasValue)RestoreMemberWater(c,i,initialRh.Value,t);
                energy[i]=Capacity(i)*t+Phase(i);
                // Retain the old total enthalpy while releasing legacy bound wax water.
                int waxSlot=i*Kinds+Wax;
                mass[i*Kinds+Air]+=mass[waxSlot];
                mass[waxSlot]=0;
            }
            nectarRows=Enumerable.Range(0,n).Where(i=>mass[i*Kinds+Nectar]>0).ToArray();
            for(int j=0;j<links.Length;j++)
            {
                var f=ex.GetFace(j);
                links[j]=new Link{a=f.A,b=f.B,area=f.AreaM2,halfA=f.HalfAM,halfB=f.HalfBM,fa=ex.FacePyramidCapacity(f.A,f.Axis,f.AreaM2)/ex.GetCell(f.A).HeatCapacityJPerK,
                    fb=ex.FacePyramidCapacity(f.B,f.Axis,f.AreaM2)/ex.GetCell(f.B).HeatCapacityJPerK,
                    heat=f.AreaM2*materials.Transfers.Between(ex.GetCell(f.A).Owner.ExchangeMaterial(ex.GetCell(f.A).Index),ex.GetCell(f.B).Owner.ExchangeMaterial(ex.GetCell(f.B).Index))};
            }
        }
        double Cp(int s)=>s==Air?VaporCp:s==Nectar?liquidCp:LiquidCp;
        double Phase(int i){double e=0;for(int s=0;s<Kinds;s++)e+=mass[i*Kinds+s]*Offset[s];return e;}
        double Capacity(int i){double c=dry[i]+volume[i]*airCv;for(int s=0;s<Kinds;s++)c+=mass[i*Kinds+s]*Cp(s);return c;}
        long[] phaseTicks;
        long lastMark;
        void Mark(int stage)
        {
            if(phaseTicks==null)return;
            long now=Stopwatch.GetTimestamp();
            phaseTicks[stage]+=now-lastMark;
            lastMark=now;
        }
        public void ProfileMinute(long[] ticks)
        {
            phaseTicks=ticks;
            lastMark=Stopwatch.GetTimestamp();
            try{Minute(1);}
            finally{phaseTicks=null;}
        }
        void Prepare(bool split=false, bool pressures=true)
        {
            if(!stateReady)
            {
                HiveClimateKernels.State(mass,dry,contentFreeVolume,volume,energy,phaseEnergy,capacity,temperature,waterDensity,airCv,liquidCp);
                stateReady=true;
            }
            if(!pressures)return;
            if(split)Mark(0);
            HiveClimateKernels.PressureAndSlope(mass,limit,volume,temperature,pressure,slope);
        }
        bool Present(int p,int s)=>s==Air?volume[p/Kinds]>0:s==Nectar?mass[p]>0:limit[p]>0;
        void Move(int p,int q,double scale,double fractionP,double fractionQ)
        {
            int s=p%Kinds,r=q%Kinds;
            if(!Present(p,s)||!Present(q,r))return;
            if(pressure[q]>pressure[p]){int swap=p;p=q;q=swap;double f=fractionP;fractionP=fractionQ;fractionQ=f;}
            if(q%Kinds==Nectar)return; // evaporation only
            double sensitivity=slope[p]/fractionP+slope[q]/fractionQ;
            double x=scale*sensitivity;
            double response=x<1e-5?scale*(1-x/2+x*x/6):(1-Math.Exp(-x))/sensitivity;
            double dm=Math.Min((pressure[p]-pressure[q])*response, mass[p]*fractionP);
            if(q%Kinds!=Air)dm=Math.Min(dm,Math.Max(0,limit[q]-mass[q])*fractionQ);
            if(dm<=0)return;
            delta[p]-=dm;delta[q]+=dm;
            // Desorbed/evaporated water travels as vapour. Phase offsets automatically
            // subtract evaporation heat at the source and release sorption heat at arrival.
            double h=dm*(Latent+VaporCp*temperature[p/Kinds]);
            dE[p/Kinds]-=h;dE[q/Kinds]+=h;
        }
        Contact[] contacts;
        double[] slope;
        static double MoistureResistance(int kind,double half)
        {
            if(kind==Wood)return half/HiveClimateKernels.WoodPermeability;
            if(kind==Air)return half/HiveClimateKernels.AirPermeability;
            return .5/(1e-9*Mobility[kind]); // retain legacy non-wood interface scale
        }
        static double FaceMoistureConductance(Link f,int a,int b)
        {
            if(a!=Wood && b!=Wood)return f.area*1e-9*Math.Min(Mobility[a],Mobility[b]);
            return f.area/(MoistureResistance(a,f.halfA)+MoistureResistance(b,f.halfB));
        }
        double LocalMoistureConductance(int row,int kind)
        {
            // Current pure-wood cells have no local air reservoir. Keep the legacy
            // mixed-cell interface law for other materials; wood uses a cube-length
            // approximation if a future mixed cell exposes a local air reservoir.
            if(kind!=Wood)return 1e-9*Math.Pow(volume[row],2d/3)*Mobility[kind];
            double woodVolume=limit[row*Kinds+Wood]/HiveClimateKernels.WoodCapacityRatio/woodDryDensity;
            double half=.5*Math.Pow(woodVolume,1d/3);
            return Math.Pow(volume[row],2d/3)/(MoistureResistance(Wood,half)+MoistureResistance(Air,.5*Math.Pow(volume[row],1d/3)));
        }
        void BuildContacts()
        {
            var active=new List<Contact>();
            var presentCount=new int[n];
            for(int i=0;i<n;i++)
                for(int s=0;s<Nectar;s++)
                    if(Present(i*Kinds+s,s))presentCount[i]++;

            // In a mixed cell, reserve half of each participating reservoir for local
            // air/material exchange. A pure air cell gives its entire budget to faces.
            double FaceShare(int i) => presentCount[i]>1 && volume[i]>0 ? .5 : 1;
            void Add(int a,int b,double g,double fa,double fb)
            {
                active.Add(new Contact {a=a,b=b,conductance=g,fractionA=fa,fractionB=fb});
            }
            foreach(var f in links)
            {
                if(presentCount[f.a]==0 || presentCount[f.b]==0)continue;
                double fa=f.fa*FaceShare(f.a)/presentCount[f.b];
                double fb=f.fb*FaceShare(f.b)/presentCount[f.a];
                for(int s=0;s<Nectar;s++)if(Present(f.a*Kinds+s,s))
                    for(int r=0;r<Nectar;r++)if(Present(f.b*Kinds+r,r))
                        Add(f.a*Kinds+s,f.b*Kinds+r,FaceMoistureConductance(f,s,r),fa,fb);
            }
            for(int i=0;i<n;i++)
            {
                if(volume[i]<=0 || presentCount[i]<2)continue;
                for(int r=Wood;r<Nectar;r++)if(Present(i*Kinds+r,r))
                    Add(i*Kinds,i*Kinds+r,LocalMoistureConductance(i,r),.5/(presentCount[i]-1),.5);
            }
            contacts=active.ToArray();
            // Shared or partially covered faces can carry rounding in the area ratios.
            // Ensure that all paired subvolumes together fit in the parent reservoir.
            var total=new double[mass.Length];
            foreach(var c in contacts){total[c.a]+=c.fractionA;total[c.b]+=c.fractionB;}
            for(int j=0;j<contacts.Length;j++)
            {
                var c=contacts[j];
                c.fractionA/=Math.Max(1,total[c.a]);c.fractionB/=Math.Max(1,total[c.b]);
                contacts[j]=c;
            }

        }
        public void Minute(int steps)
        {
            if(steps<=0)throw new ArgumentOutOfRangeException(nameof(steps));
            Prepare(pressures:false);
            if(steps==1)
            {
                AccumulateEvaporation(60);
                Prepare(true);
                Mark(1);
                if(contacts==null)BuildContacts();
                PrepareFlowTrace();
                HiveClimateKernels.PairFlux(contacts,slope,pressure,mass,limit,delta,dE,temperature,flowTransfers);
                Mark(2);
                AccumulateHeat();
                Mark(3);
                CommitChanges();
            }
            else LegacyMoisture(steps);
            Mark(4);
        }
        // Only the water deltas change here; the enthalpy of a local phase change stays in this cell.
        void AccumulateEvaporation(double seconds)
        {
            if(flowEvaporation!=null)Array.Clear(flowEvaporation,0,n);
            if(nectarRows.Length==0)return;
            foreach(int row in nectarRows)
            {
                int p=row*Kinds;
                double water=mass[p+Nectar], airVolume=volume[row];
                if(water<=0 || airVolume<=0)continue;
                double t=temperature[row], saturation=Psat(t);
                double solute=.1*sugar[row], total=water+solute;
                double vaporSlope=Rv*(273.15+t)/airVolume;
                double difference=saturation*water/total-mass[p]*vaporSlope;
                if(difference<=0)continue;

                double slopeSum=vaporSlope+saturation*solute/(total*total);
                double rateTime=seconds*1e-9*Mobility[Nectar]*Math.Pow(airVolume,2d/3);
                double x=rateTime*slopeSum;
                double response=x<1e-5?rateTime*(1-x/2+x*x/6):(1-Math.Exp(-x))/slopeSum;
                double moved=Math.Min(water,difference*response);
                if(flowEvaporation!=null)flowEvaporation[row]=moved;
                delta[p+Nectar]-=moved;
                delta[p]+=moved;
            }
        }

        void LegacyMoisture(int steps)
        {
            double dt=60d/steps;
            AccumulateHeat();
            var endHeat=(double[])dE.Clone();
            Array.Clear(dE,0,n);
            for(int k=0;k<steps;k++)
            {
                Prepare();
                AccumulateEvaporation(dt);
                foreach(var f in links)
                {
                    // Half the reservoir is available to faces, split among endpoint phases.
                    for(int s=0;s<Nectar;s++)if(Present(f.a*Kinds+s,s))for(int r=0;r<Nectar;r++)
                        Move(f.a*Kinds+s,f.b*Kinds+r,dt*FaceMoistureConductance(f,s,r),f.fa/(2*Kinds),f.fb/(2*Kinds));
                }
                for(int i=0;i<n;i++)
                    for(int r=Wood;r<Nectar;r++)Move(i*Kinds,i*Kinds+r,dt*LocalMoistureConductance(i,r),.5/(Kinds-1),.5);
                if(k==steps-1)for(int i=0;i<n;i++)dE[i]+=endHeat[i];
                CommitChanges();
            }
        }
        void PrepareHeatCapacity()
        {
            for(int i=0;i<n;i++)
            {
                capacityChanged[i]=capacity[i]!=heatCapacityAtLastExchange[i];
                heatCapacityAtLastExchange[i]=capacity[i];
            }
        }
        void AccumulateHeat()
        {
            PrepareHeatCapacity();
            ApplyExteriorHeat();
            HiveClimateKernels.ExchangeHeat(links,capacity,capacityChanged,heatCoefficients,temperature,dE);
        }
        void CommitChanges()
        {
            for(int p=0;p<mass.Length;p++){mass[p]+=delta[p];delta[p]=0;}
            for(int i=0;i<n;i++){energy[i]+=dE[i];dE[i]=0;}
            stateReady=false;
            Prepare(pressures:false);
        }
        public double Temperature(int row) => temperature[row];
        public double RelativeHumidity(int row) => volume[row] > 0
            ? 100 * pressure[row * Kinds] / Psat(temperature[row]) : double.NaN;
        public bool SameInitialState(Model other) =>
            mass.SequenceEqual(other.mass) && energy.SequenceEqual(other.energy);
        public (double m,double e) Audit()
        {
            Prepare();
            if(mass.Any(x=>!(x>=0)||double.IsInfinity(x))||temperature.Any(x=>!(x>-273.15)||double.IsInfinity(x)))throw new Exception("Invalid reservoir or temperature");
            return (mass.Sum(),energy.Sum());
        }
    }
}

