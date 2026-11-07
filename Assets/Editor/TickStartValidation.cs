using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Reflection;
using UnityEngine;
public static class TickStartValidation
{
    static readonly BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
    static void Tick(GameWorld w)
    {
        var scheduler=GameTimeRuntime.CreateWorldScheduler(w);
        scheduler.RunToAsync(w.CurrentGameMinute+1,()=>Task.CompletedTask,()=>Task.CompletedTask,CancellationToken.None).GetAwaiter().GetResult();
    }
    static GameWorld World(bool heating,out ItemData panel,out ItemData frame)
    {
        var w=new GameWorld(new WeatherSimulator(42,WeatherAssetLoader.LoadBaseline()));
        var box=w.Apiary.GetItemSlot(0,0).Item;
        panel=new ItemData(50001,"heater",ItemKind.HeatingPanel);
        panel.GetContent<HeatingPanelData>().TryConfigure(heating,100);
        var data=new FrameData();
        data.Front.SetAllConstruction(100);
        data.Front.TryAddNectar(0,0,0,40,100);
        frame=new ItemData(50002,"wet",ItemKind.Frame,data);
        if(!box.InteriorSlots.GetSlot(0).TryInsert(panel)||!box.InteriorSlots.GetSlot(1).TryInsert(frame))throw new Exception("Fixture insertion failed");
        w.PrepareThermal(ThermalMaterials.Load());w.Thermal.PrepareExchange();
        return w;
    }
    static void EvaporationIsolation(GameWorld w)
    {
        var owners=ThermalWorld.Items(w).Where(i=>i.EquipmentGrid!=null).Distinct().ToDictionary(i=>i.ThermalState);
        var m=new HiveClimate.Model(w.Thermal.Exchange,owners,ThermalMaterials.Load());
        var type=m.GetType();
        double[] Values(string name)=>(double[])type.GetField(name,Hidden).GetValue(m);
        m.Audit();
        type.GetMethod("BuildContacts",Hidden).Invoke(m,null);
        var contacts=(HiveClimateKernels.Contact[])type.GetField("contacts",Hidden).GetValue(m);
        var mass=(double[])Values("mass").Clone();
        var delta=new double[mass.Length];var dh=new double[mass.Length/5];
        HiveClimateKernels.PairFlux(contacts,Values("slope"),Values("pressure"),mass,Values("limit"),delta,dh,Values("temperature"));
        m.Minute(1);
        var end=Values("mass");
        bool evaporated=false;
        for(int p=0;p<mass.Length;p+=5){
            double evaporation=mass[p+4]-end[p+4];evaporated|=evaporation>0;
            for(int s=0;s<4;s++)
                if(Math.Abs(end[p+s]-(mass[p+s]+delta[p+s]+(s==0?evaporation:0)))>1e-16)
                    throw new Exception("Transport used post-evaporation state");
        }
        if(!evaporated)throw new Exception("No evaporation to test");
    }
    public static void Run()
    {
        var a=World(true,out var heater,out var wet);var b=World(false,out var cold,out var control);
        try{
            EvaporationIsolation(a);
            for(int minute=1;minute<=2;minute++){
                Tick(a);Tick(b);
                var x=a.Thermal.Exchange;var y=b.Thermal.Exchange;
                double panelDiff=0,otherDiff=0;
                for(int i=0;i<x.CellCount;i++){
                    var ax=x.GetCell(i);var by=y.GetCell(i);
                    double diff=Math.Abs(ax.EnergyJ/ax.HeatCapacityJPerK-by.EnergyJ/by.HeatCapacityJPerK);
                    if(ax.Owner==heater.ThermalState)panelDiff=Math.Max(diff,panelDiff);
                    else otherDiff=Math.Max(diff,otherDiff);
                }
                if(panelDiff<=0 || (minute==1?otherDiff!=0:otherDiff<=0))throw new Exception("Heater timing failed "+minute+" "+panelDiff+" "+otherDiff);
                Debug.Log("Tick-start heater test "+minute+" panel="+panelDiff+" other="+otherDiff);
            }
        }finally{a.Thermal.Dispose();b.Thermal.Dispose();}
        PyramidMoistureValidation.Run();
    }
}
