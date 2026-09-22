using HoneyComb.Simulation.Apiary;
using HoneyComb.Simulation.Hive;
using HoneyComb.Simulation.Bees;
using NUnit.Framework;
namespace HoneyComb.Tests
{
    public sealed class BeePopulationTests
    {
        [Test] public void AdultCreatesColonyOnFrameWithoutOccupyingCell()
        {
            var apiary=new ApiaryState("a","A",1,1,1);var hive=apiary.GetHive(0,0);var super=hive.AddSuper();hive.TryCreateFrame(super.Id,0,out var frame);
            var population=new BeePopulation(apiary);
            Assert.That(population.Bees.Count,Is.Zero);
            Assert.That(population.TryAddBee(hive,frame,FrameSide.Front,0,0,new BeeSpawnSettings(BeeCaste.Worker,BeeDevelopment.Adult,90),12,out var bee,out _),Is.True);
            Assert.That(bee.Health,Is.EqualTo(90));Assert.That(bee.AdultSinceHour,Is.EqualTo(12));Assert.That(bee.LaidHour,Is.Null);
            Assert.That(bee.Location.Kind,Is.EqualTo(BeeLocationKind.FrameSurface));Assert.That(bee.Location.X,Is.Null);
            Assert.That(frame.GetCell(FrameSide.Front,0,0).Value.Content,Is.EqualTo(CellContentType.Empty));
            Assert.That(population.GetColony(hive.Id).Bees[0].Id,Is.EqualTo(bee.Id));Assert.That(frame.ResidentBeeCount,Is.EqualTo(1));
        }
        [Test] public void BroodRequiresBuiltEmptyCellAndCannotBeErasedByDebugEdits()
        {
            var apiary=new ApiaryState("a","A",1,1,1);var hive=apiary.GetHive(0,0);var super=hive.AddSuper();hive.TryCreateFrame(super.Id,0,out var frame);
            var population=new BeePopulation(apiary);var settings=new BeeSpawnSettings(BeeCaste.Worker,BeeDevelopment.Larva,100);
            Assert.That(population.TryAddBee(hive,frame,FrameSide.Back,0,0,settings,1,out _,out _),Is.False);Assert.That(population.Colonies.Count,Is.Zero);
            frame.TrySetConstruction(FrameSide.Back,0,0,100);
            Assert.That(population.TryAddBee(hive,frame,FrameSide.Back,0,0,settings,1,out var bee,out _),Is.True);
            Assert.That(frame.GetCell(FrameSide.Back,0,0).Value.OccupantBeeId,Is.EqualTo(bee.Id));
            Assert.That(population.TryAddBee(hive,frame,FrameSide.Back,0,0,settings,1,out _,out _),Is.False);
            Assert.That(frame.TrySetContent(FrameSide.Back,0,0,CellContentType.Empty),Is.False);
            Assert.That(frame.TrySetCell(FrameSide.Back,0,0,new CellState(100)),Is.False);
            Assert.That(frame.TrySetAllConstruction(0),Is.False);Assert.That(frame.TrySetAllConstruction(100),Is.True);
            Assert.That(frame.GetCell(FrameSide.Back,0,0).Value.OccupantBeeId,Is.EqualTo(bee.Id));
        }
        [Test] public void FrameTransferMovesPhysicalPositionButKeepsColony()
        {
            var apiary=new ApiaryState("a","A",2,1,2);var a=apiary.GetHive(0,0);var b=apiary.GetHive(1,0);var sa=a.AddSuper();var sb=b.AddSuper();a.TryCreateFrame(sa.Id,0,out var frame);
            var population=new BeePopulation(apiary);population.TryAddBee(a,frame,FrameSide.Front,0,0,new BeeSpawnSettings(BeeCaste.Queen,BeeDevelopment.Adult,100),0,out var queen,out _);
            Assert.That(a.RemoveFrame(sa.Id,0),Is.False);Assert.That(apiary.RemoveHive(0,0),Is.False);
            Assert.That(a.MoveFrameTo(sa.Id,0,b,sb.Id,0),Is.True);
            Assert.That(population.FindPhysicalHive(queen.Location.FrameId),Is.SameAs(b));Assert.That(queen.ColonyId,Is.EqualTo(population.GetColony(a.Id).Id));
            Assert.That(apiary.RemoveHive(1,0),Is.False);
        }
        [Test] public void DetachedFrameAndInvalidSideDoNotCreateBees()
        {
            var apiary=new ApiaryState("a","A",1,1,1);var hive=apiary.GetHive(0,0);var super=hive.AddSuper();hive.TryCreateFrame(super.Id,0,out var frame);
            var population=new BeePopulation(apiary);var defaults=new BeeSpawnSettings(BeeCaste.Drone,BeeDevelopment.Adult,100);
            Assert.That(population.TryAddBee(hive,new Frame(frame.Id),FrameSide.Front,0,0,defaults,0,out _,out _),Is.False);
            Assert.That(population.TryAddBee(hive,frame,(FrameSide)99,0,0,defaults,0,out _,out _),Is.False);
            Assert.That(population.Bees.Count,Is.Zero);Assert.That(population.Colonies.Count,Is.Zero);
        }
    }
}
