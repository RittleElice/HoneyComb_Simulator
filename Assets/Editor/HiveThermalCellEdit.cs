using System;
using System.Collections.Generic;

// Editor-only intervention in one complete solver cell, including merged air members.
internal static class HiveThermalCellEdit
{
    internal static void Apply(HiveSpatialGrid grid,int index,double value,bool temperature,ThermalExchange exchange=null)
    {
        if(grid==null || index<0 || index>=grid.CellCount || !ThermalExchange.Finite(value))throw new ArgumentException("유효한 격자와 유한한 값을 입력하세요.");
        var active=grid.ActiveCellAt(index);index=active.firstIndex;
        var members=new List<(EquipmentThermalState owner,int index)>();
        var state=grid.Box.ExistingThermalState;
        ThermalExchange.Cell shared=default;bool hasShared=state!=null && state.TryGetSharedAir(index,out _,out shared);
        // Bee heat writes can unbind native storage; topology remains authoritative.
        if(!hasShared && exchange!=null)
            for(int row=0;row<exchange.CellCount && !hasShared;row++)
            {var candidate=exchange.GetCell(row);if(!candidate.IsMergedAir)continue;
             for(int k=0;k<candidate.MemberCount;k++)if(ReferenceEquals(candidate.MemberOwner(k),state)&&candidate.MemberIndex(k)==index){shared=candidate;hasShared=true;break;}}
        if(hasShared)
        {for(int k=0;k<shared.MemberCount;k++)members.Add((shared.MemberOwner(k),shared.MemberIndex(k)));}
        else for(int k=0;k<active.depthCells;k++)
        {int i=index+k*HiveGridLayout.SizeX;var source=grid.GetCell(i).SourceCell;members.Add(((source?.Equipment??grid.Box).ExistingThermalState,source?.Index??i));}
        double capacity=0;
        foreach(var m in members)
        {
            if(m.owner==null || !m.owner.IsInitialized(m.index) || m.owner.IsDirty(m.index) || m.owner.Diagnostic!=null)throw new InvalidOperationException("열 상태 갱신이 완료된 후 적용하세요.");
            double c=m.owner.HeatCapacityJPerK[m.index];
            if(!ThermalExchange.Finite(c)||c<=0)throw new InvalidOperationException("유효하지 않은 열용량입니다.");
            capacity+=c;
        }
        double target=temperature?value:value/capacity;
        if(!ThermalExchange.Finite(target)||target < -273.15)throw new ArgumentOutOfRangeException(nameof(value),"환산 온도는 절대영도(-273.15°C) 이상이어야 합니다.");
        var energies=new float[members.Count];
        for(int k=0;k<members.Count;k++)
        {
            double e=target*members[k].owner.HeatCapacityJPerK[members[k].index];
            if(!ThermalExchange.Finite(e)||e>float.MaxValue||e< -float.MaxValue)throw new ArgumentOutOfRangeException(nameof(value),"열에너지가 지원 범위를 초과합니다.");
            energies[k]=(float)e;
        }
        // Prevalidate every member so invalid input cannot partially modify a cell.
        for(int k=0;k<members.Count;k++)members[k].owner.RestoreEnergy(members[k].index,energies[k]);
    }
}
