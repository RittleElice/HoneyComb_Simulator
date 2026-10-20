using System.Globalization;
using HoneyComb.Simulation.Apiary;
using HoneyComb.Simulation.Hive;
using HoneyComb.Simulation.Bees;
using UnityEngine;
namespace HoneyComb.Unity.Presentation
{
    public sealed class BeeDebugPanel
    {
        private bool initialized;
        private Frame previous;
        private BeeCaste caste;
        private BeeDevelopment stage;
        private FrameSide side;
        private string xText="0",yText="0",message;
        private readonly BeeLocationListPanel listPanel=new BeeLocationListPanel();
        public void Draw(BeePopulation population,HiveState hive,Frame frame,long hour,BeeSpawnSettings defaults,BeeMoveSelection beeMove,GUIStyle label,GUIStyle button)
        {
            if(frame==null) return;
            if(!initialized) { caste=defaults.Caste;stage=defaults.Development;initialized=true; }
            if(!ReferenceEquals(previous,frame)) { previous=frame;message=null;xText="0";yText="0"; }
            GUILayout.Space(20);GUILayout.Label("BEES / MANUAL PLACEMENT",label);
            var colony=population.GetColony(hive.Id);
            GUILayout.Label($"Home colony: {colony?.Id ?? "Created on first addition"}\nColony members: {colony?.Bees.Count ?? 0}\nBees on this Frame: {frame.ResidentBeeCount}",label);
            var small=new GUIStyle(button){fontSize=18};
            caste=(BeeCaste)GUILayout.Toolbar((int)caste,new[]{"Worker","Queen","Drone"},small,GUILayout.Height(48));
            stage=(BeeDevelopment)GUILayout.Toolbar((int)stage,new[]{"Egg","Larva","Pupa","Adult"},small,GUILayout.Height(48));
            side=(FrameSide)GUILayout.Toolbar((int)side,new[]{"Front","Back"},small,GUILayout.Height(48));
            int x=0,y=0;bool valid=true;
            if(stage!=BeeDevelopment.Adult)
            {
                GUILayout.Label("Brood: choose a completed empty cell",label);
                var field=new GUIStyle(GUI.skin.textField){fontSize=24};
                GUILayout.BeginHorizontal();GUILayout.Label("X",label,GUILayout.Width(30));xText=GUILayout.TextField(xText,8,field,GUILayout.Width(150));
                GUILayout.Label("Y",label,GUILayout.Width(30));yText=GUILayout.TextField(yText,8,field,GUILayout.Width(150));GUILayout.EndHorizontal();
                valid=int.TryParse(xText,NumberStyles.None,CultureInfo.InvariantCulture,out x)&int.TryParse(yText,NumberStyles.None,CultureInfo.InvariantCulture,out y);
            }
            else GUILayout.Label("Adult: placed on the Frame surface (not inside a cell)",label);
            GUI.enabled=valid;
            if(GUILayout.Button("Add 1 Bee to selected Frame",button,GUILayout.Height(60)))
            {
                var settings=new BeeSpawnSettings(caste,stage,defaults.Health);
                if(population.TryAddBee(hive,frame,side,x,y,settings,hour,out var bee,out var reason)) message=$"Added Bee #{bee.Id}";
                else message=reason;
            }
            GUI.enabled=true;
            if(!string.IsNullOrEmpty(message)) GUILayout.Label(message,label);
            var locatedBees=population.GetBeesOnSurface(frame.Id,side);
            int count=locatedBees.Count;
            GUILayout.Label($"{side} bees (adults + brood): {count}",label);
            if(stage!=BeeDevelopment.Adult && valid)
            {
                var occupant=population.GetBeeInCell(frame,side,x,y);
                GUILayout.Label(occupant==null ? "Selected cell: no bee" : $"Selected cell: Bee #{occupant.Value.Id} / {occupant.Value.Development}",label);
            }
            // Frame side list includes brood; only adults can be selected for movement.
            listPanel.Draw(population,BeeLocation.OnFrame(frame.Id,side),locatedBees,beeMove,label,button);
        }
    }
}
