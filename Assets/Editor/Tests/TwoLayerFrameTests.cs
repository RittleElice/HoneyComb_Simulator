using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
public static class TwoLayerFrameTests
{
 static int checks;
 static void Check(bool ok,string why){checks++;if(!ok)throw new Exception(why);}
 static void Near(double a,double b,string why){Check(Math.Abs(a-b)<2e-6*Math.Max(1,Math.Abs(b)),why+": "+a+" / "+b);}
 [MenuItem("Tools/HoneyComb/Run Two Layer Frame Tests")]
 public static void RunBatch()
 {
  try {
   var m=ThermalMaterials.Load();
   foreach(byte build in new byte[]{0,50,100}) {
    var f=new FrameData();f.Front.SetAllConstruction(build);f.Back.SetAllConstruction(build);
    var item=new ItemData(900,"two layers",ItemKind.Frame,f);var t=item.ThermalState;var g=item.EquipmentGrid;t.Refresh(m,20);
    Check(g.SizeY==2 && g.CellCount==280,"two physical layers");Near(g.Y[2],25,"25mm overall thickness");
    double expected=.0399+.1008*build/100;Near(t.WaxMassKg.Sum(v=>(double)v),expected,"foundation + built wax conserved");
    Near(t.WaxMassKg.Where((v,i)=>i/14%2==0).Sum(v=>(double)v),expected/2,"half on each face");
    double woodVolume=(440*220-420*200)*25e-9;double totalVolume=440*220*25e-9;
    double expectedAir=totalVolume-woodVolume-expected/m[ThermalMaterialKind.Wax].Density;
    Near(t.AirVolumeM3.Sum(v=>(double)v),expectedAir,"air volume conserved");
    double expectedC=woodVolume*m[ThermalMaterialKind.Wood].Density*m[ThermalMaterialKind.Wood].SpecificHeat+expected*m[ThermalMaterialKind.Wax].SpecificHeat+expectedAir*m[ThermalMaterialKind.Air].Density*m[ThermalMaterialKind.Air].SpecificHeat;
    Near(t.HeatCapacityJPerK.Sum(v=>(double)v),expectedC,"total heat capacity conserved");
    int index=g.Index(1,0,1);float faceWax=t.WaxMassKg[index]-(float)(35*25*.25e-9*m[ThermalMaterialKind.Wax].Density);
    ThermalMath.Mix(35*25*10e-9f,0,m[ThermalMaterialKind.Wood],Math.Max(0,faceWax),m,out _,out _,out float faceK);
    ThermalMath.Mix(35*25*5e-9f,0,m[ThermalMaterialKind.Wood],35*25*.5e-9f*m[ThermalMaterialKind.Wax].Density,m,out _,out _,out float centreK);
    Near(.025/t.Conductivity(index,1),.02/faceK+.005/centreK,"full depth series resistance conserved");
    Check(t.EffectiveConductivityWPerMK[index]>=t.Conductivity(index,1),"parallel vs series conductivity");
   }
   var bare=new ItemData(901,"without contents object",ItemKind.Frame);bare.ThermalState.Refresh(m,20);Near(bare.ThermalState.WaxMassKg.Sum(v=>(double)v),.0399,"foundation without FrameData");
   foreach(var kind in new[]{ItemKind.DividerBoard,ItemKind.InsulationBoard}) {
    var item=new ItemData(902,"panel",kind);var g=item.EquipmentGrid;double solid=0;
    for(int z=0;z<g.SizeZ;z++)for(int y=0;y<g.SizeY;y++)for(int x=0;x<g.SizeX;x++)solid+=g.GetCell(x,y,z).VolumeMm3*g.SolidFraction(y);
    Near(solid,440*220*(kind==ItemKind.DividerBoard?5:25),"panel solid dimensions preserved");
   }
   HiveSpatialGridTests.RunAll();CombGeometryTests.RunAll();CombThermalTests.RunAll();ContentThermalIntegrationTests.Run(m);ExteriorThermalTests.Run(m);PersistentThermalTests.Run(m);WorldSaveTests.Run();
   var world=new GameWorld(new WeatherSimulator(42,WeatherAssetLoader.LoadBaseline()));world.PrepareThermal(m);world.Thermal.PrepareExchange();
   var snapshot=new WorldSaveSnapshot{World=world,UtcTicks=DateTime.UtcNow.Ticks,TargetMinute=world.CurrentGameMinute};
   var bytes=WorldSaveCodec.Encode(snapshot);Check(BitConverter.ToInt32(bytes,0)==4,"new schema4");Check(bytes.SequenceEqual(WorldSaveCodec.Encode(WorldSaveCodec.Decode(bytes))),"new game exact save roundtrip");
   for(byte old=1;old<4;old++){var copy=(byte[])bytes.Clone();copy[0]=old;bool rejected=false;try{WorldSaveCodec.Decode(copy);}catch(InvalidDataException){rejected=true;}Check(rejected,"old save rejected without migration");}
   Directory.CreateDirectory("outputs/two-layer-frame");File.WriteAllText("outputs/two-layer-frame/tests.txt","PASS "+checks+" conservation/schema checks; spatial, assembly, comb geometry, comb contents, thermal contents, exterior, persistent buffers, save suites passed.");
   ThermalExchange.ReleaseAllBuffers();Debug.Log("TWO LAYER PASS "+checks);if(Application.isBatchMode)EditorApplication.Exit(0);
  }catch(Exception e){Debug.LogException(e);ThermalExchange.ReleaseAllBuffers();if(Application.isBatchMode)EditorApplication.Exit(1);else throw;}
 }
}
