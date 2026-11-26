using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class BroodSpriteTests
{
    const BindingFlags Flags=BindingFlags.NonPublic|BindingFlags.Instance;
    static int checks;
    static void Check(bool ok,string label){checks++;if(!ok)throw new Exception(label);}
    static int Count(FrameCellMesh mesh,Sprite sprite)
    {using(var vh=new VertexHelper()){typeof(FrameCellMesh).GetMethod("PopulateTexture",Flags).Invoke(mesh,new object[]{vh,sprite.texture});return vh.currentVertCount;}}
    static float Width(FrameCellMesh mesh,Sprite sprite)
    {
        using(var vh=new VertexHelper())
        {
            typeof(FrameCellMesh).GetMethod("PopulateTexture",Flags).Invoke(mesh,new object[]{vh,sprite.texture});
            float min=float.PositiveInfinity,max=float.NegativeInfinity;var vertex=new UIVertex();
            for(int i=0;i<vh.currentVertCount;i++){vh.PopulateUIVertex(ref vertex,i);min=Math.Min(min,vertex.position.x);max=Math.Max(max,vertex.position.x);}
            return vh.currentVertCount==0?0:max-min;
        }
    }
    static void CheckOrientation(FrameCellMesh mesh,Sprite sprite)
    {
        Vector3[] offsets=null;var anchor=new Vector3(Mathf.Sqrt(3)*20,40,0);
        foreach(float sign in new[]{1f,-1f})
        {
            mesh.transform.localScale=new Vector3(sign,1,1);
            using(var vh=new VertexHelper())
            {
                typeof(FrameCellMesh).GetMethod("PopulateTexture",Flags).Invoke(mesh,new object[]{vh,sprite.texture});
                if(offsets==null)offsets=new Vector3[vh.currentVertCount];
                Check(vh.currentVertCount>0&&vh.currentVertCount==offsets.Length,"orientation test has matching sprite vertices");
                var vertex=new UIVertex();
                for(int i=0;i<vh.currentVertCount;i++)
                {
                    vh.PopulateUIVertex(ref vertex,i);var p=mesh.transform.TransformPoint(vertex.position)-mesh.transform.TransformPoint(anchor);
                    if(sign>0)offsets[i]=p;else Check(Vector3.Distance(offsets[i],p)<.001,"source image orientation preserved under horizontal mirror");
                }
            }
        }
        mesh.transform.localScale=Vector3.one;
    }
    public static void Run()
    {
        const string output="outputs/brood-sprites";Directory.CreateDirectory(output);
        GameWorld world=null;GameObject go=null;
        try
        {
            world=new GameWorld(new WeatherSimulator(WeatherSimulator.DefaultSeed,WeatherAssetLoader.LoadBaseline()));world.BeeCycle.SeedDemo();
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Item/InteriorSlot/Frame/PF_Frame_Hand.prefab");
            var art=(FrameCellArtwork)typeof(FrameCellPresenter).GetField("artwork",Flags).GetValue(prefab.GetComponent<FrameCellPresenter>());
            Check(art.egg&&art.larva&&art.cappedPupa&&art.pollen,"four supplied sprites bound");
            var bee=world.BeeCycle.Bees.First(b=>b.stage==CycleStage.Egg);var originalStage=bee.stage;
            var surface=new FrameSurface(1,1);surface.TrySetConstruction(0,0,100);surface.TrySetBrood(0,0,bee.id);
            go=new GameObject("Brood render check",typeof(RectTransform));var mesh=go.AddComponent<FrameCellMesh>();
            mesh.ConfigureArt(1,0,1,40,Vector2.zero,FrameCellMesh.BuildJunctions(1,1,40,Vector2.zero),0,1,.3f,art);
            int revision=surface.Revision;
            foreach(var stage in new[]{CycleStage.Egg,CycleStage.Larva,CycleStage.Pupa})
            {
                bee.stage=stage;mesh.SetSurface(surface,world.BeeCycle);
                Sprite expected=stage==CycleStage.Egg?art.egg:stage==CycleStage.Larva?art.larva:art.cappedPupa;
                foreach(var sprite in new[]{art.egg,art.larva,art.cappedPupa})Check(Count(mesh,sprite)==(sprite==expected?sprite.vertices.Length:0),"stage sprite replaces previous "+stage);
                Check(surface.Revision==revision,"stage transition without surface revision handled");
                CheckOrientation(mesh,expected);
            }
            bee.stage=CycleStage.Adult;mesh.SetSurface(surface,world.BeeCycle);Check(Count(mesh,art.cappedPupa)==0,"adult no brood image");
            bee.stage=CycleStage.Dead;mesh.SetSurface(surface,world.BeeCycle);Check(Count(mesh,art.egg)==0&&Count(mesh,art.larva)==0&&Count(mesh,art.cappedPupa)==0,"dead no brood image");
            surface.TryClearContent(0,0);surface.TrySetBrood(0,0,long.MaxValue);mesh.SetSurface(surface,world.BeeCycle);Check(Count(mesh,art.egg)==0,"unknown bee not inferred as egg");
            surface.TryClearContent(0,0);surface.TryAddPollen(0,0,10);mesh.SetSurface(surface,world.BeeCycle);Check(Count(mesh,art.pollen)==art.pollen.vertices.Length,"pollen present");
            CheckOrientation(mesh,art.pollen);CheckOrientation(mesh,art.completeCell);
            surface.WithdrawCycle(0,0,10,true,out _);mesh.SetSurface(surface,world.BeeCycle);Check(Count(mesh,art.pollen)==0,"consuming pollen removes sprite");
            surface.TryAddPollen(0,0,(float)CombBalance.PollenCapacityMg);mesh.SetSurface(surface,world.BeeCycle);float full=Width(mesh,art.pollen);
            int previous=mesh.RefreshCount;
            surface.WithdrawCycle(0,0,CombBalance.PollenCapacityMg/2,true,out _);mesh.SetSurface(surface,world.BeeCycle);
            Check(Math.Abs(Width(mesh,art.pollen)/full-.65f)<.001,"half pollen 65 percent size");
            Check(mesh.RefreshCount==previous+1,"quantity-only change invalidates pollen mesh");
            surface.TryClearContent(0,0);surface.TryAddPollen(0,0,.001f);mesh.SetSurface(surface,world.BeeCycle);
            Check(Math.Abs(Width(mesh,art.pollen)/full-.3f)<.001,"tiny pollen minimum 30 percent");
            surface.TryClearContent(0,0);mesh.SetSurface(surface,world.BeeCycle);
            surface.TryAddNectar(0,0,0,10,10);mesh.SetSurface(surface,world.BeeCycle);CheckOrientation(mesh,art.rapeSeedNectar);surface.TryClearContent(0,0);mesh.SetSurface(surface,world.BeeCycle);
            int before=mesh.RefreshCount;mesh.SetSurface(surface,world.BeeCycle);Check(mesh.RefreshCount==before,"unchanged state skips refresh");
            surface.TrySetBrood(0,0,bee.id);bee.stage=CycleStage.Pupa;mesh.SetSurface(surface,world.BeeCycle);
            mesh.SetSurface(new FrameSurface(1,1),world.BeeCycle);Check(Count(mesh,art.cappedPupa)==0,"other face has independent state");
            bee.stage=originalStage;
            UnityEngine.Object.DestroyImmediate(go);go=null;
            Render(world,prefab,output);
            File.WriteAllText(output+"/result.txt","PASS "+checks+" sprite binding, egg/larva/pupa transitions, adult/dead/unknown handling, pollen consumption, minute-driven refresh and facing-surface checks.");
            world.Thermal?.Dispose();world=null;
            CellWallValidation.Run();
        }
        catch(Exception e){File.WriteAllText(output+"/result.txt","FAIL "+e);Debug.LogException(e);EditorApplication.Exit(1);}
        finally{if(go)UnityEngine.Object.DestroyImmediate(go);world?.Thermal?.Dispose();}
    }
    static void Render(GameWorld world,GameObject prefab,string output)
    {
        var frame=ThermalWorld.Items(world).First(i=>i.Kind==ItemKind.Frame&&i.GetContent<FrameData>().CountCompletedCells()>0);
        var root=new GameObject("Brood preview",typeof(RectTransform),typeof(Canvas));
        var cg=new GameObject("Preview camera",typeof(Camera));var camera=cg.GetComponent<Camera>();camera.transform.position=new Vector3(0,0,-100);camera.orthographic=true;camera.orthographicSize=350;camera.backgroundColor=new Color(.15f,.15f,.15f);camera.clearFlags=CameraClearFlags.SolidColor;
        var rt=new RenderTexture(1200,700,24);camera.targetTexture=rt;
        var canvas=root.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=100;
        var view=(UIItemPartView)ItemVisualFactory.Create(frame,0,ItemViewContext.Hand,null,prefab,root.transform);
        var presenter=view.GetComponent<FrameCellPresenter>();presenter.BindBroodRuntime(world.BeeCycle);
        Canvas.ForceUpdateCanvases();presenter.Rebuild();Canvas.ForceUpdateCanvases();camera.Render();
        var old=RenderTexture.active;RenderTexture.active=rt;var image=new Texture2D(1200,700,TextureFormat.RGBA32,false);image.ReadPixels(new Rect(0,0,1200,700),0,0);image.Apply();File.WriteAllBytes(output+"/preview.png",image.EncodeToPNG());RenderTexture.active=old;
        var data=frame.GetContent<FrameData>();var face=data.GetFacingSurface();long id=0;
        for(int y=0;y<face.Height&&id==0;y++)for(int x=0;x<face.Width&&id==0;x++)id=face.GetBeeId(x,y);
        Check(id>0,"preview face has brood");var b=world.BeeCycle.Bees.First(v=>v.id==id);
        var meshes=presenter.GetComponentsInChildren<FrameCellMesh>();int count=meshes.Sum(m=>m.RefreshCount);int revision=face.Revision;
        b.stage=b.stage==CycleStage.Pupa?CycleStage.Egg:CycleStage.Pupa;
        typeof(BeeCycleRuntime).GetProperty("LastMinute").SetValue(world.BeeCycle,world.BeeCycle.LastMinute+1);
        typeof(FrameCellPresenter).GetMethod("LateUpdate",Flags).Invoke(presenter,null);
        Check(face.Revision==revision&&meshes.Sum(m=>m.RefreshCount)>count,"presenter detects stage on next minute without cell mutation");
        count=meshes.Sum(m=>m.RefreshCount);typeof(FrameCellPresenter).GetMethod("LateUpdate",Flags).Invoke(presenter,null);Check(meshes.Sum(m=>m.RefreshCount)==count,"same minute skips unchanged face");
        UnityEngine.Object.DestroyImmediate(image);UnityEngine.Object.DestroyImmediate(root);UnityEngine.Object.DestroyImmediate(cg);rt.Release();UnityEngine.Object.DestroyImmediate(rt);
    }
}
