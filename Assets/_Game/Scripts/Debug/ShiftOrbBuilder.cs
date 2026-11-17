#if UNITY_EDITOR
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace ShiftJam
{
    /// <summary>
    /// Shift Jam / Dev / Build Shift Orb: makes the shift orb from his Blender model (Art/Orb/ShiftOrb.fbx) and his
    /// cut-outs (orb_dots; orb_lines is the drawn-lines option). Two prefabs, both overwritten on a re-run (same GUIDs):
    ///   Resources/FX/ShiftOrbVisual  the orb alone (crystal radius 1): ShiftOrbLink.Dress hangs it on every shift
    ///                                core at run time, so the course, the Workshop and the Pocket Core need no rebuild.
    ///   Pieces/Interactables/ShiftOrb  a working core for Build Mode: trigger, ShiftSource (Toggle), link, the orb.
    /// The materials are kept, so colours tuned on them survive. Run it again after a Blender re-export that adds or
    /// removes bolts; a re-export that only reshapes them needs nothing.
    /// </summary>
    public static class ShiftOrbBuilder
    {
        private const string ModelPath = "Assets/_Game/Art/Orb/ShiftOrb.fbx";
        private const string LinesPath = "Assets/_Game/Art/Orb/orb_lines.png";
        private const string DotsPath = "Assets/_Game/Art/Orb/orb_dots.png";
        private const string CrystalPath = "Assets/_Game/Data/ShiftOrbCrystal.mat";
        private const string BoltPath = "Assets/_Game/Data/ShiftOrbBolt.mat";
        private const string InkPath = "Assets/_Game/Data/ShiftOrbInk.mat";
        private const string BoltEdgePath = "Assets/_Game/Data/ShiftOrbBoltEdge.mat";
        private const string HaloPath = "Assets/_Game/Data/ShiftOrbMote.mat";
        private const string AuraPath = "Assets/_Game/Data/ShiftOrbAura.mat";
        private const string PrefabPath = "Assets/_Game/Prefabs/Pieces/Interactables/ShiftOrb.prefab";
        private const string VisualPath = "Assets/_Game/Resources/FX/ShiftOrbVisual.prefab";

        /// <summary>The crystal's radius in the Build Mode piece. The model's (and the visual's) is 1.</summary>
        private const float Radius = 1.5f;

        [MenuItem("Shift Jam/Dev/Build Shift Orb")]
        public static void Build()
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (model == null) { Debug.LogError("[Orb] No model at " + ModelPath + " (export it from Blender first)."); return; }
            Mask(LinesPath, TextureWrapMode.Clamp);
            Mask(DotsPath, TextureWrapMode.Repeat);

            Material crystalMat = Crystal();
            // The bolts glow (16 Nov, his "the lightning blocks look a bit awkward": flat yellow with a black outline read as
            // stickers round a glass ball): their middle and their edge add light, and ShiftOrb tints both from the orb's glow.
            Material boltMat = Glow(BoltPath, new Color(1.3f, 1.9f, 2.1f));
            Material boltEdgeMat = Glow(BoltEdgePath, new Color(0.12f, 0.5f, 1.1f));
            Material inkMat = Unlit(InkPath, Color.black);
            if (crystalMat == null || boltMat == null || boltEdgeMat == null || inkMat == null) return;

            // 1. The orb alone, crystal radius 1 (the model's own size): Resources/FX/ShiftOrbVisual.
            //    ShiftOrbLink.Dress hangs it on every core at run time, scaled to the core's body.
            var visual = new GameObject("ShiftOrbVisual");
            var orb = visual.AddComponent<ShiftOrb>();

            var floating = new GameObject("Float").transform;
            floating.SetParent(visual.transform, false);

            var body = (GameObject)PrefabUtility.InstantiatePrefab(model);
            PrefabUtility.UnpackPrefabInstance(body, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            body.name = "Model";
            body.transform.SetParent(floating, false);

            var ring = new GameObject("Bolts").transform;
            ring.SetParent(body.transform, false);
            ring.gameObject.AddComponent<BoltBurst>();   // plays the bolts; it finds them under the ring by itself

            Renderer crystal = null; GameObject outline = null;
            var bolts = new System.Collections.Generic.List<Renderer>();
            foreach (var r in body.GetComponentsInChildren<MeshRenderer>(true))
            {
                r.shadowCastingMode = ShadowCastingMode.Off;
                r.receiveShadows = false;
                string n = r.name;
                if (n.StartsWith("Bolt"))
                {
                    r.sharedMaterials = r.sharedMaterials.Select(m => m != null && m.name.Contains("Yellow") ? boltMat : boltEdgeMat).ToArray();
                    r.transform.SetParent(ring, true);
                    r.enabled = false;   // hidden at rest: ShiftOrb shows them when it strikes
                    bolts.Add(r);
                }
                else if (n.Contains("Outline"))
                {
                    r.sharedMaterial = inkMat;
                    outline = r.gameObject;
                    outline.SetActive(false);
                }
                else
                {
                    r.sharedMaterial = crystalMat;
                    crystal = r;
                }
            }
            if (crystal == null) { Debug.LogError("[Orb] The model has no orb mesh (expected an object named Orb)."); Object.DestroyImmediate(visual); return; }
            bolts.Sort((a, b) => string.CompareOrdinal(a.name, b.name));

            var haloGo = GameObject.CreatePrimitive(PrimitiveType.Quad);
            haloGo.name = "Halo"; haloGo.transform.SetParent(floating, false);
            haloGo.transform.localScale = Vector3.one * 5f;   // 2.5 orb radii each way: the aura's "orb edge" of 0.4 depends on this
            Object.DestroyImmediate(haloGo.GetComponent<Collider>());
            var halo = haloGo.GetComponent<MeshRenderer>();
            // Two materials on the one quad: his mote, then the aura (outer glow + escaping crackles) drawn over it.
            halo.sharedMaterials = new[] { AssetDatabase.LoadAssetAtPath<Material>(HaloPath), Aura() };
            halo.shadowCastingMode = ShadowCastingMode.Off;
            halo.receiveShadows = false;
            haloGo.AddComponent<Billboard>();

            var lightGo = new GameObject("Light");
            lightGo.transform.SetParent(visual.transform, false);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Point; light.range = 8f; light.intensity = 1.4f;
            light.color = new Color(0.3f, 0.85f, 1f); light.shadows = LightShadows.None;

            var so = new SerializedObject(orb);
            so.FindProperty("crystal").objectReferenceValue = crystal;
            so.FindProperty("inkOutline").objectReferenceValue = outline;
            so.FindProperty("boltRing").objectReferenceValue = ring;
            so.FindProperty("floating").objectReferenceValue = floating;
            so.FindProperty("halo").objectReferenceValue = halo;
            so.FindProperty("glowLight").objectReferenceValue = light;
            so.ApplyModifiedPropertiesWithoutUndo();

            // Read what the log line needs before the scene copy is destroyed.
            string report = "crystal " + crystal.bounds.size.ToString("F2") + " across in the visual, " + bolts.Count + " bolts, outline "
                + (outline != null ? "found" : "MISSING") + ", halo " + (halo.sharedMaterials.All(m => m != null) ? "ok" : "MISSING A MATERIAL");
            Directory.CreateDirectory(Path.GetDirectoryName(VisualPath));
            var visualPrefab = PrefabUtility.SaveAsPrefabAsset(visual, VisualPath);
            Object.DestroyImmediate(visual);

            // 2. The Build Mode piece: a working core (trigger for E, ShiftSource Toggle) already wearing the orb.
            var root = new GameObject("ShiftOrb");
            var reach = root.AddComponent<SphereCollider>();
            reach.isTrigger = true; reach.radius = Radius * 1.6f;
            var source = root.AddComponent<ShiftSource>();   // Toggle, unlimited, awake: its defaults
            var worn = (GameObject)PrefabUtility.InstantiatePrefab(visualPrefab, root.transform);
            worn.name = "Orb";
            worn.transform.localScale = Vector3.one * Radius;
            foreach (var l in worn.GetComponentsInChildren<Light>(true)) l.range = 8f * Radius;
            var link = root.AddComponent<ShiftOrbLink>();
            var ls = new SerializedObject(link);
            ls.FindProperty("source").objectReferenceValue = source;
            ls.FindProperty("orb").objectReferenceValue = worn.GetComponent<ShiftOrb>();
            ls.ApplyModifiedPropertiesWithoutUndo();
            Directory.CreateDirectory(Path.GetDirectoryName(PrefabPath));
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
            AssetDatabase.SaveAssets();
            Debug.Log("[Orb] Shift orb -> " + VisualPath + " (worn by every core in Play) and " + PrefabPath + " (Build Mode piece): " + report + ".");
        }

        /// <summary>Batch entry: build the prefab, then render check stills (rest, flash, strike, dormant) to %TEMP%/sj_orb_stills.</summary>
        public static void BuildAndShoot()
        {
            Build();
            Shoot();
        }

        /// <summary>Renders the prefab in four states against a dark room colour. Nothing is saved to the project.</summary>
        public static void Shoot()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null) { Debug.LogError("[Orb] No prefab to shoot."); return; }
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            string dir = Path.Combine(Path.GetTempPath(), "sj_orb_stills");
            Directory.CreateDirectory(dir);

            var camGo = new GameObject("StillCam");
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.16f, 0.15f, 0.2f);
            cam.fieldOfView = 30f; cam.nearClipPlane = 0.1f; cam.farClipPlane = 200f;
            cam.allowHDR = false;
            camGo.tag = "MainCamera";

            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            var crystal = go.GetComponentsInChildren<MeshRenderer>(true).First(r => r.name == "Orb");
            var bolts = go.GetComponentsInChildren<MeshRenderer>(true).Where(r => r.name.StartsWith("Bolt")).ToArray();
            var ring = bolts.Length > 0 ? bolts[0].transform.parent : null;
            var halo = go.GetComponentsInChildren<MeshRenderer>(true).FirstOrDefault(r => r.name == "Halo");

            void Pose(string name, Vector3 from, float awake, float lightning, float seed, float flare, float boltScale)
            {
                camGo.transform.position = from;
                camGo.transform.rotation = Quaternion.LookRotation(-from.normalized, Vector3.up);
                var block = new MaterialPropertyBlock();
                block.SetFloat("_Awake", awake); block.SetFloat("_Lightning", lightning);
                block.SetFloat("_LightningSeed", seed); block.SetFloat("_Flare", flare);
                crystal.SetPropertyBlock(block);
                foreach (var bolt in bolts) { bolt.enabled = boltScale > 0f; bolt.transform.localScale = Vector3.one * Mathf.Max(boltScale, 0.01f); }
                if (ring != null) ring.rotation = camGo.transform.rotation;
                if (halo != null) halo.transform.rotation = camGo.transform.rotation;
                var rt = RenderTexture.GetTemporary(640, 640, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                cam.targetTexture = rt;
                cam.Render();
                RenderTexture.active = rt;
                var tex = new Texture2D(640, 640, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, 640, 640), 0, 0);
                tex.Apply();
                File.WriteAllBytes(Path.Combine(dir, name + ".png"), tex.EncodeToPNG());
                RenderTexture.active = null; cam.targetTexture = null;
                RenderTexture.ReleaseTemporary(rt);
                Object.DestroyImmediate(tex);
            }

            Vector3 close = new Vector3(0f, 0f, -10f);
            Vector3 far = new Vector3(0f, 0f, -24f);
            Vector3 side = new Vector3(7f, 3.5f, -6.5f);
            Pose("1_rest", close, 1f, 0f, 0f, 0f, 0f);
            Pose("2_flash", close, 1f, 1f, 0.05f, 0f, 0f);
            Pose("3_strike", far, 1f, 1f, 0.8f, 0.5f, 1.1f);
            Pose("4_dormant", close, 0f, 0f, 0f, 0f, 0f);
            Pose("5_rest_side", side, 1f, 0f, 0f, 0f, 0f);
            Pose("6_near_bolt", far, 1f, 1f, 0.6f, 0f, 0.85f);
            Debug.Log("[Orb] Stills -> " + dir);
        }

        private static void Mask(string path, TextureWrapMode wrap)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) { Debug.LogWarning("[Orb] Missing cut-out: " + path); return; }
            if (!importer.sRGBTexture && importer.wrapMode == wrap && importer.mipmapEnabled) return;
            importer.sRGBTexture = false;   // a mask: its values are amounts, not colours
            importer.wrapMode = wrap;
            importer.mipmapEnabled = true;
            importer.SaveAndReimport();
        }

        private static Material Crystal()
        {
            var shader = Shader.Find("ShiftJam/ShiftOrb");
            if (shader == null) { Debug.LogError("[Orb] Shader ShiftJam/ShiftOrb not found (did it fail to compile?)."); return null; }
            var mat = AssetDatabase.LoadAssetAtPath<Material>(CrystalPath);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, CrystalPath);
            }
            mat.shader = shader;
            mat.SetTexture("_Lines", AssetDatabase.LoadAssetAtPath<Texture2D>(LinesPath));
            mat.SetTexture("_Dots", AssetDatabase.LoadAssetAtPath<Texture2D>(DotsPath));
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static Material Aura()
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(AuraPath);
            if (mat != null) return mat;
            var shader = Shader.Find("ShiftJam/ShiftOrbAura");
            if (shader == null) { Debug.LogError("[Orb] Shader ShiftJam/ShiftOrbAura not found (did it fail to compile?)."); return null; }
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, AuraPath);
            return mat;
        }

        /// <summary>An unlit material that adds its light to the picture and can fade (its colour's alpha): the bolts.
        /// An existing one is kept as it is, so his tuning survives a rebuild.</summary>
        private static Material Glow(string path, Color colour)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null) return mat;
            mat = Unlit(path, colour);
            if (mat == null) return null;
            mat.SetFloat("_Surface", 1f);            // transparent
            mat.SetFloat("_Blend", 2f);              // additive
            mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)BlendMode.One);
            mat.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            mat.SetFloat("_DstBlendAlpha", (float)BlendMode.One);
            mat.SetFloat("_ZWrite", 0f);
            mat.SetFloat("_Cull", (float)CullMode.Off);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.renderQueue = (int)RenderQueue.Transparent;
            mat.SetShaderPassEnabled("DepthOnly", false);
            mat.SetShaderPassEnabled("SHADOWCASTER", false);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static Material Unlit(string path, Color colour)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null) return mat;
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) { Debug.LogError("[Orb] URP Unlit shader not found."); return null; }
            mat = new Material(shader);
            mat.SetColor("_BaseColor", colour);
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }
    }
}
#endif
