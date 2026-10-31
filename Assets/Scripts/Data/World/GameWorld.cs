using System;

public class GameWorld
{
    public WeatherSimulator Weather { get; private set; }
    public void AttachWeather(WeatherSimulator weather)
    {
        if (Weather != null || weather == null || weather.Minute != CurrentGameMinute)
            throw new InvalidOperationException("Weather must match the world clock and may only be attached once.");
        Weather = weather;
    }

    public GameClock Clock { get; } = new GameClock();
    public long CurrentGameMinute => Clock.CurrentGameMinute;
    public long Year => Clock.Year;
    public int Month => Clock.Month;
    public int Day => Clock.Day;
    public int Hour => Clock.Hour;
    public int Minute => Clock.Minute;

    public ApiaryData Apiary { get; private set; }

    public InventoryData Inventory { get; }
    public HandData Hand { get; }

    internal GameWorld(GameClock clock, ApiaryData apiary, InventoryData inventory, HandData hand)
    {
        Clock = clock; Apiary = apiary; Inventory = inventory; Hand = hand;
    }

    public GameWorld(WeatherSimulator weather = null)
    {
        if (weather != null) AttachWeather(weather);
        // 양봉장 초기화
        Apiary = new ApiaryData("양봉장", 8, 5);

        // 기존 벌통의 생물학적 데이터
        HiveData initialHive = new HiveData(1, 0, 0);

        // 기존 벌통 데이터를 담은 단상 장비
        ItemData initialBroodBox = new ItemData(
            1,
            "기존 단상",
            ItemKind.BroodBox,
            initialHive
        );

        // ItemSlot에 직접 등록
        if (!Apiary.AddHive(initialHive, initialBroodBox))
        {
            throw new InvalidOperationException(
                "초기 벌통 등록에 실패했습니다.");
        }

        // 가방과 손 초기화
        Inventory = new InventoryData(12);
        Hand = new HandData();

        // 테스트용 단상
        Inventory.TryAdd(
            new ItemData(100, "단상", ItemKind.BroodBox)
        );

        // 테스트용 계상 2개
        Inventory.TryAdd(
            new ItemData(101, "계상 1", ItemKind.Super)
        );

        Inventory.TryAdd(
            new ItemData(102, "계상 2", ItemKind.Super)
        );

        // 테스트용 뚜껑
        Inventory.TryAdd(
            new ItemData(103, "뚜껑", ItemKind.Lid)
        );

        // 테스트용 소비장 2개
        Inventory.TryAdd(new ItemData(104, "소비장 1", ItemKind.Frame, new FrameData())
        );

        Inventory.TryAdd(
            new ItemData(105, "소비장 2", ItemKind.Frame, new FrameData())
        );

        
        // 추가 테스트용 계상 3개
        Inventory.TryAdd(
            new ItemData(106, "계상 3", ItemKind.Super)
        );

        Inventory.TryAdd(
            new ItemData(107, "계상 4", ItemKind.Super)
        );

        Inventory.TryAdd(
            new ItemData(108, "계상 5", ItemKind.Super)
        );
        
        Inventory.TryAdd(
            new ItemData(109, "격왕판", ItemKind.QueenExcluder
            )
        );


    }
}
