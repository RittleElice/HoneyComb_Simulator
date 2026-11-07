using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

// A component contains every contact and every physical owner touched by its rows.
internal sealed class ClimatePartition
{
    readonly ThermalExchange source;
    readonly int[] rows;
    readonly ThermalExchange.Face[] faces;
    internal ClimatePartition(ThermalExchange source){this.source=source;}
    internal ClimatePartition(ThermalExchange source,int[] rows,ThermalExchange.Face[] faces)
    {this.source=source;this.rows=rows;this.faces=faces;}
    public int CellCount=>rows?.Length??source.CellCount;
    public int FaceCount=>faces?.Length??source.FaceCount;
    internal int Global(int i)=>rows==null?i:rows[i];
    public ThermalExchange.Cell GetCell(int i)=>source.GetCell(Global(i));
    public ThermalExchange.Face GetFace(int i)=>faces==null?source.GetFace(i):faces[i];
    public float FacePyramidCapacity(int i,int axis,float area)=>source.FacePyramidCapacity(Global(i),axis,area);
    internal double Exterior(int i)=>source.ExteriorConductance(Global(i));
    internal static ClimatePartition[] Split(ThermalExchange ex)
    {
        int n=ex.CellCount;
        var parents=Enumerable.Range(0,n).ToArray();
        int Root(int i){while(i!=parents[i]){parents[i]=parents[parents[i]];i=parents[i];}return i;}
        var owners=new Dictionary<EquipmentThermalState,int>();
        for(int i=0;i<n;i++)
        {
            var c=ex.GetCell(i);
            for(int k=0;k<c.MemberCount;k++){
                var o=c.MemberOwner(k);
                if(owners.TryGetValue(o,out int first))parents[Root(i)]=Root(first);
                else owners.Add(o,i);
            }
        }
        for(int i=0;i<ex.FaceCount;i++){var f=ex.GetFace(i);parents[Root(f.B)]=Root(f.A);}
        var groups=new Dictionary<int,int>();
        var cellGroups=new List<List<int>>();var faceGroups=new List<List<ThermalExchange.Face>>();
        var group=new int[n];var local=new int[n];
        for(int i=0;i<n;i++){
            int key=Root(i);
            if(!groups.TryGetValue(key,out int g)){g=groups.Count;groups.Add(key,g);cellGroups.Add(new List<int>());faceGroups.Add(new List<ThermalExchange.Face>());}
            group[i]=g;local[i]=cellGroups[g].Count;cellGroups[g].Add(i);
        }
        for(int i=0;i<ex.FaceCount;i++){
            var f=ex.GetFace(i);faceGroups[group[f.A]].Add(new ThermalExchange.Face(local[f.A],local[f.B],f.AreaM2,f.HalfAM,f.HalfBM,f.Axis));
        }
        return cellGroups.Select((c,i)=>new ClimatePartition(ex,c.ToArray(),faceGroups[i].ToArray())).ToArray();
    }
}

internal sealed class ClimateDomain
{
    readonly HiveClimate.Model[] models;
    internal ClimateDomain(ThermalExchange ex,List<ItemData> items,ThermalMaterials materials,double humidity)
    {
        var owners=items.Where(i=>i.EquipmentGrid!=null).ToDictionary(i=>i.ThermalState);
        models=ClimatePartition.Split(ex).Select(part=>{
            var m=new HiveClimate.Model(part,owners,materials,false,humidity);
            m.Bind(part);
            return m;
        }).ToArray();
    }
    internal void SetFlowTargets(HashSet<EquipmentThermalState> owners)
    {
        foreach(var model in models)model.SetFlowTarget(owners);
    }
    internal bool ReadFlow(EquipmentThermalState owner,int index,out MoistureFlowSnapshot snapshot)
    {
        foreach(var model in models)if(model.ReadFlow(owner,index,out snapshot))return true;
        snapshot=null;return false;
    }
    internal void Advance(double outside)
    {
        if(models.Length==1)models[0].LiveMinute(outside);
        else Parallel.For(0,models.Length,i=>models[i].LiveMinute(outside));
    }
    internal void Flush(){foreach(var m in models)m.Flush();}
    internal void CopyVaporPressures(Dictionary<EquipmentThermalState,VaporPressureCell[]> target)
    {
        foreach(var model in models)model.CopyVaporPressures(target);
    }
}

public static partial class HiveClimate
{
    public sealed partial class Model
    {
        ThermalExchange.Cell[] physical;
        double[] outsideCoefficient;
        float[] published;
        double outdoors;
        struct WetRegion
        {
            public int row,index,x,z;
            public EquipmentThermalState owner;
            public FrameSurface surface;
            public double previous;
        }
        readonly struct HeaterMember
        {
            internal readonly int row,index;
            internal readonly EquipmentThermalState owner;
            internal HeaterMember(int row,EquipmentThermalState owner,int index)
            {this.row=row;this.owner=owner;this.index=index;}
        }
        HeaterMember[] heaterMembers;
        WetRegion[] wetRegions;
        int[] correctedRows;

        sealed class EquipmentRows
        {
            internal readonly EquipmentThermalState Owner;
            internal readonly (int row,int index)[] Entries;
            internal long Revision;

            internal EquipmentRows(EquipmentThermalState owner,List<(int row,int index)> entries)
            {
                Owner=owner;
                Entries=entries.OrderBy(entry=>entry.index).ToArray();
                Revision=owner.ExternalEnergyRevision;
            }
        }

        EquipmentRows[] equipmentRows;
        int[] sharedRows;
        readonly HashSet<int> importedRows=new HashSet<int>();

        internal void Bind(ClimatePartition grid)
        {
            flowGrid=grid;
            physical=new ThermalExchange.Cell[n];published=new float[n];outsideCoefficient=new double[n];
            var wet=new List<WetRegion>();
            var heaters=new List<HeaterMember>();
            var memberMap=new Dictionary<EquipmentThermalState,List<(int row,int index)>>();
            var shared=new List<int>();
            for(int i=0;i<n;i++){
                var c=physical[i]=grid.GetCell(i);published[i]=c.EnergyJ;
                outsideCoefficient[i]=60*grid.Exterior(i);
                if(c.MemberCount>1)shared.Add(i);
                for(int k=0;k<c.MemberCount;k++){
                    var owner=c.MemberOwner(k);
                    if(!memberMap.TryGetValue(owner,out var entries))
                    {
                        entries=new List<(int,int)>();
                        memberMap.Add(owner,entries);
                    }
                    entries.Add((i,c.MemberIndex(k)));
                    if(owner.ClimateItem.Kind==ItemKind.HeatingPanel)heaters.Add(new HeaterMember(i,owner,c.MemberIndex(k)));
                }
                if(mass[i*Kinds+Nectar]<=0)continue;
                var o=c.Owner;var g=o.ClimateItem.EquipmentGrid;int j=c.Index;
                wet.Add(new WetRegion{row=i,index=j,owner=o,previous=mass[i*Kinds+Nectar],x=j%g.SizeX-1,z=j/(g.SizeX*g.SizeY)-1,
                    surface=o.ClimateItem.GetContent<FrameData>().GetSurface(j/g.SizeX%g.SizeY==0?FrameSide.Front:FrameSide.Back)});
            }
            equipmentRows=memberMap.Select(pair=>new EquipmentRows(pair.Key,pair.Value)).ToArray();
            sharedRows=shared.ToArray();
            heaterMembers=heaters.ToArray();
            wetRegions=wet.ToArray();
            correctedRows=new int[wetRegions.Length];
        }

        // Grouped rows represent geometrically partitioned layers of one material.
        static double Share(ThermalExchange.Cell c,int k)
        {
            if(c.Stack!=null)return c.Stack.Parts[k].volume/c.Stack.Volume;
            return c.IsSlotGroup?HiveGridLayout.SlotLayerEnergyShare(k):1;
        }
        void RestoreMemberWater(ThermalExchange.Cell c,int i,double rh,double t)
        {
            int p=i*Kinds;
            for(int s=0;s<Nectar;s++){
                double initial=mass[p+s],sum=0;
                for(int k=0;k<c.MemberCount;k++){
                    var saved=c.MemberOwner(k).AtmosphericAndBoundWater;
                    int j=c.MemberIndex(k)*4;
                    sum+=saved!=null && saved[j]>=0?saved[j+s]:initial*Share(c,k);
                }
                mass[p+s]=sum;
            }
        }
        internal void Flush()
        {
            if(physical==null)return;
            for(int i=0;i<n;i++){
                var c=physical[i];
                for(int k=0;k<c.MemberCount;k++){
                    var o=c.MemberOwner(k);
                    if(o.AtmosphericAndBoundWater==null){
                        o.AtmosphericAndBoundWater=new double[o.Count*4];
                        for(int t=0;t<o.Count;t++)o.AtmosphericAndBoundWater[t*4]=-1;
                    }
                    int j=4*c.MemberIndex(k);double share=Share(c,k);
                    for(int s=0;s<Nectar;s++)o.AtmosphericAndBoundWater[j+s]=mass[i*Kinds+s]*share;
                }
            }
        }
        internal void CopyVaporPressures(Dictionary<EquipmentThermalState,VaporPressureCell[]> target)
        {
            foreach(var group in equipmentRows)
            {
                if(!target.TryGetValue(group.Owner,out var cells))continue;
                foreach(var entry in group.Entries)
                {
                    int i=entry.row, p=i*Kinds;
                    double t=temperature[i];
                    double actual=volume[i]>0?mass[p]*Rv*(273.15+t)/volume[i]:double.NaN;
                    double equilibrium=double.NaN;
                    var material=group.Owner.ExchangeMaterial(entry.index);
                    int kind=material==HiveMaterial.Wood?Wood:material==HiveMaterial.Insulation?Insulation:Wax;
                    if(limit[p+kind]>0)equilibrium=Psat(t)*(kind==Wood?HiveClimateKernels.WoodActivity(mass[p+kind],limit[p+kind]):mass[p+kind]/limit[p+kind]);
                    cells[entry.index]=new VaporPressureCell(true,t,actual,equilibrium,mass[p],mass[p+Wood],mass[p+Insulation],mass[p+Nectar],physical[i].MemberCount>1);
                }
            }
        }

        ClimatePartition flowGrid;
        double[] flowTransfers,flowEvaporation;
        int[] flowAxes;
        bool flowRequested,flowReady;

        internal void SetFlowTarget(HashSet<EquipmentThermalState> owners)
        {
            flowRequested=equipmentRows.Any(group=>owners.Contains(group.Owner));
            if(flowRequested)
            {
                if(flowEvaporation==null)flowEvaporation=new double[n];
            }
            else {flowTransfers=null;flowAxes=null;flowEvaporation=null;flowReady=false;}
        }
        void PrepareFlowTrace()
        {
            flowReady=false;
            if(!flowRequested)return;
            if(flowTransfers!=null && flowTransfers.Length==contacts.Length)return;
            flowTransfers=new double[contacts.Length];flowAxes=new int[contacts.Length];
            int next=0;
            for(int face=0;face<links.Length;face++)
            {
                var f=links[face];
                for(int a=0;a<Nectar;a++)if(Present(f.a*Kinds+a,a))
                    for(int b=0;b<Nectar;b++)if(Present(f.b*Kinds+b,b))
                        flowAxes[next++]=flowGrid.GetFace(face).Axis;
            }
            while(next<flowAxes.Length)flowAxes[next++]=-1;
        }
        internal bool ReadFlow(EquipmentThermalState owner,int index,out MoistureFlowSnapshot snapshot)
        {
            snapshot=null;
            if(!flowReady || flowTransfers==null)return false;
            var group=equipmentRows.FirstOrDefault(g=>ReferenceEquals(g.Owner,owner));
            if(group==null)return false;
            int row=-1;
            foreach(var entry in group.Entries)if(entry.index==index){row=entry.row;break;}
            if(row<0)return false;
            snapshot=new MoistureFlowSnapshot{EvaporationKg=flowEvaporation[row],Shared=physical[row].MemberCount>1};
            for(int j=0;j<contacts.Length;j++)
            {
                var contact=contacts[j];int a=contact.a/Kinds,b=contact.b/Kinds;
                if(a!=row && b!=row)continue;
                double moved=flowTransfers[j];if(moved==0)continue;
                int donor=moved>0?contact.a:contact.b,receiver=moved>0?contact.b:contact.a;
                bool inward=receiver/Kinds==row;
                int other=a==row?b:a;
                int direction=flowAxes[j]<0?-1:flowAxes[j]*2+(a==row?1:0);
                snapshot.Transfers.Add(new MoistureFlowTransfer(direction,inward,Math.Abs(moved),
                    donor%Kinds,receiver%Kinds,physical[other].Owner.ClimateItem.Name,physical[other].Index));
            }
            return true;
        }

        void ApplyExteriorHeat()
        {
            if(outsideCoefficient==null)return;
            for(int i=0;i<n;i++)dE[i]+=outsideCoefficient[i]*(outdoors-temperature[i]);
        }
        internal void LiveMinute(double outside)
        {
            // The legacy sensible-energy view also receives heater and manual energy inputs.
            outdoors=outside;
            importedRows.Clear();
            foreach(var group in equipmentRows)
            {
                if(group.Revision==group.Owner.ExternalEnergyRevision)continue;
                group.Revision=group.Owner.ExternalEnergyRevision;
                foreach(var entry in group.Entries)
                {
                    int row=entry.row;
                    if(!importedRows.Add(row))continue;
                    float current=physical[row].EnergyJ;
                    if(current==published[row])continue;
                    energy[row]+=(double)current-published[row];
                    stateReady=false;
                }
            }
            foreach(var member in heaterMembers){
                var pending=member.owner.PendingHeaterJ;
                if(pending==null)continue;
                dE[member.row]+=pending[member.index];
                pending[member.index]=0;
            }
            Minute(1);
            int correctedCount=0;
            for(int k=0;k<wetRegions.Length;k++){
                var w=wetRegions[k];int p=w.row*Kinds+Nectar;
                if(mass[p]>=w.previous || w.previous<=0)continue;
                double change=w.surface.ApplyClimateWater(w.x,w.z,mass[p]/w.previous);
                w.owner.AdjustContentWater(w.index,change,liquidCp);
                // FrameSurface stores float milligrams. Retain the conversion residual
                // in the air reservoir, so the source and transported water still balance.
                double actual=w.previous+change;
                if(actual!=mass[p] && mass[w.row*Kinds]+mass[p]-actual>=0){
                    mass[w.row*Kinds]+=mass[p]-actual;mass[p]=actual;correctedRows[correctedCount++]=w.row;
                }
                w.previous=actual;wetRegions[k]=w;
            }
            if(correctedCount>0)
                HiveClimateKernels.RefreshChangedRows(correctedRows,correctedCount,mass,dry,contentFreeVolume,volume,
                    energy,phaseEnergy,capacity,temperature,waterDensity,airCv,liquidCp);
            PublishEquipmentEnergy();
            flowReady=flowRequested;
        }

        void PublishEquipmentEnergy()
        {
            for(int i=0;i<n;i++)
                if(!ThermalExchange.Finite(temperature[i]) || temperature[i]<=-273.15)
                    throw new InvalidOperationException("Invalid live climate temperature");

            foreach(var group in equipmentRows)
            {
                var owner=group.Owner;
                var capacities=owner.HeatCapacityBuffer;
                foreach(var entry in group.Entries)
                {
                    float value=(float)(temperature[entry.row]*capacities[entry.index]);
                    owner.PublishClimateEnergy(entry.index,value);
                    published[entry.row]=value;
                }
            }
            // An external edit to a shared row must use the same float summation order as Cell.EnergyJ.
            foreach(int row in sharedRows)published[row]=physical[row].EnergyJ;
        }
    }
}
