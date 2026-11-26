using System;
using System.IO;
using System.Reflection;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
public static class PollenAppearanceTests
{
 static int checks;
 static void Check(bool ok,string message){checks++;if(!ok)throw new Exception(message);}
 static byte[] Save(FrameSurface s,bool visuals){using(var ms=new MemoryStream()){using(var w=new BinaryWriter(ms,System.Text.Encoding.UTF8,true))typeof(FrameSurface).GetMethod("WriteSnapshotWithAppearance",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(s,new object[]{w,true,visuals});return ms.ToArray();}}
 static FrameSurface Load(byte[] bytes,bool visuals){var s=new FrameSurface(2,1);using(var r=new BinaryReader(new MemoryStream(bytes)))typeof(FrameSurface).GetMethod("ReadSnapshotWithAppearance",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(s,new object[]{r,true,visuals});return s;}
 static UIVertex[] Vertices(FrameCellMesh mesh,Texture t){using(var vh=new VertexHelper()){typeof(FrameCellMesh).GetMethod("PopulateTexture",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(mesh,new object[]{vh,t});var result=new UIVertex[vh.currentVertCount];for(int i=0;i<result.Length;i++)vh.PopulateUIVertex(ref result[i],i);return result;}}
 public static void Run()
 {
  const string output="outputs/pollen-style";Directory.CreateDirectory(output);
  try {
   var s=new FrameSurface(2,1);s.SetAllConstruction(100);
   Check(s.GetPollenAppearanceSeed(0,0)==0,"empty has no seed");
   s.TryAddPollen(0,0,10);uint seed=s.GetPollenAppearanceSeed(0,0);Check(seed!=0,"deposit creates seed");
   s.TryAddPollen(0,0,10);Check(s.GetPollenAppearanceSeed(0,0)==seed,"additional deposit preserves seed");
   s.WithdrawCycle(0,0,5,true,out _);Check(s.GetPollenAppearanceSeed(0,0)==seed,"partial withdrawal preserves seed");
   s.TryAddPollen(1,0,15);Check(s.GetPollenAppearanceSeed(1,0)!=seed,"cells have distinct seeds");
   var bytes=Save(s,true);var loaded=Load(bytes,true);
   Check(loaded.GetPollenAppearanceSeed(0,0)==seed,"seed restored");Check(bytes.SequenceEqual(Save(loaded,true)),"byte exact appearance roundtrip");
   s.WithdrawCycle(0,0,100,true,out _);loaded.WithdrawCycle(0,0,100,true,out _);Check(s.GetPollenAppearanceSeed(0,0)==0,"empty clears seed");
   s.TryAddPollen(0,0,10);loaded.TryAddPollen(0,0,10);Check(s.GetPollenAppearanceSeed(0,0)!=seed,"refill gets new seed");Check(s.GetPollenAppearanceSeed(0,0)==loaded.GetPollenAppearanceSeed(0,0),"future visual random stream resumes");
   s.TryClearContent(0,0);Check(s.GetPollenAppearanceSeed(0,0)==0,"explicit clearing resets");s.TryAddPollen(0,0,10);s.TrySetConstruction(0,0,50);Check(s.GetPollenAppearanceSeed(0,0)==0,"wall collapse resets");
   var migrated=Load(Save(loaded,false),false);Check(migrated.GetPollenAppearanceSeed(0,0)!=0,"old surface migrates");Check(Save(migrated,true).SequenceEqual(Save(Load(Save(migrated,true),true),true)),"migrated seeds persist");
   var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Item/InteriorSlot/Frame/PF_Frame_Hand.prefab");
   var art=(FrameCellArtwork)typeof(FrameCellPresenter).GetField("artwork",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(prefab.GetComponent<FrameCellPresenter>());
   Check(art.pollen&&art.pollenTexture&&art.pollenShader,"prefab references assigned");Check(!ShaderUtil.ShaderHasError(art.pollenShader),"shader compiles");
   var root=new GameObject("Pollen test",typeof(RectTransform),typeof(Canvas));var child=new GameObject("mesh",typeof(RectTransform));child.transform.SetParent(root.transform,false);var mesh=child.AddComponent<FrameCellMesh>();
   mesh.ConfigureArt(2,0,2,40,Vector2.zero,FrameCellMesh.BuildJunctions(2,1,40,Vector2.zero),0,1,.3f,art);mesh.SetSurface(loaded);
   var a=Vertices(mesh,art.pollen.texture);Check(a.Length>0,"pollen geometry emitted");
   Check((root.GetComponent<Canvas>().additionalShaderChannels&AdditionalCanvasShaderChannels.TexCoord1)!=0,"detail channel enabled");
   mesh.SetSurface(Load(Save(loaded,true),true));var b=Vertices(mesh,art.pollen.texture);
   Check(a.Length==b.Length&&a.Zip(b,(x,y)=>x.color.Equals(y.color)&&x.uv1==y.uv1).All(x=>x),"saved seed restores exact tint and texture UV");
   mesh.SetSurface(loaded);int refresh=mesh.RefreshCount;mesh.SetSurface(loaded);Check(mesh.RefreshCount==refresh,"unchanged appearance skips mesh refresh");
   UnityEngine.Object.DestroyImmediate(root);
   WorldSaveTests.Run();
   File.WriteAllText(output+"/result.txt","PASS "+checks+" pollen lifecycle, save migration, deterministic UV/color and shader checks; WorldSaveTests passed.");
   BroodSpriteTests.Run();
  } catch(Exception e){File.WriteAllText(output+"/result.txt","FAIL "+e);Debug.LogException(e);EditorApplication.Exit(1);}
 }
}
