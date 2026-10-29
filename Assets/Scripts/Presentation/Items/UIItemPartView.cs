
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

    public RectTransform RectTransform =>
        (RectTransform)transform;

    public Transform PartTransform => transform;

    public Image Image
    {
        get
        {
            if (itemImage == null)
            {
                itemImage = GetComponent<Image>() ??
                            GetComponentInChildren<Image>();
            }

            return itemImage;
        }
    }

    public void Initialize(MovableItem item)
    {
        Item = item;

        if (itemNameText != null)
        {
            itemNameText.text =
                item != null ? item.Name : "";
        }

        if (Image != null)
        {
            Image.raycastTarget = false;
        }
    }

    public void Bind(ItemData part, int partIndex)
    {
        Initialize(part);

        PartIndex = partIndex;

        // 공통 데이터 바인딩
        // 이 장비에 속한 표시 컴포넌트에 ItemData 전달
        MonoBehaviour[] components =
            GetComponentsInChildren<MonoBehaviour>(true);

        foreach (MonoBehaviour component in components)
        {
            if (component == null)
                continue;

            // 다른 장비의 UIItemPartView가 중첩되어 있다면
            // 해당 장비의 컴포넌트에는 전달하지 않음
            UIItemPartView owner =
                component.GetComponentInParent<UIItemPartView>();

            if (owner != this)
                continue;

            if (component is IItemDataBound receiver)
            {
                receiver.BindItem(part);
            }
        }
    }

    // Image.Type.Simple + Preserve Aspect.
    // 프리팹 Pivot을 유지한다.
    public Rect VisibleRect
    {
        get
        {
            Image image = Image;

            RectTransform rectTransform =
                image != null
                    ? image.rectTransform
                    : RectTransform;

            Rect rect = rectTransform.rect;

            if (image == null ||
                !image.preserveAspect ||
                image.sprite == null ||
                rect.width <= 0f ||
                rect.height <= 0f)
            {
                return rect;
            }

            float ratio =
                image.sprite.rect.width /
                image.sprite.rect.height;

            float width = rect.width;
            float height = rect.height;

            if (ratio > width / height)
            {
                height = width / ratio;
            }
            else
            {
                width = height * ratio;
            }

            return new Rect(
                rect.xMin +
                    (rect.width - width) *
                    rectTransform.pivot.x,
                rect.yMin +
                    (rect.height - height) *
                    rectTransform.pivot.y,
                width,
                height);
        }
    }

    private Vector3 EdgeWorld(bool upper)
    {
        Rect rect = VisibleRect;

        Transform imageTransform =
            Image != null
                ? Image.transform
                : transform;

        return imageTransform.TransformPoint(
            new Vector3(
                rect.center.x,
                upper ? rect.yMax : rect.yMin,
                0f));
    }

    public Vector3 BottomWorld =>
        bottomAnchor != null
            ? bottomAnchor.position
            : EdgeWorld(false);

    public Vector3 TopWorld =>
        topAnchor != null
            ? topAnchor.position
            : EdgeWorld(true);

    public Bounds GetBoundsIn(Transform space)
    {
        Rect rect = VisibleRect;

        Transform imageTransform =
            Image != null
                ? Image.transform
                : transform;

        return ItemAssemblyLayout.TransformBounds(
            new Bounds(
                new Vector3(
                    rect.center.x,
                    rect.center.y,
                    0f),
                new Vector3(
                    rect.width,
                    rect.height,
                    0f)),
            imageTransform,
            space);
    }
}
