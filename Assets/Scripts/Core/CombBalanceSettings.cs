using UnityEngine;

[CreateAssetMenu(menuName = "HoneyComb/Comb Balance Settings")]
public sealed class CombBalanceSettings : ScriptableObject
{
    [SerializeField] private CombBalanceParameters values = new CombBalanceParameters();
    public bool TryApply(out string error) => CombBalance.TryApply(values, out error);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => CombBalance.TryApply(new CombBalanceParameters(), out _);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    public static void ApplyDefaultAsset()
    {
        var asset = Resources.Load<CombBalanceSettings>("CombBalanceSettings");
        if (asset == null) { Debug.LogWarning("CombBalanceSettings asset missing; using default balance values."); return; }
        if (!asset.TryApply(out string error)) Debug.LogError("CombBalanceSettings: " + error);
    }
}
