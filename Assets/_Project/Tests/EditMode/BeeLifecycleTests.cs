using System;
using HoneyComb.Simulation.Apiary;
using HoneyComb.Simulation.Bees;
using HoneyComb.Simulation.Hive;
using HoneyComb.Simulation.Time;
using NUnit.Framework;
namespace HoneyComb.Tests
{
    public sealed class BeeLifecycleTests
    {
        private static BeeLifecycleSettings Fast(double speed=1,bool laying=false,int eggs=1)
            => new BeeLifecycleSettings(new BeeDevelopmentRule(2,2,2),new BeeDevelopmentRule(1,1,1),new BeeDevelopmentRule(3,3,3),speed,laying,eggs,80);
        private static BeePopulation Setup(out ApiaryState apiary,out HiveState hive,out Frame frame,BeeLifecycleSettings settings=null,long hour=0)
        {
            apiary=new ApiaryState("a","A",2,1,2);hive=apiary.GetHive(0,0);
            var super=hive.AddSuper();hive.TryCreateFrame(super.Id,0,out frame);frame.TrySetAllConstruction(100);
            return new BeePopulation(apiary,null,settings??Fast(),hour);
        }
        private static BeeState Add(BeePopulation p,HiveState h,Frame f,BeeDevelopment stage=BeeDevelopment.Egg,BeeCaste caste=BeeCaste.Worker)
        {
            Assert.That(p.TryAddBee(h,f,FrameSide.Front,0,0,new BeeSpawnSettings(caste,stage,100),p.CurrentHour,out var bee,out var error),Is.True,error);
            return bee;
        }
        [Test] public void DevelopmentUsesThresholdsAndEmergencePreservesIdentityAndIndexes()
        {
            var p=Setup(out _,out var h,out var f);var original=Add(p,h,f);
            var cellView=p.GetBeesAt(original.Location);var face=p.GetBeesOnSurface(f.Id,FrameSide.Front);
            p.TickHour(1);Assert.That(face[0].Development,Is.EqualTo(BeeDevelopment.Egg));
            p.TickHour(2);Assert.That(face[0].Development,Is.EqualTo(BeeDevelopment.Larva));
            p.TickHour(3);p.TickHour(4);Assert.That(face[0].Development,Is.EqualTo(BeeDevelopment.Pupa));
            p.TickHour(5);p.TickHour(6);
            Assert.That(p.TryGetBee(original.Id,out var adult),Is.True);
            Assert.That(adult.Development,Is.EqualTo(BeeDevelopment.Adult));Assert.That(adult.AdultSinceHour,Is.EqualTo(6));
            Assert.That(adult.LaidHour,Is.Zero);Assert.That(adult.ColonyId,Is.EqualTo(original.ColonyId));
            Assert.That(adult.Location,Is.EqualTo(BeeLocation.OnFrame(f.Id,FrameSide.Front)));
            Assert.That(cellView.Count,Is.Zero);Assert.That(face.Count,Is.EqualTo(1));Assert.That(face[0].Id,Is.EqualTo(original.Id));
            Assert.That(p.GetBeesAt(adult.Location)[0].Id,Is.EqualTo(original.Id));Assert.That(f.ResidentBeeCount,Is.EqualTo(1));
            Assert.That(p.GetBeeInCell(f,FrameSide.Front,0,0),Is.Null);
            Assert.That(f.GetCell(FrameSide.Front,0,0).Value.Content,Is.EqualTo(CellContentType.Empty));
            Assert.That(f.GetCell(FrameSide.Front,0,0).Value.Construction,Is.EqualTo(100));
            Assert.That(original.Development,Is.EqualTo(BeeDevelopment.Egg),"Read snapshots cannot mutate the canonical bee.");
            p.TickHour(7);Assert.That(p.Bees[0].AdultSinceHour,Is.EqualTo(6));
            Assert.That(Add(p,h,f).Id,Is.GreaterThan(original.Id));
        }
        [Test] public void FractionalAndZeroRatesDoNotAlterChronologicalBirthTime()
        {
            var p=Setup(out _,out var h,out var f,Fast(0.5));Add(p,h,f);
            p.TickHour(1);Assert.That(p.Bees[0].DevelopmentProgress,Is.EqualTo(0.5));
            p.TickHour(2);p.TickHour(3);p.TickHour(4);Assert.That(p.Bees[0].Development,Is.EqualTo(BeeDevelopment.Larva));Assert.That(p.Bees[0].LaidHour,Is.Zero);
            var paused=Setup(out _,out h,out f,Fast(0));Add(paused,h,f);paused.TickHour(1);Assert.That(paused.Bees[0].DevelopmentProgress,Is.Zero);
        }
        [TestCase(BeeCaste.Worker,6)] [TestCase(BeeCaste.Queen,3)] [TestCase(BeeCaste.Drone,9)]
        public void CasteRulesControlEmergence(BeeCaste caste,int hours)
        {
            var p=Setup(out _,out var h,out var f);Add(p,h,f,BeeDevelopment.Egg,caste);
            for(int hour=1;hour<=hours;hour++) p.TickHour(hour);
            Assert.That(p.Bees[0].Development,Is.EqualTo(BeeDevelopment.Adult));Assert.That(p.Bees[0].AdultSinceHour,Is.EqualTo(hours));Assert.That(p.Bees[0].CanLayEggs,Is.False);
        }
        [Test] public void TransferredBroodEmergesAtCurrentFrameWithoutChangingColony()
        {
            var p=Setup(out var a,out var h,out var f);var bee=Add(p,h,f,BeeDevelopment.Pupa);
            var destination=a.GetHive(1,0);var super=destination.AddSuper();
            Assert.That(h.MoveFrameTo(h.Supers[0].Id,0,destination,super.Id,0),Is.True);
            p.TickHour(1);p.TickHour(2);p.TryGetBee(bee.Id,out var adult);
            Assert.That(p.FindPhysicalHive(adult.Location),Is.SameAs(destination));Assert.That(adult.ColonyId,Is.EqualTo(bee.ColonyId));
            Assert.That(adult.LaidHour,Is.Null);Assert.That(adult.AdultSinceHour,Is.EqualTo(2));
            Assert.That(p.TryMoveAdult(adult.Id,BeeLocation.AtHiveBase(destination.Id),out _),Is.True);
            Assert.That(f.ResidentBeeCount,Is.Zero);Assert.That(destination.Base.ResidentBeeCount,Is.EqualTo(1));
        }
        [Test] public void ManualQueenLayingRequiresReadinessAndFreeBuiltCell()
        {
            var p=Setup(out _,out var h,out var f);f.TrySetAllConstruction(0);var q=Add(p,h,f,BeeDevelopment.Adult,BeeCaste.Queen);
            Assert.That(p.TryLayEgg(q.Id,0,out _,out _),Is.False);Assert.That(p.SetQueenLaying(q.Id,true),Is.True);
            Assert.That(p.TryLayEgg(q.Id,0,out _,out _),Is.False);Assert.That(p.Bees.Count,Is.EqualTo(1));
            f.TrySetConstruction(FrameSide.Back,2,3,100);
            Assert.That(p.TryLayEgg(q.Id,0,out var egg,out _),Is.True);Assert.That(egg.ColonyId,Is.EqualTo(q.ColonyId));
            Assert.That(egg.Location,Is.EqualTo(BeeLocation.InCell(f.Id,FrameSide.Back,2,3)));Assert.That(egg.Health,Is.EqualTo(80));
            Assert.That(f.GetCell(FrameSide.Back,2,3).Value.OccupantBeeId,Is.EqualTo(egg.Id));
            Assert.That(p.TryLayEgg(q.Id,0,out _,out _),Is.False);Assert.That(p.Store.Count,Is.EqualTo(2));
            Assert.That(p.SetQueenLaying(egg.Id,true),Is.False);
        }
        [Test] public void NewbornsDoNotDevelopInTheirBirthTickAndOutsideQueensCannotLay()
        {
            var p=Setup(out _,out var h,out var f,Fast(1,true,2));var q=Add(p,h,f,BeeDevelopment.Adult,BeeCaste.Queen);p.SetQueenLaying(q.Id,true);
            p.TickHour(1);Assert.That(p.Bees.Count,Is.EqualTo(3));Assert.That(p.Bees[1].DevelopmentProgress,Is.Zero);Assert.That(p.Bees[1].LaidHour,Is.EqualTo(1));
            p.TickHour(2);Assert.That(p.Bees.Count,Is.EqualTo(5));Assert.That(p.Bees[1].DevelopmentProgress,Is.EqualTo(1));Assert.That(p.Bees[3].DevelopmentProgress,Is.Zero);
            p.TryMoveAdult(q.Id,BeeLocation.AtExternalRegion(p.ExternalRegion.Id),out _);p.TickHour(3);Assert.That(p.Bees.Count,Is.EqualTo(5));
        }
        [Test] public void ClockBulkAdvanceMatchesHourlyAdvanceAndSupportsInitialHour()
        {
            var p=Setup(out _,out var h,out var f,Fast(),100);Add(p,h,f);
            var clock=new SimulationClock(100,1,false);clock.Tick+=p.TickHour;clock.QueueTicks(6);clock.Update(0,6);
            Assert.That(p.CurrentHour,Is.EqualTo(106));Assert.That(p.Bees[0].AdultSinceHour,Is.EqualTo(106));
            Assert.Throws<ArgumentOutOfRangeException>(()=>p.TickHour(106));Assert.Throws<ArgumentOutOfRangeException>(()=>p.TickHour(108));
            Assert.That(p.CurrentHour,Is.EqualTo(106));
        }
        [Test] public void InvalidMasterValuesAreRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(()=>new BeeDevelopmentRule(0,1,1));
            Assert.Throws<ArgumentOutOfRangeException>(()=>new BeeDevelopmentRule(double.NaN,1,1));
            Assert.Throws<ArgumentOutOfRangeException>(()=>Fast(double.PositiveInfinity));
            Assert.Throws<ArgumentOutOfRangeException>(()=>Fast(-1));
            Assert.Throws<ArgumentOutOfRangeException>(()=>Fast(1,true,-1));
        }
        [Test] public void MassEmergenceRetainsAllIndividualsAndClearsAllCells()
        {
            var p=Setup(out _,out var h,out var f,Fast(10));
            for(int i=0;i<1000;i++) Assert.That(p.TryAddBee(h,f,FrameSide.Front,i%90,i/90,new BeeSpawnSettings(BeeCaste.Worker,BeeDevelopment.Egg,100),0,out _,out _),Is.True);
            p.TickHour(1);Assert.That(p.Store.Count,Is.EqualTo(1000));Assert.That(f.ResidentBeeCount,Is.EqualTo(1000));
            Assert.That(p.GetBeesAt(BeeLocation.OnFrame(f.Id,FrameSide.Front)).Count,Is.EqualTo(1000));
            foreach(var bee in p.Bees) Assert.That(bee.Development,Is.EqualTo(BeeDevelopment.Adult));
            for(int i=0;i<1000;i++) Assert.That(f.GetCell(FrameSide.Front,i%90,i/90).Value.OccupantBeeId,Is.Zero);
        }
    }
}
