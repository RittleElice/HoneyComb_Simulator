using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class PollenTextureControlsTests
{
 static int checks;
 static void Check(bool ok,string msg){checks++;if(!ok)throw new Exception(msg);}
 static Texture2D Solid(Color color){var t=new Texture2D(4,4,TextureFormat.RGBA32,false,true);var p=new Color[16];for(int i=0;i<p.Length;i++)p[i]=color;t.SetPixels(p);t.Apply();return t;}
 static Vector4[] UV(FrameCellMesh mesh,Texture t){using(var vh=new VertexHelper()){typeof(FrameCellMesh).GetMethod("PopulateTexture",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(mesh,new object[]{vh,t});var result=new Vector4[vh.currentVertCount];var v=new UIVertex();for(int i=0;i<result.Length;i++){vh.PopulateUIVertex(ref v,i);result[i]=v.uv1;}return result;}}
 public static void Run()
 {
  const string output="outputs/pollen-controls";Directory.CreateDirectory(output);
  try {
   var shader=AssetDatabase.LoadAssetAtPath<Shader>("Assets/Shaders/CellPollen.shader");Check(shader&&!ShaderUtil.ShaderHasError(shader),"shader compilation");
   var main=Solid(new Color(.25f,.5f,.75f,1));var detail=Solid(new Color(.7f,.3f,.5f,1));var sprite=Sprite.Create(main,new Rect(0,0,4,4),new Vector2(.5f,.5f));
   var art=new FrameCellArtwork{pollen=sprite,pollenTexture=detail,pollenShader=shader,pollenColorA=Color.white,pollenColorB=Color.white};
   Check(art.pollenTextureScale==1&&art.pollenTextureOpacity==1&&art.pollenTextureBlend==PollenTextureBlend.Multiply,"unchanged defaults");
   var root=new GameObject("Pollen control canvas",typeof(RectTransform),typeof(Canvas));var cameraObject=new GameObject("Pollen control camera",typeof(Camera));var camera=cameraObject.GetComponent<Camera>();camera.transform.position=new Vector3(0,0,-100);camera.orthographic=true;camera.orthographicSize=64;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.clear;
   var rt=new RenderTexture(128,128,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Linear);camera.targetTexture=rt;
   var canvas=root.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=100;
   var child=new GameObject("Pollen mesh",typeof(RectTransform));child.transform.SetParent(root.transform,false);child.transform.localPosition=new Vector3(-Mathf.Sqrt(3)*20,-40,0);var mesh=child.AddComponent<FrameCellMesh>();
   var surface=new FrameSurface(1,1);surface.SetAllConstruction(100);surface.TryAddPollen(0,0,(float)CombBalance.PollenCapacityMg);uint seed=surface.GetPollenAppearanceSeed(0,0);
   Action configure=()=>{mesh.ConfigureArt(1,0,1,40,Vector2.zero,FrameCellMesh.BuildJunctions(1,1,40,Vector2.zero),0,1,.3f,art);mesh.SetSurface(surface);Canvas.ForceUpdateCanvases();};
   var read=new Texture2D(128,128,TextureFormat.RGBA32,false,true);
   Func<Color> render=()=>{configure();camera.Render();var old=RenderTexture.active;RenderTexture.active=rt;read.ReadPixels(new Rect(0,0,128,128),0,0);read.Apply();RenderTexture.active=old;return read.GetPixel(64,64);};
   art.pollenTextureOpacity=0;Color baseline=render();Check(Mathf.Abs(baseline.r-.25f)<.025&&Mathf.Abs(baseline.b-.75f)<.025,"untextured GPU base color");
   Color d=detail.GetPixel(0,0);
   foreach(PollenTextureBlend mode in Enum.GetValues(typeof(PollenTextureBlend)))
   {
    art.pollenTextureBlend=mode;art.pollenTextureOpacity=1;Color actual=render();Color expected=d;
    for(int k=0;k<3;k++){float b=baseline[k],t=d[k];expected[k]=mode==PollenTextureBlend.Multiply?b*t:mode==PollenTextureBlend.Screen?1-(1-b)*(1-t):mode==PollenTextureBlend.Overlay?(b<.5?2*b*t:1-2*(1-b)*(1-t)):t;Check(Mathf.Abs(actual[k]-expected[k])<.025,"GPU blend "+mode+" channel "+k);}
    Check(Mathf.Abs(actual.a-baseline.a)<.01,"blend preserves silhouette opacity");
    art.pollenTextureOpacity=.5f;Color half=render();for(int k=0;k<3;k++)Check(Mathf.Abs(half[k]-(baseline[k]+actual[k])*.5f)<.025,"half strength "+mode);
    File.WriteAllBytes(output+"/"+mode+".png",read.EncodeToPNG());
   }
   art.pollenTexture=null;art.pollenTextureOpacity=1;art.pollenTextureBlend=PollenTextureBlend.Normal;var missing=render();Check(Mathf.Abs(missing.r-baseline.r)<.01,"missing texture contributes nothing in normal mode");
   art.pollenTexture=detail;art.pollenTextureScale=1;configure();var a=UV(mesh,main);art.pollenTextureScale=2;configure();var bUV=UV(mesh,main);Check(a.Length==bUV.Length&&a.Length>1,"same geometry at different texture size");
   Check(Mathf.Abs(Vector2.Distance(a[0],a[1])*.5f-Vector2.Distance(bUV[0],bUV[1]))<.0001,"size two halves UV frequency");Check(surface.GetPollenAppearanceSeed(0,0)==seed,"control edits preserve seed");
   art.pollenTextureScale=.25f;configure();var small=UV(mesh,main);Check(Mathf.Abs(Vector2.Distance(a[0],a[1])*4-Vector2.Distance(small[0],small[1]))<.0001,"size quarter quadruples UV frequency");
   UnityEngine.Object.DestroyImmediate(root);UnityEngine.Object.DestroyImmediate(cameraObject);rt.Release();UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(read);UnityEngine.Object.DestroyImmediate(sprite);UnityEngine.Object.DestroyImmediate(main);UnityEngine.Object.DestroyImmediate(detail);
   File.WriteAllText(output+"/result.txt","PASS "+checks+" GPU blend, opacity, texture scale, missing texture, defaults and seed preservation checks.");
   PollenAppearanceTests.Run();
  }catch(Exception e){File.WriteAllText(output+"/result.txt","FAIL "+e);Debug.LogException(e);EditorApplication.Exit(1);}
 }
}
