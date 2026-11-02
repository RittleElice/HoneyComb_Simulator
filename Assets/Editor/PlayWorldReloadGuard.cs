using UnityEditor;

// The live world and its time scheduler are managed objects. Replacing their
// assemblies mid-session leaves the scene alive without its game state.
[InitializeOnLoad]
internal static class PlayWorldReloadGuard
{
    private static bool locked;

    static PlayWorldReloadGuard()
    {
        EditorApplication.playModeStateChanged += OnPlayState;
        if (EditorApplication.isPlaying) Lock();
    }

    private static void OnPlayState(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredPlayMode) Lock();
        if (state == PlayModeStateChange.ExitingPlayMode && locked)
        {
            locked = false;
            EditorApplication.UnlockReloadAssemblies();
        }
    }

    private static void Lock()
    {
        if (locked) return;
        EditorApplication.LockReloadAssemblies();
        locked = true;
    }
}
