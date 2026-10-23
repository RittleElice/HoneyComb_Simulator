using System.Collections.Generic;
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

    private readonly Dictionary<HiveData, HiveView> hiveViews =
        new Dictionary<HiveData, HiveView>();

    private void Start()
    {
        ApiaryData apiary = GameBootstrap.Instance.GameWorld.Apiary;
        apiaryNameText.text = apiary.Name;
        hiveCountText.text = $"벌통 수: {apiary.Hives.Count}";
        EnsureHiveViews();
    }

    // Start 호출 순서와 무관하게 기존 객체를 한 번만 생성한다.
    public void EnsureHiveViews()
    {
        ApiaryItemView itemView = FindAnyObjectByType<ApiaryItemView>();
        ApiaryData apiary = GameBootstrap.Instance.GameWorld.Apiary;
        foreach (HiveData hive in apiary.Hives)
        {
            if (hiveViews.TryGetValue(hive, out HiveView existing) &&
                existing != null)
                continue;

            HiveView view = Instantiate(hivePrefab);
            view.Initialize(hive, apiary.Grid.Width, gridView.CellSize);

            // 최초 생성 시에도 새 장비와 동일한 표시 순서를 즉시 적용한다.
            // 이 함수는 ApiaryItemView.Start 실행 전에도 호출할 수 있다.
            if (itemView != null)
                itemView.ApplyGridSorting(
                    view.GetComponent<SpriteRenderer>(), hive.Y);
            hiveViews[hive] = view;
        }
    }

    public bool TryGetHiveView(HiveData data, out HiveView view)
    {
        return hiveViews.TryGetValue(data, out view) && view != null;
    }

    public void HideHiveViews()
    {
        foreach (HiveView view in hiveViews.Values)
        {
            if (view != null)
                view.gameObject.SetActive(false);
        }
    }
}