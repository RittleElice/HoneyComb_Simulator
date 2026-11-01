using System;

public enum HiveMaterial { Air, Wood, Comb, Insulation }

// Physical panel profile in legacy 10/5/10 mm bands.
// These bands are integrated into the two new 12.5 mm control volumes.
// Defaults are modelling presets, not measured product specifications.
public sealed class HiveOccupancyProfile
{
    public static readonly HiveOccupancyProfile Divider = new HiveOccupancyProfile(2, 1, HiveMaterial.Wood);
    public static readonly HiveOccupancyProfile Insulation = new HiveOccupancyProfile(1, 3, HiveMaterial.Insulation);
    public int FirstY { get; }
    public int CountY { get; }
    public HiveMaterial Material { get; }

    public HiveOccupancyProfile(int firstY, int countY, HiveMaterial material)
    {
        if (firstY < 1 || countY < 1 || firstY + countY > 4)
            throw new ArgumentOutOfRangeException(nameof(firstY));
        if (material != HiveMaterial.Wood && material != HiveMaterial.Insulation)
            throw new ArgumentOutOfRangeException(nameof(material));
        FirstY = firstY; CountY = countY; Material = material;
    }

    public bool Occupies(int localX, int localY, int localZ)
        => localX >= 1 && localX <= HiveGridLayout.FrameSizeX && localZ >= 0 && localZ < HiveGridLayout.FrameSizeZ &&
           localY >= FirstY && localY < FirstY + CountY;
}
