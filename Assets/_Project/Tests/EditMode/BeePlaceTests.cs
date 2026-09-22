using HoneyComb.Simulation.Apiary;
using HoneyComb.Simulation.Bees;
using HoneyComb.Simulation.Hive;
using HoneyComb.Simulation.Environment;
using NUnit.Framework;
namespace HoneyComb.Tests
{
    public sealed class BeePlaceTests
    {
        private static BeeSpawnSettings Adult => new BeeSpawnSettings(BeeCaste.Worker,BeeDevelopment.Adult,100);
        [Test] public void AdultsCanBeCreatedInEveryNewPlace()
        {
            var a=new ApiaryState("a","A",2,2,1);var h=a.GetHive(0,0);var p=new BeePopulation(a,new ExternalRegionState("flowers","Flowers"));
            Assert.That(p.TryAddAdult(h,BeeLocation.AtHiveBase(h.Id),Adult,0,out var baseBee,out _),Is.True);
            Assert.That(p.TryAddAdult(h,BeeLocation.AtApiary(a.Id,0,0),Adult,0,out var outdoorBee,out _),Is.True);
            Assert.That(p.TryAddAdult(h,BeeLocation.AtExternalRegion("flowers"),Adult,0,out var outsideBee,out _),Is.True);
            Assert.That(p.GetBeesInBase(h.Id)[0].Id,Is.EqualTo(baseBee.Id));Assert.That(p.GetBeesOutdoors(0,0)[0].Id,Is.EqualTo(outdoorBee.Id));
            Assert.That(p.GetBeesOutside()[0].Id,Is.EqualTo(outsideBee.Id));Assert.That(h.Base.ResidentBeeCount,Is.EqualTo(1));
            Assert.That(p.GetColony(h.Id).Bees.Count,Is.EqualTo(3));
        }
        [Test] public void RoundTripUpdatesAllIndexesAndCounts()
        {
            var a=new ApiaryState("a","A",2,2,1);var h=a.GetHive(0,0);var s=h.AddSuper();h.TryCreateFrame(s.Id,0,out var f);var p=new BeePopulation(a);
            p.TryAddBee(h,f,FrameSide.Front,0,0,Adult,0,out var bee,out _);var original=bee.ColonyId;
            var front=p.GetBeesOnSurface(f.Id,FrameSide.Front);
            Assert.That(p.TryMoveAdult(bee.Id,BeeLocation.AtHiveBase(h.Id),out _),Is.True);
            Assert.That(front.Count,Is.Zero);Assert.That(f.ResidentBeeCount,Is.Zero);Assert.That(h.Base.ResidentBeeCount,Is.EqualTo(1));
            Assert.That(p.TryMoveAdult(bee.Id,BeeLocation.AtApiary(a.Id,1,1),out _),Is.True);Assert.That(h.Base.ResidentBeeCount,Is.Zero);
            Assert.That(p.GetBeesOutdoors(1,1)[0].Id,Is.EqualTo(bee.Id));
            Assert.That(p.TryMoveAdult(bee.Id,BeeLocation.AtExternalRegion(p.ExternalRegion.Id),out _),Is.True);Assert.That(p.GetBeesOutdoors(1,1).Count,Is.Zero);
            Assert.That(p.TryMoveAdult(bee.Id,BeeLocation.OnFrame(f.Id,FrameSide.Back),out _),Is.True);
            Assert.That(p.GetBeesOutside().Count,Is.Zero);Assert.That(p.GetBeesOnSurface(f.Id,FrameSide.Back)[0].Id,Is.EqualTo(bee.Id));
            Assert.That(f.ResidentBeeCount,Is.EqualTo(1));Assert.That(bee.ColonyId,Is.EqualTo(original));Assert.That(p.Bees.Count,Is.EqualTo(1));
        }
        [Test] public void InvalidMovesLeaveEverythingUnchanged()
        {
            var a=new ApiaryState("a","A",1,1,1);var h=a.GetHive(0,0);var p=new BeePopulation(a);
            p.TryAddAdult(h,BeeLocation.AtHiveBase(h.Id),Adult,0,out var bee,out _);
            var invalid=new[]{BeeLocation.AtApiary(a.Id,1,0),BeeLocation.AtApiary("wrong",0,0),BeeLocation.AtHiveBase("missing"),BeeLocation.AtExternalRegion("missing"),BeeLocation.OnFrame("missing",FrameSide.Front),default(BeeLocation)};
            foreach(var location in invalid)Assert.That(p.TryMoveAdult(bee.Id,location,out _),Is.False);
            Assert.That(p.GetBeesInBase(h.Id).Count,Is.EqualTo(1));Assert.That(h.Base.ResidentBeeCount,Is.EqualTo(1));
            Assert.That(p.TryMoveAdult(bee.Id,bee.Location,out _),Is.True);Assert.That(p.GetBeesInBase(h.Id).Count,Is.EqualTo(1));
        }
        [Test] public void BroodCannotMoveOrBePlacedOutside()
        {
            var a=new ApiaryState("a","A",1,1,1);var h=a.GetHive(0,0);var s=h.AddSuper();h.TryCreateFrame(s.Id,0,out var f);f.TrySetConstruction(FrameSide.Front,0,0,100);
            var p=new BeePopulation(a);var egg=new BeeSpawnSettings(BeeCaste.Worker,BeeDevelopment.Egg,100);
            Assert.That(p.TryAddAdult(h,BeeLocation.AtHiveBase(h.Id),egg,0,out _,out _),Is.False);Assert.That(p.Colonies.Count,Is.Zero);
            p.TryAddBee(h,f,FrameSide.Front,0,0,egg,0,out var bee,out _);
            Assert.That(p.TryMoveAdult(bee.Id,BeeLocation.AtExternalRegion(p.ExternalRegion.Id),out _),Is.False);
            Assert.That(p.GetBeeInCell(f,FrameSide.Front,0,0).Value.Id,Is.EqualTo(bee.Id));Assert.That(p.GetBeesOnFrame(f.Id).Count,Is.EqualTo(1));
        }
        [Test] public void ForeignBaseResidentBlocksHiveRemovalUntilMovedAway()
        {
            var a=new ApiaryState("a","A",2,1,2);var home=a.GetHive(0,0);var guest=a.GetHive(1,0);var p=new BeePopulation(a);
            p.TryAddAdult(home,BeeLocation.AtHiveBase(guest.Id),Adult,0,out var bee,out _);
            Assert.That(guest.HasColony,Is.False);Assert.That(a.RemoveHive(1,0),Is.False);
            p.TryMoveAdult(bee.Id,BeeLocation.AtExternalRegion(p.ExternalRegion.Id),out _);
            Assert.That(guest.Base.ResidentBeeCount,Is.Zero);Assert.That(a.RemoveHive(1,0),Is.True);
            Assert.That(a.RemoveHive(0,0),Is.False);
        }
        [Test] public void BaseLocationFollowsHiveButOutdoorLocationStaysAtGridCoordinate()
        {
            var a=new ApiaryState("a","A",2,1,1);var h=a.GetHive(0,0);var p=new BeePopulation(a);
            p.TryAddAdult(h,BeeLocation.AtHiveBase(h.Id),Adult,0,out var inside,out _);
            p.TryAddAdult(h,BeeLocation.AtApiary(a.Id,0,0),Adult,0,out var outdoor,out _);
            a.MoveHive(0,0,1,0);
            Assert.That(p.FindPhysicalHive(inside.Location),Is.SameAs(a.GetHive(1,0)));Assert.That(p.GetBeesOutdoors(0,0)[0].Id,Is.EqualTo(outdoor.Id));
            Assert.That(p.FindPhysicalHive(outdoor.Location),Is.Null);
        }
        [Test] public void ReturnedIndexesAreReadOnlyLiveViews()
        {
            var a=new ApiaryState("a","A",1,1,1);var h=a.GetHive(0,0);var p=new BeePopulation(a);
            p.TryAddAdult(h,BeeLocation.AtHiveBase(h.Id),Adult,0,out var bee,out _);var view=p.GetBeesInBase(h.Id);
            Assert.That(view,Is.Not.InstanceOf<System.Collections.Generic.IList<BeeState>>());
            p.TryMoveAdult(bee.Id,BeeLocation.AtExternalRegion(p.ExternalRegion.Id),out _);Assert.That(view.Count,Is.Zero);
            p.TryMoveAdult(bee.Id,BeeLocation.AtHiveBase(h.Id),out _);Assert.That(view.Count,Is.EqualTo(1));
        }
    }
}
