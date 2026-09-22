using HoneyComb.Simulation.Apiary;
using HoneyComb.Simulation.Bees;
using HoneyComb.Simulation.Hive;
using UnityEngine;
namespace HoneyComb.Unity.Presentation
{
    // Presentation-only navigation. Simulation remains the source of truth.
    public sealed class ObservationPanel
    {
        private enum Page { Apiary, Hive, Frame }
        private Page page;
        private HiveState hive;
        private string superId;
        private int slot,x,y,frameTab;
        private Vector2 scroll,gridScroll;
        private readonly BeeMoveSelection beeMove=new BeeMoveSelection();
        private readonly ApiaryPlacesDebugPanel places=new ApiaryPlacesDebugPanel();
        private readonly BeeDebugPanel bees=new BeeDebugPanel();
        private readonly CellConstructionDebugPanel construction=new CellConstructionDebugPanel();
        private HiveState carryingHive;
        private HiveState sourceHive;
        private string sourceSuper;
        private int sourceSlot;
        private Frame sourceFrame;
        private bool showPlaces;
        private string message;
        private static readonly Color Honey=new Color(.94f,.65f,.20f);
        private static readonly Color Green=new Color(.24f,.57f,.44f);
        private static readonly Color Slate=new Color(.19f,.25f,.32f);
        private static readonly Color Selected=new Color(.42f,.66f,.83f);
        private GUIStyle title,label,button,small;
        private void Go(Page target) { page=target;scroll=Vector2.zero;message=null; }
        private bool Action(string text,bool enabled=true)
        { GUI.enabled=enabled;bool pressed=GUILayout.Button(text,button,GUILayout.Height(50));GUI.enabled=true;return pressed; }
        private void Info(string text) => GUILayout.Label(text,label);
        public void Draw(ApiaryState apiary,BeePopulation population,long hour,BeeSpawnSettings defaults,float width,float height)
        {
            title=new GUIStyle(GUI.skin.label){fontSize=28,fontStyle=FontStyle.Bold,wordWrap=true};
            label=new GUIStyle(GUI.skin.label){fontSize=20,wordWrap=true};
            button=new GUIStyle(GUI.skin.button){fontSize=20,wordWrap=true};
            small=new GUIStyle(GUI.skin.label){fontSize=16,alignment=TextAnchor.MiddleCenter,wordWrap=true};
            if(hive!=null && !ReferenceEquals(population.FindHive(hive.Id),hive)) { hive=null;page=Page.Apiary; }
            if(sourceFrame!=null && !ReferenceEquals(sourceHive.GetSuper(sourceSuper)?.GetSlot(sourceSlot)?.Frame,sourceFrame)) sourceFrame=null;
            GUILayout.BeginArea(new Rect(0,0,width,height));
            GUILayout.BeginHorizontal();
            if(GUILayout.Button("Apiary",button,GUILayout.Height(48))) Go(Page.Apiary);
            if(page!=Page.Apiary && GUILayout.Button("< Hive",button,GUILayout.Height(48))) Go(Page.Hive);
            GUILayout.EndHorizontal();
            GUILayout.Label(page==Page.Apiary ? apiary.Name : page==Page.Hive ? "HIVE / "+hive.Id : "FRAME / SLOT "+slot,title);
            if(carryingHive!=null)
            {
                Info("Moving Hive: "+carryingHive.Id);
                if(Action("Cancel Hive move")) carryingHive=null;
            }
            if(sourceFrame!=null)
            {
                Info("Carrying Frame: "+sourceFrame.Id);
                if(Action("Cancel Frame move")) sourceFrame=null;
            }
            if(beeMove.BeeId.HasValue) Info("Bee #"+beeMove.BeeId.Value+" selected for movement");
            scroll=GUILayout.BeginScrollView(scroll);
            if(page==Page.Apiary) DrawApiary(apiary,population,hour,defaults);
            else if(page==Page.Hive) DrawHive(apiary,population,hour,defaults);
            else DrawFrame(population,hour,defaults);
            if(!string.IsNullOrEmpty(message)) Info(message);
            GUILayout.EndScrollView();GUILayout.EndArea();
        }
        private void DrawApiary(ApiaryState apiary,BeePopulation population,long hour,BeeSpawnSettings defaults)
        {
            Info($"{apiary.Width} x {apiary.Height} plots    |    {apiary.HiveCount} hives    |    {population.Bees.Count} bees");
            Info("Select a plot. Gold = hive, green = open ground.");
            gridScroll=GUILayout.BeginScrollView(gridScroll,GUILayout.Height(380));
            for(int row=apiary.Height-1;row>=0;row--)
            {
                GUILayout.BeginHorizontal();
                for(int col=0;col<apiary.Width;col++)
                {
                    var here=apiary.GetHive(col,row);var rect=GUILayoutUtility.GetRect(96,112,GUILayout.Width(96),GUILayout.Height(112));
                    var color=GUI.backgroundColor;GUI.backgroundColor=(x==col&&y==row) ? Selected : here==null ? Green : Honey;
                    if(GUI.Button(rect,"",button)) { x=col;y=row;showPlaces=false; }
                    GUI.backgroundColor=color;
                    if(here!=null)
                    {
                        for(int stack=0;stack<3;stack++) Paint(new Rect(rect.x+27,rect.y+12+stack*11,42,9),new Color(.30f,.20f,.10f));
                    }
                    else Paint(new Rect(rect.x+25,rect.y+26,46,4),Green);
                    GUI.Label(new Rect(rect.x,rect.y+49,rect.width,56),$"({col},{row})\n{(here==null ? "Empty" : "Hive: "+Residents(here)+" bees")}\nOut: {population.GetBeesOutdoors(col,row).Count}",small);
                }
                GUILayout.EndHorizontal();
            }
            GUILayout.EndScrollView();
            var selected=apiary.GetHive(x,y);
            GUILayout.BeginVertical(GUI.skin.box);
            Info($"SELECTED PLOT ({x}, {y})");
            if(selected==null)
            {
                if(Action("+ Place Hive")) apiary.TryCreateHive(x,y,out _);
                if(carryingHive!=null && Action("Move selected Hive here"))
                {
                    bool moved=false;
                    for(int row=0;row<apiary.Height&&!moved;row++) for(int col=0;col<apiary.Width&&!moved;col++)
                        if(ReferenceEquals(apiary.GetHive(col,row),carryingHive)) moved=apiary.MoveHive(col,row,x,y);
                    message=moved ? "Hive moved." : "Hive move rejected.";carryingHive=null;
                }
            }
            else
            {
                Info($"{selected.Id}\n{selected.Supers.Count} supers | {Residents(selected)} bees inside");
                if(Action("Open Hive")) { hive=selected;Go(Page.Hive); }
                if(Action("Select Hive to move")) carryingHive=selected;
                if(Action("Remove empty Hive",!selected.HasColony&&!selected.HasResidentBees)) { apiary.RemoveHive(x,y); }
            }
            if(Action(showPlaces ? "Hide outdoor / base bees" : "Outdoor / base / outside bees")) showPlaces=!showPlaces;
            if(showPlaces) places.Draw(apiary,population,selected,x,y,hour,defaults,beeMove,label,button);
            GUILayout.EndVertical();
        }
        private void DrawHive(ApiaryState apiary,BeePopulation population,long hour,BeeSpawnSettings defaults)
        {
            Info("Top to bottom. Gold slots contain Frames; dark slots are empty.");
            if(Action("+ Add Super on top")) hive.AddSuper();
            for(int i=hive.Supers.Count-1;i>=0;i--)
            {
                var super=hive.Supers[i];
                GUILayout.BeginVertical(GUI.skin.box);
                Info($"SUPER {i+1}  |  {super.Id}");
                for(int row=0;row<2;row++)
                {
                    GUILayout.BeginHorizontal();
                    for(int col=0;col<5;col++)
                    {
                        int index=row*5+col;var frame=super.Slots[index].Frame;
                        Color before=GUI.backgroundColor;GUI.backgroundColor=frame==null ? Slate : Honey;
                        string text=frame==null ? $"{index}\nEmpty" : $"{index}\nFrame\n{frame.ResidentBeeCount} bees";
                        if(GUILayout.Button(text,button,GUILayout.Width(96),GUILayout.Height(92))) { superId=super.Id;slot=index;frameTab=0;Go(Page.Frame); }
                        GUI.backgroundColor=before;
                    }
                    GUILayout.EndHorizontal();
                }
                if(Action("Remove this empty Super",super.IsEmpty)) { hive.RemoveSuper(super.Id);GUILayout.EndVertical();break; }
                GUILayout.EndVertical();GUILayout.Space(10);
            }
            Color previous=GUI.backgroundColor;GUI.backgroundColor=Green;
            if(Action($"BASE  |  Entrance <-> Outside\n{hive.Base.ResidentBeeCount} bees")) showPlaces=!showPlaces;
            GUI.backgroundColor=previous;
            if(showPlaces) places.Draw(apiary,population,hive,x,y,hour,defaults,beeMove,label,button);
        }
        private void DrawFrame(BeePopulation population,long hour,BeeSpawnSettings defaults)
        {
            var super=hive.GetSuper(superId);var target=super?.GetSlot(slot);
            if(target==null) { Info("This slot no longer exists. Return to Hive.");return; }
            if(target.Frame==null)
            {
                Info("Empty Frame slot");
                if(Action("+ Insert new Frame")) hive.TryCreateFrame(superId,slot,out _);
                if(sourceFrame!=null && Action("Move carried Frame into this slot"))
                {
                    message=sourceHive.MoveFrameTo(sourceSuper,sourceSlot,hive,superId,slot) ? "Frame moved." : "Move rejected.";
                    sourceFrame=null;
                }
                return;
            }
            var frame=target.Frame;
            Info(frame.Id);
            Info($"{frame.Front.Width} x {frame.Front.Height} cells per side | {frame.TotalCellCount:N0} total\n{frame.ResidentBeeCount} bees | {frame.CompletedCellCount:N0} completed cells");
            var bar=GUILayoutUtility.GetRect(200,26,GUILayout.Height(26));Paint(bar,Slate);
            Paint(new Rect(bar.x,bar.y,bar.width*(float)(frame.AverageConstruction/100),bar.height),Honey);
            GUI.Label(bar,$"Construction {frame.AverageConstruction:0.0}%",small);
            frameTab=GUILayout.Toolbar(frameTab,new[]{"Overview","Cells","Bees","Move / Remove"},button,GUILayout.Height(54));
            if(frameTab==0)
            {
                GUILayout.Space(12);
                Info($"FRONT\n{frame.Front.CompletedCellCount:N0} / {frame.Front.CellCount:N0} complete\nBACK\n{frame.Back.CompletedCellCount:N0} / {frame.Back.CellCount:N0} complete");
                Info("Choose Cells to edit construction, or Bees to inspect and place individuals.");
            }
            else if(frameTab==1) construction.Draw(frame,label,button);
            else if(frameTab==2) bees.Draw(population,hive,frame,hour,defaults,beeMove,label,button);
            else
            {
                if(Action("Select Frame to move")) { sourceHive=hive;sourceSuper=superId;sourceSlot=slot;sourceFrame=frame;Go(Page.Hive); }
                if(Action("Remove Frame",frame.ResidentBeeCount==0)) hive.RemoveFrame(superId,slot);
                if(frame.ResidentBeeCount>0) Info("Frames with bees can be moved, but cannot be removed.");
            }
        }
        private static int Residents(HiveState hive)
        {
            int count=hive.Base.ResidentBeeCount;
            foreach(var super in hive.Supers) foreach(var slot in super.Slots) count+=slot.Frame?.ResidentBeeCount ?? 0;
            return count;
        }
        private static void Paint(Rect rect,Color color)
        { var previous=GUI.color;GUI.color=color;GUI.DrawTexture(rect,Texture2D.whiteTexture);GUI.color=previous; }
    }
}
