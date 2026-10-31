using UnityEditor;
using UnityEngine;

// Scene-free preview plus live apiary inspection; Unity coordinates are not used by the data grid.
public sealed class HiveSpatialGridDebugWindow : EditorWindow
{
    private HiveSpatialGrid preview;
    private bool live;
    private int apiaryX, apiaryY, boxIndex, axis = 1, slice = 3;
    private Vector2 scroll;

    [MenuItem("Tools/HoneyComb/Hive Spatial Grid")]
    public static void Open() => GetWindow<HiveSpatialGridDebugWindow>("Hive Spatial Grid");

    private void OnInspectorUpdate() { if (live) Repaint(); }
    private void OnGUI()
    {
        live = EditorGUILayout.Toggle("Live apiary (Play Mode)", live);
        HiveSpatialGrid grid = preview;
        if (live)
        {
            apiaryX = EditorGUILayout.IntField("Apiary X", apiaryX);
            apiaryY = EditorGUILayout.IntField("Apiary Y", apiaryY);
            boxIndex = EditorGUILayout.IntField("Assembly part index", boxIndex);
            var world = GameBootstrap.Instance == null ? null : GameBootstrap.Instance.GameWorld;
            var slot = world?.Apiary.Grid.GetItemSlot(apiaryX, apiaryY);
            ItemData box = slot?.Item;
            if (slot?.Value is ItemAssemblyData assembly)
                box = boxIndex >= 0 && boxIndex < assembly.Parts.Count ? assembly.Parts[boxIndex] : null;
            grid = box?.SpatialGrid;
        }
        else
        {
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Brood sample")) CreatePreview(ItemKind.BroodBox);
            if (GUILayout.Button("Super sample")) CreatePreview(ItemKind.Super);
            if (GUILayout.Button("Lid sample")) CreatePreview(ItemKind.Lid);
            EditorGUILayout.EndHorizontal();
            grid = preview;
        }
        if (grid == null) { EditorGUILayout.HelpBox("Select a sample or a live hive box/lid.", MessageType.Info); return; }
        axis = GUILayout.Toolbar(axis, new[] { "YZ (X fixed)", "XZ (Y fixed)", "XY (Z fixed)" });
        slice = EditorGUILayout.IntSlider("Slice", slice, 0, (axis == 0 ? 18 : axis == 1 ? 52 : grid.Layout.SizeZ) - 1);
        EditorGUILayout.LabelField($"18 x 52 x {grid.Layout.SizeZ} = {grid.CellCount:N0}; revision {grid.Revision}");
        EditorGUILayout.LabelField($"496 x 390 x {grid.Layout.Z[grid.Layout.SizeZ]} mm; queen excluder: {grid.TopBoundaryEquipment != null}");
        EditorGUILayout.LabelField("Air: blue | Wood: brown | Comb: yellow | Insulation: purple");
        var horizontal = axis == 0 ? grid.Layout.Y : grid.Layout.X;
        var vertical = axis == 2 ? grid.Layout.Y : grid.Layout.Z;
        const float scale = 1.25f;
        scroll = EditorGUILayout.BeginScrollView(scroll);
        Rect area = GUILayoutUtility.GetRect((float)horizontal[horizontal.Count - 1] * scale, (float)vertical[vertical.Count - 1] * scale);
        for (int v = 0; v < vertical.Count - 1; v++)
        for (int h = 0; h < horizontal.Count - 1; h++)
        {
            int x = axis == 0 ? slice : h, y = axis == 0 ? h : axis == 1 ? slice : v, z = axis == 2 ? slice : v;
            var cell = grid.GetCell(x, y, z);
            Color color = cell.Material == HiveMaterial.Air ? new Color(.3f,.55f,.75f) : cell.Material == HiveMaterial.Wood ? new Color(.5f,.28f,.1f) : cell.Material == HiveMaterial.Comb ? Color.yellow : new Color(.7f,.3f,.8f);
            var rect = new Rect(area.x + (float)horizontal[h] * scale, area.y + (float)(vertical[vertical.Count - 1] - vertical[v + 1]) * scale,
                (float)(horizontal[h + 1] - horizontal[h]) * scale - 1, (float)(vertical[v + 1] - vertical[v]) * scale - 1);
            EditorGUI.DrawRect(rect, color);
            GUI.Label(rect, new GUIContent("", $"({x},{y},{z}) {cell.Material}\n{grid.Layout.VolumeMm3(grid.Layout.Index(x,y,z))} mm3\n{cell.Equipment?.Name}\n열용량: {grid.GetHeatCapacityJPerK(x,y,z):F4} J/K\n질량 미지정 소방: {cell.SourceCell?.ContentThermalMass.UnquantifiedCellCount ?? 0}"));
        }
        if (axis != 2 && grid.TopBoundaryEquipment != null && slice > 0 && slice < (axis == 0 ? 17 : 51))
        {
            float height = (float)(vertical[vertical.Count - 1] - grid.Layout.Z[grid.Layout.SizeZ - 1]) * scale;
            EditorGUI.DrawRect(new Rect(area.x + 20 * scale, area.y + height - 1, (float)(horizontal[horizontal.Count - 1] - 40) * scale, 2), Color.red);
        }
        EditorGUILayout.EndScrollView();
    }
    private void CreatePreview(ItemKind kind)
    {
        var box = new ItemData(0, "Debug box", kind);
        if (kind == ItemKind.Lid) { preview = box.SpatialGrid; return; }
        box.InteriorSlots.GetSlot(0).TryInsert(new ItemData(1, "Frame", ItemKind.Frame, new FrameData()));
        box.InteriorSlots.GetSlot(1).TryInsert(new ItemData(2, "Divider", ItemKind.DividerBoard));
        box.InteriorSlots.GetSlot(2).TryInsert(new ItemData(3, "Insulation", ItemKind.InsulationBoard));
        box.TopSlot.TryInsert(new ItemData(4, "Excluder", ItemKind.QueenExcluder));
        preview = box.SpatialGrid;
    }
}
