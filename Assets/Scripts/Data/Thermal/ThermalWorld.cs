using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

public sealed class ThermalWorld : IGameMinuteParticipant, IDisposable
{
    private object[] topologyStamp;
    private ThermalTransferCoefficients appliedTransfers;
    public ThermalExchange Exchange { get; private set; }
    public string Failure { get; private set; }
    public int TopologyBuilds { get; private set; }
    public int LocalTopologyUpdates { get; private set; }
    private readonly List<ItemData> minuteItems = new List<ItemData>();
    private readonly HashSet<ItemSlot> watchedSlots = new HashSet<ItemSlot>();
    private readonly HashSet<EquipmentThermalState> tracked = new HashSet<EquipmentThermalState>();
    private readonly HashSet<EquipmentThermalState> dirty = new HashSet<EquipmentThermalState>();
    private readonly List<EquipmentThermalState> refreshWork = new List<EquipmentThermalState>();
    private readonly List<HiveData> watchedHives = new List<HiveData>();
    private int watchedWidth, watchedHeight, balanceRevision;
    private bool layoutDirty = true, topologyDirty = true, viewsDirty = true;

    private void SlotChanged(ItemSlot _) => layoutDirty = true;
    private void GridChanged(HiveSpatialGrid _) => layoutDirty = true;
    private void StateChanged(EquipmentThermalState state) => dirty.Add(state);
    private void Watch(ItemSlot slot)
    {
        if (slot != null && watchedSlots.Add(slot)) slot.OnChanged += SlotChanged;
    }
    private void UnwatchLayout()
    {
        foreach (var slot in watchedSlots) slot.OnChanged -= SlotChanged;
        watchedSlots.Clear();
        foreach (var item in minuteItems)
            if (item.SpatialGrid != null) item.SpatialGrid.Changed -= GridChanged;
    }
    private bool RootsMatch()
    {
        var apiary = world.Apiary;
        if (watchedWidth != apiary.Grid.UnlockedWidth || watchedHeight != apiary.Grid.Height ||
            watchedHives.Count != apiary.Hives.Count) return false;
        for (int i = 0; i < watchedHives.Count; i++)
            if (!ReferenceEquals(watchedHives[i], apiary.Hives[i])) return false;
        return true;
    }
    private void EnsureLayout()
    {
        if (!layoutDirty && RootsMatch()) return;
        UnwatchLayout();
        minuteItems.Clear();
        minuteItems.AddRange(Items(world));
        watchedHives.Clear();
        watchedHives.AddRange(world.Apiary.Hives);
        watchedWidth = world.Apiary.Grid.UnlockedWidth;
        watchedHeight = world.Apiary.Grid.Height;
        foreach (var hive in watchedHives) Watch(hive.Slot);
        for (int y = 0; y < watchedHeight; y++)
            for (int x = 0; x < watchedWidth; x++) Watch(world.Apiary.GetItemSlot(x,y));
        for (int i = 0; i < world.Inventory.Capacity; i++) Watch(world.Inventory.GetSlot(i));
        Watch(world.Hand.Slot);
        var present = new HashSet<EquipmentThermalState>();
        var liveGrids = new HashSet<HiveSpatialGrid>();
        foreach (var item in minuteItems)
        {
            if (item.SpatialGrid != null)
            {
                liveGrids.Add(item.SpatialGrid);
                item.SpatialGrid.Changed += GridChanged;
            }
            if (item.EquipmentGrid == null) continue;
            var state = item.ThermalState;
            present.Add(state);
            if (tracked.Add(state))
            {
                state.InputsChanged += StateChanged;
                dirty.Add(state);
            }
        }
        foreach (var state in tracked.Where(state => !present.Contains(state)).ToArray())
        {
            state.InputsChanged -= StateChanged;
            tracked.Remove(state);
            dirty.Remove(state);
        }
        foreach (var grid in views.Keys.Where(grid => !liveGrids.Contains(grid)).ToArray()) views.Remove(grid);
        layoutDirty = false;
        viewsDirty = true;
        topologyDirty = true;
    }
    public void PrepareExchange()
    {
        EnsureLayout();
        var transfers=materials.Transfers;
        bool settingsChanged=!ReferenceEquals(transfers,appliedTransfers);
        if (!topologyDirty && Exchange != null && !settingsChanged)
        {
            Exchange.RefreshCoefficients();
            return;
        }
        var stamp = ThermalTopology.Signature(world, minuteItems);
        if (Exchange == null || settingsChanged || !stamp.SequenceEqual(topologyStamp))
        {
            if(!settingsChanged && Exchange != null && Exchange.TopologyBinding != null && Exchange.TopologyBinding.TryUpdate(world,Exchange,minuteItems))
                LocalTopologyUpdates++;
            else
            {
                var replacement = ThermalTopology.Build(world, transfers, minuteItems);
                Exchange?.Dispose();
                Exchange = replacement; TopologyBuilds++;
            }
            topologyStamp = stamp;
            appliedTransfers=transfers;
        }
        topologyDirty = false;
        Exchange.RefreshCoefficients();
    }
    public Task CalculateMinuteAsync(long nextMinute, CancellationToken cancellation)
    {
        if (Failure != null) throw new InvalidOperationException("Thermal recovery required: " + Failure);
        try
        {
            cancellation.ThrowIfCancellationRequested();
            if (world.Weather == null || world.Weather.Minute != nextMinute-1)
                throw new InvalidOperationException("Exterior forcing requires weather at the start of the minute");
            // Keep thermal before weather: T_air(n) forces [n,n+1), without advancing RNG on thermal failure.
            double outside=world.Weather.Current.TemperatureCelsius;
            Refresh();
            PrepareExchange();
            SupplyItemHeating(minuteItems, 60);
            Exchange.StepPrepared(60, outside);
            return Task.CompletedTask;
        }
        catch (Exception e) { Failure = e.Message; throw; }
    }
    public double LastHeatingEnergyJ { get; private set; }
    private void SupplyHeating(double seconds)
    {
        EnsureLayout();
        SupplyItemHeating(minuteItems, seconds);
    }
    private void SupplyItemHeating(IReadOnlyList<ItemData> items, double seconds)
    {
        LastHeatingEnergyJ=0;
        foreach(var item in items)if(item.GetContent<HeatingPanelData>() is HeatingPanelData heater)heater.LastSuppliedJoules=0;
        // A heater in inventory or the hand is unplugged. Only interior-slot installations heat.
        foreach(var box in items)if(box.InteriorSlots!=null)
            for(int i=0;i<box.InteriorSlots.Capacity;i++){
                var panel=box.InteriorSlots.GetSlot(i).Item;var heater=panel?.GetContent<HeatingPanelData>();
                if(panel?.Kind!=ItemKind.HeatingPanel||heater==null||!heater.Enabled||heater.OutputWatts==0)continue;
                double joules=seconds*heater.OutputWatts;panel.ThermalState.SupplyHeat(joules);heater.LastSuppliedJoules=joules;LastHeatingEnergyJ+=joules;
            }
    }
    private readonly GameWorld world;
    private ThermalMaterials materials;
    private readonly Dictionary<HiveSpatialGrid, HiveThermalView> views = new Dictionary<HiveSpatialGrid, HiveThermalView>();
    public ThermalWorld(GameWorld owner, ThermalMaterials values) { world = owner; materials = values; }
    public HiveThermalView View(HiveSpatialGrid grid)
    {
        if (!views.TryGetValue(grid, out var view)) { view = new HiveThermalView(grid); views.Add(grid, view); }
        return view;
    }
    public void Refresh(float? fixedOutsideC = null)
    {
        EnsureLayout();
        if (!fixedOutsideC.HasValue && world.Weather == null)
            throw new InvalidOperationException("Weather is required for new thermal cells.");
        float outside = fixedOutsideC ?? (float)world.Weather.Current.TemperatureCelsius;
        var latest = materials.RefreshLiveSnapshot();
        if (!ReferenceEquals(materials, latest)) dirty.UnionWith(tracked);
        materials = latest;
        if (balanceRevision != CombBalance.Revision)
        {
            foreach (var item in minuteItems)
                if (item.Kind == ItemKind.Frame) dirty.Add(item.ThermalState);
            balanceRevision = CombBalance.Revision;
        }
        if (dirty.Count != 0)
        {
            refreshWork.Clear();
            refreshWork.AddRange(dirty);
            foreach (var state in refreshWork)
            {
                state.Refresh(materials, outside);
                dirty.Remove(state); // Refresh consumed all pending inputs for this owner.
            }
            refreshWork.Clear();
        }
        if (viewsDirty)
        {
            foreach (var item in minuteItems)
                if (item.SpatialGrid != null) View(item.SpatialGrid).Refresh(outside);
            viewsDirty = false;
        }
    }
    public void Dispose()
    {
        UnwatchLayout();
        foreach (var state in tracked) state.InputsChanged -= StateChanged;
        tracked.Clear();
        dirty.Clear();
        refreshWork.Clear();
        minuteItems.Clear();
        watchedHives.Clear();
        views.Clear();
        Exchange?.Dispose();
        Exchange = null;
        layoutDirty = true;
    }
    public static IEnumerable<ItemData> Items(GameWorld world)
    {
        var seen = new HashSet<MovableItem>();
        foreach (var hive in world.Apiary.Hives) foreach (var i in Walk(hive.Slot.Value, seen)) yield return i;
        for (int y = 0; y < world.Apiary.Grid.Height; y++) for (int x = 0; x < world.Apiary.Grid.UnlockedWidth; x++)
            foreach (var i in Walk(world.Apiary.GetItemSlot(x,y).Value, seen)) yield return i;
        for (int j = 0; j < world.Inventory.Capacity; j++) foreach (var i in Walk(world.Inventory.GetSlot(j).Value, seen)) yield return i;
        foreach (var i in Walk(world.Hand.Value, seen)) yield return i;
    }
    private static IEnumerable<ItemData> Walk(MovableItem value, HashSet<MovableItem> seen)
    {
        if (value == null || !seen.Add(value)) yield break;
        if (value is ItemAssemblyData assembly)
        { foreach (var part in assembly.Parts) foreach (var i in Walk(part, seen)) yield return i; }
        else if (value is ItemData item)
        {
            yield return item;
            if (item.InteriorSlots != null) for (int j = 0; j < item.InteriorSlots.Capacity; j++)
                foreach (var i in Walk(item.InteriorSlots.GetSlot(j).Value, seen)) yield return i;
            if (item.TopSlot != null) foreach (var i in Walk(item.TopSlot.Value, seen)) yield return i;
        }
    }
}
