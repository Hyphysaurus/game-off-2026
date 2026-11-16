#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ShiftJam
{
    /// <summary>
    /// "Build Paint": everything the paint needs, made by script in one go, safe to run again.
    /// 1. the 64 paint pictures' import settings (bump pictures as normal maps), 2. the decal shader and the greyable
    /// shader checked, 3. the materials (decal base, drop, bits, streak, ball), 4. the Decal renderer feature on every
    /// renderer the project's pipelines use, 5. Resources/PaintSettings (the pictures, materials, shader and sounds are
    /// refilled; the numbers on an existing asset are kept), 6. the PaintBall and MischiefSoldier prefabs (made once,
    /// then only re-wired), 7. the Colour Corner scene, its place in Build Settings and its title-menu button checked.
    /// Every line it logs starts with PAINTBUILD; a failed check is "PAINTBUILD FAIL" and makes a batch run exit 1.
    /// Batch: -executeMethod ShiftJam.PaintBuilder.Build
    /// </summary>
    public static class PaintBuilder
    {
        private const string Tag = "PAINTBUILD";
        private const string Pictures = "Assets/_Game/Art/PaintSplats/";
        private const string SettingsPath = "Assets/_Game/Resources/" + PaintSettings.ResourceName + ".asset";
        private const string DecalGraph = "Assets/_Game/Shaders/PaintDecal.shadergraph";
        private const string GreyShader = "Assets/_Game/Shaders/Greyable.shader";
        private const string Data = "Assets/_Game/Data/";
        private const string DecalMat = Data + "PaintDecal.mat", DropMat = Data + "PaintDrop.mat", BitsMat = Data + "PaintBits.mat";
        private const string StreakMat = Data + "PaintStreak.mat", BallMat = Data + "PaintBall.mat";
        private const string Combat = "Assets/_Game/Prefabs/Combat";
        private const string BallPrefab = Combat + "/PaintBall.prefab";
        private const string SoldierPrefab = Combat + "/MischiefSoldier.prefab";
        private const string GuardPrefab = "Assets/_Game/Prefabs/Pieces/Interactables/ToySoldier.prefab";   // the body the mischief soldier is made from
        private const string Sounds = "Assets/_Imports/CasePortman/Paint/";

        // What the paint ball and the soldier are made with the first time (afterwards the prefabs keep their own).
        private const float BallSpeed = 32f, BallDrop = 6f, BallLife = 5f, BallGloss = 0.8f;
        private const float SoldierHealth = 45f;   // three pistol darts, as the Godot bot's 3

        // The splat families as the pictures were drawn: name, the impact angle, where the ball touched down the picture.
        private static readonly (string name, float drawnAt, float anchor)[] Families =
            { ("head", 90f, 0.5f), ("a60", 60f, 0.44f), ("a35", 35f, 0.38f), ("a15", 15f, 0.3f) };
        private const int SplatPictures = 4, DropPictures = 6, RunPictures = 4, FlakePictures = 3;

        private static int _checks, _failed;

        /// <summary>A build-time check: logs it, and counts it when it fails.</summary>
        internal static bool Check(bool ok, string what)
        {
            _checks++;
            if (ok) Debug.Log($"{Tag} ok: {what}");
            else { _failed++; Debug.LogError($"{Tag} FAIL: {what}"); }
            return ok;
        }

        internal static void Say(string what) => Debug.Log($"{Tag} {what}");

        /// <summary>True for a shader that is there, has no compile errors and still has a subshader this machine can
        /// draw (a shader whose subshaders were all thrown out has "no errors" too, and draws nothing).</summary>
        private static bool Sound(Shader shader) => shader != null && !ShaderUtil.ShaderHasError(shader) && shader.isSupported && shader.passCount > 0;

        [MenuItem("Shift Jam/Paint/Build Paint (pictures, materials, decals, Colour Corner)")]
        public static void Build()
        {
            _checks = 0; _failed = 0;
            AssetDatabase.Refresh();
            ImportPictures();

            var decalShader = AssetDatabase.LoadAssetAtPath<Shader>(DecalGraph);
            Check(Sound(decalShader), "the decal shader " + DecalGraph + " imports without errors and this machine can draw it");
            var greyShader = AssetDatabase.LoadAssetAtPath<Shader>(GreyShader);
            Check(Sound(greyShader), "the greyable shader " + GreyShader + " compiles without errors and this machine can draw it");

            Material decal = MakeDecalMaterial(decalShader);
            Material drop = MakeParticleMaterial(DropMat, "Universal Render Pipeline/Particles/Simple Lit", false);
            Material bits = MakeParticleMaterial(BitsMat, "Universal Render Pipeline/Particles/Unlit", false);
            Material streak = MakeParticleMaterial(StreakMat, "Universal Render Pipeline/Particles/Unlit", true);
            Material ballLook = MakeBallMaterial();
            EnsureDecalFeature();

            PaintSettings settings = MakeSettings(decal, drop, bits, greyShader);
            PaintBall ball = MakeBall(ballLook, streak);
            GameObject soldier = MakeSoldier();
            AssetDatabase.SaveAssets();

            // By path: opening the scene can unload what was just made, and a reference held here would then be empty.
            if (settings != null && ball != null && soldier != null) CourseBuilder.LandColourCorner(SettingsPath, BallPrefab, SoldierPrefab);
            else Check(false, "the settings, the paint ball and the soldier are there to build the Colour Corner with");
            Check(TitleMenu.IsSandbox(TitleMenu.ColourCornerScene), "the title menu lists the Colour Corner as a sandbox (its 'Colours' button loads it)");
            AssetDatabase.SaveAssets();

            if (_failed == 0) Debug.Log($"{Tag} DONE: {_checks} checks, 0 failed");
            else Debug.LogError($"{Tag} DONE: {_checks} checks, {_failed} FAILED");
            if (Application.isBatchMode) EditorApplication.Exit(_failed > 0 ? 1 : 0);
        }

        // ---------- pictures ----------

        private static IEnumerable<string> PictureNames()
        {
            foreach (var family in Families.Select(f => f.name).Append("soft"))
                for (int n = 0; n < SplatPictures; n++) { yield return $"{family}_{n}_albedo"; yield return $"{family}_{n}_normal"; }
            for (int n = 0; n < DropPictures; n++) { yield return $"drop_{n}_albedo"; yield return $"drop_{n}_normal"; }
            for (int n = 0; n < RunPictures; n++) { yield return $"run_{n}_albedo"; yield return $"run_{n}_normal"; }
            for (int n = 0; n < FlakePictures; n++) yield return $"flakes_{n}_albedo";
            yield return "bounce_0_albedo";
        }

        /// <summary>Every paint picture imported the way a decal needs it: paint pictures as colour with their alpha,
        /// bump pictures as normal maps, never tiled (a decal shows one picture, edge to edge).</summary>
        private static void ImportPictures()
        {
            int found = 0, changed = 0, total = 0;
            foreach (string name in PictureNames())
            {
                total++;
                string path = Pictures + name + ".png";
                if (!(AssetImporter.GetAtPath(path) is TextureImporter importer)) { Debug.LogError($"{Tag} FAIL: missing picture {path}"); continue; }
                found++;
                bool bumps = name.EndsWith("_normal");
                var type = bumps ? TextureImporterType.NormalMap : TextureImporterType.Default;
                bool same = importer.textureType == type && importer.sRGBTexture == !bumps && importer.alphaIsTransparency == !bumps
                    && importer.mipmapEnabled && importer.wrapMode == TextureWrapMode.Clamp && importer.filterMode == FilterMode.Trilinear
                    && importer.npotScale == TextureImporterNPOTScale.None && importer.textureCompression == TextureImporterCompression.CompressedHQ;
                if (same) continue;
                importer.textureType = type;
                importer.sRGBTexture = !bumps;
                importer.alphaSource = TextureImporterAlphaSource.FromInput;
                importer.alphaIsTransparency = !bumps;
                importer.mipmapEnabled = true;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.filterMode = FilterMode.Trilinear;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.textureCompression = TextureImporterCompression.CompressedHQ;
                importer.SaveAndReimport();
                changed++;
            }
            Check(found == total, $"every paint picture is in {Pictures} ({found} of {total}; {changed} import settings set)");
        }

        private static PaintSettings.Look Look(string stem)
        {
            return new PaintSettings.Look
            {
                albedo = AssetDatabase.LoadAssetAtPath<Texture2D>(Pictures + stem + "_albedo.png"),
                normal = AssetDatabase.LoadAssetAtPath<Texture2D>(Pictures + stem + "_normal.png"),
            };
        }

        private static PaintSettings.Look[] Looks(string stem, int count) => Enumerable.Range(0, count).Select(n => Look($"{stem}_{n}")).ToArray();

        // ---------- materials ----------

        /// <summary>The decal material every paint material is copied from at run time (PaintLooks sets the picture, the
        /// colour and the gloss on each copy).</summary>
        private static Material MakeDecalMaterial(Shader shader)
        {
            if (shader == null) return null;
            var mat = AssetDatabase.LoadAssetAtPath<Material>(DecalMat);
            if (mat == null) { mat = new Material(shader) { name = "PaintDecal" }; AssetDatabase.CreateAsset(mat, DecalMat); }
            mat.shader = shader;
            mat.enableInstancing = true;   // decals that share a material are drawn in one call
            EditorUtility.SetDirty(mat);
            // Read from the shader's own data: in a batch run no render pipeline is up yet, so the material's active
            // subshader is the fallback and FindPass finds nothing.
            var names = new List<string>();
            var data = ShaderUtil.GetShaderData(shader);
            for (int i = 0; i < data.SubshaderCount; i++)
                for (int j = 0; j < data.GetSubshader(i).PassCount; j++) names.Add(data.GetSubshader(i).GetPass(j).Name);
            Check(names.Contains("DBufferProjector") && names.Contains("DecalScreenSpaceProjector"),
                "the decal shader has URP's projector passes (DBufferProjector, DecalScreenSpaceProjector); it has: " + string.Join(", ", names.Distinct()));
            string[] wanted = { "Base_Map", "Normal_Map", "Normal_Blend", "_PaintTint", "_PaintGloss", "_DrawOrder" };
            string missing = string.Join(", ", wanted.Where(p => !mat.HasProperty(p)));
            Check(missing.Length == 0, "the decal shader has the properties PaintLooks sets" + (missing.Length > 0 ? " (missing: " + missing + ")" : ""));
            return mat;
        }

        /// <summary>A URP particle material that takes each particle's colour: opaque for drops and bits, see-through for
        /// the ball's streak. Its look is only set when it is first made.</summary>
        private static Material MakeParticleMaterial(string path, string shaderName, bool seeThrough)
        {
            var shader = Shader.Find(shaderName);
            if (!Check(shader != null, "URP shader '" + shaderName + "' for " + path)) return null;
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null) return mat;
            mat = new Material(shader) { name = System.IO.Path.GetFileNameWithoutExtension(path) };
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", BallGloss);
            if (seeThrough)
            {
                mat.SetFloat("_Surface", 1f); mat.SetFloat("_Blend", 0f);
                mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha); mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                mat.SetFloat("_ZWrite", 0f);
                mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                mat.SetOverrideTag("RenderType", "Transparent");
                mat.renderQueue = (int)RenderQueue.Transparent;
            }
            else
            {
                mat.SetFloat("_Surface", 0f);
                mat.SetOverrideTag("RenderType", "Opaque");
            }
            mat.enableInstancing = true;
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        /// <summary>The ball in flight: glossy, white (each ball is tinted its shell's colour).</summary>
        private static Material MakeBallMaterial()
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(BallMat);
            if (mat != null) return mat;
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (!Check(shader != null, "URP Lit for " + BallMat)) return null;
            mat = new Material(shader) { name = "PaintBall" };
            mat.SetColor("_BaseColor", Color.white);
            mat.SetFloat("_Smoothness", BallGloss);
            AssetDatabase.CreateAsset(mat, BallMat);
            return mat;
        }

        // ---------- the Decal renderer feature ----------

        /// <summary>URP draws Decal Projectors only when the renderer has the Decal feature. Adds it (technique
        /// Automatic: on a PC that is the DBuffer, so paint takes the room's light and its own gloss) to every renderer
        /// of every pipeline asset the project uses, the way the renderer's own Inspector does.</summary>
        private static void EnsureDecalFeature()
        {
            var pipelines = new List<RenderPipelineAsset> { GraphicsSettings.defaultRenderPipeline };
            for (int i = 0; i < QualitySettings.count; i++) pipelines.Add(QualitySettings.GetRenderPipelineAssetAt(i));
            var renderers = new List<UniversalRendererData>();
            foreach (var pipeline in pipelines.Where(p => p is UniversalRenderPipelineAsset).Distinct())
            {
                var list = new SerializedObject(pipeline).FindProperty("m_RendererDataList");
                for (int i = 0; list != null && i < list.arraySize; i++)
                    if (list.GetArrayElementAtIndex(i).objectReferenceValue is UniversalRendererData data && !renderers.Contains(data)) renderers.Add(data);
            }
            int added = 0, had = 0;
            foreach (var data in renderers)
            {
                var feature = data.rendererFeatures.OfType<DecalRendererFeature>().FirstOrDefault();
                if (feature != null) { had++; if (!feature.isActive) { feature.SetActive(true); EditorUtility.SetDirty(data); } continue; }
                feature = ScriptableObject.CreateInstance<DecalRendererFeature>();
                feature.name = nameof(DecalRendererFeature);
                AssetDatabase.AddObjectToAsset(feature, data);
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out _, out long localId);
                var so = new SerializedObject(data);
                var features = so.FindProperty("m_RendererFeatures"); var map = so.FindProperty("m_RendererFeatureMap");
                features.arraySize++; features.GetArrayElementAtIndex(features.arraySize - 1).objectReferenceValue = feature;
                map.arraySize++; map.GetArrayElementAtIndex(map.arraySize - 1).longValue = localId;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(data);
                added++;
                Say("added the Decal renderer feature to " + AssetDatabase.GetAssetPath(data));
            }
            AssetDatabase.SaveAssets();
            bool all = renderers.Count > 0 && renderers.All(r => r.rendererFeatures.OfType<DecalRendererFeature>().Any(f => f.isActive));
            Check(all, $"the Decal renderer feature is on every renderer in use ({renderers.Count} renderers: {had} had it, {added} added)");
        }

        // ---------- settings ----------

        private static PaintSettings MakeSettings(Material decal, Material drop, Material bits, Shader greyShader)
        {
            var s = AssetDatabase.LoadAssetAtPath<PaintSettings>(SettingsPath);
            bool made = s == null;
            if (made) { s = ScriptableObject.CreateInstance<PaintSettings>(); AssetDatabase.CreateAsset(s, SettingsPath); }

            // The families' numbers are tuning: only made when there are none. Their pictures are refilled by name.
            if (s.families == null || s.families.Length == 0)
                s.families = Families.Select(f => new PaintSettings.Family { name = f.name, drawnAt = f.drawnAt, anchor = f.anchor }).ToArray();
            foreach (var family in s.families) family.looks = Looks(family.name, SplatPictures);
            if (s.soft == null) s.soft = new PaintSettings.Family { name = "soft", anchor = 0.5f };
            s.soft.looks = Looks(s.soft.name, SplatPictures);
            s.drops = Looks("drop", DropPictures);
            s.runs = Looks("run", RunPictures);
            s.flakes = Looks("flakes", FlakePictures);
            s.skipMark = Look("bounce_0");

            s.decalMaterial = decal; s.dropMaterial = drop; s.bitsMaterial = bits; s.greyableShader = greyShader;
            s.beadMesh = Resources.GetBuiltinResource<Mesh>("Sphere.fbx");
            s.fireClips = Clips("fire_paint_", 2);
            s.hitClips = Clips("hit_paint_", 3);
            s.restoreClip = Clip("restore");
            s.finishedClip = Clip("corner_done");
            EditorUtility.SetDirty(s);

            var all = s.families.SelectMany(f => f.looks).Concat(s.soft.looks).Concat(s.drops).Concat(s.runs).ToList();
            int whole = all.Count(l => l.albedo != null && l.normal != null) + s.flakes.Count(l => l.albedo != null) + (s.skipMark.albedo != null ? 1 : 0);
            int wanted = all.Count + s.flakes.Length + 1;
            Check(whole == wanted, $"{SettingsPath} holds every look ({whole} of {wanted}); {(made ? "made with the built-in numbers" : "its numbers were kept")}");
            Check(s.beadMesh != null && decal != null && drop != null && bits != null && greyShader != null, "the settings' ball mesh, materials and greyable shader are all there");
            return s;
        }

        private static AudioClip Clip(string name)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(Sounds + name + ".ogg");
            if (clip == null) Debug.LogWarning($"{Tag} missing sound {Sounds}{name}.ogg (kept out of git: copy the Case Portman paint sounds there); it will not play");
            return clip;
        }

        private static AudioClip[] Clips(string stem, int count) =>
            Enumerable.Range(1, count).Select(n => Clip(stem + n)).Where(c => c != null).ToArray();

        // ---------- prefabs ----------

        /// <summary>The paint ball: a Projectile with a round ball and a streak. Made once (its speed, drop and life are
        /// then the prefab's to tune); a later run only re-wires its parts.</summary>
        private static PaintBall MakeBall(Material look, Material streak)
        {
            if (!AssetDatabase.IsValidFolder(Combat)) AssetDatabase.CreateFolder("Assets/_Game/Prefabs", "Combat");
            bool exists = AssetDatabase.LoadAssetAtPath<GameObject>(BallPrefab) != null;
            GameObject root = exists ? PrefabUtility.LoadPrefabContents(BallPrefab) : new GameObject("PaintBall");
            var ball = root.GetComponent<PaintBall>();
            if (ball == null) ball = root.AddComponent<PaintBall>();
            var settings = AssetDatabase.LoadAssetAtPath<PaintSettings>(SettingsPath);
            float diameter = settings != null ? settings.realDiameter : 0.02f;

            Transform body = root.transform.Find("Ball");
            if (body == null)
            {
                body = GameObject.CreatePrimitive(PrimitiveType.Sphere).transform;
                Object.DestroyImmediate(body.GetComponent<Collider>());
                body.name = "Ball";
                body.SetParent(root.transform, false);
                body.localScale = Vector3.one * diameter;
            }
            var drawn = body.GetComponent<MeshRenderer>();
            drawn.sharedMaterial = look;
            drawn.shadowCastingMode = ShadowCastingMode.Off;

            Transform tail = root.transform.Find("Streak");
            if (tail == null) { tail = new GameObject("Streak").transform; tail.SetParent(root.transform, false); }
            var trail = tail.GetComponent<TrailRenderer>();
            if (trail == null)
            {
                trail = tail.gameObject.AddComponent<TrailRenderer>();
                trail.minVertexDistance = diameter * 0.5f;
                trail.numCapVertices = 2;
                trail.widthMultiplier = settings != null ? settings.trailWidth : 0.035f;
                trail.time = (settings != null ? settings.trailLength : 0.9f) / BallSpeed;
            }
            trail.sharedMaterial = streak;
            trail.shadowCastingMode = ShadowCastingMode.Off;
            trail.receiveShadows = false;

            var so = new SerializedObject(ball);
            so.FindProperty("ball").objectReferenceValue = drawn;
            so.FindProperty("trail").objectReferenceValue = trail;
            if (!exists)
            {
                so.FindProperty("speed").floatValue = BallSpeed;
                so.FindProperty("radius").floatValue = diameter * 0.5f;
                so.FindProperty("lifetime").floatValue = BallLife;
                so.FindProperty("drop").floatValue = BallDrop;
                so.FindProperty("faceTravel").boolValue = false;
            }
            so.ApplyModifiedPropertiesWithoutUndo();

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, BallPrefab);
            if (exists) PrefabUtility.UnloadPrefabContents(root); else Object.DestroyImmediate(root);
            var result = saved != null ? saved.GetComponent<PaintBall>() : null;
            Check(result != null, BallPrefab + (exists ? " re-wired (its flight numbers kept)" : $" made (speed {BallSpeed}, drop {BallDrop}, life {BallLife} s)"));
            return result;
        }

        /// <summary>The soldier that rubs colour off: the guard soldier's body (the pack's rifleman, fitted and with its
        /// capsule and health) with the guard's mind taken out and the mischief put in. Made once.</summary>
        private static GameObject MakeSoldier()
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(SoldierPrefab);
            if (existing != null && existing.GetComponent<MischiefSoldier>() != null) { Check(true, SoldierPrefab + " kept"); return existing; }
            if (!Check(AssetDatabase.LoadAssetAtPath<GameObject>(GuardPrefab) != null, GuardPrefab + " is there to make the mischief soldier from (Shift Jam > Workshop > Build Combat & Vehicle Pieces makes it)")) return null;
            GameObject root = PrefabUtility.LoadPrefabContents(GuardPrefab);
            root.name = "MischiefSoldier";
            var guard = root.GetComponent<ToySoldier>();
            if (guard != null) Object.DestroyImmediate(guard);
            Transform muzzle = root.transform.Find("Muzzle");
            if (muzzle != null) Object.DestroyImmediate(muzzle.gameObject);
            var soldier = root.AddComponent<MischiefSoldier>();
            var so = new SerializedObject(soldier);
            so.FindProperty("visual").objectReferenceValue = root.transform.Find("Visual");
            so.ApplyModifiedPropertiesWithoutUndo();
            var health = new SerializedObject(root.GetComponent<Damageable>());
            health.FindProperty("maxHealth").floatValue = SoldierHealth;
            health.ApplyModifiedPropertiesWithoutUndo();
            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, SoldierPrefab);
            PrefabUtility.UnloadPrefabContents(root);
            Check(saved != null && saved.GetComponent<MischiefSoldier>() != null && saved.GetComponentInChildren<Renderer>() != null,
                SoldierPrefab + $" made from the guard soldier (health {SoldierHealth})");
            return saved;
        }
    }
}
#endif
