using System;

public enum HiveMaterial { Air, Wood, Comb, Insulation }

// Immutable grid-aligned profile. Only the central three Y cells may be occupied.
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
        => localX >= 1 && localX <= 14 && localZ >= 0 && localZ < 10 &&
           localY >= FirstY && localY < FirstY + CountY;
}
