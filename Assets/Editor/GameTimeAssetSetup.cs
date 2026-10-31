using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
public static class GameTimeAssetSetup
{
    [MenuItem("HoneyComb/Developer/Create Missing Time UI Assets")]
    public static void CreateMissing()
    {
        if (AssetDatabase.LoadAssetAtPath<GameTimeUIProfile>("Assets/Resources/GameTimeUIProfile.asset") == null)
            AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<GameTimeUIProfile>(), "Assets/Resources/GameTimeUIProfile.asset");
        if (AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/GameTimeUI.prefab") == null)
        {
            var root = new GameObject("GameTimeUI", typeof(RectTransform), typeof(Image), typeof(GameTimeUI));
            root.GetComponent<Image>().color = new Color(.07f,.09f,.08f,.88f); root.GetComponent<Image>().raycastTarget = false;
            var text = new GameObject("DateTime", typeof(RectTransform), typeof(TextMeshProUGUI)); text.transform.SetParent(root.transform, false);
            var label = text.GetComponent<TextMeshProUGUI>(); label.font = TMP_Settings.defaultFontAsset;
            label.text = "1년차 01월 01일 00:00"; label.fontSize = 25; label.alignment = TextAlignmentOptions.Center; label.raycastTarget = false;
            label.rectTransform.anchorMin = Vector2.zero; label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = new Vector2(10,4); label.rectTransform.offsetMax = new Vector2(-10,-4);
            root.GetComponent<GameTimeUI>().label = label;
            root.GetComponent<GameTimeUI>().Place(new GameTimeUIProfile.Placement());
            PrefabUtility.SaveAsPrefabAsset(root,"Assets/Resources/GameTimeUI.prefab"); Object.DestroyImmediate(root);
        }
        AssetDatabase.SaveAssets();
    }
}
