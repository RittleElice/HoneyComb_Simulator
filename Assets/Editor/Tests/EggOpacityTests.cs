using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
public static class EggOpacityTests
{
 static int checks;static void Check(bool ok,string msg){checks++;if(!ok)throw new Exception(msg);}
 static UIVertex[] Read(FrameCellMesh mesh,Sprite sprite){using(var vh=new VertexHelper()){typeof(FrameCellMesh).GetMethod("PopulateTexture",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(mesh,new object[]{vh,sprite.texture});var vertices=new UIVertex[vh.currentVertCount];for(int i=0;i<vertices.Length;i++)vh.PopulateUIVertex(ref vertices[i],i);return vertices;}}
 public static void Run(){Directory.CreateDirectory("outputs/egg-opacity");GameWorld world=null,loaded=null;GameObject go=null;
 try{
  world=new GameWorld(new WeatherSimulator(WeatherSimulator.DefaultSeed,WeatherAssetLoader.LoadBaseline()));world.BeeCycle.SeedDemo();var eggs=world.BeeCycle.Bees.Where(b=>b.stage==CycleStage.Egg).Take(2).ToArray();
  var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Item/InteriorSlot/Frame/PF_Frame_Hand.prefab");var art=(FrameCellArtwork)typeof(FrameCellPresenter).GetField("artwork",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(prefab.GetComponent<FrameCellPresenter>());
  var face=new FrameSurface(1,1);face.SetAllConstruction(100);face.TrySetBrood(0,0,eggs[0].id);go=new GameObject("Egg controls test",typeof(RectTransform));var mesh=go.AddComponent<FrameCellMesh>();
  Action<float,float,float> setup=(alpha,min,max)=>{mesh.ConfigureArt(1,0,1,40,Vector2.zero,FrameCellMesh.BuildJunctions(1,1,40,Vector2.zero),0,1,.3f,art,50,1,.3f,.3f,1,1,.3f,1,null,null,alpha,min,max);mesh.SetSurface(face,world.BeeCycle);};
  setup(1,0,0);var baseline=Read(mesh,art.egg);var center=new Vector3(Mathf.Sqrt(3)*20,40,0);
  foreach(float alpha in new[]{0f,.35f,1f}){setup(alpha,0,0);Check(Read(mesh,art.completeCell).All(v=>Mathf.Abs(v.color.a/255f-alpha)<.005),"complete cell opacity");Check(Read(mesh,art.egg).All(v=>v.color.a==255),"egg opacity independent");Check(Read(mesh,art.wallTwoArm).All(v=>v.color.a==255),"wall opacity independent");}
  setup(1,90,90);var fixedAngle=Read(mesh,art.egg);for(int i=0;i<baseline.Length;i++){var p=baseline[i].position-center;Check(Vector3.Distance(fixedAngle[i].position-center,new Vector3(-p.y,p.x,0))<.001,"fixed 90 degree angle");}
  setup(1,-35,70);var random=Read(mesh,art.egg);var p0=baseline[0].position-center;var p1=random[0].position-center;float angle=Vector2.SignedAngle(p0,p1);Check(angle>=-35&&angle<=70,"bounded random angle");
  setup(1,70,-35);Check(Read(mesh,art.egg).Zip(random,(a,b)=>Vector3.Distance(a.position,b.position)<.001).All(v=>v),"reversed endpoints normalize");
  var snapshot=new WorldSaveSnapshot{World=world,UtcTicks=1};loaded=WorldSaveCodec.Decode(WorldSaveCodec.Encode(snapshot)).World;mesh.SetSurface(face,loaded.BeeCycle);Check(Read(mesh,art.egg).Zip(random,(a,b)=>Vector3.Distance(a.position,b.position)<.001).All(v=>v),"rotation survives save reload");
  mesh.transform.localScale=new Vector3(-1,1,1);var mirrored=Read(mesh,art.egg);for(int i=0;i<random.Length;i++)Check(Vector3.Distance(random[i].position-center,mesh.transform.TransformPoint(mirrored[i].position)-mesh.transform.TransformPoint(center))<.001,"rotated egg not mirrored");mesh.transform.localScale=Vector3.one;
  face.TryClearContent(0,0);face.TrySetBrood(0,0,eggs[1].id);mesh.SetSurface(face,world.BeeCycle);Check(Vector3.Distance(Read(mesh,art.egg)[0].position,random[0].position)>.001,"different identity different rotation");
  UnityEngine.Object.DestroyImmediate(go);go=null;world.Thermal?.Dispose();world=null;loaded.Thermal?.Dispose();loaded=null;
  File.WriteAllText("outputs/egg-opacity/result.txt","PASS "+checks+" completion opacity, independent layers, egg bounds, fixed angle, identity, saved rotation and no-mirror checks.");BroodGrowthTests.Run();
 }catch(Exception e){File.WriteAllText("outputs/egg-opacity/result.txt","FAIL "+e);Debug.LogException(e);EditorApplication.Exit(1);}finally{if(go)UnityEngine.Object.DestroyImmediate(go);world?.Thermal?.Dispose();loaded?.Thermal?.Dispose();}}
}
