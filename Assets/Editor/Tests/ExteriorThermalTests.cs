using System;
using System.Linq;
using System.Threading;

public static class ExteriorThermalTests
{
    static void Check(bool ok,string label) { if(!ok)throw new Exception("Exterior thermal: "+label); }
    static void Near(double a,double b,double eps,string label) => Check(Math.Abs(a-b)<=eps,label+": "+a+" vs "+b);
    static void Reject(Action action,string label) { bool fail=false;try{action();}catch(ArgumentException){fail=true;}catch(InvalidOperationException){fail=true;}Check(fail,label); }
    public static void Run(ThermalMaterials materials)
    {
        var item=new ItemData(9950,"boundary",ItemKind.Lid);var state=item.ThermalState;state.Refresh(materials,20);
        var cells=new[]{new ThermalExchange.Cell(state,0,"surface"),new ThermalExchange.Cell(state,1,"interior")};
        var bounds=new[]{new ThermalExchange.Boundary(0,0,.02,.01)};
        var solver=new ThermalExchange(cells,Array.Empty<ThermalExchange.Face>(),.1,bounds,5);
        double c=state.HeatCapacityJPerK[0],k=state.EffectiveConductivityWPerMK[0];
        solver.RefreshCoefficients();double expectedG=Math.Min(.02/(.01/k+1d/5),.1*c/60);
        Near(solver.ExteriorConductance(0),expectedG,1e-12,"film and half-cell resistance with cap");
        foreach(double outside in new[]{-20d,20d,40d})
        {
            state.RestoreEnergy(0,(float)(c*20));double old=state.EnergyJ[0];float isolated=state.EnergyJ[1];
            double q=60*expectedG*(outside-old/c);solver.Step(60,outside);
            Near(state.EnergyJ[0],(float)(old+q),0,"analytical heating/cooling");
            Near(solver.ExternalEnergyJ,q,1e-12,"signed boundary energy");
            Near(state.EnergyJ[0]-old-solver.ExternalEnergyJ,0,Math.Max(1e-9,Math.Abs(state.EnergyJ[0])*1.2e-7),"independent external energy balance");
            Check(state.EnergyJ[1]==isolated,"unexposed cell unchanged");
        }
        float before=state.EnergyJ[0];
        Reject(()=>solver.Step(),"missing weather rejected");Reject(()=>solver.Step(60,double.NaN),"invalid weather rejected");Reject(()=>solver.Step(60,-300),"below absolute zero rejected");
        Check(state.EnergyJ[0]==before,"invalid forcing atomic");
        var disabled=new ThermalExchange(cells,Array.Empty<ThermalExchange.Face>(),.1,bounds,0);disabled.Step(60,-100);Check(state.EnergyJ[0]==before && disabled.ExternalEnergyJ==0,"zero film disables boundary");
        var combined=new ThermalExchange(cells,new[]{new ThermalExchange.Face(0,1,10,.001,.001)},.1,bounds,1e6);combined.RefreshCoefficients();
        for(int i=0;i<2;i++)Check(60*combined.SumConductance(i)/state.HeatCapacityJPerK[i]<=.1+1e-12,"internal and external share minute budget");
        for(int n=0;n<120;n++) {combined.Step(60,30);Check(state.TemperatureC(0)<=30.00001 && state.TemperatureC(1)<=30.00001,"no ambient overshoot");}
        Reject(()=>new ThermalExchange(cells,Array.Empty<ThermalExchange.Face>(),.1,new[]{bounds[0],bounds[0]}),"duplicate exterior face");
        Reject(()=>new ThermalExchange(cells,Array.Empty<ThermalExchange.Face>(),.1,bounds,-1),"invalid film");

        var world=new GameWorld(WeatherAssetLoader.Create(42));world.PrepareThermal(materials);world.Thermal.PrepareExchange();
        var slot=world.Apiary.GetItemSlot(0,0);var box=slot.Item;var layout=box.SpatialGrid.Layout;
        Func<ThermalExchange,EquipmentThermalState,int,int> find=(e,owner,index)=>Enumerable.Range(0,e.CellCount).First(i=>ReferenceEquals(e.GetCell(i).Owner,owner)&&e.GetCell(i).Index==index);
        Func<ThermalExchange,int,int,bool> has=(e,index,dir)=>Enumerable.Range(0,e.BoundaryCount).Any(i=>e.GetBoundary(i).Cell==index&&e.GetBoundary(i).Direction==dir);
        var graph=world.Thermal.Exchange;int topLocal=layout.Index(5,5,layout.SizeZ-1);
        Check(has(graph,find(graph,box.ThermalState,topLocal),5),"open top exposed");
        int beforeCount=graph.BoundaryCount;
        var lid=new ItemData(9951,"lid",ItemKind.Lid);var temp=new ItemSlot();Check(temp.TryInsert(lid)&&slot.TryCombineFrom(temp),"attach lid");world.Thermal.Refresh();world.Thermal.PrepareExchange();graph=world.Thermal.Exchange;
        Check(!has(graph,find(graph,box.ThermalState,topLocal),5),"covered box top excluded");
        int lidCell=find(graph,lid.ThermalState,lid.EquipmentGrid.Index(5,5,0));
        Check(!has(graph,lidCell,4)&&has(graph,lidCell,5),"lid bottom internal and top exterior");
        Check(graph.BoundaryCount==beforeCount+2*(18+52),"stack interface removes both exposed faces");
        Check(slot.TrySplitTo(1,world.Hand.Slot),"detach lid");world.Thermal.Refresh();world.Thermal.PrepareExchange();graph=world.Thermal.Exchange;
        Check(has(graph,find(graph,box.ThermalState,topLocal),5)&&has(graph,find(graph,lid.ThermalState,lid.EquipmentGrid.Index(5,5,0)),4),"split reopens both surfaces");
        var frame=new ItemData(9952,"frame",ItemKind.Frame,new FrameData());Check(box.InteriorSlots.GetSlot(0).TryInsert(frame),"insert frame");world.Thermal.Refresh();world.Thermal.PrepareExchange();graph=world.Thermal.Exchange;
        Check(!Enumerable.Range(0,graph.BoundaryCount).Any(i=>ReferenceEquals(graph.GetCell(graph.GetBoundary(i).Cell).Owner,frame.ThermalState)),"enclosed frame has no exterior faces");
        for(int i=0;i<graph.CellCount;i++){var cell=graph.GetCell(i);cell.Owner.RestoreEnergy(cell.Index,cell.Owner.HeatCapacityJPerK[cell.Index]*20);}
        double outsideNow=world.Weather.Current.TemperatureCelsius;
        double expected=Enumerable.Range(0,graph.CellCount).Sum(i=>{var cell=graph.GetCell(i);return 60*graph.ExteriorConductance(i)*(outsideNow-(double)cell.Owner.EnergyJ[cell.Index]/cell.Owner.HeatCapacityJPerK[cell.Index]);});
        world.Thermal.CalculateMinuteAsync(1,CancellationToken.None).GetAwaiter().GetResult();
        Near(graph.ExternalEnergyJ,expected,1e-5,"minute-start weather drives boundary");Check(world.Weather.Minute==0,"thermal does not advance weather");
        UnityEngine.Debug.Log("EXTERIOR THERMAL TESTS PASS");
    }
}
