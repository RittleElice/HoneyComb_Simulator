using System.Collections.Generic;

public sealed class HiveVaporPressureMonitor
{
    public VaporPressureCell[] Cells { get; private set; }
    public bool Ready { get; private set; }
    public long Minute { get; private set; }=-1;
    readonly Dictionary<EquipmentThermalState,VaporPressureCell[]> buffers=new Dictionary<EquipmentThermalState,VaporPressureCell[]>();

    public bool Capture(HiveSpatialGrid grid,GameWorld world)
    {
        Ready=false;
        Minute=-1;
        Cells=new VaporPressureCell[grid.CellCount];
        buffers.Clear();
        for(int i=0;i<grid.CellCount;i++)
        {
            var source=grid.GetCell(i).SourceCell;
            var owner=source?.Equipment.ThermalState??grid.Box.ThermalState;
            if(!buffers.ContainsKey(owner))buffers.Add(owner,new VaporPressureCell[owner.Count]);
        }
        if(world?.Thermal==null || !world.Thermal.TryReadVaporPressures(buffers))return false;
        for(int i=0;i<grid.CellCount;i++)
        {
            var source=grid.GetCell(i).SourceCell;
            var owner=source?.Equipment.ThermalState??grid.Box.ThermalState;
            Cells[i]=buffers[owner][source?.Index??i];
        }
        Minute=world.CurrentGameMinute;
        Ready=true;
        return true;
    }

    public VaporPressureCell At(int index)=>Ready && Cells!=null && index>=0 && index<Cells.Length?Cells[index]:default;
}
