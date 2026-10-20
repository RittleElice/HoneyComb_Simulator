using HoneyComb.Simulation.Time;
using NUnit.Framework;
namespace HoneyComb.Tests
{
 public sealed class SimulationClockTests
 {
  [Test] public void AutomaticTimeRetainsFractionAndCatchesUp()
  {
   var clock = new SimulationClock(0, 1, true);
   clock.Update(.75, 10); Assert.That(clock.TotalMinutes, Is.Zero);
   clock.Update(2.5, 10); Assert.That(clock.TotalMinutes, Is.EqualTo(3));
   Assert.That(clock.AccumulatedSeconds, Is.EqualTo(.25).Within(1e-9));
  }
  [Test] public void ManualYearRunsEveryTickWithFrameBudgetWhilePaused()
  {
   var clock = new SimulationClock(0, 1, false); int count=0;
   clock.Tick += _ => count++;
   clock.QueueTicks(525600); clock.Update(100, 240);
   Assert.That(clock.TotalMinutes, Is.EqualTo(240));
   Assert.That(clock.PendingTicks, Is.EqualTo(525360));
   while(clock.PendingTicks>0) clock.Update(100,240);
   Assert.That(clock.TotalMinutes, Is.EqualTo(525600));
   Assert.That(count, Is.EqualTo(525600));
   Assert.That(clock.AccumulatedSeconds, Is.Zero);
  }
  [Test] public void PausePreservesPartialAutomaticInterval()
  {
   var clock=new SimulationClock(0,1,true);
   clock.Update(.5,10); clock.Automatic=false; clock.Update(100,10);
   clock.QueueTicks(1); clock.Update(0,10);
   Assert.That(clock.TotalMinutes, Is.EqualTo(1));
   clock.Automatic=true; clock.Update(.5,10);
   Assert.That(clock.TotalMinutes, Is.EqualTo(2));
  }
  [Test] public void FiveSecondsAdvancesOneMinuteAndHourBoundaryFollowsMinute()
  {
   var clock=new SimulationClock(59,5,true);
   var events=new System.Collections.Generic.List<string>();
   clock.Tick+=m=>events.Add("minute:"+m);
   clock.HourTick+=h=>events.Add("hour:"+h);
   clock.Update(4.9,240); Assert.That(clock.TotalMinutes,Is.EqualTo(59));
   clock.Update(.2,240);
   Assert.That(clock.TotalMinutes,Is.EqualTo(60));
   Assert.That(clock.TotalHours,Is.EqualTo(1));
   Assert.That(events,Is.EqualTo(new[]{"minute:60","hour:1"}));
   Assert.That(clock.AccumulatedSeconds,Is.EqualTo(.1).Within(1e-9));
  }
  [Test] public void BulkMinutesMatchSingleStepsAcrossYearBoundary()
  {
   var bulk=new SimulationClock(525599,5,false);
   var single=new SimulationClock(525599,5,false);
   var hours=new System.Collections.Generic.List<long>();
   bulk.HourTick+=hours.Add;
   bulk.QueueTicks(121); while(bulk.PendingTicks>0) bulk.Update(0,17);
   for(int i=0;i<121;i++){single.QueueTicks(1);single.Update(0,1);}
   Assert.That(bulk.TotalMinutes,Is.EqualTo(single.TotalMinutes));
   Assert.That(hours,Is.EqualTo(new long[]{8760,8761,8762}));
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
