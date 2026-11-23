using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace LinuxFuriaPinguim.EditorTools
{
    /// <summary>
    /// Gera o executável do jogo para Windows (64 bits) com todas as cenas do Build Settings.
    /// Menu: Tools > Furia Pinguim > Build > Gerar .exe (Windows).
    /// Linha de comando: Unity -batchmode -quit -projectPath . -executeMethod
    ///   LinuxFuriaPinguim.EditorTools.WindowsBuilder.BuildBatch -buildPath C:\pasta\Jogo.exe
    /// </summary>
    public static class WindowsBuilder
    {
        private const string ExeName = "LinuxAFuriaDoPinguim.exe";

        [MenuItem("Tools/Furia Pinguim/Build/Gerar .exe (Windows)")]
        public static void BuildMenu()
        {
            string folder = EditorUtility.SaveFolderPanel("Pasta do build", "", "LinuxAFuriaDoPinguim");
            if (string.IsNullOrEmpty(folder)) { return; }
            var report = Build(Path.Combine(folder, ExeName));
            EditorUtility.DisplayDialog("Build", report.summary.result == BuildResult.Succeeded
                ? "Build pronto em " + folder : "Build falhou: " + report.summary.result, "OK");
        }

        public static void BuildBatch()
        {
            var args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, "-buildPath");
            string path = i >= 0 && i + 1 < args.Length ? args[i + 1] : Path.Combine("Build", ExeName);
            var report = Build(path);
            EditorApplication.Exit(report.summary.result == BuildResult.Succeeded ? 0 : 1);
        }

        private static BuildReport Build(string exePath)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(exePath));
            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled && File.Exists(s.path)).Select(s => s.path).ToArray();
            Debug.Log("[Build] Cenas: " + string.Join(", ", scenes));
            PlayerSettings.productName = "Linux: A Fúria do Pinguim";
            var options = new BuildPlayerOptions {
                scenes = scenes,
                locationPathName = exePath,
                target = BuildTarget.StandaloneWindows64,
                targetGroup = BuildTargetGroup.Standalone,
                options = BuildOptions.None,
            };
            var report = BuildPipeline.BuildPlayer(options);
            Debug.Log($"[Build] Resultado: {report.summary.result}, {report.summary.totalSize / (1024f * 1024f):F1} MB, " +
                      $"{report.summary.totalErrors} erro(s), {report.summary.totalTime.TotalSeconds:F0}s -> {exePath}");
            return report;
        }
    }
}
