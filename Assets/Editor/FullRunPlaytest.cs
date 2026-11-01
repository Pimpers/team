using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
// Batch: -executeMethod FullRunPlaytest.Run -fullRunPlaytest -playtestOut <dir> : plays a whole run to the boss by itself and writes <dir>/report.json (stages, hits, chests, cards, routes, victory).
// and leaves when its report is written (or after 9 minutes).
public static class FullRunPlaytest
{
    static double start; static string outDir;
    public static void Run()
    {
        var args = System.Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++) if (args[i] == "-playtestOut") outDir = args[i + 1];
        PlayerPrefs.SetInt("selected_character", System.Environment.GetEnvironmentVariable("HC_CHAR") == "1" ? 1 : 0);
        EditorSceneManager.OpenScene("Assets/Scenes/TitleScene.unity");
        start = EditorApplication.timeSinceStartup;
        EditorApplication.update += Tick; EditorApplication.isPlaying = true;
    }
    static void Tick()
    {
        bool done = outDir != null && Directory.Exists(outDir) && Directory.GetFiles(outDir, "*.json").Length > 0;
        if (!done && EditorApplication.timeSinceStartup - start < 540) return;
        EditorApplication.update -= Tick;
        EditorApplication.playModeStateChanged += st => { if (st == PlayModeStateChange.EnteredEditMode) EditorApplication.Exit(0); };
        EditorApplication.isPlaying = false;
    }
}
