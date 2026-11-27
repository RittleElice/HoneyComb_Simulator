using System;
using System.Collections.Generic;
using System.IO;

[Serializable] public sealed class ColonyDemandState {
 public int anchor;
 public double forage,build,lay,clear;
 public double sugarDays,pollenDays,workers,brood,complete,empty,unfinished,sugarDaily,pollenDaily,sugarSignal,pollenSignal,spaceShortage;
}
[Serializable] public sealed class RegionDemandState {
 public CyclePoint target;
 public double build,clear,nurse,heat;
}
[Serializable] public sealed class DemandFailureRecord { public long bee,minute;public int anchor,kind; }
public sealed partial class BeeCycleRuntime {
 public IEnumerable<string> DemandObservation(ISet<int> equipment){
  foreach(var d in ColonyDemands)if(equipment.Contains(d.anchor))yield return $"봉군 수요: 채집 {d.forage:F1} / 건설 {d.build:F1} / 산란 {d.lay:F1} / 정리 {d.clear:F1} · 비축 {d.sugarDays:F1}/{d.pollenDays:F1}일 · 예상 소비 {d.sugarDaily:F1}/{d.pollenDaily:F1}mg/일";
  foreach(var d in RegionDemands)if(equipment.Contains(d.target.equipment)&&(d.build+d.clear+d.nurse+d.heat)>0)yield return $"격자 {d.target.Grid} 수요: 건설 {d.build:F1} / 정리 {d.clear:F1} / 육아 {d.nurse:F1} / 발열 {d.heat:F1}";
 }
 public List<ColonyDemandState> ColonyDemands {get;private set;}=new List<ColonyDemandState>();
 public List<RegionDemandState> RegionDemands {get;private set;}=new List<RegionDemandState>();
 readonly List<DemandFailureRecord> demandFailures=new List<DemandFailureRecord>();
 readonly Dictionary<int,ColonyDemandState> colonyDemandIndex=new Dictionary<int,ColonyDemandState>();
 readonly Dictionary<BeeGridPosition,RegionDemandState> regionDemandIndex=new Dictionary<BeeGridPosition,RegionDemandState>();
 readonly Dictionary<int,int> demandAnchors=new Dictionary<int,int>();
 readonly Dictionary<(int,int),HashSet<long>> demandFailureSets=new Dictionary<(int,int),HashSet<long>>();
 readonly Dictionary<BeeGridPosition,double> demandBuildPulse=new Dictionary<BeeGridPosition,double>(),demandClearPulse=new Dictionary<BeeGridPosition,double>();
 double DemandNext(double previous,double increase,double decrease)=>F("demandNext",new CycleInputs{amount=previous,available=increase,need=decrease});
 double DemandRatio(double amount,double available)=>F("demandRatio",new CycleInputs{amount=amount,available=available});
 double DemandDeficit(double amount,double target)=>target<=0?0:F("deficit",new CycleInputs{amount=amount,available=target});
 bool DemandOccurs(double value)=>value>0&&Random()<F("demandProbability",new CycleInputs{amount=value});
 RegionDemandState DemandAt(Region r){if(!regionDemandIndex.TryGetValue(r.position,out var d)){d=new RegionDemandState{target=new CyclePoint(r.position)};RegionDemands.Add(d);regionDemandIndex.Add(r.position,d);}return d;}
 int DemandAnchor(int group){if(demandAnchors.TryGetValue(group,out int id))return id;return 0;}
 void RecordDemandShortage(CycleBee b,bool storage,bool pollen){
  if(Settings.demandEnabled==0||!Position(b,out var p))return;var air=Air(p);if(air==null||!groupRegions.TryGetValue(air.Group,out var members))return;
  double available=0;foreach(var r in members){Scan(r);if(storage){if(!r.clearing)available+=(b.cargo.Sugar>0?r.freeSugar:0)+(b.cargo.pollen>0?r.freePollen:0);}else available+=pollen?r.pollen:r.sugar;}
  if(available<=1e-9)RecordDemandFailure(b,storage?2:pollen?1:0);
 }
 void RecordDemandFailure(CycleBee bee,int kind){
  if(Settings.demandEnabled==0||!Position(bee,out var p))return;var air=Air(p);if(air==null)return;int anchor=DemandAnchor(air.Group);if(anchor==0)return;
  long minute=LastMinute+1;foreach(var f in demandFailures)if(f.bee==bee.id&&f.kind==kind&&f.anchor==anchor){f.minute=minute;return;}
  demandFailures.Add(new DemandFailureRecord{bee=bee.id,kind=kind,anchor=anchor,minute=minute});
 }
 double FailureRatio(int anchor,int kind,double workers)=>demandFailureSets.TryGetValue((anchor,kind),out var set)?DemandRatio(set.Count,Math.Max(1,workers)):0;
 void PulseDemandCommand(CycleTask task,Region r,int group,BeeGridPosition target,bool local){
  Commands.Add(new CycleCommand{task=task,target=new CyclePoint(target),group=group,need=1,localOnly=local});Counters.commands++;
 }
 Region ChooseDemandRegion(IReadOnlyList<Region> candidates,Func<Region,double> weight){Region selected=null;double sum=0;foreach(var r in candidates){double w=weight(r);if(w<=0)continue;sum+=w;if(Random()*sum<w)selected=r;}return selected;}
 bool CanClear(Region r,IReadOnlyList<Region> members){
  if(r.sugar+r.pollen<=0)return false;double sugar=0,pollen=0;
  foreach(var other in members)if(other!=r&&!other.clearing){sugar+=other.freeSugar;pollen+=other.freePollen;}
  return r.sugar<=sugar&&r.pollen<=pollen;
 }
 double ClearCellCount(Region r){Scan(r);return r.demandClearCells;}
 // One-minute pulses. Demand persistence never depends on command completion.
 void RefreshDemandCommands(){
  Commands.Clear();activeCommands.Clear();commandIndex.Clear();demandAnchors.Clear();colonyDemandIndex.Clear();regionDemandIndex.Clear();demandBuildPulse.Clear();demandClearPulse.Clear();
  foreach(var d in ColonyDemands)colonyDemandIndex.Add(d.anchor,d);
  foreach(var d in RegionDemands)regionDemandIndex.Add(d.target.Grid,d);
  foreach(var pair in groups){int anchor=0;if(entrances.TryGetValue(pair.Key,out var entry))anchor=entry.EquipmentId;else if(groupRegions.TryGetValue(pair.Key,out var list))foreach(var r in list)if(anchor==0||r.frame.Id<anchor)anchor=r.frame.Id;if(anchor!=0)demandAnchors[pair.Key]=anchor;}
  demandFailures.RemoveAll(f=>LastMinute+1-f.minute>=Settings.demandFailureWindow);
  demandFailureSets.Clear();foreach(var f in demandFailures){var key=(f.anchor,f.kind);if(!demandFailureSets.TryGetValue(key,out var set)){set=new HashSet<long>();demandFailureSets.Add(key,set);}set.Add(f.bee);}
  var touched=new HashSet<int>();
  foreach(var pair in groups){
   int group=pair.Key,anchor=DemandAnchor(group);if(anchor==0||!groupRegions.TryGetValue(group,out var members))continue;touched.Add(anchor);
   if(!colonyDemandIndex.TryGetValue(anchor,out var d)){d=new ColonyDemandState{anchor=anchor};ColonyDemands.Add(d);colonyDemandIndex.Add(anchor,d);}
   var g=pair.Value;double workers=g.demandWorkers,adults=g.count,brood=g.demandBrood,physicalWorkers=g.demandPhysicalWorkers;CycleBee queen=g.demandQueen;
   double complete=0,unfinished=0,freeSugar=0,freePollen=0,reserved=0;bool transferable=false;
   foreach(var r in members){var rd=DemandAt(r);complete+=20-r.unfinished;unfinished+=r.unfinished;freeSugar+=r.freeSugar;freePollen+=r.freePollen;bool clear=CanClear(r,members);transferable|=clear;
    reserved+=F("demandReservedCells",new CycleInputs{amount=r.unfinished,ratio=rd.build/100});if(clear)reserved+=F("demandReservedCells",new CycleInputs{amount=ClearCellCount(r),ratio=rd.clear/100});}
   double qs=F("demandDailySugar",new CycleInputs{amount=adults,available=brood}),qp=F("demandDailyPollen",new CycleInputs{amount=adults,available=brood});
   double ds=qs>0?F("demandDays",new CycleInputs{amount=g.sugar,available=qs}):Settings.demandSugarReserveDays,dp=qp>0?F("demandDays",new CycleInputs{amount=g.pollen,available=qp}):Settings.demandPollenReserveDays;
   double sourceSugar=0,sourcePollen=0;var sources=World.Weather?.Nectar?.Sources;if(sources!=null)foreach(var s in sources){if(s==null)continue;if(s.sugar>0&&F("forageSourceWeight",new CycleInputs{available=s.sugar})>0)sourceSugar+=s.sugar;if(s.pollen>0&&F("forageSourceWeight",new CycleInputs{available=s.pollen})>0)sourcePollen+=s.pollen;}
   double sugarSupply=F("demandSupplyTarget",new CycleInputs{amount=physicalWorkers,available=Settings.forageSugarRate}),pollenSupply=F("demandSupplyTarget",new CycleInputs{amount=physicalWorkers,available=Settings.foragePollenRate});
   double os=sugarSupply>0?DemandRatio(sourceSugar,sugarSupply):0,op=pollenSupply>0?DemandRatio(sourcePollen,pollenSupply):0;
   double hs=qs>0?DemandDeficit(ds,Settings.demandSugarDays):0,hp=qp>0?DemandDeficit(dp,Settings.demandPollenDays):0,rs=qs>0?DemandDeficit(ds,Settings.demandSugarReserveDays):0,rp=qp>0?DemandDeficit(dp,Settings.demandPollenReserveDays):0;
   double fs=FailureRatio(anchor,0,workers),fp=FailureRatio(anchor,1,workers),spaceFailure=FailureRatio(anchor,2,workers);
   double js=F("demandFoodSignal",new CycleInputs{amount=hs,available=os,ratio=rs,need=qs>0?DemandRatio(freeSugar,Settings.demandStorageDays*qs):0});
   double jp=F("demandFoodSignal",new CycleInputs{amount=hp,available=op,ratio=rp,need=qp>0?DemandRatio(freePollen,Settings.demandStorageDays*qp):0});
   double wt=F("demandWorkerTarget",new CycleInputs{amount=complete}),bt=F("demandBroodTarget",new CycleInputs{amount=workers,available=complete});
   double et=F("demandEmptyTarget",new CycleInputs{amount=workers,available=bt,need=brood});
   // Reservation suppresses new construction/clearing requests, not existing demand.
   double he=DemandDeficit(g.empty+reserved,et);
   double space=Math.Max(he,Math.Max(Math.Max(hs,os*rs)*DemandDeficit(freeSugar,Settings.demandStorageDays*qs),Math.Max(hp,op*rp)*DemandDeficit(freePollen,Settings.demandStorageDays*qp)));
   double growth=Math.Min(F("demandGrowthSugar",new CycleInputs{amount=ds}),F("demandGrowthPollen",new CycleInputs{amount=dp}));
   d.forage=DemandNext(d.forage,F("demandForageIncrease",new CycleInputs{amount=js,available=jp,need=Math.Max(fs,fp)}),Settings.demandForageDecay);
   d.build=DemandNext(d.build,unfinished>0?F("demandBuildIncrease",new CycleInputs{amount=space,need=spaceFailure}):0,Settings.demandBuildDecay);
   d.lay=DemandNext(d.lay,F("demandLayIncrease",new CycleInputs{amount=DemandDeficit(workers,wt),available=DemandDeficit(brood,bt),ratio=growth,need=DemandRatio(g.empty,Math.Max(1,Settings.demandEmptyPerWorker*bt))}),Settings.demandLayDecay);
   d.clear=DemandNext(d.clear,transferable?F("demandClearIncrease",new CycleInputs{amount=space,ratio=1,need=spaceFailure}):0,Settings.demandClearDecay);
   d.sugarDays=ds;d.pollenDays=dp;d.workers=workers;d.brood=brood;d.complete=complete;d.empty=g.empty;d.unfinished=unfinished;d.sugarDaily=qs;d.pollenDaily=qp;d.sugarSignal=js;d.pollenSignal=jp;d.spaceShortage=space;
   if(DemandOccurs(d.forage)&&entrances.TryGetValue(group,out var entrance)){
    double sw=sourceSugar>0?js+Settings.demandFailureGain*fs:0,pw=sourcePollen>0?jp+Settings.demandFailureGain*fp:0;
    if(sw+pw>0)PulseDemandCommand(Random()*(sw+pw)<sw?CycleTask.ForageNectar:CycleTask.ForagePollen,null,group,entrance,false);
   }
   if(DemandOccurs(d.build)&&ds>Settings.demandBuildSugarFloor){var target=ChooseDemandRegion(members,r=>r.unfinished>0?F("demandBuildCandidate",new CycleInputs{amount=r.unfinished,available=r.buildRemaining,need=DemandAt(r).build}):0);if(target!=null)demandBuildPulse[target.position]=Settings.demandRegionBuildPulse;}
   if(DemandOccurs(d.lay)&&queen!=null){var target=ChooseDemandRegion(members,r=>r.empty>0?F("demandLayCandidate",new CycleInputs{amount=r.empty,ratio=F("broodTemperature",new CycleInputs{temperature=r.temperature})}):0);if(target!=null)PulseDemandCommand(CycleTask.Lay,target,group,target.position,false);else if(g.empty==0)RecordDemandFailure(queen,2);}
   if(DemandOccurs(d.clear)){var target=ChooseDemandRegion(members,r=>CanClear(r,members)?F("demandClearCandidate",new CycleInputs{amount=ClearCellCount(r),available=r.sugar+r.pollen,need=DemandAt(r).clear}):0);if(target!=null)demandClearPulse[target.position]=Settings.demandRegionClearPulse;}
  }
  foreach(var d in ColonyDemands)if(!touched.Contains(d.anchor)){d.forage=DemandNext(d.forage,0,Settings.demandForageDecay);d.build=DemandNext(d.build,0,Settings.demandBuildDecay);d.lay=DemandNext(d.lay,0,Settings.demandLayDecay);d.clear=DemandNext(d.clear,0,Settings.demandClearDecay);}
  var seen=new HashSet<BeeGridPosition>();
  foreach(var r in regions){seen.Add(r.position);var d=DemandAt(r);demandBuildPulse.TryGetValue(r.position,out double bp);demandClearPulse.TryGetValue(r.position,out double cp);
   double sugarNeed=0,pollenNeed=0,sugarTarget=0,pollenTarget=0;foreach(var b in r.brood)if(b.stage==CycleStage.Larva){double s=Settings.larvaSugarPerTick*RepresentedBees(b)*Settings.broodReserveTicks,p=Settings.larvaPollenPerTick*RepresentedBees(b)*Settings.broodReserveTicks;sugarTarget+=s;pollenTarget+=p;sugarNeed+=Math.Max(0,s-b.broodSugar);pollenNeed+=Math.Max(0,p-b.broodPollen);}
   double nurse=Math.Max(sugarTarget>0?DemandRatio(sugarNeed,sugarTarget):0,pollenTarget>0?DemandRatio(pollenNeed,pollenTarget):0);
   double heat=r.brood.Count>0?F("heatNeed",new CycleInputs{amount=Math.Max(0,Settings.broodIdealTemperature-r.temperature)}):0;
   d.build=DemandNext(d.build,bp,Settings.demandRegionBuildDecay);d.clear=DemandNext(d.clear,cp,Settings.demandRegionClearDecay);d.nurse=DemandNext(d.nurse,F("demandNurseIncrease",new CycleInputs{ratio=nurse}),Settings.demandRegionNurseDecay);d.heat=DemandNext(d.heat,F("demandHeatIncrease",new CycleInputs{ratio=heat}),Settings.demandRegionHeatDecay);
   // Keep the clearing target unavailable for storage while its demand lingers.
   r.clearing=d.clear>0&&CanClear(r,groupRegions[r.group]);
   if(DemandOccurs(d.build))PulseDemandCommand(CycleTask.Build,r,r.group,r.position,d.build<Settings.demandBuildBroadcast);
   if(DemandOccurs(d.clear))PulseDemandCommand(CycleTask.Clear,r,r.group,r.position,d.clear<Settings.demandClearBroadcast);
   if(DemandOccurs(d.nurse))PulseDemandCommand(CycleTask.Nurse,r,r.group,r.position,d.nurse<Settings.demandNurseBroadcast);
   if(DemandOccurs(d.heat))PulseDemandCommand(CycleTask.Heat,r,r.group,r.position,d.heat<Settings.demandHeatBroadcast);
  }
  foreach(var d in RegionDemands)if(!seen.Contains(d.target.Grid)){d.build=DemandNext(d.build,0,Settings.demandRegionBuildDecay);d.clear=DemandNext(d.clear,0,Settings.demandRegionClearDecay);d.nurse=DemandNext(d.nurse,0,Settings.demandRegionNurseDecay);d.heat=DemandNext(d.heat,0,Settings.demandRegionHeatDecay);}
  IndexCommands();
 }
 void WriteDemandState(BinaryWriter w){CycleBinary.Write(w,ColonyDemands.GetType(),ColonyDemands);CycleBinary.Write(w,RegionDemands.GetType(),RegionDemands);CycleBinary.Write(w,demandFailures.GetType(),demandFailures);}
 void ReadDemandState(BinaryReader r){
  ColonyDemands=(List<ColonyDemandState>)CycleBinary.Read(r,typeof(List<ColonyDemandState>),15);RegionDemands=(List<RegionDemandState>)CycleBinary.Read(r,typeof(List<RegionDemandState>),15);demandFailures.Clear();demandFailures.AddRange((List<DemandFailureRecord>)CycleBinary.Read(r,typeof(List<DemandFailureRecord>),15));
  var anchors=new HashSet<int>();foreach(var d in ColonyDemands){if(d.anchor<=0||!anchors.Add(d.anchor))throw new InvalidDataException("Invalid colony demand identity");CheckDemand(d.forage);CheckDemand(d.build);CheckDemand(d.lay);CheckDemand(d.clear);}
  var points=new HashSet<BeeGridPosition>();foreach(var d in RegionDemands){if(!points.Add(d.target.Grid))throw new InvalidDataException("Duplicate region demand");CheckDemand(d.build);CheckDemand(d.clear);CheckDemand(d.nurse);CheckDemand(d.heat);}
  foreach(var f in demandFailures)if(f.bee<=0||f.anchor<=0||f.kind<0||f.kind>2||f.minute>LastMinute)throw new InvalidDataException("Invalid demand failure");
 }
 static void CheckDemand(double x){if(double.IsNaN(x)||double.IsInfinity(x)||x<0||x>100)throw new InvalidDataException("Invalid demand value");}
}
