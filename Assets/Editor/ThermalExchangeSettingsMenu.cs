using UnityEditor;
using UnityEngine;

public static class ThermalExchangeSettingsMenu
{
    [MenuItem("Tools/HoneyComb/격자 열교환 설정")]
    public static void Open()
    {
        Selection.activeObject = Resources.Load<ThermalExchangeSettings>("Thermal/ExchangeSettings");
        EditorGUIUtility.PingObject(Selection.activeObject);
    }
}
