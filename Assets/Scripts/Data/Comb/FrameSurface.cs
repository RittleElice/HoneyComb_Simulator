using System;

public sealed partial class FrameSurface
{
    private readonly CellData[] cells;
    private CellMaterialStore materials;
    private CombRegionThermalMass[] thermalRegions;
    private bool thermalDirty = true;
    private void AdvanceRevision() { unchecked { Revision++; } thermalDirty = true; }

    public event Action<FrameSurface, CellContentLoss> ContentLost;
    // Array payload only; excludes small object headers and the existing spatial/UI grids.
    public long AllocatedDataBytes => (long)cells.Length * 8 + (materials?.AllocatedDataBytes ?? 0) + (thermalRegions == null ? 0 : 96L * 32);
    public bool HasQuantifiedContent(int x, int y) => materials != null && materials.Has(GetIndex(x, y));


    public int Revision { get; private set; }
    public bool TrySetType(int x, int y, CombCellType type)
    {
        if (!IsInside(x, y)) return false;
        int index = GetIndex(x, y);
        var before = cells[index];
        cells[index].SetType(type);
        MarkChanged(index, before);
        return true;
    }

    public int Width { get; }
    public int Height { get; }
    public int CellCount => cells.Length;

    public FrameSurface(int width, int height)
    {
        if (width <= 0)
            throw new ArgumentOutOfRangeException(nameof(width));

        if (height <= 0)
            throw new ArgumentOutOfRangeException(nameof(height));

        Width = width;
        Height = height;

        cells = new CellData[checked(width * height)];
    }

    public bool IsInside(int x, int y)
    {
        return x >= 0 && x < Width &&
               y >= 0 && y < Height;
    }

    public int GetIndex(int x, int y)
    {
        if (!IsInside(x, y))
            throw new ArgumentOutOfRangeException();

        return y * Width + x;
    }

    public CellData GetCell(int x, int y)
    {
        return cells[GetIndex(x, y)];
    }

    public bool TrySetConstruction(int x, int y, byte value)
    {
        if (!IsInside(x, y))
            return false;

        int index = GetIndex(x, y);

        var before = cells[index];
        cells[index].SetConstruction(value);
        MarkChanged(index, before);
        return true;
    }

    public bool TrySetContent(
        int x,
        int y,
        CellContentKind content)
    {
        if (!IsInside(x, y))
            return false;

        int index = GetIndex(x, y);

        var before = cells[index];
        bool success = cells[index].TrySetContent(content);
        MarkChanged(index, before);
        return success;
    }

    public bool TrySetStoredContent(int x, int y, CellContentKind content, float fillRatio)
    {
        if (!IsInside(x, y)) return false;
        int index = GetIndex(x, y);
        var before = cells[index];
        // Legacy ratios cannot overwrite physical masses. Explicit clear remains available.
        if (materials != null && materials.Has(index)) return false;
        bool success = cells[index].TrySetStoredContent(content, fillRatio);
        MarkChanged(index, before);
        return success;
    }

    public bool TrySetFillRatio(int x, int y, float value)
    {
        if (!IsInside(x, y)) return false;
        int index = GetIndex(x, y);
        var before = cells[index];
        if (materials != null && materials.Has(index)) return false;
        bool success = cells[index].TrySetFillRatio(value);
        MarkChanged(index, before);
        return success;
    }

    public bool TrySetCapped(int x, int y, bool value)
    {
        if (!IsInside(x, y)) return false;
        int index = GetIndex(x, y);
        var before = cells[index];
        bool success = cells[index].TrySetCapped(value);
        MarkChanged(index, before);
        return success;
    }

    private static bool SameState(CellData a, CellData b) =>
        a.Type == b.Type && a.Construction == b.Construction && a.Content == b.Content &&
        a.FillRatio == b.FillRatio && a.CappingProgress == b.CappingProgress && a.IsComplete == b.IsComplete;

    private void MarkChanged(int index, CellData before)
    {
        if (SameState(before, cells[index])) return;
        CellContentLoss loss = default;
        bool removed = before.Content != CellContentKind.Empty && before.Content != cells[index].Content;
        if (removed)
        {
            bool quantified = materials != null && materials.Has(index);
            double sugar = quantified && before.Content == CellContentKind.Nectar ? materials.TotalSugar(index) : 0;
            double water = quantified && before.Content == CellContentKind.Nectar ? materials.Mass(index) : 0;
            double pollen = quantified && before.Content == CellContentKind.Pollen ? materials.Mass(index) : 0;
            long bee = quantified && before.Content == CellContentKind.Brood ? materials.Bee(index) : 0;
            var reason = before.IsComplete && !cells[index].IsComplete ? CellRemovalReason.ConstructionCollapse :
                cells[index].Content == CellContentKind.Empty ? CellRemovalReason.ExplicitClear : CellRemovalReason.LegacyReplacement;
            loss = new CellContentLoss(index % Width, index / Width, before, reason, sugar, water, pollen, bee);
            materials?.Clear(index, before.Content);
        }
        AdvanceRevision();
        if (removed) ContentLost?.Invoke(this, loss);
    }

    public void SetAllConstruction(byte value)
    {
        for (int i = 0; i < cells.Length; i++)
        {
            var before = cells[i]; cells[i].SetConstruction(value); MarkChanged(i, before);
        }
    }

    public bool TrySetCappingProgress(int x, int y, byte value)
    {
        if (!IsInside(x, y)) return false;
        int i = GetIndex(x, y); var before = cells[i];
        bool ok = cells[i].TrySetCappingProgress(value); MarkChanged(i, before); return ok;
    }
    public bool TryClearContent(int x, int y) => TrySetContent(x, y, CellContentKind.Empty);

    private bool CanWriteMaterial(int x, int y, CellContentKind kind, out int index)
    {
        index = -1;
        if (!IsInside(x, y)) return false;
        index = GetIndex(x, y);
        return cells[index].IsComplete && (cells[index].Content == CellContentKind.Empty ||
            (cells[index].Content == kind && materials != null && materials.Has(index)));
    }
    // Milligrams, float32 (~7 significant digits). Source ID is an opaque nonnegative
    // stable forage ID. No implicit mixing with unquantified legacy FillRatio data.
    public bool TryAddNectar(int x, int y, int sourceId, float sugarMg, float waterMg)
    {
        if (sourceId < 0 || !CellMaterialStore.ValidMass(sugarMg) || !CellMaterialStore.ValidMass(waterMg) ||
            (sugarMg == 0 && waterMg == 0) || !CanWriteMaterial(x, y, CellContentKind.Nectar, out int i)) return false;
        double sugar = (materials?.TotalSugar(i) ?? 0) + sugarMg;
        double water = (double)(materials?.Mass(i) ?? 0) + waterMg;
        if (CombBalance.NectarVolumeMl(sugar, water) > CombBalance.LiquidCapacityMl * (1 + 1e-6)) return false;
        if (materials == null) materials = new CellMaterialStore(cells.Length);
        if (!materials.AddNectar(i, sourceId, sugarMg, waterMg)) return false;
        cells[i].SetQuantifiedContent(CellContentKind.Nectar); AdvanceRevision(); return true;
    }
    public bool TryAddPollen(int x, int y, float pollenMg)
    {
        if (!CellMaterialStore.ValidMass(pollenMg) || pollenMg == 0 ||
            !CanWriteMaterial(x, y, CellContentKind.Pollen, out int i)) return false;
        if ((double)(materials?.Mass(i) ?? 0) + pollenMg > CombBalance.PollenCapacityMg * (1 + 1e-6)) return false;
        if (materials == null) materials = new CellMaterialStore(cells.Length);
        if (!materials.AddPollen(i, pollenMg)) return false;
        cells[i].SetQuantifiedContent(CellContentKind.Pollen); AdvanceRevision(); return true;
    }
    public bool TrySetBrood(int x, int y, long beeId)
    {
        if (beeId <= 0 || !CanWriteMaterial(x, y, CellContentKind.Brood, out int i)) return false;
        if (materials == null) materials = new CellMaterialStore(cells.Length);
        if (cells[i].Content == CellContentKind.Brood) return materials.Bee(i) == beeId;
        materials.SetBee(i, beeId); cells[i].SetQuantifiedContent(CellContentKind.Brood);
        AdvanceRevision(); return true;
    }
    public float GetWaterMassMg(int x, int y) => GetCell(x, y).Content == CellContentKind.Nectar ? materials?.Mass(GetIndex(x, y)) ?? 0 : 0;
    public float GetPollenMassMg(int x, int y) => GetCell(x, y).Content == CellContentKind.Pollen ? materials?.Mass(GetIndex(x, y)) ?? 0 : 0;
    public long GetBeeId(int x, int y) => GetCell(x, y).Content == CellContentKind.Brood ? materials?.Bee(GetIndex(x, y)) ?? 0 : 0;
    public double GetTotalSugarMassMg(int x, int y) => GetCell(x, y).Content == CellContentKind.Nectar ? materials?.TotalSugar(GetIndex(x, y)) ?? 0 : 0;
    public float GetSugarMassMg(int x, int y, int sourceId) => GetCell(x, y).Content == CellContentKind.Nectar ? materials?.SugarMass(GetIndex(x, y), sourceId) ?? 0 : 0;
    public void CopyNectarSugars(int x, int y, System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<int, float>> output)
    {
        if (output == null) throw new ArgumentNullException(nameof(output));
        int i = GetIndex(x, y); output.Clear();
        if (cells[i].Content == CellContentKind.Nectar) materials?.CopySugars(i, output);
    }

    // Worker capacity is deliberately shared by all existing type tags until drone modelling is implemented.
    // The ratio may exceed one after tuning capacity down. Existing material is never discarded.
    public double GetCapacityFillRatio(int x, int y)
    {
        var cell = GetCell(x, y);
        if (!HasQuantifiedContent(x, y)) return cell.FillRatio;
        if (cell.Content == CellContentKind.Nectar)
            return CombBalance.NectarVolumeMl(GetTotalSugarMassMg(x,y), GetWaterMassMg(x,y)) / CombBalance.LiquidCapacityMl;
        if (cell.Content == CellContentKind.Pollen) return GetPollenMassMg(x,y) / CombBalance.PollenCapacityMg;
        return 0;
    }

    public CombRegionThermalMass GetThermalRegion(int regionX, int regionZ)
    {
        if (Width != 84 || Height != 40) throw new InvalidOperationException("Thermal mapping requires an 84 x 40 surface.");
        if (regionX < 0 || regionX >= 12 || regionZ < 0 || regionZ >= 8) throw new ArgumentOutOfRangeException();
        if (thermalRegions == null) thermalRegions = new CombRegionThermalMass[96];
        if (thermalDirty)
        {
            Array.Clear(thermalRegions, 0, thermalRegions.Length);
            for (int i = 0; i < cells.Length; i++)
            {
                if (cells[i].Content == CellContentKind.Empty) continue;
                int region = (i % Width) / 7 + 12 * ((i / Width) / 5);
                var mass = thermalRegions[region];
                if (materials == null || !materials.Has(i)) mass.UnquantifiedCellCount++;
                else switch (cells[i].Content)
                {
                    case CellContentKind.Nectar: mass.SugarMg += materials.TotalSugar(i); mass.WaterMg += materials.Mass(i); break;
                    case CellContentKind.Pollen: mass.PollenMg += materials.Mass(i); break;
                    case CellContentKind.Brood: mass.BroodCount++; break;
                }
                thermalRegions[region] = mass;
            }
            thermalDirty = false;
        }
        return thermalRegions[regionX + 12 * regionZ];
    }

    public int CountCompletedCells()
    {
        int count = 0;

        for (int i = 0; i < cells.Length; i++)
        {
            if (cells[i].IsComplete)
                count++;
        }

        return count;
    }

    public float GetAverageConstruction()
    {
        long sum = 0;

        for (int i = 0; i < cells.Length; i++)
            sum += cells[i].Construction;

        return cells.Length > 0
            ? (float)sum / cells.Length
            : 0f;
    }
}
