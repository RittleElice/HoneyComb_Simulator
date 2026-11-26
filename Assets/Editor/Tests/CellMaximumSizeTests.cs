using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
public static class CellMaximumSizeTests
{
 static float Width(FrameCellMesh mesh,Sprite sprite){using(var vh=new VertexHelper()){typeof(FrameCellMesh).GetMethod("PopulateTexture",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(mesh,new object[]{vh,sprite.texture});float lo=float.PositiveInfinity,hi=float.NegativeInfinity;var v=new UIVertex();for(int i=0;i<vh.currentVertCount;i++){vh.PopulateUIVertex(ref v,i);lo=Mathf.Min(lo,v.position.x);hi=Mathf.Max(hi,v.position.x);}return hi-lo;}}
 public static void Run()
 {
  Directory.CreateDirectory("outputs/cell-growth");int checks=0;
  try{
   var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Item/InteriorSlot/Frame/PF_Frame_Hand.prefab");var art=(FrameCellArtwork)typeof(FrameCellPresenter).GetField("artwork",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(prefab.GetComponent<FrameCellPresenter>());
   var go=new GameObject("Maximum size test",typeof(RectTransform));var mesh=go.AddComponent<FrameCellMesh>();var s=new FrameSurface(1,1);s.SetAllConstruction(100);
   foreach(var kind in new[]{CellContentKind.Pollen,CellContentKind.Nectar}){
    s.TryClearContent(0,0);s.TrySetStoredContent(0,0,kind,1);var sprite=kind==CellContentKind.Pollen?art.pollen:art.wildflowerNectar;
    Action<float> setup=max=>{mesh.ConfigureArt(1,0,1,40,Vector2.zero,FrameCellMesh.BuildJunctions(1,1,40,Vector2.zero),0,1,.3f,art,50,1,.3f,.3f,max,max);mesh.SetSurface(s);};
    setup(1);float original=Width(mesh,sprite);
    foreach(float max in new[]{.6f,1.5f}){
     s.TrySetFillRatio(0,0,1);setup(max);if(Mathf.Abs(Width(mesh,sprite)/original-max)>.001)throw new Exception(kind+" full maximum");checks++;
     s.TrySetFillRatio(0,0,.5f);mesh.SetSurface(s);if(Mathf.Abs(Width(mesh,sprite)/original-max*.65f)>.001)throw new Exception(kind+" half interpolated");checks++;
     s.TrySetFillRatio(0,0,.00001f);mesh.SetSurface(s);if(Mathf.Abs(Width(mesh,sprite)/original-max*.3f)>.001)throw new Exception(kind+" relative minimum");checks++;
    }
   }
   UnityEngine.Object.DestroyImmediate(go);File.WriteAllText("outputs/cell-growth/result.txt","PASS "+checks+" nectar/pollen maximum and relative minimum size checks.");PollenTextureControlsTests.Run();
  }catch(Exception e){File.WriteAllText("outputs/cell-growth/result.txt","FAIL "+e);Debug.LogException(e);EditorApplication.Exit(1);}
 }
}
