using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
public static class NectarOutlineAlphaTests
{
 public static void Run(){GameObject go=null;try{
 var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Item/InteriorSlot/Frame/PF_Frame_Hand.prefab");
 var bound=(FrameCellArtwork)typeof(FrameCellPresenter).GetField("artwork",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(prefab.GetComponent<FrameCellPresenter>());
 var art=new FrameCellArtwork{rapeSeedNectar=bound.rapeSeedNectar,wildflowerNectar=bound.rapeSeedNectar,rapeSeedNectarOutside=bound.rapeSeedNectarOutside,wildflowerNectarOutside=bound.rapeSeedNectarOutside,nectarOutsideDryAlpha=.8f,nectarOutsideWetAlpha=.2f};
 go=new GameObject("Outline alpha test",typeof(RectTransform),typeof(CanvasRenderer));var mesh=go.AddComponent<FrameCellMesh>();
 mesh.ConfigureArt(1,0,1,40,Vector2.zero,FrameCellMesh.BuildJunctions(1,1,40,Vector2.zero),0,1,1,art,50,.4f,.4f);
 var face=new FrameSurface(1,1);face.TrySetConstruction(0,0,100);
 int checks=0;
 foreach(float moisture in new[]{0f,.5f,1f}){
 face.TryClearContent(0,0);if(!face.TryAddNectar(0,0,0,100*(1-moisture),100*moisture))throw new Exception("Deposit failed");int prior=mesh.RefreshCount;mesh.SetSurface(face);if(mesh.RefreshCount<=prior)throw new Exception("Outer alpha change did not refresh");checks++;
 foreach(bool outer in new[]{false,true})using(var vh=new VertexHelper()){
 var sprite=outer?art.rapeSeedNectarOutside:art.rapeSeedNectar;
 typeof(FrameCellMesh).GetMethod("PopulateTexture",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(mesh,new object[]{vh,sprite.texture});
 if(vh.currentVertCount==0)throw new Exception("Nectar missing");float expected=outer?Mathf.Lerp(.8f,.2f,moisture):.4f;
 for(int i=0;i<vh.currentVertCount;i++){var v=new UIVertex();vh.PopulateUIVertex(ref v,i);if(Math.Abs(v.color.a/255f-expected)>.005f)throw new Exception("Alpha mismatch");checks++;}
 }
 int stable=mesh.RefreshCount;mesh.SetSurface(face);if(mesh.RefreshCount!=stable)throw new Exception("Unchanged cells refreshed");checks++;
 }
 Directory.CreateDirectory("outputs/nectar-outline-alpha");File.WriteAllText("outputs/nectar-outline-alpha/result.txt","PASS "+checks+" moisture endpoints/midpoint, independent inside alpha, outer-only dirty update and unchanged skip checks.");UnityEngine.Object.DestroyImmediate(go);CombPreviewTests.Run();
 }catch(Exception e){Directory.CreateDirectory("outputs/nectar-outline-alpha");File.WriteAllText("outputs/nectar-outline-alpha/result.txt","FAIL "+e);if(go)UnityEngine.Object.DestroyImmediate(go);EditorApplication.Exit(1);}}
}
