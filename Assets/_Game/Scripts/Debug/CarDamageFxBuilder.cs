#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// "Build Car Damage FX" (D-0059): writes Resources/CarDamageFx from the licensed packs (referenced by path, never
    /// copied): Polygon Arsenal's smoke and spark effects (its URP upgrade must be imported, or they draw pink) and Case
    /// Portman's impact sounds (copied into _Imports/CasePortman, kept out of git). It makes the flecks' material
    /// (Data/DamageFleck), bakes every car piece's paint colour from its body mesh and texture, points the car cannon's
    /// puff at the new smoke, and checks every effect it names for materials URP cannot draw. Tuning on an existing asset
    /// is kept; the picks, the material and the paints are rewritten. Batch: -executeMethod ShiftJam.CarDamageFxBuilder.Build
    /// </summary>
    public static class CarDamageFxBuilder
    {
        private const string Asset = "Assets/_Game/Resources/" + CarDamageFxSettings.ResourceName + ".asset";
        private const string FleckMat = "Assets/_Game/Data/DamageFleck.mat";
        private const string Vehicles = "Assets/_Game/Prefabs/Pieces/Vehicles";
        private const string Cannon = "Assets/_Game/Prefabs/Pieces/Traversal/CarCannon.prefab";
        private const string PA = "Assets/Polygon Arsenal/Prefabs/Environment/";
        private const string Sounds = "Assets/_Imports/CasePortman/CarImpacts/";
        private const string Thumbs = "Assets/_Game/Art/Thumbs/Pieces_Vehicles_";   // Build Catalog's renders of each piece

        public const string CannonSmoke = PA + "Smoke/Explosion/SmokeExplosionBright.prefab";
        private const string LightSmoke = PA + "Smoke/Rising/SmokeRisingBright.prefab";
        private const string DarkSmoke = PA + "Smoke/Rising/SmokeRisingDark.prefab";
        private const string SmallSparks = PA + "Sparks/Explosion/SparkDirectionalSmall.prefab";
        private const string NormalSparks = PA + "Sparks/Explosion/SparkDirectionalNormal.prefab";
        private const string WreckSparks = PA + "Sparks/Explosion/SparkExplosionNormal.prefab";
        private const string WreckSmoke = PA + "Smoke/Explosion/SmokeExplosionDark.prefab";

        [MenuItem("Shift Jam/Workshop/Build Car Damage FX")]
        public static void Build()
        {
            var fx = AssetDatabase.LoadAssetAtPath<CarDamageFxSettings>(Asset);
            if (fx == null)
            {
                fx = ScriptableObject.CreateInstance<CarDamageFxSettings>();
                AssetDatabase.CreateAsset(fx, Asset);
            }

            fx.lightSmoke = Load(LightSmoke);
            fx.darkSmoke = Load(DarkSmoke);
            fx.spitSparks = Load(SmallSparks);
            fx.hitSparks = Load(SmallSparks);
            fx.bigHitSparks = Load(NormalSparks);
            fx.wreckSparks = Load(WreckSparks);
            fx.wreckSmoke = Load(WreckSmoke);
            fx.knock = Clips("Impact_Dull_");
            fx.crash = Clips("Impact_Clash_Light_");
            fx.bigCrash = Clips("Impact_Clash_Strong_");
            fx.wreck = Clips("Impact_Powerful_");
            fx.fleckMaterial = MakeFleckMaterial();
            fx.paints = BakePaints();
            EditorUtility.SetDirty(fx);

            var cannon = PrefabUtility.LoadPrefabContents(Cannon);
            if (cannon != null)
            {
                var gun = cannon.GetComponent<CarCannon>();
                if (gun != null)
                {
                    var so = new SerializedObject(gun);
                    so.FindProperty("smokePrefab").objectReferenceValue = Load(CannonSmoke);
                    so.ApplyModifiedPropertiesWithoutUndo();
                    PrefabUtility.SaveAsPrefabAsset(cannon, Cannon);
                }
                PrefabUtility.UnloadPrefabContents(cannon);
            }
            AssetDatabase.SaveAssets();

            int bad = 0;
            foreach (var path in new[] { LightSmoke, DarkSmoke, SmallSparks, NormalSparks, WreckSparks, WreckSmoke, CannonSmoke })
                bad += CheckDrawable(path);
            Debug.Log($"DAMAGEFX built {Asset}: sounds {fx.knock.Length}/{fx.crash.Length}/{fx.bigCrash.Length}/{fx.wreck.Length}, " +
                      $"paints {fx.paints.Count}, fleck material {(fx.fleckMaterial != null ? fx.fleckMaterial.shader.name : "none")}, " +
                      $"undrawable materials {bad}");
        }

        private static GameObject Load(string path)
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (go == null) Debug.LogWarning("DAMAGEFX missing effect " + path);
            return go;
        }

        private static AudioClip[] Clips(string stem)
        {
            var list = new List<AudioClip>();
            for (int i = 1; i <= 4; i++)
            {
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>($"{Sounds}{stem}{i:00}.ogg");
                if (clip != null) list.Add(clip);
                else Debug.LogWarning($"DAMAGEFX missing sound {Sounds}{stem}{i:00}.ogg");
            }
            return list.ToArray();
        }

        /// <summary>An opaque URP particle material that takes the particle's colour: the flecks are tinted per chip.</summary>
        private static Material MakeFleckMaterial()
        {
            var shader = Shader.Find("Universal Render Pipeline/Particles/Simple Lit");
            if (shader == null) { Debug.LogWarning("DAMAGEFX no URP particle shader: no flecks"); return null; }
            var mat = AssetDatabase.LoadAssetAtPath<Material>(FleckMat);
            if (mat == null)
            {
                mat = new Material(shader) { name = "DamageFleck" };
                AssetDatabase.CreateAsset(mat, FleckMat);
            }
            mat.shader = shader;
            if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 0f);   // opaque
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.6f);   // a little die-cast shine
            mat.SetOverrideTag("RenderType", "Opaque");
            mat.renderQueue = -1;
            mat.enableInstancing = true;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        // ---------- paint colours ----------

        /// <summary>Every car piece's body paint. Which colour it is comes from the piece's catalog render where there is
        /// one (what a player sees: the mesh's own areas count its underside and insides), else from the body mesh by area;
        /// the colour itself is read from the body's texture (unshaded), ignoring greys, blacks and whites (tyres, glass,
        /// trim) unless the car is mostly grey.</summary>
        private static List<CarDamageFxSettings.Paint> BakePaints()
        {
            var paints = new List<CarDamageFxSettings.Paint>();
            var seen = new HashSet<string>();
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { Vehicles }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    if (root.GetComponent<ArcadeCar>() == null) continue;
                    var body = CarDamageFx.BodyOf(root.transform);
                    if (body == null || body.sharedMesh == null || !seen.Add(body.sharedMesh.name)) continue;
                    int seen0 = SeenHue(Thumbs + System.IO.Path.GetFileNameWithoutExtension(path) + ".png", out Color shaded);
                    if (Paint(body, seen0, shaded, out Color colour))
                    {
                        paints.Add(new CarDamageFxSettings.Paint { mesh = body.sharedMesh.name, colour = colour });
                        Debug.Log($"DAMAGEFX paint {System.IO.Path.GetFileNameWithoutExtension(path)} body {body.sharedMesh.name} #{ColorUtility.ToHtmlStringRGB(colour)} (from {(seen0 == -2 ? "mesh" : seen0 == -1 ? "render: grey" : "render")})");
                    }
                    else Debug.LogWarning($"DAMAGEFX no paint read for {path} ({body.sharedMesh.name})");
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            return paints;
        }

        /// <summary>The hue bucket (0..17, or -1 for grey) covering most of the car in its render, and that bucket's
        /// shaded colour; -2 when there is no render. A hue has to cover a good share of the car to count: a grey car with
        /// coloured trim stays grey.</summary>
        private static int SeenHue(string path, out Color shaded)
        {
            shaded = Color.grey;
            if (!System.IO.File.Exists(path)) return -2;
            var tex = new Texture2D(2, 2);
            try
            {
                if (!tex.LoadImage(System.IO.File.ReadAllBytes(path))) return -2;
                var px = tex.GetPixels32();
                var bg = px[0];
                var buckets = new Dictionary<int, (int n, Vector3 sum)>();
                int total = 0;
                foreach (var p in px)
                {
                    if (p.a < 200 || Mathf.Abs(p.r - bg.r) + Mathf.Abs(p.g - bg.g) + Mathf.Abs(p.b - bg.b) < 12) continue;
                    Color c = p;
                    int key = Bucket(c);
                    buckets.TryGetValue(key, out var b);
                    buckets[key] = (b.n + 1, b.sum + new Vector3(c.r, c.g, c.b));
                    total++;
                }
                if (total == 0) return -2;
                int best = -1, most = 0;
                foreach (var kv in buckets) if (kv.Key >= 0 && kv.Value.n > most) { most = kv.Value.n; best = kv.Key; }
                if (most < total * 0.13f) best = -1;
                if (!buckets.TryGetValue(best, out var won)) return -2;
                Vector3 mean = won.sum / won.n;
                shaded = new Color(mean.x, mean.y, mean.z, 1f);
                return best;
            }
            finally { Object.DestroyImmediate(tex); }
        }

        private static int Bucket(Color c)
        {
            Color.RGBToHSV(c, out float hue, out float sat, out float val);
            return sat < 0.25f || val < 0.2f ? -1 : Mathf.Clamp(Mathf.FloorToInt(hue * 18f), 0, 17);
        }

        /// <summary>The body's paint from its texture: the bucket <paramref name="seen"/> names (or, with no render
        /// (-2), the one covering most of the mesh). With nothing of that bucket on the mesh, the render's colour
        /// brightened, as its lighting darkened it.</summary>
        private static bool Paint(MeshFilter body, int seen, Color shaded, out Color colour)
        {
            colour = Color.white;
            var mesh = body.sharedMesh;
            var renderer = body.GetComponent<MeshRenderer>();
            var uv = mesh.uv;
            var verts = mesh.vertices;
            if (uv == null || uv.Length == 0 || renderer == null) return false;
            var mats = renderer.sharedMaterials;
            var buckets = new Dictionary<int, (float area, Vector3 sum)>();
            float total = 0f;
            for (int sub = 0; sub < mesh.subMeshCount; sub++)
            {
                var mat = sub < mats.Length ? mats[sub] : null;
                if (mat == null) continue;
                Color tint = mat.HasProperty("_BaseColor") ? mat.GetColor("_BaseColor") : mat.HasProperty("_Color") ? mat.GetColor("_Color") : Color.white;
                var tex = mat.HasProperty("_BaseMap") ? mat.GetTexture("_BaseMap") as Texture2D : mat.mainTexture as Texture2D;
                var pixels = Readable(tex, out int w, out int h);
                var tris = mesh.GetTriangles(sub);
                for (int t = 0; t + 2 < tris.Length; t += 3)
                {
                    int a = tris[t], b = tris[t + 1], c = tris[t + 2];
                    float area = Vector3.Cross(verts[b] - verts[a], verts[c] - verts[a]).magnitude * 0.5f;
                    if (area <= 0f) continue;
                    Vector2 mid = (uv[a] + uv[b] + uv[c]) / 3f;
                    Color px = pixels != null ? pixels[Wrap(mid.y, h) * w + Wrap(mid.x, w)] : Color.white;
                    Color col = px * tint;
                    int key = Bucket(col);
                    buckets.TryGetValue(key, out var bucket);
                    buckets[key] = (bucket.area + area, bucket.sum + new Vector3(col.r, col.g, col.b) * area);
                    total += area;
                }
            }
            if (total <= 0f) return false;
            int best = seen;
            if (seen >= 0)
            {
                // lighting shifts a hue a little: the render's bucket or a neighbour, whichever the texture has most of
                float most = 0f;
                for (int d = -1; d <= 1; d++)
                {
                    int k = (seen + d + 18) % 18;
                    if (buckets.TryGetValue(k, out var near) && near.area > most) { most = near.area; best = k; }
                }
            }
            if (seen == -2)
            {
                best = -1; float most = 0f;
                foreach (var kv in buckets)
                    if (kv.Key >= 0 && kv.Value.area > most) { most = kv.Value.area; best = kv.Key; }
                if (best < 0 || most < total * 0.1f) best = -1;
            }
            if (!buckets.TryGetValue(best, out var hit) || hit.area <= 0f)
            {
                if (seen == -2) return false;
                Color.RGBToHSV(shaded, out float h, out float sat, out float v);
                colour = Color.HSVToRGB(h, sat, Mathf.Max(v, best >= 0 ? 0.8f : 0.55f));
                return true;
            }
            Vector3 mean = buckets[best].sum / buckets[best].area;
            colour = new Color(mean.x, mean.y, mean.z, 1f);
            return true;
        }

        private static int Wrap(float t, int size) => Mathf.Clamp(Mathf.FloorToInt(Mathf.Repeat(t, 1f) * size), 0, size - 1);

        private static readonly Dictionary<Texture2D, (Color[] px, int w, int h)> Read = new Dictionary<Texture2D, (Color[], int, int)>();

        /// <summary>A texture's pixels without changing its import settings: drawn into a small render texture and read
        /// back (pack textures are not Read/Write).</summary>
        private static Color[] Readable(Texture2D tex, out int w, out int h)
        {
            w = h = 0;
            if (tex == null) return null;
            if (Read.TryGetValue(tex, out var done)) { w = done.w; h = done.h; return done.px; }
            w = Mathf.Min(256, tex.width); h = Mathf.Min(256, tex.height);
            var rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var was = RenderTexture.active;
            Graphics.Blit(tex, rt);
            RenderTexture.active = rt;
            var copy = new Texture2D(w, h, TextureFormat.RGBA32, false, false);
            copy.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            copy.Apply();
            RenderTexture.active = was;
            RenderTexture.ReleaseTemporary(rt);
            var px = copy.GetPixels();
            Object.DestroyImmediate(copy);
            Read[tex] = (px, w, h);
            return px;
        }

        // ---------- the pink check ----------

        /// <summary>How many of the effect's materials URP cannot draw (missing, broken or a built-in pipeline shader: the
        /// pink squares). Each is logged.</summary>
        private static int CheckDrawable(string path)
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (go == null) return 1;
            int bad = 0;
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                if (r is ParticleSystemRenderer pr && pr.renderMode == ParticleSystemRenderMode.None) continue;
                if (!r.enabled) continue;
                var mats = new List<Material>();
                if (r is ParticleSystemRenderer psr)
                {
                    mats.Add(psr.sharedMaterial);
                    if (psr.TryGetComponent(out ParticleSystem ps) && ps.trails.enabled) mats.Add(psr.trailMaterial);
                }
                else mats.AddRange(r.sharedMaterials);
                foreach (var m in mats)
                {
                    string why = m == null ? "no material" : m.shader == null ? "no shader" : !Drawable(m.shader) ? m.shader.name : null;
                    if (why == null) continue;
                    bad++;
                    Debug.LogWarning($"DAMAGEFX PINK {path} > {r.name}: {(m != null ? m.name : "-")} ({why})");
                }
            }
            return bad;
        }

        private static bool Drawable(Shader shader)
        {
            if (shader.name == "Hidden/InternalErrorShader" || !shader.isSupported) return false;
            foreach (var builtIn in new[] { "Standard", "Legacy Shaders/", "Particles/", "Mobile/" })
                if (shader.name.StartsWith(builtIn)) return false;
            return true;
        }
    }
}
#endif
