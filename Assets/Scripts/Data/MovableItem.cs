// Runtime ownership prevents one object occupying multiple slots or assemblies.
public abstract class MovableItem
{
    internal object Owner { get; set; }
    public abstract string Name { get; }
}
public enum ItemKind { Generic, BroodBox, Super, Lid, Frame }
