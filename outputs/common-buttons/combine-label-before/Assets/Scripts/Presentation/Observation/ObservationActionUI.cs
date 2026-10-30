
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DefaultExecutionOrder(120)]
public class ObservationActionUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private ObservationSelectionController selection;
    [SerializeField] private Transform buttonContainer;
    [SerializeField] private Button buttonPrefab;
    [SerializeField] private CanvasGroup panelCanvasGroup;
    [SerializeField] private RectTransform actionLayer;
    [SerializeField] private ObservationItemPresenter presenter;

    [Header("Position")]
    [SerializeField] private float anchorGap = 16f;
    [SerializeField] private float screenPadding = 16f;
    
    [Header("Frame Slot Menu")]
    [SerializeField]
    private float frameMenuGap = 20f;

    private Button combineButton;
    private Button actionButton;
    private TMP_Text title;

    private ItemSlot observedSlot;
    private ItemSlot handSlot;
    private ItemSlot observedFrameSlot;
    private ItemSlot observedTopSlot;

    private ItemSlotObserver observer;
    private bool dirty = true;

    // 자동 결합 버튼의 기존 데이터 검증
    private ItemSlot expectedTarget;
    private ItemSlot expectedSource;

    private MovableItem expectedTargetValue;
    private MovableItem expectedSourceValue;

    private ItemData expectedTop;
    private ObservationAction expectedAction;

    private void OnEnable()
    {
        if (selection != null)
            selection.SelectionChanged += MarkDirty;

        dirty = true;
        SetVisible(false);
    }

    private void MarkDirty()
    {
        dirty = true;
    }

    private void LateUpdate()
    {
        ItemSlot currentHand =
            GameBootstrap.Instance != null &&
            GameBootstrap.Instance.GameWorld != null
                ? GameBootstrap.Instance.GameWorld.Hand.Slot
                : null;

        ItemSlot currentTarget =
            selection != null &&
            selection.Observation != null
                ? selection.Observation.ObservedSlot
                : null;

        ItemSlot currentFrameSlot = null;

        if (selection != null &&
            selection.Selection.Kind ==
            ObservationSelectionKind.FrameSlot)
        {
            currentFrameSlot =
                selection.Selection.GetSelectedFrameSlot();
        }

        ItemSlot currentTopSlot = ObservationSelection.GetExposedBox(currentTarget)?.TopSlot;

        bool referencesChanged =
            !ReferenceEquals(observedTopSlot, currentTopSlot) ||
            !ReferenceEquals(handSlot, currentHand) ||
            !ReferenceEquals(observedSlot, currentTarget) ||
            !ReferenceEquals(observedFrameSlot, currentFrameSlot);

        if (referencesChanged)
        {
            observedTopSlot = currentTopSlot;
            handSlot = currentHand;
            observedSlot = currentTarget;
            observedFrameSlot = currentFrameSlot;

            RebindObserver();
            dirty = true;
        }

        if (dirty)
            RefreshActions();

        UpdatePositions();
    }

    private void RebindObserver()
    {
        if (observer == null)
            observer = new ItemSlotObserver(_ => MarkDirty());

        observer.Clear();

        if (handSlot != null)
            observer.Observe(handSlot);

        if (observedSlot != null)
            observer.Observe(observedSlot);

        if (observedFrameSlot != null)
            observer.Observe(observedFrameSlot);
        if (observedTopSlot != null)
            observer.Observe(observedTopSlot);
    }

    public void RefreshActions()
    {
        dirty = false;

        if (selection == null ||
            buttonContainer == null ||
            buttonPrefab == null ||
            panelCanvasGroup == null)
        {
            return;
        }

        // 자동 결합 버튼은 선택 상태와 독립적으로 갱신
        RefreshCombine();

        ObservationSelection state = selection.Selection;

        ItemSlot slot =
            selection.Observation != null
                ? selection.Observation.ObservedSlot
                : null;

        if (!state.Revalidate(slot))
        {
            SetVisible(false);
            return;
        }

        EnsureTitle();

        ItemSlot frameSlot =
            state.Kind == ObservationSelectionKind.FrameSlot
                ? state.GetSelectedFrameSlot()
                : null;

        title.text = BuildTitle(state, frameSlot);

        if (actionButton != null)
        {
            ItemVisualFactory.Release(actionButton.gameObject);
            actionButton = null;
        }

        ObservationAction action =
            ObservationActions.Available(
                state,
                slot,
                handSlot);

        // Combine/Place는 기존 독립 버튼에서 처리
        if (action == ObservationAction.Lift ||
            action == ObservationAction.InsertFrame ||
            action == ObservationAction.TakeFrame ||
            action == ObservationAction.TakeTopAttachment)
        {
            CreateActionButton(
                action,
                state,
                slot,
                frameSlot);
        }

        SetVisible(true);
    }

    private void EnsureTitle()
    {
        if (title != null)
            return;

        GameObject go = new GameObject(
            "SelectionTitle",
            typeof(RectTransform));

        go.transform.SetParent(buttonContainer, false);

        title = go.AddComponent<TextMeshProUGUI>();
        title.raycastTarget = false;

        TMP_Text template =
            buttonPrefab.GetComponentInChildren<TMP_Text>(true);

        if (template != null)
        {
            title.font = template.font;
            title.fontSharedMaterial =
                template.fontSharedMaterial;
        }

        title.fontSize = 24f;
        title.color = Color.white;
        title.alignment = TextAlignmentOptions.Center;

        LayoutElement layout =
            go.AddComponent<LayoutElement>();

        layout.preferredHeight = 70f;
    }

    private static string BuildTitle(
        ObservationSelection state,
        ItemSlot frameSlot)
    {
        switch (state.Kind)
        {
            case ObservationSelectionKind.TopAttachment:
                return state.Part.Name;

            case ObservationSelectionKind.Ground:
                return "바닥";

            case ObservationSelectionKind.FrameSlot:
            {
                string status =
                    frameSlot == null || frameSlot.IsEmpty
                        ? "빈 슬롯"
                        : "소비장 있음";

                return
                    $"{state.PartIndex + 1}층 " +
                    $"소비장 {state.FrameSlotIndex + 1}번\n" +
                    status;
            }

            case ObservationSelectionKind.EquipmentPart:
                return state.Part.Kind == ItemKind.Lid
                    ? "뚜껑"
                    : $"{state.PartIndex + 1}층 · {state.Part.Name}";

            default:
                return "";
        }
    }

    private void CreateActionButton(
        ObservationAction action,
        ObservationSelection state,
        ItemSlot slot,
        ItemSlot frameSlot)
    {
        actionButton = ActionButtonManager.Create(buttonPrefab, buttonContainer,
            action == ObservationAction.InsertFrame ? ActionButtonId.InsertFrame :
            action == ObservationAction.TakeFrame ? ActionButtonId.TakeFrame : ActionButtonId.Lift,
            GetActionLabel(action, state, slot));

        actionButton.gameObject.SetActive(true);

        TMP_Text label =
            actionButton.GetComponentInChildren<TMP_Text>(true);

        if (label != null)
        {
            label.text = GetActionLabel(action, state, slot);

            label.enableAutoSizing = true;
            label.fontSizeMin = 16f;
            label.fontSizeMax = 25f;
        }

        LayoutElement layout =
            actionButton.GetComponent<LayoutElement>() ??
            actionButton.gameObject.AddComponent<LayoutElement>();

        layout.preferredHeight = 90f;

        // 버튼 생성 시점의 선택 상태
        ItemSlot expectedSlot = slot;
        MovableItem expectedValue = slot?.Value;

        ItemSlot expectedHandSlot = handSlot;
        MovableItem expectedHandValue = handSlot?.Value;

        ObservationSelectionKind expectedKind = state.Kind;
        ItemData expectedPart = state.Part;
        int expectedPartIndex = state.PartIndex;
        int expectedFrameIndex = state.FrameSlotIndex;

        ItemSlot expectedAttachmentSlot = state.SelectedTopAttachmentSlot;
        MovableItem expectedAttachment = expectedAttachmentSlot?.Value;

        ItemSlot expectedFrameSlot = frameSlot;
        MovableItem expectedFrameValue = frameSlot?.Value;

        actionButton.onClick.AddListener(() =>
        {
            ItemSlot current =
                selection.Observation != null
                    ? selection.Observation.ObservedSlot
                    : null;

            ItemSlot liveHand =
                GameBootstrap.Instance != null &&
                GameBootstrap.Instance.GameWorld != null
                    ? GameBootstrap.Instance.GameWorld.Hand.Slot
                    : null;

            ObservationSelection liveState =
                selection.Selection;

            // 현재 선택을 다시 검증
            if (!liveState.Revalidate(current))
            {
                dirty = true;
                return;
            }

            ItemSlot liveFrameSlot =
                liveState.Kind ==
                ObservationSelectionKind.FrameSlot
                    ? liveState.GetSelectedFrameSlot()
                    : null;

            bool valid =
                ReferenceEquals(liveState.SelectedTopAttachmentSlot, expectedAttachmentSlot) &&
                ReferenceEquals(liveState.SelectedTopAttachmentSlot?.Value, expectedAttachment) &&
                ReferenceEquals(current, expectedSlot) &&
                ReferenceEquals(current?.Value, expectedValue) &&
                ReferenceEquals(liveHand, expectedHandSlot) &&
                ReferenceEquals(liveHand?.Value, expectedHandValue) &&
                liveState.Kind == expectedKind &&
                ReferenceEquals(liveState.Part, expectedPart) &&
                liveState.PartIndex == expectedPartIndex &&
                liveState.FrameSlotIndex == expectedFrameIndex &&
                ReferenceEquals(liveFrameSlot, expectedFrameSlot) &&
                ReferenceEquals(
                    liveFrameSlot?.Value,
                    expectedFrameValue);

            if (!valid)
            {
                dirty = true;
                return;
            }

            bool succeeded =
                ObservationActions.Execute(
                    action,
                    liveState,
                    current,
                    liveHand);

            if (succeeded)
            {
                if (action == ObservationAction.InsertFrame ||
                    action == ObservationAction.TakeFrame)
                {
                    // 같은 소비장 슬롯 선택 유지
                    selection.Synchronize();
                }
                else
                {
                    selection.ClearSelection();
                }
            }

            dirty = true;
        });
    }

    private static string GetActionLabel(
        ObservationAction action,
        ObservationSelection state,
        ItemSlot slot)
    {
        switch (action)
        {
            case ObservationAction.TakeTopAttachment:
                return "들기";

            case ObservationAction.InsertFrame:
                return "소비장 넣기";

            case ObservationAction.TakeFrame:
                return "소비장 꺼내기";

            case ObservationAction.Lift:
                if (state.Part.Kind == ItemKind.Lid)
                    return "뚜껑 들기";

                return slot.Value is ItemAssemblyData
                    ? $"{state.PartIndex + 1}층부터 위 모두 들기"
                    : "들기";

            default:
                return "";
        }
    }

    private void SetVisible(bool value)
    {
        if (panelCanvasGroup == null)
            return;

        panelCanvasGroup.alpha = value ? 1f : 0f;
        panelCanvasGroup.interactable = value;
        panelCanvasGroup.blocksRaycasts = value;
    }

    // -------------------------------------------------
    // 자동 결합 버튼: 기존 동작 유지
    // -------------------------------------------------

    private Image TopImage(out ItemData top)
    {
        top = null;

        ItemSlot slot =
            selection != null &&
            selection.Observation != null
                ? selection.Observation.ObservedSlot
                : null;

        var parts =
            ItemVisualFactory.GetParts(slot?.Value);

        if (parts.Count == 0 ||
            presenter == null ||
            presenter.CurrentView == null)
            return null;

        top = parts[parts.Count - 1];

        foreach (var view in presenter.CurrentView.Parts)
        {
            if (ReferenceEquals(view.Part, top))
                return view.Image;
        }

        return null;
    }

    private ObservationAction FloatingAction(
        ItemSlot target,
        ItemSlot source,
        out Image image,
        out ItemData top)
    {
        image = null;
        top = null;

        if (target == null ||
            source == null ||
            ReferenceEquals(target, source))
            return ObservationAction.None;

        if (target.IsEmpty)
        {
            ObservationSelection groundSelection =
                new ObservationSelection();

            groundSelection.SelectGround(target);

            image = selection.GroundImage;

            return image != null
                ? ObservationActions.Available(
                    groundSelection,
                    target,
                    source)
                : ObservationAction.None;
        }

        image = TopImage(out top);

        return image != null &&
               target.CanCombineFrom(source)
            ? ObservationAction.Combine
            : ObservationAction.None;
    }

    private void RefreshCombine()
    {
        ItemSlot target =
            selection.Observation != null
                ? selection.Observation.ObservedSlot
                : null;

        ObservationAction action =
            FloatingAction(
                target,
                handSlot,
                out _,
                out ItemData top);

        bool available =
            actionLayer != null &&
            action != ObservationAction.None;

        if (available && combineButton == null)
        {
            combineButton = ActionButtonManager.Create(buttonPrefab, actionLayer, ActionButtonId.Combine);

            combineButton.name = "CombineButton";

            RectTransform rect =
                (RectTransform)combineButton.transform;

            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);

            rect.sizeDelta = new Vector2(280f, 80f);
            rect.localScale = Vector3.one;

            TMP_Text label =
                combineButton.GetComponentInChildren<TMP_Text>(true);

            if (label != null)
            {
                label.text = "결합하기";
                label.enableAutoSizing = true;
                label.fontSizeMin = 16f;
                label.fontSizeMax = 25f;
            }

            combineButton.onClick.AddListener(Combine);
        }

        if (combineButton != null && action != ObservationAction.None)
            ActionButtonManager.Adopt(combineButton, action == ObservationAction.Place ? ActionButtonId.Place : ActionButtonId.Combine);

        expectedAction = action;

        expectedTarget = target;
        expectedSource = handSlot;

        expectedTargetValue = target?.Value;
        expectedSourceValue = handSlot?.Value;

        expectedTop = top;

        if (combineButton != null)
            ActionButtonManager.SetVisible(combineButton, available);
    }

    private void Combine()
    {
        ItemSlot target =
            selection.Observation != null
                ? selection.Observation.ObservedSlot
                : null;

        ItemSlot source =
            GameBootstrap.Instance != null &&
            GameBootstrap.Instance.GameWorld != null
                ? GameBootstrap.Instance.GameWorld.Hand.Slot
                : null;

        ObservationAction action =
            FloatingAction(
                target,
                source,
                out Image image,
                out ItemData top);

        bool valid =
            target != null &&
            source != null &&
            image != null &&
            ReferenceEquals(target, expectedTarget) &&
            ReferenceEquals(source, expectedSource) &&
            ReferenceEquals(target.Value, expectedTargetValue) &&
            ReferenceEquals(source.Value, expectedSourceValue) &&
            ReferenceEquals(top, expectedTop) &&
            action != ObservationAction.None &&
            action == expectedAction;

        if (!valid)
        {
            dirty = true;
            return;
        }

        bool succeeded;

        if (action == ObservationAction.Place)
        {
            ObservationSelection groundSelection =
                new ObservationSelection();

            groundSelection.SelectGround(target);

            succeeded =
                ObservationActions.Execute(
                    action,
                    groundSelection,
                    target,
                    source);
        }
        else
        {
            succeeded = target.TryCombineFrom(source);
        }

        if (succeeded)
            selection.Synchronize();

        dirty = true;
    }

    // -------------------------------------------------
    // 메뉴 위치 추적
    // -------------------------------------------------

    private void UpdatePositions()
    {
        if (actionLayer == null || selection == null)
            return;

        if (panelCanvasGroup != null &&
            panelCanvasGroup.alpha > 0f)
        {
            RectTransform menu =
                (RectTransform)panelCanvasGroup.transform;

            if (selection.Selection.Kind ==
                ObservationSelectionKind.FrameSlot)
            {
                if (!FollowFrameSlot(menu))
                    SetVisible(false);
            }
            else
            {
                Image image = selection.SelectedImage;

                if (image == null)
                    SetVisible(false);
                else
                    Follow(menu, image, selection.Selection.Kind == ObservationSelectionKind.TopAttachment);
            }
        }

        if (combineButton != null &&
            combineButton.gameObject.activeSelf)
        {
            ItemSlot target =
                selection.Observation != null
                    ? selection.Observation.ObservedSlot
                    : null;

            ObservationAction action =
                FloatingAction(
                    target,
                    handSlot,
                    out Image image,
                    out _);

            if (image == null ||
                action == ObservationAction.None ||
                action != expectedAction)
            {
                combineButton.gameObject.SetActive(false);
                dirty = true;
            }
            else
            {
                Follow(
                    (RectTransform)combineButton.transform,
                    image,
                    true);
            }
        }
    }

   
    private bool FollowFrameSlot(RectTransform menu)
    {
        if (presenter == null ||
            presenter.CurrentView == null ||
            selection == null)
            return false;

        ObservationSelection state = selection.Selection;

        if (state.Kind != ObservationSelectionKind.FrameSlot)
            return false;

        foreach (var part in presenter.CurrentView.Parts)
        {
            if (!ReferenceEquals(part.Part, state.Part))
                continue;

            ObservationFrameSlotLayout layout =
                part.GetComponent<ObservationFrameSlotLayout>();

            if (layout == null)
                return false;

            Vector3[] polygon =
                layout.GetSlotWorldPolygon(
                    state.FrameSlotIndex);

            if (polygon == null || polygon.Length < 4)
                return false;

            // 선택한 소비장 슬롯의 위쪽 두 점
            // (기존 polygon[1], polygon[2]는 오른쪽 변)
            Vector3 topCenter =
                (polygon[2] + polygon[3]) * 0.5f;

            // true = 메뉴를 기준점 위쪽에 배치
            return FollowWorldPoint(
                menu,
                topCenter,
                true);
        }

        return false;
    }
    public void FollowTopAttachment(RectTransform menu, Image image)
    {
        Follow(menu, image, true);
    }

    private void Follow(
        RectTransform menu,
        Image image,
        bool above)
    {
        Rect r = image.rectTransform.rect;

        if (image.preserveAspect &&
            image.sprite != null &&
            r.width > 0f &&
            r.height > 0f)
        {
            float ratio =
                image.sprite.rect.width /
                image.sprite.rect.height;

            float w = r.width;
            float h = r.height;

            if (ratio > w / h)
                h = w / ratio;
            else
                w = h * ratio;

            r = new Rect(
                r.xMin +
                    (r.width - w) *
                    image.rectTransform.pivot.x,
                r.yMin +
                    (r.height - h) *
                    image.rectTransform.pivot.y,
                w,
                h);
        }

        Vector3 worldPoint =
            image.transform.TransformPoint(
                above
                    ? new Vector3(r.center.x, r.yMax, 0f)
                    : new Vector3(r.xMax, r.center.y, 0f));

        FollowWorldPoint(menu, worldPoint, above);
    }

    private bool FollowWorldPoint(
        RectTransform menu,
        Vector3 worldPoint,
        bool above)
    {
        if (panelCanvasGroup != null && menu == panelCanvasGroup.transform && actionButton != null)
        {
            var selectedView = actionButton.GetComponent<ActionButtonView>();
            if (selectedView != null) selectedView.SetFollowPoint(worldPoint, selection.UICamera, above, anchorGap);
        }
        var common = menu.GetComponent<ActionButtonView>();
        if (common != null && common.HasStyle)
        {
            common.SetFollowPoint(worldPoint, selection.UICamera, above, anchorGap);
            common.Apply();
            return true;
        }

        Canvas canvas =
            actionLayer.GetComponentInParent<Canvas>().rootCanvas;

        Camera camera =
            canvas.renderMode == RenderMode.ScreenSpaceOverlay
                ? null
                : canvas.worldCamera;

        Vector2 screenPoint =
            RectTransformUtility.WorldToScreenPoint(
                selection.UICamera,
                worldPoint);

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                actionLayer,
                screenPoint,
                camera,
                out Vector2 point))
        {
            return false;
        }

        Vector2 half = menu.rect.size * 0.5f;

        point += above
            ? new Vector2(0f, half.y + anchorGap)
            : new Vector2(half.x + anchorGap, 0f);

        Rect bounds = actionLayer.rect;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            actionLayer,
            Vector2.zero,
            camera,
            out Vector2 min);

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            actionLayer,
            new Vector2(Screen.width, Screen.height),
            camera,
            out Vector2 max);

        float left =
            Mathf.Max(bounds.xMin, min.x) +
            screenPadding + half.x;

        float right =
            Mathf.Min(bounds.xMax, max.x) -
            screenPadding - half.x;

        float bottom =
            Mathf.Max(bounds.yMin, min.y) +
            screenPadding + half.y;

        float top =
            Mathf.Min(bounds.yMax, max.y) -
            screenPadding - half.y;

        point.x = left <= right
            ? Mathf.Clamp(point.x, left, right)
            : (left + right) * 0.5f;

        point.y = bottom <= top
            ? Mathf.Clamp(point.y, bottom, top)
            : (bottom + top) * 0.5f;

        menu.localPosition =
            new Vector3(point.x, point.y, 0f);

        return true;
    }

    private void OnDisable()
    {
        if (selection != null)
            selection.SelectionChanged -= MarkDirty;

        observer?.Clear();

        handSlot = null;
        observedSlot = null;
        observedFrameSlot = null;
        observedTopSlot = null;

        SetVisible(false);

        if (combineButton != null)
            combineButton.gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        observer?.Dispose();

        if (combineButton != null)
            ItemVisualFactory.Release(combineButton.gameObject);
    }
}

           
