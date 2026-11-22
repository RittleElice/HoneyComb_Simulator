using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

public static class BeeStartupRegression
{
    public static void Run()
    {
        foreach (long minute in new long[] { 0, 1440 })
        {
            var world = NewGameWeatherPreparation.CreateAsync(minute,
                WeatherAssetLoader.Create(WeatherSimulator.DefaultSeed),
                () => Task.CompletedTask, () => Task.CompletedTask,
                CancellationToken.None).GetAwaiter().GetResult();
            try
            {
                if (world.Thermal != null) throw new Exception("Test must start without thermal initialization");
                world.BeeCycle.SeedDemo();
                if (world.BeeCycle.Bees.Count != world.BeeCycle.Settings.demoWorkers + 1)
                    throw new Exception("Initial bee population missing");
                if (world.CurrentGameMinute != minute) throw new Exception("Seeding advanced time");
                foreach (var region in world.BeeCycle.Regions)
                    if (double.IsNaN(region.temperature) || double.IsInfinity(region.temperature))
                        throw new Exception("Invalid initial region temperature");
                world.PrepareThermal(ThermalMaterials.Load()); // BeginWorld initialization
                var scheduler = GameTimeRuntime.CreateWorldScheduler(world);
                scheduler.RunToAsync(minute + 2, () => Task.CompletedTask,
                    () => Task.CompletedTask, CancellationToken.None).GetAwaiter().GetResult();
                var bytes = WorldSaveCodec.Encode(new WorldSaveSnapshot {
                    World = world, UtcTicks = 1, TargetMinute = world.CurrentGameMinute });
                var loaded = WorldSaveCodec.Decode(bytes).World;
                try {
                    if (loaded.BeeCycle.Bees.Count != world.BeeCycle.Bees.Count)
                        throw new Exception("Startup save lost bees");
                } finally { loaded.Thermal?.Dispose(); }
                Debug.Log("Bee startup regression PASS: minute=" + minute + ", bees=" + world.BeeCycle.Bees.Count);
            }
            finally { world.Thermal?.Dispose(); }
        }
    }
}
