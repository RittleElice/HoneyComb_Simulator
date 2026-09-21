using HoneyComb.Simulation.Hive;
using NUnit.Framework;
namespace HoneyComb.Tests {
 public sealed class FrameTransferTests {
  [Test] public void TransferPreservesFrameAndBothFaces(){
   var a=new HiveState("a");var b=new HiveState("b",3,2);var sa=a.AddSuper();var sb=b.AddSuper();a.TryCreateFrame(sa.Id,0,out var f);
   f.TrySetCell(FrameSide.Front,89,39,new CellState(CellContentType.Honey));f.TrySetCell(FrameSide.Back,10,3,new CellState(CellContentType.Pollen));
   Assert.That(a.MoveFrameTo(sa.Id,0,b,sb.Id,9),Is.True);Assert.That(sa.Slots[0].Frame,Is.Null);Assert.That(sb.Slots[9].Frame,Is.SameAs(f));
   Assert.That(f.TotalCellCount,Is.EqualTo(7200));Assert.That(f.GetCell(FrameSide.Front,89,39).Value.Content,Is.EqualTo(CellContentType.Honey));
   Assert.That(f.GetCell(FrameSide.Back,10,3).Value.Content,Is.EqualTo(CellContentType.Pollen));
   Assert.That(b.MoveFrameTo(sb.Id,9,a,sa.Id,1),Is.True);
  }
  [Test] public void FailedTransfersLeaveSourceUntouched(){
   var a=new HiveState("a");var b=new HiveState("b");var sa=a.AddSuper();var sb=b.AddSuper();a.TryCreateFrame(sa.Id,0,out var f);b.TryCreateFrame(sb.Id,0,out _);
   Assert.That(a.MoveFrameTo(sa.Id,0,b,sb.Id,0),Is.False);Assert.That(a.MoveFrameTo(sa.Id,0,b,sb.Id,10),Is.False);
   Assert.That(a.MoveFrameTo(sa.Id,0,null,sb.Id,1),Is.False);Assert.That(a.MoveFrameTo(sa.Id,0,b,"missing",0),Is.False);
   b.InsertFrame(sb.Id,2,new Frame(f.Id));Assert.That(a.MoveFrameTo(sa.Id,0,b,sb.Id,3),Is.False);
   Assert.That(sa.Slots[0].Frame,Is.SameAs(f));Assert.That(b.InsertFrame(sb.Id,4,f),Is.False);
  }
 }
}
