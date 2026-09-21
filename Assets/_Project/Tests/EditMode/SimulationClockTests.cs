using HoneyComb.Simulation.Time;
using NUnit.Framework;
namespace HoneyComb.Tests
{
 public sealed class SimulationClockTests
 {
  [Test] public void AutomaticTimeRetainsFractionAndCatchesUp()
  {
   var clock = new SimulationClock(0, 1, true);
   clock.Update(.75, 10); Assert.That(clock.TotalHours, Is.Zero);
   clock.Update(2.5, 10); Assert.That(clock.TotalHours, Is.EqualTo(3));
   Assert.That(clock.AccumulatedSeconds, Is.EqualTo(.25).Within(1e-9));
  }
  [Test] public void ManualYearRunsEveryTickWithFrameBudgetWhilePaused()
  {
   var clock = new SimulationClock(0, 1, false); int count=0;
   clock.Tick += _ => count++;
   clock.QueueTicks(8760); clock.Update(100, 240);
   Assert.That(clock.TotalHours, Is.EqualTo(240));
   Assert.That(clock.PendingTicks, Is.EqualTo(8520));
   while(clock.PendingTicks>0) clock.Update(100,240);
   Assert.That(clock.TotalHours, Is.EqualTo(8760));
   Assert.That(count, Is.EqualTo(8760));
   Assert.That(clock.AccumulatedSeconds, Is.Zero);
  }
  [Test] public void PausePreservesPartialAutomaticInterval()
  {
   var clock=new SimulationClock(0,1,true);
   clock.Update(.5,10); clock.Automatic=false; clock.Update(100,10);
   clock.QueueTicks(1); clock.Update(0,10);
   Assert.That(clock.TotalHours, Is.EqualTo(1));
   clock.Automatic=true; clock.Update(.5,10);
   Assert.That(clock.TotalHours, Is.EqualTo(2));
  }
  [Test] public void InvalidTimeIsRejected()
  {
   Assert.Throws<System.ArgumentOutOfRangeException>(()=>new SimulationClock(0,0,true));
   Assert.Throws<System.ArgumentOutOfRangeException>(()=>new SimulationClock(0,double.NaN,true));
   var clock=new SimulationClock(0,1,true);
   Assert.Throws<System.ArgumentOutOfRangeException>(()=>clock.QueueTicks(-1));
  }
 }
}
