using System;

public class GameWorld
{
    public int Year { get; private set; }
    public int Month { get; private set; }
    public int Day { get; private set; }
    public int Hour { get; private set; }
    public int Minute { get; private set; }

    public ApiaryData Apiary { get; private set; }

    public InventoryData Inventory { get; }
    public HandData Hand { get; }

    public GameWorld()
    {
        // 게임 시간 초기화
        Year = 1;
        Month = 2;
        Day = 1;
        Hour = 0;
        Minute = 0;

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
        Inventory = new InventoryData(8);
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
        Inventory.TryAdd(
            new ItemData(104, "소비장 1", ItemKind.Frame)
        );

        Inventory.TryAdd(
            new ItemData(105, "소비장 2", ItemKind.Frame)
        );
    }
}