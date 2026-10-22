
using UnityEngine;
using TMPro;

public class ApiaryUI : MonoBehaviour
{
    [SerializeField]
    private TMP_Text apiaryNameText;

    private void Start()
    {
        GameBootstrap bootstrap = GameBootstrap.Instance;

        if (bootstrap == null || bootstrap.GameWorld == null)
        {
            Debug.LogError("[ApiaryUI] GameWorld가 없습니다.");
            return;
        }

        ApiaryData apiary = bootstrap.GameWorld.Apiary;

        apiaryNameText.text = apiary.Name;

        Debug.Log(
            $"[ApiaryUI] 양봉장 데이터 연결: {apiary.Name}"
        );
    }
}
