using System;
using System.IO;
using UnityEditor;
using UnityEditor.AddressableAssets.Build;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// One-click WebGL build for itch.io: rebuilds the Addressables content for
/// WebGL first (the gem prefab is loaded by key at runtime, so a player built
/// without fresh content dies at PRESS START), then builds the player.
///
/// Runs from the menu or headless:
///   Unity -batchmode -quit -projectPath . -buildTarget WebGL
///         -executeMethod WebGLBuilder.BuildForItch -logFile build.log
///
/// Batch mode auto-answers the "save modified scenes?" style dialogs that
/// otherwise block an Editor-driven build.
/// </summary>
public static class WebGLBuilder
{
    private const string ScenePath = "Assets/_Project/scenes/mainGame.unity";
    private const string OutputDir = "Builds/WebGL/LandOfTheLustrous";

    [MenuItem("Tools/Merge3/Build WebGL for itch.io")]
    public static void BuildForItch()
    {
        Debug.Log("[WebGLBuilder] Addressables content build (WebGL) starting.");
        AddressableAssetSettings.CleanPlayerContent();
        AddressableAssetSettings.BuildPlayerContent(out AddressablesPlayerBuildResult content);
        if (!string.IsNullOrEmpty(content.Error))
        {
            Debug.LogError("[WebGLBuilder] Addressables build failed: " + content.Error);
            if (Application.isBatchMode) EditorApplication.Exit(1);
            return;
        }
        Debug.Log("[WebGLBuilder] Addressables done in " + content.Duration.ToString("0") + "s.");

        if (Directory.Exists(OutputDir))
            Directory.Delete(OutputDir, true);

        var options = new BuildPlayerOptions
        {
            scenes = new[] { ScenePath },
            locationPathName = OutputDir,
            target = BuildTarget.WebGL,
            options = BuildOptions.None,
        };

        Debug.Log("[WebGLBuilder] Player build starting -> " + OutputDir);
        BuildReport report = BuildPipeline.BuildPlayer(options);
        BuildSummary summary = report.summary;

        foreach (BuildStep step in report.steps)
            foreach (BuildStepMessage message in step.messages)
                if (message.type == LogType.Error || message.type == LogType.Exception)
                    Debug.LogError("[WebGLBuilder] " + step.name + ": " + message.content);

        Debug.Log("[WebGLBuilder] Result=" + summary.result + " errors=" + summary.totalErrors +
                  " warnings=" + summary.totalWarnings + " size=" + summary.totalSize / (1024 * 1024) + "MB" +
                  " time=" + summary.totalTime.TotalSeconds.ToString("0") + "s");

        if (Application.isBatchMode)
            EditorApplication.Exit(summary.result == BuildResult.Succeeded ? 0 : 1);
    }
}
