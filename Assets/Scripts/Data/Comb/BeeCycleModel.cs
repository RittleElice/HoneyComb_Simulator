using System;
using System.Collections.Generic;

public enum CycleTask { None, Feed, Rest, Recover, Store, ForageNectar, ForagePollen, Build, Lay, Nurse, Heat, Clear }
public enum CycleAction { Idle, Move, Acquire, Feed, Rest, Recover, Store, Outside, Build, Lay, Nurse, Heat, Clear, Develop, Dead }
public enum CycleStage { Egg, Larva, Pupa, Adult, Dead }
public enum CycleEnd { Completed, ResourceMissing, PathBlocked, Interrupted }

// All rates are provisional and expressed per game minute, masses in mg, movement in mm/s.
[Serializable] public sealed class BeeCycleSettings
{
    public List<CycleFormula> formulas=BeeCycleRules.Defaults();
    public double naturalDecay=10, commandMaximum=30;
    public double feedWeight=40,restWeight=40,recoverWeight=40,storeWeight=40;
    public double reserveTarget=8, maxEnergy=100, maxHealth=100, carryCapacity=20;
    public double basalSugar=0.02, basalEnergy=0.15, movementEnergy=0.01;
    public double feedRate=2, restRate=8, energyPerSugar=10, recoveryRate=2, recoveryEnergy=2;
    public double speed=10, acquireRate=5, storeRate=5;
    public double forageMinutes=8, forageSugar=15, forageWater=15, foragePollen=15;
    public double stockSugarPerBee=12, stockPollenPerBee=4;
    public double buildPoints=1, sugarPerBuildPoint=8, desiredEmptyCellsPerBee=5;
    public double interruptPerElapsedMinute=0.0005, storageHeightBias=2, storageDistanceMm=200;
    public int demoWorkers=48, forageSourceId=0;
    public int demoFrames=2,demoConstruction=70;
    public int routeCacheEntries=128;
    public double demoSugar=300,demoWater=300,demoPollen=100;
    public double basalHeatJ=0.02712, extraHeatJ=0.6, heatEnergy=0.1;
    public double broodTick=1.0/1440, eggEnd=3, larvaEnd=9, adultStart=21;
    public double idleDevelopment=0.0007, restDevelopment=0.0007, recoverDevelopment=0.0008;
    public double feedDevelopment=0.001, moveDevelopment=0.0035, acquireDevelopment=0.0025;
    public double storeDevelopment=0.0025, forageDevelopment=0.0044, buildDevelopment=0.0035;
    public double nurseDevelopment=0.0025, heatDevelopment=0.0035, layDevelopment=0.0025, clearDevelopment=0.003;
    public double broodMinTemperature=20, broodIdealTemperature=34, broodMaxTemperature=40;
    public double larvaSugarPerTick=0.001, larvaPollenPerTick=0.0005, larvaEnergyPerTick=0.005;
    public double nurseRate=1, queenLayPerMinute=0.1, broodTarget=60, starvationDamage=0.01;
    public double maxDevelopment=200, broodReserveTicks=60, heatDeficitScale=10, storageHeightScale=20;
    public void Validate()
    {
        foreach(var field in typeof(BeeCycleSettings).GetFields())
        {
            if(field.FieldType!=typeof(double)&&field.FieldType!=typeof(int))continue;
            double v=Convert.ToDouble(field.GetValue(this));
            if(double.IsNaN(v)||double.IsInfinity(v)||v<0)throw new ArgumentException("Invalid setting: "+field.Name);
        }
        if(reserveTarget<=0||maxEnergy<=0||maxHealth<=0||carryCapacity<=0||energyPerSugar<=0||speed<=0||storageDistanceMm<=0||stockSugarPerBee<=0||stockPollenPerBee<=0||desiredEmptyCellsPerBee<=0||sugarPerBuildPoint<=0||recoveryEnergy<=0||heatEnergy<=0||broodTarget<=0||larvaSugarPerTick<=0||larvaPollenPerTick<=0||larvaEnergyPerTick<=0||heatDeficitScale<=0||storageHeightScale<=0||broodMinTemperature>=broodIdealTemperature||broodIdealTemperature>=broodMaxTemperature||eggEnd>=larvaEnd||larvaEnd>=adultStart||adultStart>=maxDevelopment)
            throw new ArgumentException("Positive capacity/rate denominator required.");
        if(routeCacheEntries<1||demoConstruction>100||demoFrames>8)throw new ArgumentException("Invalid cache/demo limits.");
    }
}

[Serializable] public sealed class CycleSugar { public int source; public double mass; }
[Serializable] public sealed class CycleCargo
{
    public List<CycleSugar> sugars=new List<CycleSugar>();
    public double pollen;
    public double Sugar { get { double n=0;foreach(var s in sugars)n+=s.mass;return n; } }
    public double Mass=>Sugar+pollen;
    public void Add(int source,double amount)
    {
        if(amount<=0)return;
        foreach(var s in sugars)if(s.source==source){s.mass+=amount;return;}
        sugars.Add(new CycleSugar{source=source,mass=amount});
    }
    public void Add(CycleCargo other,double fraction)
    { foreach(var s in other.sugars)Add(s.source,s.mass*fraction);pollen+=other.pollen*fraction; }
    public double Consume(double requested)
    {
        double total=Sugar,take=Math.Min(total,Math.Max(0,requested));
        if(total>0)foreach(var s in sugars)s.mass*=1-take/total;
        return take;
    }
}
[Serializable] public struct CyclePoint
{
    public int equipment,x,y,z,face;
    public CyclePoint(BeeGridPosition p){equipment=p.EquipmentId;x=p.X;y=p.Y;z=p.Z;face=(int)p.Face;}
    public BeeGridPosition Grid=>new BeeGridPosition(equipment,x,y,z,(BeeFace)face);
}
[Serializable] public sealed class CycleDesire
{
    public CycleTask task;public CyclePoint target;public double weight;
}
[Serializable] public sealed class CycleBee
{
    public long id;
    public bool queen;
    public int homeBox,broodColumn,broodRow;
    public CycleStage stage=CycleStage.Adult;
    public double development=21,broodSugar,broodPollen;
    public double reserve,energy,health;
    public CycleCargo cargo=new CycleCargo();
    public CycleTask task;
    public CycleAction action;
    public List<CycleDesire> desires=new List<CycleDesire>();
    internal Dictionary<(CycleTask,BeeGridPosition),CycleDesire> desireIndex;
    public CyclePoint target,waypoint,returnPoint;
    public bool hasTarget,hasWaypoint,outside,returning,interruptAfterReturn;
    public bool hasStoreExclusion;
    public CyclePoint storeExclusion;
    public double elapsed,outsideRemaining,progress;
    public List<CyclePoint> path=new List<CyclePoint>();
    public int pathIndex,topologyVersion;
    public double lastAcquired;
    public double tickActivity;
}
[Serializable] public sealed class CycleCounters
{
    public long completed,resourceFailed,pathFailed,interrupted,commands;
    public long deaths,emerged,eggs;
    public long[] actions=new long[15];
    public double externalSugar,externalPollen,discardedWater,consumedSugar,built;
    public double heatJ,lostSugar,lostPollen,consumedPollen;
}
