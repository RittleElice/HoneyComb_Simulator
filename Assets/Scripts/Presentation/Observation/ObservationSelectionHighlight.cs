
using System;
using UnityEngine;
using UnityEngine.UI;

[DefaultExecutionOrder(110)]
public class ObservationSelectionHighlight : MonoBehaviour
{
    [SerializeField]
    private ObservationSelectionController selection;

    [Header("Equipment Highlight")]
    [SerializeField]
    private Color color = new Color(1f, 0.8f, 0.1f, 1f);

    [SerializeField]
    private Vector2 width = new Vector2(4f, 4f);

    [Header("Frame Slot Highlight")]
    [SerializeField]
    private Color frameFillColor =
        new Color(1f, 0.8f, 0.1f, 0.25f);

    [SerializeField]
    private Color frameOutlineColor = Color.yellow;

    private Image target;
    private Outline outline;

    private ObservationFrameSlotGraphic frameGraphic;
    private ObservationFrameSlotLayout currentLayout;
    private int currentFrameIndex = -1;

    private void LateUpdate()
    {
        if (selection == null)
        {
            Clear();
            return;
        }

        var state = selection.Selection;

        if (state.Kind == ObservationSelectionKind.FrameSlot)
        {
            UpdateFrameHighlight();
            return;
        }

        ClearFrameHighlight();

        Image next = selection.SelectedImage;

        if (next == target && outline != null)
            return;

        ClearEquipmentHighlight();

        target = next;

        if (target == null)
            return;

        outline = target.gameObject.AddComponent<Outline>();
        outline.effectColor = color;
        outline.effectDistance = width;
        outline.useGraphicAlpha = true;
    }

    private void UpdateFrameHighlight()
    {
        ClearEquipmentHighlight();

        var state = selection.Selection;

        ItemAssemblyView view =
            selection.Observation != null
                ? FindCurrentView()
                : null;

        if (view == null)
        {
            ClearFrameHighlight();
            return;
        }

        ObservationFrameSlotLayout layout = null;

        foreach (var part in view.Parts)
        {
            if (ReferenceEquals(part.Part, state.Part))
            {
                layout =
                    part.GetComponent<ObservationFrameSlotLayout>();
                break;
            }
        }

        if (layout == null)
        {
            ClearFrameHighlight();
            return;
        }

        if (frameGraphic != null &&
            currentLayout == layout &&
            currentFrameIndex == state.FrameSlotIndex)
            return;

        ClearFrameHighlight();

        GameObject obj = new GameObject(
            "FrameSlotHighlight",
            typeof(RectTransform));

        obj.transform.SetParent(layout.transform, false);

        RectTransform rect =
            obj.GetComponent<RectTransform>();

        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.pivot = layout.GetComponent<RectTransform>().pivot;

        frameGraphic =
            obj.AddComponent<ObservationFrameSlotGraphic>();

        frameGraphic.color = frameFillColor;
        frameGraphic.raycastTarget = false;

        Vector2[] normalized =
            layout.GetSlotPolygon(state.FrameSlotIndex);

        Rect parentRect =
            layout.GetComponent<RectTransform>().rect;

        Vector2[] local = new Vector2[normalized.Length];

        for (int i = 0; i < normalized.Length; i++)
        {
            local[i] = new Vector2(
                parentRect.xMin +
                    normalized[i].x * parentRect.width,
                parentRect.yMin +
                    normalized[i].y * parentRect.height);
        }

        frameGraphic.SetPolygon(local);

        currentLayout = layout;
        currentFrameIndex = state.FrameSlotIndex;
    }

    private ItemAssemblyView FindCurrentView()
    {
        ObservationItemPresenter presenter =
            FindAnyObjectByType<ObservationItemPresenter>();

        return presenter != null
            ? presenter.CurrentView
            : null;
    }

    private void ClearEquipmentHighlight()
    {
        if (outline != null)
        {
            outline.enabled = false;

            if (Application.isPlaying)
                Destroy(outline);
            else
                DestroyImmediate(outline);
        }

        outline = null;
        target = null;
    }

    private void ClearFrameHighlight()
    {
        if (frameGraphic != null)
        {
            frameGraphic.gameObject.SetActive(false);

            if (Application.isPlaying)
                Destroy(frameGraphic.gameObject);
            else
                DestroyImmediate(frameGraphic.gameObject);
        }

        frameGraphic = null;
        currentLayout = null;
        currentFrameIndex = -1;
    }

    private void Clear()
    {
        ClearEquipmentHighlight();
        ClearFrameHighlight();
    }

    private void OnDisable()
    {
        Clear();
    }
}
