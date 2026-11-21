from pathlib import Path
import shutil
root=Path(__file__).parent/'comb30-validation'
original=Path(r'C:\UnityProject\HoneyComb_Simulator')
for p in (original/'Assets/Scripts').rglob('*.cs'):
 assert p.read_bytes()==(root/p.relative_to(original)).read_bytes(),str(p)
out=root/'outputs/stage-profile';out.mkdir(parents=True,exist_ok=True)
def patch(rel, replacements):
 p=root/rel;s=p.read_text(encoding='utf-8-sig');q=out/'before'/rel;q.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(p,q)
 for old,new in replacements:
  assert s.count(old)==1,(rel,old,s.count(old))
  s=s.replace(old,new)
 p.write_text(s,encoding='utf-8')
patch('Assets/Scripts/Data/Thermal/HiveClimate.cs',[
 ('        readonly int n;', '''        // Benchmark-only instrumentation in isolated copy. No production code change.
        public static bool BenchEnabled, BenchSerial;
        public readonly long[] BenchTimes = new long[9];
        long benchStamp;
        void BenchStart(){if(!BenchEnabled)return;Array.Clear(BenchTimes,0,9);benchStamp=Stopwatch.GetTimestamp();}
        void BenchMark(int stage){if(!BenchEnabled)return;long now=Stopwatch.GetTimestamp();BenchTimes[stage]+=now-benchStamp;benchStamp=now;}
        readonly int n;'''),
 ('            activeExteriorAir=null;\n            if(steps==1)', '            activeExteriorAir=null;\n            BenchMark(0);\n            if(steps==1)'),
 ('                AccumulateEvaporation(60);\n                Prepare(true);','                AccumulateEvaporation(60);\n                BenchMark(1);\n                Prepare(true);\n                BenchMark(2);'),
 ('                AccumulateExteriorMoisture(60);\n                Mark(2);','                BenchMark(3);\n                AccumulateExteriorMoisture(60);\n                BenchMark(4);\n                Mark(2);'),
 ('                AccumulateHeat();\n                Mark(3);','                AccumulateHeat();\n                BenchMark(5);\n                Mark(3);'),
 ('                CommitChanges();\n            }\n            else LegacyMoisture(steps);','                CommitChanges();\n                BenchMark(6);\n            }\n            else LegacyMoisture(steps);')])
patch('Assets/Scripts/Data/Thermal/ClimateDomain.cs',[
 ('        if(models.Length==1)models[0].LiveMinute(outside,humidity,wind);','        if(HiveClimate.Model.BenchSerial)foreach(var m in models)m.LiveMinute(outside,humidity,wind);\n        else if(models.Length==1)models[0].LiveMinute(outside,humidity,wind);'),
 ('        internal void LiveMinute(double outside,double humidity,double wind=0)\n        {','        internal void LiveMinute(double outside,double humidity,double wind=0)\n        {\n            BenchStart();'),
 ('            Minute(1);\n            int correctedCount=0;','            BenchMark(0);\n            Minute(1);\n            int correctedCount=0;'),
 ('            PublishEquipmentEnergy();\n            flowReady=flowRequested;','            BenchMark(7);\n            PublishEquipmentEnergy();\n            flowReady=flowRequested;\n            BenchMark(8);')])
# Reuse the already measured, unchanged fixture. Only the runner and profiling hooks differ.
p=root/'Assets/Editor/CombTickTiming.cs';s=p.read_text();
s=s.replace('public static class CombTickTiming','public static class CombStageTiming').replace('outputs/tick-timing','outputs/stage-profile')
s=s.replace('foreach(int count in new[]{1,10,100}){', '''foreach(string mode in new[]{"parallel-off-before","parallel-profile","serial-profile","parallel-off-after"}){
                    const int count=100;
                    HiveClimate.Model.BenchEnabled=mode.Contains("profile");
                    HiveClimate.Model.BenchSerial=mode=="serial-profile";
                    var stageTotal=new double[9];
                    HiveClimate.Model[] profiledModels=null;''')
s=s.replace('hives,minute,thermal_ms,weather_nectar_ms,total_ms','mode,hives,minute,thermal_ms,weather_nectar_ms,total_ms,prepare_sum_ms,evap_sum_ms,pressure_sum_ms,internal_moisture_sum_ms,external_moisture_sum_ms,heat_sum_ms,commit_sum_ms,wet_writeback_sum_ms,publish_sum_ms')
s=s.replace('hives,frames,logical_cells,thermal_cells,faces,boundaries,first_ms,mean_ms,p50_ms,p95_ms,max_ms,thermal_mean_ms,weather_mean_ms,encode_ms,store_validate_ms,save_bytes','mode,hives,frames,logical_cells,thermal_cells,faces,boundaries,first_ms,mean_ms,p50_ms,p95_ms,max_ms,thermal_mean_ms,weather_mean_ms,encode_ms,store_validate_ms,save_bytes,prepare_sum_ms,evap_sum_ms,pressure_sum_ms,internal_moisture_sum_ms,external_moisture_sum_ms,heat_sum_ms,commit_sum_ms,wet_writeback_sum_ms,publish_sum_ms,sha256')
s=s.replace('csv.WriteLine(FormattableString.Invariant($"{count},{minute},{thermal.Ms:F6},{weather.Ms:F6},{s.Elapsed.TotalMilliseconds:F6}"));', '''if(profiledModels==null){
                                var domain=typeof(ThermalWorld).GetField("climate",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).GetValue(world.Thermal);
                                profiledModels=(HiveClimate.Model[])domain.GetType().GetField("models",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).GetValue(domain);
                                if(profiledModels.Length!=100)throw new Exception("Expected 100 independent hive partitions");
                            }
                            var stages=new double[9];
                            if(HiveClimate.Model.BenchEnabled)foreach(var m in profiledModels)for(int k=0;k<9;k++)stages[k]+=m.BenchTimes[k]*1000d/Stopwatch.Frequency;
                            if(minute>10)for(int k=0;k<9;k++)stageTotal[k]+=stages[k];
                            csv.WriteLine(FormattableString.Invariant($"{mode},{count},{minute},{thermal.Ms:F6},{weather.Ms:F6},{s.Elapsed.TotalMilliseconds:F6},")+string.Join(",",stages.Select(v=>v.ToString("F6",System.Globalization.CultureInfo.InvariantCulture))));''')
s=s.replace('UtcTicks=DateTime.UtcNow.Ticks','UtcTicks=1').replace('folder+"/save-"+count','folder+"/save-"+mode')
s=s.replace('$"{count},{count*30}', '$"{mode},{count},{count*30}')
s=s.replace('{bytes.Length}"));','''{bytes.Length},")+string.Join(",",stageTotal.Select(v=>(v/100).ToString("F6",System.Globalization.CultureInfo.InvariantCulture)))+","+BitConverter.ToString(System.Security.Cryptography.SHA256.Create().ComputeHash(bytes)).Replace("-",""));''')
s=s.replace('"TIMING COMPLETE hives="+count','"STAGE TIMING COMPLETE mode="+mode')
(root/'Assets/Editor/CombStageTiming.cs').write_text(s,encoding='utf-8')
print('Isolated instrumentation installed; original runtime source untouched.')
