using System.Collections.Generic;
using HoneyComb.Simulation.Bees;
using UnityEngine;
namespace HoneyComb.Unity.Presentation
{
    // Shared across all debug places, including Frame panels.
    public sealed class BeeMoveSelection
    {
        public long? BeeId;
        public string Message;
    }
    public sealed class BeeLocationListPanel
    {
        private int index;
        private BeeLocation previous;
        public void Draw(BeePopulation population,BeeLocation destination,IReadOnlyList<BeeState> list,BeeMoveSelection move,GUIStyle label,GUIStyle button)
        {
            if(!previous.Equals(destination)) { previous=destination;index=0; }
            GUILayout.Label($"At this place: {list.Count} bees",label);
            if(list.Count>0)
            {
                index=Mathf.Clamp(index,0,list.Count-1);
                GUILayout.BeginHorizontal();
                if(GUILayout.Button("Previous Bee",button,GUILayout.Height(48))) index=(index+list.Count-1)%list.Count;
                if(GUILayout.Button("Next Bee",button,GUILayout.Height(48))) index=(index+1)%list.Count;
                GUILayout.EndHorizontal();
                var bee=list[index];
                GUILayout.Label($"Bee {index+1}/{list.Count}: #{bee.Id}\n{bee.Caste} / {bee.Sex} / {bee.Development}\nColony: {bee.ColonyId}\nLocation: {bee.Location}\nHealth: {bee.Health:0.##} | Alive: {bee.Alive}\nCreated hour: {bee.CreatedHour}",label);
                GUILayout.Label($"Current hive: {population.FindPhysicalHive(bee.Location)?.Id ?? "Outside hives"}\nLaid hour: {bee.LaidHour?.ToString() ?? "Unknown (manual placement)"}\nAdult since: {bee.AdultSinceHour?.ToString() ?? "N/A"}",label);
                GUILayout.Label($"Development: {bee.DevelopmentProgress:0.##} h ({bee.DevelopmentFraction:P1})",label);
                if(bee.Caste==BeeCaste.Queen && bee.Development==BeeDevelopment.Adult)
                {
                    if(GUILayout.Button(bee.CanLayEggs ? "Disable prototype laying" : "Enable prototype laying (assume mated)",button,GUILayout.Height(48)))
                        population.SetQueenLaying(bee.Id,!bee.CanLayEggs);
                    if(GUILayout.Button("Lay 1 worker egg (ready queen)",button,GUILayout.Height(48)))
                    {
                        if(population.TryLayEgg(bee.Id,population.CurrentHour,out var egg,out var reason)) move.Message="Laid egg #"+egg.Id;
                        else move.Message=reason;
                    }
                }
                GUI.enabled=bee.Development==BeeDevelopment.Adult;
                if(GUILayout.Button("Select this Bee to move",button,GUILayout.Height(52))) { move.BeeId=bee.Id;move.Message="Select a destination place, then Move selected Bee here."; }
                GUI.enabled=true;
            }
            if(move.BeeId.HasValue)
            {
                GUILayout.Label("Moving Bee #"+move.BeeId.Value,label);
                if(GUILayout.Button("Move selected Bee here",button,GUILayout.Height(56)))
                {
                    if(population.TryMoveAdult(move.BeeId.Value,destination,out var error)) { move.Message="Bee moved. Colony preserved.";move.BeeId=null; }
                    else move.Message=error;
                }
                if(GUILayout.Button("Cancel Bee move",button,GUILayout.Height(44))) { move.BeeId=null;move.Message=null; }
            }
            if(!string.IsNullOrEmpty(move.Message)) GUILayout.Label(move.Message,label);
        }
    }
}
