using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class InitialNectarPaletteTests
{
    static void Check(bool ok,string text){if(!ok)throw new Exception(text);}
    public static void Run()
    {
        string folder="outputs/initial-nectar";Directory.CreateDirectory(folder);
        GameWorld world=null;
        try
        {
            var report=new System.Text.StringBuilder();
            for(int test=0;test<3;test++)
            {
                world=new GameWorld(new WeatherSimulator(WeatherSimulator.DefaultSeed,WeatherAssetLoader.LoadBaseline()));
                var settings=JsonUtility.FromJson<BeeCycleSettings>(JsonUtility.ToJson(Resources.Load<BeeCycleSettingsAsset>("BeeCycleParameters").values));
                settings.UpgradeForageFormula();settings.UpgradeRelocationFormulas();
                if(test==1)settings.demoWater=0;
                if(test==2)settings.demoSugar=0;
                world.BeeCycle.SeedDemo(settings);
                double sugar=0,water=0,pollen=0,min=1,max=0;int cells=0;
                var frames=ThermalWorld.Items(world).Where(i=>i.Kind==ItemKind.Frame&&i.GetContent<FrameData>().Front.CountCompletedCells()>0).ToArray();
                var sources=new HashSet<int>();var contents=new List<KeyValuePair<int,float>>();
                foreach(var frame in frames)
                foreach(var side in new[]{frame.GetContent<FrameData>().Front,frame.GetContent<FrameData>().Back})
                {
                    var faceSources=new HashSet<int>();
                    for(int y=0;y<side.Height;y++)for(int x=0;x<side.Width;x++)
                    {
                        double s=side.GetTotalSugarMassMg(x,y),w=side.GetWaterMassMg(x,y);
                        sugar+=s;water+=w;pollen+=side.GetPollenMassMg(x,y);
                        if(s+w==0)continue;
                        cells++;Check(side.GetCapacityFillRatio(x,y)<=1.000001,"capacity exceeded");
                        side.CopyNectarSugars(x,y,contents);
                        foreach(var entry in contents){sources.Add(entry.Key);faceSources.Add(entry.Key);Check(entry.Key>=0&&entry.Key<=4,"canonical source");}
                        min=Math.Min(min,w/(s+w));max=Math.Max(max,w/(s+w));
                    }
                    if(test==0)Check(faceSources.Count==5,"all five sources on every face");
                }
                Check(Math.Abs(sugar-settings.demoSugar)<.01,"sugar total");Check(Math.Abs(water-settings.demoWater)<.01,"water total");Check(Math.Abs(pollen-settings.demoPollen)<.01,"pollen unchanged");
                if(test==0){Check(sources.Count==5&&max-min>.2,"visible source/moisture variety");Render(frames[0],folder);}
                if(test==1)Check(water==0,"pure sugar edge case");if(test==2)Check(sugar==0,"pure water edge case");
                report.AppendLine($"PASS case {test}: nectar cells={cells}, sources={sources.Count}, sugar={sugar:R}mg, water={water:R}mg, pollen={pollen:R}mg, moisture={min:P2}..{max:P2}");
                world.Thermal?.Dispose();world=null;
            }
            File.WriteAllText(folder+"/result.txt",report.ToString());EditorApplication.Exit(0);
        }
        catch(Exception e){File.WriteAllText(folder+"/result.txt","FAIL "+e);Debug.LogException(e);EditorApplication.Exit(1);}
        finally{world?.Thermal?.Dispose();}
    }
    static void Render(ItemData frame,string folder)
    {
        var root=new GameObject("Initial nectar preview",typeof(RectTransform),typeof(Canvas));
        var cg=new GameObject("Camera",typeof(Camera));var camera=cg.GetComponent<Camera>();camera.transform.position=new Vector3(0,0,-100);camera.orthographic=true;camera.orthographicSize=350;camera.backgroundColor=new Color(.15f,.15f,.15f);camera.clearFlags=CameraClearFlags.SolidColor;
        var rt=new RenderTexture(1200,700,24);camera.targetTexture=rt;
        var canvas=root.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=100;
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Item/InteriorSlot/Frame/PF_Frame_Hand.prefab");
        var view=(UIItemPartView)ItemVisualFactory.Create(frame,0,ItemViewContext.Hand,null,prefab,root.transform);
        Canvas.ForceUpdateCanvases();view.GetComponent<FrameCellPresenter>().Rebuild();Canvas.ForceUpdateCanvases();camera.Render();
        var previous=RenderTexture.active;RenderTexture.active=rt;var image=new Texture2D(1200,700,TextureFormat.RGBA32,false);image.ReadPixels(new Rect(0,0,1200,700),0,0);image.Apply();File.WriteAllBytes(folder+"/preview.png",image.EncodeToPNG());RenderTexture.active=previous;
        UnityEngine.Object.DestroyImmediate(image);UnityEngine.Object.DestroyImmediate(root);UnityEngine.Object.DestroyImmediate(cg);rt.Release();UnityEngine.Object.DestroyImmediate(rt);
    }
}
