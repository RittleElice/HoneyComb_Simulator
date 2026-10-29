// Runtime ownership prevents one object occupying multiple slots or assemblies.

public abstract class MovableItem
{
    // 하나의 장비가 여러 슬롯이나 조립체에
    // 동시에 존재하지 않도록 소유권 관리
    internal object Owner { get; set; }

    public abstract string Name { get; }
}

public enum ItemKind
{
    Generic,
    BroodBox,
    Frame,
    Lid,
    Super,
    QueenExcluder
}