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
        if (!ReferenceEquals(handSlot,currentHand))
        {
            if (observer == null) observer = new ItemSlotObserver(_ => MarkDirty());
            observer.Clear(); handSlot = currentHand; observer.Observe(handSlot); dirty = true;
        }
        if (dirty) RefreshActions();
    }
    public void RefreshActions()
    {
        dirty = false;
        if (selection == null || buttonContainer == null || buttonPrefab == null || panelCanvasGroup == null) return;
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
        if (action != ObservationAction.None)
        {
            actionButton = Instantiate(buttonPrefab,buttonContainer,false);
            actionButton.gameObject.SetActive(true);
            var label = actionButton.GetComponentInChildren<TMP_Text>(true);
            if (label != null)
            {
                label.text = action == ObservationAction.Place ? "놓기" : action == ObservationAction.Combine ? "위에 결합하기" :
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
        observer?.Clear(); handSlot = null; SetVisible(false);
    }
    private void OnDestroy() => observer?.Dispose();
}
