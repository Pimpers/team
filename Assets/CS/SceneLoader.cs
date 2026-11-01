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
        LeafWipe.ArrivedThroughLeaves = false;
        GameClock.ResetAll(); // never carry a paused card draft into the loading screen
        ScreenFader.FadeOut(() => SceneManager.LoadScene(LoadingSceneName));
    }

    // Stage to stage through the forest (StageExit): leaves sweep over the screen (LeafWipe) and the next stage
    // loads straight behind them - no loading screen in between, the leaves are the transition.
    public static void LoadThroughLeaves(string sceneName)
    {
        if (ScreenFader.Switching || LeafWipe.Busy) return;
        TargetScene = sceneName;
        GameClock.ResetAll();
        LeafWipe.ArrivedThroughLeaves = true;
        LeafWipe.Cover(LeafWipe.ToneFor(sceneName), () => SceneManager.LoadScene(sceneName));
    }
}
