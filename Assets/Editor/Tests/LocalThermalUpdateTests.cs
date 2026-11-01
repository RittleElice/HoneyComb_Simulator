using System;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
public static class LocalThermalUpdateTests
{
 static int checks;
 static void Check(bool ok,string why){checks++;if(!ok)throw new Exception("Local thermal: "+why);}
 static void Equal(double a,double b,string why){Check(BitConverter.DoubleToInt64Bits(a)==BitConverter.DoubleToInt64Bits(b),why+": "+a.ToString("R")+" != "+b.ToString("R"));}
 // Independent full rebuild of the original formula and summation order.
 static void FullReference(ThermalExchange e)
 {
  int n=e.CellCount,m=e.FaceCount;var raw=new double[m];var sums=new double[n];var ext=new double[n];var scales=new double[n];
  for(int j=0;j<m;j++){var f=e.GetFace(j);var a=e.GetCell(f.A);var b=e.GetCell(f.B);double g=ThermalExchange.Conductance(f.AreaM2,f.HalfAM,a.Owner.Conductivity(a.Index,f.Axis),f.HalfBM,b.Owner.Conductivity(b.Index,f.Axis));raw[j]=g;sums[f.A]+=g;sums[f.B]+=g;}
  for(int j=0;j<e.BoundaryCount;j++){var b=e.GetBoundary(j);var c=e.GetCell(b.Cell);double g=b.AreaM2/(b.HalfM/c.Owner.Conductivity(c.Index,b.Direction/2)+1d/5);ext[b.Cell]+=g;sums[b.Cell]+=g;}
  double rawMinimum=double.PositiveInfinity;
  for(int i=0;i<n;i++){var c=e.GetCell(i);double cap=c.Owner.HeatCapacityJPerK[c.Index];double limit=sums[i]>0?cap/sums[i]:double.PositiveInfinity;rawMinimum=Math.Min(rawMinimum,limit);scales[i]=Math.Min(1,.1*(limit/60));}
  Equal(e.RawMinimumStepSeconds,rawMinimum,"raw global minimum");Array.Clear(sums,0,n);
  for(int j=0;j<m;j++){var f=e.GetFace(j);double g=raw[j]*Math.Min(scales[f.A],scales[f.B]);sums[f.A]+=g;sums[f.B]+=g;}
  double minimum=double.PositiveInfinity;int limiting=-1;
  for(int i=0;i<n;i++){double exterior=ext[i]*scales[i];sums[i]+=exterior;Equal(e.SumConductance(i),sums[i],"sum G "+i);Equal(e.ExteriorConductance(i),exterior,"exterior "+i);Equal(e.RawExteriorConductance(i),ext[i],"raw exterior "+i);var c=e.GetCell(i);double cap=c.Owner.HeatCapacityJPerK[c.Index];double limit=sums[i]>0?cap/sums[i]:double.PositiveInfinity;if(limit<minimum){minimum=limit;limiting=i;}}
  Equal(e.MinimumStepSeconds,minimum,"minimum");Check(e.LimitingCell==limiting,"minimum tie order");
 }
 [MenuItem("Tools/HoneyComb/Run Local Thermal Update Tests")]
 public static void RunBatch()
 {
  try{
   var materials=ThermalMaterials.Load();var w=new GameWorld(WeatherAssetLoader.Create(42));var box=w.Apiary.GetItemSlot(0,0).Item;var other=ThermalWorld.Items(w).First(i=>i.Kind==ItemKind.BroodBox&&!ReferenceEquals(i,box));
   var frame=new ItemData(99881,"local test",ItemKind.Frame,new FrameData());var f=frame.GetContent<FrameData>();f.Front.SetAllConstruction(100);f.Back.SetAllConstruction(100);Check(box.InteriorSlots.GetSlot(0).TryInsert(frame),"initial place");
   w.PrepareThermal(materials);w.Thermal.PrepareExchange();var e=w.Thermal.Exchange;FullReference(e);e.Step(60,20);int allocations=e.BufferAllocations;
   f.Front.TryAddNectar(0,0,1,1,1);w.Thermal.Refresh();w.Thermal.PrepareExchange();
   Check(e.LastChangedCells==1,"one changed content region");Check(e.LastRawFacesUpdated<=6 && e.LastEffectiveFacesUpdated<=42,"local connection work bounded");FullReference(e);
   f.Front.TryAddNectar(1,0,1,1,1);frame.ThermalState.Refresh(materials,20);f.Back.TryAddPollen(2,2,1);frame.ThermalState.Refresh(materials,20);w.Thermal.PrepareExchange();Check(e.LastChangedCells==2,"multiple refreshes before consumer retain both revisions");FullReference(e);
   Check(box.InteriorSlots.GetSlot(0).TryMoveTo(box.InteriorSlots.GetSlot(3)),"move within box");w.Thermal.Refresh();w.Thermal.PrepareExchange();Check(ReferenceEquals(e,w.Thermal.Exchange)&&w.Thermal.TopologyBuilds==1,"same network preserved");Check(e.LastChangedCells==560,"old and new 280-cell frame sites only");Check(e.LastRawFacesUpdated<4000,"local move connections bounded");FullReference(e);e.Step(60,20);Check(e.BufferAllocations==allocations,"native buffers retained");
   Check(box.InteriorSlots.GetSlot(3).TryMoveTo(other.InteriorSlots.GetSlot(0)),"cross-box move");w.Thermal.Refresh();w.Thermal.PrepareExchange();Check(ReferenceEquals(e,w.Thermal.Exchange),"cross-box network retained");FullReference(e);
   // Two changes before topology refresh, including a move back to the original site.
   Check(other.InteriorSlots.GetSlot(0).TryMoveTo(box.InteriorSlots.GetSlot(3)),"return");Check(box.InteriorSlots.GetSlot(3).TryMoveTo(box.InteriorSlots.GetSlot(0)),"return original");w.Thermal.Refresh();w.Thermal.PrepareExchange();FullReference(e);
   int builds=e.CoefficientBuilds;frame.ThermalState.MarkAllDirty();w.Thermal.Refresh();w.Thermal.PrepareExchange();Check(e.CoefficientBuilds==builds,"unchanged material values skip coefficient update");
   e.Dispose();e.Step(60,20);Check(e.BufferAllocations==allocations+1,"buffer recreation after partial changes");
   Check(w.Hand.Slot.TryInsert(box.InteriorSlots.GetSlot(0).Take()),"detach");w.Thermal.Refresh();w.Thermal.PrepareExchange();Check(!ReferenceEquals(e,w.Thermal.Exchange),"membership resize rebuild fallback");FullReference(w.Thermal.Exchange);
   w.Thermal.Exchange.Dispose();System.IO.Directory.CreateDirectory("outputs/local-thermal-update");System.IO.File.WriteAllText("outputs/local-thermal-update/tests.txt","PASS "+checks+" local update/full-reference comparisons");Debug.Log("LOCAL THERMAL TESTS PASS "+checks);
   TwoLayerFrameTests.RunBatch();
  }catch(Exception ex){ThermalExchange.ReleaseAllBuffers();Debug.LogException(ex);if(Application.isBatchMode)EditorApplication.Exit(1);else throw;}
 }
}
