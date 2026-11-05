using System;
using System.Collections.Generic;

// Editor-only, read-only sampling. Never initializes or refreshes simulation state.
public sealed class HiveTemperatureMonitor
{
    public readonly struct Point
    {
        public readonly long Minute;
        public readonly double Temperature, Outside;
        public Point(long minute,double temperature,double outside) { Minute=minute;Temperature=temperature;Outside=outside; }
    }
    private HiveSpatialGrid grid;
    public int[] SharedRows { get; private set; }
    public int SharedLocalCount { get; private set; }
    public readonly HashSet<int> SharedIds=new HashSet<int>();
    private int revision=-1;
    private double[] previous;
    private long previousMinute=-1;
    private readonly List<Point> history=new List<Point>();
    public IReadOnlyList<Point> History => history;
    public double[] Temperatures { get; private set; }
    public double[] Capacities { get; private set; }
    public double[] ContentCapacities { get; private set; }
    public double[] Energies { get; private set; }
    public int Selected { get; private set; }=-1;
    public long Minute { get; private set; }=-1;
    public double Outside { get; private set; }
    public double Minimum { get; private set; }
    public double Maximum { get; private set; }
    public double WeightedMean { get; private set; }
    public bool Matches(HiveSpatialGrid value) => ReferenceEquals(grid,value) && revision==value.Revision && Temperatures!=null;
    public void Reset()
    {
        SharedRows=null;SharedLocalCount=0;SharedIds.Clear();grid=null; revision=-1; Selected=-1; Minute=-1; previousMinute=-1;
        Temperatures=Capacities=ContentCapacities=Energies=previous=null;history.Clear();
    }
    public void Select(int index)
    {
        if(Temperatures==null || index<0 || index>=Temperatures.Length) return;
        index=grid.ActiveCellAt(index).firstIndex;
        if(Selected==index) return;
        Selected=index;history.Clear();Record();
    }
    public void ClearHistory() { history.Clear(); if(Selected>=0) Record(); }
    public bool TryRate(out double rate,out long elapsedMinutes)
    {
        rate=0;elapsedMinutes=Minute-previousMinute;
        if(Selected<0 || previous==null || previousMinute<0 || elapsedMinutes<=0) return false;
        rate=(Temperatures[Selected]-previous[Selected])/elapsedMinutes;return true;
    }
    public bool Capture(HiveSpatialGrid source,long minute,double outside,out string reason)
    {
        reason=null;
        if(source==null || minute<0 || !ThermalExchange.Finite(outside)) {reason="열 상태를 기다리는 중입니다.";return false;}
        bool changed=!ReferenceEquals(source,grid) || revision!=source.Revision || minute<Minute;
        if(changed) Reset();
        var temperatures=new double[source.CellCount];var capacities=new double[source.CellCount];
        var contents=new double[source.CellCount];var energies=new double[source.CellCount];
        double minimum=double.PositiveInfinity,maximum=double.NegativeInfinity,totalEnergy=0,totalCapacity=0;
        var sharedRows=new int[source.CellCount];Array.Fill(sharedRows,-1);
        int sharedLocalCount=0;var ids=new HashSet<int>();
        foreach(var active in source.ActiveCells())
        {
            double capacity=0,energy=0,content=0;
            for(int k=0;k<active.depthCells;k++)
            {
                int i=active.firstIndex+k*HiveGridLayout.SizeX;
                var cell=source.GetCell(i).SourceCell;
                var owner=(cell?.Equipment ?? source.Box).ExistingThermalState;
                int local=cell?.Index ?? i;
                if(owner==null || !owner.IsInitialized(local) || owner.IsDirty(local) || owner.Diagnostic!=null)
                {reason="격자 열 상태 갱신을 기다리는 중입니다.";return false;}
                double c=owner.HeatCapacityJPerK[local],e=owner.EnergyJ[local];
                if(!ThermalExchange.Finite(e) || !ThermalExchange.Finite(c) || c<=0)
                {reason="유효하지 않은 열 상태입니다.";return false;}
                energy+=e;capacity+=c;content+=owner.ContentHeatCapacityJPerK[local];
            }
            totalCapacity+=capacity;totalEnergy+=energy;
            int shared=-1;
            var state=source.Box.ExistingThermalState;
            if(state!=null && state.TryGetSharedAir(active.firstIndex,out int row,out var composite))
            {
                shared=row;ids.Add(row);sharedLocalCount++;
                capacity=composite.HeatCapacityJPerK;energy=composite.EnergyJ;
            }
            double t=energy/capacity;
            for(int k=0;k<active.depthCells;k++)
            {
                int i=active.firstIndex+k*HiveGridLayout.SizeX;
                sharedRows[i]=shared;capacities[i]=capacity;contents[i]=content;energies[i]=energy;temperatures[i]=t;
            }
            minimum=Math.Min(minimum,t);maximum=Math.Max(maximum,t);
        }
        if(Temperatures!=null && minute>Minute) {previous=Temperatures;previousMinute=Minute;}
        SharedRows=sharedRows;SharedLocalCount=sharedLocalCount;SharedIds.Clear();SharedIds.UnionWith(ids);
        grid=source;revision=source.Revision;Minute=minute;Outside=outside;
        Temperatures=temperatures;Capacities=capacities;ContentCapacities=contents;Energies=energies;
        Minimum=minimum;Maximum=maximum;WeightedMean=totalEnergy/totalCapacity;
        if(Selected>=0) Record();
        return true;
    }
    private void Record()
    {
        var point=new Point(Minute,Temperatures[Selected],Outside);
        if(history.Count>0 && history[history.Count-1].Minute==Minute) history[history.Count-1]=point;
        else {history.Add(point);if(history.Count>240)history.RemoveAt(0);}
    }
}
