using System.Globalization;
using HoneyComb.Simulation.Hive;
using UnityEngine;
namespace HoneyComb.Unity.Presentation
{
    public sealed class CellConstructionDebugPanel
    {
        private Frame selected;
        private FrameSide side;
        private string xText="0",yText="0",constructionText="0",message;
        public void Draw(Frame frame,GUIStyle label,GUIStyle button)
        {
            if(frame==null) return;
            if(!ReferenceEquals(selected,frame)) { selected=frame;side=FrameSide.Front;xText="0";yText="0";message=null; }
            GUILayout.Space(12);GUILayout.Label("CELL CONSTRUCTION",label);
            GUILayout.Label($"Complete: {frame.CompletedCellCount:N0} / {frame.TotalCellCount:N0}\nAverage: {frame.AverageConstruction:0.00}%",label);
            GUILayout.BeginHorizontal();
            if(GUILayout.Button(side==FrameSide.Front ? "[Front]" : "Front",button,GUILayout.Height(50))) side=FrameSide.Front;
            if(GUILayout.Button(side==FrameSide.Back ? "[Back]" : "Back",button,GUILayout.Height(50))) side=FrameSide.Back;
            GUILayout.EndHorizontal();
            var surface=frame.GetSurface(side);
            GUILayout.Label($"{side}: {surface.CompletedCellCount:N0} / {surface.CellCount:N0}, avg {surface.AverageConstruction:0.00}%",label);
            var field=new GUIStyle(GUI.skin.textField){fontSize=24};
            GUILayout.BeginHorizontal();GUILayout.Label("X",label,GUILayout.Width(30));xText=GUILayout.TextField(xText,8,field,GUILayout.Width(160));
            GUILayout.Label("Y",label,GUILayout.Width(30));yText=GUILayout.TextField(yText,8,field,GUILayout.Width(160));GUILayout.EndHorizontal();
            bool valid=int.TryParse(xText,NumberStyles.None,CultureInfo.InvariantCulture,out int x)
                && int.TryParse(yText,NumberStyles.None,CultureInfo.InvariantCulture,out _);
            int.TryParse(yText,NumberStyles.None,CultureInfo.InvariantCulture,out int y);
            var cell=valid ? frame.GetCell(side,x,y) : null;
            GUILayout.Label(cell.HasValue ? $"({x},{y}): {cell.Value.Construction}% / {cell.Value.Content}" : $"Coordinates: X 0..{surface.Width-1}, Y 0..{surface.Height-1}",label);
            GUILayout.Label("Set construction (0..100%)",label);
            constructionText=GUILayout.TextField(constructionText,4,field,GUILayout.Height(48));
            bool validAmount=int.TryParse(constructionText,NumberStyles.None,CultureInfo.InvariantCulture,out int amount) && amount<=100;
            GUI.enabled=cell.HasValue && validAmount;
            if(GUILayout.Button("Apply to selected Cell",button,GUILayout.Height(54)))
                message=frame.TrySetConstruction(side,x,y,amount) ? "Cell construction updated." : "Rejected: occupied cells must remain at 100%.";
            GUI.enabled=validAmount;
            if(GUILayout.Button("Apply to ALL Cells (both sides)",button,GUILayout.Height(54)))
                message=frame.TrySetAllConstruction(amount) ? "Both sides updated." : "Rejected: occupied cells must remain at 100%. No changes applied.";
            GUI.enabled=true;
            if(!string.IsNullOrEmpty(message)) GUILayout.Label(message,label);
        }
    }
}
