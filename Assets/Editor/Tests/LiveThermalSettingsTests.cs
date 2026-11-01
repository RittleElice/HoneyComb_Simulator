using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
public static class LiveThermalSettingsTests {
 static void Check(bool v,string m){if(!v)throw new Exception(m);}
 public static void Run(){var settings=Resources.Load<ThermalExchangeSettings>("Thermal/ExchangeSettings");double oldF=settings.MaximumFractionPerMinute,oldH=settings.ExteriorFilmCoefficientWPerM2K;
 string output=Path.GetFullPath("outputs/thermal-controls/results.txt");
 Directory.CreateDirectory(Path.GetDirectoryName(output));
 try{
 settings.MaximumFractionPerMinute=.1;settings.ExteriorFilmCoefficientWPerM2K=5;
 var m=ThermalMaterials.Load();var w=new GameWorld(new WeatherSimulator(42,WeatherAssetLoader.LoadBaseline()));w.PrepareThermal(m);w.Thermal.PrepareExchange();
 var e=w.Thermal.Exchange;var energies=Enumerable.Range(0,e.CellCount).Select(i=>{var c=e.GetCell(i);return c.Owner.EnergyJ[c.Index];}).ToArray();
 var snap=new WorldSaveSnapshot{World=w,UtcTicks=DateTime.UtcNow.Ticks,TargetMinute=0};var bytes=WorldSaveCodec.Encode(snap);int builds=w.Thermal.TopologyBuilds;
 settings.MaximumFractionPerMinute=.5;w.Thermal.PrepareExchange();var next=w.Thermal.Exchange;
 Check(!ReferenceEquals(e,next)&&next.MaximumFractionPerMinute==.5,"live fraction");Check(w.Thermal.TopologyBuilds==builds+1,"one rebuild");
 Check(energies.SequenceEqual(Enumerable.Range(0,next.CellCount).Select(i=>{var c=next.GetCell(i);return c.Owner.EnergyJ[c.Index];})),"energy preserved on apply");
 Check(bytes.SequenceEqual(WorldSaveCodec.Encode(snap)),"setting application does not rewrite world/save");w.Thermal.PrepareExchange();Check(ReferenceEquals(next,w.Thermal.Exchange),"no recurring rebuild");
 for(int i=0;i<next.CellCount;i++){var c=next.GetCell(i);Check(60*next.SumConductance(i)/c.Owner.HeatCapacityJPerK[c.Index]<=.50000001,"50% budget");c.Owner.RestoreEnergy(c.Index,c.Owner.HeatCapacityJPerK[c.Index]*20);}
 for(int n=0;n<120;n++)next.Step(60,0);
 for(int i=0;i<next.CellCount;i++){var c=next.GetCell(i);double t=c.Owner.TemperatureC(c.Index);Check(t>=-1e-5&&t<=20.00001,"stable cooling");}
 settings.ExteriorFilmCoefficientWPerM2K=0;w.Thermal.PrepareExchange();next=w.Thermal.Exchange;Check(Enumerable.Range(0,next.CellCount).All(i=>next.ExteriorConductance(i)==0),"exterior zero live");
 settings.ExteriorFilmCoefficientWPerM2K=25;w.Thermal.PrepareExchange();Check(Enumerable.Range(0,next.CellCount).Any(i=>w.Thermal.Exchange.ExteriorConductance(i)>0),"exterior restored live");
 settings.MaximumFractionPerMinute=double.NaN;settings.ExteriorFilmCoefficientWPerM2K=-1;Check(m.MaximumExchangeFractionPerMinute==.1&&m.ExteriorFilmCoefficientWPerM2K==0,"invalid input sanitized");
 settings.MaximumFractionPerMinute=oldF;settings.ExteriorFilmCoefficientWPerM2K=oldH;
 ContentThermalIntegrationTests.Run(ThermalMaterials.Load());ExteriorThermalTests.Run(ThermalMaterials.Load());PersistentThermalTests.Run(ThermalMaterials.Load());
 File.WriteAllText(output,"PASS live 10->50% limit, one-time rebuild, unchanged energy/save, no repeated rebuild, 120-minute stable cooling at 50%, exterior 5->0->25 live, invalid value sanitation, content/exterior/persistent regression. Defaults restored; no play save changed.");
 }catch(Exception ex){File.WriteAllText(output,ex.ToString());Debug.LogException(ex);settings.MaximumFractionPerMinute=oldF;settings.ExteriorFilmCoefficientWPerM2K=oldH;ThermalExchange.ReleaseAllBuffers();EditorApplication.Exit(1);return;}
 settings.MaximumFractionPerMinute=oldF;settings.ExteriorFilmCoefficientWPerM2K=oldH;ThermalExchange.ReleaseAllBuffers();EditorApplication.Exit(0);
 }
}
