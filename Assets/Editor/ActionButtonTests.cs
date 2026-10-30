using System;
using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public static class ActionButtonTests
{
    private static int checks;
    private static void Check(bool ok, string message) { checks++; if (!ok) throw new Exception(message); }
    public static void RunBatch()
    {
        GameObject canvasObject = null;
        try
        {
            var catalog = Resources.Load<ActionButtonCatalog>("ActionButtonCatalog");
            foreach (ActionButtonId id in Enum.GetValues(typeof(ActionButtonId)))
                Check(catalog.Find(id) != null, "Catalog/icon " + id);
            canvasObject = new GameObject("ButtonTestCanvas", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
            var canvas = canvasObject.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var root = (RectTransform)canvasObject.transform;
            var buttonObject = new GameObject("Existing", typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(root, false);
            var button = buttonObject.GetComponent<Button>(); button.targetGraphic = buttonObject.GetComponent<Image>();
            int calls = 0; button.onClick.AddListener(() => calls++);
            var view = ActionButtonManager.Adopt(button, ActionButtonId.Back);
            Check(view == ActionButtonManager.Adopt(button, ActionButtonId.Back), "adoption is idempotent");
            Check(canvasObject.GetComponents<ActionButtonManager>().Length == 1 && buttonObject.GetComponents<ActionButtonView>().Length == 1, "no duplicate components");
            button.onClick.Invoke(); Check(calls == 1, "original listener retained exactly once");
            Canvas.ForceUpdateCanvases(); view.Apply();
            var rect = (RectTransform)button.transform;
            Vector3 p = rect.position; view.Apply(); Check(Vector3.Distance(p, rect.position) < .01f, "fixed position stable");
            var entry = new ActionButtonCatalog.Entry { size = new Vector2(80,80), placement = ActionButtonPlacement.Relative, anchor = new Vector2(.5f,.5f), offset = new Vector2(20,30), clampToScreen = false };
            var positioner = button.GetComponent<ActionButtonPositioner>();
            positioner.Apply(entry, root); p = rect.position; positioner.Apply(entry, root);
            Check(Vector3.Distance(p, rect.position) < .01f, "relative offset does not accumulate");
            entry.placement = ActionButtonPlacement.FollowTarget; positioner.SetFollowPoint(root.position, null, true, 16);
            positioner.Apply(entry, null); p = rect.position;
            positioner.SetFollowPoint(root.position + Vector3.right * 20, null, true, 16); positioner.Apply(entry, null);
            Check(Mathf.Abs(rect.position.x - p.x - 20) < .1f, "follow updates");
            entry.placement = ActionButtonPlacement.Fixed; entry.offset = Vector2.one * 100000; entry.clampToScreen = true;
            positioner.Apply(entry, null);
            var corners = new Vector3[4]; rect.GetWorldCorners(corners);
            Check(corners[2].x <= Screen.width + .1f && corners[2].y <= Screen.height + .1f, "screen clamp");
            var eventData = new PointerEventData(null) { eligibleForClick = true };
            view.OnBeginDrag(eventData); Check(!eventData.eligibleForClick, "drag cancels click");
            Check(button.targetGraphic.raycastTarget, "button blocks world input");
            foreach (var graphic in button.GetComponentsInChildren<Graphic>())
                if (graphic != button.targetGraphic) Check(!graphic.raycastTarget, "decorations do not capture rays");
            ActionButtonManager.SetVisible(button, false); Check(!button.gameObject.activeSelf, "hide removes hit target");
            ActionButtonManager.SetVisible(button, true); Check(button.gameObject.activeSelf, "show reuses button");
            var panel = new GameObject("LayoutParent", typeof(RectTransform), typeof(VerticalLayoutGroup));
            panel.transform.SetParent(root, false);
            ((RectTransform)panel.transform).sizeDelta = new Vector2(320, 210);
            button.transform.SetParent(panel.transform, false);
            var layoutElement = button.GetComponent<LayoutElement>(); layoutElement.ignoreLayout = false;
            entry.placement = ActionButtonPlacement.Relative; entry.useLayoutSlot = true;
            entry.offset = new Vector2(12, 8); entry.clampToScreen = false;
            positioner.Apply(entry, null); p = rect.position;
            for (int i = 0; i < 10; i++) positioner.Apply(entry, null);
            Check(Vector3.Distance(p, rect.position) < .01f, "layout slot offset does not drift");
            Check(Vector2.Distance(rect.rect.size, entry.size) < .01f, "catalog size survives force-expand layout");
            var prefab = AssetDatabase.LoadAssetAtPath<Button>("Assets/Prefabs/ActionButton.prefab");
            var labelButton = ActionButtonManager.Create(prefab, root, ActionButtonId.Back);
            var labelView = labelButton.GetComponent<ActionButtonView>();
            var label = labelButton.GetComponentInChildren<TMPro.TMP_Text>(true);
            var manager = canvasObject.GetComponent<ActionButtonManager>();
            var privateCatalog = UnityEngine.Object.Instantiate(catalog); manager.Catalog = privateCatalog;
            privateCatalog.Find(ActionButtonId.Back).showLabel = false; labelView.Apply();
            Check(!label.gameObject.activeSelf, "icon-only mode hides text");
            privateCatalog.Find(ActionButtonId.Back).icon = null; labelView.Apply();
            Check(label.gameObject.activeSelf && label.text == "뒤로가기", "missing icon falls back to readable text");
            privateCatalog.entries.Clear(); labelView.Apply();
            Check(!labelView.HasStyle && label.gameObject.activeSelf, "missing catalog entry preserves legacy display");
            UnityEngine.Object.DestroyImmediate(privateCatalog);
            File.WriteAllText("outputs/common-buttons/tests.txt", "PASS: " + checks + " common button checks");
            UnityEngine.Object.DestroyImmediate(canvasObject); EditorApplication.Exit(0);
        }
        catch (Exception e)
        {
            Debug.LogException(e); File.WriteAllText("outputs/common-buttons/tests.txt", "FAIL: " + e);
            if (canvasObject != null) UnityEngine.Object.DestroyImmediate(canvasObject);
            EditorApplication.Exit(1);
        }
    }
}
