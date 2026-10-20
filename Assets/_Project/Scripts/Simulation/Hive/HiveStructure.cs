using System;
using System.Collections.Generic;
namespace HoneyComb.Simulation.Hive
{
    public sealed class HiveBase
    {
        public bool CanHostBees => true;
        public HiveEntrance Entrance { get; } = new HiveEntrance();
    }
    public sealed class HiveEntrance { public bool ConnectsToOutside => true; }
    public sealed class FrameSlot
    {
        public int Index { get; }
        public Frame Frame { get; internal set; }
        public bool CanHostBees => Frame != null;
        internal FrameSlot(int index) { Index=index; }
    }
    public sealed class Super
    {
        // Physical format, not a configurable balance value.
        public const int SlotCount=10;
        public string Id { get; }
        public IReadOnlyList<FrameSlot> Slots { get; }
        public bool IsEmpty
        {
            get { for(int i=0;i<Slots.Count;i++) if(Slots[i].Frame!=null) return false; return true; }
        }
        internal Super(string id)
        {
            Id=id;var slots=new FrameSlot[SlotCount];
            for(int i=0;i<slots.Length;i++) slots[i]=new FrameSlot(i);
            Slots=Array.AsReadOnly(slots);
        }
        public FrameSlot GetSlot(int index) => index>=0 && index<SlotCount ? Slots[index] : null;
    }
}

