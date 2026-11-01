using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

public sealed class ThermalWorld : IGameMinuteParticipant
{
    private object[] topologyStamp;
    public ThermalExchange Exchange { get; private set; }
    public string Failure { get; private set; }
    public int TopologyBuilds { get; private set; }
    public void PrepareExchange()
    {
        var stamp = ThermalTopology.Signature(world);
        if (Exchange == null || !stamp.SequenceEqual(topologyStamp))
        {
            var replacement = ThermalTopology.Build(world, materials.MaximumExchangeFractionPerMinute, materials.ExteriorFilmCoefficientWPerM2K);
            Exchange?.Dispose();
            Exchange = replacement; topologyStamp = stamp; TopologyBuilds++;
        }
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
            Refresh(); PrepareExchange(); Exchange.Step(60,outside);
            return Task.CompletedTask;
        }
        catch (Exception e) { Failure = e.Message; throw; }
    }
    private readonly GameWorld world;
    private readonly ThermalMaterials materials;
    private readonly Dictionary<HiveSpatialGrid, HiveThermalView> views = new Dictionary<HiveSpatialGrid, HiveThermalView>();
    public ThermalWorld(GameWorld owner, ThermalMaterials values) { world = owner; materials = values; }
    public HiveThermalView View(HiveSpatialGrid grid)
    {
        if (!views.TryGetValue(grid, out var view)) { view = new HiveThermalView(grid); views.Add(grid, view); }
        return view;
    }
    public void Refresh(float? fixedOutsideC = null)
    {
        if (!fixedOutsideC.HasValue && world.Weather == null) throw new InvalidOperationException("Weather is required for new thermal cells.");
        float t = fixedOutsideC ?? (float)world.Weather.Current.TemperatureCelsius;
        foreach (var item in Items(world)) if (item.EquipmentGrid != null) item.ThermalState.Refresh(materials, t);
        foreach (var item in Items(world)) if (item.SpatialGrid != null) View(item.SpatialGrid).Refresh(t);
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
