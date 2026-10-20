using HoneyComb.Simulation.Apiary;
using UnityEngine;
using HoneyComb.Simulation.Bees;
namespace HoneyComb.Unity.Presentation
{
    // UI-only selection and movement state; no Unity coordinates enter the model.
    public sealed class ApiaryGridDebugPanel
    {
        private int selectedX,selectedY;
        private int? moveX;
        private int moveY;
        private string movingHiveId;
        private string message;
        private Vector2 scroll;
        private readonly BeeMoveSelection beeMove=new BeeMoveSelection();
        private readonly ApiaryPlacesDebugPanel placesPanel=new ApiaryPlacesDebugPanel();
        private readonly HiveStructureDebugPanel structurePanel=new HiveStructureDebugPanel();
        public void Draw(ApiaryState apiary,BeePopulation population,long hour,BeeSpawnSettings defaults,GUIStyle label,GUIStyle button)
        {
            GUILayout.Label("APIARY GRID",label);
            GUILayout.Label($"{apiary.Name}\nGrid: {apiary.Width} x {apiary.Height}\nHives: {apiary.HiveCount} / {apiary.CellCount}",label);
            var cellStyle=new GUIStyle(button){fontSize=18};
            scroll=GUILayout.BeginScrollView(scroll,GUILayout.Height(350));
            GUILayout.BeginHorizontal(); GUILayout.Space(60);
            for(int x=0;x<apiary.Width;x++) GUILayout.Label($"X{x}",label,GUILayout.Width(76));
            GUILayout.EndHorizontal();
            for(int y=apiary.Height-1;y>=0;y--)
            {
                GUILayout.BeginHorizontal(); GUILayout.Label($"Y{y}",label,GUILayout.Width(56));
                for(int x=0;x<apiary.Width;x++)
                {
                    bool selected=selectedX==x && selectedY==y;
                    string content=apiary.GetHive(x,y)==null ? "." : "H";
                    if(GUILayout.Button(selected ? "["+content+"]" : content,cellStyle,GUILayout.Width(76),GUILayout.Height(60)))
                    { selectedX=x; selectedY=y; message=""; }
                }
                GUILayout.EndHorizontal();
            }
            GUILayout.EndScrollView();
            var hive=apiary.GetHive(selectedX,selectedY);
            GUILayout.Label($"Selected: ({selectedX}, {selectedY})\nHive: {(hive==null ? "Empty" : hive.Id)}",label);
            GUI.enabled=hive==null;
            if(GUILayout.Button("Place Hive here",button,GUILayout.Height(60)))
                message=apiary.TryCreateHive(selectedX,selectedY,out _) ? "Hive placed." : "Placement rejected.";
            GUI.enabled=hive!=null;
            if(GUILayout.Button("Remove Hive here",button,GUILayout.Height(60)))
            { message=apiary.RemoveHive(selectedX,selectedY) ? "Hive removed." : "Cannot remove a hive with a colony or resident bees."; moveX=null; }
            if(GUILayout.Button("Move: select this hive",button,GUILayout.Height(60)))
            { moveX=selectedX; moveY=selectedY; movingHiveId=hive.Id; message="Select an empty destination, then Move here."; }
            GUI.enabled=true;
            if(moveX.HasValue)
            {
                GUILayout.Label($"Moving from ({moveX.Value}, {moveY})",label);
                GUI.enabled=apiary.GetHive(selectedX,selectedY)==null;
                if(GUILayout.Button("Move here",button,GUILayout.Height(60)))
                {
                    bool valid=apiary.GetHive(moveX.Value,moveY)?.Id==movingHiveId;
                    bool moved=valid && apiary.MoveHive(moveX.Value,moveY,selectedX,selectedY);
                    message=moved ? "Hive moved (same ID)." : "Move rejected; source or destination changed.";
                    moveX=null;
                }
                GUI.enabled=true;
                if(GUILayout.Button("Cancel move",button,GUILayout.Height(60))) moveX=null;
            }
            if(!string.IsNullOrEmpty(message)) GUILayout.Label(message,label);
            placesPanel.Draw(apiary,population,apiary.GetHive(selectedX,selectedY),selectedX,selectedY,hour,defaults,beeMove,label,button);
            structurePanel.Draw(apiary,apiary.GetHive(selectedX,selectedY),population,hour,defaults,beeMove,label,button);
        }
    }
}
