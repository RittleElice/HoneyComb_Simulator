public class GameWorld
{
    public int Year { get; private set; }
    public int Month { get; private set; }
    public int Day { get; private set; }
    public int Hour { get; private set; }
    public int Minute { get; private set; }
    public ApiaryData Apiary { get; private set; }
    
    public GameWorld()
    {
        Year = 1;
        Month = 2;
        Day = 1;
        Hour = 0;
        Minute = 0;
        
        Apiary = new ApiaryData("양봉장", 8, 5);
        Apiary.AddHive(new HiveData(1, 0, 0));

    }
}