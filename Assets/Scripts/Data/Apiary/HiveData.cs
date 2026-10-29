public class HiveData
{
    public ItemSlot Slot { get; } = new ItemSlot();

    public int Id { get; private set; }

    public int X { get; private set; }
    public int Y { get; private set; }

    public HiveData(int id, int x, int y)
    {
        Id = id;
        X = x;
        Y = y;
    }

    // 벌통이 배치된 격자 좌표 갱신
    public void SetPosition(int x, int y)
    {
        X = x;
        Y = y;
    }
}