using System;
[Serializable] public sealed class CycleCommand
{
    public CycleTask task;
    public CyclePoint target;
    public int group;
    public double need;
    public bool localOnly;
}
