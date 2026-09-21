using System;
using HoneyComb.Simulation.Apiary;
using HoneyComb.Simulation.Hive;
using NUnit.Framework;
namespace HoneyComb.Tests {
 public sealed class ApiaryStateTests {
  [Test] public void GridCoordinatesAndInitialPlacement(){
   var a=new ApiaryState("a","Test",5,4,6);
   Assert.That(a.CellCount,Is.EqualTo(20)); Assert.That(a.HiveCount,Is.EqualTo(6));
   Assert.That(a.GetHive(0,1),Is.Not.Null); Assert.That(a.GetHive(1,1),Is.Null);
   Assert.That(a.GetCell(4,3).X,Is.EqualTo(4));Assert.That(a.GetCell(4,3).Y,Is.EqualTo(3));
   Assert.That(a.GetCell(-1,0),Is.Null);Assert.That(a.GetCell(5,0),Is.Null);Assert.That(a.GetCell(0,4),Is.Null);
  }
  [Test] public void MoveIsAtomicAndPreservesIdentity(){
   var a=new ApiaryState("a","Test",3,2,2);var h=a.GetHive(0,0);
   Assert.That(a.MoveHive(0,0,1,0),Is.False);Assert.That(a.GetHive(0,0),Is.SameAs(h));
   Assert.That(a.MoveHive(0,0,2,1),Is.True);Assert.That(a.GetHive(2,1),Is.SameAs(h));
   Assert.That(a.GetHive(0,0),Is.Null);Assert.That(a.HiveCount,Is.EqualTo(2));
   Assert.That(a.MoveHive(2,1,2,1),Is.False);Assert.That(a.MoveHive(2,1,9,0),Is.False);
  }
  [Test] public void InvalidPlacementAndRemovalDoNotChangeGrid(){
   var a=new ApiaryState("a","Test",2,2,1);var h=a.GetHive(0,0);
   Assert.That(a.TryCreateHive(0,0,out _),Is.False);Assert.That(a.TryCreateHive(8,15,out _),Is.False);
   Assert.That(a.PlaceHive(h,1,1),Is.False);Assert.That(a.PlaceHive(new HiveState(h.Id),1,1),Is.False);
   Assert.That(a.RemoveHive(1,1),Is.False);Assert.That(a.RemoveHive(-1,0),Is.False);
   Assert.That(a.MoveHive(1,1,1,0),Is.False);Assert.That(a.HiveCount,Is.EqualTo(1));
   Assert.That(a.RemoveHive(0,0),Is.True);Assert.That(a.HiveCount,Is.Zero);
   a.TryCreateHive(0,0,out var next);Assert.That(next.Id,Is.Not.EqualTo(h.Id));
  }
  [Test] public void HiveCannotOccupyTwoApiaries(){
   var a=new ApiaryState("a","A",1,1,1);var b=new ApiaryState("b","B",1,1,0);var h=a.GetHive(0,0);
   Assert.That(b.PlaceHive(h,0,0),Is.False);a.RemoveHive(0,0);
   Assert.That(b.PlaceHive(h,0,0),Is.True);
  }
  [Test] public void InvalidConfigurationIsRejected(){
   Assert.Throws<ArgumentOutOfRangeException>(()=>new ApiaryState("a","A",0,1,0));
   Assert.Throws<ArgumentOutOfRangeException>(()=>new ApiaryState("a","A",1,-1,0));
   Assert.Throws<ArgumentOutOfRangeException>(()=>new ApiaryState("a","A",int.MaxValue,int.MaxValue,0));
   Assert.Throws<ArgumentOutOfRangeException>(()=>new ApiaryState("a","A",1,1,2));
  }
 }
}
