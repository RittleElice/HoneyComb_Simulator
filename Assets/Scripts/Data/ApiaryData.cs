using System.Collections.Generic;

public class ApiaryData
{
    public string Name { get; private set; }

    public ApiaryGrid Grid { get; private set; }

    // 기존 벌통 목록 (추후 제거 예정)
    public List<HiveData> Hives { get; private set; }

    public ApiaryData(string name, int width, int height)
    {
        Name = name;
        Grid = new ApiaryGrid(width, height);
        Hives = new List<HiveData>();
    }

    // 기존 벌통 배치 기능 유지
    public bool AddHive(HiveData hive)
    {
        if (hive == null)
            return false;

        foreach (HiveData existing in Hives)
        {
            if (existing.Id == hive.Id)
                return false;
        }

        // 새 아이템이 있는 격자에는 기존 벌통 배치 금지
        ItemSlot target = Grid.GetItemSlot(hive.X, hive.Y);

        if (target == null || !target.IsEmpty)
            return false;

        if (!Grid.TryPlace(hive.Id, hive.X, hive.Y))
            return false;

        Hives.Add(hive);
        return true;
    }

    // 특정 격자의 아이템 슬롯 가져오기
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

        if (target == null)
            return false;

        // 기존 HiveData가 차지한 격자에는 배치 금지
        if (!Grid.IsEmpty(x, y))
            return false;

        // 이미 아이템이 있는 격자에는 배치 금지
        if (!target.IsEmpty)
            return false;

        return source.TryMoveTo(target);
    }
}