using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
public static class CombPreviewTests
{
 static int checks;static void Check(bool ok,string msg){checks++;if(!ok)throw new Exception(msg);}
 public static void Run()
 {
  const string output="outputs/comb-preview";Directory.CreateDirectory(output);
  var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);FrameCellEditorPreview preview=null;
  const string temp="Assets/Editor/Tests/CombPreviewTemp.unity";
  try{
   var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Item/InteriorSlot/Frame/PF_Frame_Hand.prefab");string prefabBefore=File.ReadAllText(AssetDatabase.GetAssetPath(prefab));
   var root=new GameObject("Preview test canvas",typeof(RectTransform),typeof(Canvas));SceneManager.MoveGameObjectToScene(root,scene);
   var camObject=new GameObject("Camera",typeof(Camera));SceneManager.MoveGameObjectToScene(camObject,scene);var camera=camObject.GetComponent<Camera>();camera.transform.position=new Vector3(0,0,-100);camera.orthographic=true;camera.orthographicSize=350;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.15f,.15f,.15f);
   var rt=new RenderTexture(1200,700,24);camera.targetTexture=rt;var canvas=root.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=100;
   var instance=(GameObject)PrefabUtility.InstantiatePrefab(prefab,scene);instance.transform.SetParent(root.transform,false);((RectTransform)instance.transform).sizeDelta=new Vector2(1000,500);
   var presenter=instance.GetComponent<FrameCellPresenter>();var data=new SerializedObject(presenter);if(data.FindProperty("cellArea").objectReferenceValue==null){data.FindProperty("cellArea").objectReferenceValue=instance.transform.Find("FrameCellArea") as RectTransform;data.ApplyModifiedProperties();}Canvas.ForceUpdateCanvases();string before=EditorJsonUtility.ToJson(presenter);var channels=canvas.additionalShaderChannels;
   preview=new FrameCellEditorPreview();preview.Update(presenter);Check(preview.Root,"preview created");Check(presenter.Frame==null,"no game frame bound");Check(EditorJsonUtility.ToJson(presenter)==before,"presenter settings untouched");Check((canvas.additionalShaderChannels&AdditionalCanvasShaderChannels.TexCoord1)!=0,"preview texture channel enabled");
   Check(preview.Root.GetComponentsInChildren<Transform>(true).All(t=>(t.gameObject.hideFlags&HideFlags.DontSaveInEditor)!=0),"all preview objects excluded from save");
   var first=preview.Root;preview.Update(presenter);Check(preview.Root==first,"unchanged preview reused");
   var meshes=preview.Root.GetComponentsInChildren<FrameCellMesh>();Check(meshes.Length>0,"production renderer reused");
   var previewFace=(FrameSurface)typeof(FrameCellMesh).GetField("surface",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(meshes[0]);
   var expectedWorld=new GameWorld(new WeatherSimulator(WeatherSimulator.DefaultSeed,WeatherAssetLoader.LoadBaseline()));try{expectedWorld.BeeCycle.SeedDemo();var expectedFace=ThermalWorld.Items(expectedWorld).First(i=>i.Kind==ItemKind.Frame&&i.GetContent<FrameData>().Front.CountCompletedCells()>0).GetContent<FrameData>().Front;for(int y=0;y<previewFace.Height;y++)for(int x=0;x<previewFace.Width;x++){Check(previewFace.GetCell(x,y).Content==expectedFace.GetCell(x,y).Content,"initial layout role");Check(Math.Abs(previewFace.GetPollenMassMg(x,y)-expectedFace.GetPollenMassMg(x,y))<.001&&Math.Abs(previewFace.GetTotalSugarMassMg(x,y)-expectedFace.GetTotalSugarMassMg(x,y))<.001&&Math.Abs(previewFace.GetWaterMassMg(x,y)-expectedFace.GetWaterMassMg(x,y))<.001,"initial quantities match");}}finally{expectedWorld.Thermal?.Dispose();}

   var art=(FrameCellArtwork)typeof(FrameCellPresenter).GetField("artwork",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(presenter);
   Check(art.cappedPupa.name.StartsWith("Capping_Base"),"new capping sprite bound");
   var cappingUV=new System.Collections.Generic.List<Vector2>();
   foreach(var mesh in meshes)using(var vh=new VertexHelper()){
    typeof(FrameCellMesh).GetMethod("PopulateTexture",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(mesh,new object[]{vh,art.cappedPupa.texture});
    for(int i=0;i<vh.currentVertCount;i++){var v=new UIVertex();vh.PopulateUIVertex(ref v,i);Check(v.uv1.z==1,"capping detail enabled");cappingUV.Add(new Vector2(v.uv1.x,v.uv1.y));}
   }
   Check(cappingUV.Count>0&&cappingUV.Distinct().Count()>art.cappedPupa.vertices.Length,"different brood have different texture transforms");
   foreach(var blend in new[]{0,1,2,3}){
    data.FindProperty("artwork.cappingTexture").objectReferenceValue=art.pollenTexture;
    data.FindProperty("artwork.cappingTextureOpacity").floatValue=.35f;
    data.FindProperty("artwork.cappingTextureBlend").enumValueIndex=blend;data.ApplyModifiedProperties();preview.Update(presenter);
    foreach(var layer in preview.Root.GetComponentsInChildren<FrameCellSpriteLayer>().Where(l=>l.mainTexture==art.cappedPupa.texture)){
     Check(layer.material.GetTexture("_DetailTex")==art.pollenTexture,"capping texture assigned");
     Check(Math.Abs(layer.material.GetFloat("_DetailOpacity")-.35f)<.001f&&layer.material.GetFloat("_DetailBlend")==blend,"capping material controls");
    }
   }
   data.FindProperty("artwork.cappingTexture").objectReferenceValue=null;
   data.FindProperty("artwork.cappingTextureOpacity").floatValue=1;
   data.FindProperty("artwork.cappingTextureBlend").enumValueIndex=1;data.ApplyModifiedProperties();preview.Update(presenter);
   var restoredUV=new System.Collections.Generic.List<Vector2>();
   foreach(var mesh in preview.Root.GetComponentsInChildren<FrameCellMesh>())using(var vh=new VertexHelper()){
    typeof(FrameCellMesh).GetMethod("PopulateTexture",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(mesh,new object[]{vh,art.cappedPupa.texture});
    for(int i=0;i<vh.currentVertCount;i++){var v=new UIVertex();vh.PopulateUIVertex(ref v,i);restoredUV.Add(new Vector2(v.uv1.x,v.uv1.y));}
   }
   Check(cappingUV.SequenceEqual(restoredUV),"capping pattern stable after rebuild");meshes=preview.Root.GetComponentsInChildren<FrameCellMesh>();
   float dry=data.FindProperty("dryNectarAlpha").floatValue,wet=data.FindProperty("wetNectarAlpha").floatValue;
   data.FindProperty("dryNectarAlpha").floatValue=.2f;data.FindProperty("wetNectarAlpha").floatValue=.2f;data.ApplyModifiedProperties();preview.Update(presenter);
   for(int kind=0;kind<5;kind++){
    Check(art.Nectar(kind)&&art.NectarOutside(kind),"inside/outside assigned for source "+kind);
    int insideCount=0,outsideCount=0;
    foreach(var mesh in preview.Root.GetComponentsInChildren<FrameCellMesh>()){
     foreach(bool outside in new[]{false,true})using(var vh=new VertexHelper()){
      var texture=(outside?art.NectarOutside(kind):art.Nectar(kind)).texture;
      typeof(FrameCellMesh).GetMethod("PopulateTexture",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(mesh,new object[]{vh,texture});
      if(outside)outsideCount+=vh.currentVertCount;else insideCount+=vh.currentVertCount;
      for(int i=0;i<vh.currentVertCount;i++){var v=new UIVertex();vh.PopulateUIVertex(ref v,i);Check(v.color.a==(outside?255:51),"only inside alpha follows moisture");}
     }
     var layers=mesh.GetComponentsInChildren<FrameCellSpriteLayer>();
     Check(Array.FindIndex(layers,l=>l.mainTexture==art.NectarOutside(kind).texture)>Array.FindIndex(layers,l=>l.mainTexture==art.Nectar(kind).texture),"outside layer above inside");
    }
    Check(insideCount>0&&outsideCount>0,"both nectar layers visible");
   }
   data.FindProperty("dryNectarAlpha").floatValue=dry;data.FindProperty("wetNectarAlpha").floatValue=wet;data.ApplyModifiedProperties();preview.Update(presenter);meshes=preview.Root.GetComponentsInChildren<FrameCellMesh>();
   foreach(var sprite in new[]{art.egg,art.larva,art.cappedPupa,art.pollen,art.rapeSeedNectar}){int count=0;foreach(var mesh in meshes)using(var vh=new VertexHelper()){typeof(FrameCellMesh).GetMethod("PopulateTexture",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(mesh,new object[]{vh,sprite.texture});count+=vh.currentVertCount;}Check(count>0,"sample sprite visible: "+sprite.name);}
   data.FindProperty("completeCellColor").colorValue=new Color(.4f,.6f,.8f,1);data.FindProperty("completeCellOpacity").floatValue=.4f;data.ApplyModifiedProperties();preview.Update(presenter);Check(preview.Root!=first,"settings edit refreshes preview");
   foreach(var mesh in preview.Root.GetComponentsInChildren<FrameCellMesh>())using(var vh=new VertexHelper()){typeof(FrameCellMesh).GetMethod("PopulateTexture",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(mesh,new object[]{vh,art.completeCell.texture});var v=new UIVertex();vh.PopulateUIVertex(ref v,0);Color color=v.color;Check(Mathf.Abs(color.r-.4f)<.005&&Mathf.Abs(color.a-.4f)<.005,"complete tint and independent opacity applied");}
   EditorSceneManager.SaveScene(scene,temp);Check(canvas.additionalShaderChannels==channels,"canvas channels restored before save");Check(!File.ReadAllText(temp).Contains("소방 예시"),"preview absent from saved scene");
   // Restore sample appearance for the screenshot without writing the source prefab.
   data.Update();data.FindProperty("completeCellColor").colorValue=Color.white;data.FindProperty("completeCellOpacity").floatValue=1;data.ApplyModifiedProperties();preview.Update(presenter);Canvas.ForceUpdateCanvases();camera.Render();var old=RenderTexture.active;RenderTexture.active=rt;var image=new Texture2D(1200,700,TextureFormat.RGBA32,false);image.ReadPixels(new Rect(0,0,1200,700),0,0);image.Apply();File.WriteAllBytes(output+"/preview.png",image.EncodeToPNG());RenderTexture.active=old;UnityEngine.Object.DestroyImmediate(image);rt.Release();UnityEngine.Object.DestroyImmediate(rt);
   preview.Dispose();Check(!preview.Root,"preview cleanup");Check(canvas.additionalShaderChannels==channels,"canvas channels restored");Check(File.ReadAllText(AssetDatabase.GetAssetPath(prefab))==prefabBefore,"original prefab unchanged");
   File.WriteAllText(output+"/result.txt","PASS "+checks+" edit-mode samples, live settings, tint, cleanup, no game binding and scene/prefab preservation checks.");
   EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);AssetDatabase.DeleteAsset(temp);EggOpacityTests.Run();
  }catch(Exception e){File.WriteAllText(output+"/result.txt","FAIL "+e);Debug.LogException(e);preview?.Dispose();if(scene.IsValid())EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);AssetDatabase.DeleteAsset(temp);EditorApplication.Exit(1);}
 }
}

