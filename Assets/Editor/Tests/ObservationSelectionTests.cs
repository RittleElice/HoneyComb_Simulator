using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public static class ObservationSelectionTests
{
    private static int checks;
    private static void Check(bool value, string message)
    {
        checks++; if (!value) throw new Exception("Observation selection: " + message);
    }
    private static void Set(object target,string field,object value) => target.GetType().GetField(field,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(target,value);
    private static T Get<T>(object target,string field) => (T)target.GetType().GetField(field,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(target);
    private static void Call(object target,string method) => target.GetType().GetMethod(method,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(target,null);
    private static T Find<T>(Scene scene) where T:Component => scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<T>(true)).Single();
    private static ItemData[] Parts() => new[] {new ItemData(10,"단상",ItemKind.BroodBox),new ItemData(11,"계상",ItemKind.Super),new ItemData(12,"계상",ItemKind.Super),new ItemData(13,"뚜껑",ItemKind.Lid)};

    [MenuItem("Tools/HoneyComb/Run Observation Selection Tests")]
    public static void RunAll()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Run outside Play Mode.");
        checks=0;
        TestActions();
        var scene=EditorSceneManager.OpenPreviewScene("Assets/Scenes/Observation.unity");
        var shared=EditorSceneManager.OpenPreviewScene("Assets/Scenes/SharedUI.unity");
        try
        {
            var selection=Find<ObservationSelectionController>(scene);
            var observation=Find<ObservationController>(scene);
            var presenter=Find<ObservationItemPresenter>(scene);
            var menu=Find<ObservationActionUI>(scene);
            var highlight=Find<ObservationSelectionHighlight>(scene);
            var registry=Find<ItemPrefabRegistry>(shared);
            var canvas=Find<Canvas>(scene);
            var scaler=canvas.GetComponent<CanvasScaler>(); if(scaler!=null)scaler.enabled=false;
            canvas.renderMode=RenderMode.WorldSpace;
            var rect=(RectTransform)canvas.transform;
            rect.localScale=Vector3.one; rect.pivot=Vector2.zero; rect.sizeDelta=new Vector2(1080,1920); rect.position=Vector3.zero;
            Set(presenter,"prefabRegistry",registry);
            var slot=new ItemSlot(); var parts=Parts(); slot.TryInsertValue(new ItemAssemblyData(parts));
            Set(observation,"observedSlot",slot);
            presenter.Show(slot.Value); selection.Synchronize();
            Check(selection.Observation==observation,"Inspector controller connection");
            Check(Get<ObservationSelectionController>(menu,"selection")==selection,"Inspector menu connection");
            Check(Get<ObservationSelectionController>(highlight,"selection")==selection,"Inspector highlight connection");
            Check(Get<Button>(menu,"buttonPrefab")!=null,"existing action prefab connection");
            TestMenuButton(scene,selection,observation,menu);
            Set(observation,"observedSlot",slot); presenter.Show(slot.Value); selection.Synchronize();
            var ground=Get<ObservationHitArea>(selection,"ground");
            Check(ground.ContainsNormalized(new Vector2(.1f,.1f)) && !ground.ContainsNormalized(new Vector2(.05f,.95f)),"ground silhouette");
            for(int i=0;i<4;i++)
            {
                var part=presenter.CurrentView.Parts[i];
                var area=part.GetComponent<ObservationHitArea>();
                Check(area!=null && area.ContainsNormalized(new Vector2(.5f,.5f)),"prefab hit area");
                Check(!area.ContainsNormalized(new Vector2(.02f,.98f)),"transparent upper-left corner excluded");
                var point=(Vector2)part.Image.transform.TransformPoint(part.VisibleRect.center);
                selection.SelectAt(point);
                Check(selection.Selection.Kind==ObservationSelectionKind.EquipmentPart && ReferenceEquals(selection.Selection.Part,parts[i]) && selection.Selection.PartIndex==i,"independent layer selection "+i);
            }
            // Overlap two parts deliberately; the last sibling must win even if it has a lower part index.
            var first=presenter.CurrentView.Parts[0]; var second=presenter.CurrentView.Parts[1];
            first.transform.position=second.transform.position;
            first.transform.SetAsLastSibling();
            selection.SelectAt(first.Image.transform.TransformPoint(first.VisibleRect.center));
            Check(ReferenceEquals(selection.Selection.Part,parts[0]),"actual draw order wins over parts index");
            presenter.Show(slot.Value); selection.Synchronize();
            Check(ReferenceEquals(selection.Selection.Part,parts[0]) && selection.SelectedImage==presenter.CurrentView.Parts[0].Image,"selection rebinds after refresh");
            Call(highlight,"LateUpdate");
            Check(selection.SelectedImage.GetComponent<Outline>()!=null && !selection.SelectedImage.raycastTarget,"highlight on selected image only, no input interception");
            var groundRect=ground.Image.rectTransform.rect;
            var groundPoint=ground.transform.TransformPoint(new Vector3(groundRect.xMin+groundRect.width*.1f,groundRect.yMin+groundRect.height*.1f));
            selection.SelectAt(groundPoint);
            Check(selection.Selection.Kind==ObservationSelectionKind.Ground,"occupied ground selected separately");
            Call(highlight,"LateUpdate");
            Check(ground.Image.GetComponent<Outline>()!=null && presenter.CurrentView.Parts.All(p=>p.GetComponent<Outline>()==null),"highlight switches and removes old outline");
            Set(menu,"handSlot",new ItemSlot()); menu.RefreshActions();
            Check(Get<CanvasGroup>(menu,"panelCanvasGroup").alpha==1 && Get<Button>(menu,"actionButton")==null,"occupied ground information has no lift action");
            selection.Selection.SelectPart(slot,parts[1]); menu.RefreshActions();
            var button=Get<Button>(menu,"actionButton");
            Check(button!=null && button.GetComponentInChildren<TMPro.TMP_Text>().text.Contains("2층"),"menu uses selected layer");
            selection.SelectAt(new Vector2(30,30));
            Check(selection.Selection.Kind==ObservationSelectionKind.None,"empty background clears selection");
            menu.RefreshActions(); Check(!Get<CanvasGroup>(menu,"panelCanvasGroup").blocksRaycasts,"hidden panel releases input");

            // Existing hand images use manual hit testing; shield clicks even with raycastTarget=false.
            var handGo=new GameObject("TestHand"); SceneManager.MoveGameObjectToScene(handGo,scene);
            var hand=handGo.AddComponent<HandItemPresenter>();
            Set(hand,"currentView",presenter.CurrentView); Set(selection,"hand",hand);
            var body=presenter.CurrentView.Parts[0];
            Check(selection.IsBlocked(body.Image.transform.TransformPoint(body.VisibleRect.center)),"hand image blocks click-through");
            hand.SetInspectionInputCaptured(true); Check(selection.IsBlocked(new Vector2(30,30)),"hand drag captures outside its image");
            hand.SetInspectionInputCaptured(false); Set(hand,"currentView",null); UnityEngine.Object.DestroyImmediate(handGo);

            selection.Selection.SelectPart(slot,parts[2]);
            var handSlot=new ItemSlot(); slot.TrySplitTo(2,handSlot); presenter.Show(slot.Value); selection.Synchronize();
            Check(selection.Selection.Kind==ObservationSelectionKind.None,"removed selected part invalidates selection");
            slot.TryMoveTo(new ItemSlot()); presenter.Clear(); selection.Synchronize(); selection.SelectAt(groundPoint);
            Check(selection.Selection.Kind==ObservationSelectionKind.Ground,"empty ground stays selectable");
            Set(observation,"observedSlot",new ItemSlot()); selection.Synchronize();
            Check(selection.Selection.Kind==ObservationSelectionKind.None,"changed observation target clears selection");
            Debug.Log("Observation selection tests passed: "+checks+" checks.");
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); EditorSceneManager.ClosePreviewScene(shared); }
    }

    private static void TestMenuButton(Scene scene,ObservationSelectionController selection,ObservationController observation,ObservationActionUI menu)
    {
        var previous=GameBootstrap.Instance;
        var go=new GameObject("MenuTestBootstrap"); go.SetActive(false); SceneManager.MoveGameObjectToScene(go,scene);
        var bootstrap=go.AddComponent<GameBootstrap>();
        var world=new GameWorld();
        var instance=typeof(GameBootstrap).GetProperty("Instance");
        try
        {
            typeof(GameBootstrap).GetProperty("GameWorld").SetValue(bootstrap,world);
            instance.SetValue(null,bootstrap);
            var slot=new ItemSlot(); var parts=Parts(); slot.TryInsertValue(new ItemAssemblyData(parts));
            Set(observation,"observedSlot",slot); Set(menu,"handSlot",world.Hand.Slot);
            Find<ObservationItemPresenter>(scene).Show(slot.Value);
            selection.Selection.SelectPart(slot,parts[1]); menu.RefreshActions();
            Get<Button>(menu,"actionButton").onClick.Invoke();
            Check(ItemVisualFactory.GetParts(world.Hand.Slot.Value).Count==3 && ReferenceEquals(slot.Value,parts[0]),"real menu click splits selected upper stack");
            Check(selection.Selection.Kind==ObservationSelectionKind.None,"successful action clears selection");
            Find<ObservationItemPresenter>(scene).Show(slot.Value);
            selection.Selection.SelectPart(slot,parts[0]); menu.RefreshActions();
            Check(Get<Button>(menu,"actionButton")==null,"selection menu has no duplicate combine");
            selection.ClearSelection(); menu.RefreshActions();
            var button=Get<Button>(menu,"combineButton");
            Check(button!=null && button.gameObject.activeSelf,"combine available without selection");
            var original=world.Hand.Slot.TakeValue();
            world.Hand.Slot.TryInsert(new ItemData(22,"replacement",ItemKind.Super));
            button.onClick.Invoke();
            Check(ReferenceEquals(slot.Value,parts[0]) && world.Hand.Slot.Value!=original,"stale menu cannot act on replaced hand item");
            menu.RefreshActions(); button.onClick.Invoke();
            Check(ItemVisualFactory.GetParts(slot.Value).Count==2 && world.Hand.Slot.IsEmpty,"independent button combines into live target");
            menu.RefreshActions();
            Check(!button.gameObject.activeSelf,"empty hand hides combination button");
            world.Hand.Slot.TryInsert(new ItemData(25,"frame",ItemKind.Frame)); menu.RefreshActions();
            Check(!button.gameObject.activeSelf,"incompatible hand hides combination button"); world.Hand.Slot.TakeValue();
            var layer=Get<RectTransform>(menu,"actionLayer");
            Check(layer!=null && layer.parent.GetComponent<Canvas>()!=null && layer.anchorMin==Vector2.zero && layer.anchorMax==Vector2.one,"full stretch layer outside zoom world");
            Check(menu.transform.parent==layer && button.transform.parent==layer,"both menus on unscaled layer");
            var panel=(RectTransform)menu.transform;
            var panelSize=panel.rect.size;
            var anchorObject=new GameObject("FollowAnchor",typeof(RectTransform),typeof(Image));
            SceneManager.MoveGameObjectToScene(anchorObject,scene);
            var anchor=anchorObject.GetComponent<Image>(); anchor.raycastTarget=false;
            anchor.rectTransform.sizeDelta=new Vector2(40,40);
            anchor.transform.position=new Vector3(100,150,0);
            var follow=typeof(ObservationActionUI).GetMethod("Follow",BindingFlags.Instance|BindingFlags.NonPublic);
            follow.Invoke(menu,new object[]{panel,anchor,false});
            var first=panel.position;
            anchor.transform.position+=new Vector3(20,30,0);
            follow.Invoke(menu,new object[]{panel,anchor,false});
            Check(Vector2.Distance((Vector2)(panel.position-first),new Vector2(20,30))<.1f,"menu follows actual image translation");
            Check(Mathf.Abs(panel.position.x-(anchor.transform.position.x+20+16+panel.rect.width*.5f))<.1f,"menu anchors at image right edge");
            anchor.transform.position=new Vector3(10000,10000,0);
            follow.Invoke(menu,new object[]{panel,anchor,false});
            Check(panel.position.x+panel.rect.width*.5f<=Screen.width-15.9f && panel.position.y+panel.rect.height*.5f<=Screen.height-15.9f,"panel clamped inside screen");
            anchor.transform.position=new Vector3(200,150,0);
            follow.Invoke(menu,new object[]{(RectTransform)button.transform,anchor,true});
            Check(Mathf.Abs(button.transform.position.x-200)<.1f && Mathf.Abs(button.transform.position.y-226)<.1f,"combine anchors above image center");
            UnityEngine.Object.DestroyImmediate(anchorObject);
            var view=Find<ObservationItemPresenter>(scene);
            view.Show(slot.Value); selection.Synchronize();
            selection.Selection.SelectPart(slot,parts[0]); menu.RefreshActions();
            Call(menu,"UpdatePositions");
            var equipment=view.CurrentView.transform.parent;
            var oldScale=equipment.localScale; var oldPosition=equipment.localPosition;
            equipment.localScale=oldScale*2f; equipment.localPosition+=new Vector3(0,100,0);
            Call(menu,"UpdatePositions");
            Check(panel.rect.size==panelSize && panel.localScale==Vector3.one,"zoom and pan retain menu size");
            equipment.localScale=oldScale; equipment.localPosition=oldPosition;
            world.Hand.Slot.TryInsert(new ItemData(23,"lid",ItemKind.Lid));
            menu.RefreshActions();
            var replacementTarget=new ItemSlot(); replacementTarget.TryInsert(new ItemData(24,"other",ItemKind.BroodBox));
            Set(observation,"observedSlot",replacementTarget); button.onClick.Invoke();
            Check(!world.Hand.Slot.IsEmpty && replacementTarget.Value is ItemData,"stale button cannot combine into replaced target");
            var emptyGround=new ItemSlot();
            Set(observation,"observedSlot",emptyGround); view.Clear(); selection.ClearSelection();
            world.Hand.Slot.TakeValue(); world.Hand.Slot.TryInsert(new ItemData(31,"frame",ItemKind.Frame));
            menu.RefreshActions(); Check(!button.gameObject.activeSelf,"unplaceable item hides ground button");
            world.Hand.Slot.TakeValue(); var brood=new ItemData(32,"brood",ItemKind.BroodBox); world.Hand.Slot.TryInsert(brood);
            menu.RefreshActions(); Call(menu,"UpdatePositions");
            Check(button.gameObject.activeSelf && selection.Selection.Kind==ObservationSelectionKind.None,"ground button available without selecting ground");
            Check(button.GetComponentInChildren<TMPro.TMP_Text>().text=="결합하기","ground button label");
            var actualPosition=button.transform.position;
            follow.Invoke(menu,new object[]{(RectTransform)button.transform,selection.GroundImage,true});
            Check(Vector3.Distance(actualPosition,button.transform.position)<.1f,"ground button follows ground image upper center");
            selection.Selection.SelectGround(emptyGround); menu.RefreshActions();
            Check(Get<Button>(menu,"actionButton")==null,"ground selection has no duplicate placement button");
            selection.ClearSelection(); menu.RefreshActions();
            world.Hand.Slot.TakeValue(); world.Hand.Slot.TryInsert(new ItemData(33,"replacement",ItemKind.BroodBox));
            button.onClick.Invoke(); Check(emptyGround.IsEmpty && !world.Hand.Slot.IsEmpty,"stale ground button rejects replaced hand data");
            menu.RefreshActions(); var placed=world.Hand.Slot.Value;
            button.onClick.Invoke();
            Check(ReferenceEquals(emptyGround.Value,placed) && world.Hand.Slot.IsEmpty,"ground button moves original equipment without selection");
            menu.RefreshActions(); Check(!button.gameObject.activeSelf,"ground button hides after placement");
            emptyGround.TakeValue(); world.Hand.Slot.TryInsert(new ItemData(34,"brood",ItemKind.BroodBox)); menu.RefreshActions();
            var occupied=new ItemData(35,"occupied",ItemKind.BroodBox); emptyGround.TryInsert(occupied);
            button.onClick.Invoke();
            Check(ReferenceEquals(emptyGround.Value,occupied) && !world.Hand.Slot.IsEmpty,"stale ground button cannot overwrite newly occupied ground");
        }
        finally { instance.SetValue(null,previous); UnityEngine.Object.DestroyImmediate(go); }
    }

    private static void TestActions()
    {
        for(int index=0;index<4;index++)
        {
            var parts=Parts(); var slot=new ItemSlot(); var hand=new ItemSlot(); slot.TryInsertValue(new ItemAssemblyData(parts));
            var state=new ObservationSelection(); Check(state.SelectPart(slot,parts[index]),"select data part");
            Check(ObservationActions.Execute(ObservationAction.Lift,state,slot,hand),"lift selected layer");
            var lower=ItemVisualFactory.GetParts(slot.Value); var upper=ItemVisualFactory.GetParts(hand.Value);
            Check(lower.Count==index && upper.Count==4-index,"split carries all upper parts");
            for(int i=0;i<upper.Count;i++)Check(ReferenceEquals(upper[i],parts[index+i]),"part identity retained");
            Check(!state.Revalidate(slot),"moved part cannot be reused");
        }
        var target=new ItemSlot(); var holding=new ItemSlot(); var selection=new ObservationSelection();
        selection.SelectGround(target); holding.TryInsert(new ItemData(1,"소비장",ItemKind.Frame));
        Check(ObservationActions.Available(selection,target,holding)==ObservationAction.None,"frame cannot be placed on ground");
        holding.TakeValue(); var brood=new ItemData(1,"단상",ItemKind.BroodBox); holding.TryInsert(brood);
        Check(ObservationActions.Execute(ObservationAction.Place,selection,target,holding) && ReferenceEquals(target.Value,brood),"ground placement preserves data");
        Check(ObservationActions.Available(selection,target,holding)==ObservationAction.None,"ground never lifts equipment");
        selection.SelectPart(target,brood); holding.TryInsert(new ItemData(2,"계상",ItemKind.Super));
        Check(ObservationActions.Execute(ObservationAction.Combine,selection,target,holding),"top combines through slot");
        holding.TryInsert(new ItemData(3,"뚜껑",ItemKind.Lid));
        Check(ObservationActions.Available(selection,target,holding)==ObservationAction.None,"middle selection cannot insert equipment");
        var top=((ItemAssemblyData)target.Value).Parts[1]; selection.SelectPart(target,top);
        Check(ObservationActions.Execute(ObservationAction.Combine,selection,target,holding),"new top combines lid");
        Check(!ObservationActions.Execute(ObservationAction.Lift,selection,new ItemSlot(),holding),"stale target rejected");
    }

    public static void RunBatch()
    {
        try
        {
            ItemAssemblyTests.RunAll(); ItemVisualTests.RunAll(); RunAll();
            File.WriteAllText("observation-tests-result.txt","PASS: "+checks+" observation checks; assembly and visual regressions passed.");
            ObservationPlayInputTests.BeginBatch();
        }
        catch(Exception e)
        {
            Debug.LogException(e); File.WriteAllText("observation-tests-result.txt","FAIL: "+e); EditorApplication.Exit(1);
        }
    }
}
