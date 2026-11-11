#if UNITY_EDITOR
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace ShiftJam
{
    /// <summary>
    /// "Build Race Pieces" (D-0037) writes the race's Build Mode pieces into Pieces/Traversal from the Battle Stadium pack
    /// (referenced by asset path, never copied): RaceGate_1..8 (the checkpoint arch sized so a toy car passes, its number on
    /// a board on top, a trigger across the opening), RaceFinish (the finish arch with the RaceLoop and the chequered line
    /// on the floor), RaceTV (the jumbotron, the race on all four screens) and RacePodium (the winner's block with
    /// fireworks). Every piece faces +Z: cars drive through along +Z. The pack's own mesh colliders stay on (static
    /// scenery, exact shapes). Re-running overwrites the same prefabs in place (GUIDs kept). Then Build Catalog lists them.
    /// </summary>
    public static class RaceBuilder
    {
        private const string Traversal = "Assets/_Game/Prefabs/Pieces/Traversal";
        private const string Data = "Assets/_Game/Data";
        private const string BS = "Assets/_Imports/ithappy/Battle_Stadium/Prefabs/";
        private const string Sfx = "Assets/_Imports/ithappy/Playroom/Playroom_Sound_FX/";

        // Toy units (the toy is 1.8 tall; a Cyber car is 3.8 long and 2.1 wide, the wooden car 10.8 long and 4.2 wide).
        private const int GateCount = 8;             // RaceGate_1 .. RaceGate_8
        internal const float GateOpening = 7f;        // clear width between a gate's posts (the pack's is 5.55: x1.26)
        internal const float FinishOpening = 20f;     // the start/finish straight is wider (the pack's is 39.7: x0.5)
        private const float TvHeight = 26f;          // the pack's is 50.4 (x0.52): its screens come out 14 x 6
        private const float PodiumHeight = 6f;       // the pack's block is 20.9 tall and as wide (x0.29)
        private const float TriggerDepth = 4f;       // along the way through: a fast car never skips it
        private const float BoardW = 2.6f, BoardH = 2f, BoardD = 0.3f;   // a gate's number board, on top of the arch

        // TV_001's +X screen as measured from its Emission faces (pack units), used if that material is not found by name.
        private static readonly Vector3 TvScreenCentre = new Vector3(21.353f, 13.067f, 0.288f);
        private static readonly Vector3 TvScreenNormal = new Vector3(0.9792f, -0.2029f, 0f);
        private static readonly Vector2 TvScreenSize = new Vector2(25.77f, 10.69f);

        [MenuItem("Shift Jam/Workshop/Build Race Pieces")]
        public static void Build()
        {
            EnsureFolder(Traversal);
            var dark = DarkMaterial();
            var spark = AssetDatabase.LoadAssetAtPath<Material>(Data + "/Spark.mat");
            if (spark == null) Debug.LogWarning("[Race] Missing " + Data + "/Spark.mat: the fireworks get the default particle look.");
            var made = new List<string>();
            for (int n = 1; n <= GateCount; n++) if (BuildGate(n, dark)) made.Add("RaceGate_" + n);
            if (BuildFinish()) made.Add("RaceFinish");
            if (BuildTv(dark)) made.Add("RaceTV");
            if (BuildPodium(spark)) made.Add("RacePodium");
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[Race] Built {made.Count} pieces into {Traversal}: {string.Join(", ", made)}. Next: Shift Jam > Workshop > Build Catalog.");
        }

        // ---------- pieces ----------

        /// <summary>The pack's inflatable checkpoint arch, its opening sized to <see cref="GateOpening"/>; the trigger fills
        /// the opening and the arch's own mesh collider makes the sides solid. The number stands on a board on top.</summary>
        private static bool BuildGate(int number, Material dark)
        {
            var model = Load(BS + "Inflatable/checkpoint_001.prefab");
            if (model == null) return false;
            var root = new GameObject("RaceGate_" + number);
            var holder = new GameObject("Model").transform;
            holder.SetParent(root.transform, false);
            var inst = Place(model, holder, true);
            Bounds b = LocalBounds(holder, inst);
            if (!Opening(holder, inst, out float half, out float clear, out float midZ))
            {
                Debug.LogError("[Race] checkpoint_001: no opening found in its mesh; RaceGate not built.");
                Object.DestroyImmediate(root);
                return false;
            }
            float k = GateOpening / (2f * half);
            Fit(holder, k, new Vector3(0f, b.min.y, midZ), 0f);
            float clearH = (clear - b.min.y) * k, top = b.size.y * k;

            var trigger = root.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.center = new Vector3(0f, clearH * 0.5f, 0f);
            trigger.size = new Vector3(GateOpening * 0.98f, clearH, TriggerDepth);

            float boardY = top + BoardH * 0.5f - 0.15f;   // sits on the arch's round top
            var board = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.DestroyImmediate(board.GetComponent<Collider>());
            board.name = "NumberBoard";
            board.transform.SetParent(root.transform, false);
            board.transform.localPosition = new Vector3(0f, boardY, 0f);
            board.transform.localScale = new Vector3(BoardW, BoardH, BoardD);
            var br = board.GetComponent<MeshRenderer>();
            if (dark != null) br.sharedMaterial = dark;
            br.shadowCastingMode = ShadowCastingMode.Off;

            var signs = new List<Object>();
            for (int face = -1; face <= 1; face += 2)
            {
                // Text reads from the side it faces: the -Z board face looks at cars coming in, the +Z one at cars leaving.
                var tmp = Text(face < 0 ? "NumberIn" : "NumberOut", root.transform, new Vector3(0f, boardY, face * (BoardD * 0.5f + 0.02f)),
                    Quaternion.Euler(0f, face > 0 ? 180f : 0f, 0f), new Vector2(BoardW, BoardH), BoardH * 9.5f);
                tmp.text = number.ToString();
                signs.Add(tmp);
            }

            var gate = root.AddComponent<RaceGate>();
            Set(gate, "order", number);
            SetArray(gate, "signs", signs);
            Save(root, Traversal + "/RaceGate_" + number + ".prefab");
            return true;
        }

        /// <summary>The pack's inflatable finish arch, its opening sized to <see cref="FinishOpening"/> and turned so its lit
        /// sign faces the cars coming in; the pack's chequered strip lies across the floor under it; the RaceLoop's trigger
        /// fills the opening.</summary>
        private static bool BuildFinish()
        {
            var model = Load(BS + "Inflatable/finish_002.prefab");
            if (model == null) return false;
            var root = new GameObject("RaceFinish");
            var holder = new GameObject("Model").transform;
            holder.SetParent(root.transform, false);
            var inst = Place(model, holder, true);
            Bounds b = LocalBounds(holder, inst);
            if (!Opening(holder, inst, out float half, out float clear, out float midZ))
            {
                Debug.LogError("[Race] finish_002: no opening found in its mesh; RaceFinish not built.");
                Object.DestroyImmediate(root);
                return false;
            }
            float k = FinishOpening / (2f * half);
            Fit(holder, k, new Vector3(0f, b.min.y, midZ), 180f);   // the pack's sign faces its +Z: ours faces -Z, the way in
            float clearH = (clear - b.min.y) * k;

            // The chequered strip (flat, the pack's finish_001) across the opening, a hair above the floor.
            var stripModel = Load(BS + "Other/finish_001.prefab");
            if (stripModel != null)
            {
                var sh = new GameObject("FinishLine").transform;
                sh.SetParent(root.transform, false);
                var strip = Place(stripModel, sh, false);
                Bounds sb = LocalBounds(sh, strip);
                bool alongX = sb.size.x >= sb.size.z;
                float ks = FinishOpening / Mathf.Max(0.01f, alongX ? sb.size.x : sb.size.z);
                Fit(sh, ks, new Vector3(sb.center.x, sb.min.y, sb.center.z), alongX ? 0f : 90f);
                sh.localPosition += Vector3.up * 0.03f;
                foreach (var r in strip.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = ShadowCastingMode.Off;
            }

            var trigger = root.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.center = new Vector3(0f, clearH * 0.5f, 0f);
            trigger.size = new Vector3(FinishOpening * 0.98f, clearH, TriggerDepth);

            var audio = root.AddComponent<AudioSource>();
            audio.playOnAwake = false;
            audio.spatialBlend = 0f;   // the race's sounds are heard wherever the car is
            var loop = root.AddComponent<RaceLoop>();
            Set(loop, "laps", 3);
            Set(loop, "startClip", Clip("UI/UI_Confirm_v2.wav"));
            Set(loop, "gateClip", Clip("UI/UI_Select_v1.wav"));
            Set(loop, "lapClip", Clip("UI/UI_Notification_v2.wav"));
            Set(loop, "missedClip", Clip("UI/UI_Back_v1.wav"));
            Set(loop, "finishClip", Clip("UI/Win_v1.wav"));
            Save(root, Traversal + "/RaceFinish.prefab");
            return true;
        }

        /// <summary>The pack's four-sided jumbotron on its own base, <see cref="TvHeight"/> tall. Each screen gets a dark panel
        /// over the pack's lit picture and the race readout on it (RaceTv).</summary>
        private static bool BuildTv(Material dark)
        {
            var model = Load(BS + "Stadium/TV_001.prefab");
            if (model == null) return false;
            var root = new GameObject("RaceTV");
            var holder = new GameObject("Model").transform;
            holder.SetParent(root.transform, false);
            var inst = Place(model, holder, true);
            Bounds b = LocalBounds(holder, inst);
            if (!TvScreen(holder, inst, out Vector3 centre, out Vector3 normal, out Vector2 size))
                Debug.LogWarning("[Race] TV_001: no Emission screen faces found by name; using the measured screen.");
            float k = TvHeight / Mathf.Max(0.01f, b.size.y);
            Fit(holder, k, new Vector3(b.center.x, b.min.y, b.center.z), 0f);

            var screens = new List<Object>();
            for (int i = 0; i < 4; i++)
            {
                // The four screens are the +X one turned a quarter at a time about the TV's middle.
                Quaternion turn = Quaternion.Euler(0f, 90f * i, 0f);
                Vector3 n = holder.TransformDirection(turn * normal).normalized;
                Vector3 c = holder.TransformPoint(turn * centre);
                Quaternion face = Quaternion.LookRotation(-n, Vector3.ProjectOnPlane(Vector3.up, n));
                float w = size.x * k * 1.06f, h = size.y * k * 1.12f;   // a little larger than the pack's picture, to cover it

                var panel = GameObject.CreatePrimitive(PrimitiveType.Quad);
                Object.DestroyImmediate(panel.GetComponent<Collider>());
                panel.name = "Screen" + i;
                panel.transform.SetParent(root.transform, false);
                panel.transform.localPosition = c + n * 0.04f;
                panel.transform.localRotation = face;   // a quad shows its -Z side: here, outward
                panel.transform.localScale = new Vector3(w, h, 1f);
                var pr = panel.GetComponent<MeshRenderer>();
                if (dark != null) pr.sharedMaterial = dark;
                pr.shadowCastingMode = ShadowCastingMode.Off;

                var tmp = Text("Readout" + i, root.transform, c + n * 0.09f, face, new Vector2(w * 0.94f, h * 0.92f), h * 1.9f);
                tmp.text = "DRIVE THROUGH\n<size=160%>THE FINISH</size>\nTO START";
                screens.Add(tmp);
            }

            var tv = root.AddComponent<RaceTv>();
            SetArray(tv, "screens", screens);
            Save(root, Traversal + "/RaceTV.prefab");
            return true;
        }

        /// <summary>The pack's winner's block, <see cref="PodiumHeight"/> tall, with a world-space spark system on its top
        /// that RacePodium fires rockets and bursts into when a race is won.</summary>
        private static bool BuildPodium(Material spark)
        {
            var model = Load(BS + "Stadium/podium_001.prefab");
            if (model == null) return false;
            var root = new GameObject("RacePodium");
            var holder = new GameObject("Model").transform;
            holder.SetParent(root.transform, false);
            var inst = Place(model, holder, true);
            Bounds b = LocalBounds(holder, inst);
            float k = PodiumHeight / Mathf.Max(0.01f, b.size.y);
            Fit(holder, k, new Vector3(b.center.x, b.min.y, b.center.z), 0f);

            var fx = new GameObject("Fireworks");
            fx.transform.SetParent(root.transform, false);
            fx.transform.localPosition = new Vector3(0f, b.size.y * k + 0.2f, 0f);
            var ps = fx.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = false;
            main.loop = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Shape;   // the script hands every size and speed over, already scaled
            main.startSpeed = 0f;
            main.startLifetime = 1.3f;
            main.startSize = 1f;
            main.gravityModifier = 0.25f;
            main.maxParticles = 2000;
            var emission = ps.emission;
            emission.enabled = false;   // only RacePodium emits
            var shape = ps.shape;
            shape.enabled = false;
            var fade = ps.colorOverLifetime;
            fade.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.55f), new GradientAlphaKey(0f, 1f) });
            fade.color = new ParticleSystem.MinMaxGradient(g);
            var shrink = ps.sizeOverLifetime;
            shrink.enabled = true;
            shrink.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.35f));
            var psr = fx.GetComponent<ParticleSystemRenderer>();
            if (spark != null) psr.sharedMaterial = spark;
            psr.renderMode = ParticleSystemRenderMode.Billboard;
            psr.shadowCastingMode = ShadowCastingMode.Off;
            psr.receiveShadows = false;

            var audio = root.AddComponent<AudioSource>();
            audio.playOnAwake = false;
            audio.spatialBlend = 0.6f;   // mostly where the podium is, still heard from across the room
            audio.rolloffMode = AudioRolloffMode.Linear;
            audio.maxDistance = 400f;
            var podium = root.AddComponent<RacePodium>();
            Set(podium, "sparks", ps);
            Set(podium, "popClip", Clip("Toys/Toy_Gun_v3.wav"));
            Save(root, Traversal + "/RacePodium.prefab");
            return true;
        }

        // ---------- measuring ----------

        /// <summary>
        /// The clear opening of an arch that spans X (cars go through along Z), from its mesh vertices in
        /// <paramref name="space"/>: half the width between the posts (the smallest |x| in the lower half), the height of
        /// the lowest point over the middle, and the posts' middle in Z. Pack meshes are not Read/Write, but the editor can
        /// read them.
        /// </summary>
        private static bool Opening(Transform space, GameObject model, out float half, out float clear, out float midZ)
        {
            half = clear = midZ = 0f;
            var pts = new List<Vector3>();
            foreach (var f in model.GetComponentsInChildren<MeshFilter>())
                if (f.sharedMesh != null)
                    foreach (var v in f.sharedMesh.vertices) pts.Add(space.InverseTransformPoint(f.transform.TransformPoint(v)));
            if (pts.Count == 0) return false;
            float y0 = float.MaxValue, y1 = float.MinValue;
            foreach (var p in pts) { y0 = Mathf.Min(y0, p.y); y1 = Mathf.Max(y1, p.y); }
            float low = y0 + (y1 - y0) * 0.5f, z0 = float.MaxValue, z1 = float.MinValue;
            half = float.MaxValue;
            foreach (var p in pts)
            {
                if (p.y > low) continue;
                half = Mathf.Min(half, Mathf.Abs(p.x));
                z0 = Mathf.Min(z0, p.z); z1 = Mathf.Max(z1, p.z);
            }
            if (half == float.MaxValue || half < 0.01f) return false;
            clear = y1;
            foreach (var p in pts) if (Mathf.Abs(p.x) < half * 0.9f) clear = Mathf.Min(clear, p.y);
            midZ = (z0 + z1) * 0.5f;
            return clear - y0 > 0.01f;
        }

        /// <summary>The TV's +X screen in <paramref name="space"/>, from the faces of its Emission material (the pack's lit
        /// picture) that look along +X: its middle on the outer surface, the way it faces, and its width and height along
        /// its tilt. Falls back to the measured numbers when there are none.</summary>
        private static bool TvScreen(Transform space, GameObject model, out Vector3 centre, out Vector3 normal, out Vector2 size)
        {
            centre = TvScreenCentre; normal = TvScreenNormal.normalized; size = TvScreenSize;
            var pts = new List<Vector3>();
            Vector3 sum = Vector3.zero;
            foreach (var mr in model.GetComponentsInChildren<MeshRenderer>())
            {
                var mf = mr.GetComponent<MeshFilter>();
                var mesh = mf != null ? mf.sharedMesh : null;
                if (mesh == null) continue;
                var mats = mr.sharedMaterials;
                var verts = mesh.vertices;
                for (int sub = 0; sub < mesh.subMeshCount && sub < mats.Length; sub++)
                {
                    if (mats[sub] == null || mats[sub].name != "Emission") continue;
                    var tris = mesh.GetTriangles(sub);
                    for (int i = 0; i + 2 < tris.Length; i += 3)
                    {
                        Vector3 a = space.InverseTransformPoint(mf.transform.TransformPoint(verts[tris[i]]));
                        Vector3 bb = space.InverseTransformPoint(mf.transform.TransformPoint(verts[tris[i + 1]]));
                        Vector3 c = space.InverseTransformPoint(mf.transform.TransformPoint(verts[tris[i + 2]]));
                        Vector3 n = Vector3.Cross(bb - a, c - a);   // the front face's normal, as long as twice its area
                        if (n.sqrMagnitude < 1e-12f || n.normalized.x < 0.7f) continue;
                        sum += n;
                        pts.Add(a); pts.Add(bb); pts.Add(c);
                    }
                }
            }
            if (pts.Count == 0 || sum.sqrMagnitude < 1e-12f) return false;
            normal = sum.normalized;
            Quaternion face = Quaternion.LookRotation(-normal, Vector3.ProjectOnPlane(Vector3.up, normal));
            Vector3 right = face * Vector3.right, up = face * Vector3.up;
            float r0 = float.MaxValue, r1 = float.MinValue, u0 = float.MaxValue, u1 = float.MinValue, d = float.MinValue;
            foreach (var p in pts)
            {
                float pr = Vector3.Dot(p, right), pu = Vector3.Dot(p, up);
                r0 = Mathf.Min(r0, pr); r1 = Mathf.Max(r1, pr);
                u0 = Mathf.Min(u0, pu); u1 = Mathf.Max(u1, pu);
                d = Mathf.Max(d, Vector3.Dot(p, normal));
            }
            centre = right * ((r0 + r1) * 0.5f) + up * ((u0 + u1) * 0.5f) + normal * d;
            size = new Vector2(r1 - r0, u1 - u0);
            return true;
        }

        // ---------- helpers (from WorkshopPieces) ----------

        /// <summary>Scales a holder by <paramref name="k"/>, turns it <paramref name="yaw"/> degrees and moves it so the model's
        /// <paramref name="pivot"/> (holder space, measured at scale 1) lands on its parent's origin.</summary>
        private static void Fit(Transform holder, float k, Vector3 pivot, float yaw)
        {
            holder.localScale = Vector3.one * k;
            holder.localRotation = Quaternion.Euler(0f, yaw, 0f);
            holder.localPosition = -(holder.localRotation * (pivot * k));
        }

        private static TextMeshPro Text(string name, Transform parent, Vector3 position, Quaternion rotation, Vector2 size, float fontSize)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localRotation = rotation;
            var tmp = go.AddComponent<TextMeshPro>();
            tmp.rectTransform.sizeDelta = size;
            tmp.fontSize = fontSize;
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.overflowMode = TextOverflowModes.Overflow;
            tmp.color = Color.white;
            var mr = go.GetComponent<MeshRenderer>();
            if (mr != null) { mr.shadowCastingMode = ShadowCastingMode.Off; mr.receiveShadows = false; }
            return tmp;
        }

        private static GameObject Load(string path)
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (go == null) Debug.LogWarning("[Race] Missing pack prefab, skipped: " + path);
            return go;
        }

        private static AudioClip Clip(string path)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(Sfx + path);
            if (clip == null) Debug.LogWarning("[Race] Missing sound, left empty: " + Sfx + path);
            return clip;
        }

        /// <summary>An instance of a pack prefab under <paramref name="parent"/>. <paramref name="solid"/>: its own colliders
        /// stay on (static scenery keeps its exact shape); otherwise they are switched off.</summary>
        private static GameObject Place(GameObject prefab, Transform parent, bool solid)
        {
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            inst.transform.localPosition = Vector3.zero;
            inst.transform.localRotation = Quaternion.identity;
            if (!solid) foreach (var c in inst.GetComponentsInChildren<Collider>(true)) c.enabled = false;
            foreach (var r in inst.GetComponentsInChildren<Rigidbody>(true)) r.isKinematic = true;
            return inst;
        }

        private static Bounds LocalBounds(Transform space, GameObject go)
        {
            Bounds b = new Bounds(); bool any = false;
            foreach (var r in go.GetComponentsInChildren<Renderer>())
            {
                if (r is ParticleSystemRenderer) continue;
                Bounds rb = RendererBounds(space, r);
                if (!any) { b = rb; any = true; } else b.Encapsulate(rb);
            }
            return any ? b : new Bounds(Vector3.zero, Vector3.one * 0.1f);
        }

        /// <summary>A renderer's mesh box, measured in <paramref name="space"/> (tighter than the world AABB).</summary>
        private static Bounds RendererBounds(Transform space, Renderer r)
        {
            Bounds local; Transform from = r.transform;
            var mf = r is MeshRenderer ? r.GetComponent<MeshFilter>() : null;
            if (mf != null && mf.sharedMesh != null) local = mf.sharedMesh.bounds;
            else if (r is SkinnedMeshRenderer smr) { local = smr.localBounds; if (smr.rootBone != null) from = smr.rootBone; }
            else
            {
                var wb = r.bounds;
                local = new Bounds(r.transform.InverseTransformPoint(wb.center), r.transform.InverseTransformVector(wb.size));
            }
            Vector3 c = local.center, e = local.extents;
            Bounds result = new Bounds(space.InverseTransformPoint(from.TransformPoint(c - e)), Vector3.zero);
            for (int i = 1; i < 8; i++)
            {
                var corner = c + new Vector3((i & 1) != 0 ? e.x : -e.x, (i & 2) != 0 ? e.y : -e.y, (i & 4) != 0 ? e.z : -e.z);
                result.Encapsulate(space.InverseTransformPoint(from.TransformPoint(corner)));
            }
            return result;
        }

        private static GameObject Save(GameObject root, string path)
        {
            var saved = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return saved;
        }

        /// <summary>Near-black, a little glossy, for the TV's screens and the gates' number boards: a copy of our plain lit
        /// material (BuildShape), recoloured. No shader lookups.</summary>
        private static Material DarkMaterial()
        {
            const string path = Data + "/RaceScreen.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null) return mat;
            if (!AssetDatabase.CopyAsset(Data + "/BuildShape.mat", path)) { Debug.LogWarning("[Race] Could not copy BuildShape.mat for the screens."); return null; }
            mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            mat.SetColor("_BaseColor", new Color(0.06f, 0.07f, 0.09f));
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.6f);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int slash = path.LastIndexOf('/');
            string parent = path.Substring(0, slash);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, path.Substring(slash + 1));
        }

        /// <summary>Sets a serialized (private) field by name.</summary>
        private static void Set(Object target, string field, object value)
        {
            var so = new SerializedObject(target);
            var p = so.FindProperty(field);
            if (p == null) { Debug.LogError($"[Race] {target.GetType().Name} has no field '{field}'."); return; }
            switch (value)
            {
                case float f: p.floatValue = f; break;
                case int i: p.intValue = i; break;
                case bool b: p.boolValue = b; break;
                case Object o: p.objectReferenceValue = o; break;
                case null: p.objectReferenceValue = null; break;
                default: Debug.LogError($"[Race] Unsupported value for '{field}'."); return;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetArray(Object target, string field, List<Object> values)
        {
            var so = new SerializedObject(target);
            var p = so.FindProperty(field);
            if (p == null || !p.isArray) { Debug.LogError($"[Race] {target.GetType().Name} has no array '{field}'."); return; }
            p.arraySize = values.Count;
            for (int i = 0; i < values.Count; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
#endif
