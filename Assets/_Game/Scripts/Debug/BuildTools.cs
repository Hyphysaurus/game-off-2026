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
    /// Shift Jam / Build (D-0040, D-0127): a Windows player of the scenes ticked in Build Settings, written to
    /// Builds/Windows/ShiftJam_vN/ShiftJam.exe (git-ignored) and ready to ship as it is: the folder holds the game only.
    /// Every build takes the next whole version number, stamped into the player settings so the title shows "vN"; a
    /// failed build puts the old number back. The result, time, size and biggest files go to
    /// Builds/Windows/logs/ShiftJam_vN-build-report.txt, beside the game rather than in it.
    /// Batch: -executeMethod ShiftJam.BuildTools.WindowsBatch [-buildVersion N] [-development] (quits by itself, exit 1 on
    /// failure). A development build (D-0043) keeps the dev keys (F1 console, F2 Pocket Core, F8 size) for his playtests;
    /// the title then reads "vN dev", in ShiftJam_vN_dev. The jam release stays a plain build, and drops Burst's
    /// DoNotShip debug folder. Build, check and upload in one go: docs/tools/ship.ps1.
    /// Check a build with: ShiftJam.exe -batchmode -smoketest -logFile smoke.log (SmokeRun).
    /// </summary>
    public static class BuildTools
    {
        private const string ExeName = "ShiftJam.exe";

        [MenuItem("Shift Jam/Build/Windows (next version)")]
        public static void WindowsMenu() => Windows(NextVersion());

        [MenuItem("Shift Jam/Build/Windows Development (next version, dev keys on)")]
        public static void WindowsDevelopmentMenu() => Windows(NextVersion(), true);

        public static void WindowsBatch()
        {
            int version = NextVersion();
            string[] args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, "-buildVersion");
            if (i >= 0 && i + 1 < args.Length && int.TryParse(args[i + 1], out int asked)) version = asked;
            bool ok = Windows(version, Array.IndexOf(args, "-development") >= 0);
            LLMUnity.LLMBuilder.Reset();   // as PartyBuilder.RebuildAndBuildBatch: quitting skips LLMUnity's own restore
            EditorApplication.Exit(ok ? 0 : 1);
        }

        /// <summary>Builds/Windows: the game folders, and their logs in Builds/Windows/logs (ship.ps1 reads the same).</summary>
        public static string BuildsDir => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Builds", "Windows"));

        public static string FolderName(int version, bool development) => "ShiftJam_v" + version + (development ? "_dev" : "");

        private static int NextVersion() => int.TryParse(PlayerSettings.bundleVersion, out int now) ? now + 1 : 1;

        public static bool Windows(int version, bool development = false)
        {
            string[] scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            if (scenes.Length == 0) { Debug.LogError("[Build] No scenes ticked in Build Settings."); return false; }
            string name = FolderName(version, development);
            string dir = Path.Combine(BuildsDir, name);
            string logs = Path.Combine(BuildsDir, "logs");
            Directory.CreateDirectory(dir);
            Directory.CreateDirectory(logs);

            string before = PlayerSettings.bundleVersion;
            PlayerSettings.bundleVersion = version.ToString();
            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = Path.Combine(dir, ExeName),
                target = BuildTarget.StandaloneWindows64,
                options = development ? UnityEditor.BuildOptions.Development : UnityEditor.BuildOptions.None,   // ShiftJam.BuildOptions is Build Mode's own
            };
            Debug.Log($"[Build] v{version}{(development ? " (development)" : "")}: {scenes.Length} scenes ({string.Join(", ", scenes.Select(Path.GetFileNameWithoutExtension))}) → {dir}");
            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary sum = report.summary;
            bool ok = sum.result == BuildResult.Succeeded;
            if (!ok) PlayerSettings.bundleVersion = before;
            AssetDatabase.SaveAssets();
            // Burst's debug symbols are for us, not players (Unity names the folder DoNotShip): a release build drops them.
            if (ok && !development)
                foreach (string sub in Directory.GetDirectories(dir, "*_BurstDebugInformation_DoNotShip"))
                    Directory.Delete(sub, true);
            string libraries = ok ? DropUnusedLibraries(dir, scenes) : null;

            var text = new StringBuilder();
            text.AppendLine($"Shift Jam v{version} Windows {(development ? "development build (dev keys on)" : "build")}: {sum.result}");
            text.AppendLine($"Time {sum.totalTime.TotalMinutes:0.0} min, size {sum.totalSize / (1024f * 1024f):0.0} MB, errors {sum.totalErrors}, warnings {sum.totalWarnings}");
            text.AppendLine($"Scenes: {string.Join(", ", scenes)}");
            if (libraries != null) text.AppendLine(libraries);
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
            File.WriteAllText(Path.Combine(logs, name + "-build-report.txt"), text.ToString());
            Debug.Log("[Build] " + text.ToString().Replace("\n", " | "));
            return ok;
        }

        /// <summary>
        /// LLMUnity (for the local AI player idea, after the jam) keeps its libraries in Assets/StreamingAssets, and Unity
        /// copies that folder into every build whole: 425 MB of Windows libraries that no scene uses yet (D-0127; they
        /// used to be cut from a hand-made share copy). Once the build is made they come out of it again, unless a ticked
        /// scene uses an LLMUnity part. Taking them out of the build, not out of the project, leaves LLMUnity untouched:
        /// it downloads its libraries again when they are missing, and moves them about during a build itself.
        /// Returns the build report's line.
        /// </summary>
        private static string DropUnusedLibraries(string dir, string[] scenes)
        {
            string streaming = Path.Combine(dir, Path.GetFileNameWithoutExtension(ExeName) + "_Data", "StreamingAssets");
            string[] folders = Directory.Exists(streaming) ? Directory.GetDirectories(streaming, "LlamaLib*") : new string[0];
            if (folders.Length == 0) return null;
            bool sawScripts = false;
            foreach (string scene in scenes)
            {
                string[] parts = AssetDatabase.GetDependencies(scene, true);
                sawScripts |= parts.Any(p => p.EndsWith(".cs", StringComparison.Ordinal));
                string part = parts.FirstOrDefault(p => p.StartsWith("Assets/LLMUnity/", StringComparison.Ordinal) && p.EndsWith(".cs", StringComparison.Ordinal));
                if (part != null) return $"LLMUnity libraries kept: {Path.GetFileNameWithoutExtension(scene)} uses {part}";
            }
            // Blind (no scene listed any script at all): keep them rather than strip what a scene might need.
            if (!sawScripts) return "LLMUnity libraries kept: the scenes' scripts could not be listed, so their use was not ruled out";
            long bytes = 0;
            foreach (string folder in folders)
            {
                bytes += new DirectoryInfo(folder).EnumerateFiles("*", SearchOption.AllDirectories).Sum(f => f.Length);
                Directory.Delete(folder, true);
            }
            if (!Directory.EnumerateFileSystemEntries(streaming).Any()) Directory.Delete(streaming);
            return $"LLMUnity libraries left out: {bytes / (1024f * 1024f):0} MB (no scene uses LLMUnity)";
        }
    }
}
#endif
