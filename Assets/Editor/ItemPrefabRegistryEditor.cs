using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(ItemPrefabRegistry)), CanEditMultipleObjects]
public class ItemPrefabRegistryEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        var registry = (ItemPrefabRegistry)target;
        if (registry.Catalog == null)
            EditorGUILayout.HelpBox("Item Prefab Catalog 에셋을 연결하세요.", MessageType.Warning);
        else if (GUILayout.Button("Catalog 등록 데이터 열기"))
            Selection.activeObject = registry.Catalog;
    }
}
