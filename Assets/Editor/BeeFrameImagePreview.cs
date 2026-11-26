using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Uses the shipped frame prefab and its cell renderer; never changes FrameData.FacingSide.
internal sealed class BeeFrameImagePreview : IDisposable
{
    internal const string PrefabPath="Assets/Prefabs/Item/InteriorSlot/Frame/PF_Frame_Hand.prefab";
    const int Width=1024,Height=768;
    Scene scene;
    GameObject root,view,cameraObject;
    Camera camera;
    RenderTexture texture;
    FrameCellPresenter presenter;
    RectTransform area,visual;
    float scale;
    GameWorld lastWorld;
    ItemData lastItem;
    int lastSide=-1,lastRevision=-1;
    long lastMinute=-1;
    internal Texture Image=>texture;
    internal int RenderCount {get;private set;}
    internal string Error {get;private set;}
    static readonly BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
    internal bool Capture(GameWorld world,ItemData item,int side,bool force=false)
    {
        var frame=item?.GetContent<FrameData>();if(frame==null)return false;
        var surface=side==0?frame.Front:frame.Back;
        if(!force&&texture&&ReferenceEquals(world,lastWorld)&&ReferenceEquals(item,lastItem)&&side==lastSide&&surface.Revision==lastRevision&&world.CurrentGameMinute==lastMinute)return true;
        try{
            if(!root)Create();
            presenter.BindBroodRuntime(world.BeeCycle);presenter.BindItem(item);
            view.transform.localScale=new Vector3(side==0?.72f:-.72f,.72f,.72f);
            foreach(var mesh in presenter.GetComponentsInChildren<FrameCellMesh>(true))mesh.SetSurface(surface,world.BeeCycle);
            foreach(var t in root.GetComponentsInChildren<Transform>(true)){t.gameObject.layer=31;t.gameObject.hideFlags=HideFlags.HideAndDontSave;}
            visual=(RectTransform)typeof(FrameCellPresenter).GetField("visualRoot",Flags).GetValue(presenter);
            scale=(float)typeof(FrameCellPresenter).GetField("displayScale",Flags).GetValue(presenter);
            Canvas.ForceUpdateCanvases();camera.Render();
            lastWorld=world;lastItem=item;lastSide=side;lastRevision=surface.Revision;lastMinute=world.CurrentGameMinute;RenderCount++;Error=null;return true;
        }catch(Exception e){Dispose();Error="소비장 이미지 표시 실패: "+e.Message;return false;}
    }
    void Create()
    {
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);if(!prefab)throw new InvalidOperationException("소비장 원본 프리팹을 찾을 수 없습니다.");
        scene=EditorSceneManager.NewPreviewScene();
        root=new GameObject("Bee observation preview",typeof(RectTransform),typeof(Canvas));root.hideFlags=HideFlags.HideAndDontSave;SceneManager.MoveGameObjectToScene(root,scene);
        cameraObject=new GameObject("Bee observation camera",typeof(Camera));cameraObject.hideFlags=HideFlags.HideAndDontSave;SceneManager.MoveGameObjectToScene(cameraObject,scene);
        camera=cameraObject.GetComponent<Camera>();camera.enabled=false;camera.scene=scene;camera.transform.position=new Vector3(0,0,-100);camera.orthographic=true;camera.orthographicSize=Height/2f;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.12f,.12f,.12f,1);camera.cullingMask=1<<31;
        texture=new RenderTexture(Width,Height,24,RenderTextureFormat.ARGB32){hideFlags=HideFlags.HideAndDontSave,name="Selected comb image"};texture.Create();camera.targetTexture=texture;
        var canvas=root.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=100;canvas.additionalShaderChannels|=AdditionalCanvasShaderChannels.TexCoord1;
        view=UnityEngine.Object.Instantiate(prefab,root.transform,false);view.name="Selected real frame";
        foreach(var behavior in view.GetComponentsInChildren<MonoBehaviour>(true))if(!(behavior is Graphic))behavior.enabled=false;
        presenter=view.GetComponent<FrameCellPresenter>();if(!presenter)throw new InvalidOperationException("소비장 표시 구성 요소가 없습니다.");
        area=(RectTransform)typeof(FrameCellPresenter).GetField("cellArea",Flags).GetValue(presenter);
        foreach(var t in root.GetComponentsInChildren<Transform>(true)){t.gameObject.layer=31;t.gameObject.hideFlags=HideFlags.HideAndDontSave;}
        Canvas.ForceUpdateCanvases();
    }
    internal bool Pick(Vector2 uv,out int column,out int row)
    {
        column=row=-1;if(!presenter||!camera||!area)return false;
        if(!RectTransformUtility.ScreenPointToLocalPointInRectangle(area,new Vector2(uv.x*Width,uv.y*Height),camera,out var local)||!presenter.TryGetCellAtLocalPoint(local,out var id))return false;
        column=id.Column;row=id.Row;return true;
    }
    internal Vector2 CellUV(int column,int row)
    {
        if(!visual||lastItem==null)return Vector2.zero;
        var data=lastItem.GetContent<FrameData>();new CombGeometry(data.Width,data.Height).Center(column,row,out double x,out double y);
        var local=new Vector3(((float)x-(float)CombGeometry.AreaWidth/2)*scale,((float)y-(float)CombGeometry.AreaHeight/2)*scale,0);
        return camera.WorldToViewportPoint(visual.TransformPoint(local));
    }
    public void Dispose()
    {
        if(camera)camera.targetTexture=null;
        if(texture){texture.Release();UnityEngine.Object.DestroyImmediate(texture);}texture=null;
        if(scene.IsValid())EditorSceneManager.ClosePreviewScene(scene);
        root=view=cameraObject=null;presenter=null;area=visual=null;camera=null;lastWorld=null;lastItem=null;
    }
}
