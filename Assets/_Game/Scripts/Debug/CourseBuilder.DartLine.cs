#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ShiftJam
{
    /// <summary>
    /// The Dart Line (D-0109): the device, its stand in the armory, and the places it holds.
    /// - Prefabs/Weapons/DartLine: the Dart Pistol with its blaster swapped for a DartLineWeapon (every hold number the
    ///   pistol was fitted with carried over), a reel under the barrel; Pieces/Weapons/DartLinePickup: its stand.
    /// - The armory: the stand at the rack's north end, a practice shelf on the west wall with a pad under its lip, a sign.
    /// - Pull-ups: up the playroom wardrobe; onto the bedroom bed; downstairs, up through the hole under the pencil bridge
    ///   to the hallway floor (the way back up from the living room).
    /// - A zip line from the wardrobe's top across the playroom to the desk.
    /// Toy units throughout (the toy is 1.8 tall). Idempotent: everything under a "DartLine" object per room.
    /// Menu: Shift Jam / House / Land Dart Line (D-0109).
    /// </summary>
    public static partial class CourseBuilder
    {
        const string DartPistolPrefab = "Assets/_Game/Prefabs/Weapons/DartPistol.prefab";
        const string DartLinePrefab = "Assets/_Game/Prefabs/Weapons/DartLine.prefab";
        const string DartPistolStand = "Assets/_Game/Prefabs/Pieces/Weapons/DartPistolPickup.prefab";
        const string DartLineStand = "Assets/_Game/Prefabs/Pieces/Weapons/DartLinePickup.prefab";

        [MenuItem("Shift Jam/House/Land Dart Line (D-0109)")]
        public static void LandDartLine()
        {
            Log.Clear();
            var lineMat = PlainMat("DartLine_Line", new Color(1f, 0.55f, 0.15f), true);
            var stringMat = PlainMat("DartLine_String", new Color(0.95f, 0.92f, 0.82f), true);
            var padMat = PlainMat("DartLine_Pad", new Color(0.86f, 0.16f, 0.14f), false);
            var weapon = MakeDartLine(lineMat);
            var stand = MakeDartLineStand(weapon);
            if (weapon == null || stand == null) { Debug.LogError("D-0109: " + Log); return; }

            var active = SceneManager.GetActiveScene();
            var scene = active.path == ScenePath ? active : EditorSceneManager.OpenScene(ScenePath);
            var r4 = GameObject.Find("Room/Course/R4_Playroom"); var bed = GameObject.Find("Room/Bedroom"); var down = GameObject.Find("Room/Course/Downstairs");
            if (r4 == null) { Debug.LogError("D-0109: no R4_Playroom"); return; }

            // ---- the playroom: the stand, the practice shelf, the wardrobe, the zip line to the desk
            var root = Fresh(r4.transform, "DartLine");
            var armory = r4.transform.Find("Hub_Armory");
            var s = (GameObject)PrefabUtility.InstantiatePrefab(stand, armory != null ? armory : root);
            s.name = "Stand_DartLine";
            s.transform.SetPositionAndRotation(new Vector3(289f, 0f, -251f), Quaternion.LookRotation(Vector3.right));
            var wood = Mat("Assets/_Game/Data/Wardrobe_Wood.mat", new Color(0.36f, 0.23f, 0.13f));
            Cube("PracticeShelf", root, new Vector3(289f, 14f, -242f), new Vector3(8f, 1f, 8f), wood);
            Anchor("Pull_PracticeShelf", root, new Vector3(293.15f, 13f, -242f), Vector3.right, padMat, new Vector3(289f, 15.2f, -242f), new Vector3(295.5f, 15.8f, -242f));
            TapeSign("Sign_DartLine", root, new Vector3(285.4f, 21f, -242f), Vector3.right, "DART LINE  (toy only)\nAim at a red pad, click.\nClick or Jump lets go.", 9f, 3.4f);
            Anchor("Pull_Wardrobe", root, new Vector3(633.9f, 108f, -265f), Vector3.left, padMat, new Vector3(642f, 113.6f, -265f), new Vector3(630f, 116.5f, -265f));
            Zip("Zip_WardrobeToDesk", root, new Vector3(646f, 121f, -268f), new Vector3(374f, 41f, -4f), padMat, stringMat);

            // ---- the bedroom: onto the bed from its foot side
            if (bed != null)
            {
                var broot = Fresh(bed.transform, "DartLine");
                Anchor("Pull_Bed", broot, new Vector3(98f, 37f, -54.9f), Vector3.back, padMat, new Vector3(98f, 41f, -46f), new Vector3(98f, 42.5f, -57.5f));
            }
            else Log.AppendLine("no Room/Bedroom: no bed pull-up");

            // ---- downstairs: up through the hole to the hallway floor past the bridge (through the boost ring's middle,
            //      beside the pencils, over the pit's east edge)
            if (down != null)
            {
                var droot = Fresh(down.transform, "DartLine");
                Anchor("Pull_UpThroughTheHole", droot, new Vector3(189f, -29.5f, -146f), Vector3.down, padMat, new Vector3(213f, 0.3f, -146f),
                    new Vector3(189f, -40f, -150f), new Vector3(189f, -20f, -146f), new Vector3(189f, 1.5f, -146f));
            }
            else Log.AppendLine("no Downstairs: no way up through the hole (run Land Downstairs first)");

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("D-0109 DART LINE LANDED\n" + Log);
        }

        /// <summary>A plain material in Data (Unlit for the line and the string, Lit for the pads).</summary>
        static Material PlainMat(string name, Color c, bool unlit)
        {
            string path = "Assets/_Game/Data/" + name + ".mat";
            var shader = Shader.Find(unlit ? "Universal Render Pipeline/Unlit" : "Universal Render Pipeline/Lit");
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(shader) { name = name }; AssetDatabase.CreateAsset(m, path); }
            m.shader = shader; m.SetColor("_BaseColor", c);
            EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>The Dart Pistol, its blaster swapped for the line (its hold numbers kept), a reel under the barrel.</summary>
        static DartLineWeapon MakeDartLine(Material lineMat)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(DartPistolPrefab) == null) { Log.AppendLine("no " + DartPistolPrefab); return null; }
            if (AssetDatabase.LoadAssetAtPath<GameObject>(DartLinePrefab) != null) AssetDatabase.DeleteAsset(DartLinePrefab);
            AssetDatabase.CopyAsset(DartPistolPrefab, DartLinePrefab);
            var root = PrefabUtility.LoadPrefabContents(DartLinePrefab);
            try
            {
                root.name = "DartLine";
                var blaster = root.GetComponent<BlasterWeapon>();
                string held = blaster != null ? EditorJsonUtility.ToJson(blaster) : null;
                if (blaster != null) Object.DestroyImmediate(blaster, true);
                var line = root.AddComponent<DartLineWeapon>();
                if (held != null) EditorJsonUtility.FromJsonOverwrite(held, line);   // the Weapon fields: the hold, the view, the icon
                Set(line, "displayName", "Dart Line");
                Set(line, "lineMaterial", lineMat);
                var icon = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/_Game/Art/Thumbs/Pieces_Weapons_DartPistolPickup.png");
                if (icon != null) Set(line, "icon", icon);
                foreach (var (prop, clip) in new[] { ("fireClip", "Toy_Dart_Fire"), ("latchClip", "Impact_Dull_"), ("missClip", "Impact_Dull_"), ("letGoClip", "Toy_Dart_Fire") })
                {
                    var g = AssetDatabase.FindAssets(clip + " t:AudioClip");
                    if (g.Length > 0) Set(line, prop, AssetDatabase.LoadAssetAtPath<AudioClip>(AssetDatabase.GUIDToAssetPath(g[0])));
                }
                // the reel: an orange drum under the barrel, in front of the grip
                var reel = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                reel.name = "Reel"; Object.DestroyImmediate(reel.GetComponent<Collider>());
                reel.transform.SetParent(root.transform, false);
                reel.transform.localPosition = new Vector3(0f, -0.035f, 0.07f);
                reel.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                reel.transform.localScale = new Vector3(0.045f, 0.012f, 0.045f);
                reel.GetComponent<Renderer>().sharedMaterial = lineMat;
                PrefabUtility.SaveAsPrefabAsset(root, DartLinePrefab);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            var saved = AssetDatabase.LoadAssetAtPath<GameObject>(DartLinePrefab);
            Log.AppendLine("Dart Line: " + DartLinePrefab);
            return saved != null ? saved.GetComponent<DartLineWeapon>() : null;
        }

        /// <summary>The pistol's stand, handing out the Dart Line.</summary>
        static GameObject MakeDartLineStand(DartLineWeapon weapon)
        {
            if (weapon == null || AssetDatabase.LoadAssetAtPath<GameObject>(DartPistolStand) == null) { Log.AppendLine("no " + DartPistolStand); return null; }
            if (AssetDatabase.LoadAssetAtPath<GameObject>(DartLineStand) != null) AssetDatabase.DeleteAsset(DartLineStand);
            AssetDatabase.CopyAsset(DartPistolStand, DartLineStand);
            var root = PrefabUtility.LoadPrefabContents(DartLineStand);
            try
            {
                root.name = "DartLinePickup";
                var pick = root.GetComponentInChildren<WeaponPickup>(true);
                if (pick != null) Set(pick, "weapon", weapon);
                PrefabUtility.SaveAsPrefabAsset(root, DartLineStand);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            Log.AppendLine("stand: " + DartLineStand);
            return AssetDatabase.LoadAssetAtPath<GameObject>(DartLineStand);
        }

        /// <summary>A red suction-cup pad facing <paramref name="faces"/>, with its glow, its landing and its way there.</summary>
        static HookAnchor Anchor(string name, Transform parent, Vector3 at, Vector3 faces, Material padMat, Vector3 land, params Vector3[] via)
        {
            var go = new GameObject(name); go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(at, Quaternion.LookRotation(faces));
            Pad(go.transform, at, faces, padMat);
            var glow = Glow(go.transform, at + faces * 0.3f);
            var a = go.AddComponent<HookAnchor>();
            var landT = new GameObject("Land").transform; landT.SetParent(go.transform, false); landT.position = land;
            var vias = new List<Transform>();
            for (int i = 0; i < via.Length; i++) { var v = new GameObject("Via" + (i + 1)).transform; v.SetParent(go.transform, false); v.position = via[i]; vias.Add(v); }
            Set(a, "land", landT); SetArray(a, "via", vias.ToArray()); Set(a, "glow", glow);
            Log.AppendLine($"pull-up {name} at {at}, lands at {land} via {via.Length}");
            return a;
        }

        static void Pad(Transform parent, Vector3 at, Vector3 faces, Material padMat)
        {
            var pad = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pad.name = "Pad"; Object.DestroyImmediate(pad.GetComponent<Collider>());
            pad.transform.SetParent(parent, false);
            pad.transform.position = at;
            pad.transform.rotation = Quaternion.FromToRotation(Vector3.up, faces);
            pad.transform.localScale = new Vector3(2.4f, 0.15f, 2.4f);
            pad.GetComponent<Renderer>().sharedMaterial = padMat;
        }

        static Transform Glow(Transform parent, Vector3 at)
        {
            var glowMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Game/Data/OrbGlow.mat");
            var q = Quad("Glow", parent, at, Vector3.zero, Vector3.one * 4.5f, glowMat);
            q.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            q.AddComponent<Billboard>();
            return q.transform;
        }

        /// <summary>A zip line from <paramref name="from"/> down to <paramref name="to"/>, its start pad hanging where the
        /// string begins.</summary>
        static void Zip(string name, Transform parent, Vector3 from, Vector3 to, Material padMat, Material stringMat)
        {
            var go = new GameObject(name); go.transform.SetParent(parent, false);
            var a = new GameObject("Start").transform; a.SetParent(go.transform, false); a.position = from;
            var b = new GameObject("End").transform; b.SetParent(go.transform, false); b.position = to;
            var lr = go.AddComponent<LineRenderer>();
            lr.sharedMaterial = stringMat; lr.startWidth = lr.endWidth = 0.18f; lr.numCapVertices = 2;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var zip = go.AddComponent<ZipLine>();
            Set(zip, "start", a); Set(zip, "end", b);
            zip.Draw();
            // a post at each end the string is tied to (thin dowels)
            Physics.SyncTransforms();
            foreach (var (end, nm) in new[] { (from, "PostStart"), (to, "PostEnd") })
            {
                float floor = Physics.Raycast(end, Vector3.down, out RaycastHit hit, 60f, ~0, QueryTriggerInteraction.Ignore) ? hit.point.y : end.y - 4f;
                float h = Mathf.Max(0.5f, end.y - floor);
                var post = Cube(nm, go.transform, new Vector3(end.x, floor + h / 2f, end.z), new Vector3(0.5f, h, 0.5f), Mat("Assets/_Game/Data/Wardrobe_Wood.mat", new Color(0.36f, 0.23f, 0.13f)));
                Object.DestroyImmediate(post.GetComponent<Collider>());   // a dowel, not a wall: the toy rides past it
            }
            var anchorGo = new GameObject("StartPad"); anchorGo.transform.SetParent(go.transform, false); anchorGo.transform.position = from + Vector3.down * 0.5f;
            Vector3 back = (from - to); back.y = 0f; back = back.sqrMagnitude > 1e-4f ? back.normalized : Vector3.back;
            Pad(anchorGo.transform, anchorGo.transform.position, -back, padMat);
            var glow = Glow(anchorGo.transform, anchorGo.transform.position);
            var anchor = anchorGo.AddComponent<HookAnchor>();
            Set(anchor, "kind", (int)HookAnchor.Kind.ZipStart); Set(anchor, "zip", zip); Set(anchor, "glow", glow);
            Log.AppendLine($"zip {name}: {from} -> {to}, {Vector3.Distance(from, to):F0} long");
        }
    }
}
#endif
