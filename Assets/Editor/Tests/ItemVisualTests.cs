using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class ItemVisualTests
{
    private static int checks;
    private static void Check(bool value, string message)
    {
        checks++;
        if (!value) throw new Exception("Item visual test failed: " + message);
    }
    private static void Near(Vector3 a, Vector3 b, string message) => Check((a-b).magnitude < 0.02f, message);

    [MenuItem("Tools/HoneyComb/Run Item Visual Tests")]
    public static void RunAll()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Run visual tests outside Play Mode.");
        checks = 0;
        Scene shared = EditorSceneManager.OpenPreviewScene("Assets/Scenes/SharedUI.unity");
        Scene preview = EditorSceneManager.NewPreviewScene();
        GameObject root = new GameObject("VisualTestRoot", typeof(RectTransform));
        SceneManager.MoveGameObjectToScene(root, preview);
        try
        {
            var registry = shared.GetRootGameObjects()
                .SelectMany(go => go.GetComponentsInChildren<ItemPrefabRegistry>(true)).Single();
            Check(registry.Entries.Count == 16, "all sixteen registrations retained");
            foreach (var entry in registry.Entries)
            {
                Check(entry.prefab != null && entry.sprite != null, "valid registered prefab and sprite");
                var data = new ItemData(1, "test", entry.kind);
                IItemPartView part = ItemVisualFactory.Create(data, 3, entry.context, registry, null, root.transform);
                Check(part != null, "factory creates " + entry.kind + "/" + entry.context);
                Check(ReferenceEquals(part.Part, data) && part.PartIndex == 3, "original reference and part index");
                Sprite actual = part is UIItemPartView ui ? ui.Image.sprite : ((WorldItemPartView)part).Renderer.sprite;
                Check(actual == entry.sprite, "registry sprite used at runtime");
                Sprite prefabSprite = entry.context == ItemViewContext.Apiary
                    ? entry.prefab.GetComponent<SpriteRenderer>().sprite
                    : entry.prefab.GetComponent<UIItemPartView>().Image.sprite;
                Check(prefabSprite == entry.sprite, "prefab preview synchronized");
                if (entry.context == ItemViewContext.Observation)
                {
                    Check(AssetDatabase.GetAssetPath(entry.prefab).EndsWith("_Observation.prefab"), "observation naming");
                    Check(entry.prefab != registry.GetPrefab(entry.kind, ItemViewContext.Hand), "separate observation prefab");
                    Check(part.PartTransform.Find("BottomAnchor") != null && part.PartTransform.Find("TopAnchor") != null,
                        "explicit observation contacts");
                }
                ItemVisualFactory.Release(part.PartTransform.gameObject);
            }

            TestUI(root.transform, registry, ItemViewContext.Inventory, AssemblyAlignment.Center, AssemblyScaleMode.FitWithinBounds);
            TestUI(root.transform, registry, ItemViewContext.Hand, AssemblyAlignment.Bottom, AssemblyScaleMode.Fixed);
            TestUI(root.transform, registry, ItemViewContext.Observation, AssemblyAlignment.BaseAnchor, AssemblyScaleMode.FitWithinBounds);
            TestWorld(root.transform, registry);
            TestPivot(root.transform, registry);
            TestPrefabSync(root.transform, registry);

            Scene observation = EditorSceneManager.OpenPreviewScene("Assets/Scenes/Observation.unity");
            try
            {
                var presenters = observation.GetRootGameObjects()
                    .SelectMany(go => go.GetComponentsInChildren<ObservationItemPresenter>(true)).ToArray();
                Check(presenters.Length == 1, "one connected observation presenter");
                var presenter = presenters[0];
                var so = new SerializedObject(presenter);
                var ground = so.FindProperty("groundImage").objectReferenceValue as RectTransform;
                Check(ground != null && so.FindProperty("itemContainer").objectReferenceValue != null, "observation Inspector references");
                so.FindProperty("prefabRegistry").objectReferenceValue = registry;
                so.ApplyModifiedPropertiesWithoutUndo();
                var brood = new ItemData(2,"brood",ItemKind.BroodBox);
                presenter.Show(brood);
                Vector3 center = ground.TransformPoint(ground.rect.center);
                Near(presenter.CurrentView.Parts[0].BottomWorld, center, "single brood contact on ground");
                presenter.Show(new ItemAssemblyData(new[] { brood, new ItemData(3,"super",ItemKind.Super), new ItemData(4,"lid",ItemKind.Lid) }));
                Near(presenter.CurrentView.Parts[0].BottomWorld, center, "assembled brood contact remains on ground");
                presenter.Clear();
                Check(presenter.CurrentView == null, "observation clear");
            }
            finally { EditorSceneManager.ClosePreviewScene(observation); }
            Debug.Log("Item visual tests passed: " + checks + " checks.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
            EditorSceneManager.ClosePreviewScene(preview);
            EditorSceneManager.ClosePreviewScene(shared);
        }
    }

    private static void TestUI(Transform parent, ItemPrefabRegistry registry, ItemViewContext context,
        AssemblyAlignment alignment, AssemblyScaleMode scaleMode)
    {
        var data = new[] { new ItemData(1,"brood",ItemKind.BroodBox), new ItemData(2,"super",ItemKind.Super),
            new ItemData(3,"super2",ItemKind.Super), new ItemData(4,"lid",ItemKind.Lid) };
        var assembly = new ItemAssemblyData(data);
        var go = new GameObject("Assembly",typeof(RectTransform));
        go.transform.SetParent(parent,false);
        var view = go.AddComponent<ItemAssemblyView>();
        view.Initialize(assembly,registry,context,null,600f,800f,alignment,scaleMode);
        Check(ReferenceEquals(view.Item,assembly) && view.Parts.Count==4, "assembly identity/count");
        for(int i=0;i<4;i++)
        {
            Check(ReferenceEquals(view.Parts[i].Part,data[i]) && view.Parts[i].PartIndex==i, "ordered actual indices");
            if(i>0) Near(view.Parts[i-1].TopWorld,view.Parts[i].BottomWorld,"contact coincidence");
        }
        Bounds bounds=ItemAssemblyLayout.GetBounds(view.Parts,go.transform);
        if(context!=ItemViewContext.Observation)
        {
            float expectedHeight=0f,expectedWidth=0f;
            foreach(var part in view.Parts)
            {
                expectedHeight+=part.VisibleRect.height;
                expectedWidth=Mathf.Max(expectedWidth,part.VisibleRect.width);
            }
            Near(bounds.size,new Vector3(expectedWidth,expectedHeight,0f),"legacy image-bounds footprint preserved");
            for(int i=0;i<4;i++)
                Near(view.Parts[i].RectTransform.pivot,new Vector2(.5f,.5f),"existing prefab pivots retained");
        }
        if(alignment==AssemblyAlignment.Center) Near(bounds.center,Vector3.zero,"inventory centered");
        if(alignment==AssemblyAlignment.Bottom) Check(Mathf.Abs(bounds.min.y)<.02f,"hand bottom alignment");
        if(alignment==AssemblyAlignment.BaseAnchor) Near(view.Parts[0].BottomWorld,go.transform.position,"base anchor origin");
        if(scaleMode==AssemblyScaleMode.Fixed) Near(go.transform.localScale,Vector3.one,"hand fixed scale");
        else Check(bounds.size.x*go.transform.localScale.x<=600.02f && bounds.size.y*go.transform.localScale.y<=800.02f,"fit bounds");

        // Rotated/scaled parent and explicit contact moves still coincide.
        parent.localRotation=Quaternion.Euler(0f,0f,11f);
        parent.localScale=new Vector3(1.1f,.8f,1f);
        view.AlignBaseTo(new Vector3(100f,-50f,0f));
        Near(view.Parts[0].BottomWorld,new Vector3(100f,-50f,0f),"transformed contact alignment");
        for(int i=1;i<4;i++) Near(view.Parts[i-1].TopWorld,view.Parts[i].BottomWorld,"stack retained after base move");
        parent.localRotation=Quaternion.identity; parent.localScale=Vector3.one;
        view.Initialize(null,registry,context,null,600f,800f,alignment,scaleMode);
        Check(view.Parts.Count==0 && view.Item==null && go.transform.childCount==0,"reinitialize/clear removes visuals");
        UnityEngine.Object.DestroyImmediate(go);
    }

    private static void TestWorld(Transform parent,ItemPrefabRegistry registry)
    {
        var lower=(WorldItemPartView)ItemVisualFactory.Create(new ItemData(1,"brood",ItemKind.BroodBox),0,ItemViewContext.Apiary,registry,null,parent);
        var upper=(WorldItemPartView)ItemVisualFactory.Create(new ItemData(2,"super",ItemKind.Super),1,ItemViewContext.Apiary,registry,null,parent);
        lower.ApplyLayout(2,3,9,1f); upper.ApplyLayout(2,3,9,1f);
        lower.transform.rotation=Quaternion.Euler(45f,0f,0f);
        ItemAssemblyLayout.Stack(lower,upper,true);
        Near(lower.TopWorld,upper.BottomWorld,"world contacts coincide");
        Near(upper.transform.eulerAngles,lower.transform.eulerAngles,"world rotation preserved");
        Bounds b=lower.Renderer.sprite.bounds;
        Near(lower.TopWorld,lower.transform.TransformPoint(new Vector3(b.center.x,b.max.y,b.center.z)),"legacy world edge fallback");
        Check(lower.GridX==2 && lower.GridY==3,"world grid identity");
        ItemVisualFactory.Release(lower.gameObject); ItemVisualFactory.Release(upper.gameObject);
    }

    private static void TestPivot(Transform parent,ItemPrefabRegistry registry)
    {
        var ui=(UIItemPartView)ItemVisualFactory.Create(new ItemData(9,"test",ItemKind.Super),0,ItemViewContext.Inventory,registry,null,parent);
        ui.RectTransform.pivot=new Vector2(.2f,.7f);
        ItemAssemblyLayout.AlignBottom(ui,new Vector3(100,200,0));
        Near(ui.RectTransform.pivot,new Vector2(.2f,.7f),"custom pivot not overwritten by layout");
        Near(ui.BottomWorld,new Vector3(100,200,0),"custom pivot contact");
        ItemVisualFactory.Release(ui.gameObject);
    }

    private static void TestPrefabSync(Transform parent,ItemPrefabRegistry source)
    {
        string folder="Assets/__ItemVisualTest_"+Guid.NewGuid().ToString("N");
        AssetDatabase.CreateFolder("Assets",Path.GetFileName(folder));
        var go=new GameObject("TestRegistry"); go.transform.SetParent(parent,false);
        try
        {
            string file=folder+"/Temporary.prefab";
            AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(source.GetPrefab(ItemKind.BroodBox,ItemViewContext.Inventory)),file);
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(file);
            var registry=go.AddComponent<ItemPrefabRegistry>();
            var serialized=new SerializedObject(registry);
            var entries=serialized.FindProperty("entries"); entries.arraySize=1;
            var entry=entries.GetArrayElementAtIndex(0);
            entry.FindPropertyRelative("kind").enumValueIndex=(int)ItemKind.BroodBox;
            entry.FindPropertyRelative("context").enumValueIndex=(int)ItemViewContext.Inventory;
            entry.FindPropertyRelative("prefab").objectReferenceValue=prefab;
            Sprite a=source.GetSprite(ItemKind.Super,ItemViewContext.Observation);
            Sprite b=source.GetSprite(ItemKind.Lid,ItemViewContext.Observation);
            entry.FindPropertyRelative("sprite").objectReferenceValue=a;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            Check(ItemPrefabRegistryEditor.SyncPrefabs(registry)==1 && prefab.GetComponent<UIItemPartView>().Image.sprite==a,"registry sync changes prefab asset");
            Check(ItemPrefabRegistryEditor.SyncPrefabs(registry)==0,"sync is idempotent");
            serialized.Update(); entries=serialized.FindProperty("entries");
            entries.GetArrayElementAtIndex(0).FindPropertyRelative("sprite").objectReferenceValue=b;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            Check(ItemPrefabRegistryEditor.SyncPrefabs(registry)==1 && prefab.GetComponent<UIItemPartView>().Image.sprite==b,"replacing registry image updates prefab again");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(go);
            AssetDatabase.DeleteAsset(folder);
        }
    }

    public static void RunBatch()
    {
        try
        {
            ItemAssemblyTests.RunAll();
            RunAll();
            File.WriteAllText("visual-tests-result.txt","PASS: "+checks+" visual checks; assembly tests also passed.");
            EditorApplication.Exit(0);
        }
        catch(Exception exception)
        {
            Debug.LogException(exception);
            File.WriteAllText("visual-tests-result.txt","FAIL: "+exception);
            EditorApplication.Exit(1);
        }
    }
}
