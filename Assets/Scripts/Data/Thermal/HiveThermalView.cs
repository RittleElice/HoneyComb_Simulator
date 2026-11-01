using System;

// Placement view only: equipment energy has one owner, never a copied authoritative value.
public sealed class HiveThermalView
{
    private readonly HiveSpatialGrid grid;
    private readonly EquipmentSpatialCell[] previous;
    private bool bound;
    private int revision = -1;
    public HiveThermalView(HiveSpatialGrid source) { grid = source; previous = new EquipmentSpatialCell[grid.CellCount]; }
    public EquipmentThermalState Owner(int index) => grid.GetCell(index).SourceCell?.Equipment.ThermalState ?? grid.Box.ThermalState;
    public int OwnerIndex(int index) => grid.GetCell(index).SourceCell?.Index ?? index;
    public float EnergyJ(int index) => Owner(index).EnergyJ[OwnerIndex(index)];
    public float HeatCapacityJPerK(int index) => Owner(index).HeatCapacityJPerK[OwnerIndex(index)];
    public float AirVolumeM3(int index) => Owner(index).AirVolumeM3[OwnerIndex(index)];
    public float ConductivityWPerMK(int index) => Owner(index).EffectiveConductivityWPerMK[OwnerIndex(index)];
    public float TemperatureC(int index) => Owner(index).TemperatureC(OwnerIndex(index));
    internal void Refresh(float outsideC)
    {
        if (bound && revision == grid.Revision) return;
        for (int i = 0; i < grid.CellCount; i++)
        {
            var source = grid.GetCell(i).SourceCell;
            // Newly exposed ambient cell is newly created air, not old air kept under a frame.
            if (bound && previous[i] != null && source == null)
                grid.Box.ThermalState.RestoreEnergy(i, grid.Box.ThermalState.HeatCapacityJPerK[i] * outsideC);
            previous[i] = source;
        }
        bound = true;
        revision = grid.Revision;
    }
}
