using TMPro;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(RectTransform))]
public class UIItemPartView : MonoBehaviour
{
    [Header("아이템 표시")]
    [SerializeField] private Image itemImage;
    [SerializeField] private TMP_Text itemNameText;

    public MovableItem Item { get; private set; }

    public void Initialize(MovableItem item)
    {
        Item = item;

        if (itemNameText != null)
            itemNameText.text = item != null ? item.Name : "";

        if (itemImage != null)
            itemImage.raycastTarget = false;
    }
}