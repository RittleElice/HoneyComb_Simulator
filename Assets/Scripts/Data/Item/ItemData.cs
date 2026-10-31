public class ItemData : MovableItem
{
    // Dimensionless 0..1. No time stepping or degradation coefficients yet.
    public float Aging { get; private set; }
    public bool AgingAffectsInsulation => Kind == ItemKind.BroodBox || Kind == ItemKind.Super || Kind == ItemKind.Lid;
    public bool AgingAffectsCombDecay => Kind == ItemKind.Frame;
    public bool TrySetAging(float value)
    {
        if (float.IsNaN(value) || value < 0 || value > 1) return false;
        Aging = value; return true;
    }

    public int Id { get; }
    public override string Name { get; }
    public object Content { get; }
    public ItemKind Kind { get; }

    // ItemKind에 따라 상위 카테고리 자동 결정
    public SlotCategory Category =>
        ItemClassification.GetCategory(Kind);

    // 단상·계상 내부의 범용 장비 슬롯 10개
    public InventoryData InteriorSlots { get; }

    // 기존 코드와의 호환성
    public InventoryData Frames => InteriorSlots;

    // 단상·계상 상단의 범용 장비 슬롯 1개
    public ItemSlot TopSlot { get; }

    private HiveSpatialGrid spatialGrid;
    private EquipmentSpatialGrid equipmentGrid;
    // The equipment owns its geometry even while detached or carried in a hand.
    public EquipmentSpatialGrid EquipmentGrid => !EquipmentSpatialGrid.Supports(Kind) ? null :
        equipmentGrid ?? (equipmentGrid = new EquipmentSpatialGrid(this));
    // Compatibility API: composed space view, distinct from owned EquipmentGrid.
    public HiveSpatialGrid SpatialGrid => InteriorSlots == null && Kind != ItemKind.Lid ? null :
        spatialGrid ?? (spatialGrid = new HiveSpatialGrid(this));

    public ItemData(int id, string name, object content = null)
        : this(id, name, ItemKind.Generic, content)
    {
    }

    public ItemData(
        int id,
        string name,
        ItemKind kind,
        object content = null)
    {
        Id = id;
        Name = name;
        Kind = kind;
        Content = content;

        if (kind == ItemKind.BroodBox ||
            kind == ItemKind.Super)
        {
            InteriorSlots = new InventoryData(
                10,
                HiveInteriorRules.CanInsert);

            TopSlot = new ItemSlot(
                HiveTopAttachmentRules.CanInsert);
        }
    }

    public T GetContent<T>() where T : class
    {
        return Content as T;
    }
}
