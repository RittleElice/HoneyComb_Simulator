using HoneyComb.Simulation.Hive;
using NUnit.Framework;
namespace HoneyComb.Tests {
 public sealed class FrameCellTests {
  [Test] public void FacesAndCoordinatesAreIndependent(){
   var f=new Frame("f");Assert.That(f.TotalCellCount,Is.EqualTo(7200));
   Assert.That(f.TrySetCell(FrameSide.Front,89,39,new CellState(CellContentType.Honey)),Is.True);
   Assert.That(f.GetCell(FrameSide.Front,89,39).Value.Content,Is.EqualTo(CellContentType.Honey));
   Assert.That(f.GetCell(FrameSide.Back,89,39).Value.Content,Is.EqualTo(CellContentType.Empty));
   Assert.That(f.GetCell(FrameSide.Front,88,39).Value.Content,Is.EqualTo(CellContentType.Empty));
   var copy=f.GetCell(FrameSide.Front,89,39).Value;copy=new CellState(CellContentType.Pollen);
   Assert.That(f.GetCell(FrameSide.Front,89,39).Value.Content,Is.EqualTo(CellContentType.Honey));
  }
  [Test] public void StateSurvivesFrameMovementAndReinsertion(){
   var h=new HiveState("h");var s=h.AddSuper();h.TryCreateFrame(s.Id,0,out var f);
   f.TrySetCell(FrameSide.Back,3,2,new CellState(CellContentType.Pollen));
   Assert.That(h.MoveFrame(s.Id,0,s.Id,9),Is.True);
   Assert.That(s.Slots[9].Frame.GetCell(FrameSide.Back,3,2).Value.Content,Is.EqualTo(CellContentType.Pollen));
   h.RemoveFrame(s.Id,9);h.InsertFrame(s.Id,0,f);
   Assert.That(s.Slots[0].Frame.GetCell(FrameSide.Back,3,2).Value.Content,Is.EqualTo(CellContentType.Pollen));
  }
  [Test] public void InvalidCoordinatesDoNotWrite(){
   var f=new Frame("f");var honey=new CellState(CellContentType.Honey);
   Assert.That(f.TrySetCell(FrameSide.Front,90,0,honey),Is.False);Assert.That(f.TrySetCell(FrameSide.Back,-1,0,honey),Is.False);
   Assert.That(f.TrySetCell((FrameSide)5,0,0,honey),Is.False);Assert.That(f.TryGetCell(FrameSide.Back,0,40,out _),Is.False);
   Assert.That(f.GetCell(FrameSide.Front,0,0).Value.Content,Is.EqualTo(CellContentType.Empty));
  }
 }
}
