#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace ShiftJam
{
    /// <summary>
    /// The visual pass's tools (D-0119). Shift Jam / Look / Render Stills (before | after): the same camera spots in
    /// every scene, rendered to docs/stills/look_&lt;tag&gt;_&lt;scene&gt;_&lt;n&gt;.png, so a change is judged side by side.
    /// Shift Jam / Look / Apply Shift Jam Look: the game's look, written into URP's global default volume profile (so
    /// every scene gets it with no scene edits; Colour Corner's own grade still sits on top): neutral tonemapping, a
    /// soft bloom on bright toys, a warm grade, a light vignette; the ambient occlusion tuned for toy-sized contact
    /// shadows. Every number is a constant below; running it again sets them again.
    /// </summary>
    public static class LookTools
    {
        private const string StillDir = "docs/stills";

        private readonly struct Shot
        {
            public readonly string Scene; public readonly Vector3 Spot, Forward;
            public Shot(string scene, Vector3 spot, Vector3 forward) { Scene = scene; Spot = spot; Forward = forward.normalized; }
        }

        /// <summary>Spots where a toy stands (from his play logs), looking along the way he went. Zero spot = the scene's
        /// Player.</summary>
        private static readonly Shot[] Shots =
        {
            new Shot("Greybox", new Vector3(92.4f, 0f, -144.4f), new Vector3(1f, 0f, 0f)),          // the hallway
            new Shot("Greybox", new Vector3(329.3f, 36f, -4.5f), new Vector3(0.27f, 0f, -0.96f)),   // the playroom, from the table
            new Shot("Stadium", new Vector3(-6f, 0f, -12f), new Vector3(0.6f, 0f, 0.8f)),
            new Shot("Stadium", new Vector3(10f, 0f, 14f), new Vector3(-0.5f, 0f, -0.85f)),
            new Shot("Raceway", Vector3.zero, Vector3.zero),
            new Shot("ColourCorner", Vector3.zero, Vector3.zero),
            new Shot("Workshop", Vector3.zero, Vector3.zero),
        };

        [MenuItem("Shift Jam/Look/Render Stills (before)")] private static void Before() => Render("before");
        [MenuItem("Shift Jam/Look/Render Stills (after)")] private static void After() => Render("after");

        public static void Render(string tag)
        {
            if (EditorApplication.isPlaying) { Debug.LogWarning("LookTools: stop Play first."); return; }
            Scene home = SceneManager.GetActiveScene();
            if (home.isDirty && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Directory.CreateDirectory(StillDir);
            var count = new Dictionary<string, int>();
            foreach (var shot in Shots)
            {
                Scene s = SceneManager.GetSceneByName(shot.Scene);
                bool opened = false;
                if (!s.isLoaded) { s = EditorSceneManager.OpenScene("Assets/_Game/Scenes/" + shot.Scene + ".unity", OpenSceneMode.Additive); opened = true; }
                var hidden = new List<GameObject>();
                for (int i = 0; i < SceneManager.sceneCount; i++)
                {
                    Scene other = SceneManager.GetSceneAt(i);
                    if (other == s || !other.isLoaded) continue;
                    foreach (var r in other.GetRootGameObjects()) if (r.activeSelf) { r.SetActive(false); hidden.Add(r); }
                }
                SceneManager.SetActiveScene(s);
                try
                {
                    Vector3 spot = shot.Spot, fwd = shot.Forward;
                    if (fwd == Vector3.zero)
                    {
                        GameObject p = null;
                        foreach (var r in s.GetRootGameObjects()) if (r.name == "Player") p = r;
                        if (p == null) continue;
                        spot = p.transform.position; fwd = Vector3.ProjectOnPlane(p.transform.forward, Vector3.up).normalized;
                    }
                    int n = count.TryGetValue(shot.Scene, out int c) ? c + 1 : 1; count[shot.Scene] = n;
                    RenderOne(spot, fwd, $"{StillDir}/look_{tag}_{shot.Scene}_{n}.png");
                }
                finally
                {
                    foreach (var r in hidden) if (r != null) r.SetActive(true);
                    SceneManager.SetActiveScene(home);
                    if (opened) EditorSceneManager.CloseScene(s, true);
                }
            }
            Debug.Log($"LookTools: {tag} stills in {StillDir}.");
        }

        private static void RenderOne(Vector3 spot, Vector3 fwd, string path)
        {
            const float body = 1.8f;
            var go = new GameObject("LookCam");
            try
            {
                var cam = go.AddComponent<Camera>();
                cam.fieldOfView = 60f; cam.nearClipPlane = 0.05f; cam.farClipPlane = 3000f;
                var data = go.AddComponent<UniversalAdditionalCameraData>();
                data.renderPostProcessing = true;
                data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
                go.transform.position = spot - fwd * (3.2f * body) + Vector3.up * (1.6f * body);
                go.transform.LookAt(spot + fwd * (4f * body) + Vector3.up * (0.6f * body));
                var rt = new RenderTexture(1600, 900, 24);
                cam.targetTexture = rt;
                cam.Render();
                RenderTexture.active = rt;
                var tex = new Texture2D(1600, 900, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0); tex.Apply();
                File.WriteAllBytes(path, tex.EncodeToPNG());
                RenderTexture.active = null; cam.targetTexture = null;
                Object.DestroyImmediate(rt); Object.DestroyImmediate(tex);
            }
            finally { Object.DestroyImmediate(go); }
        }

        // ---------------------------------------------------------------- the look

        [MenuItem("Shift Jam/Look/Apply Shift Jam Look (D-0119)")]
        public static void ApplyLook()
        {
            var settings = GraphicsSettings.GetRenderPipelineSettings<URPDefaultVolumeProfileSettings>();
            VolumeProfile p = settings != null ? settings.volumeProfile : null;
            if (p == null) { Debug.LogError("LookTools: no global default volume profile."); return; }
            Undo.RecordObject(p, "Shift Jam Look");

            if (p.TryGet(out Tonemapping tone)) { tone.active = true; tone.mode.Override(TonemappingMode.Neutral); }
            if (p.TryGet(out Bloom bloom))
            {
                bloom.active = true;
                bloom.threshold.Override(1.05f); bloom.intensity.Override(0.35f); bloom.scatter.Override(0.6f);
            }
            if (p.TryGet(out ColorAdjustments grade))
            {
                grade.active = true;
                grade.postExposure.Override(0.12f); grade.contrast.Override(10f); grade.saturation.Override(12f);
                grade.colorFilter.Override(new Color(1f, 0.975f, 0.94f));   // a touch warm: late afternoon
            }
            if (p.TryGet(out Vignette vig)) { vig.active = true; vig.intensity.Override(0.18f); vig.smoothness.Override(0.45f); }
            foreach (var c in p.components) EditorUtility.SetDirty(c);
            EditorUtility.SetDirty(p);

            // Ambient occlusion: the renderer already has it; stronger and tighter for toy-scale contact shadows.
            var rendererData = AssetDatabase.LoadAssetAtPath<UniversalRendererData>("Assets/Settings/PC_Renderer.asset");
            if (rendererData != null)
                foreach (var f in rendererData.rendererFeatures)
                    if (f != null && f.name == "ScreenSpaceAmbientOcclusion")
                    {
                        var so = new SerializedObject(f);
                        var s = so.FindProperty("m_Settings");
                        if (s != null)
                        {
                            s.FindPropertyRelative("Intensity").floatValue = 0.9f;
                            s.FindPropertyRelative("Radius").floatValue = 0.22f;
                            s.FindPropertyRelative("DirectLightingStrength").floatValue = 0.3f;
                            so.ApplyModifiedPropertiesWithoutUndo();
                        }
                        f.SetActive(true);
                        EditorUtility.SetDirty(rendererData);
                    }
            AssetDatabase.SaveAssets();
            Debug.Log("LookTools: Shift Jam look applied to the global default profile and the renderer's AO.");
        }
    }
}
#endif
