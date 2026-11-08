#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// Shift Jam / Build (D-0040): a Windows player of the scenes ticked in Build Settings, written to
    /// Builds/Windows/vN/ShiftJam.exe (git-ignored). Every build takes the next whole version number, stamped into the
    /// player settings so the title shows "vN"; a failed build puts the old number back. The result, time, size and
    /// biggest files go to Builds/Windows/vN/build-report.txt.
    /// Batch: -executeMethod ShiftJam.BuildTools.WindowsBatch [-buildVersion N] (quits by itself, exit 1 on failure).
    /// Check a build with: ShiftJam.exe -batchmode -smoketest -logFile smoke.log (SmokeRun).
    /// </summary>
    public static class BuildTools
    {
        private const string ExeName = "ShiftJam.exe";

        [MenuItem("Shift Jam/Build/Windows (next version)")]
        public static void WindowsMenu() => Windows(NextVersion());

        public static void WindowsBatch()
        {
            int version = NextVersion();
            string[] args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, "-buildVersion");
            if (i >= 0 && i + 1 < args.Length && int.TryParse(args[i + 1], out int asked)) version = asked;
            bool ok = Windows(version);
            EditorApplication.Exit(ok ? 0 : 1);
        }

        private static int NextVersion() => int.TryParse(PlayerSettings.bundleVersion, out int now) ? now + 1 : 1;

        public static bool Windows(int version)
        {
            string[] scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            if (scenes.Length == 0) { Debug.LogError("[Build] No scenes ticked in Build Settings."); return false; }
            string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Builds", "Windows", "v" + version));
            Directory.CreateDirectory(dir);

            string before = PlayerSettings.bundleVersion;
            PlayerSettings.bundleVersion = version.ToString();
            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = Path.Combine(dir, ExeName),
                target = BuildTarget.StandaloneWindows64,
                options = UnityEditor.BuildOptions.None,   // ShiftJam.BuildOptions is Build Mode's own
            };
            Debug.Log($"[Build] v{version}: {scenes.Length} scenes ({string.Join(", ", scenes.Select(Path.GetFileNameWithoutExtension))}) → {dir}");
            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary sum = report.summary;
            bool ok = sum.result == BuildResult.Succeeded;
            if (!ok) PlayerSettings.bundleVersion = before;
            AssetDatabase.SaveAssets();

            var text = new StringBuilder();
            text.AppendLine($"Shift Jam v{version} Windows build: {sum.result}");
            text.AppendLine($"Time {sum.totalTime.TotalMinutes:0.0} min, size {sum.totalSize / (1024f * 1024f):0.0} MB, errors {sum.totalErrors}, warnings {sum.totalWarnings}");
            text.AppendLine($"Scenes: {string.Join(", ", scenes)}");
            if (ok && Directory.Exists(dir))
            {
                text.AppendLine("Folder sizes:");
                foreach (string sub in Directory.GetDirectories(dir))
                {
                    long bytes = new DirectoryInfo(sub).EnumerateFiles("*", SearchOption.AllDirectories).Sum(f => f.Length);
                    text.AppendLine($"  {Path.GetFileName(sub)}: {bytes / (1024f * 1024f):0.0} MB");
                }
                text.AppendLine("Biggest files:");
                foreach (var f in new DirectoryInfo(dir).EnumerateFiles("*", SearchOption.AllDirectories).OrderByDescending(f => f.Length).Take(8))
                    text.AppendLine($"  {f.FullName.Substring(dir.Length + 1)}: {f.Length / (1024f * 1024f):0.0} MB");
            }
            foreach (var step in report.steps)
                foreach (var m in step.messages)
                    if (m.type == LogType.Error || m.type == LogType.Exception)
                        text.AppendLine($"ERROR ({step.name}): {m.content.Split('\n')[0]}");
            File.WriteAllText(Path.Combine(dir, "build-report.txt"), text.ToString());
            Debug.Log("[Build] " + text.ToString().Replace("\n", " | "));
            return ok;
        }
    }
}
#endif
