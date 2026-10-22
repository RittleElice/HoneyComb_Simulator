using UnityEngine;

public class GameBootstrap : MonoBehaviour
{
    private void Awake()
    {
        InitializeGame();
    }

    private void InitializeGame()
    {
        Debug.Log("[GameBootstrap] 게임 초기화 시작");

        // 추후 게임 시스템 초기화
        // GameWorld
        // GameFlowManager
        // GameTimeManager
        // SaveSystem

        Debug.Log("[GameBootstrap] 게임 초기화 완료");
    }
}