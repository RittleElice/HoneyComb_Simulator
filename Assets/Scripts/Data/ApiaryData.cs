using System.Collections.Generic;

public class ApiaryData
{
    public string Name { get; private set; }

    public ApiaryGrid Grid { get; private set; }

    public List<HiveData> Hives { get; private set; }

    public ApiaryData(string name, int width, int height)
    {
        Name = name;
        Grid = new ApiaryGrid(width, height);
        Hives = new List<HiveData>();
    }

    public bool AddHive(HiveData hive)
    {
        if (hive == null)
            return false;

        foreach (HiveData existing in Hives)
        {
            if (existing.Id == hive.Id)
                return false;
        }

        if (!Grid.TryPlace(hive.Id, hive.X, hive.Y))
            return false;

        Hives.Add(hive);
        return true;
    }
}