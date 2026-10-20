using System;
using HoneyComb.Simulation.Environment;
using HoneyComb.Simulation.Time;
using NUnit.Framework;
namespace HoneyComb.Tests
{
    public sealed class EnvironmentSystemTests
    {
        [TestCase(1)] [TestCase(24)] [TestCase(8760)]
        public void ManualHoursUpdateEnvironmentExactlyOncePerTick(int hours)
        {
            var environment = new EnvironmentSystem(new EnvironmentState(20,60,0,0,1), 100);
            var clock = new SimulationClock(100,1,false);
            clock.Tick += environment.TickHour;
            clock.QueueTicks(hours);
            while(clock.PendingTicks > 0) clock.Update(0,240);
            Assert.That(environment.ProcessedTicks, Is.EqualTo(hours));
            Assert.That(environment.LastUpdatedHour, Is.EqualTo(100 + hours));
            Assert.That(environment.Current.TemperatureCelsius, Is.EqualTo(20));
            Assert.That(environment.Current.HumidityPercent, Is.EqualTo(60));
            Assert.That(environment.Current.Daylight, Is.EqualTo(1));
        }
        [Test] public void PauseAndUnsubscribeStopUpdates()
        {
            var environment = new EnvironmentSystem(new EnvironmentState(20,60,0,0,1),0);
            var clock = new SimulationClock(0,1,false);
            clock.Tick += environment.TickHour;
            clock.Update(100,240);
            Assert.That(environment.ProcessedTicks, Is.Zero);
            clock.Automatic = true; clock.Update(2.5,240);
            Assert.That(environment.ProcessedTicks, Is.EqualTo(2));
            clock.Tick -= environment.TickHour; clock.Update(10,240);
            Assert.That(environment.ProcessedTicks, Is.EqualTo(2));
        }
        [Test] public void InvalidWeatherAndDuplicateTicksAreRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new EnvironmentState(20,101,0,0,1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new EnvironmentState(float.NaN,60,0,0,1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new EnvironmentState(20,60,-1,0,1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new EnvironmentState(20,60,0,-1,1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new EnvironmentState(20,60,0,0,2));
            var environment = new EnvironmentSystem(new EnvironmentState(20,60,0,0,1),0);
            environment.TickHour(1);
            Assert.Throws<ArgumentOutOfRangeException>(() => environment.TickHour(1));
        }
    }
}
