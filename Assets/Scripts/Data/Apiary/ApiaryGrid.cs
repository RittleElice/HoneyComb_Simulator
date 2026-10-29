
using System;

public class ApiaryGrid
{
    public const int InitialUnlockedWidth = 8;

    // 전체 생성된 격자 너비 (잠긴 열 포함)
    public int Width { get; private set; }

    public int Height { get; private set; }

    // 실제 사용 가능한 너비
    public int UnlockedWidth { get; private set; }

    // 다음 해금 대상 열
    public int LockedColumnX => UnlockedWidth;

    private ItemSlot[,] itemSlots;

    public ApiaryGrid(int width, int height)
    {
        if (width <= 0)
            throw new ArgumentOutOfRangeException(nameof(width));

        if (height <= 0)
            throw new ArgumentOutOfRangeException(nameof(height));

        UnlockedWidth = width;
        Width = checked(width + 1);
        Height = height;

        itemSlots = new ItemSlot[Width, Height];

        // 해금된 영역만 슬롯 생성
        for (int x = 0; x < UnlockedWidth; x++)
        {
            for (int y = 0; y < Height; y++)
            {
                itemSlots[x, y] = CreateApiarySlot();
            }
        }
    }

    private static ItemSlot CreateApiarySlot()
    {
        return new ItemSlot(
            ApiaryPlacementRules.CanPlace,
            true
        );
    }

    public bool IsInside(int x, int y)
    {
        return x >= 0 &&
               x < Width &&
               y >= 0 &&
               y < Height;
    }

    public bool IsUnlocked(int x, int y)
    {
        return IsInside(x, y) &&
               x < UnlockedWidth;
    }

    public bool IsLocked(int x, int y)
    {
        return IsInside(x, y) &&
               x >= UnlockedWidth;
    }

    public bool IsEmpty(int x, int y)
    {
        ItemSlot slot = GetItemSlot(x, y);

        return slot != null && slot.IsEmpty;
    }

    // 잠긴 격자는 슬롯을 반환하지 않음
    public ItemSlot GetItemSlot(int x, int y)
    {
        if (!IsUnlocked(x, y))
            return null;

        return itemSlots[x, y];
    }

    public int GetHiveId(int x, int y)
    {
        ItemSlot slot = GetItemSlot(x, y);

        if (slot == null || slot.IsEmpty)
            return 0;

        if (slot.Value is ItemData item)
            return GetHiveIdFromPart(item);

        if (slot.Value is ItemAssemblyData assembly)
        {
            foreach (ItemData part in assembly.Parts)
            {
                int hiveId = GetHiveIdFromPart(part);

                if (hiveId > 0)
                    return hiveId;
            }
        }

        return 0;
    }

    private static int GetHiveIdFromPart(ItemData item)
    {
        if (item == null ||
            item.Kind != ItemKind.BroodBox)
            return 0;

        HiveData hive = item.GetContent<HiveData>();

        return hive != null ? hive.Id : 0;
    }

    // 잠긴 1열 해금
    public bool TryUnlockNextColumn()
    {
        if (UnlockedWidth >= int.MaxValue - 1)
            return false;

        int nextUnlockedWidth = UnlockedWidth + 1;
        int nextTotalWidth = nextUnlockedWidth + 1;

        // 새 잠긴 열까지 공간 확보
        if (!Expand(nextTotalWidth, Height))
            return false;

        // 이전 잠긴 열에 슬롯 생성
        int newColumn = UnlockedWidth;

        for (int y = 0; y < Height; y++)
        {
            if (itemSlots[newColumn, y] == null)
            {
                itemSlots[newColumn, y] =
                    CreateApiarySlot();
            }
        }

        UnlockedWidth = nextUnlockedWidth;

        return true;
    }

    // 기존 슬롯을 보존하면서 전체 격자 확장
    public bool Expand(int newWidth, int newHeight)
    {
        if (newWidth < Width ||
            newHeight < Height)
            return false;

        if (newWidth == Width &&
            newHeight == Height)
            return false;

        ItemSlot[,] expandedSlots =
            new ItemSlot[newWidth, newHeight];

        for (int x = 0; x < newWidth; x++)
        {
            for (int y = 0; y < newHeight; y++)
            {
                if (x < Width && y < Height)
                {
                    expandedSlots[x, y] =
                        itemSlots[x, y];
                }
                else if (x < UnlockedWidth)
                {
                    expandedSlots[x, y] =
                        CreateApiarySlot();
                }
            }
        }

        itemSlots = expandedSlots;

        Width = newWidth;
        Height = newHeight;

        return true;
    }
}
