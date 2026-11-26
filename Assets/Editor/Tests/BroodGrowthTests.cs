using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
public static class BroodGrowthTests
{
 static int checks;
 static void Check(bool ok,string message){checks++;if(!ok)throw new Exception(message);}
 static UIVertex[] Vertices(FrameCellMesh mesh,Sprite sprite){using(var vh=new VertexHelper()){typeof(FrameCellMesh).GetMethod("PopulateTexture",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(mesh,new object[]{vh,sprite.texture});var v=new UIVertex[vh.currentVertCount];for(int i=0;i<v.Length;i++)vh.PopulateUIVertex(ref v[i],i);return v;}}
 static Vector3 Center=new Vector3(Mathf.Sqrt(3)*20,40,0);
 static float Radius(UIVertex[] vertices)=>vertices.Max(v=>(v.position-Center).magnitude);
 public static void Run()
 {
  const string output="outputs/cell-growth";Directory.CreateDirectory(output);GameWorld world=null,loaded=null;GameObject go=null;
  try{
   world=new GameWorld(new WeatherSimulator(WeatherSimulator.DefaultSeed,WeatherAssetLoader.LoadBaseline()));world.BeeCycle.SeedDemo();var runtime=world.BeeCycle;var bee=runtime.Bees.First(b=>b.stage==CycleStage.Larva);
   var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Item/InteriorSlot/Frame/PF_Frame_Hand.prefab");var art=(FrameCellArtwork)typeof(FrameCellPresenter).GetField("artwork",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(prefab.GetComponent<FrameCellPresenter>());
   go=new GameObject("Brood growth mesh",typeof(RectTransform));var mesh=go.AddComponent<FrameCellMesh>();var face=new FrameSurface(1,1);face.SetAllConstruction(100);face.TrySetBrood(0,0,bee.id);
   var firstColor=Color.white;var lastColor=new Color(.4f,.6f,.8f,1);
   Action<float> configure=max=>{mesh.ConfigureArt(1,0,1,40,Vector2.zero,FrameCellMesh.BuildJunctions(1,1,40,Vector2.zero),0,1,.3f,art,50,1,.3f,.3f,1,1,.3f,max,firstColor,lastColor);mesh.SetSurface(face,runtime);};
   bee.development=runtime.Settings.larvaEnd;configure(1);var full=Vertices(mesh,art.larva);float fullRadius=Radius(full);
   foreach(float t in new[]{0f,.5f,1f}){
    bee.development=runtime.Settings.eggEnd+t*(runtime.Settings.larvaEnd-runtime.Settings.eggEnd);int revision=face.Revision;int refresh=mesh.RefreshCount;mesh.SetSurface(face,runtime);var current=Vertices(mesh,art.larva);float factor=.3f+.7f*t;
    Check(Mathf.Abs(Radius(current)/fullRadius-factor)<.001,"stage-local larval growth "+t);Check(face.Revision==revision,"display does not mutate cell data");
    Check(mesh.RefreshCount>refresh,"development-only change refreshes");
    for(int i=0;i<current.Length;i++)Check(Vector3.Distance((current[i].position-Center)/factor,full[i].position-Center)<.001,"rotation stays fixed during development");
   }
   configure(1.4f);Check(Mathf.Abs(Radius(Vertices(mesh,art.larva))/fullRadius-1.4f)<.001,"larval maximum adjustable");configure(1);
   var positive=Vertices(mesh,art.larva);mesh.transform.localScale=new Vector3(-1,1,1);var negative=Vertices(mesh,art.larva);
   for(int i=0;i<positive.Length;i++)Check(Vector3.Distance(positive[i].position-Center,mesh.transform.TransformPoint(negative[i].position)-mesh.transform.TransformPoint(Center))<.001,"rotated larva never mirrors");mesh.transform.localScale=Vector3.one;
   var other=runtime.Bees.First(b=>b.stage==CycleStage.Larva&&b.id!=bee.id);other.development=bee.development;face.TryClearContent(0,0);face.TrySetBrood(0,0,other.id);mesh.SetSurface(face,runtime);var different=Vertices(mesh,art.larva);Check(Vector3.Distance(different[0].position,positive[0].position)>.001,"new brood receives different rotation");face.TryClearContent(0,0);face.TrySetBrood(0,0,bee.id);mesh.SetSurface(face,runtime);
   var saved=WorldSaveCodec.Encode(new WorldSaveSnapshot{World=world,UtcTicks=new DateTime(2026,11,26,0,0,0,DateTimeKind.Utc).Ticks});loaded=WorldSaveCodec.Decode(saved).World;mesh.SetSurface(face,loaded.BeeCycle);var restored=Vertices(mesh,art.larva);for(int i=0;i<restored.Length;i++)Check(Vector3.Distance(restored[i].position,positive[i].position)<.001,"save restores exact rotation and growth");
   bee.stage=CycleStage.Pupa;
   foreach(float t in new[]{0f,.5f,1f}){bee.development=runtime.Settings.larvaEnd+t*(runtime.Settings.adultStart-runtime.Settings.larvaEnd);mesh.SetSurface(face,runtime);var cap=Vertices(mesh,art.cappedPupa);Check(cap.Length>0&&Vertices(mesh,art.larva).Length==0,"pupa replaces larva");Color actual=cap[0].color,expected=Color.Lerp(firstColor,lastColor,t);for(int k=0;k<3;k++)Check(Mathf.Abs(actual[k]-expected[k])<.005,"pupa color at "+t);}
   int unchanged=mesh.RefreshCount;mesh.SetSurface(face,runtime);Check(mesh.RefreshCount==unchanged,"unchanged development skips refresh");bee.stage=CycleStage.Adult;mesh.SetSurface(face,runtime);Check(Vertices(mesh,art.cappedPupa).Length==0,"adult clears brood artwork");
   File.WriteAllText(output+"/brood-result.txt","PASS "+checks+" larval growth, fixed rotation, mirrored layout, saved identity and pupa color progression checks.");
   UnityEngine.Object.DestroyImmediate(go);go=null;world.Thermal?.Dispose();world=null;loaded.Thermal?.Dispose();loaded=null;
   CellMaximumSizeTests.Run();
  }catch(Exception e){File.WriteAllText(output+"/brood-result.txt","FAIL "+e);Debug.LogException(e);EditorApplication.Exit(1);}
  finally{if(go)UnityEngine.Object.DestroyImmediate(go);world?.Thermal?.Dispose();loaded?.Thermal?.Dispose();}
 }
}
