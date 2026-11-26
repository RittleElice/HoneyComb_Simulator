using System;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class CellSpriteStorageRegression
{
    public static void Run()
    {
        string output="outputs/cell-sprites/storage-regression.txt";
        try
        {
            var report=new System.Text.StringBuilder();
            Check(report,"EquipmentCellDataTests",EquipmentCellDataTests.RunAll);
            Check(report,"CombThermalTests",CombThermalTests.RunAll);
            Check(report,"WorldSaveTests",WorldSaveTests.Run);
            Check(report,"BeeForageTests",BeeForageTests.Run);
            File.WriteAllText(output,report.ToString());
            if(Application.isBatchMode)EditorApplication.Exit(0);
        }
        catch(Exception e){File.WriteAllText(output,"FAIL "+e);Debug.LogException(e);if(Application.isBatchMode)EditorApplication.Exit(1);throw;}
    }
    static void Check(System.Text.StringBuilder report,string name,Action action)
    {
        try{action();report.AppendLine("PASS "+name);}
        catch(Exception e){report.AppendLine("FAIL "+name+": "+e);}
    }
}
