using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

[InitializeOnLoad]
public static class ThermalStageTwoTests
{
    static ThermalStageTwoTests() { EditorApplication.update += Watch; }
    static void Watch()
    {
        if(EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        if(File.Exists("Temp/run-thermal-stage-two")) { File.Delete("Temp/run-thermal-stage-two"); RunBatch(); }
        if(File.Exists("Temp/build-thermal-stage-two")) { File.Delete("Temp/build-thermal-stage-two"); BuildAndroid(); }
    }
    static int checks;
    static readonly List<string> report=new List<string>();
    static void Check(bool b,string text) { checks++; if(!b) throw new Exception(text); }
    static void Near(double a,double b,double eps,string text) => Check(Math.Abs(a-b)<=eps,text+": "+a+" vs "+b);
    static void Reject(Action a,string label) { bool failed=false; try { a(); } catch(ArgumentException) {failed=true;} catch(InvalidOperationException) {failed=true;} catch(InvalidDataException) {failed=true;} Check(failed,label); }
    static WorldSaveSnapshot Snapshot(GameWorld w) => new WorldSaveSnapshot { World=w,UtcTicks=DateTime.UtcNow.Ticks,TargetMinute=w.CurrentGameMinute };
    static byte[] WeatherBytes(WeatherSimulator w) { using(var s=new MemoryStream()) using(var b=new BinaryWriter(s)) {w.WriteSnapshot(b);return s.ToArray();} }
    static float[] Energies(ThermalExchange e) => Enumerable.Range(0,e.CellCount).Select(i=>e.GetCell(i).Owner.EnergyJ[e.GetCell(i).Index]).ToArray();
    public static void RunBatch() => PyramidThermalTests.RunBatch();
    public static void BuildAndroid()
    {
        var scenes=new[]{"Assets/Scenes/Bootstrap.unity","Assets/Scenes/MainMenu.unity","Assets/Scenes/Apiary.unity","Assets/Scenes/Observation.unity","Assets/Scenes/SharedUI.unity"};
        var result=BuildPipeline.BuildPlayer(new BuildPlayerOptions {scenes=scenes,locationPathName="outputs/calculation-only-thermal/HoneyComb.apk",target=BuildTarget.Android,options=BuildOptions.Development});
        File.WriteAllText("outputs/calculation-only-thermal/android.txt",result.summary.result+"; errors="+result.summary.totalErrors+"; time="+result.summary.totalTime);
        if(Application.isBatchMode) EditorApplication.Exit(result.summary.result==BuildResult.Succeeded?0:1);
    }
}


