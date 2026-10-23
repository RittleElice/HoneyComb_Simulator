using TMPro;
using UnityEngine;

public class ApiaryUI : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private TMP_Text apiaryNameText;
    [SerializeField] private TMP_Text hiveCountText;

    [Header("벌통 표시")]
    [SerializeField] private HiveView hivePrefab;
    [SerializeField] private ApiaryGridView gridView;

    private void Start()
    {
        ApiaryData apiary =
            GameBootstrap.Instance.GameWorld.Apiary;

        apiaryNameText.text = apiary.Name;
        hiveCountText.text = $"벌통 수: {apiary.Hives.Count}";

        foreach (HiveData hive in apiary.Hives)
        {
            HiveView view = Instantiate(hivePrefab);

            view.Initialize(
                hive,
                apiary.Grid.Width,
                gridView.CellSize
            );
        }
    }
}