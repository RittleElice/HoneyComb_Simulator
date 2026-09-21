using HoneyComb.Simulation.Hive;
using NUnit.Framework;
namespace HoneyComb.Tests {
 public sealed class HiveStructureTests {
  [Test] public void EmptyHiveAndOrderedSupers(){
   var h=new HiveState("h");Assert.That(h.Supers.Count,Is.Zero);Assert.That(h.Base.CanHostBees,Is.True);Assert.That(h.Base.Entrance.ConnectsToOutside,Is.True);
   var a=h.AddSuper();var b=h.AddSuper();Assert.That(h.Supers[0],Is.SameAs(a));Assert.That(h.Supers[1],Is.SameAs(b));
   Assert.That(a.Slots.Count,Is.EqualTo(10));foreach(var slot in a.Slots)Assert.That(slot.CanHostBees,Is.False);
  }
  [Test] public void FrameMovePreservesIdentityAndRejectsOccupiedDestination(){
   var h=new HiveState("h");var a=h.AddSuper();var b=h.AddSuper();h.TryCreateFrame(a.Id,0,out var f);h.TryCreateFrame(b.Id,1,out _);
   Assert.That(h.RemoveSuper(a.Id),Is.False);Assert.That(h.MoveFrame(a.Id,0,b.Id,1),Is.False);Assert.That(a.Slots[0].Frame,Is.SameAs(f));
   Assert.That(h.MoveFrame(a.Id,0,b.Id,9),Is.True);Assert.That(b.Slots[9].Frame,Is.SameAs(f));Assert.That(a.Slots[0].CanHostBees,Is.False);
   Assert.That(h.RemoveSuper(a.Id),Is.True);Assert.That(h.Supers[0],Is.SameAs(b));
  }
  [Test] public void InvalidOperationsDoNotMutateStructure(){
   var h=new HiveState("h");var s=h.AddSuper();h.TryCreateFrame(s.Id,0,out var f);
   Assert.That(h.TryCreateFrame(s.Id,-1,out _),Is.False);Assert.That(h.TryCreateFrame(s.Id,10,out _),Is.False);
   Assert.That(h.InsertFrame(s.Id,1,f),Is.False);Assert.That(h.TryCreateFrame(s.Id,0,out _),Is.False);
   Assert.That(h.MoveFrame(s.Id,0,s.Id,0),Is.False);Assert.That(h.MoveFrame(s.Id,1,s.Id,2),Is.False);
   Assert.That(h.RemoveFrame(s.Id,1),Is.False);Assert.That(h.RemoveSuper("missing"),Is.False);
   Assert.That(s.Slots[0].Frame,Is.SameAs(f));
  }
  [Test] public void RemovalAndReinsertionDoNotDuplicateOwnership(){
   var a=new HiveState("a");var b=new HiveState("b");var sa=a.AddSuper();var sb=b.AddSuper();a.TryCreateFrame(sa.Id,0,out var f);
   Assert.That(b.InsertFrame(sb.Id,0,f),Is.False);a.RemoveFrame(sa.Id,0);Assert.That(sa.Slots[0].CanHostBees,Is.False);
   Assert.That(b.InsertFrame(sb.Id,0,f),Is.True);Assert.That(a.RemoveSuper(sa.Id),Is.True);Assert.That(a.Base,Is.Not.Null);
  }
 }
}
