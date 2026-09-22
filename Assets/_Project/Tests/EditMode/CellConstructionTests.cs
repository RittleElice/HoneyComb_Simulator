using System;
using HoneyComb.Simulation.Hive;
using NUnit.Framework;
namespace HoneyComb.Tests {
 public sealed class CellConstructionTests {
  [Test] public void FoundationAndIncrementalStatistics(){
   var f=new Frame("f");Assert.That(f.CompletedCellCount,Is.Zero);Assert.That(f.AverageConstruction,Is.Zero);
   f.TrySetConstruction(FrameSide.Front,89,39,50);Assert.That(f.AverageConstruction,Is.EqualTo(50.0/7200).Within(1e-10));
   f.TrySetConstruction(FrameSide.Front,89,39,100);Assert.That(f.CompletedCellCount,Is.EqualTo(1));Assert.That(f.Back.AverageConstruction,Is.Zero);
   f.TrySetConstruction(FrameSide.Front,89,39,100);Assert.That(f.CompletedCellCount,Is.EqualTo(1));
  }
  [Test] public void UnbuiltContentAndOccupiedDowngradeAreRejected(){
   var f=new Frame("f");Assert.That(f.TrySetContent(FrameSide.Front,0,0,CellContentType.Nectar),Is.False);
   Assert.Throws<ArgumentException>(()=>new CellState(99,CellContentType.Honey));
   f.TrySetConstruction(FrameSide.Front,0,0,100);Assert.That(f.TrySetContent(FrameSide.Front,0,0,CellContentType.Honey),Is.True);
   Assert.That(f.TrySetConstruction(FrameSide.Front,0,0,20),Is.False);
   Assert.That(f.TrySetAllConstruction(20),Is.False);Assert.That(f.Back.AverageConstruction,Is.Zero);Assert.That(f.CompletedCellCount,Is.EqualTo(1));
  }
  [Test] public void BulkUpdatesAndBadInputs(){
   var f=new Frame("f");Assert.That(f.TrySetAllConstruction(100),Is.True);Assert.That(f.CompletedCellCount,Is.EqualTo(7200));
   f.TrySetAllConstruction(20);Assert.That(f.AverageConstruction,Is.EqualTo(20));Assert.That(f.CompletedCellCount,Is.Zero);
   Assert.That(f.TrySetAllConstruction(101),Is.False);Assert.That(f.TrySetAllConstruction(-1),Is.False);
   Assert.That(f.TrySetConstruction(FrameSide.Front,90,0,100),Is.False);Assert.That(f.AverageConstruction,Is.EqualTo(20));
  }
 }
}
