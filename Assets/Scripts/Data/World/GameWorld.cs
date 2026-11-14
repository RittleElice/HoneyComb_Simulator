using System;

public class GameWorld
{
    public BeeSurfaceLocations BeeLocations { get; } = new BeeSurfaceLocations();
    public ThermalWorld Thermal { get; private set; }
    public void PrepareThermal(ThermalMaterials materials)
    { if (Thermal == null) Thermal = new ThermalWorld(this, materials); Thermal.Refresh(); }

    public WeatherSimulator Weather { get; private set; }
    public void AttachWeather(WeatherSimulator weather)
    {
        if (Weather != null || weather == null || weather.Minute != CurrentGameMinute)
            throw new InvalidOperationException("Weather must match the world clock and may only be attached once.");
        weather.BindApiaryArea(() => Apiary.Grid.UnlockedArea);
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
    public InventoryData Warehouse { get; } = new InventoryData(100);
    public int LastShopId { get; internal set; }

    public bool TryAcquire(ItemKind kind, out string message)
    {
        string name = ShopName(kind);
        if (name == null) { message = "등록되지 않은 장비입니다."; return false; }
        ItemSlot target = FreeSlot(Inventory);
        if (target == null) { message = "소지품이 가득 찼습니다."; return false; }
        int highest = LastShopId;
        foreach (ItemData item in ThermalWorld.Items(this)) highest = Math.Max(highest, item.Id);
        if (highest == int.MaxValue) { message = "사용 가능한 장비 번호가 없습니다."; return false; }
        var created = new ItemData(highest + 1, name, kind, kind == ItemKind.Frame ? new FrameData() : null);
        LastShopId = created.Id;
        bool added = target.TryInsert(created);
        message = added ? name + " 장비를 소지품에 넣었습니다." : "빈 슬롯을 사용할 수 없습니다.";
        return added;
    }

    public static string ShopName(ItemKind kind)
    {
        switch (kind)
        {
            case ItemKind.BroodBox: return "단상";
            case ItemKind.Super: return "계상";
            case ItemKind.Lid: return "뚜껑";
            case ItemKind.Frame: return "소비장";
            case ItemKind.DividerBoard: return "격리판";
            case ItemKind.QueenExcluder: return "격왕판";
            case ItemKind.InsulationBoard: return "단열판";
            case ItemKind.HeatingPanel: return "벌통 가온판";
            default: return null;
        }
    }

    private static ItemSlot FreeSlot(InventoryData bag)
    {
        for (int i = 0; i < bag.Capacity; i++)
            if (bag.GetSlot(i).IsEmpty) return bag.GetSlot(i);
        return null;
    }

    public InventoryData Trash { get; } = new InventoryData(12);

    public bool TryEmptyTrash(out string message)
    {
        int count = 0;
        for (int i = 0; i < Trash.Capacity; i++)
        {
            var value = Trash.GetSlot(i).Value;
            if (value == null) continue;
            if (!(value is ItemData item) || item.Content is HiveData || item.TopSlot?.Value != null ||
                HasContents(item.InteriorSlots))
            {
                message = "조립체나 내용물이 있는 장비, 등록된 벌통을 먼저 꺼내세요.";
                return false;
            }
            if (BeeLocations.HasEquipment(((ItemData)value).Id)) { message = "벌이 붙어 있는 장비는 삭제할 수 없습니다."; return false; }
            count++;
        }
        for (int i = 0; i < Trash.Capacity; i++)
        {
            var item = Trash.GetSlot(i).Item;
            if (item == null) continue;
            LastShopId = Math.Max(LastShopId, item.Id);
            Trash.GetSlot(i).Take();
        }
        message = count == 0 ? "쓰레기통이 비어 있습니다." : count + "개 장비를 삭제했습니다.";
        return count > 0;
    }

    private static bool HasContents(InventoryData slots)
    {
        if (slots == null) return false;
        for (int i = 0; i < slots.Capacity; i++)
            if (!slots.GetSlot(i).IsEmpty) return true;
        return false;
    }

    public HandData Hand { get; }

    internal GameWorld(GameClock clock, ApiaryData apiary, InventoryData inventory, HandData hand)
    {
        Clock = clock; Apiary = apiary; Inventory = inventory; Hand = hand;
    }

    public GameWorld(WeatherSimulator weather = null)
    {

        // 양봉장 초기화
        Apiary = new ApiaryData("양봉장", 8, 5);
        if (weather != null) AttachWeather(weather);

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
        Inventory.TryAdd(new ItemData(100, "단상", ItemKind.BroodBox));
        // 테스트용 계상 2개
        Inventory.TryAdd(new ItemData(101, "계상 1", ItemKind.Super));
        Inventory.TryAdd(new ItemData(102, "계상 2", ItemKind.Super));
        // 테스트용 뚜껑
        Inventory.TryAdd(new ItemData(103, "뚜껑", ItemKind.Lid));
        Inventory.TryAdd(new ItemData(114, "뚜껑", ItemKind.Lid));
        // 테스트용 소비장 2개
        Inventory.TryAdd(new ItemData(104, "소비장 1", ItemKind.Frame, new FrameData()));
        Inventory.TryAdd(new ItemData(105, "소비장 2", ItemKind.Frame, new FrameData()));
        // 추가 테스트용 계상 3개
        Inventory.TryAdd(new ItemData(106, "계상 3", ItemKind.Super));
        //Inventory.TryAdd(new ItemData(107, "계상 4", ItemKind.Super));
        //Inventory.TryAdd(new ItemData(108, "계상 5", ItemKind.Super));
        //Inventory.TryAdd(new ItemData(109, "격왕판", ItemKind.QueenExcluder));
        Inventory.TryAdd(new ItemData(110, "단열판", ItemKind.InsulationBoard));
        Inventory.TryAdd(new ItemData(113, "단열판", ItemKind.InsulationBoard));
        Inventory.TryAdd(new ItemData(111, "벌통 가온판", ItemKind.HeatingPanel));
        Inventory.TryAdd(new ItemData(112, "벌통 가온판", ItemKind.HeatingPanel));
        


    }
}
