
using System;
using System.Collections.Generic;

public class ApiaryData
{
    public string Name { get; private set; }

    public ApiaryGrid Grid { get; private set; }

    // 벌통의 생물학적 데이터 목록
    public List<HiveData> Hives { get; private set; }

    public ApiaryData(string name, int width, int height)
    {
        Name = name;
        Grid = new ApiaryGrid(width, height);
        Hives = new List<HiveData>();
    }

    // 벌통 데이터와 단상 장비를 함께 등록
    public bool AddHive(HiveData hive, ItemData broodBox)
    {
        if (hive == null || broodBox == null)
            return false;

        if (broodBox.Kind != ItemKind.BroodBox)
            return false;

        if (!ReferenceEquals(
                broodBox.GetContent<HiveData>(), hive))
            return false;

        foreach (HiveData existing in Hives)
        {
            if (existing.Id == hive.Id)
                return false;
        }

        ItemSlot target = Grid.GetItemSlot(hive.X, hive.Y);

        if (target == null || !target.IsEmpty)
            return false;

        // 슬롯 등록에 성공한 경우에만 목록에 추가
        if (!target.TryInsert(broodBox))
            return false;

        Hives.Add(hive);

        return true;
    }

    public ItemSlot GetItemSlot(int x, int y)
    {
        return Grid.GetItemSlot(x, y);
    }

    // 다른 슬롯의 아이템을 빈 양봉장 격자로 이동
    public bool TryPlaceItem(int x, int y, ItemSlot source)
    {
        if (source == null)
            return false;

        ItemSlot target = Grid.GetItemSlot(x, y);

        if (target == null || !target.IsEmpty)
            return false;

        return source.TryMoveTo(target);
    }

    // 실제 격자에서 벌통의 현재 위치 조회
    public bool TryGetHivePosition(
        HiveData hive,
        out int x,
        out int y)
    {
        x = -1;
        y = -1;

        if (hive == null)
            return false;

        for (int gridX = 0; gridX < Grid.Width; gridX++)
        {
            for (int gridY = 0; gridY < Grid.Height; gridY++)
            {
                ItemSlot slot = Grid.GetItemSlot(gridX, gridY);

                if (slot == null || slot.IsEmpty)
                    continue;

                if (slot.Value is ItemData item)
                {
                    if (IsHivePart(item, hive))
                    {
                        x = gridX;
                        y = gridY;
                        return true;
                    }
                }
                else if (slot.Value is ItemAssembly assembly)
                {
                    foreach (ItemData part in assembly.Parts)
                    {
                        if (IsHivePart(part, hive))
                        {
                            x = gridX;
                            y = gridY;
                            return true;
                        }
                    }
                }
            }
        }

        return false;
    }

    // 해당 단상이 지정한 벌통 데이터를 보유하는지 확인
    private static bool IsHivePart(ItemData item, HiveData hive)
    {
        return item != null &&
               item.Kind == ItemKind.BroodBox &&
               ReferenceEquals(
                   item.GetContent<HiveData>(),
                   hive);
    }
}
