using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Selection-independent placement; shares the existing zoom-stable positioning.
[DefaultExecutionOrder(121)]
public sealed class ObservationTopAttachmentActionUI : MonoBehaviour
{
    [SerializeField] private ObservationSelectionController selection;
    [SerializeField] private ObservationItemPresenter presenter;
    [SerializeField] private ObservationActionUI positioner;
    [SerializeField] private RectTransform actionLayer;
    [SerializeField] private Button buttonPrefab;
    private Button placeButton;
    private ItemSlot observedSlot, handSlot, topSlot;
    private ItemSlot expectedObserved, expectedHand, expectedTopSlot;
    private MovableItem expectedValue, expectedHeld, expectedAttachment;
    private ItemData expectedBox;
    private ItemSlotObserver observer;
    private bool dirty = true;

    private ItemSlot LiveObserved => selection != null && selection.Observation != null
        ? selection.Observation.ObservedSlot : null;
    private ItemSlot LiveHand => GameBootstrap.Instance != null && GameBootstrap.Instance.GameWorld != null
        ? GameBootstrap.Instance.GameWorld.Hand.Slot : null;

    private void OnEnable() { dirty = true; }
    private void LateUpdate()
    {
        var target = LiveObserved;
        var hand = LiveHand;
        var top = ObservationSelection.GetExposedBox(target)?.TopSlot;
        if (!ReferenceEquals(target, observedSlot) || !ReferenceEquals(hand, handSlot) || !ReferenceEquals(top, topSlot))
        {
            observedSlot = target; handSlot = hand; topSlot = top;
            if (observer == null) observer = new ItemSlotObserver(_ => dirty = true);
            observer.Clear();
            if (target != null) observer.Observe(target);
            if (hand != null) observer.Observe(hand);
            if (top != null) observer.Observe(top);
            dirty = true;
        }
        Image image = BoxImage(ObservationSelection.GetExposedBox(target));
        bool available = image != null && positioner != null && positioner.isActiveAndEnabled &&
            actionLayer != null && buttonPrefab != null && ObservationActions.CanPlaceTopAttachment(target, hand);
        if (dirty || (available && placeButton == null))
        {
            dirty = false;
            expectedObserved = target; expectedHand = hand; expectedTopSlot = top;
            expectedValue = target?.Value; expectedHeld = hand?.Value; expectedAttachment = top?.Value;
            expectedBox = ObservationSelection.GetExposedBox(target);
            if (available && placeButton == null)
            {
                placeButton = Instantiate(buttonPrefab, actionLayer, false);
                placeButton.name = "PlaceTopAttachmentButton";
                var rect = (RectTransform)placeButton.transform;
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
                rect.sizeDelta = new Vector2(280f, 80f); rect.localScale = Vector3.one;
                var label = placeButton.GetComponentInChildren<TMP_Text>(true);
                if (label != null) { label.text = "배치하기"; label.enableAutoSizing = true; label.fontSizeMin = 16f; label.fontSizeMax = 25f; }
                placeButton.onClick.AddListener(Place);
            }
        }
        if (placeButton == null) return;
        placeButton.gameObject.SetActive(available);
        if (available) positioner.FollowTopAttachment((RectTransform)placeButton.transform, image);
    }
    private Image BoxImage(ItemData box)
    {
        if (box == null || presenter == null || presenter.CurrentView == null) return null;
        foreach (var part in presenter.CurrentView.Parts)
            if (ReferenceEquals(part.Part, box) && part.Image != null && part.Image.gameObject.activeInHierarchy)
                return part.Image;
        return null;
    }
    private void Place()
    {
        var target = LiveObserved; var hand = LiveHand;
        var box = ObservationSelection.GetExposedBox(target);
        bool valid = isActiveAndEnabled && ReferenceEquals(target, expectedObserved) &&
            ReferenceEquals(hand, expectedHand) && ReferenceEquals(target?.Value, expectedValue) &&
            ReferenceEquals(hand?.Value, expectedHeld) && ReferenceEquals(box, expectedBox) &&
            ReferenceEquals(box?.TopSlot, expectedTopSlot) && ReferenceEquals(box?.TopSlot?.Value, expectedAttachment) &&
            BoxImage(box) != null && ObservationActions.CanPlaceTopAttachment(target, hand);
        if (valid && ObservationActions.PlaceTopAttachment(target, hand)) selection.Synchronize();
        dirty = true;
    }
    private void OnDisable()
    {
        observer?.Clear(); observedSlot = handSlot = topSlot = null; dirty = true;
        if (placeButton != null) placeButton.gameObject.SetActive(false);
    }
    private void OnDestroy()
    {
        observer?.Dispose();
        if (placeButton != null) ItemVisualFactory.Release(placeButton.gameObject);
    }
}

