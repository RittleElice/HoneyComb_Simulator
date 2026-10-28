
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class HandItemPresenter : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TMP_Text handText;
    [SerializeField] private RectTransform itemContainer;
    [SerializeField] private ItemPrefabRegistry prefabRegistry;
    [SerializeField] private UIItemPartView itemPrefab;

    [Header("기본 배율")]
    [SerializeField, Min(0.01f)]
    private float displayScale = 1f;

    [Header("높이 - Canvas 중앙 기준 Y")]
    [SerializeField] private float raisedHeight = 300f;
    [SerializeField] private float loweredHeight = -350f;
    [SerializeField] private float inspectionHeight = 500f;

    [Header("애니메이션")]
    [SerializeField, Min(0f)]
    private float raisedHoldDuration = 0.5f;

    [SerializeField, Min(0f)]
    private float animationDuration = 0.3f;

    [Header("관찰 배율")]
    [SerializeField, Min(0.01f)]
    private float inspectionScale = 1.5f;

    [Header("조립체 표시 크기")]
    [SerializeField, Min(1f)]
    private float displayWidth = 800f;

    [SerializeField, Min(1f)]
    private float displayHeight = 1200f;

    public enum HandDisplayState
    {
        Pickup,
        Lowered,
        Inspecting
    }

    private HandData hand;
    private ItemSlot observedSlot;
    private ItemSlotObserver slotObserver;

    private bool started;
    private MovableItem cachedItem;

    private RectTransform presentationRoot;
    private RectTransform visualRect;
    private ItemAssemblyView currentView;

    private Canvas rootCanvas;
    private Coroutine animation;

    private HandDisplayState state = HandDisplayState.Lowered;

    private float currentTopY;
    private float currentScale;

    private bool isFrame;
    private string lastSceneName;

    public bool IsInspecting =>
        state == HandDisplayState.Inspecting &&
        animation == null &&
        !IsInspectionInputCaptured;

    public HandDisplayState State => state;
    public bool IsTransitioning => animation != null;
    public bool IsAtInspectionHeight => state == HandDisplayState.Inspecting && !IsTransitioning;
    public float HeightRange => inspectionHeight - loweredHeight;
    public float HeightProgress => GetCurrentProgress();
    public event System.Action VisualInvalidated;

    public void AnimateToLowered() => AnimateTo(HandDisplayState.Lowered);
    public void AnimateToInspecting() => AnimateTo(HandDisplayState.Inspecting);
    public void SetHeightProgress(float progress)
    {
        StopAnimation();
        ApplyDragProgress(progress);
    }
    public bool TryScreenToLocal(Vector2 screen, out Vector2 local)
    {
        local = default;
        return itemContainer != null && RectTransformUtility.ScreenPointToLocalPointInRectangle(
            itemContainer, screen, GetUICamera(), out local);
    }

    public bool IsFrameHeld =>
        isFrame && cachedItem != null;

    public MovableItem CurrentItem => cachedItem;

    public RectTransform InspectionVisual => visualRect;

    public bool IsInspectionInputCaptured { get; private set; }

    public void SetInspectionInputCaptured(bool captured)
    {
        IsInspectionInputCaptured = captured;
    }

    private void OnEnable()
    {
        if (started)
            Refresh();
    }

    private void OnDisable()
    {
        UnbindHand();
        ClearVisual();
    }

    private void Start()
    {
        started = true;

        if (itemContainer != null)
        {
            rootCanvas =
                itemContainer.GetComponentInParent<Canvas>();

            if (rootCanvas != null)
                rootCanvas = rootCanvas.rootCanvas;
        }

        lastSceneName =
            SceneManager.GetActiveScene().name;

        Refresh();
    }

    private void Update()
    {
        GameBootstrap bootstrap = GameBootstrap.Instance;

        HandData source =
            bootstrap != null &&
            bootstrap.GameWorld != null
                ? bootstrap.GameWorld.Hand
                : null;

        if (!ReferenceEquals(hand, source))
            Refresh();

        string sceneName =
            SceneManager.GetActiveScene().name;

        if (sceneName != lastSceneName)
        {
            lastSceneName = sceneName;

            if (sceneName != "Observation")
            {
                VisualInvalidated?.Invoke();
                IsInspectionInputCaptured = false;

                if (presentationRoot != null &&
                    state == HandDisplayState.Inspecting)
                {
                    AnimateTo(HandDisplayState.Lowered);
                }
            }
        }

    }

    private void BindHand()
    {
        GameBootstrap bootstrap =
            GameBootstrap.Instance;

        if (bootstrap == null ||
            bootstrap.GameWorld == null ||
            bootstrap.GameWorld.Hand == null)
        {
            UnbindHand();
            return;
        }

        HandData currentHand =
            bootstrap.GameWorld.Hand;

        ItemSlot currentSlot =
            currentHand.Slot;

        if (currentSlot == null)
            return;

        if (ReferenceEquals(observedSlot, currentSlot))
        {
            hand = currentHand;
            return;
        }

        UnbindHand();

        hand = currentHand;
        observedSlot = currentSlot;

        if (slotObserver == null)
        {
            slotObserver =
                new ItemSlotObserver(OnHandChanged);
        }

        slotObserver.Observe(observedSlot);

        if (prefabRegistry == null)
        {
            prefabRegistry =
                FindAnyObjectByType<ItemPrefabRegistry>();
        }
    }

    private void UnbindHand()
    {
        slotObserver?.Clear();

        observedSlot = null;
        hand = null;
    }

    private void OnHandChanged(ItemSlot slot)
    {
        Refresh();
    }

    public void Refresh()
    {
        if (!isActiveAndEnabled)
            return;

        BindHand();

        if (hand == null || hand.IsEmpty)
        {
            if (handText != null)
                handText.text = "손: 빈 칸";

            ClearVisual();
            return;
        }

        if (handText != null)
            handText.text = "손:";

        if (ReferenceEquals(cachedItem, hand.Value) &&
            currentView != null &&
            currentView.gameObject.activeInHierarchy)
        {
            return;
        }

        ClearVisual();

        cachedItem = hand.Value;

        isFrame =
            hand.Value is ItemData item &&
            item.Kind == ItemKind.Frame;

        CreateVisual();

        if (presentationRoot == null)
            return;

        ApplyState(HandDisplayState.Pickup);

        animation =
            StartCoroutine(PickupRoutine());
    }

    private void CreateVisual()
    {
        if (itemContainer == null)
        {
            Debug.LogWarning(
                "[HandItemPresenter] ItemContainer가 없습니다.");
            return;
        }

        GameObject root = new GameObject(
            "HandPresentationRoot",
            typeof(RectTransform));

        root.transform.SetParent(
            itemContainer,
            false);

        presentationRoot =
            root.GetComponent<RectTransform>();

        presentationRoot.anchorMin =
            new Vector2(0.5f, 0.5f);

        presentationRoot.anchorMax =
            new Vector2(0.5f, 0.5f);

        presentationRoot.pivot =
            new Vector2(0.5f, 0.5f);

        presentationRoot.anchoredPosition =
            Vector2.zero;

        presentationRoot.localScale =
            Vector3.one;

        GameObject visual = new GameObject(
            "HandItemVisual",
            typeof(RectTransform));

        visual.transform.SetParent(
            presentationRoot,
            false);

        visualRect =
            visual.GetComponent<RectTransform>();

        currentView =
            visual.AddComponent<ItemAssemblyView>();

        currentView.Initialize(
            hand.Value,
            prefabRegistry,
            ItemViewContext.Hand,
            itemPrefab,
            displayWidth,
            displayHeight,
            AssemblyAlignment.Bottom,
            AssemblyScaleMode.Fixed);

        visualRect.anchorMin =
            new Vector2(0.5f, 0.5f);

        visualRect.anchorMax =
            new Vector2(0.5f, 0.5f);

        visualRect.anchoredPosition =
            Vector2.zero;
    }

    private IEnumerator PickupRoutine()
    {
        if (raisedHoldDuration > 0f)
        {
            yield return new WaitForSecondsRealtime(
                raisedHoldDuration);
        }

        else
        {
            yield return null;
        }

        animation = null;
        AnimateTo(HandDisplayState.Lowered);
    }

    private void GetTarget(
        HandDisplayState target,
        out float topY,
        out float scale)
    {
        scale =
            target == HandDisplayState.Inspecting
                ? inspectionScale
                : displayScale;

        if (target == HandDisplayState.Pickup)
        {
            float height = GetVisualHeight();

            topY =
                raisedHeight +
                height * scale * 0.5f;
        }
        else if (target == HandDisplayState.Inspecting)
        {
            topY = inspectionHeight;
        }
        else
        {
            topY = loweredHeight;
        }
    }

    private float GetVisualHeight()
    {
        if (visualRect == null)
            return 0f;

        return visualRect.rect.height *
               visualRect.localScale.y;
    }

    private void ApplyPresentation(
        float topY,
        float scale)
    {
        if (presentationRoot == null ||
            visualRect == null)
            return;

        float height = GetVisualHeight();

        currentTopY = topY;
        currentScale = scale;

        presentationRoot.localScale =
            Vector3.one * scale;

        presentationRoot.anchoredPosition =
            new Vector2(
                0f,
                topY - height * scale);
    }

    private void ApplyState(HandDisplayState target)
    {
        GetTarget(
            target,
            out float topY,
            out float scale);

        ApplyPresentation(topY, scale);

        state = target;
    }

    private void AnimateTo(HandDisplayState target)
    {
        StopAnimation();

        if (presentationRoot == null || !isActiveAndEnabled) return;
        if (animationDuration <= 0f) { ApplyState(target); return; }
        animation = StartCoroutine(AnimateRoutine(target));
    }

    private IEnumerator AnimateRoutine(
        HandDisplayState target)
    {
        float startY = currentTopY;
        float startScale = currentScale;

        GetTarget(
            target,
            out float endY,
            out float endScale);

        if (animationDuration <= 0f)
        {
            ApplyPresentation(endY, endScale);

            state = target;
            animation = null;
            yield break;
        }

        float elapsed = 0f;

        while (elapsed < animationDuration)
        {
            elapsed += Time.unscaledDeltaTime;

            float t = Mathf.Clamp01(
                elapsed / animationDuration);

            t = t * t * (3f - 2f * t);

            ApplyPresentation(
                Mathf.Lerp(startY, endY, t),
                Mathf.Lerp(startScale, endScale, t));

            yield return null;
        }

        ApplyPresentation(endY, endScale);

        state = target;
        animation = null;
    }

    private void ApplyDragProgress(float progress)
    {
        progress = Mathf.Clamp01(progress);

        float topY =
            Mathf.Lerp(
                loweredHeight,
                inspectionHeight,
                progress);

        float scale =
            Mathf.Lerp(
                displayScale,
                inspectionScale,
                progress);

        ApplyPresentation(topY, scale);
    }

    private float GetCurrentProgress()
    {
        float range =
            inspectionHeight - loweredHeight;

        if (Mathf.Abs(range) < 0.001f)
            return 0f;

        return Mathf.Clamp01(
            (currentTopY - loweredHeight) /
            range);
    }

    public bool BlocksObservationInput(Vector2 position)
    {
        return isActiveAndEnabled &&
               (GetComponent<HandGestureController>()?.BlocksObservationInput == true ||
                IsInspectionInputCaptured ||
                IsPointerOnHand(position));
    }

    public bool IsPointerOnHand(
        Vector2 screenPosition)
    {
        if (currentView == null)
            return false;

        Camera uiCamera = GetUICamera();

        Image[] images =
            currentView.GetComponentsInChildren<Image>(true);

        foreach (Image image in images)
        {
            if (image == null ||
                !image.isActiveAndEnabled ||
                image.sprite == null)
                continue;

            if (SpriteImageHitTest.Contains(
                    image,
                    screenPosition,
                    uiCamera))
            {
                return true;
            }
        }

        return false;
    }

    private Camera GetUICamera()
    {
        if (rootCanvas == null ||
            rootCanvas.renderMode ==
            RenderMode.ScreenSpaceOverlay)
            return null;

        return rootCanvas.worldCamera;
    }

    private void StopAnimation()
    {
        if (animation == null)
            return;

        StopCoroutine(animation);
        animation = null;
    }

    private void ClearVisual()
    {
        VisualInvalidated?.Invoke();
        StopAnimation();

        IsInspectionInputCaptured = false;

        if (presentationRoot != null)
        {
            presentationRoot.gameObject.SetActive(false);
            Destroy(presentationRoot.gameObject);
        }

        presentationRoot = null;
        visualRect = null;
        currentView = null;
        cachedItem = null;

        isFrame = false;
        state = HandDisplayState.Lowered;
    }

    private void OnDestroy()
    {
        UnbindHand();
        slotObserver?.Dispose();
        ClearVisual();
    }
}
