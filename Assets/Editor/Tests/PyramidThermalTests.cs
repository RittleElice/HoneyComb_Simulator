using System;
using System.Linq;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class PyramidThermalTests
{
    static int checks;
    static void Check(bool ok,string label){checks++;if(!ok)throw new Exception("Pyramid: "+label);}
    static void Near(double a,double b,double tolerance,string label)=>Check(Math.Abs(a-b)<=tolerance,label+": "+a+" != "+b);
    static double[] Energy(ThermalExchange e)=>Enumerable.Range(0,e.CellCount).Select(i=>(double)e.GetCell(i).EnergyJ).ToArray();
    static void SetTemperature(ThermalExchange e,Func<int,double> value){e.Dispose();for(int i=0;i<e.CellCount;i++){var c=e.GetCell(i);for(int j=0;j<c.MemberCount;j++){int member=c.MemberIndex(j);c.Owner.RestoreEnergy(member,(float)(c.Owner.HeatCapacityJPerK[member]*value(i)));}}}
    public static void Analytic(ThermalMaterials m)
    {
        var items=Enumerable.Range(0,7).Select(i=>new ItemData(99000+i,"analytic",ItemKind.Lid)).ToArray();foreach(var x in items)x.ThermalState.Refresh(m,20);
        var cells=items.Select(x=>new ThermalExchange.Cell(x.ThermalState,0,"test")).ToArray();
        var faces=Enumerable.Range(1,6).Select(i=>new ThermalExchange.Face(0,i,cells[0].Owner.FaceAreaM2(0,(i-1)/2,false),.000001,.000001,(i-1)/2)).ToArray();
        using(var e=new ThermalExchange(cells,faces,null,m.Transfers)){
            e.RefreshCoefficients();SetTemperature(e,i=>i==0?25:5);double before=Energy(e).Sum();
            double center=cells[0].EnergyJ,capacity=cells[0].HeatCapacityJPerK,q=0;
            foreach(var f in faces){double ca=capacity/6,cb=cells[f.B].HeatCapacityJPerK/6,cr=ca*cb/(ca+cb);double g=f.AreaM2*m.Transfers.Between(cells[0].Owner.ExchangeMaterial(0),cells[f.B].Owner.ExchangeMaterial(0));q+=cr*(1-Math.Exp(-60*g/cr))*(cells[f.B].EnergyJ/cells[f.B].HeatCapacityJPerK-center/capacity);}
            e.Step();Near(cells[0].EnergyJ,(float)(center+q),1e-5,"six independent pyramids");Near(Energy(e).Sum(),before,Math.Abs(before)*2e-7,"closed energy");
            for(int n=0;n<100;n++)e.Step();foreach(var c in cells)Check(c.EnergyJ/c.HeatCapacityJPerK>=5-1e-4&&c.EnergyJ/c.HeatCapacityJPerK<=25+1e-4,"strong links bounded");
            e.Step(.1);e.Step(60);Check(e.CoefficientBuilds==3,"timestep refresh");
            int builds=e.CoefficientBuilds;e.Step();Check(e.CoefficientBuilds==builds,"steady reuse");
        }
        using(var empty=new ThermalExchange(Array.Empty<ThermalExchange.Cell>(),Array.Empty<ThermalExchange.Face>()))empty.Step();
    }
    public static void Exterior(ThermalMaterials m)
    {
        var item=new ItemData(99900,"surface",ItemKind.Lid);var owner=item.ThermalState;owner.Refresh(m,25);double area=owner.FaceAreaM2(0,0,false),half=.01;var air=m.Transfers;
        var cells=new[]{new ThermalExchange.Cell(owner,0,"outside")};var bounds=new[]{new ThermalExchange.Boundary(0,0,area,half)};
        using(var e=new ThermalExchange(cells,Array.Empty<ThermalExchange.Face>(),bounds,air)){
            e.DiagnosticsEnabled=true;e.RefreshCoefficients();double capacity=cells[0].HeatCapacityJPerK,cs=capacity/6,g=area*air.Outside(owner.ExchangeMaterial(0));double factor=cs*(1-Math.Exp(-60*g/cs));
            Near(60*e.ExteriorConductance(0),factor,Math.Max(1e-9,Math.Abs(factor)*2e-6),"outside pair coefficient");double before=cells[0].EnergyJ,q=factor*(5-before/capacity);e.Step(60,5);Near(cells[0].EnergyJ,(float)(before+q),0,"ambient exponential");Near(e.ExternalEnergyJ,q,Math.Max(1e-8,Math.Abs(q)*2e-6),"signed external heat");
        }
        using(var e=new ThermalExchange(cells,Array.Empty<ThermalExchange.Face>(),bounds,ThermalTransferCoefficients.Sealed)){double before=cells[0].EnergyJ;e.Step(60,-20);Near(cells[0].EnergyJ,before,0,"sealed boundary");}
        var w=new GameWorld(WeatherAssetLoader.Create(42));w.PrepareThermal(m);w.Thermal.PrepareExchange();int beforeCount=w.Thermal.Exchange.BoundaryCount;var lid=new ItemSlot();lid.TryInsert(new ItemData(99901,"lid",ItemKind.Lid));Check(w.Apiary.GetItemSlot(0,0).TryCombineFrom(lid),"attach lid");w.Thermal.Refresh();w.Thermal.PrepareExchange();Check(w.Thermal.Exchange.BoundaryCount!=beforeCount,"boundary topology updated");CheckReference(w.Thermal.Exchange,m.Transfers);w.Thermal.Dispose();
    }
    public static void CheckReference(ThermalExchange e,ThermalTransferCoefficients air)
    {
        var cells=Enumerable.Range(0,e.CellCount).Select(e.GetCell).ToArray();var faces=Enumerable.Range(0,e.FaceCount).Select(e.GetFace).ToArray();var bounds=Enumerable.Range(0,e.BoundaryCount).Select(e.GetBoundary).ToArray();
        using(var full=new ThermalExchange(cells,faces,bounds,air)){
            full.RefreshCoefficients();for(int i=0;i<e.CellCount;i++){Near(e.SumConductance(i),full.SumConductance(i),1e-12,"local vs full coefficient");Near(e.ExteriorConductance(i),full.ExteriorConductance(i),1e-12,"local vs full exterior");Check(60*e.SumConductance(i)/e.GetCell(i).HeatCapacityJPerK<=1+1e-6,"bounded coefficient sum");}
        }
    }
    private static void CheckSlotGrouping(ThermalExchange e,ItemData box,int slot,bool expected)
    {
        bool air=false,wood=false;
        for(int i=0;i<e.CellCount;i++)
        {
            var c=e.GetCell(i);
            if(!ReferenceEquals(c.Owner,box.ThermalState) || !c.IsSlotGroup)continue;
            box.SpatialGrid.Layout.Coordinates(c.Index,out _,out int y,out _);
            if((y-1)/HiveGridLayout.SlotDepthCells!=slot)continue;
            var material=c.Owner.ExchangeMaterial(c.Index);
            for(int k=1;k<c.MemberCount;k++)
                Check(material==c.Owner.ExchangeMaterial(c.MemberIndex(k)),"group contains one material");
            air|=material==HiveMaterial.Air;wood|=material==HiveMaterial.Wood;
        }
        Check(air==expected && wood==expected,"empty slot groups air and wood; occupied slot splits both");
    }
    public static void Local(ThermalMaterials m)
    {
        var w=new GameWorld(WeatherAssetLoader.Create(42));var box=w.Apiary.GetItemSlot(0,0).Item;w.PrepareThermal(m);w.Thermal.PrepareExchange();var e=w.Thermal.Exchange;
        CheckSlotGrouping(e,box,0,true);
        CheckReference(e,m.Transfers);
        SetTemperature(e,i=>i%2==0?15:25);e.Step(60,5);Check(Enumerable.Range(0,e.CellCount).All(i=>{var c=e.GetCell(i);double t=c.EnergyJ/c.HeatCapacityJPerK;return t>=5-1e-4&&t<=25+1e-4;}),"grouped grid bounded");
        var frame=new ItemData(99881,"content",ItemKind.Frame,new FrameData());var fdata=frame.GetContent<FrameData>();fdata.Front.SetAllConstruction(100);fdata.Back.SetAllConstruction(100);Check(box.InteriorSlots.GetSlot(0).TryInsert(frame),"insert frame");w.Thermal.Refresh();w.Thermal.PrepareExchange();e=w.Thermal.Exchange;CheckSlotGrouping(e,box,0,false);CheckSlotGrouping(e,box,1,true);CheckReference(e,m.Transfers);e.Step(60,5);int builds=e.CoefficientBuilds;
        Check(fdata.Front.TryAddNectar(0,0,1,1,1),"nectar edit");w.Thermal.Refresh();w.Thermal.PrepareExchange();Check(e.CoefficientBuilds==builds+1&&e.LastChangedCells==1,"one content cell invalidated");Check(e.LastRawFacesUpdated<=6,"local face work");CheckReference(e,m.Transfers);
        e.Step(60,5);int stable=e.CoefficientBuilds;e.RefreshCoefficients();Check(e.CoefficientBuilds==stable && e.LastChangedCells==0,"no-input fast path");
        e.Dispose();Check(fdata.Front.TryAddNectar(1,0,1,1,1),"edit while released");w.Thermal.Refresh();e.RefreshCoefficients();CheckReference(e,m.Transfers);e.Step(60,5);
        Check(box.InteriorSlots.GetSlot(0).TryMoveTo(box.InteriorSlots.GetSlot(3)),"move frame");w.Thermal.Refresh();w.Thermal.PrepareExchange();CheckReference(w.Thermal.Exchange,m.Transfers);
        Check(w.Hand.Slot.TryInsert(box.InteriorSlots.GetSlot(3).Take()),"detach");w.Thermal.Refresh();w.Thermal.PrepareExchange();CheckSlotGrouping(w.Thermal.Exchange,box,3,true);CheckReference(w.Thermal.Exchange,m.Transfers);
        var snap=new WorldSaveSnapshot{World=w,UtcTicks=638000000000000000,TargetMinute=w.CurrentGameMinute};byte[] bytes=WorldSaveCodec.Encode(snap);var restored=WorldSaveCodec.Decode(bytes);restored.World.PrepareThermal(m);restored.World.Thermal.PrepareExchange();Check(bytes.SequenceEqual(WorldSaveCodec.Encode(restored)),"save/load exact");restored.World.Thermal.Dispose();w.Thermal.Dispose();
    }
    public static void Live()
    {
        var air=Resources.Load<ThermalMaterialAsset>("Thermal/Air");var settings=Resources.Load<PyramidTransferSettings>("Thermal/PyramidTransferSettings");double oldH=settings.AirAir,oldOutside=settings.AirOutside;float oldC=air.EffectiveVolumetricHeatCapacityJPerM3K;GameWorld w=null;
        try{w=new GameWorld(WeatherAssetLoader.Create(42));w.PrepareThermal(ThermalMaterials.Load());w.Thermal.PrepareExchange();var e=w.Thermal.Exchange;double[] before=Energy(e);settings.AirAir=oldH*2;w.Thermal.Refresh();w.Thermal.PrepareExchange();Check(!ReferenceEquals(e,w.Thermal.Exchange),"pair coefficient updates");Check(before.SequenceEqual(Energy(w.Thermal.Exchange)),"pair edit preserves energy");e=w.Thermal.Exchange;before=Energy(e);air.EffectiveVolumetricHeatCapacityJPerM3K=oldC*1.1f;ThermalMaterials.NotifyAssetsChanged();w.Thermal.Refresh();w.Thermal.PrepareExchange();Check(ReferenceEquals(e,w.Thermal.Exchange),"capacity edit keeps graph");Check(before.SequenceEqual(Energy(e)),"capacity edit preserves energy");CheckReference(e,settings.Read());
            double interior=settings.Read().Between(HiveMaterial.Air,HiveMaterial.Air);settings.AirOutside=0;w.Thermal.PrepareExchange();Check(settings.Read().Between(HiveMaterial.Air,HiveMaterial.Air)==interior,"external setting independent");for(int i=0;i<w.Thermal.Exchange.CellCount;i++){var cell=w.Thermal.Exchange.GetCell(i);if(cell.Owner.ExchangeMaterial(cell.Index)==HiveMaterial.Air)Check(w.Thermal.Exchange.ExteriorConductance(i)==0,"air exterior disabled");}}
        finally{w?.Thermal.Dispose();settings.AirAir=oldH;settings.AirOutside=oldOutside;air.EffectiveVolumetricHeatCapacityJPerM3K=oldC;ThermalMaterials.NotifyAssetsChanged();}
    }
    public static string RunAll(){checks=0;var m=ThermalMaterials.Load();Analytic(m);Exterior(m);Local(m);Live();ThermalExchange.ReleaseAllBuffers();return "PASS "+checks+" checks: analytic, exterior, slot topology, content edits, movement, save/load, live material changes";}
    [MenuItem("Tools/HoneyComb/°ËÁõ/Run Pyramid Thermal Tests")]
    public static void RunBatch(){Directory.CreateDirectory("outputs/pyramid-thermal");try{string result=RunAll();File.WriteAllText("outputs/pyramid-thermal/results.txt",result);Debug.Log(result);if(Application.isBatchMode)EditorApplication.Exit(0);}catch(Exception e){ThermalExchange.ReleaseAllBuffers();File.WriteAllText("outputs/pyramid-thermal/error.txt",e.ToString());Debug.LogException(e);if(Application.isBatchMode)EditorApplication.Exit(1);else throw;}}
}
