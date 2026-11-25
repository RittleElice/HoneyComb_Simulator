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
        // Immutable constructor inputs only. Dynamic mass/energy are always restored normally.
        struct RowInput { public double wood,insulation,sugar,water,volume,dry; }
        RowInput[] rowInputs;
        float[] faceCapacities;
        HiveMaterial[] faceMaterials;
        Dictionary<EquipmentThermalState,int> inputRevisions;
        ThermalMaterials inputMaterials;
        int inputBalanceRevision;
        int n;
        Link[] links;
        double[] mass,limit,pressure,delta,energy,dE,temperature,volume,dry,sugar;
        double[] contentFreeVolume,capacity,phaseEnergy;
        int[] nectarRows;
        bool stateReady;
        double waterDensity, airCv, woodDryDensity;
        double liquidCp;
        ThermalTransferCoefficients exteriorTransfers;
        public Model(ThermalExchange ex, Dictionary<EquipmentThermalState,ItemData> owners,ThermalMaterials materials,bool warmGradient=false)
            : this(new ClimatePartition(ex),owners,materials,warmGradient,null) {}
        internal Model(ClimatePartition ex, Dictionary<EquipmentThermalState,ItemData> owners,ThermalMaterials materials,
            bool warmGradient,double? initialRh,Model previous=null)
        { InitializeAsync(ex,owners,materials,warmGradient,initialRh,previous,null).GetAwaiter().GetResult(); }
        private Model() { }
        internal static async Task<Model> CreateAsync(ClimatePartition ex, Dictionary<EquipmentThermalState,ItemData> owners,
            ThermalMaterials materials,double humidity,Model previous,SimulationSlice slice)
        {
            var model=new Model();
            await model.InitializeAsync(ex,owners,materials,false,humidity,previous,slice);
            return model;
        }
        async Task InitializeAsync(ClimatePartition ex, Dictionary<EquipmentThermalState,ItemData> owners,
            ThermalMaterials materials,bool warmGradient,double? initialRh,Model previous,SimulationSlice slice)
        {
            n=ex.CellCount;exteriorTransfers=materials.Transfers;
            inputMaterials=materials;inputBalanceRevision=CombBalance.Revision;
            faceCapacities=new float[n];faceMaterials=new HiveMaterial[n];
            rowInputs=new RowInput[n];inputRevisions=new Dictionary<EquipmentThermalState,int>();
            if(previous!=null && (previous.n!=n || !ReferenceEquals(previous.inputMaterials,materials) || previous.inputBalanceRevision!=inputBalanceRevision))previous=null;
            foreach(var owner in owners.Keys)inputRevisions[owner]=owner.CacheRevision;
            phaseEnergy=new double[n];capacity=new double[n];
            links=new Link[ex.FaceCount];
            mass=new double[n*Kinds];limit=new double[mass.Length];delta=new double[mass.Length];
            energy=new double[n];dE=new double[n];temperature=new double[n];volume=new double[n];dry=new double[n];sugar=new double[n];contentFreeVolume=new double[n];
            var air=materials[ThermalMaterialKind.Air];
            airCv=air.EffectiveVolumetricHeatCapacityJPerM3K>0?air.EffectiveVolumetricHeatCapacityJPerM3K:air.Density*air.SpecificHeat;
            woodDryDensity=materials[ThermalMaterialKind.Wood].Density/(1+HiveClimateKernels.WoodReferenceMoisture);
            var balance=CombBalance.Snapshot();
            waterDensity=1000*balance.WaterDensityGPerMl;
            liquidCp=balance.WaterSpecificHeatJPerKgK;
            for(int i=0;i<n;i++)
            {
                if(slice!=null && (i&63)==0)await slice.CheckpointAsync();
                var c=ex.GetCell(i);
                faceCapacities[i]=c.HeatCapacityJPerK;faceMaterials[i]=c.Owner.ExchangeMaterial(c.Index);
                bool reuse=previous!=null;
                if(reuse)for(int k=0;k<c.MemberCount;k++) {
                    var owner=c.MemberOwner(k);
                    // Evaporation adjusts frame contents/capacity without a cache revision.
                    // Re-read frame inputs; only unchanged non-frame inputs may be reused.
                    if(owner.ClimateItem.Kind==ItemKind.Frame || !previous.inputRevisions.TryGetValue(owner,out int revision) || revision!=owner.CacheRevision){reuse=false;break;}
                }
                if(reuse) {
                    var cached=previous.rowInputs[i];
                    limit[i*Kinds+Wood]=cached.wood;limit[i*Kinds+Insulation]=cached.insulation;
                    sugar[i]=cached.sugar;mass[i*Kinds+Nectar]=cached.water;volume[i]=cached.volume;dry[i]=cached.dry;
                }
                else for(int k=0;k<c.MemberCount;k++)
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
                rowInputs[i]=new RowInput{wood=limit[i*Kinds+Wood],insulation=limit[i*Kinds+Insulation],sugar=sugar[i],water=mass[i*Kinds+Nectar],volume=volume[i],dry=dry[i]};
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
                if(slice!=null && (j&127)==0)await slice.CheckpointAsync();
                var f=ex.GetFace(j);
                // Recompute from geometry and current axis-specific conductivity, including conductivity-only edits.
                links[j]=new Link{a=f.A,b=f.B,area=f.AreaM2,halfA=f.HalfAM,halfB=f.HalfBM,fa=1,
                    fb=1,
                    heat=ThermalExchange.InternalConductance(f,ex.GetCell(f.A),ex.GetCell(f.B))};
            }
        }
        double Cp(int s)=>s==Air?VaporCp:s==Nectar?liquidCp:LiquidCp;
        double Phase(int i){double e=0;for(int s=0;s<Kinds;s++)e+=mass[i*Kinds+s]*Offset[s];return e;}
        double Capacity(int i){double c=dry[i]+volume[i]*airCv;for(int s=0;s<Kinds;s++)c+=mass[i*Kinds+s]*Cp(s);return c;}
#if UNITY_EDITOR
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
#endif
        void Prepare(bool pressures=true)
        {
            if(!stateReady)
            {
                HiveClimateKernels.State(mass,dry,contentFreeVolume,volume,energy,phaseEnergy,capacity,temperature,waterDensity,airCv,liquidCp);
                stateReady=true;
            }
            if(!pressures)return;
            // Diagnostics only: ordinary balance ticks do not use these arrays.
            if(pressure==null){pressure=new double[mass.Length];slope=new double[mass.Length];}
            HiveClimateKernels.PressureAndSlope(mass,limit,volume,temperature,pressure,slope);
        }
        bool Present(int p,int s)=>s==Air?volume[p/Kinds]>0:s==Nectar?mass[p]>0:limit[p]>0;
        Contact[] contacts;
        HiveClimateKernels.ExteriorContact[] exteriorContacts=Array.Empty<HiveClimateKernels.ExteriorContact>();
        double[] slope;
        double MoistureMobility(int kind)=>kind==Air?exteriorTransfers.MoistureAirMobility:
            kind==Insulation?exteriorTransfers.MoistureInsulationMobility:
            kind==Nectar?exteriorTransfers.MoistureEvaporationMobility:Mobility[kind];
        double AirFilmResistance()=>exteriorTransfers.MoistureAirPermeability>0?
            exteriorTransfers.MoistureExteriorAirFilmM/exteriorTransfers.MoistureAirPermeability:double.PositiveInfinity;
        double MoistureResistance(int kind,double half)
        {
            if(kind==Wood)return exteriorTransfers.MoistureWoodPermeability>0?half/exteriorTransfers.MoistureWoodPermeability:double.PositiveInfinity;
            if(kind==Air)return exteriorTransfers.MoistureAirPermeability>0?half/exteriorTransfers.MoistureAirPermeability:double.PositiveInfinity;
            return .5/(1e-9*MoistureMobility(kind)); // retain legacy non-wood interface scale
        }
        double FaceMoistureConductance(Link f,int a,int b)
        {
            if(a!=Wood && b!=Wood && !(a==Air && b==Air))return f.area*1e-9*Math.Min(MoistureMobility(a),MoistureMobility(b));
            return f.area/(MoistureResistance(a,f.halfA)+MoistureResistance(b,f.halfB));
        }
        double LocalMoistureConductance(int row,int kind)
        {
            // Current pure-wood cells have no local air reservoir. Keep the legacy
            // mixed-cell interface law for other materials; wood uses a cube-length
            // approximation if a future mixed cell exposes a local air reservoir.
            if(kind!=Wood)return 1e-9*Math.Pow(volume[row],2d/3)*MoistureMobility(kind);
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

            void Add(int a,int b,double g,double fa,double fb)
            {
                active.Add(new Contact {a=a,b=b,conductance=g,fractionA=fa,fractionB=fb});
            }
            foreach(var f in links)
            {
                if(presentCount[f.a]==0 || presentCount[f.b]==0)continue;
                double fa=1;
                double fb=1;
                for(int s=0;s<Nectar;s++)if(Present(f.a*Kinds+s,s))
                    for(int r=0;r<Nectar;r++)if(Present(f.b*Kinds+r,r))
                        Add(f.a*Kinds+s,f.b*Kinds+r,FaceMoistureConductance(f,s,r),fa,fb);
            }
            for(int i=0;i<n;i++)
            {
                if(volume[i]<=0 || presentCount[i]<2)continue;
                for(int r=Wood;r<Nectar;r++)if(Present(i*Kinds+r,r))
                    Add(i*Kinds,i*Kinds+r,LocalMoistureConductance(i,r),1,1);
            }
            contacts=active.ToArray();
            var exterior=new List<HiveClimateKernels.ExteriorContact>();
            if(flowGrid!=null)for(int j=0;j<flowGrid.BoundaryCount;j++){
                var b=flowGrid.GetBoundary(j);int row=b.Cell;
                double fraction=1;
                for(int kind=0;kind<Nectar;kind++)if(Present(row*Kinds+kind,kind)){
                    double resistance=MoistureResistance(kind,b.HalfM)+AirFilmResistance();
                    exterior.Add(new HiveClimateKernels.ExteriorContact{reservoir=row*Kinds+kind,direction=b.Direction,
                        conductance=b.AreaM2/resistance,fraction=fraction,area=b.AreaM2,materialResistance=MoistureResistance(kind,b.HalfM)});
                }
            }
            exteriorContacts=exterior.ToArray();
        }
        public void Minute(int steps)
        {
            if(steps!=1)throw new ArgumentOutOfRangeException(nameof(steps),"Balance solver uses one 60-second tick.");
            ExternalWaterKg=ExternalMoistureEnergyJ=ExternalSensibleEnergyJ=0;
            Prepare(pressures:false);
            if(contacts==null)BuildContacts();
            PrepareFlowTrace();
            AccumulateEvaporation(60);
#if UNITY_EDITOR
            Mark(1);
#endif
            try{AccumulateBalance();}catch{Array.Clear(delta,0,delta.Length);throw;}
#if UNITY_EDITOR
            Mark(4);
#endif
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
                double rateTime=seconds*1e-9*MoistureMobility(Nectar)*Math.Pow(airVolume,2d/3);
                double x=rateTime*slopeSum;
                double response=x<1e-5?rateTime*(1-x/2+x*x/6):(1-Math.Exp(-x))/slopeSum;
                double moved=Math.Min(water,difference*response);
                if(flowEvaporation!=null)flowEvaporation[row]=moved;
                delta[p+Nectar]-=moved;
                delta[p]+=moved;
            }
        }

        public double Temperature(int row) => temperature[row];
        public double RelativeHumidity(int row){Prepare();return volume[row]>0?100*pressure[row*Kinds]/Psat(temperature[row]):double.NaN;}
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

