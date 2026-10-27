using TMPro;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(RectTransform))]
public class UIItemPartView : MonoBehaviour, IItemPartView
{
    [Header("아이템 표시")]
    [SerializeField] private Image itemImage;
    [SerializeField] private TMP_Text itemNameText;

    [Header("결합점 (미지정 시 이미지 경계 사용)")]
    [SerializeField] private RectTransform bottomAnchor;
    [SerializeField] private RectTransform topAnchor;

    public MovableItem Item { get; private set; }
    public ItemData Part => Item as ItemData;
    public int PartIndex { get; private set; } = -1;
    public RectTransform RectTransform => (RectTransform)transform;
    public Transform PartTransform => transform;
    public Image Image
    {
        get
        {
            if (itemImage == null)
                itemImage = GetComponent<Image>() ?? GetComponentInChildren<Image>();
            return itemImage;
        }
    }

    public void Initialize(MovableItem item)
    {
        Item = item;
        if (itemNameText != null)
            itemNameText.text = item != null ? item.Name : "";
        if (Image != null)
            Image.raycastTarget = false;
    }

    public void Bind(ItemData part, int partIndex)
    {
        Initialize(part);
        PartIndex = partIndex;
    }

    // Image.Type.Simple with Preserve Aspect. Preserve the prefab pivot.
    public Rect VisibleRect
    {
        get
        {
            Image image = Image;
            RectTransform rectTransform = image != null ? image.rectTransform : RectTransform;
            Rect rect = rectTransform.rect;
            if (image == null || !image.preserveAspect || image.sprite == null ||
                rect.width <= 0f || rect.height <= 0f)
                return rect;

            float ratio = image.sprite.rect.width / image.sprite.rect.height;
            float width = rect.width;
            float height = rect.height;
            if (ratio > width / height)
                height = width / ratio;
            else
                width = height * ratio;
            return new Rect(rect.xMin + (rect.width - width) * rectTransform.pivot.x,
                rect.yMin + (rect.height - height) * rectTransform.pivot.y, width, height);
        }
    }

    private Vector3 EdgeWorld(bool upper)
    {
        Rect rect = VisibleRect;
        Transform imageTransform = Image != null ? Image.transform : transform;
        return imageTransform.TransformPoint(new Vector3(rect.center.x, upper ? rect.yMax : rect.yMin, 0f));
    }

    public Vector3 BottomWorld => bottomAnchor != null ? bottomAnchor.position : EdgeWorld(false);
    public Vector3 TopWorld => topAnchor != null ? topAnchor.position : EdgeWorld(true);

    public Bounds GetBoundsIn(Transform space)
    {
        Rect rect = VisibleRect;
        Transform imageTransform = Image != null ? Image.transform : transform;
        return ItemAssemblyLayout.TransformBounds(
            new Bounds(new Vector3(rect.center.x, rect.center.y, 0f),
                new Vector3(rect.width, rect.height, 0f)), imageTransform, space);
    }
}
