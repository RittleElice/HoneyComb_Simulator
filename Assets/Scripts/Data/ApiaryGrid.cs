
using System;

public class ApiaryGrid
{
    public int Width { get; private set; }
    public int Height { get; private set; }

    // 격자 점유 정보는 ItemSlot에서만 관리
    private ItemSlot[,] itemSlots;

    public ApiaryGrid(int width, int height)
    {
        if (width <= 0 || height <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(width),
                "격자 크기는 1 이상이어야 합니다.");
        }

        Width = width;
        Height = height;

        itemSlots = new ItemSlot[width, height];

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                // 양봉장 배치 제한 + 장비 조립 허용
                itemSlots[x, y] = CreateApiarySlot();
            }
        }
    }

    // 모든 양봉장 격자에 동일한 규칙 적용
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

    // 슬롯이 비어 있는지 확인
    public bool IsEmpty(int x, int y)
    {
        ItemSlot slot = GetItemSlot(x, y);

        return slot != null && slot.IsEmpty;
    }

    // 격자 슬롯 조회
    public ItemSlot GetItemSlot(int x, int y)
    {
        if (!IsInside(x, y))
            return null;

        return itemSlots[x, y];
    }

    // 기존 코드 호환용 벌통 ID 조회
    public int GetHiveId(int x, int y)
    {
        ItemSlot slot = GetItemSlot(x, y);

        if (slot == null || slot.IsEmpty)
            return 0;

        if (slot.Value is ItemData item)
            return GetHiveIdFromPart(item);

        if (slot.Value is ItemAssembly assembly)
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

    // 기존 슬롯 객체를 보존하면서 격자 확장
    public bool Expand(int newWidth, int newHeight)
    {
        if (newWidth < Width || newHeight < Height)
            return false;

        if (newWidth == Width && newHeight == Height)
            return false;

        ItemSlot[,] expandedSlots =
            new ItemSlot[newWidth, newHeight];

        for (int x = 0; x < newWidth; x++)
        {
            for (int y = 0; y < newHeight; y++)
            {
                if (x < Width && y < Height)
                {
                    // 기존 장비 및 소유 관계 유지
                    expandedSlots[x, y] = itemSlots[x, y];
                }
                else
                {
                    // 새 격자에도 동일한 배치·조립 규칙 적용
                    expandedSlots[x, y] = CreateApiarySlot();
                }
            }
        }

        itemSlots = expandedSlots;

        Width = newWidth;
        Height = newHeight;

        return true;
    }
}
