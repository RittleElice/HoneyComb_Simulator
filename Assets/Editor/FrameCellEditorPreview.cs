using System;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Ephemeral edit-mode artwork. Never binds or mutates the presenter's real frame.
internal sealed class FrameCellEditorPreview : IDisposable
{
    ulong sampleSeed=WeatherSimulator.DefaultSeed;
    GameObject root;
    Canvas parentCanvas;
    AdditionalCanvasShaderChannels previousChannels;
    FrameSurface surface;
    readonly Dictionary<long,(CycleStage stage,float progress)> brood=new Dictionary<long,(CycleStage,float)>();
    string lastSettings;
    Vector2 lastSize;
    RectTransform lastArea;
    static readonly FieldInfo Art=typeof(FrameCellPresenter).GetField("artwork",BindingFlags.Instance|BindingFlags.NonPublic);
    internal GameObject Root=>root;

    internal void Regenerate(FrameCellPresenter presenter)
    {
        ulong next;
        do { next=BitConverter.ToUInt64(Guid.NewGuid().ToByteArray(),0); } while((int)next==(int)sampleSeed);
        SetSeed(presenter,next);
    }
    internal void ResetSamples(FrameCellPresenter presenter)=>SetSeed(presenter,WeatherSimulator.DefaultSeed);
    internal void SetSeed(FrameCellPresenter presenter,ulong seed)
    {
        Dispose();sampleSeed=seed;Update(presenter);
    }

    internal void Update(FrameCellPresenter presenter)
    {
        if(!presenter||EditorUtility.IsPersistent(presenter)||Application.isPlaying||!presenter.isActiveAndEnabled||presenter.Frame!=null){Dispose();return;}
        var data=new SerializedObject(presenter);
        var area=data.FindProperty("cellArea").objectReferenceValue as RectTransform;
        if(!area||!data.FindProperty("showCells").boolValue||area.rect.width<=0||area.rect.height<=0){Dispose();return;}
        string settings=EditorJsonUtility.ToJson(presenter);
        if(root&&lastSettings==settings&&lastArea==area&&lastSize==area.rect.size)return;
        ClearRoot();
        if(surface==null)CreateSamples();
        lastSettings=settings;lastArea=area;lastSize=area.rect.size;
        root=new GameObject("소방 예시 (저장되지 않음)",typeof(RectTransform));
        StageUtility.PlaceGameObjectInCurrentStage(root);
        root.hideFlags=HideFlags.HideAndDontSave;
        root.transform.SetParent(area,false);
        parentCanvas=area.GetComponentInParent<Canvas>();
        if(parentCanvas){previousChannels=parentCanvas.additionalShaderChannels;parentCanvas.additionalShaderChannels|=AdditionalCanvasShaderChannels.TexCoord1;}
        EditorSceneManager.sceneSaving+=BeforeSceneSave;
        PrefabStage.prefabSaving+=BeforePrefabSave;
        var rect=(RectTransform)root.transform;rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(.5f,.5f);rect.sizeDelta=area.rect.size;
        rect.anchoredPosition=data.FindProperty("cellOffset").vector2Value;
        var size=data.FindProperty("cellScale").vector2Value;rect.localScale=new Vector3(size.x*(data.FindProperty("flipCellsX").boolValue?-1:1),size.y,1);
        var geometry=new CombGeometry(surface.Width,surface.Height);
        float fit=Mathf.Min(area.rect.width/(float)CombGeometry.AreaWidth,area.rect.height/(float)CombGeometry.AreaHeight)*F(data,"areaFill");
        float radius=(float)geometry.Radius*fit;
        var origin=new Vector2(-(float)geometry.Width*fit/2,-(float)geometry.Height*fit/2);
        var topology=FrameCellMesh.BuildJunctions(surface.Width,surface.Height,radius,origin);
        var art=(FrameCellArtwork)Art.GetValue(presenter);
        int chunk=Mathf.Clamp(data.FindProperty("cellsPerMesh").intValue,50,500);
        for(int start=0;start<surface.CellCount;start+=chunk)
        {
            var go=new GameObject("예시 소방",typeof(RectTransform),typeof(CanvasRenderer));go.hideFlags=HideFlags.HideAndDontSave;go.transform.SetParent(root.transform,false);
            var child=(RectTransform)go.transform;child.anchorMin=Vector2.zero;child.anchorMax=Vector2.one;child.offsetMin=child.offsetMax=Vector2.zero;
            var mesh=go.AddComponent<FrameCellMesh>();var angles=data.FindProperty("eggRotationRange").vector2Value;
            mesh.ConfigureArt(surface.Width,start,Math.Min(surface.CellCount,start+chunk),radius,origin,topology,
                F(data,"wallMinimumAlpha"),F(data,"wallMaximumAlpha"),F(data,"nectarMinimumScale"),art,F(data,"representativeNectarPercent"),F(data,"dryNectarAlpha"),F(data,"wetNectarAlpha"),F(data,"pollenMinimumScale"),F(data,"nectarMaximumScale"),F(data,"pollenMaximumScale"),F(data,"larvaMinimumScale"),F(data,"larvaMaximumScale"),C(data,"pupaStartColor"),C(data,"pupaEndColor"),F(data,"completeCellOpacity"),angles.x,angles.y,C(data,"completeCellColor"));
            mesh.SetEditorPreview(surface,brood);
        }
        foreach(var t in root.GetComponentsInChildren<Transform>(true))t.gameObject.hideFlags=HideFlags.HideAndDontSave;
        Canvas.ForceUpdateCanvases();SceneView.RepaintAll();
    }
    static float F(SerializedObject data,string name)=>data.FindProperty(name).floatValue;
    static Color C(SerializedObject data,string name)=>data.FindProperty(name).colorValue;
    void CreateSamples()
    {
        // Reuse the new-game initializer in an isolated, non-ticking model. No
        // GameBootstrap binding, save writes, or modification of the live world.
        var world=new GameWorld(new WeatherSimulator(sampleSeed,WeatherAssetLoader.LoadBaseline()));
        try
        {
            world.BeeCycle.SeedDemo();
            var item=ThermalWorld.Items(world).FirstOrDefault(i=>i.Kind==ItemKind.Frame&&i.GetContent<FrameData>().Front.CountCompletedCells()>0);
            if(item==null)throw new InvalidOperationException("초기 예시를 표시할 소비장이 없습니다.");
            surface=item.GetContent<FrameData>().Front;
            brood.Clear();
            foreach(var bee in world.BeeCycle.Bees)
                if(world.BeeCycle.TryGetObservationDevelopment(bee.id,out var stage,out double progress))
                    brood[bee.id]=(stage,(float)progress);
        }
        finally { world.Thermal?.Dispose(); }
    }
    void BeforeSceneSave(UnityEngine.SceneManagement.Scene scene,string path){if(root&&root.scene==scene)ClearRoot();}
    void BeforePrefabSave(GameObject contents){if(root&&root.scene==contents.scene)ClearRoot();}
    void ClearRoot()
    {
        EditorSceneManager.sceneSaving-=BeforeSceneSave;PrefabStage.prefabSaving-=BeforePrefabSave;
        if(root)UnityEngine.Object.DestroyImmediate(root);root=null;
        if(parentCanvas)parentCanvas.additionalShaderChannels=previousChannels;parentCanvas=null;
    }
    public void Dispose(){ClearRoot();surface=null;brood.Clear();lastSettings=null;lastArea=null;}
}


