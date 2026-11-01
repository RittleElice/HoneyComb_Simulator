using UnityEditor;

[InitializeOnLoad]
public static class ThermalBufferLifetime
{
    static ThermalBufferLifetime()
    {
        AssemblyReloadEvents.beforeAssemblyReload += ThermalExchange.ReleaseAllBuffers;
        EditorApplication.quitting += ThermalExchange.ReleaseAllBuffers;
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.ExitingPlayMode) ThermalExchange.ReleaseAllBuffers();
        };
    }
}
