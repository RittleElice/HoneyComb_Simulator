using System;
using HoneyComb.Simulation.Apiary;
using NUnit.Framework;
namespace HoneyComb.Tests {
 public sealed class ApiaryStateTests {
  [Test] public void CapacityAndRemovalAreEnforced(){
   var apiary=new ApiaryState("a","Test",1,0);
   Assert.That(apiary.TryAddHive(out var hive),Is.True);
   Assert.That(apiary.TryAddHive(out _),Is.False);
   Assert.That(apiary.RemoveHive("missing"),Is.False);
   Assert.That(apiary.HiveCount,Is.EqualTo(1));
   Assert.That(apiary.RemoveHive(hive.Id),Is.True);
   Assert.That(apiary.RemoveHive(hive.Id),Is.False);
   Assert.That(apiary.HiveCount,Is.Zero);
  }
  [Test] public void IdsRemainUniqueAfterRemoval(){
   var apiary=new ApiaryState("a","Test",2,1); var old=apiary.Hives[0].Id;
   apiary.RemoveHive(old); apiary.TryAddHive(out var replacement);
   Assert.That(replacement.Id,Is.Not.EqualTo(old));
   var other=new ApiaryState("b","Other",1,1);
   Assert.That(other.Hives[0].Id,Is.Not.EqualTo(replacement.Id));
  }
  [Test] public void InvalidInitialConditionsAreRejected(){
   Assert.Throws<ArgumentException>(()=>new ApiaryState("","Test",1,0));
   Assert.Throws<ArgumentOutOfRangeException>(()=>new ApiaryState("a","Test",1,2));
   Assert.Throws<ArgumentOutOfRangeException>(()=>new ApiaryState("a","Test",-1,0));
   Assert.Throws<ArgumentOutOfRangeException>(()=>new ApiaryState("a","Test",1,-1));
  }
  [Test] public void ZeroCapacityIsValidButCannotAdd(){
   var apiary=new ApiaryState("a","Test",0,0);
   Assert.That(apiary.TryAddHive(out _),Is.False);
  }
 }
}
