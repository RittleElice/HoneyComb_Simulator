using HoneyComb.Simulation.Apiary;
using HoneyComb.Simulation.Hive;
using HoneyComb.Simulation.Bees;
using UnityEngine;
namespace HoneyComb.Unity.Presentation
{
    public sealed class ApiaryPlacesDebugPanel
    {
        private int place;
        private BeeCaste caste;
        private bool initialized;
        private HiveState home;
        private readonly BeeLocationListPanel listPanel=new BeeLocationListPanel();
        public void Draw(ApiaryState apiary,BeePopulation population,HiveState selectedHive,int x,int y,long hour,BeeSpawnSettings defaults,BeeMoveSelection move,GUIStyle label,GUIStyle button)
        {
            if(!initialized) { caste=defaults.Caste;initialized=true; }
            if(selectedHive!=null) home=selectedHive;
            if(home!=null && !ReferenceEquals(population.FindHive(home.Id),home)) home=null;
            GUILayout.Space(18);GUILayout.Label("BEE PLACES / OUTDOOR",label);
            var small=new GUIStyle(button){fontSize=18};
            place=GUILayout.Toolbar(place,new[]{"Hive Base","Apiary X/Y","Outside"},small,GUILayout.Height(50));
            BeeLocation location;
            if(place==0)
            {
                if(selectedHive==null) { GUILayout.Label("Select a hive on the grid to view its base.",label);return; }
                location=BeeLocation.AtHiveBase(selectedHive.Id);
            }
            else if(place==1) location=BeeLocation.AtApiary(apiary.Id,x,y);
            else location=BeeLocation.AtExternalRegion(population.ExternalRegion.Id);
            GUILayout.Label(location.ToString(),label);
            if(place==2) GUILayout.Label(population.ExternalRegion.Name+" (no coordinates / gathering yet)",label);
            GUILayout.Label("New Bee's colony home: "+(home?.Id ?? "Select a hive first"),label);
            caste=(BeeCaste)GUILayout.Toolbar((int)caste,new[]{"Worker","Queen","Drone"},small,GUILayout.Height(46));
            GUI.enabled=home!=null;
            if(GUILayout.Button("Add 1 Adult Bee here",button,GUILayout.Height(56)))
            {
                if(population.TryAddAdult(home,location,new BeeSpawnSettings(caste,BeeDevelopment.Adult,defaults.Health),hour,out var bee,out var error)) move.Message="Added Bee #"+bee.Id;
                else move.Message=error;
            }
            GUI.enabled=true;
            listPanel.Draw(population,location,population.GetBeesAt(location),move,label,button);
        }
    }
}
