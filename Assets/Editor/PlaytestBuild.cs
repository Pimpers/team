using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

// Command-line development player containing the opt-in background playtest harnesses.
public static class PlaytestBuild
{
    public static void BuildWindows()
    {
        string output = "Build/Playtest/CatsSmash.exe";
        var args = System.Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++)
            if (args[i] == "-playtestBuildPath") output = args[i + 1];
        var scenes = EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path).ToArray();
        var options = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = output,
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.Development
        };
        BuildReport report = BuildPipeline.BuildPlayer(options);
        if (report.summary.result != BuildResult.Succeeded)
        {
            Debug.LogError($"Playtest build failed: {report.summary.result}");
            if (Application.isBatchMode) EditorApplication.Exit(1);
            return;
        }
        if (Application.isBatchMode) EditorApplication.Exit(0);
    }
}
