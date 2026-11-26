using UnityEditor;
using UnityEngine;

public sealed partial class BeeCycleWindow
{
    BeeFrameImagePreview frameImage;
    bool showCellTextGrid;
    bool frameBeeVisible;
    Vector2 frameBeeUV;
    long frameBeeId;
    void CaptureFrameImage(GameWorld world,ItemData item,int selectedSide)
    {
        if(frameImage==null)frameImage=new BeeFrameImagePreview();frameImage.Capture(world,item,selectedSide);frameBeeVisible=false;
        if(world.BeeLocations.TryGet(selected,out var p)&&p.EquipmentId==item.Id&&p.Face==(selectedSide==0?BeeFace.MinusY:BeeFace.PlusY)&&p.X>=0&&p.X<HiveGridLayout.CombColumns&&p.Z>=0&&p.Z<HiveGridLayout.CombRows){
            var data=item.GetContent<FrameData>();int col=Mathf.Clamp(Mathf.FloorToInt((p.X+.5f)*data.Width/HiveGridLayout.CombColumns),0,data.Width-1),row=Mathf.Clamp(Mathf.FloorToInt((p.Z+.5f)*data.Height/HiveGridLayout.CombRows),0,data.Height-1);
            frameBeeUV=frameImage.CellUV(col,row);frameBeeId=selected;frameBeeVisible=true;
        }
    }
    void DrawFrameImage(bool interactive)
    {
        if(frameImage==null||!frameImage.Image){if(frameImage?.Error!=null)EditorGUILayout.HelpBox(frameImage.Error,MessageType.Warning);return;}
        float width=Mathf.Clamp(position.width-55,200,1100);var rect=GUILayoutUtility.GetRect(width,width*.75f,GUILayout.ExpandWidth(false));
        GUI.DrawTexture(rect,frameImage.Image,ScaleMode.StretchToFill,false);
        var uv=frameImage.CellUV(cellX,cellY);var center=new Vector2(rect.x+uv.x*rect.width,rect.y+(1-uv.y)*rect.height);
        Handles.BeginGUI();Handles.color=Color.cyan;Handles.DrawWireDisc(center,Vector3.forward,7);Handles.EndGUI();
        if(frameBeeVisible){var beeCenter=new Vector2(rect.x+frameBeeUV.x*rect.width,rect.y+(1-frameBeeUV.y)*rect.height);Handles.BeginGUI();Handles.color=Color.green;Handles.DrawWireDisc(beeCenter,Vector3.forward,10);Handles.EndGUI();GUI.Label(new Rect(beeCenter.x+12,beeCenter.y-10,150,22),$"벌 {frameBeeId} (이동 격자)");}
        if(interactive&&Event.current.type==EventType.MouseDown&&Event.current.button==0&&rect.Contains(Event.current.mousePosition)){
            var p=Event.current.mousePosition;
            if(frameImage.Pick(new Vector2((p.x-rect.x)/rect.width,1-(p.y-rect.y)/rect.height),out int col,out int row)){cellX=col;cellY=row;Event.current.Use();Repaint();}
        }
    }
}
