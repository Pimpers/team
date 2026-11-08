using UnityEngine;
using UnityEngine.SceneManagement;

// Goes through LoadingScene (the rolling-cat screen) instead of jumping straight to a
// scene. LoadingScreen reads TargetScene and loads it in the background.
public static class SceneLoader
{
    public const string LoadingSceneName = "LoadingScene";

    public static string TargetScene { get; private set; }

    // Fades to black first (ScreenFader): the switch's loading hitch then happens behind the black screen.
    public static void Load(string sceneName)
    {
        if (ScreenFader.Switching) return; // a second click while already leaving
        TargetScene = sceneName;
        StageWipe.ArrivedThroughExit = false;
        GameClock.ResetAll(); // never carry a paused card draft into the loading screen
        ScreenFader.FadeOut(() => SceneManager.LoadScene(LoadingSceneName));
    }

    // Stage to stage through the exit log (StageExit): the screen irises shut on the log's mouth (StageWipe) and the
    // next stage loads straight behind it - no loading screen in between, the iris is the transition.
    public static void LoadThroughExit(string sceneName, Vector2 mouthOnScreen)
    {
        if (ScreenFader.Switching || StageWipe.Busy) return;
        TargetScene = sceneName;
        GameClock.ResetAll();
        StageWipe.ArrivedThroughExit = true;
        StageWipe.Close(mouthOnScreen, () => SceneManager.LoadScene(sceneName));
    }
}
