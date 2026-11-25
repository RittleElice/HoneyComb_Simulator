using System;
using System.Collections.Generic;

public sealed class HiveVaporPressureMonitor
{
    public VaporPressureCell[] Cells { get; private set; }
    public bool Ready { get; private set; }
    public bool IsStale { get; private set; }
    public long Minute { get; private set; }=-1;
    public string Status => !Ready?"수증기압: 첫 계산 결과 대기 중":IsStale?$"수증기압 갱신 대기 · 표시: 게임 {Minute:N0}분의 마지막 수분 계산 상태":$"수증기압 표시: 게임 {Minute:N0}분의 수분 계산 상태";
    GameWorld sampledWorld;
    HiveSpatialGrid sampledGrid;
    int revision=-1;
    readonly Dictionary<EquipmentThermalState,VaporPressureCell[]> buffers=new Dictionary<EquipmentThermalState,VaporPressureCell[]>();

    public bool Capture(HiveSpatialGrid grid,GameWorld world)
    {
        // Never carry a snapshot into a different world, layout, or rewound timeline.
        if(!ReferenceEquals(sampledWorld,world)||!ReferenceEquals(sampledGrid,grid)||grid==null||revision!=grid.Revision||(world!=null&&world.CurrentGameMinute<Minute))
        {Ready=false;Cells=null;Minute=-1;sampledWorld=world;sampledGrid=grid;revision=grid?.Revision??-1;}
        IsStale=true;
        if(grid==null||world?.Thermal==null)return false;
        buffers.Clear();
        for(int i=0;i<grid.CellCount;i++)
        {
            var source=grid.GetCell(i).SourceCell;
            var owner=(source?.Equipment??grid.Box).ExistingThermalState;
            if(owner==null)return false;
            if(!buffers.ContainsKey(owner))buffers.Add(owner,new VaporPressureCell[owner.Count]);
        }
        // Failure must retain the last complete snapshot and its original timestamp.
        if(!world.Thermal.TryReadVaporPressures(buffers))return false;
        var next=new VaporPressureCell[grid.CellCount];bool temperatureChanged=false;
        for(int i=0;i<grid.CellCount;i++)
        {
            var source=grid.GetCell(i).SourceCell;
            var owner=(source?.Equipment??grid.Box).ExistingThermalState;
            int local=source?.Index??i;
            var cell=buffers[owner][local];next[i]=cell;
            if(cell.Available)
            {
                if(!owner.IsInitialized(local)||owner.IsDirty(local)||owner.Diagnostic!=null)temperatureChanged=true;
                else if(Math.Abs(owner.TemperatureC(local)-cell.TemperatureC)>.001)temperatureChanged=true;
            }
        }
        Cells=next;Minute=world.CurrentGameMinute;Ready=true;IsStale=temperatureChanged;
        return true;
    }

    public VaporPressureCell At(int index)=>Ready && Cells!=null && index>=0 && index<Cells.Length?Cells[index]:default;
}
