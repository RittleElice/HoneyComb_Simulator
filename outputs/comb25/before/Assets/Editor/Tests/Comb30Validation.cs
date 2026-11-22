using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
public static class Comb30Validation
{
    static int checks;
    static readonly string Folder = "outputs/comb30";
    static void Check(bool ok,string label){checks++;if(!ok)throw new Exception(label);}
    static void Near(double a,double b,double tolerance,string label){Check(Math.Abs(a-b)<=tolerance,label+": "+a+" vs "+b);}
    [MenuItem("Tools/HoneyComb/검증/Run Comb 30x15 Validation")]
    public static void Run()
    {
        Directory.CreateDirectory(Folder);
        try
        {
            var asset=Resources.Load<CombBalanceSettings>("CombBalanceSettings");
            Check(asset!=null && asset.TryApply(out _),"runtime balance asset loads");
            Near(CombBalance.LiquidCapacityMl*900,1989,.001,"whole-frame liquid capacity");
            Near(CombBalance.PollenCapacityMg*900,1326000,.1,"whole-frame pollen capacity");
            var defaults=new CombBalanceParameters();
            Near(defaults.CellLiquidCapacityMl,CombBalance.LiquidCapacityMl,1e-6,"asset and code liquid agree");
            Near(defaults.CellPollenCapacityMg,CombBalance.PollenCapacityMg,.001,"asset and code pollen agree");
            CombGeometryTests.RunAll(); CellStateTests.RunAll(); CombThermalTests.RunAll();
            ContentThermalIntegrationTests.Run(ThermalMaterials.Load()); WorldSaveTests.Run(); BeeSurfaceTests.Run();
            var f=new FrameData();var item=new ItemData(93000,"comb30",ItemKind.Frame,f);
            Check(f.TotalCellCount==900,"900 cells");
            Near(item.EquipmentGrid.X.Last()-item.EquipmentGrid.X.First(),440,1e-8,"frame width unchanged");
            Near(item.EquipmentGrid.Z.Last()-item.EquipmentGrid.Z.First(),220,1e-8,"frame height unchanged");
            double water=0,pollen=0;
            foreach(var side in new[]{FrameSide.Front,FrameSide.Back})
            {
                var s=f.GetSurface(side);s.SetAllConstruction(100);
                for(int y=0;y<15;y++)for(int x=0;x<30;x++)
                {
                    Check(s.TryAddNectar(x,y,0,0,2210),"full liquid deposit");
                    Check(!s.TryAddNectar(x,y,0,0,1),"liquid overflow rejected");water+=s.GetWaterMassMg(x,y);
                    s.TryClearContent(x,y);
                    Check(s.TryAddPollen(x,y,(float)CombBalance.PollenCapacityMg),"full pollen deposit");
                    Check(!s.TryAddPollen(x,y,1),"pollen overflow rejected");pollen+=s.GetPollenMassMg(x,y);
                    s.TryClearContent(x,y);
                    Check(s.TrySetBrood(x,y,1+x+30*y+(int)side*450),"one brood per cell");
                    Check(!s.TrySetBrood(x,y,999999),"second brood rejected");s.TryClearContent(x,y);
                }
            }
            Near(water,1989000,.1,"full frame stored liquid mass");Near(pollen,1326000,.1,"full frame stored pollen mass");
            var wet=f.Front;
            for(int y=0;y<5;y++)for(int x=0;x<6;x++)Check(wet.TryAddNectar(x,y,0,100,100),"wet region setup");
            var before=wet.GetThermalRegion(0,0);
            var applyWater=typeof(FrameSurface).GetMethod("ApplyClimateWater",BindingFlags.Instance|BindingFlags.NonPublic);
            Near((double)applyWater.Invoke(wet,new object[]{0,0,.5}),-.0015,1e-10,"region water loss reported in kg");
            var after=wet.GetThermalRegion(0,0);
            Near(before.WaterMg-after.WaterMg,1500,1e-6,"region water balance");
            Near(after.SugarMg,before.SugarMg,1e-6,"evaporation preserves sugar");
            for(int y=0;y<5;y++)for(int x=0;x<6;x++){Near(wet.GetWaterMassMg(x,y),50,1e-6,"water propagated to every member");wet.TryClearContent(x,y);}
            item.ThermalState.Refresh(ThermalMaterials.Load(),20);
            Near(item.ThermalState.WaxMassKg.Sum(v=>(double)v),.04598+.09945,2e-7,"foundation plus preserved worker wax");
            foreach(var side in new[]{FrameSide.Front,FrameSide.Back})for(int y=0;y<15;y++)for(int x=0;x<30;x++)f.GetSurface(side).TrySetType(x,y,CombCellType.Drone);
            item.ThermalState.Refresh(ThermalMaterials.Load(),20);
            Near(item.ThermalState.WaxMassKg.Sum(v=>(double)v),.04598+.1326,2e-7,"preserved drone wax");
            f.Front.SetAllConstruction(50);f.Back.SetAllConstruction(50);item.ThermalState.Refresh(ThermalMaterials.Load(),20);
            Near(item.ThermalState.WaxMassKg.Sum(v=>(double)v),.04598+.0663,2e-7,"partial construction wax");
            Environment.SetEnvironmentVariable("REDUCED_PRODUCTION_REPORT",Path.GetFullPath(Folder+"/integration"));
            ReducedGridIntegrationTests.Run();
            ValidateUI();
            File.WriteAllText(Folder+"/result.txt","PASS: "+checks+" additional assertions; geometry, cell states, material capacity, content thermal integration, save integrity, 60-minute three-tier thermal/moisture simulation and resume. Schema 16 only; 900 cells; liquid 1989 mL; pollen 1326 g; worker constructed wax 99.45 g plus unchanged foundation. UI prefab rendered and all 900 front/back centre hit tests passed.\n");
            Debug.Log("COMB30 PASS "+checks);if(Application.isBatchMode)EditorApplication.Exit(0);
        }
        catch(Exception e){File.WriteAllText(Folder+"/result.txt",e.ToString());Debug.LogException(e);if(Application.isBatchMode)EditorApplication.Exit(1);else throw;}
        finally{ThermalExchange.ReleaseAllBuffers();}
    }
    static void ValidateUI()
    {
        var camObject=new GameObject("Comb30 preview camera");var cam=camObject.AddComponent<Camera>();
        cam.orthographic=true;cam.orthographicSize=550;cam.transform.position=new Vector3(0,0,-100);cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=new Color(.15f,.17f,.2f);
        var rt=new RenderTexture(1200,1100,24);cam.targetTexture=rt;
        var canvasObject=new GameObject("Comb30 preview canvas",typeof(RectTransform),typeof(Canvas));var canvas=canvasObject.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=cam;canvas.planeDistance=100;
        var go=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Item/InteriorSlot/Frame/PF_Frame_Hand.prefab"),canvasObject.transform);
        var presenter=go.GetComponent<FrameCellPresenter>();var f=new FrameData();f.Front.SetAllConstruction(100);f.Back.SetAllConstruction(80);
        f.Front.TryAddNectar(0,0,0,100,100);f.Back.TryAddPollen(29,14,100);
        var item=new ItemData(94000,"UI test",ItemKind.Frame,f);Canvas.ForceUpdateCanvases();presenter.BindItem(item);Canvas.ForceUpdateCanvases();
        var area=(RectTransform)typeof(FrameCellPresenter).GetField("cellArea",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(presenter);
        float scale=Mathf.Min(area.rect.width/420,area.rect.height/200);var geometry=new CombGeometry();
        foreach(var side in new[]{FrameSide.Front,FrameSide.Back})
        {
            f.SetFacingSide(side);presenter.Rebuild();Canvas.ForceUpdateCanvases();
            Check(go.GetComponentsInChildren<FrameCellMesh>().Length==5,"450 cells use five prefab mesh batches");
            for(int y=0;y<15;y++)for(int x=0;x<30;x++)
            {geometry.Center(x,y,out double cx,out double cz);var p=area.rect.center+new Vector2((float)(cx-210)*scale,(float)(cz-100)*scale);Check(presenter.TryGetCellAtLocalPoint(p,out var id)&&id.Column==x&&id.Row==y&&id.Side==side,"prefab cell hit identity");}
            Check(!presenter.TryGetCellAtLocalPoint(area.rect.center+new Vector2(0,-100*scale),out _),"margin not selectable");
            cam.Render();RenderTexture.active=rt;var texture=new Texture2D(1200,1100,TextureFormat.RGB24,false);texture.ReadPixels(new Rect(0,0,1200,1100),0,0);texture.Apply();File.WriteAllBytes(Folder+"/"+side+".png",texture.EncodeToPNG());UnityEngine.Object.DestroyImmediate(texture);RenderTexture.active=null;
        }
        UnityEngine.Object.DestroyImmediate(canvasObject);UnityEngine.Object.DestroyImmediate(camObject);rt.Release();UnityEngine.Object.DestroyImmediate(rt);
    }
}
