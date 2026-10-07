using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Build.Reporting;

public static class DriftWindowsBuild
{
    public static string Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode before building.");
        var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
        if (scenes.Length != 1 || scenes[0] != "Assets/Drift/Scenes/Drift.unity") throw new InvalidOperationException("Unexpected startup scene configuration.");
        string output = Path.GetFullPath("../Builds/Windows-20261006/drift.exe");
        Directory.CreateDirectory(Path.GetDirectoryName(output));
        string reportFile = Path.GetFullPath("../Validation/windows-build-result.txt");
        File.WriteAllText(reportFile, "BUILDING");
            try
            {
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = scenes, locationPathName = output,
                    target = BuildTarget.StandaloneWindows64, options = BuildOptions.Development
                });
                var summary = report.summary;
                var text = new StringBuilder();
                text.AppendLine("Result: " + summary.result);
                text.AppendLine("Errors: " + summary.totalErrors + "; warnings: " + summary.totalWarnings);
                text.AppendLine("Duration: " + summary.totalTime);
                text.AppendLine("Bytes: " + summary.totalSize);
                text.AppendLine("Output: " + output);
                foreach (var step in report.steps)
                    foreach (var message in step.messages)
                        if (message.type == UnityEngine.LogType.Error || message.type == UnityEngine.LogType.Exception || message.type == UnityEngine.LogType.Warning)
                            text.AppendLine(message.type + ": " + message.content);
                File.WriteAllText(reportFile, text.ToString());
            }
            catch (Exception error) { File.WriteAllText(reportFile, error.ToString()); }
        return File.ReadAllText(reportFile);
    }
}
