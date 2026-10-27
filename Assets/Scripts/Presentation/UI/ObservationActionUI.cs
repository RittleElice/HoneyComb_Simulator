using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DefaultExecutionOrder(120)]
public class ObservationActionUI : MonoBehaviour
{
    [SerializeField] private ObservationSelectionController selection;
    [SerializeField] private Transform buttonContainer;
    [SerializeField] private Button buttonPrefab;
    [SerializeField] private CanvasGroup panelCanvasGroup;
    [SerializeField] private RectTransform actionLayer;
    [SerializeField] private ObservationItemPresenter presenter;
    [SerializeField] private float anchorGap = 16f;
    [SerializeField] private float screenPadding = 16f;
    private Button combineButton;
    private ItemSlot observedSlot;
    private ItemSlot expectedTarget, expectedSource;
    private MovableItem expectedTargetValue, expectedSourceValue;
    private ItemData expectedTop;
    private ObservationAction expectedAction;
    private Button actionButton;
    private TMP_Text title;
    private ItemSlot handSlot;
    private ItemSlotObserver observer;
    private bool dirty = true;

    private void OnEnable()
    {
        if (selection != null) selection.SelectionChanged += MarkDirty;
        dirty = true; SetVisible(false);
    }
    private void MarkDirty() => dirty = true;
    private void LateUpdate()
    {
        var currentHand = GameBootstrap.Instance != null && GameBootstrap.Instance.GameWorld != null
            ? GameBootstrap.Instance.GameWorld.Hand.Slot : null;
        var currentTarget = selection != null && selection.Observation != null ? selection.Observation.ObservedSlot : null;
        if (!ReferenceEquals(handSlot,currentHand) || !ReferenceEquals(observedSlot,currentTarget))
        {
            if (observer == null) observer = new ItemSlotObserver(_ => MarkDirty());
            observer.Clear(); handSlot = currentHand; observer.Observe(handSlot); observedSlot = currentTarget; observer.Observe(observedSlot); dirty = true;
        }
        if (dirty) RefreshActions();
        UpdatePositions();
    }
    public void RefreshActions()
    {
        dirty = false;
        if (selection == null || buttonContainer == null || buttonPrefab == null || panelCanvasGroup == null) return;
        RefreshCombine();
        var state = selection.Selection;
        var slot = selection.Observation != null ? selection.Observation.ObservedSlot : null;
        if (!state.Revalidate(slot)) { SetVisible(false); return; }
        if (title == null)
        {
            var go = new GameObject("SelectionTitle", typeof(RectTransform));
            go.transform.SetParent(buttonContainer,false);
            title = go.AddComponent<TextMeshProUGUI>(); title.raycastTarget = false;
            var template = buttonPrefab.GetComponentInChildren<TMP_Text>(true);
            if (template != null) { title.font = template.font; title.fontSharedMaterial = template.fontSharedMaterial; }
            title.fontSize = 24; title.color = Color.white;
            title.alignment = TextAlignmentOptions.Center;
            go.AddComponent<LayoutElement>().preferredHeight = 70;
        }
        title.text = state.Kind == ObservationSelectionKind.Ground ? "바닥" :
            state.Part.Kind == ItemKind.Lid ? "뚜껑" : $"{state.PartIndex+1}층 · {state.Part.Name}";
        if (actionButton != null) ItemVisualFactory.Release(actionButton.gameObject);
        actionButton = null;
        ObservationAction action = ObservationActions.Available(state,slot,handSlot);
        if (action == ObservationAction.Lift)
        {
            actionButton = Instantiate(buttonPrefab,buttonContainer,false);
            actionButton.gameObject.SetActive(true);
            var label = actionButton.GetComponentInChildren<TMP_Text>(true);
            if (label != null)
            {
                label.text =
                    state.Part.Kind == ItemKind.Lid ? "뚜껑 들기" :
                    slot.Value is ItemAssemblyData ? $"{state.PartIndex+1}층부터 위 모두 들기" : "들기";
                label.enableAutoSizing = true; label.fontSizeMin = 16; label.fontSizeMax = 25;
            }
            var layout = actionButton.GetComponent<LayoutElement>() ?? actionButton.gameObject.AddComponent<LayoutElement>();
            layout.preferredHeight = 90;
            var expectedPart = state.Part; var expectedSlot = slot; var expectedKind = state.Kind;
            var expectedValue = slot.Value; var expectedHand = handSlot?.Value;
            actionButton.onClick.AddListener(() =>
            {
                var current = selection.Observation.ObservedSlot;
                var liveHand = GameBootstrap.Instance != null && GameBootstrap.Instance.GameWorld != null ? GameBootstrap.Instance.GameWorld.Hand.Slot : null;
                if (ReferenceEquals(current,expectedSlot) && ReferenceEquals(current?.Value,expectedValue) &&
                    ReferenceEquals(liveHand,handSlot) && ReferenceEquals(liveHand?.Value,expectedHand) &&
                    state.Kind == expectedKind && ReferenceEquals(state.Part,expectedPart) &&
                    ObservationActions.Execute(action,state,current,liveHand)) selection.ClearSelection();
                dirty = true;
            });
        }
        SetVisible(true);
    }
    private void SetVisible(bool value)
    {
        if (panelCanvasGroup == null) return;
        panelCanvasGroup.alpha = value ? 1 : 0;
        panelCanvasGroup.interactable = panelCanvasGroup.blocksRaycasts = value;
    }
    private void OnDisable()
    {
        if (selection != null) selection.SelectionChanged -= MarkDirty;
        observer?.Clear(); handSlot = null; observedSlot = null; SetVisible(false); if (combineButton != null) combineButton.gameObject.SetActive(false);
    }
    private Image TopImage(out ItemData top)
    {
        top = null;
        var slot = selection != null && selection.Observation != null ? selection.Observation.ObservedSlot : null;
        var parts = ItemVisualFactory.GetParts(slot?.Value);
        if (parts.Count == 0 || presenter == null || presenter.CurrentView == null) return null;
        top = parts[parts.Count - 1];
        foreach (var view in presenter.CurrentView.Parts)
            if (ReferenceEquals(view.Part, top)) return view.Image;
        return null;
    }

    private ObservationAction FloatingAction(ItemSlot target, ItemSlot source, out Image image, out ItemData top)
    {
        image = null; top = null;
        if (target == null || source == null || ReferenceEquals(target,source)) return ObservationAction.None;
        if (target.IsEmpty)
        {
            // A temporary action context leaves the user's selection untouched.
            var groundSelection = new ObservationSelection();
            groundSelection.SelectGround(target);
            image = selection.GroundImage;
            return image != null ? ObservationActions.Available(groundSelection,target,source) : ObservationAction.None;
        }
        image = TopImage(out top);
        return image != null && target.CanCombineFrom(source) ? ObservationAction.Combine : ObservationAction.None;
    }

    private void RefreshCombine()
    {
        var target = selection.Observation != null ? selection.Observation.ObservedSlot : null;
        var action = FloatingAction(target, handSlot, out _, out var top);
        bool available = actionLayer != null && action != ObservationAction.None;
        if (available && combineButton == null)
        {
            combineButton = Instantiate(buttonPrefab, actionLayer, false);
            combineButton.name = "CombineButton";
            var rect = (RectTransform)combineButton.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.sizeDelta = new Vector2(280f, 80f);
            rect.localScale = Vector3.one;
            var label = combineButton.GetComponentInChildren<TMP_Text>(true);
            if (label != null) { label.text = "결합하기"; label.enableAutoSizing = true; label.fontSizeMin = 16; label.fontSizeMax = 25; }
            combineButton.onClick.AddListener(Combine);
        }
        expectedAction = action;
        expectedTarget = target; expectedSource = handSlot;
        expectedTargetValue = target?.Value; expectedSourceValue = handSlot?.Value; expectedTop = top;
        if (combineButton != null) combineButton.gameObject.SetActive(available);
    }

    private void Combine()
    {
        var target = selection.Observation != null ? selection.Observation.ObservedSlot : null;
        var source = GameBootstrap.Instance != null && GameBootstrap.Instance.GameWorld != null
            ? GameBootstrap.Instance.GameWorld.Hand.Slot : null;
        var action = FloatingAction(target, source, out var image, out var top);
        if (target != null && source != null && image != null &&
            ReferenceEquals(target, expectedTarget) && ReferenceEquals(source, expectedSource) &&
            ReferenceEquals(target.Value, expectedTargetValue) && ReferenceEquals(source.Value, expectedSourceValue) &&
            ReferenceEquals(top, expectedTop) && action != ObservationAction.None && action == expectedAction)
        {
            bool succeeded;
            if (action == ObservationAction.Place)
            {
                var groundSelection = new ObservationSelection();
                groundSelection.SelectGround(target);
                succeeded = ObservationActions.Execute(action,groundSelection,target,source);
            }
            else succeeded = target.TryCombineFrom(source);
            if (succeeded) selection.Synchronize();
        }
        dirty = true;
    }

    private void UpdatePositions()
    {
        if (actionLayer == null || selection == null) return;
        if (panelCanvasGroup != null && panelCanvasGroup.alpha > 0)
        {
            var image = selection.SelectedImage;
            if (image == null) SetVisible(false);
            else Follow((RectTransform)panelCanvasGroup.transform, image, false);
        }
        if (combineButton != null && combineButton.gameObject.activeSelf)
        {
            var target = selection.Observation != null ? selection.Observation.ObservedSlot : null;
            var action = FloatingAction(target,handSlot,out var image,out _);
            if (image == null || action == ObservationAction.None || action != expectedAction) { combineButton.gameObject.SetActive(false); dirty = true; }
            else Follow((RectTransform)combineButton.transform, image, true);
        }
    }

    private void Follow(RectTransform menu, Image image, bool above)
    {
        // Match the image's preserve-aspect drawing rectangle, including its pivot.
        Rect r = image.rectTransform.rect;
        if (image.preserveAspect && image.sprite != null && r.width > 0 && r.height > 0)
        {
            float ratio = image.sprite.rect.width / image.sprite.rect.height;
            float w = r.width, h = r.height;
            if (ratio > w / h) h = w / ratio; else w = h * ratio;
            r = new Rect(r.xMin + (r.width-w)*image.rectTransform.pivot.x,
                r.yMin + (r.height-h)*image.rectTransform.pivot.y, w, h);
        }
        Vector3 world = image.transform.TransformPoint(above ? new Vector3(r.center.x,r.yMax,0) : new Vector3(r.xMax,r.center.y,0));
        var canvas = actionLayer.GetComponentInParent<Canvas>().rootCanvas;
        Camera camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        Vector2 screen = RectTransformUtility.WorldToScreenPoint(selection.UICamera,world);
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(actionLayer,screen,camera,out var point)) return;
        Vector2 half = menu.rect.size * .5f;
        point += above ? new Vector2(0,half.y+anchorGap) : new Vector2(half.x+anchorGap,0);
        Rect bounds = actionLayer.rect;
        // Clamp to the physical screen as well as the layer (camera canvases may be smaller).
        RectTransformUtility.ScreenPointToLocalPointInRectangle(actionLayer,Vector2.zero,camera,out var min);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(actionLayer,new Vector2(Screen.width,Screen.height),camera,out var max);
        float left = Mathf.Max(bounds.xMin,min.x)+screenPadding+half.x;
        float right = Mathf.Min(bounds.xMax,max.x)-screenPadding-half.x;
        float bottom = Mathf.Max(bounds.yMin,min.y)+screenPadding+half.y;
        float top = Mathf.Min(bounds.yMax,max.y)-screenPadding-half.y;
        point.x = left <= right ? Mathf.Clamp(point.x,left,right) : (left+right)*.5f;
        point.y = bottom <= top ? Mathf.Clamp(point.y,bottom,top) : (bottom+top)*.5f;
        menu.localPosition = new Vector3(point.x,point.y,0);
    }

    private void OnDestroy()
    {
        observer?.Dispose();
        if (combineButton != null) ItemVisualFactory.Release(combineButton.gameObject);
    }
}
