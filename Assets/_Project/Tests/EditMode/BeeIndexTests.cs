using System.Collections.Generic;
using HoneyComb.Simulation.Apiary;
using HoneyComb.Simulation.Bees;
using HoneyComb.Simulation.Hive;
using NUnit.Framework;
namespace HoneyComb.Tests {
 public sealed class BeeIndexTests {
  [Test] public void IndexesSeparateFramesAndSidesAndReturnSameBee(){
   var apiary=new ApiaryState("a","A",1,1,1);var h=apiary.GetHive(0,0);var s=h.AddSuper();h.TryCreateFrame(s.Id,0,out var f);h.TryCreateFrame(s.Id,1,out var other);
   var p=new BeePopulation(apiary);var adult=new BeeSpawnSettings(BeeCaste.Worker,BeeDevelopment.Adult,100);
   p.TryAddBee(h,f,FrameSide.Front,0,0,adult,0,out var first,out _);var view=p.GetBeesOnSurface(f.Id,FrameSide.Front);
   p.TryAddBee(h,f,FrameSide.Back,0,0,adult,0,out var back,out _);p.TryAddBee(h,other,FrameSide.Front,0,0,adult,0,out _,out _);
   Assert.That(p.TryGetBee(first.Id,out var found),Is.True);Assert.That(found.Id,Is.EqualTo(first.Id));
   Assert.That(p.GetBeesOnFrame(f.Id).Count,Is.EqualTo(2));Assert.That(view.Count,Is.EqualTo(1));Assert.That(p.GetBeesOnSurface(f.Id,FrameSide.Back)[0].Id,Is.EqualTo(back.Id));
   p.TryAddBee(h,f,FrameSide.Front,0,0,adult,0,out _,out _);Assert.That(view.Count,Is.EqualTo(2));
   Assert.That(view,Is.Not.InstanceOf<IList<BeeState>>());
  }
  [Test] public void FailedPlacementDoesNotEnterIndexesAndCellLookupResolvesBrood(){
   var a=new ApiaryState("a","A",1,1,1);var h=a.GetHive(0,0);var s=h.AddSuper();h.TryCreateFrame(s.Id,0,out var f);var p=new BeePopulation(a);
   var egg=new BeeSpawnSettings(BeeCaste.Worker,BeeDevelopment.Egg,100);
   Assert.That(p.TryAddBee(h,f,FrameSide.Front,0,0,egg,0,out _,out _),Is.False);Assert.That(p.GetBeesOnFrame(f.Id).Count,Is.Zero);
   f.TrySetConstruction(FrameSide.Front,0,0,100);p.TryAddBee(h,f,FrameSide.Front,0,0,egg,0,out var bee,out _);
   Assert.That(p.GetBeeInCell(f,FrameSide.Front,0,0).Value.Id,Is.EqualTo(bee.Id));Assert.That(p.GetBeesOnSurface(f.Id,FrameSide.Front)[0].Id,Is.EqualTo(bee.Id));
   p.TryAddBee(h,f,FrameSide.Front,0,0,egg,0,out _,out _);Assert.That(p.GetBeesOnFrame(f.Id).Count,Is.EqualTo(1));
  }
  [Test] public void TransferKeepsLocationIndexAndChangesResolvedHive(){
   var a=new ApiaryState("a","A",2,1,2);var h=a.GetHive(0,0);var to=a.GetHive(1,0);var s=h.AddSuper();var t=to.AddSuper();h.TryCreateFrame(s.Id,0,out var f);
   var p=new BeePopulation(a);p.TryAddBee(h,f,FrameSide.Back,0,0,new BeeSpawnSettings(BeeCaste.Worker,BeeDevelopment.Adult,100),0,out var bee,out _);
   var view=p.GetBeesOnSurface(f.Id,FrameSide.Back);h.MoveFrameTo(s.Id,0,to,t.Id,0);
   Assert.That(view[0].Id,Is.EqualTo(bee.Id));Assert.That(p.FindPhysicalHive(f.Id),Is.SameAs(to));Assert.That(p.GetBeesOnFrame(f.Id).Count,Is.EqualTo(1));
  }
  [Test] public void MissingLocationsAreSafe(){
   var p=new BeePopulation(new ApiaryState("a","A",1,1,0));
   Assert.That(p.TryGetBee(-1,out _),Is.False);Assert.That(p.GetBeesOnFrame(null).Count,Is.Zero);
   Assert.That(p.GetBeesOnSurface("missing",(FrameSide)8).Count,Is.Zero);Assert.That(p.GetBeeInCell(null,FrameSide.Front,0,0),Is.Null);
  }
 }
}
