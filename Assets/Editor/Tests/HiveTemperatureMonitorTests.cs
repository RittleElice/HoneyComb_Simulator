using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class HiveTemperatureMonitorTests
{
    static int checks;
    static void Check(bool ok,string text) {checks++;if(!ok)throw new Exception("Temperature monitor: "+text);}
    static void Near(double a,double b,string text)=>Check(Math.Abs(a-b)<.0001,text+": "+a+" vs "+b);
    private static void CheckDisplay(HiveSpatialGrid grid,int x,int z,int expectedLayers)
    {
        ulong starts=HiveSpatialGridDebugWindow.ActiveYStarts(grid);
        int count=Enumerable.Range(0,HiveGridLayout.SizeY).Count(y=>(starts&(1UL<<y))!=0);
        Check(count==(grid.Layout.IsLid?42:expectedLayers==1?15:12),"active Y slices");
        int visible=0;
        for(int y=1;y<=4;y++)
        {
            bool xy=HiveSpatialGridDebugWindow.TrySliceCell(grid,2,z,x,y,out int xyIndex,out int span,out var xyRect);
            bool yz=HiveSpatialGridDebugWindow.TrySliceCell(grid,0,x,y,z,out int yzIndex,out _,out var yzRect);
            Check(xy==yz && xyIndex==yzIndex,"XY and YZ use the same active address");
            Check(span==expectedLayers,"active width follows occupancy");
            if(xy)
            {
                visible++;
                int first=expectedLayers==4?1:y;
                double mm=grid.Layout.Y[first+span]-grid.Layout.Y[first];
                Near(xyRect.height,mm,"XY footprint");Near(yzRect.width,mm,"YZ footprint");
            }
            Check(HiveSpatialGridDebugWindow.TrySliceCell(grid,1,y,x,z,out int xzIndex,out _,out _) && xzIndex==xyIndex,
                "XZ slice inside any member finds the active cell");
        }
        Check(visible==4/expectedLayers,"one rectangle per visible cell");
    }

    public static void RunBatch()
    {
        const string folder="outputs/thermal-monitor";Directory.CreateDirectory(folder);
        try
        {
            var m=ThermalMaterials.Load();var box=new ItemData(9980,"monitor",ItemKind.BroodBox);var grid=box.SpatialGrid;
            var monitor=new HiveTemperatureMonitor();
            Check(!monitor.Capture(grid,0,10,out _),"uninitialized world unavailable");Check(box.ExistingThermalState==null,"monitor does not initialize state");
            var state=box.ThermalState;state.Refresh(m,20);
            var energies=state.EnergyJ.ToArray();int revision=state.CacheRevision;
            Check(monitor.Capture(grid,0,10,out _),"read initialized snapshot");
            Check(energies.SequenceEqual(state.EnergyJ)&&state.CacheRevision==revision,"capture changes no energies or caches");
            Near(monitor.Minimum,20,"minimum");Near(monitor.Maximum,20,"maximum");Near(monitor.WeightedMean,20,"heat weighted mean");
            CheckDisplay(grid,0,0,4);CheckDisplay(grid,4,4,4);
            int first=grid.Layout.Index(0,1,0);
            monitor.Select(first+2*HiveGridLayout.SizeX);Check(monitor.Selected==first,"group click selects full cell");
            double groupedCapacity=0,groupedEnergy=0;
            for(int k=0;k<4;k++){int j=first+k*HiveGridLayout.SizeX;groupedCapacity+=state.HeatCapacityJPerK[j];groupedEnergy+=state.EnergyJ[j];}
            Near(monitor.Capacities[first],groupedCapacity,"grouped capacity");Near(monitor.Energies[first],groupedEnergy,"grouped energy");
            monitor.Select(0);Check(monitor.History.Count==1,"selection starts history");
            Check(!monitor.TryRate(out _,out _),"no invented first delta");
            state.RestoreEnergy(0,state.HeatCapacityJPerK[0]*21);monitor.Capture(grid,1,11,out _);
            Check(monitor.TryRate(out double rate,out long elapsed)&&elapsed==1,"one-minute rate");Near(rate,1,"one-degree rate");
            state.RestoreEnergy(0,state.HeatCapacityJPerK[0]*22);monitor.Capture(grid,1,11,out _);
            Check(monitor.History.Count==2,"same minute replaces point");monitor.TryRate(out rate,out elapsed);Near(rate,2,"same minute preserves prior baseline");
            state.RestoreEnergy(0,state.HeatCapacityJPerK[0]*25);monitor.Capture(grid,4,12,out _);
            Check(monitor.TryRate(out rate,out elapsed)&&elapsed==3,"jump interval is explicit");Near(rate,1,"gap average rate");Check(monitor.History.Count==3,"jump does not invent missing minutes");
            for(int i=5;i<250;i++)monitor.Capture(grid,i,12,out _);
            Check(monitor.History.Count==240,"bounded history");
            monitor.Capture(grid,0,12,out _);Check(monitor.Selected==-1&&monitor.History.Count==0,"rewind clears identity and history");
            monitor.Select(0);state.MarkDirty(0);Check(!monitor.Capture(grid,1,12,out _),"dirty cache not read");Check(state.IsDirty(0),"monitor does not refresh dirty cache");state.Refresh(m,20);
            var frame=new ItemData(9981,"frame",ItemKind.Frame,new FrameData());box.InteriorSlots.GetSlot(0).TryInsert(frame);
            Check(!monitor.Capture(grid,1,12,out _)&&frame.ExistingThermalState==null,"placement never initializes new owner");
            CheckDisplay(grid,0,0,1);CheckDisplay(grid,4,4,1);
            frame.ThermalState.Refresh(m,20);Check(monitor.Capture(grid,1,12,out _),"capture new topology");Check(monitor.Selected==-1&&monitor.History.Count==0,"placement clears previous sample identity");
            var source=frame.EquipmentGrid.GetCell(1,0,1);int mapped=Enumerable.Range(0,grid.CellCount).First(i=>ReferenceEquals(grid.GetCell(i).SourceCell,source));
            var f=frame.GetContent<FrameData>();f.Front.SetAllConstruction(100);f.Front.TryAddNectar(0,0,0,0,100);CheckDisplay(grid,0,0,1);CheckDisplay(grid,4,4,1);
            frame.ThermalState.Refresh(m,20);
            monitor.Capture(grid,2,12,out _);Near(monitor.ContentCapacities[mapped],.418,"monitor reads runtime contents capacity");
            Near(monitor.Capacities[mapped],frame.ThermalState.HeatCapacityJPerK[source.Index],"monitor uses solver capacity not legacy grid estimate");
            monitor.Select(mapped);monitor.ClearHistory();Check(monitor.History.Count==1,"clear keeps current point");
            Check(!monitor.Capture(grid,3,double.NaN,out _),"invalid outside rejected");
            var lid=new ItemData(9982,"lid",ItemKind.Lid);lid.ThermalState.Refresh(m,5);monitor.Capture(lid.SpatialGrid,3,0,out _);
            Check(monitor.Selected==-1&&monitor.History.Count==0,"target switch clears history");Near(monitor.WeightedMean,5,"new target values");
            CheckDisplay(lid.SpatialGrid,0,0,1);
            File.WriteAllText(folder+"/results.txt","PASS "+checks+" monitor checks: read-only sampling, runtime capacity, minute deltas, jump gaps, bounded history, placement and target resets. Interactive layout/Play/device review not performed.\n");
            Debug.Log("THERMAL MONITOR PASS "+checks);if(Application.isBatchMode)EditorApplication.Exit(0);
        }
        catch(Exception e){File.WriteAllText(folder+"/failure.txt",e.ToString());Debug.LogException(e);if(Application.isBatchMode)EditorApplication.Exit(1);}
    }
}
