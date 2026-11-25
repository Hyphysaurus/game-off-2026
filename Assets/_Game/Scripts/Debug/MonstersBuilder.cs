#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace ShiftJam
{
    /// <summary>
    /// The monsters under the bed (D-0222) and the claw machine's prizes (D-0223), placed into the OPEN Greybox; it never
    /// opens or saves a scene.
    /// 1. Resources/MonsterSettings (made if missing; an existing asset keeps its numbers, a missing kind is added, empty
    ///    sounds are filled with the Playroom's Toy_Squeak_Single_v1 and Ball_Bounce_v1).
    /// 2. A prefab VARIANT of each pack monster in use under Prefabs/Monsters (the git-ignored Dungeon Mason prefab is the
    ///    base and is only referenced, never copied): a CharacterController sized to its baked body, a Damageable, the
    ///    DarkMonster and its eye glints (small unlit spheres on the body's front straight ahead of its eye bones, riding
    ///    those bones). A second run rewrites them.
    /// 3. The cave's monsters under Room/Bedroom/BedMonsters: the cave under the bed measured by raycasts (the bed frame's
    ///    real underside over a grid), the way from the cave's mouth (where the line from the toy box to the bed's
    ///    flashlight goes under the bed) to BedFlashlight, and each spot from MonsterSettings > Cave Spots checked: free floor,
    ///    room overhead, its leash out of the way and clear of the flashlight.
    /// 4. The prizes under Room/Bedroom/ClawPrizes (NOT under ClawMachine, so Land Start Room Toys can re-make the machine
    ///    without taking them): the glass case found again from the machine's glass material, its prize floor by raycasts
    ///    (the chute's hole left out), each prize scaled to read as a plush and kept wholly inside the glass.
    /// Menu: Shift Jam / House / Land Bedroom Monsters (D-0222, D-0223). A second run replaces what the first placed. The
    /// scene is marked changed and not saved (Ctrl+S keeps it).
    /// </summary>
    public static class MonstersBuilder
    {
        const string Tag = "[Bedroom Monsters]";
        const string ScenePath = "Assets/_Game/Scenes/Greybox.unity";
        const string SettingsFolder = "Assets/_Game/Resources";
        const string SettingsPath = SettingsFolder + "/" + MonsterSettings.ResourceName + ".asset";
        const string PrefabFolder = "Assets/_Game/Prefabs/Monsters";
        const string GlowFolder = "Assets/_Game/Art/Monsters";
        const string GlowPath = GlowFolder + "/MonsterEyeGlow.mat";
        const string SqueakPath = "Assets/_Imports/ithappy/Playroom/Playroom_Sound_FX/Toys/Toy_Squeak_Single_v1.wav";
        const string BumpPath = "Assets/_Imports/ithappy/Playroom/Playroom_Sound_FX/Toys/Ball_Bounce_v1.wav";
        const string CaveHolder = "BedMonsters";
        const string PrizeHolder = "ClawPrizes";

        // The cave.
        static readonly Vector3 ToyStartFallback = new Vector3(0f, 0f, -20f);   // the toy's start in the toy box (no Room/ToyBox found)
        const float CaveGridStep = 3f;        // toy units between the clearance rays under the bed
        const float CaveLeastRoom = 3f;       // a grid point with less room overhead than this is not cave floor
        const float CaveInset = 1.5f;         // the cave box the monsters keep inside, in from the measured floor's edge
        const float CaveBackInset = 3f;       // the cave's back point, in from that box's edge
        const float ToyRadius = 0.5f, ToyTall = 1.8f;   // the toy's capsule, for the way's check and the flashlight's room
        const float HeadRoom = 0.5f;          // room kept over a monster's head at its spot
        const float SpotPush = 2f;            // a spot that is not free is tried this much further out, twice, then on the other side
        const float SpotsApart = 2f;          // least gap between two monsters' bodies at their spots

        // The prizes.
        const int PrizeGrid = 14;             // floor rays across the glass case each way
        const int PrizeRing = 8;              // floor rays round a prize's footprint
        const float PrizeShrink = 0.85f;      // fewer than Prizes At Least fit: all a little smaller, and again
        const int PrizeTries = 3;
        const int PrizesAtLeast = 6;

        static readonly StringBuilder Log = new StringBuilder();

        /// <summary>What the maker made of one kind: its prefab variant and its body measured at scale 1 (root space).</summary>
        sealed class Made
        {
            public GameObject prefab;
            public Bounds body;
            public float radius;
            public float Tall => body.size.y;
            public float Wide => Mathf.Max(body.size.x, body.size.z);
        }

        [MenuItem("Shift Jam/House/Land Bedroom Monsters (D-0222, D-0223)")]
        public static void LandMenu()
        {
            string report = Land();
            if (report.StartsWith(Tag + " NOT ")) Debug.LogWarning(report); else Debug.Log(report);
        }

        /// <summary>
        /// Makes the settings, the prefab variants, the cave's monsters and the claw machine's prizes in the OPEN Greybox and
        /// marks it changed; it does not save it. A second run replaces BedMonsters and ClawPrizes and rewrites the prefab
        /// variants (MonsterSettings keeps its numbers). Returns the report, one fact a line; a report that starts
        /// "[Bedroom Monsters] NOT " means nothing was changed.
        /// </summary>
        public static string Land()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return $"{Tag} NOT landed: this does not run in Play. Stop the game first. Nothing was changed.";
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded || scene.path != ScenePath)
                return $"{Tag} NOT landed: the active scene is '{scene.path}', and the bedroom is in {ScenePath}. Open Greybox and run this again (it never opens a scene by itself). Nothing was changed.";
            GameObject bedroomGo = GameObject.Find("Room/Bedroom");
            if (bedroomGo == null) return $"{Tag} NOT landed: no Room/Bedroom in {ScenePath}. Nothing was changed.";
            Shader unlit = Shader.Find("Universal Render Pipeline/Unlit");
            if (unlit == null) return $"{Tag} NOT landed: no 'Universal Render Pipeline/Unlit' shader for the eye glints. Nothing was changed.";
            Transform bedroom = bedroomGo.transform;

            Log.Clear();
            bool again = bedroom.Find(CaveHolder) != null || bedroom.Find(PrizeHolder) != null;
            Log.AppendLine($"{Tag} Land Bedroom Monsters (D-0222 the monsters under the bed, D-0223 the claw machine's prizes): {(again ? "SECOND run: BedMonsters and ClawPrizes are replaced and the prefab variants rewritten (MonsterSettings keeps its numbers)" : "first run")}. Greybox is MARKED CHANGED and NOT SAVED: Ctrl+S keeps it.");

            // What a first run placed goes first: its prefabs are rewritten below.
            Remove(bedroom, CaveHolder);
            Remove(bedroom, PrizeHolder);

            MonsterSettings s = Settings();
            Material glow = GlowMaterial(unlit, s);

            var made = new Dictionary<string, Made>();
            var wanted = new HashSet<string>();
            if (s.caveSpots != null) foreach (var spot in s.caveSpots) wanted.Add(spot.kind);
            if (s.prizes != null) foreach (string p in s.prizes) wanted.Add(p);
            EnsureFolder(PrefabFolder);
            foreach (MonsterSettings.Kind kind in s.kinds)
            {
                if (kind == null || !wanted.Contains(kind.name) || made.ContainsKey(kind.name)) continue;
                Made m = MakeVariant(kind, s, glow);
                if (m != null) made[kind.name] = m;
            }
            foreach (string w in wanted) if (!s.Has(w)) Log.AppendLine($"KIND '{w}': named in Cave Spots or Prizes but NOT in MonsterSettings > Kinds: skipped.");
            AssetDatabase.SaveAssets();

            RoomLights lights = bedroom.GetComponentInChildren<RoomLights>(true);
            StarLamp lamp = bedroom.GetComponentInChildren<StarLamp>(true);
            Log.AppendLine($"THE ROOM'S LIGHTS: wall switch {(lights != null ? Path(lights.transform) + " (RoomLights, saved '" + lights.Key + "')" : "NO RoomLights under Room/Bedroom: that rule is off")}; star lamp {(lamp != null ? Path(lamp.transform) + " (StarLamp, saved '" + lamp.Key + "')" : "NO StarLamp under Room/Bedroom: that rule is off")}. Either one lit = every monster and prize is a frozen toy.");

            Physics.SyncTransforms();
            LandCave(bedroom, s, made, lights, lamp);
            LandPrizes(bedroom, s, made, lights, lamp);

            EditorSceneManager.MarkSceneDirty(scene);
            Log.AppendLine("Greybox MARKED CHANGED, not saved. Online: nothing networked; each PC's monsters react to that PC's own player (MonsterSettings > Online off keeps them still toys online). Off switch: MonsterSettings > On.");
            return Log.ToString();
        }

        // ------------------------------------------------------------------------------------------------ the settings

        static MonsterSettings Settings()
        {
            var s = AssetDatabase.LoadAssetAtPath<MonsterSettings>(SettingsPath);
            bool fresh = s == null;
            if (fresh)
            {
                EnsureFolder(SettingsFolder);
                s = ScriptableObject.CreateInstance<MonsterSettings>();
                AssetDatabase.CreateAsset(s, SettingsPath);
            }
            var kinds = new List<MonsterSettings.Kind>();
            if (s.kinds != null) foreach (var k in s.kinds) if (k != null) kinds.Add(k);
            var added = new List<string>();
            foreach (var d in MonsterSettings.DefaultKinds()) if (!s.Has(d.name)) { kinds.Add(d); added.Add(d.name); }
            s.kinds = kinds.ToArray();
            var squeak = AssetDatabase.LoadAssetAtPath<AudioClip>(SqueakPath);
            var bump = AssetDatabase.LoadAssetAtPath<AudioClip>(BumpPath);
            if (s.squeak.clip == null) s.squeak.clip = squeak;
            if (s.bump.clip == null) s.bump.clip = bump;
            EditorUtility.SetDirty(s);
            AssetDatabase.SaveAssetIfDirty(s);
            Log.AppendLine($"SETTINGS: {SettingsPath} {(fresh ? "MADE with its defaults" : "FOUND: its numbers kept")}{(added.Count > 0 && !fresh ? "; kinds added: " + string.Join(", ", added) : "")}. {s.kinds.Length} kinds. Sight {s.sight}, leash {s.leash}, creep {s.creepSpeed} / run {s.runSpeed} a second, bump push {s.bumpPush} + lift {s.bumpLift} (the toy's units a second; no damage), at most {s.bumpsAtMost} bumps in {s.bumpWindow} s, cooldown {s.bumpCooldown} s, flee {s.fleeDistance}, back home {s.returnAfter} s after the light leaves, health {s.health}, down {s.downSeconds} s.");
            Log.AppendLine($"SOUNDS: squeak (the flinch) {Clip(s.squeak.clip, SqueakPath)} at {s.squeak.volume}; bump {Clip(s.bump.clip, BumpPath)} at {s.bump.volume}; knocked down: the soldiers' thud (Resources/WeaponAmmo, through KnockDown). Heard from the monster: full within {s.soundNear}, silent past {s.soundFar}.");
            return s;
        }

        static string Clip(AudioClip clip, string expected) => clip != null ? $"{clip.name} ({clip.length:F2} s)" : $"NONE (no {expected}: silent)";

        static Material GlowMaterial(Shader unlit, MonsterSettings s)
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(GlowPath);
            bool fresh = m == null;
            if (fresh)
            {
                EnsureFolder(GlowFolder);
                m = new Material(unlit) { name = "MonsterEyeGlow" };
                AssetDatabase.CreateAsset(m, GlowPath);
            }
            else if (m.shader != unlit) m.shader = unlit;
            m.SetColor("_BaseColor", s.glint);
            EditorUtility.SetDirty(m);
            AssetDatabase.SaveAssetIfDirty(m);
            Log.AppendLine($"EYES: {GlowPath} {(fresh ? "MADE" : "found")} (URP Unlit, colour {s.glint}: over 1 glows under bloom; not lit by anything, so a Dark Zone does not dim it).");
            return m;
        }

        // ------------------------------------------------------------------------------------------------ the prefab variants

        /// <summary>One kind's prefab variant: the pack prefab as its base, our collider, health, monster and eyes on top.</summary>
        static Made MakeVariant(MonsterSettings.Kind kind, MonsterSettings s, Material glow)
        {
            var pack = AssetDatabase.LoadAssetAtPath<GameObject>(kind.packPrefab);
            if (pack == null) { Log.AppendLine($"KIND {kind.name}: NO {kind.packPrefab} (the Dungeon Mason packs are git-ignored: import them on this PC). Not made; its spots and prizes are skipped."); return null; }
            string path = $"{PrefabFolder}/{kind.name}Monster.prefab";
            var temps = new List<Object>();
            var shells = new List<Collider>();
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(pack, SceneManager.GetActiveScene());
            try
            {
                inst.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                inst.transform.localScale = Vector3.one;
                Physics.SyncTransforms();
                Bounds body = Body(inst.transform, temps, shells);
                float tall = Mathf.Max(0.05f, body.size.y);
                float radius = Mathf.Clamp(0.5f * Mathf.Min(body.size.x, body.size.z), 0.05f, 0.5f * tall);

                var cc = inst.AddComponent<CharacterController>();
                cc.height = tall; cc.radius = radius; cc.center = body.center;
                cc.skinWidth = Mathf.Max(0.01f, 0.08f * radius);
                cc.stepOffset = 0.25f * tall; cc.slopeLimit = 45f; cc.minMoveDistance = 0f;

                var health = inst.AddComponent<Damageable>();
                var hso = new SerializedObject(health);
                hso.FindProperty("maxHealth").floatValue = s.health;
                hso.FindProperty("takeKnockback").boolValue = false;   // nothing of ours takes a shove: no body to push
                hso.ApplyModifiedPropertiesWithoutUndo();

                List<Renderer> eyes = Eyes(inst.transform, kind, body, glow, shells, out string eyeLine);
                Animator animator = inst.GetComponentInChildren<Animator>(true);

                var monster = inst.AddComponent<DarkMonster>();
                var mso = new SerializedObject(monster);
                mso.FindProperty("kind").stringValue = kind.name;
                mso.FindProperty("animator").objectReferenceValue = animator;
                var list = mso.FindProperty("glints");
                list.arraySize = eyes.Count;
                for (int i = 0; i < eyes.Count; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = eyes[i];
                mso.ApplyModifiedPropertiesWithoutUndo();

                string states = States(animator, kind);
                foreach (Object t in temps) if (t != null) Object.DestroyImmediate(t);
                temps.Clear();

                GameObject saved = PrefabUtility.SaveAsPrefabAsset(inst, path, out bool ok);
                if (!ok || saved == null) { Log.AppendLine($"KIND {kind.name}: COULD NOT SAVE {path}. Its spots and prizes are skipped."); return null; }
                Log.AppendLine($"KIND {kind.name}: {path} ({PrefabUtility.GetPrefabAssetType(saved)} of {kind.packPrefab}): body {body.size.x:F2} x {body.size.y:F2} x {body.size.z:F2} at scale 1 (a toy is about 2 tall); CharacterController height {tall:F2}, radius {radius:F2}, centre {body.center}; Damageable {s.health} (MonsterSettings > Health sets it at run time); {eyeLine}; Animator {(animator != null && animator.runtimeAnimatorController != null ? animator.runtimeAnimatorController.name : "NONE")}: {states}.");
                return new Made { prefab = saved, body = body, radius = radius };
            }
            finally
            {
                foreach (Object t in temps) if (t != null) Object.DestroyImmediate(t);
                if (inst != null) Object.DestroyImmediate(inst);
            }
        }

        /// <summary>The body's exact box in the root's own space (the skinned meshes baked as they stand, plus any plain
        /// meshes), and a temporary mesh collider on each part for the eye rays.</summary>
        static Bounds Body(Transform root, List<Object> temps, List<Collider> shells)
        {
            bool any = false;
            var box = new Bounds(Vector3.zero, Vector3.zero);
            foreach (var smr in root.GetComponentsInChildren<SkinnedMeshRenderer>(false))
            {
                if (!smr.enabled || smr.sharedMesh == null) continue;
                var mesh = new Mesh { name = "~MonsterBake" };
                smr.BakeMesh(mesh, true);
                temps.Add(mesh);
                Matrix4x4 world = Matrix4x4.TRS(smr.transform.position, smr.transform.rotation, Vector3.one);
                Take(mesh, world, root, ref box, ref any);
                shells.Add(Shell(mesh, smr.transform.position, smr.transform.rotation, Vector3.one, temps));
            }
            foreach (var mf in root.GetComponentsInChildren<MeshFilter>(false))
            {
                var mr = mf.GetComponent<MeshRenderer>();
                if (mf.sharedMesh == null || mr == null || !mr.enabled) continue;
                Take(mf.sharedMesh, mf.transform.localToWorldMatrix, root, ref box, ref any);
                shells.Add(Shell(mf.sharedMesh, mf.transform.position, mf.transform.rotation, mf.transform.lossyScale, temps));
            }
            if (!any) box = new Bounds(Vector3.up * 0.5f, Vector3.one);
            Physics.SyncTransforms();
            return box;
        }

        static void Take(Mesh mesh, Matrix4x4 world, Transform root, ref Bounds box, ref bool any)
        {
            foreach (Vector3 v in mesh.vertices)
            {
                Vector3 p = root.InverseTransformPoint(world.MultiplyPoint3x4(v));
                if (!any) { box = new Bounds(p, Vector3.zero); any = true; } else box.Encapsulate(p);
            }
        }

        static Collider Shell(Mesh mesh, Vector3 at, Quaternion turn, Vector3 scale, List<Object> temps)
        {
            var go = new GameObject("~MonsterShell") { hideFlags = HideFlags.HideAndDontSave };
            go.transform.SetPositionAndRotation(at, turn);
            go.transform.localScale = scale;
            var mc = go.AddComponent<MeshCollider>();
            mc.sharedMesh = mesh;
            temps.Add(go);
            return mc;
        }

        /// <summary>The eye glints: for each eye bone (one, or a pair Eye Pair Gap apart), a small unlit sphere on the body's
        /// front straight ahead of it, riding the bone. Which way is the front: the side the eye bones are on.</summary>
        static List<Renderer> Eyes(Transform root, MonsterSettings.Kind kind, Bounds body, Material glow, List<Collider> shells, out string line)
        {
            var eyes = new List<Renderer>();
            float tall = Mathf.Max(0.05f, body.size.y);
            float radius = kind.eyeSize * tall;
            var bones = new List<Transform>();
            var missing = new List<string>();
            if (kind.eyeBones != null)
                foreach (string name in kind.eyeBones) { Transform b = FindDeep(root, name); if (b != null) bones.Add(b); else missing.Add(name); }
            float sign = 1f;
            if (bones.Count > 0) sign = root.InverseTransformPoint(bones[0].position).z >= body.center.z ? 1f : -1f;
            Vector3 front = root.forward * sign;
            int hits = 0, misses = 0;
            var anchors = new List<(Transform bone, Vector3 at)>();
            if (bones.Count > 0)
                foreach (Transform b in bones)
                {
                    if (kind.eyePairGap > 0f)
                    {
                        anchors.Add((b, b.position - root.right * (0.5f * kind.eyePairGap * tall)));
                        anchors.Add((b, b.position + root.right * (0.5f * kind.eyePairGap * tall)));
                    }
                    else anchors.Add((b, b.position));
                }
            else
            {
                Vector3 at = root.TransformPoint(new Vector3(body.center.x, body.min.y + kind.eyeHeight * tall, body.center.z));
                if (kind.eyePairGap > 0f)
                {
                    anchors.Add((root, at - root.right * (0.5f * kind.eyePairGap * tall)));
                    anchors.Add((root, at + root.right * (0.5f * kind.eyePairGap * tall)));
                }
                else anchors.Add((root, at));
            }
            foreach (var (bone, at) in anchors)
            {
                var ray = new Ray(at + front * (2f * tall), -front);
                float best = float.MaxValue; Vector3 point = Vector3.zero;
                foreach (Collider c in shells)
                    if (c != null && c.Raycast(ray, out RaycastHit hit, 4f * tall) && hit.distance < best) { best = hit.distance; point = hit.point; }
                bool found = best < float.MaxValue;
                if (found) hits++; else misses++;
                Vector3 centre = found ? point + front * (0.3f * radius) : at + front * (0.15f * tall);
                eyes.Add(Glint(bone, centre, radius, glow));
            }
            line = $"{eyes.Count} eye glint{(eyes.Count == 1 ? "" : "s")} (radius {radius:F3}) "
                + (bones.Count > 0 ? $"on {string.Join(", ", BoneNames(bones))}" : "on the root at Eye Height (no eye bone found)")
                + (missing.Count > 0 ? $" (bones NOT found: {string.Join(", ", missing)})" : "")
                + $", front = the root's {(sign > 0f ? "+Z" : "-Z")}, {hits} on the body's surface{(misses > 0 ? $", {misses} with NO surface found ahead (floating just in front of the bone)" : "")}";
            return eyes;
        }

        static IEnumerable<string> BoneNames(List<Transform> bones) { foreach (Transform b in bones) yield return b.name; }

        static Renderer Glint(Transform parent, Vector3 centre, float radius, Material glow)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "EyeGlint";
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.position = centre;
            go.transform.rotation = parent.rotation;
            Vector3 ls = parent.lossyScale;
            float d = 2f * radius;
            go.transform.localScale = new Vector3(d / Mathf.Max(1e-4f, Mathf.Abs(ls.x)), d / Mathf.Max(1e-4f, Mathf.Abs(ls.y)), d / Mathf.Max(1e-4f, Mathf.Abs(ls.z)));
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = glow;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.lightProbeUsage = LightProbeUsage.Off;
            r.reflectionProbeUsage = ReflectionProbeUsage.Off;
            return r;
        }

        /// <summary>Which of the kind's state names the controller has (a missing one falls back to the idle at run time).</summary>
        static string States(Animator animator, MonsterSettings.Kind kind)
        {
            RuntimeAnimatorController rac = animator != null ? animator.runtimeAnimatorController : null;
            if (rac is AnimatorOverrideController over) rac = over.runtimeAnimatorController;
            var ctrl = rac as UnityEditor.Animations.AnimatorController;
            if (ctrl == null || ctrl.layers.Length == 0) return "NO controller to check the state names against";
            var names = new HashSet<string>();
            Collect(ctrl.layers[0].stateMachine, names);
            var missing = new List<string>();
            foreach (string n in new[] { kind.idle, kind.alert, kind.walk, kind.run, kind.back, kind.attack, kind.hit, kind.hide, kind.die, kind.rest })
                if (!string.IsNullOrEmpty(n) && !names.Contains(n) && !missing.Contains(n)) missing.Add(n);
            return missing.Count == 0 ? "all 10 state names found" : $"state names NOT in it (they fall back to the idle): {string.Join(", ", missing)}";
        }

        static void Collect(UnityEditor.Animations.AnimatorStateMachine machine, HashSet<string> names)
        {
            if (machine == null) return;
            foreach (var st in machine.states) if (st.state != null) names.Add(st.state.name);
            foreach (var sub in machine.stateMachines) Collect(sub.stateMachine, names);
        }

        // ------------------------------------------------------------------------------------------------ the cave (D-0222)

        static void LandCave(Transform bedroom, MonsterSettings s, Dictionary<string, Made> made, RoomLights lights, StarLamp lamp)
        {
            Transform bed = bedroom.Find("Bed");
            Transform torchT = bedroom.Find("UnderBed/BedFlashlight");
            if (torchT == null)
            {
                Transform under = bedroom.Find("UnderBed");
                Flashlight f = under != null ? under.GetComponentInChildren<Flashlight>(true) : null;
                if (f != null) torchT = f.transform;
            }
            if (bed == null || torchT == null) { Log.AppendLine($"CAVE MONSTERS NOT placed: no {(bed == null ? "Room/Bedroom/Bed" : "Room/Bedroom/UnderBed/BedFlashlight")} to measure the cave by."); return; }
            Bounds bb = WorldBounds(bed.gameObject);
            Vector3 torch = torchT.position;
            float floorY = Floor(new Vector3(bb.center.x, 0f, bb.center.z), torch.y + 3f, bed, out bool floorFound);

            // The room under the frame, on a grid.
            var room = new List<float>();
            bool anyCave = false;
            Vector2 caveMin = Vector2.zero, caveMax = Vector2.zero;
            string other = null;
            for (float x = bb.min.x + CaveGridStep * 0.5f; x < bb.max.x; x += CaveGridStep)
                for (float z = bb.min.z + CaveGridStep * 0.5f; z < bb.max.z; z += CaveGridStep)
                {
                    float up = Clearance(new Vector3(x, floorY, z), bed, out _, out string hitOther);
                    if (hitOther != null && other == null) other = $"{hitOther} near ({x:F0}, {z:F0})";
                    if (up < CaveLeastRoom) continue;
                    room.Add(up);
                    var p = new Vector2(x, z);
                    if (!anyCave) { caveMin = caveMax = p; anyCave = true; }
                    else { caveMin = Vector2.Min(caveMin, p); caveMax = Vector2.Max(caveMax, p); }
                }
            if (!anyCave) { Log.AppendLine($"CAVE MONSTERS NOT placed: no room under the bed found (rays up from the floor at y {floorY:F2} over the bed's footprint met the frame lower than {CaveLeastRoom})."); return; }
            room.Sort();
            float least = room[0], most = room[room.Count - 1], middle = room[room.Count / 2];
            Log.AppendLine($"CAVE CLEARANCE (measured: {room.Count} rays up from the floor at y {floorY:F2}{(floorFound ? "" : " (NO floor hit: y 0 assumed)")}, every {CaveGridStep} over the bed's footprint x {bb.min.x:F1}..{bb.max.x:F1}, z {bb.min.z:F1}..{bb.max.z:F1}, that met the bed frame {CaveLeastRoom} or more up): least {least:F2}, middle {middle:F2}, most {most:F2} (D-0155 said 8.6 to 9.8; the dark zone's box is 8.5 tall). Cave floor x {caveMin.x:F1}..{caveMax.x:F1}, z {caveMin.y:F1}..{caveMax.y:F1}.{(other != null ? " Something other than the bed is overhead in places: " + other + "." : "")}");

            var cave = new Bounds(new Vector3((caveMin.x + caveMax.x) * 0.5f, floorY + least * 0.5f, (caveMin.y + caveMax.y) * 0.5f),
                new Vector3(Mathf.Max(1f, caveMax.x - caveMin.x - 2f * CaveInset), least, Mathf.Max(1f, caveMax.y - caveMin.y - 2f * CaveInset)));

            // The cave's mouth: where the line from the toy box to the flashlight goes under the bed.
            GameObject toyBox = GameObject.Find("Room/ToyBox");
            Vector3 start = toyBox != null ? toyBox.transform.position : ToyStartFallback;
            Vector3 aim = Level(torch);
            Vector3 mouth = Entry(Level(start), aim, cave, out bool fromInside);
            mouth.y = floorY; aim.y = floorY;
            Vector3 way = Level(aim - mouth);
            float wayLength = way.magnitude;
            Vector3 dir = wayLength > 1e-3f ? way / wayLength : Vector3.right;
            Vector3 left = Vector3.Cross(Vector3.up, dir);
            string wayClear = WayClear(mouth - dir * 3f, aim, floorY, torchT);
            Vector3 back = new Vector3(cave.center.x, floorY, cave.center.z) + Level(new Vector3(cave.center.x, floorY, cave.center.z) - mouth).normalized * 1000f;
            back.x = Mathf.Clamp(back.x, cave.min.x + CaveBackInset, cave.max.x - CaveBackInset);
            back.z = Mathf.Clamp(back.z, cave.min.z + CaveBackInset, cave.max.z - CaveBackInset);
            Log.AppendLine($"THE WAY IN: from {(toyBox != null ? "Room/ToyBox" : "the toy's start (no Room/ToyBox)")} at {Level(start)} to BedFlashlight at {torch}: the cave's mouth at {mouth}{(fromInside ? " (the start is already under the bed)" : "")}, {wayLength:F1} to the flashlight; a toy's capsule along it: {wayClear}. Kept open: {s.laneHalfWidth} either side of that line (no monster stands in it, nor creeps or backs into it). The cave's back (where they run from a beam): {back}. The box they keep inside: x {cave.min.x:F1}..{cave.max.x:F1}, z {cave.min.z:F1}..{cave.max.z:F1}.");

            var holder = new GameObject(CaveHolder).transform;
            holder.SetParent(bedroom, false);
            holder.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            var placed = new List<(Vector3 at, float radius)>();
            int index = 0;
            foreach (MonsterSettings.CaveSpot spot in s.caveSpots ?? new MonsterSettings.CaveSpot[0])
            {
                index++;
                if (!made.TryGetValue(spot.kind ?? "", out Made m)) { Log.AppendLine($"CAVE SPOT {index} ({spot.kind}): NOT placed (no prefab for that kind)."); continue; }
                float least2 = s.laneHalfWidth + s.leash + m.radius + 0.5f;
                float apart = Mathf.Max(spot.apart, least2);
                string why = null; Vector3 at = Vector3.zero; float roomOver = 0f; bool ok = false;
                var tried = new List<string>();
                float side = spot.side >= 0f ? 1f : -1f;
                for (int sideTry = 0; sideTry < 2 && !ok; sideTry++, side = -side)
                    for (int push = 0; push < 3 && !ok; push++)
                    {
                        at = mouth + dir * (spot.along * wayLength) + left * (side * (apart + push * SpotPush));
                        why = SpotProblem(at, m, s, cave, aim, placed, bed, floorY, out roomOver, out float spotFloor);
                        if (why == null) { ok = true; at.y = spotFloor; }
                        else tried.Add($"({at.x:F1}, {at.z:F1}): {why}");
                    }
                if (!ok) { Log.AppendLine($"CAVE SPOT {index} ({spot.kind}, along {spot.along}, side {spot.side}, apart {spot.apart}): NOT placed: " + string.Join("; ", tried) + ". Change its numbers in MonsterSettings > Cave Spots and run again."); continue; }

                var go = (GameObject)PrefabUtility.InstantiatePrefab(m.prefab, holder);
                go.name = $"{spot.kind}_{index}";
                Vector3 onWay = Closest(mouth, aim, at);
                Vector3 look = Level(onWay - at);
                go.transform.SetPositionAndRotation(new Vector3(at.x, at.y - m.body.min.y + 0.01f, at.z), look.sqrMagnitude > 1e-4f ? Quaternion.LookRotation(look, Vector3.up) : Quaternion.identity);
                go.transform.localScale = Vector3.one;
                var so = new SerializedObject(go.GetComponent<DarkMonster>());
                so.FindProperty("mode").enumValueIndex = (int)DarkMonster.Mode.Roam;
                so.FindProperty("roomLights").objectReferenceValue = lights;
                so.FindProperty("starLamp").objectReferenceValue = lamp;
                so.FindProperty("cave").boundsValue = cave;
                so.FindProperty("caveBack").vector3Value = back;
                so.FindProperty("laneFrom").vector3Value = mouth;
                so.FindProperty("laneTo").vector3Value = aim;
                so.FindProperty("laneHalfWidth").floatValue = s.laneHalfWidth;
                so.ApplyModifiedPropertiesWithoutUndo();
                placed.Add((at, m.radius));
                float toWay = Level(at - onWay).magnitude;
                Log.AppendLine($"CAVE SPOT {index}: {go.name} at ({at.x:F1}, {at.y:F2}, {at.z:F1}){(tried.Count > 0 ? " (tried first: " + string.Join("; ", tried) + ")" : "")}: {Level(at - aim).magnitude:F1} from BedFlashlight, {Level(at - mouth).magnitude:F1} from the cave's mouth, {toWay:F1} from the way's middle line (its leash edge {toWay - s.leash - m.radius:F1} from it, the kept way {s.laneHalfWidth}), {roomOver:F2} of room overhead for a {m.Tall:F2}-tall body; facing the way.");
            }
            Log.AppendLine($"CAVE MONSTERS: {placed.Count} under Room/Bedroom/{CaveHolder} (its own object: a re-run of Land Bedroom Light Beats, which remakes UnderBed, leaves them). Each sees a toy {s.sight} away (in sight), creeps no more than {s.leash} from its spot; the flashlight is at least {s.flashlightClear} clear of every leash edge, so the toy can always walk up and take it.");
            if (placed.Count < 3) Log.AppendLine($"CAVE WARNING: only {placed.Count} placed (3 to 5 wanted): see the spot lines above.");
        }

        /// <summary>Why a monster may not stand at <paramref name="at"/> (null when it may): too near the cave's edge, the
        /// way or the flashlight, another monster, no room overhead, no floor, or something solid there.</summary>
        static string SpotProblem(Vector3 at, Made m, MonsterSettings s, Bounds cave, Vector3 torch, List<(Vector3 at, float radius)> placed, Transform bed, float floorY, out float roomOver, out float spotFloor)
        {
            roomOver = 0f; spotFloor = floorY;
            float edge = s.leash + m.radius;
            if (at.x < cave.min.x + edge || at.x > cave.max.x - edge || at.z < cave.min.z + edge || at.z > cave.max.z - edge)
                return $"its leash would reach out from under the bed (keep {edge:F1} in from the cave's box)";
            float needTorch = s.leash + m.radius + s.bumpGap + ToyRadius + s.flashlightClear;
            float toTorch = Level(at - torch).magnitude;
            if (toTorch < needTorch) return $"{toTorch:F1} from the flashlight (wants {needTorch:F1})";
            foreach (var (other, r) in placed)
            {
                float gap = Level(at - other).magnitude - r - m.radius;
                if (gap < SpotsApart) return $"{gap:F1} from another monster";
            }
            spotFloor = Floor(at, floorY + 3f, bed, out bool found);
            if (!found || Mathf.Abs(spotFloor - floorY) > 0.5f) return found ? $"its floor is at y {spotFloor:F2}, not the cave's {floorY:F2}" : "no floor under it";
            Clearance(new Vector3(at.x, spotFloor, at.z), bed, out roomOver, out string over);
            if (roomOver < m.Tall + HeadRoom) return $"{roomOver:F2} of room overhead for {m.Tall:F2}{(over != null ? " (under " + over + ")" : "")}";
            Vector3 low = new Vector3(at.x, spotFloor + m.radius + 0.05f, at.z);
            Vector3 high = new Vector3(at.x, spotFloor + Mathf.Max(m.radius + 0.06f, m.Tall - m.radius), at.z);
            foreach (Collider c in Physics.OverlapCapsule(low, high, m.radius, ~0, QueryTriggerInteraction.Ignore))
                if (c != null) return $"{c.name} stands there";
            return null;
        }

        /// <summary>Where the level line from <paramref name="from"/> to <paramref name="to"/> first enters the box (seen from
        /// above). The start itself when it is already inside; the end when the line never enters.</summary>
        static Vector3 Entry(Vector3 from, Vector3 to, Bounds box, out bool inside)
        {
            inside = from.x >= box.min.x && from.x <= box.max.x && from.z >= box.min.z && from.z <= box.max.z;
            if (inside) return from;
            Vector3 d = to - from;
            float enter = 0f, leave = 1f;
            if (!Slab(from.x, d.x, box.min.x, box.max.x, ref enter, ref leave) || !Slab(from.z, d.z, box.min.z, box.max.z, ref enter, ref leave)) return to;
            return from + d * enter;
        }

        static bool Slab(float start, float delta, float min, float max, ref float enter, ref float leave)
        {
            if (Mathf.Abs(delta) < 1e-6f) return start >= min && start <= max;
            float a = (min - start) / delta, b = (max - start) / delta;
            if (a > b) (a, b) = (b, a);
            enter = Mathf.Max(enter, a); leave = Mathf.Min(leave, b);
            return enter <= leave;
        }

        /// <summary>A toy's capsule swept along the way, a little off the floor: "clear", or what stops it and where.</summary>
        static string WayClear(Vector3 from, Vector3 to, float floorY, Transform torch)
        {
            Vector3 way = Level(to - from);
            float length = way.magnitude;
            if (length < 1e-3f) return "too short to test";
            Vector3 low = new Vector3(from.x, floorY + ToyRadius + 0.3f, from.z), high = new Vector3(from.x, floorY + ToyTall - ToyRadius, from.z);
            RaycastHit[] hits = Physics.CapsuleCastAll(low, high, ToyRadius, way / length, length, ~0, QueryTriggerInteraction.Ignore);
            float nearest = float.MaxValue; string what = null;
            foreach (RaycastHit h in hits)
            {
                if (h.collider.transform.IsChildOf(torch)) continue;
                if (h.distance < nearest) { nearest = h.distance; what = h.collider.name; }
            }
            return what == null ? "clear all the way to the flashlight" : $"STOPPED by {what} {nearest:F1} along (the toy has to step round it)";
        }

        /// <summary>The floor's height under <paramref name="at"/>: the first up-facing hit going down from <paramref name="fromY"/>
        /// (under the bed frame), not a monster nor anything with a body. y of <paramref name="at"/> when nothing is found.</summary>
        static float Floor(Vector3 at, float fromY, Transform bed, out bool found)
        {
            found = false;
            RaycastHit[] hits = Physics.RaycastAll(new Vector3(at.x, fromY, at.z), Vector3.down, fromY + 20f, ~0, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue, y = at.y;
            foreach (RaycastHit h in hits)
            {
                if (h.normal.y < 0.7f || h.collider.attachedRigidbody != null || h.collider.GetComponentInParent<DarkMonster>() != null) continue;
                if (bed != null && h.collider.transform.IsChildOf(bed)) continue;
                if (h.distance < best) { best = h.distance; y = h.point.y; found = true; }
            }
            return y;
        }

        /// <summary>The room under the bed frame at a floor point: up to the first hit on the bed (0 when the bed is not met
        /// within 60). <paramref name="free"/>: the room up to the first thing of any kind (something under the frame, an
        /// ammo box, is lower); <paramref name="other"/>: that thing's name when it is not the bed.</summary>
        static float Clearance(Vector3 floor, Transform bed, out float free, out string other)
        {
            other = null;
            RaycastHit[] hits = Physics.RaycastAll(floor + Vector3.up * 0.2f, Vector3.up, 60f, ~0, QueryTriggerInteraction.Ignore);
            float bedAt = float.MaxValue, otherAt = float.MaxValue; string otherName = null;
            foreach (RaycastHit h in hits)
            {
                if (h.collider.transform.IsChildOf(bed)) { if (h.distance < bedAt) bedAt = h.distance; }
                else if (h.collider.GetComponentInParent<DarkMonster>() == null && h.distance < otherAt) { otherAt = h.distance; otherName = h.collider.name; }
            }
            float room = bedAt == float.MaxValue ? 0f : bedAt + 0.2f;
            free = Mathf.Min(room, otherAt == float.MaxValue ? float.MaxValue : otherAt + 0.2f);
            if (otherAt < bedAt) other = otherName;
            return room;
        }

        static Vector3 Closest(Vector3 a, Vector3 b, Vector3 p)
        {
            Vector3 ab = Level(b - a), ap = Level(p - a);
            float t = ab.sqrMagnitude > 1e-6f ? Mathf.Clamp01(Vector3.Dot(ap, ab) / ab.sqrMagnitude) : 0f;
            Vector3 q = a + ab * t; q.y = p.y;
            return q;
        }

        // ------------------------------------------------------------------------------------------------ the prizes (D-0223)

        static void LandPrizes(Transform bedroom, MonsterSettings s, Dictionary<string, Made> made, RoomLights lights, StarLamp lamp)
        {
            Transform claw = bedroom.Find("ClawMachine");
            Transform machine = claw != null ? (FindDeep(claw, "Toy_Claw_01") ?? (claw.childCount > 0 ? claw.GetChild(0) : null)) : null;
            if (machine == null) { Log.AppendLine("PRIZES NOT placed: no Room/Bedroom/ClawMachine (run Land Start Room Toys first)."); return; }

            // The glass case, in the machine's own space (its scale and turn taken out). In this pack the case's glass is one
            // material of the machine's own body, which also holds the prize floor, while the chute's cap and the lamps wear
            // glass too (found at landing, 25 Nov: measuring every glass part put the case's foot at the cap, y 11, under the
            // real prize floor at 33). So when the body has glass of its own, the case is that glass alone, and the body is not
            // skipped as a glass part when the floor is looked for; every other part wearing glass still is.
            var glassParts = new List<Transform>();
            bool anyGlass = false;
            var glass = new Bounds(Vector3.zero, Vector3.zero);
            MeshRenderer own = machine.GetComponent<MeshRenderer>();
            bool ownGlass = false;
            if (own != null) foreach (Material m in own.sharedMaterials) if (IsGlass(m)) { ownGlass = true; break; }
            foreach (var mr in machine.GetComponentsInChildren<MeshRenderer>(true))
            {
                var mf = mr.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null) continue;
                Material[] mats = mr.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (!IsGlass(mats[i])) continue;
                    if (mf.transform != machine && !glassParts.Contains(mf.transform)) glassParts.Add(mf.transform);
                    if (ownGlass && mr != own) continue;   // the case is the body's own glass
                    Bounds local = mats.Length == 1 || i >= mf.sharedMesh.subMeshCount ? mf.sharedMesh.bounds : mf.sharedMesh.GetSubMesh(i).bounds;
                    foreach (Vector3 corner in Corners(local))
                    {
                        Vector3 p = machine.InverseTransformPoint(mf.transform.TransformPoint(corner));
                        if (!anyGlass) { glass = new Bounds(p, Vector3.zero); anyGlass = true; } else glass.Encapsulate(p);
                    }
                }
            }
            if (!anyGlass) { Log.AppendLine($"PRIZES NOT placed: no part of {Path(machine)} wears a Glass material to find the case by."); return; }
            float unit = Mathf.Max(1e-4f, machine.lossyScale.y);   // world units per machine unit
            float narrow = Mathf.Min(glass.size.x, glass.size.z);
            float margin = s.prizeMargin * narrow;
            var inner = new Bounds(glass.center, glass.size - new Vector3(2f * margin, 0f, 2f * margin));

            // The prize floor: straight down inside the glass, the first up-facing part that is not glass nor the claw.
            var floor = new List<Vector3>();
            float top = glass.max.y - margin;
            for (int ix = 0; ix < PrizeGrid; ix++)
                for (int iz = 0; iz < PrizeGrid; iz++)
                {
                    float lx = Mathf.Lerp(inner.min.x, inner.max.x, (ix + 0.5f) / PrizeGrid);
                    float lz = Mathf.Lerp(inner.min.z, inner.max.z, (iz + 0.5f) / PrizeGrid);
                    if (FloorIn(machine, glassParts, glass, lx, lz, top, unit, out float ly)) floor.Add(new Vector3(lx, ly, lz));
                }
            if (floor.Count == 0) { Log.AppendLine($"PRIZES NOT placed: no prize floor found inside the glass of {Path(machine)} (rays down from inside the case met nothing up-facing near the glass's foot)."); return; }
            float floorY = 0f; foreach (Vector3 f in floor) floorY += f.y; floorY /= floor.Count;
            Log.AppendLine($"CLAW CASE (measured in {Path(machine)}'s own space, x{unit:F2} to the world): the glass x {glass.min.x:F3}..{glass.max.x:F3}, y {glass.min.y:F3}..{glass.max.y:F3}, z {glass.min.z:F3}..{glass.max.z:F3} ({glass.size.x * unit:F1} x {glass.size.y * unit:F1} x {glass.size.z * unit:F1} in the world; parts: {string.Join(", ", NamesOf(glassParts))}); the prize floor found at {floor.Count} of {PrizeGrid * PrizeGrid} points, about y {floorY:F3} (the rest: the chute's hole or the edges).");

            // Which way is the front (the lever and button side): prizes face it.
            Transform panel = FindDeep(machine, "Lever_01") ?? FindDeep(machine, "Button_01");
            Vector3 frontWorld = machine.forward;
            if (panel != null) { Vector3 f = Level(panel.position - machine.TransformPoint(glass.center)); if (f.sqrMagnitude > 1e-6f) frontWorld = f.normalized; }
            Vector3 frontLocal = Level(machine.InverseTransformDirection(frontWorld)).normalized;
            floor.Sort((a, b) => Vector3.Dot(a, frontLocal).CompareTo(Vector3.Dot(b, frontLocal)));   // the back of the case first

            var holder = new GameObject(PrizeHolder).transform;
            holder.SetParent(bedroom, false);
            holder.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            var lines = new List<string>();
            float shrink = 1f, used = 1f;
            int count = 0;
            int want = Mathf.Min(PrizesAtLeast, s.prizes != null ? s.prizes.Length : 0);
            for (int attempt = 0; attempt < PrizeTries; attempt++)
            {
                for (int i = holder.childCount - 1; i >= 0; i--) Object.DestroyImmediate(holder.GetChild(i).gameObject);
                lines.Clear();
                used = shrink;
                count = PlacePrizes(holder, machine, s, made, glass, inner, floor, glassParts, frontWorld, unit, used, lights, lamp, lines);
                if (count >= want) break;
                shrink *= PrizeShrink;
            }
            foreach (string l in lines) Log.AppendLine(l);
            Log.AppendLine($"PRIZES: {count} under Room/Bedroom/{PrizeHolder} (its own object, not under ClawMachine: a re-run of Land Start Room Toys leaves them; this maker finds the case again each run){(used < 0.999f ? $"; made {used:F2} of Prize Tall to fit" : "")}. Stay Put: idling{(s.prizeGlint ? " with their eye glint" : "")} in the dark, frozen once the room is lit. Every prize's box is wholly inside the glass, {margin * unit:F2} in from it, and touches nothing but the floor.");
            if (count < PrizesAtLeast) Log.AppendLine($"PRIZE WARNING: only {count} fit (6 to 8 wanted): lower MonsterSettings > Prize Tall / Prize Wide and run again.");
        }

        /// <summary>One go at the prizes at <paramref name="shrink"/> of their size: each kind on the first floor point where it
        /// sits wholly on the floor, wholly inside the glass and touching nothing else. Returns how many were placed.</summary>
        static int PlacePrizes(Transform holder, Transform machine, MonsterSettings s, Dictionary<string, Made> made, Bounds glass, Bounds inner,
            List<Vector3> floor, List<Transform> glassParts, Vector3 frontWorld, float unit, float shrink, RoomLights lights, StarLamp lamp, List<string> lines)
        {
            var taken = new List<(Vector3 at, float radius)>();
            float narrow = Mathf.Min(inner.size.x, inner.size.z);
            int index = 0, count = 0;
            foreach (string kindName in s.prizes ?? new string[0])
            {
                index++;
                if (!made.TryGetValue(kindName ?? "", out Made m)) { lines.Add($"PRIZE {index} ({kindName}): NOT placed (no prefab for that kind)."); continue; }
                float scale = s.prizeTall * inner.size.y * unit / Mathf.Max(0.01f, m.Tall);
                float wideCap = s.prizeWide * narrow * unit;
                if (m.Wide * scale > wideCap) scale = wideCap / Mathf.Max(0.01f, m.Wide);
                scale *= shrink;
                float radius = 0.5f * m.Wide * scale / unit;   // machine units
                float turn = s.prizeTurn * ((index * 7919 % 11) / 5f - 1f);
                Quaternion facing = Quaternion.AngleAxis(turn, Vector3.up) * Quaternion.LookRotation(frontWorld, Vector3.up);
                bool placed = false;
                foreach (Vector3 c in floor)
                {
                    if (c.x - radius < inner.min.x || c.x + radius > inner.max.x || c.z - radius < inner.min.z || c.z + radius > inner.max.z) continue;
                    bool free = true;
                    foreach (var (at, r) in taken) if (Level(c - at).magnitude < radius + r) { free = false; break; }
                    if (!free || !OnFloorAllRound(machine, glassParts, glass, c, radius, unit)) continue;

                    var go = (GameObject)PrefabUtility.InstantiatePrefab(m.prefab, holder);
                    go.name = $"Prize_{kindName}";
                    Vector3 floorWorld = machine.TransformPoint(c);
                    go.transform.localScale = Vector3.one * scale;
                    go.transform.SetPositionAndRotation(floorWorld + Vector3.up * (0.002f * unit - m.body.min.y * scale), facing);
                    Physics.SyncTransforms();
                    string why = PrizeProblem(go, m, machine, glass, scale);
                    if (why != null) { Object.DestroyImmediate(go); continue; }
                    var so = new SerializedObject(go.GetComponent<DarkMonster>());
                    so.FindProperty("mode").enumValueIndex = (int)DarkMonster.Mode.StayPut;
                    so.FindProperty("roomLights").objectReferenceValue = lights;
                    so.FindProperty("starLamp").objectReferenceValue = lamp;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    taken.Add((c, radius));
                    placed = true; count++;
                    lines.Add($"PRIZE {index}: {go.name} at {go.transform.position} (the case's own space {c}), {m.Tall * scale:F1} tall (scale {scale:F2}; a toy is 2, the machine {glass.size.y * unit:F1} of glass), facing the front {turn:F0} degrees off.");
                    break;
                }
                if (!placed) lines.Add($"PRIZE {index} ({kindName}): NOT placed: no free floor left inside the glass at {m.Tall * scale:F1} tall.");
            }
            return count;
        }

        /// <summary>Null when the placed prize is wholly inside the glass and touches nothing but the floor; else why not.</summary>
        static string PrizeProblem(GameObject go, Made m, Transform machine, Bounds glass, float scale)
        {
            foreach (Vector3 corner in Corners(m.body))
            {
                Vector3 p = machine.InverseTransformPoint(go.transform.TransformPoint(corner));
                if (p.x < glass.min.x || p.x > glass.max.x || p.z < glass.min.z || p.z > glass.max.z || p.y > glass.max.y) return "pokes through the glass";
            }
            Bounds b = m.body;
            Vector3 centre = go.transform.TransformPoint(b.center + Vector3.up * (0.05f * b.size.y));
            Vector3 half = Vector3.Scale(b.extents, new Vector3(0.95f, 0.9f, 0.95f)) * scale;
            foreach (Collider c in Physics.OverlapBox(centre, half, go.transform.rotation, ~0, QueryTriggerInteraction.Ignore))
                if (c != null && !c.transform.IsChildOf(go.transform)) return $"touches {c.name}";
            return null;
        }

        /// <summary>The floor holds the whole footprint: rays round its edge all find the floor at its height.</summary>
        static bool OnFloorAllRound(Transform machine, List<Transform> glassParts, Bounds glass, Vector3 c, float radius, float unit)
        {
            float top = glass.max.y - 0.02f * glass.size.y;
            for (int i = 0; i < PrizeRing; i++)
            {
                float a = i * 2f * Mathf.PI / PrizeRing;
                float x = c.x + Mathf.Cos(a) * radius, z = c.z + Mathf.Sin(a) * radius;
                if (!FloorIn(machine, glassParts, glass, x, z, top, unit, out float y) || Mathf.Abs(y - c.y) > 0.03f * glass.size.y) return false;
            }
            return true;
        }

        /// <summary>The prize floor at (x, z) in the machine's own space: straight down from <paramref name="top"/>, the first
        /// up-facing hit on the machine that is not glass nor the claw's parts, near the glass's foot. False over the chute.</summary>
        static bool FloorIn(Transform machine, List<Transform> glassParts, Bounds glass, float x, float z, float top, float unit, out float y)
        {
            y = 0f;
            Vector3 from = machine.TransformPoint(new Vector3(x, top, z));
            RaycastHit[] hits = Physics.RaycastAll(from, -machine.up, (top - glass.min.y + 0.5f * glass.size.y) * unit, ~0, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue; RaycastHit first = default;
            foreach (RaycastHit h in hits)
            {
                Transform t = h.collider.transform;
                if (!t.IsChildOf(machine) || glassParts.Contains(t) || t.name.StartsWith("Claw") || Vector3.Dot(h.normal, machine.up) < 0.7f) continue;
                if (h.distance < best) { best = h.distance; first = h; }
            }
            if (best == float.MaxValue) return false;
            y = machine.InverseTransformPoint(first.point).y;
            return y >= glass.min.y - 0.06f * glass.size.y && y <= glass.min.y + 0.15f * glass.size.y;
        }

        // ------------------------------------------------------------------------------------------------ helpers

        /// <summary>A material the claw machine's glass wears (found by its name).</summary>
        static bool IsGlass(Material m) => m != null && m.name.IndexOf("Glass", System.StringComparison.OrdinalIgnoreCase) >= 0;

        static Vector3[] Corners(Bounds b)
        {
            Vector3 n = b.min, x = b.max;
            return new[]
            {
                new Vector3(n.x, n.y, n.z), new Vector3(x.x, n.y, n.z), new Vector3(n.x, x.y, n.z), new Vector3(x.x, x.y, n.z),
                new Vector3(n.x, n.y, x.z), new Vector3(x.x, n.y, x.z), new Vector3(n.x, x.y, x.z), new Vector3(x.x, x.y, x.z),
            };
        }

        static Bounds WorldBounds(GameObject go)
        {
            bool any = false; var b = new Bounds(go.transform.position, Vector3.zero);
            foreach (Renderer r in go.GetComponentsInChildren<Renderer>())
            {
                if (r is ParticleSystemRenderer) continue;
                if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
            }
            return b;
        }

        static Transform FindDeep(Transform root, string name)
        {
            if (root == null || string.IsNullOrEmpty(name)) return null;
            var queue = new Queue<Transform>();
            queue.Enqueue(root);
            while (queue.Count > 0)
            {
                Transform t = queue.Dequeue();
                if (string.Equals(t.name, name, System.StringComparison.OrdinalIgnoreCase)) return t;
                foreach (Transform c in t) queue.Enqueue(c);
            }
            return null;
        }

        static void Remove(Transform parent, string name)
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
                if (parent.GetChild(i).name == name) Object.DestroyImmediate(parent.GetChild(i).gameObject);
        }

        static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;
            int cut = folder.LastIndexOf('/');
            string parent = folder.Substring(0, cut);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, folder.Substring(cut + 1));
        }

        static string Path(Transform t)
        {
            string p = t.name;
            for (Transform up = t.parent; up != null; up = up.parent) p = up.name + "/" + p;
            return p;
        }

        static IEnumerable<string> NamesOf(List<Transform> parts) { foreach (Transform t in parts) yield return t.name; }

        static Vector3 Level(Vector3 v) { v.y = 0f; return v; }
    }
}
#endif
