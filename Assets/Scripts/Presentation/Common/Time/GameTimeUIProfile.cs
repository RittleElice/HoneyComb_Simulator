using System;
using UnityEngine;
[CreateAssetMenu(menuName = "HoneyComb/Game Time UI Profile")]
public sealed class GameTimeUIProfile : ScriptableObject
{
    [Serializable] public class Placement
    {
        public string scene;
        public Vector2 anchor = new Vector2(.5f, 1);
        public Vector2 pivot = new Vector2(.5f, 1);
        public Vector2 position = new Vector2(0, -20);
        public Vector2 size = new Vector2(460, 50);
    }
    public Placement[] placements = {
        new Placement { scene = "MainMenu" },
        new Placement { scene = "Apiary" },
        new Placement { scene = "Observation" }
    };
    public Placement Find(string scene) => Array.Find(placements, p => p.scene == scene);
}
