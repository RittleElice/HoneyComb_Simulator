using System.Collections.Generic;

public class ApiaryData
{
    public string Name { get; private set; }

    public List<HiveData> Hives { get; private set; }

    public ApiaryData(string name)
    {
        Name = name;
        Hives = new List<HiveData>();
    }

    public void AddHive(HiveData hive)
    {
        Hives.Add(hive);
    }
}