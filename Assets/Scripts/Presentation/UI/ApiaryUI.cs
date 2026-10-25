
using TMPro;
using UnityEngine;

public class ApiaryUI : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private TMP_Text apiaryNameText;
    [SerializeField] private TMP_Text hiveCountText;

    private ApiaryData apiary;
    private int previousHiveCount = -1;

    private void Start()
    {
        GameBootstrap bootstrap = GameBootstrap.Instance;

        if (bootstrap == null || bootstrap.GameWorld == null)
        {
            Debug.LogError(
                "[ApiaryUI] GameWorld가 없습니다.");
            enabled = false;
            return;
        }

        apiary = bootstrap.GameWorld.Apiary;

        if (apiaryNameText != null)
            apiaryNameText.text = apiary.Name;

        RefreshHiveCount();
    }

    private void Update()
    {
        if (apiary == null)
            return;

        RefreshHiveCount();
    }

    private void RefreshHiveCount()
    {
        int count = CountPlacedHives();

        if (count == previousHiveCount)
            return;

        previousHiveCount = count;

        if (hiveCountText != null)
            hiveCountText.text = $"벌통 수: {count}";
    }

    private int CountPlacedHives()
    {
        ApiaryGrid grid = apiary.Grid;
        int count = 0;

        for (int x = 0; x < grid.Width; x++)
        {
            for (int y = 0; y < grid.Height; y++)
            {
                ItemSlot slot = grid.GetItemSlot(x, y);

                if (slot == null || slot.IsEmpty)
                    continue;

                if (slot.Value is ItemData item)
                {
                    if (item.Kind == ItemKind.BroodBox)
                        count++;
                }
                else if (slot.Value is ItemAssembly assembly)
                {
                    foreach (ItemData part in assembly.Parts)
                    {
                        if (part.Kind == ItemKind.BroodBox)
                            count++;
                    }
                }
            }
        }

        return count;
    }
}
