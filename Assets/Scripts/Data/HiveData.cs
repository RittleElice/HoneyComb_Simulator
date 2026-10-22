
public class HiveData
{
    public int Id { get; private set; }

    public int X { get; private set; }
    public int Y { get; private set; }

    public HiveData(int id, int x, int y)
    {
        Id = id;
        X = x;
        Y = y;
    }
}
