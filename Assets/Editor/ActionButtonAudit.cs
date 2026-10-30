using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class ActionButtonAudit
{
    public sealed class Usage
    {
        public readonly ActionButtonId Id;
        public readonly string Name, Scene, Owner, Action, Target, Source;
        public Usage(ActionButtonId id, string name, string scene, string owner, string action, string target, string source)
        { Id=id; Name=name; Scene=scene; Owner=owner; Action=action; Target=target; Source="Assets/Scripts/Presentation/"+source+".cs"; }
    }
    public static readonly Usage[] Usages =
    {
        new Usage(ActionButtonId.Observe,"양봉장 / 관찰","Apiary","ApiaryActionUI","GameFlow.OpenObservation","장비 또는 격자 중앙","Apiary/ApiaryActionUI"),
        new Usage(ActionButtonId.Place,"양봉장 / 놓기","Apiary","ApiaryActionUI","handSlot.TryMoveTo","격자 중앙, 관찰과 나란히","Apiary/ApiaryActionUI"),
        new Usage(ActionButtonId.Combine,"양봉장 / 결합","Apiary","ApiaryActionUI","gridSlot.TryCombineFrom","장비 오른쪽","Apiary/ApiaryActionUI"),
        new Usage(ActionButtonId.Lift,"양봉장 / 들기","Apiary","ApiaryActionUI","TryMoveTo / TrySplitTo","각 부품 오른쪽","Apiary/ApiaryActionUI"),
        new Usage(ActionButtonId.InsertFrame,"소비장 / 넣기","Observation","ObservationActionUI","ObservationActions.Execute → TryMoveTo","선택 슬롯 위쪽","Observation/ObservationActionUI"),
        new Usage(ActionButtonId.TakeFrame,"소비장 / 꺼내기","Observation","ObservationActionUI","ObservationActions.Execute → TryMoveTo","선택 슬롯 위쪽","Observation/ObservationActionUI"),
        new Usage(ActionButtonId.PlaceTopAttachment,"격왕판 / 배치","Observation","ObservationTopAttachmentActionUI","ObservationActions.PlaceTopAttachment","노출된 상자 위쪽","Observation/ObservationTopAttachmentActionUI"),
        new Usage(ActionButtonId.Back,"이동 / 뒤로가기","Observation","ObservationController + ActionButtonBinding","GoBack → GameFlow.OpenApiary","Canvas 왼쪽 위","Observation/ObservationController"),
        new Usage(ActionButtonId.NewGame,"메뉴 / 새 게임","MainMenu","MainMenuUI + ActionButtonBinding","OnNewGameClicked → StartNewGame","Canvas 중앙","UI/MainMenuUI"),
        new Usage(ActionButtonId.UnlockLand,"양봉장 / 부지 해금","Apiary","ApiaryActionUI","TryUnlockNextColumn","잠긴 격자의 기존 메뉴 슬롯","Apiary/ApiaryActionUI"),
        new Usage(ActionButtonId.InventorySlot,"인벤토리 / 슬롯","SharedUI","InventoryItemPresenter","OnSlotClicked → hand.Swap","6열 격자 (양봉장·관찰 공통)","UI/InventoryItemPresenter"),
        new Usage(ActionButtonId.ObservationPlace,"관찰 / 놓기","Observation","ObservationActionUI","Combine → Execute(Place)","바닥 이미지 위쪽","Observation/ObservationActionUI"),
        new Usage(ActionButtonId.ObservationCombine,"관찰 / 결합","Observation","ObservationActionUI","Combine → TryCombineFrom","최상단 장비 위쪽","Observation/ObservationActionUI"),
        new Usage(ActionButtonId.ObservationLift,"관찰 / 들기","Observation","ObservationActionUI","Execute(Lift) → TryMoveTo / TrySplitTo","선택 장비 오른쪽","Observation/ObservationActionUI"),
        new Usage(ActionButtonId.TakeTopAttachment,"격왕판 / 꺼내기","Observation","ObservationActionUI","Execute(TakeTopAttachment) → TryMoveTo","격왕판 위쪽","Observation/ObservationActionUI")
    };
    public sealed class Result
    {
        public int Errors, Scenes, Prefabs, SceneButtons, PrefabButtons;
        public readonly StringBuilder Details = new StringBuilder();
        public void Error(string text) { Errors++; Details.AppendLine("ERROR: " + text); }
        public string Text => $"{(Errors == 0 ? "PASS" : "FAIL")}: {Errors} errors; {Scenes} scenes, {Prefabs} prefabs; {SceneButtons} scene buttons, {PrefabButtons} prefab buttons.\n" + Details;
    }
    public static Result Run()
    {
        var result = new Result();
        var catalog = AssetDatabase.LoadAssetAtPath<ActionButtonCatalog>(ActionButtonSetup.CatalogPath);
        if (catalog == null) { result.Error("Catalog missing"); return result; }
        foreach (var group in catalog.entries.Where(e => e != null).GroupBy(e => e.id))
            if (group.Count() != 1) result.Error("Duplicate ID " + group.Key);
        if (catalog.entries.Any(e => e == null)) result.Error("Null catalog entry");
        var knownPrefabs = new HashSet<string>();
        foreach (ActionButtonId id in Enum.GetValues(typeof(ActionButtonId)))
        {
            var e = catalog.Find(id);
            if (e == null) { result.Error("Missing ID " + id); continue; }
            if (!Enum.IsDefined(typeof(ActionButtonFollowAnchor), e.followAnchor)) result.Error("Invalid follow anchor " + id);
            if (e.overrideLidFollowAnchor && !Enum.IsDefined(typeof(ActionButtonFollowAnchor), e.lidFollowAnchor)) result.Error("Invalid lid follow anchor " + id);
            if (e.size.x <= 0 || e.size.y <= 0) result.Error("Invalid size " + id);
            if (!Enum.IsDefined(typeof(ActionButtonPlacement), e.placement)) result.Error("Invalid placement " + id);
            if (e.placement == ActionButtonPlacement.Grid && (e.gridColumns < 1 || id != ActionButtonId.InventorySlot)) result.Error("Invalid grid usage " + id);
            if (id == ActionButtonId.InventorySlot && e.placement != ActionButtonPlacement.Grid) result.Error("InventorySlot must retain Grid placement");
            if (id != ActionButtonId.Back && id != ActionButtonId.NewGame && e.prefab == null) result.Error("Missing dynamic prefab " + id);
            if (e.prefab != null)
            {
                string path = AssetDatabase.GetAssetPath(e.prefab);
                knownPrefabs.Add(path);
                if (!(e.prefab.transform is RectTransform) || e.prefab.targetGraphic == null) result.Error("Invalid UI prefab " + id);
                if (e.prefab.GetComponentInChildren<TMPro.TMP_Text>(true) == null) result.Error("Prefab label missing " + id);
                if (e.prefab.onClick.GetPersistentEventCount() > 0) result.Error("Dynamic prefab must not add persistent click actions: " + id);
                if (e.prefab.GetComponent<ActionButtonBinding>() != null) result.Error("Dynamic prefab has static binding: " + id);
            }
        }
        foreach (var e in catalog.entries.Where(e => e != null))
            if (!Enum.IsDefined(typeof(ActionButtonId), e.id)) result.Error("Unknown ID " + e.id);
        foreach (var name in Usages.Select(u => u.Scene).Distinct())
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/Scenes/" + name + ".unity") == null) result.Error("Missing scene " + name);
        foreach (string guid in AssetDatabase.FindAssets("t:Scene", new[] { "Assets" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var scene = EditorSceneManager.OpenPreviewScene(path);
            try
            {
                result.Scenes++;
                var roots = scene.GetRootGameObjects();
                var components = roots.SelectMany(g => g.GetComponentsInChildren<Component>(true)).Where(c => c != null).ToArray();
                CheckMissingScripts(roots, path, result);
                foreach (var button in components.OfType<Button>())
                {
                    result.SceneButtons++;
                    var binding = button.GetComponent<ActionButtonBinding>();
                    if (binding == null || catalog.Find(binding.Id) == null) result.Error(path + ": unregistered Button " + button.name);
                    else result.Details.AppendLine(path + "/" + button.name + " → " + binding.Id);
                    var canvas = button.GetComponentInParent<Canvas>();
                    var manager = canvas != null ? canvas.rootCanvas.GetComponent<ActionButtonManager>() : null;
                    if (manager == null || manager.Catalog != catalog) result.Error(path + ": Canvas catalog missing for " + button.name);
                    if (binding != null && binding.Id == ActionButtonId.NewGame)
                    {
                        if (button.onClick.GetPersistentEventCount() != 1 || !(button.onClick.GetPersistentTarget(0) is MainMenuUI) || button.onClick.GetPersistentMethodName(0) != "OnNewGameClicked")
                            result.Error("NewGame persistent click connection changed");
                    }
                }
                foreach (var usage in Usages.Where(u => path == "Assets/Scenes/" + u.Scene + ".unity"))
                {
                    string owner = usage.Owner.Split(' ')[0];
                    var controller = components.FirstOrDefault(c => c.GetType().Name == owner);
                    if (controller == null) { result.Error(usage.Id + ": missing owner " + owner); continue; }
                    if (!File.Exists(usage.Source)) result.Error(usage.Id + ": missing source");
                    else if (usage.Id != ActionButtonId.NewGame && !File.ReadAllText(usage.Source).Contains("ActionButtonId." + usage.Id)) result.Error(usage.Id + ": source registration missing");
                    var serialized = new SerializedObject(controller);
                    string[] fields = owner == "InventoryItemPresenter" ? new[] { "slotContainer", "slotPrefab" } :
                        owner == "ObservationController" ? new[] { "backButton" } :
                        owner == "ApiaryActionUI" ? new[] { "buttonContainer", "buttonPrefab", "selectionController" } :
                        owner == "ObservationActionUI" ? new[] { "buttonContainer", "buttonPrefab", "actionLayer", "selection" } :
                        owner == "ObservationTopAttachmentActionUI" ? new[] { "buttonPrefab", "actionLayer", "selection", "positioner" } : new string[0];
                    foreach (string field in fields)
                        if (serialized.FindProperty(field)?.objectReferenceValue == null) result.Error(usage.Id + ": missing " + owner + "." + field);
                    if (usage.Id == ActionButtonId.InventorySlot && controller is InventoryItemPresenter)
                    {
                        var container = serialized.FindProperty("slotContainer").objectReferenceValue as Transform;
                        if (container == null || container.GetComponent<GridLayoutGroup>() == null) result.Error("Inventory grid missing");
                    }
                    result.Details.AppendLine(usage.Id + " : " + usage.Scene + " / " + usage.Owner + " / " + usage.Action);
                }
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) continue;
            result.Prefabs++;
            CheckMissingScripts(new[] { prefab }, path, result);
            foreach (var button in prefab.GetComponentsInChildren<Button>(true))
            {
                result.PrefabButtons++;
                if (!knownPrefabs.Contains(path) && button.GetComponent<ActionButtonBinding>() == null) result.Error(path + ": unregistered prefab button " + button.name);
                if (knownPrefabs.Contains(path) && button.gameObject != prefab) result.Error(path + ": nested button needs separate registration " + button.name);
            }
        }
        return result;
    }
    private static void CheckMissingScripts(GameObject[] roots, string path, Result result)
    {
        foreach (var transform in roots.SelectMany(r => r.GetComponentsInChildren<Transform>(true)))
            if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject) > 0) result.Error(path + ": missing script on " + transform.name);
    }
}
