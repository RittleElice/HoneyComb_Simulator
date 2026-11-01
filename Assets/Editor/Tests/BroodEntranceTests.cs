using System;
using System.Linq;
public static class BroodEntranceTests {
 static void Check(bool b,string why){if(!b)throw new Exception("Entrance: "+why);}
 static void Near(double a,double b,double tolerance,string why){Check(Math.Abs(a-b)<tolerance,why+": "+a+" / "+b);}
 public static void Run(ThermalMaterials m){
  var world=new GameWorld(new WeatherSimulator(42,WeatherAssetLoader.LoadBaseline()));world.PrepareThermal(m);world.Thermal.PrepareExchange();
  var exchange=world.Thermal.Exchange;int tested=0;
  foreach(var box in ThermalWorld.Items(world).Where(i=>i.Kind==ItemKind.BroodBox)){
   var grid=box.SpatialGrid;var l=grid.Layout;double area=0;
   for(int y=0;y<HiveGridLayout.SizeY;y++){
    bool opening=y>0&&y<HiveGridLayout.SizeY-1;int index=l.Index(0,y,1);var cell=grid.GetCell(index);
    Check(l.IsEntranceCell(0,y,1)==opening,"entrance classification");
    Check((cell.Material==HiveMaterial.Air)==opening,"only selected front cells open");
    Check(grid.GetCell(0,y,0).Material==HiveMaterial.Wood,"floor remains solid");
    Check(grid.GetCell(HiveGridLayout.SizeX-1,y,1).Material==HiveMaterial.Wood,"rear remains solid");
    if(!opening)continue;
    Check(cell.SourceCell==null&&box.EquipmentGrid.GetCell(0,y,1)==null,"entrance not wood owned");
    Check(grid.GetCell(1,y,1).Material==HiveMaterial.Air,"path connects to interior");
    Check(grid.Edges().Any(e=>e.A==index&&e.B==l.Index(1,y,1)&&e.AllowsAir&&e.AllowsWorkers),"permeable inner face");
    int global=Enumerable.Range(0,exchange.CellCount).Single(i=>ReferenceEquals(exchange.GetCell(i).Owner,box.ThermalState)&&exchange.GetCell(i).Index==index);
    var exterior=Enumerable.Range(0,exchange.BoundaryCount).Select(exchange.GetBoundary).Where(b=>b.Cell==global&&b.Direction==0).ToArray();
    Check(exterior.Length==1,"one exterior face");Near(exterior[0].AreaM2,(l.Y[y+1]-l.Y[y])*10e-6,1e-12,"Y width by 10 face");Near(exterior[0].HalfM,.01,1e-12,"20mm wall tunnel half depth");
    Near(box.ThermalState.Conductivity(index,0),m[ThermalMaterialKind.Air].Conductivity,1e-7,"air conductivity");
    double c=box.ThermalState.HeatCapacityJPerK[index];Near(c,(l.Y[y+1]-l.Y[y])*20*10e-9*m[ThermalMaterialKind.Air].Density*m[ThermalMaterialKind.Air].SpecificHeat,1e-6,"air capacity");
    Check(exchange.ExteriorConductance(global)>0,"ambient exchange active");area+=exterior[0].AreaM2;tested++;
   }
   Near(area,.0035,1e-12,"350 by 10 total opening");
  }
  foreach(var box in ThermalWorld.Items(world).Where(i=>i.Kind==ItemKind.BroodBox))for(int x=0;x<HiveGridLayout.SizeX;x++)Check(box.SpatialGrid.GetCell(x,0,1).Material==HiveMaterial.Wood,"incorrect Y=0 opening closed");
  Check(tested==80,"two default brood boxes each forty entrance cells");
  foreach(var box in ThermalWorld.Items(world).Where(i=>i.Kind==ItemKind.Super))Check(box.SpatialGrid.GetCell(3,0,1).Material==HiveMaterial.Wood,"super wall closed");
  foreach(var cell in Enumerable.Range(0,exchange.CellCount).Select(exchange.GetCell))cell.Owner.RestoreEnergy(cell.Index,cell.Owner.HeatCapacityJPerK[cell.Index]*20);
  exchange.Step(60,-10);
  var brood=ThermalWorld.Items(world).First(i=>i.Kind==ItemKind.BroodBox);Check(brood.ThermalState.TemperatureC(brood.SpatialGrid.Layout.Index(0,3,1))<20,"opening responds to outside cold");
  exchange.Dispose();UnityEngine.Debug.Log("BROOD ENTRANCE PASS: 350x10mm at X=0, 80 air cells, exterior faces, interior paths, cold response, closed rear/floor/super.");
 }
}
