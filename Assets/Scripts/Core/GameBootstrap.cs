using UnityEngine;

public class GameBootstrap : MonoBehaviour
{
    private GameWorld gameWorld;

    private void Awake()
    {
        InitializeGame();
    }

    private void InitializeGame()
    {
        Debug.Log("[GameBootstrap] 게임 초기화 시작");

        gameWorld = new GameWorld();
        
        Debug.Log(
            $"[GameWorld] 양봉장: {gameWorld.Apiary.Name}"
        );


        Debug.Log(
            $"[GameWorld] 시작 날짜: " +
            $"{gameWorld.Year}년 " +
            $"{gameWorld.Month}월 " +
            $"{gameWorld.Day}일 " +
            $"{gameWorld.Hour:D2}:" +
            $"{gameWorld.Minute:D2}"
        );

        Debug.Log("[GameBootstrap] 게임 초기화 완료");
    }
}