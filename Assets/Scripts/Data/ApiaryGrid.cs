
public class ApiaryGrid
{
    public int Width { get; private set; }
    public int Height { get; private set; }

    // 기존 벌통 ID 관리 (마이그레이션 완료 후 제거 예정)
    private int[,] occupiedBy;

    // 각 격자의 아이템 슬롯
    private ItemSlot[,] itemSlots;

    public ApiaryGrid(int width, int height)
    {
        Width = width;
        Height = height;

        occupiedBy = new int[width, height];
        itemSlots = new ItemSlot[width, height];

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                itemSlots[x, y] = new ItemSlot();
            }
        }
    }

    public bool IsInside(int x, int y)
    {
        return x >= 0 && x < Width &&
               y >= 0 && y < Height;
    }

    // 기존 벌통 또는 새로운 장비가 있는지 함께 확인
    public bool IsEmpty(int x, int y)
    {
        return IsInside(x, y) &&
               occupiedBy[x, y] == 0 &&
               itemSlots[x, y].IsEmpty;
    }

    // 기존 벌통 배치 기능
    public bool TryPlace(int hiveId, int x, int y)
    {
        if (hiveId <= 0 || !IsEmpty(x, y))
            return false;

        occupiedBy[x, y] = hiveId;
        return true;
    }

    // 기존 벌통 ID 조회
    public int GetHiveId(int x, int y)
    {
        if (!IsInside(x, y))
            return 0;

        return occupiedBy[x, y];
    }

    // 기존 벌통 점유 정보를 제거
    public bool TryRemoveLegacyHive(int hiveId, int x, int y)
    {
        if (hiveId <= 0 || !IsInside(x, y))
            return false;

        if (occupiedBy[x, y] != hiveId)
            return false;

        occupiedBy[x, y] = 0;
        return true;
    }

    // 기존 벌통을 새 아이템 슬롯으로 이전
    public bool TryMigrateHive(
        int hiveId,
        int x,
        int y,
        ItemData broodBox)
    {
        if (hiveId <= 0 ||
            broodBox == null ||
            broodBox.Kind != ItemKind.BroodBox ||
            !IsInside(x, y))
            return false;

        if (occupiedBy[x, y] != hiveId)
            return false;

        ItemSlot target = itemSlots[x, y];

        if (!target.IsEmpty)
            return false;

        if (!target.TryInsert(broodBox))
            return false;

        // 슬롯 등록이 성공한 경우에만 기존 점유 정보 제거
        occupiedBy[x, y] = 0;

        return true;
    }

    public ItemSlot GetItemSlot(int x, int y)
    {
        if (!IsInside(x, y))
            return null;

        return itemSlots[x, y];
    }

    // 격자 확장
    public bool Expand(int newWidth, int newHeight)
    {
        if (newWidth < Width || newHeight < Height)
            return false;

        if (newWidth == Width && newHeight == Height)
            return false;

        int[,] expanded = new int[newWidth, newHeight];
        ItemSlot[,] expandedSlots =
            new ItemSlot[newWidth, newHeight];

        for (int x = 0; x < newWidth; x++)
        {
            for (int y = 0; y < newHeight; y++)
            {
                if (x < Width && y < Height)
                {
                    expanded[x, y] = occupiedBy[x, y];
                    expandedSlots[x, y] = itemSlots[x, y];
                }
                else
                {
                    expandedSlots[x, y] = new ItemSlot();
                }
            }
        }

        occupiedBy = expanded;
        itemSlots = expandedSlots;

        Width = newWidth;
        Height = newHeight;

        return true;
    }
}
