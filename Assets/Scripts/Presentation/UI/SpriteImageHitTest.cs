using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// One geometry/alpha rule for selection, manual hand dragging and camera shielding.
public static class SpriteImageHitTest
{
    public const float DefaultThreshold = .1f;
    private static readonly HashSet<Sprite> warned = new HashSet<Sprite>();

    public static Rect VisibleRect(Image image)
    {
        Rect rect = image.GetPixelAdjustedRect();
        Sprite sprite = image.overrideSprite;
        if (sprite == null || !image.preserveAspect || rect.width <= 0 || rect.height <= 0)
            return rect;
        float ratio = sprite.rect.width / sprite.rect.height;
        float width = Mathf.Min(rect.width, rect.height * ratio);
        float height = width / ratio;
        Vector2 pivot = image.rectTransform.pivot;
        return new Rect(rect.xMin + (rect.width - width) * pivot.x,
            rect.yMin + (rect.height - height) * pivot.y, width, height);
    }

    public static bool TryGetNormalizedPoint(Image image, Vector2 screenPoint, Camera camera, out Vector2 uv)
    {
        uv = default;
        if (image == null || !image.isActiveAndEnabled || image.overrideSprite == null || image.color.a <= 0)
            return false;
        if (image.type != Image.Type.Simple)
        {
            Warn(image.overrideSprite, "Selection supports Simple images; this image uses " + image.type);
            return false;
        }
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(image.rectTransform, screenPoint, camera, out var local))
            return false;
        Rect rect = VisibleRect(image);
        if (rect.width <= 0 || rect.height <= 0) return false;
        uv = new Vector2((local.x - rect.xMin) / rect.width, (local.y - rect.yMin) / rect.height);
        return uv.x >= 0 && uv.x <= 1 && uv.y >= 0 && uv.y <= 1;
    }

    public static bool Contains(Image image, Vector2 screenPoint, Camera camera, float threshold = DefaultThreshold)
    {
        return TryGetNormalizedPoint(image, screenPoint, camera, out var uv) && ContainsNormalized(image, uv, threshold);
    }

    public static bool ContainsNormalized(Image image, Vector2 uv, float threshold = DefaultThreshold)
    {
        if (image == null || image.overrideSprite == null || uv.x < 0 || uv.x > 1 || uv.y < 0 || uv.y > 1)
            return false;
        Sprite sprite = image.overrideSprite;
        float alpha;
        if (!SpriteAlphaMaskDatabase.TrySample(sprite, uv, out alpha))
        {
            // Support procedurally created, readable sprites without persisting them.
            if (sprite.texture == null || !sprite.texture.isReadable || sprite.packed)
            {
                Warn(sprite, "Missing generated alpha mask. Reimport the sprite or rebuild Sprite Alpha Masks.");
                return false;
            }
            Rect rect = sprite.rect;
            int x = Mathf.Clamp(Mathf.FloorToInt(uv.x * rect.width), 0, (int)rect.width - 1);
            int y = Mathf.Clamp(Mathf.FloorToInt(uv.y * rect.height), 0, (int)rect.height - 1);
            alpha = sprite.texture.GetPixel((int)rect.x + x, (int)rect.y + y).a;
        }
        alpha *= image.color.a;
        return alpha > 0 && alpha >= Mathf.Clamp01(threshold);
    }

    private static void Warn(Sprite sprite, string message)
    {
        if (warned.Add(sprite)) Debug.LogWarning(message + " Sprite: " + sprite.name, sprite);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetWarnings() { warned.Clear(); }
}
