using HoneyComb.Simulation.Hive;
using HoneyComb.Simulation.Apiary;
using System.Linq;
using UnityEngine;
using HoneyComb.Simulation.Bees;
namespace HoneyComb.Unity.Presentation
{
    public sealed class HiveStructureDebugPanel
    {
        private HiveState previous, sourceHive;
        private Frame movingFrame;
        private readonly BeeDebugPanel beePanel=new BeeDebugPanel();
        private readonly CellConstructionDebugPanel constructionPanel=new CellConstructionDebugPanel();
        private string selectedSuper,sourceSuper,sourceFrame,message;
        private int selectedSlot,sourceSlot;
        public void Draw(ApiaryState apiary,HiveState hive,BeePopulation population,long hour,BeeSpawnSettings defaults,BeeMoveSelection beeMove,GUIStyle label,GUIStyle button)
        {
            if(!ReferenceEquals(previous,hive)) { previous=hive;selectedSuper=null; }
            if(sourceSuper!=null)
            {
                bool valid=apiary.Hives.Contains(sourceHive) && ReferenceEquals(sourceHive.GetSuper(sourceSuper)?.GetSlot(sourceSlot)?.Frame,movingFrame);
                if(!valid) { sourceSuper=null;sourceHive=null;movingFrame=null;message="Move cancelled: source was removed or changed."; }
                else
                {
                    GUILayout.Label($"Moving Frame: {movingFrame.Id}\nFrom: {sourceHive.Id} / Slot {sourceSlot}\nSelect a hive and an empty slot.",label);
                    if(GUILayout.Button("Cancel Frame move",button,GUILayout.Height(50))) { sourceSuper=null;sourceHive=null;movingFrame=null; }
                }
            }
            if(hive==null) return;
            GUILayout.Space(20);GUILayout.Label("HIVE STRUCTURE",label);
            GUILayout.Label("Base: 1 / Entrance: Outside\nSupers: "+hive.Supers.Count,label);
            if(GUILayout.Button("Add Super on top",button,GUILayout.Height(60))) selectedSuper=hive.AddSuper().Id;
            var small=new GUIStyle(button){fontSize=18};
            // Draw top to bottom; modify only below this enumeration.
            for(int s=hive.Supers.Count-1;s>=0;s--)
            {
                var super=hive.Supers[s];
                if(GUILayout.Button($"Level {s+1}: {super.Id}",small,GUILayout.Height(46))) selectedSuper=super.Id;
                for(int row=0;row<2;row++)
                {
                    GUILayout.BeginHorizontal();
                    for(int col=0;col<5;col++)
                    {
                        int index=row*5+col;var slot=super.Slots[index];
                        string text=$"{index}: {(slot.Frame==null ? "." : "F")}";
                        if(selectedSuper==super.Id && selectedSlot==index) text="["+text+"]";
                        if(GUILayout.Button(text,small,GUILayout.Width(94),GUILayout.Height(54))) { selectedSuper=super.Id;selectedSlot=index; }
                    }
                    GUILayout.EndHorizontal();
                }
            }
            GUILayout.Label("BASE / Entrance <-> Outside",label);
            var selected=hive.GetSuper(selectedSuper);var target=selected?.GetSlot(selectedSlot);
            if(selected!=null)
            {
                GUILayout.Label($"Selected: {selected.Id} / Slot {selectedSlot}\nFrame: {target.Frame?.Id ?? "Empty"}\nBee space: {(target.CanHostBees ? "Active" : "Inactive")}",label);
                if(target.Frame!=null)
                {
                    var frame=target.Frame;
                    GUILayout.Label($"Front: {frame.Front.Width} x {frame.Front.Height} = {frame.Front.CellCount:N0} cells\nBack: {frame.Back.Width} x {frame.Back.Height} = {frame.Back.CellCount:N0} cells\nTotal: {frame.TotalCellCount:N0} cells",label);
                }
                GUI.enabled=selected.IsEmpty;
                if(GUILayout.Button("Remove selected empty Super",button,GUILayout.Height(60)))
                { hive.RemoveSuper(selected.Id);selectedSuper=null;sourceSuper=null;GUI.enabled=true;return; }
                GUI.enabled=target.Frame==null;
                if(GUILayout.Button("Insert new Frame",button,GUILayout.Height(60))) hive.TryCreateFrame(selected.Id,selectedSlot,out _);
                GUI.enabled=target.Frame!=null && target.Frame.ResidentBeeCount==0;
                if(GUILayout.Button("Remove Frame",button,GUILayout.Height(60)))
                { hive.RemoveFrame(selected.Id,selectedSlot);sourceSuper=null; }
                GUI.enabled=target.Frame!=null;
                if(GUILayout.Button("Move: select this Frame",button,GUILayout.Height(60)))
                { sourceHive=hive;movingFrame=target.Frame;sourceSuper=selected.Id;sourceSlot=selectedSlot;sourceFrame=target.Frame.Id;message="Select an empty slot in this or another hive, then Move Frame here."; }
                GUI.enabled=true;
                if(sourceSuper!=null)
                {
                    GUI.enabled=target.Frame==null;
                    if(GUILayout.Button("Move Frame here",button,GUILayout.Height(60)))
                    {
                        bool valid=apiary.Hives.Contains(sourceHive) && ReferenceEquals(sourceHive.GetSuper(sourceSuper)?.GetSlot(sourceSlot)?.Frame,movingFrame);
                        message=valid && sourceHive.MoveFrameTo(sourceSuper,sourceSlot,hive,selected.Id,selectedSlot) ? "Frame moved; ID preserved." : "Move rejected.";
                        sourceSuper=null;
                    }
                    GUI.enabled=true;

                }
                if(!selected.IsEmpty) GUILayout.Label("Remove all Frames before removing this Super.",label);
                constructionPanel.Draw(target.Frame,label,button);
                if(target.Frame!=null && target.Frame.ResidentBeeCount>0) GUILayout.Label("Remove Frame is disabled while bees are present. Moving is allowed.",label);
                beePanel.Draw(population,hive,target.Frame,hour,defaults,beeMove,label,button);
            }
            if(!string.IsNullOrEmpty(message)) GUILayout.Label(message,label);
        }
    }
}
