using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class CellWallValidation
{
    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
    static int checks;
    static void Check(bool ok, string label) { checks++; if (!ok) throw new Exception(label); }
    static void Late(FrameCellPresenter p) => typeof(FrameCellPresenter).GetMethod("LateUpdate", Flags).Invoke(p, null);
    static Sprite Load(string name) => AssetDatabase.LoadAllAssetsAtPath("Assets/Art/CellObservation/" + name + ".png").OfType<Sprite>().First();
    static FrameCellArtwork Art() => new FrameCellArtwork { wallTwoArm=Load("Cell_Unit_001"),wallThreeArm=Load("Cell_Unit_002"),completeCell=Load("Cell_Complete"),rapeSeedNectar=Load("Nectar_RapeSeed_Inside"),wildflowerNectar=Load("Nectar_Wildflower_Inside"),cherryBlossomNectar=Load("Nectar_CherryBlossom_Inside"),blackLocustNectar=Load("Nectar_BlackLocust_Inside"),chestnutNectar=Load("Nectar_Chestnut_Inside") };
    static void Draw(FrameCellMesh mesh, Sprite sprite, VertexHelper vh) => typeof(FrameCellMesh).GetMethod("PopulateTexture", Flags).Invoke(mesh, new object[]{vh,sprite.texture});
    static int Count(FrameCellMesh mesh, Sprite sprite) { using(var vh=new VertexHelper()){Draw(mesh,sprite,vh);return vh.currentVertCount;} }
    static void Deposit(FrameSurface s,int source,float sugar,float water=0) => Check(s.TryAddNectar(0,0,source,sugar,water),"deposit");
    [MenuItem("Tools/HoneyComb/검증/Run Cell Wall Validation")]
    public static void Run()
    {
        const string folder="outputs/cell-sprites"; Directory.CreateDirectory(folder);
        GameObject go=null;
        try
        {
            var art=Art();
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Item/InteriorSlot/Frame/PF_Frame_Hand.prefab");
            var bound=(FrameCellArtwork)typeof(FrameCellPresenter).GetField("artwork",Flags).GetValue(prefab.GetComponent<FrameCellPresenter>());
            Check(bound.wallTwoArm==art.wallTwoArm&&bound.wallThreeArm==art.wallThreeArm&&bound.completeCell==art.completeCell,"prefab walls and complete sprite bound");
            for(int i=0;i<5;i++)Check(bound.Nectar(i)==art.Nectar(i),"prefab nectar sprite bound "+i);
            var surface=new FrameSurface(3,3);
            go=new GameObject("sprite test",typeof(RectTransform));var mesh=go.AddComponent<FrameCellMesh>();
            mesh.ConfigureArt(3,0,9,40,Vector2.zero,FrameCellMesh.BuildJunctions(3,3,40,Vector2.zero),0,1,.3f,art);
            mesh.SetSurface(surface);Check(Count(mesh,art.wallTwoArm)==0,"empty construction hidden");
            surface.TrySetConstruction(0,0,60);mesh.SetSurface(surface);
            Check(Count(mesh,art.wallTwoArm)==6*art.wallTwoArm.vertices.Length,"isolated cell six two-arm sprites");
            surface.TrySetConstruction(1,0,100);mesh.SetSurface(surface);
            Check(Count(mesh,art.wallThreeArm)==2*art.wallThreeArm.vertices.Length,"two shared junctions");
            using(var vh=new VertexHelper())
            {
                Draw(mesh,art.wallThreeArm,vh);var v=new UIVertex();vh.PopulateUIVertex(ref v,0);Check(v.color.a==255,"maximum neighbor opacity");
            }
            surface.TrySetConstruction(0,0,100);
            Deposit(surface,0,50,300);Deposit(surface,1,50);
            Check(FrameCellMesh.RepresentativeNectar(surface,0,0,50)==4,"50/50 -> mixed");
            Deposit(surface,0,1);
            Check(FrameCellMesh.RepresentativeNectar(surface,0,0,50)==0,"strict majority, water excluded");
            Check(FrameCellMesh.RepresentativeNectar(surface,0,0,60)==4,"adjustable higher threshold");
            surface.TryClearContent(0,0);
            Deposit(surface,4,10);Deposit(surface,5,20);Deposit(surface,6,30);Deposit(surface,7,40);
            var entries=new List<KeyValuePair<int,float>>();surface.CopyNectarSugars(0,0,entries);
            Check(entries.Count==1&&entries[0].Key==4&&entries[0].Value==100,"four wildflowers stored as one source");
            Check(surface.GetSugarMassMg(0,0,5)==0&&surface.GetTotalSugarMassMg(0,0)==100,"no alias double counting");
            Deposit(surface,2,90);Check(FrameCellMesh.RepresentativeNectar(surface,0,0,50)==4,"combined wildflower majority");
            Check(Math.Abs(surface.DepositCycle(0,0,6,3,false)-3)<1e-6,"legacy source deposit reports actual canonical increase");
            var cargo=surface.WithdrawCycle(0,0,193,false,out _);
            Check(cargo.sugars.All(s=>s.source<5||s.source>7)&&Math.Abs(cargo.Sugar-193)<1e-6,"withdrawal preserves total and canonical identity");
            var oldCargo=new CycleCargo();oldCargo.sugars.Add(new CycleSugar{source=5,mass=12});oldCargo.sugars.Add(new CycleSugar{source=7,mass=18});oldCargo.NormalizeWildflowerSources();oldCargo.Add(6,10);
            Check(oldCargo.sugars.Count==1&&oldCargo.sugars[0].source==4&&oldCargo.Sugar==40,"legacy and new cargo collapse wildflower");
            using(var stream=new MemoryStream())
            {
                using(var w=new BinaryWriter(stream,System.Text.Encoding.UTF8,true))
                {
                    w.Write((byte)2);w.Write((byte)6);w.Write((byte)100);w.Write((byte)0);w.Write(0f);
                    w.Write(true);w.Write(100f);w.Write(4);
                    for(int i=4;i<8;i++){w.Write(i);w.Write((float)(i-3)*10);}
                }
                stream.Position=0;var restored=new FrameSurface(1,1);
                using(var reader=new BinaryReader(stream,System.Text.Encoding.UTF8,true))typeof(FrameSurface).GetMethod("ReadSnapshot",Flags).Invoke(restored,new object[]{reader,false});
                restored.CopyNectarSugars(0,0,entries);
                Check(entries.Count==1&&entries[0].Key==4&&entries[0].Value==100&&restored.GetWaterMassMg(0,0)==100,"old snapshot merges all four wildflowers");
                using(var roundtrip=new MemoryStream())
                {
                    using(var w=new BinaryWriter(roundtrip,System.Text.Encoding.UTF8,true))typeof(FrameSurface).GetMethod("WriteSnapshot",Flags).Invoke(restored,new object[]{w,false});
                    roundtrip.Position=13;using(var r=new BinaryReader(roundtrip))Check(r.ReadInt32()==1&&r.ReadInt32()==4,"new save writes only one wildflower entry");
                }
            }
            Check(Math.Abs(FrameCellMesh.NectarOpacity(100,0,1,.3f)-1)<1e-6,"dry opaque");
            Check(Math.Abs(FrameCellMesh.NectarOpacity(100,100,1,.3f)-.65f)<1e-6,"half water opacity");
            Check(Math.Abs(FrameCellMesh.NectarOpacity(0,100,1,.3f)-.3f)<1e-6,"water-only minimum opacity");
            for(int kind=0;kind<5;kind++)
            {
                surface.TryClearContent(0,0);Deposit(surface,kind,100,100);mesh.SetSurface(surface);
                Check(Count(mesh,art.Nectar(kind))==art.Nectar(kind).vertices.Length,"source texture rendered "+kind);
                using(var vh=new VertexHelper()){Draw(mesh,art.Nectar(kind),vh);var v=new UIVertex();vh.PopulateUIVertex(ref v,0);Check(Math.Abs(v.color.a/255f-.65f)<.005,"rendered moisture alpha");}
            }
            int revision=mesh.RefreshCount;mesh.SetSurface(surface);Check(mesh.RefreshCount==revision,"unchanged state no mesh rebuild");
            surface.TryClearContent(0,0);Deposit(surface,1,100,100);mesh.SetSurface(surface);Check(mesh.RefreshCount==revision+1,"composition change with equal fill invalidates mesh");
            Check(Count(mesh,art.wildflowerNectar)==0,"old source texture cleared");
            surface.TryClearContent(0,0);Deposit(surface,1,.001f,.001f);mesh.SetSurface(surface);
            using(var vh=new VertexHelper())
            {
                Draw(mesh,art.cherryBlossomNectar,vh);float lo=float.PositiveInfinity,hi=float.NegativeInfinity;var v=new UIVertex();
                for(int i=0;i<vh.currentVertCount;i++){vh.PopulateUIVertex(ref v,i);lo=Math.Min(lo,v.position.x);hi=Math.Max(hi,v.position.x);}
                float originalWidth=(art.cherryBlossomNectar.vertices.Max(p=>p.x)-art.cherryBlossomNectar.vertices.Min(p=>p.x))*art.cherryBlossomNectar.pixelsPerUnit/art.cherryBlossomNectar.rect.width*57;
                Check(Math.Abs((hi-lo)/originalWidth-.3f)<.001,"minimum size 30 percent");
            }
            mesh.SetSurface(new FrameSurface(3,3));Check(Count(mesh,art.cherryBlossomNectar)==0&&Count(mesh,art.wallTwoArm)==0,"back surface clears all layers");
            UnityEngine.Object.DestroyImmediate(go);go=null;
            RenderExample(folder);
            File.WriteAllText(folder+"/result.txt","PASS "+checks+" sprite binding, topology, threshold, moisture, canonical storage/cargo, old save migration and rendering checks.");
            FrameLayoutTests.Run();
        }
        catch(Exception e){File.WriteAllText(folder+"/result.txt","FAIL "+e);Debug.LogException(e);if(Application.isBatchMode)EditorApplication.Exit(1);throw;}
        finally{if(go)UnityEngine.Object.DestroyImmediate(go);}
    }

    static void RenderExample(string folder)
    {
        var canvasGo = new GameObject("Preview", typeof(RectTransform), typeof(Canvas));
        var cameraGo = new GameObject("Preview camera", typeof(Camera));
        var camera = cameraGo.GetComponent<Camera>();
        camera.transform.position = new Vector3(0, 0, -100); camera.orthographic = true; camera.orthographicSize = 500;
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.16f, .16f, .16f);
        var rt = new RenderTexture(1800, 1000, 24); camera.targetTexture = rt;
        var canvas = canvasGo.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = camera; canvas.planeDistance = 100;
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Item/InteriorSlot/Frame/PF_Frame_Hand.prefab");
        var frame = new FrameData();
        var view = (UIItemPartView)ItemVisualFactory.Create(new ItemData(987651, "wall preview", ItemKind.Frame, frame), 0, ItemViewContext.Hand, null, prefab, canvasGo.transform);
        var presenter = view.GetComponent<FrameCellPresenter>();
        Check(presenter && presenter.Frame == frame, "existing hand prefab binds new renderer");
        int[,] cells = { {9,6}, {10,6}, {11,6}, {10,7}, {11,7}, {11,8}, {12,8}, {12,5}, {13,5} };
        for (int i = 0; i < cells.GetLength(0); i++) frame.Front.TrySetConstruction(cells[i,0], cells[i,1], 70);
        for (int i = 0; i < 5; i++)
        {
            frame.Front.TrySetConstruction(6 + i * 3, 3, (byte)(25 + 25 * i));
            frame.Front.TrySetConstruction(6 + i * 3, 10, 100);
            frame.Front.TryAddNectar(6 + i * 3, 10, i, 500, 500);
        }
        // Same source/amount of sugar, increasing moisture from left to right.
        for(int i=0;i<5;i++)
        {
            frame.Front.TrySetConstruction(6+i*3,1,100);
            frame.Front.TryAddNectar(6+i*3,1,2,200,i*200);
        }
        frame.Front.TrySetConstruction(12, 8, 100);
        Canvas.ForceUpdateCanvases(); presenter.Rebuild(); Canvas.ForceUpdateCanvases(); camera.Render();
        var old = RenderTexture.active; RenderTexture.active = rt;
        var capture = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false);
        capture.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0); capture.Apply();
        File.WriteAllBytes(folder + "/unity-preview.png", capture.EncodeToPNG());
        RenderTexture.active = old;
        frame.Flip(); Late(presenter);
        Check(presenter.transform.localScale.x < 0, "existing facing-side mirror retained");
        UnityEngine.Object.DestroyImmediate(capture); UnityEngine.Object.DestroyImmediate(canvasGo);
        UnityEngine.Object.DestroyImmediate(cameraGo); rt.Release(); UnityEngine.Object.DestroyImmediate(rt);
    }
}
