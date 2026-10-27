using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
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

    private enum DisplayState
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

    private DisplayState state = DisplayState.Lowered;

    private float currentTopY;
    private float currentScale;

    private bool isFrame;
    private bool isDragging;

    private Vector2 dragStartLocalPosition;
    private float dragStartProgress;


    private string lastSceneName;

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

        lastSceneName = SceneManager.GetActiveScene().name;

        Refresh();
    }

    private void Update()
    {
        // A new game may replace the source while SharedUI remains loaded.
        GameBootstrap bootstrap = GameBootstrap.Instance;
        HandData source = bootstrap != null && bootstrap.GameWorld != null
            ? bootstrap.GameWorld.Hand
            : null;

        if (!ReferenceEquals(hand, source))
            Refresh();

        string sceneName = SceneManager.GetActiveScene().name;

        if (sceneName != lastSceneName)
        {
            lastSceneName = sceneName;

            if (sceneName != "Observation")
            {
                isDragging = false;

                if (presentationRoot != null &&
                    state == DisplayState.Inspecting)
                {
                    AnimateTo(DisplayState.Lowered);
                }
            }
        }

        HandleDrag();
    }

    private void BindHand()
    {
        GameBootstrap bootstrap = GameBootstrap.Instance;

        if (bootstrap == null ||
            bootstrap.GameWorld == null ||
            bootstrap.GameWorld.Hand == null)
        {
            UnbindHand();
            return;
        }

        HandData currentHand = bootstrap.GameWorld.Hand;
        ItemSlot currentSlot = currentHand.Slot;

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
            slotObserver = new ItemSlotObserver(OnHandChanged);

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
            return;

        ClearVisual();

        cachedItem = hand.Value;

        isFrame =
            hand.Value is ItemData item &&
            item.Kind == ItemKind.Frame;

        CreateVisual();

        if (presentationRoot == null)
            return;

        // 모든 장비는 Pickup 위치에서 시작
        ApplyState(DisplayState.Pickup);

        // 잠시 정지한 뒤 자동으로 Lowered 이동
        animation = StartCoroutine(PickupRoutine());
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

        root.transform.SetParent(itemContainer, false);

        presentationRoot =
            root.GetComponent<RectTransform>();

        presentationRoot.anchorMin =
            new Vector2(0.5f, 0.5f);

        presentationRoot.anchorMax =
            new Vector2(0.5f, 0.5f);

        presentationRoot.pivot =
            new Vector2(0.5f, 0.5f);

        presentationRoot.anchoredPosition = Vector2.zero;
        presentationRoot.localScale = Vector3.one;

        GameObject visual = new GameObject(
            "HandItemVisual",
            typeof(RectTransform));

        visual.transform.SetParent(
            presentationRoot, false);

        visualRect = visual.GetComponent<RectTransform>();

        currentView =
            visual.AddComponent<ItemAssemblyView>();

        currentView.Initialize(
            hand.Value,
            prefabRegistry,
            ItemViewContext.Hand,
            itemPrefab,
            displayWidth,
            displayHeight,
            AssemblyAlignment.Bottom);

        visualRect.anchorMin =
            new Vector2(0.5f, 0.5f);

        visualRect.anchorMax =
            new Vector2(0.5f, 0.5f);

        visualRect.anchoredPosition = Vector2.zero;
    }

    private IEnumerator PickupRoutine()
    {
        if (raisedHoldDuration > 0f)
        {
            yield return new WaitForSecondsRealtime(
                raisedHoldDuration);
        }

        animation = null;

        AnimateTo(DisplayState.Lowered);
    }

    private void GetTarget(
        DisplayState target,
        out float topY,
        out float scale)
    {
        scale = target == DisplayState.Inspecting
            ? inspectionScale
            : displayScale;

        if (target == DisplayState.Pickup)
        {
            // Pickup은 이미지 중앙 기준
            float height = GetVisualHeight();

            topY =
                raisedHeight + height * scale * 0.5f;
        }
        else if (target == DisplayState.Inspecting)
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

        // Bottom 정렬의 실제 이미지 최상단 기준
        presentationRoot.anchoredPosition =
            new Vector2(
                0f,
                topY - height * scale);
    }

    private void ApplyState(DisplayState target)
    {
        GetTarget(
            target,
            out float topY,
            out float scale);

        ApplyPresentation(topY, scale);
        state = target;
    }

    private void AnimateTo(DisplayState target)
    {
        StopAnimation();

        animation = StartCoroutine(
            AnimateRoutine(target));
    }

    private IEnumerator AnimateRoutine(
        DisplayState target)
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

    private void HandleDrag()
    {
        if (!isFrame ||
            currentView == null ||
            presentationRoot == null ||
            itemContainer == null ||
            SceneManager.GetActiveScene().name != "Observation")
        {
            ResetDrag();
            return;
        }

        // Pickup 및 자동 하강 중에는 드래그 불가
        if (state == DisplayState.Pickup ||
            (animation != null && !isDragging))
            return;

        if (!TryGetPointer(
                out Vector2 screenPosition,
                out bool pressed,
                out bool down,
                out bool up))
            return;

        if (!RectTransformUtility
            .ScreenPointToLocalPointInRectangle(
                itemContainer,
                screenPosition,
                GetUICamera(),
                out Vector2 localPosition))
            return;

        // 실제 이미지 영역에서 드래그 시작
        if (down && !isDragging)
        {
            if (!IsPointerOnHand(screenPosition))
                return;

            StopAnimation();

            isDragging = true;
            dragStartLocalPosition = localPosition;

            float range =
                inspectionHeight - loweredHeight;

            dragStartProgress =
                Mathf.Abs(range) < 0.001f
                    ? 0f
                    : Mathf.Clamp01(
                        (currentTopY - loweredHeight) / range);
        }

        // 드래그 중: 위치와 배율 함께 보간
        if (isDragging && pressed)
        {
            float range =
                inspectionHeight - loweredHeight;

            if (Mathf.Abs(range) < 0.001f)
                return;

            float deltaY =
                localPosition.y - dragStartLocalPosition.y;

            float progress = Mathf.Clamp01(
                dragStartProgress + deltaY / range);

            ApplyDragProgress(progress);
        }

        // 손가락을 떼면 가까운 끝으로 이동
        if (isDragging && (up || !pressed))
        {
            float progress = GetCurrentProgress();

            ResetDrag();

            AnimateTo(
                progress >= 0.5f
                    ? DisplayState.Inspecting
                    : DisplayState.Lowered);
        }
    }

    private void ApplyDragProgress(float progress)
    {
        progress = Mathf.Clamp01(progress);

        float topY = Mathf.Lerp(
            loweredHeight,
            inspectionHeight,
            progress);

        float scale = Mathf.Lerp(
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
            (currentTopY - loweredHeight) / range);
    }

    // 실제 Image 컴포넌트의 사각형으로 판정
    private bool IsPointerOnHand(Vector2 screenPosition)
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

            if (RectTransformUtility
                .RectangleContainsScreenPoint(
                    image.rectTransform,
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

    private bool TryGetPointer(
        out Vector2 position,
        out bool pressed,
        out bool down,
        out bool up)
    {
        position = Vector2.zero;
        pressed = false;
        down = false;
        up = false;

        Touchscreen touchscreen = Touchscreen.current;

        if (touchscreen != null)
        {
            var touch = touchscreen.primaryTouch;

            if (touch.press.isPressed ||
                touch.press.wasPressedThisFrame ||
                touch.press.wasReleasedThisFrame)
            {
                position = touch.position.ReadValue();
                pressed = touch.press.isPressed;
                down = touch.press.wasPressedThisFrame;
                up = touch.press.wasReleasedThisFrame;

                return true;
            }
        }

        Mouse mouse = Mouse.current;

        if (mouse == null)
            return false;

        position = mouse.position.ReadValue();
        pressed = mouse.leftButton.isPressed;
        down = mouse.leftButton.wasPressedThisFrame;
        up = mouse.leftButton.wasReleasedThisFrame;

        return true;
    }

    private void ResetDrag()
    {
        isDragging = false;
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
        StopAnimation();
        ResetDrag();

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
        state = DisplayState.Lowered;
    }

    private void OnDestroy()
    {
        UnbindHand();
        slotObserver?.Dispose();
        ClearVisual();
    }
}
