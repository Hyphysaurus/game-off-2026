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
    /// The monsters under the bed (D-0222, made readable, alive and beatable in D-0228) and the claw machine's prizes
    /// (D-0223), placed into the OPEN Greybox; it never opens or saves a scene.
    /// 1. Resources/MonsterSettings (made if missing; an existing asset keeps its numbers, apart from the one D-0228 pass
    ///    that raises the leash to 14 and the sight to 20 and gives each kind its sense and dizzy states; a missing kind is
    ///    added; empty sounds are filled with the Playroom's Toy_Squeak_Single_v1 and Ball_Bounce_v1).
    /// 2. The eye materials (calm, hunting, dizzy: URP Unlit) and the glow materials (the shift orb's glow shader, crackles
    ///    off) under Art/Monsters, their colours written from the settings each run, and set on the settings.
    /// 3. A prefab VARIANT of each pack monster in use under Prefabs/Monsters (the git-ignored Dungeon Mason prefab is the
    ///    base and is only referenced, never copied): a CharacterController sized to its baked body, a Damageable, the
    ///    DarkMonster, its eye glints (small unlit spheres on the body's front straight ahead of its eye bones, riding those
    ///    bones) and its glow square (switched off in the prefab). A variant that exists is UPDATED IN PLACE (its own parts
    ///    re-made, its components kept), so every placed copy keeps its own settings (the prizes' Stay Put).
    /// 4. The cave's monsters under Room/Bedroom/BedMonsters, replaced each run: the cave under the bed measured by raycasts,
    ///    the way from the cave's mouth to BedFlashlight, each spot from MonsterSettings > Cave Spots checked (free floor,
    ///    room overhead for its body at Cave Scale, its wander area out of the way and clear of the flashlight), placed at
    ///    Cave Scale.
    /// 5. The prizes under Room/Bedroom/ClawPrizes: placed only when there are none (a first run); once placed they are
    ///    KEPT as they are (D-0229's claw game may take one out), only reported.
    /// 6. The gold star "Monsters under the bed" (MonsterSettings > Star Id) on Resources/ObjectiveSet: added once (his
    ///    title and room never overwritten; its rule's objects are the cave monsters placed now), and an ObjectiveHook under
    ///    "ObjectiveHooks (D-0220)" that each cave monster's Damageable On Died calls with its own number (four different
    ///    parts in one visit), named as Build Objectives names it, so a later run of that maker keeps the same wiring.
    /// Menu: Shift Jam / House / Land Bedroom Monsters (D-0222, D-0223, D-0228). The scene is marked changed and not saved
    /// (Ctrl+S keeps it).
    /// </summary>
    public static class MonstersBuilder
    {
        const string Tag = "[Bedroom Monsters]";
        const string ScenePath = "Assets/_Game/Scenes/Greybox.unity";
        const string SettingsFolder = "Assets/_Game/Resources";
        const string SettingsPath = SettingsFolder + "/" + MonsterSettings.ResourceName + ".asset";
        const string PrefabFolder = "Assets/_Game/Prefabs/Monsters";
        const string GlowFolder = "Assets/_Game/Art/Monsters";
        const string EyeCalmPath = GlowFolder + "/MonsterEyeGlow.mat";
        const string EyeHuntPath = GlowFolder + "/MonsterEyeHunt.mat";
        const string EyeDizzyPath = GlowFolder + "/MonsterEyeDizzy.mat";
        const string HaloCalmPath = GlowFolder + "/MonsterHaloCalm.mat";
        const string HaloHuntPath = GlowFolder + "/MonsterHaloHunt.mat";
        const string HaloDizzyPath = GlowFolder + "/MonsterHaloDizzy.mat";
        const string AuraShader = "ShiftJam/ShiftOrbAura";
        const string SqueakPath = "Assets/_Imports/ithappy/Playroom/Playroom_Sound_FX/Toys/Toy_Squeak_Single_v1.wav";
        const string BumpPath = "Assets/_Imports/ithappy/Playroom/Playroom_Sound_FX/Toys/Ball_Bounce_v1.wav";
        const string CaveHolder = "BedMonsters";
        const string PrizeHolder = "ClawPrizes";
        const string GlintName = "EyeGlint";
        const string HaloName = "MonsterHalo";

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

        // The star's wiring (as ObjectivesBuilder writes it).
        const int ModeInt = 3, RuntimeOnly = 2;   // PersistentListenerMode.Int, UnityEventCallState.RuntimeOnly

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

        /// <summary>The materials the variants and the settings wear.</summary>
        sealed class Looks
        {
            public Material eyeCalm, eyeHunt, eyeDizzy, haloCalm, haloHunt, haloDizzy;
        }

        [MenuItem("Shift Jam/House/Land Bedroom Monsters (D-0222, D-0223, D-0228)")]
        public static void LandMenu()
        {
            string report = Land();
            if (report.StartsWith(Tag + " NOT ")) Debug.LogWarning(report); else Debug.Log(report);
        }

        /// <summary>
        /// Makes or updates the settings, the materials, the prefab variants, the cave's monsters, the prizes (only when
        /// there are none) and the gold star in the OPEN Greybox and marks it changed; it does not save it. A second run
        /// replaces BedMonsters, updates the variants in place and keeps ClawPrizes. Returns the report, one fact a line;
        /// a report that starts "[Bedroom Monsters] NOT " means nothing was changed.
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
            Log.AppendLine($"{Tag} Land Bedroom Monsters (D-0222 the monsters under the bed, D-0223 the claw machine's prizes, D-0228 readable, alive and beatable): {(again ? "SECOND run: BedMonsters is replaced, the prefab variants are updated in place, ClawPrizes is KEPT as it is" : "first run")}. Greybox is MARKED CHANGED and NOT SAVED: Ctrl+S keeps it.");

            // The cave's monsters go first (they are placed again below); the prizes stay.
            Remove(bedroom, CaveHolder);

            MonsterSettings s = Settings();
            Looks looks = Materials(unlit, s);

            var made = new Dictionary<string, Made>();
            var wanted = new HashSet<string>();
            if (s.caveSpots != null) foreach (var spot in s.caveSpots) wanted.Add(spot.kind);
            if (s.prizes != null) foreach (string p in s.prizes) wanted.Add(p);
            EnsureFolder(PrefabFolder);
            foreach (MonsterSettings.Kind kind in s.kinds)
            {
                if (kind == null || !wanted.Contains(kind.name) || made.ContainsKey(kind.name)) continue;
                Made m = Variant(kind, s, looks);
                if (m != null) made[kind.name] = m;
            }
            foreach (string w in wanted) if (!s.Has(w)) Log.AppendLine($"KIND '{w}': named in Cave Spots or Prizes but NOT in MonsterSettings > Kinds: skipped.");
            AssetDatabase.SaveAssets();

            RoomLights lights = bedroom.GetComponentInChildren<RoomLights>(true);
            StarLamp lamp = bedroom.GetComponentInChildren<StarLamp>(true);
            Log.AppendLine($"THE ROOM'S LIGHTS: wall switch {(lights != null ? Path(lights.transform) + " (RoomLights, saved '" + lights.Key + "'): on = every monster and prize is a FROZEN toy (a hit tips a cave monster over)" : "NO RoomLights under Room/Bedroom: they never freeze")}; star lamp {(lamp != null ? Path(lamp.transform) + " (StarLamp, saved '" + lamp.Key + "'): awake (switch off) = the cave's monsters are SLEEPY (speeds x" + s.sleepySpeed + ", sight x" + s.sleepySight + "), the prizes idle" : "NO StarLamp under Room/Bedroom: never sleepy")}.");

            Physics.SyncTransforms();
            List<DarkMonster> cave = LandCave(bedroom, s, made, lights, lamp);
            Transform prizes = bedroom.Find(PrizeHolder);
            if (prizes != null) ReportPrizes(prizes);
            else LandPrizes(bedroom, s, made, lights, lamp);
            Star(scene, s, cave);

            EditorSceneManager.MarkSceneDirty(scene);
            Log.AppendLine("Greybox MARKED CHANGED, not saved. Online: nothing networked; each PC's monsters react to that PC's own player (MonsterSettings > Online off keeps them still toys online). Off switches: MonsterSettings > On (all still toys), Halo On (no glow), Stay Down (off: they get up after Down Seconds); untick the star's hook to drop the star's wiring.");
            Log.AppendLine("Test without leaving Play: the console's 'bedroom reset' (` then type it): the active slot forgets the bedroom's lights and star lamp, the monsters stand whole at their spots.");
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
            string pass = Pass(s);
            var squeak = AssetDatabase.LoadAssetAtPath<AudioClip>(SqueakPath);
            var bump = AssetDatabase.LoadAssetAtPath<AudioClip>(BumpPath);
            if (s.squeak.clip == null) s.squeak.clip = squeak;
            if (s.spotSqueak.clip == null) s.spotSqueak.clip = squeak;
            if (s.downSqueak.clip == null) s.downSqueak.clip = squeak;
            if (s.bump.clip == null) s.bump.clip = bump;
            EditorUtility.SetDirty(s);
            AssetDatabase.SaveAssetIfDirty(s);
            Log.AppendLine($"SETTINGS: {SettingsPath} {(fresh ? "MADE with its defaults" : "FOUND: its numbers kept")}{(added.Count > 0 && !fresh ? "; kinds added: " + string.Join(", ", added) : "")}. {s.kinds.Length} kinds. {pass}");
            Log.AppendLine($"NUMBERS (toy units; the toy is 2 tall and walks 7): Cave Scale x{s.caveScale}. Sight {s.sight} (sleepy x{s.sleepySight} = {s.sight * s.sleepySight:0.#}; kept track of within x{s.keepTrack} once after the toy), lose it after {s.loseAfter} s. Wander within {s.wanderRadius} of its spot, a walk every {s.wanderPauseMin}..{s.wanderPauseMax} s. Spotting: stares {s.senseSeconds} s with a \"{s.markText}\" ({s.markSize} of its height, {s.markPopSeconds} s pop to x{s.markOvershoot}, held {s.markHoldSeconds} s), no second one within {s.spotAgainAfter} s. Leash {s.leash}; speeds walk {s.creepSpeed}, chase {s.chaseSpeed}, run {s.runSpeed}, back {s.backSpeed} (sleepy x{s.sleepySpeed}). The bump: wind-up {s.windupSeconds} s (held from {s.windupPoseAt} s into its attack clip), push {s.strikeSeconds} s after the swing (gap {s.bumpGap}, missed past x{s.bumpMissShare} of the reach), away {s.bumpPush} + up {s.bumpLift} (no damage), cooldown {s.bumpCooldown} s, all monsters at most {s.bumpsAtMost} bumps in {s.bumpWindow} s. The beam: flinch {s.flinchSeconds} s, flee {s.fleeDistance}, home {s.returnAfter} s after; DIZZY after {s.dizzyAfter} s of beam (forgotten after {s.beamForget} s without), for {s.dizzySeconds} s, hits x{s.dizzyHitScale}. Health {s.health} (dart 15, punch 10, kick 20), Stay Down {(s.stayDown ? "ON (down for the visit)" : "off (up after " + s.downSeconds + " s)")}. Frozen tip: {s.tipAngle} degrees over {s.tipSeconds} s, lies {s.tipLie} s, rights itself in {s.tipRightSeconds} s. Way kept {s.laneHalfWidth} either side; flashlight kept {s.flashlightClear} clear of every body.");
            Log.AppendLine($"SOUNDS: squeak (the beam's flinch) {Clip(s.squeak.clip, SqueakPath)} at {s.squeak.volume}; spotting {Clip(s.spotSqueak.clip, SqueakPath)} at {s.spotSqueak.volume}, pitch {s.spotPitch}; knocked down {Clip(s.downSqueak.clip, SqueakPath)} at {s.downSqueak.volume}, pitch {s.downPitch}, with the soldiers' burst and thud (Resources/WeaponAmmo, through KnockDown); bump and tip {Clip(s.bump.clip, BumpPath)} at {s.bump.volume}. Heard from the monster: full within {s.soundNear}, silent past {s.soundFar}.");
            return s;
        }

        /// <summary>The D-0228 pass, once per asset (Pass below 228): the leash and the sight raised to this file's defaults,
        /// each kind given its sense and dizzy states (its old hide state was the dizzy clip: it cowers in its alert now).</summary>
        static string Pass(MonsterSettings s)
        {
            if (s.pass >= MonsterSettings.CurrentPass) return $"Pass {s.pass}: up to date, nothing raised.";
            var d = ScriptableObject.CreateInstance<MonsterSettings>();
            var said = new StringBuilder($"D-0228 PASS (Pass {s.pass} -> {MonsterSettings.CurrentPass}): leash {s.leash} -> {d.leash}, sight {s.sight} -> {d.sight}");
            s.leash = d.leash; s.sight = d.sight;
            Object.DestroyImmediate(d);
            MonsterSettings.Kind[] defaults = MonsterSettings.DefaultKinds();
            foreach (MonsterSettings.Kind k in s.kinds)
            {
                if (k == null) continue;
                MonsterSettings.Kind def = null;
                foreach (var x in defaults) if (x.name == k.name) { def = x; break; }
                if (string.IsNullOrEmpty(k.sense)) k.sense = def != null ? def.sense : "";
                if (string.IsNullOrEmpty(k.dizzy)) k.dizzy = def != null ? def.dizzy : k.hide;
                if (k.hide == k.dizzy) k.hide = k.alert;
                said.Append($"; {k.name}: sense '{k.sense}', dizzy '{k.dizzy}', hide '{k.hide}'");
            }
            s.pass = MonsterSettings.CurrentPass;
            return said.Append('.').ToString();
        }

        static string Clip(AudioClip clip, string expected) => clip != null ? $"{clip.name} ({clip.length:F2} s)" : $"NONE (no {expected}: silent)";

        /// <summary>The eye and glow materials, made or found, their colours written from the settings, and set on them.</summary>
        static Looks Materials(Shader unlit, MonsterSettings s)
        {
            EnsureFolder(GlowFolder);
            var looks = new Looks
            {
                eyeCalm = Eye(EyeCalmPath, unlit, s.glint),
                eyeHunt = Eye(EyeHuntPath, unlit, s.glintHunt),
                eyeDizzy = Eye(EyeDizzyPath, unlit, s.glintDizzy),
            };
            Shader aura = Shader.Find(AuraShader);
            if (aura != null)
            {
                looks.haloCalm = Halo(HaloCalmPath, aura, s.haloCalmColour, s);
                looks.haloHunt = Halo(HaloHuntPath, aura, s.haloHuntColour, s);
                looks.haloDizzy = Halo(HaloDizzyPath, aura, s.haloDizzyColour, s);
            }
            s.eyeCalm = looks.eyeCalm; s.eyeHunt = looks.eyeHunt; s.eyeDizzy = looks.eyeDizzy;
            s.haloCalm = looks.haloCalm; s.haloHunt = looks.haloHunt; s.haloDizzy = looks.haloDizzy;
            EditorUtility.SetDirty(s);
            AssetDatabase.SaveAssetIfDirty(s);
            Log.AppendLine($"EYES: {EyeCalmPath} (calm {s.glint}), {EyeHuntPath} (after the toy {s.glintHunt}), {EyeDizzyPath} (dizzy {s.glintDizzy}): URP Unlit, over 1 glows under bloom, not dimmed by a Dark Zone; set on MonsterSettings > Eye Calm / Hunt / Dizzy.");
            Log.AppendLine(aura != null
                ? $"GLOW: {HaloCalmPath}, {HaloHuntPath}, {HaloDizzyPath} ({AuraShader}: the shift orb's soft additive glow, crackles off; calm {s.haloCalmColour}, after the toy {s.haloHuntColour}, dizzy {s.haloDizzyColour}; brightness {s.haloStrength}, falloff {s.haloReach}, the square {s.haloSpread} times the body); Halo On {(s.haloOn ? "ticked" : "UNTICKED: no glow")}."
                : $"GLOW: NO '{AuraShader}' shader (did it fail to compile?): no glow materials, the monsters have no glow (the eyes still show).");
            return looks;
        }

        static Material Eye(string path, Shader unlit, Color colour)
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(unlit) { name = System.IO.Path.GetFileNameWithoutExtension(path) };
                AssetDatabase.CreateAsset(m, path);
            }
            else if (m.shader != unlit) m.shader = unlit;
            m.SetColor("_BaseColor", colour);
            EditorUtility.SetDirty(m);
            AssetDatabase.SaveAssetIfDirty(m);
            return m;
        }

        static Material Halo(string path, Shader aura, Color colour, MonsterSettings s)
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(aura) { name = System.IO.Path.GetFileNameWithoutExtension(path) };
                AssetDatabase.CreateAsset(m, path);
            }
            else if (m.shader != aura) m.shader = aura;
            m.SetColor("_BaseColor", colour);
            m.SetColor("_FarColor", colour * 0.6f);
            m.SetFloat("_OrbEdge", Mathf.Clamp(1f / Mathf.Max(1f, s.haloSpread), 0.1f, 0.9f));   // the bright ring at the body's own edge
            m.SetFloat("_GlowStrength", s.haloStrength);
            m.SetFloat("_GlowReach", s.haloReach);
            m.SetFloat("_Escapes", 0f);
            m.SetFloat("_EscapeBright", 0f);
            EditorUtility.SetDirty(m);
            AssetDatabase.SaveAssetIfDirty(m);
            return m;
        }

        // ------------------------------------------------------------------------------------------------ the prefab variants

        /// <summary>One kind's prefab variant: made from the pack prefab the first time, updated in place after that (its
        /// components kept, so every placed copy keeps its own settings), our collider, health, monster, eyes and glow on top.</summary>
        static Made Variant(MonsterSettings.Kind kind, MonsterSettings s, Looks looks)
        {
            var pack = AssetDatabase.LoadAssetAtPath<GameObject>(kind.packPrefab);
            if (pack == null) { Log.AppendLine($"KIND {kind.name}: NO {kind.packPrefab} (the Dungeon Mason packs are git-ignored: import them on this PC). Not made; its spots and prizes are skipped."); return null; }
            string path = $"{PrefabFolder}/{kind.name}Monster.prefab";
            bool inPlace = AssetDatabase.LoadAssetAtPath<GameObject>(path) != null;
            var temps = new List<Object>();
            var shells = new List<Collider>();
            GameObject root = inPlace ? PrefabUtility.LoadPrefabContents(path) : (GameObject)PrefabUtility.InstantiatePrefab(pack, SceneManager.GetActiveScene());
            try
            {
                if (!inPlace) { root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity); root.transform.localScale = Vector3.one; }
                int removed = RemoveOurParts(root.transform);
                Physics.SyncTransforms();
                Bounds body = Body(root.transform, temps, shells);
                float tall = Mathf.Max(0.05f, body.size.y);
                float radius = Mathf.Clamp(0.5f * Mathf.Min(body.size.x, body.size.z), 0.05f, 0.5f * tall);

                var cc = root.GetComponent<CharacterController>();
                if (cc == null) cc = root.AddComponent<CharacterController>();
                cc.height = tall; cc.radius = radius; cc.center = body.center;
                cc.skinWidth = Mathf.Max(0.01f, 0.08f * radius);
                cc.stepOffset = 0.25f * tall; cc.slopeLimit = 45f; cc.minMoveDistance = 0f;

                var health = root.GetComponent<Damageable>();
                if (health == null) health = root.AddComponent<Damageable>();
                var hso = new SerializedObject(health);
                hso.FindProperty("maxHealth").floatValue = s.health;
                hso.FindProperty("takeKnockback").boolValue = false;   // nothing of ours takes a shove: no body to push
                hso.ApplyModifiedPropertiesWithoutUndo();

                List<Renderer> eyes = Eyes(root.transform, kind, body, looks.eyeCalm, shells, out string eyeLine);
                Renderer glow = looks.haloCalm != null ? GlowSquare(root.transform, body, s, looks.haloCalm) : null;
                Animator animator = root.GetComponentInChildren<Animator>(true);

                var monster = root.GetComponent<DarkMonster>();
                if (monster == null) monster = root.AddComponent<DarkMonster>();
                var mso = new SerializedObject(monster);
                mso.FindProperty("kind").stringValue = kind.name;
                mso.FindProperty("animator").objectReferenceValue = animator;
                mso.FindProperty("halo").objectReferenceValue = glow;
                var list = mso.FindProperty("glints");
                list.arraySize = eyes.Count;
                for (int i = 0; i < eyes.Count; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = eyes[i];
                mso.ApplyModifiedPropertiesWithoutUndo();

                string states = States(animator, kind);
                foreach (Object t in temps) if (t != null) Object.DestroyImmediate(t);
                temps.Clear();

                GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, path, out bool ok);
                if (!ok || saved == null) { Log.AppendLine($"KIND {kind.name}: COULD NOT SAVE {path}. Its spots and prizes are skipped."); return null; }
                Log.AppendLine($"KIND {kind.name}: {path} ({PrefabUtility.GetPrefabAssetType(saved)} of {kind.packPrefab}; {(inPlace ? "UPDATED IN PLACE, " + removed + " old part" + (removed == 1 ? "" : "s") + " of ours re-made" : "MADE")}): body {body.size.x:F2} x {body.size.y:F2} x {body.size.z:F2} at scale 1 (a toy is about 2 tall; in the cave x{s.caveScale} = {body.size.y * s.caveScale:F2} tall); CharacterController height {tall:F2}, radius {radius:F2}, centre {body.center}; Damageable {s.health}; {eyeLine}; glow square {(glow != null ? $"{s.haloSpread * Mathf.Max(body.size.x, body.size.z):F2} x {s.haloSpread * tall:F2} (off in the prefab)" : "NONE (no glow material)")}; Animator {(animator != null && animator.runtimeAnimatorController != null ? animator.runtimeAnimatorController.name : "NONE")}: {states}.");
                return new Made { prefab = saved, body = body, radius = radius };
            }
            finally
            {
                foreach (Object t in temps) if (t != null) Object.DestroyImmediate(t);
                if (root != null)
                {
                    if (inPlace) PrefabUtility.UnloadPrefabContents(root);
                    else Object.DestroyImmediate(root);
                }
            }
        }

        /// <summary>Our own parts on a variant (the eye glints and the glow square), removed so they are made again. Returns how many.</summary>
        static int RemoveOurParts(Transform root)
        {
            var ours = new List<GameObject>();
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                if (t != root && (t.name == GlintName || t.name == HaloName)) ours.Add(t.gameObject);
            foreach (GameObject go in ours) if (go != null) Object.DestroyImmediate(go);
            return ours.Count;
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
                if (mf.sharedMesh == null || mr == null || !mr.enabled || mf.name == GlintName || mf.name == HaloName) continue;
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
            go.name = GlintName;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            Adopt(go, parent);
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

        /// <summary>The glow square: a quad on the root at the body's middle, Halo Spread times its width and height, wearing
        /// the calm glow; switched OFF here (the monster turns it on while awake and places it behind itself each frame).</summary>
        static Renderer GlowSquare(Transform root, Bounds body, MonsterSettings s, Material glow)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = HaloName;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            Adopt(go, root);
            go.transform.localPosition = body.center;
            go.transform.localRotation = Quaternion.identity;
            Vector3 ls = root.lossyScale;
            float wide = s.haloSpread * Mathf.Max(body.size.x, body.size.z), tall = s.haloSpread * body.size.y;
            go.transform.localScale = new Vector3(wide / Mathf.Max(1e-4f, Mathf.Abs(ls.x)), tall / Mathf.Max(1e-4f, Mathf.Abs(ls.y)), 1f);
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = glow;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.lightProbeUsage = LightProbeUsage.Off;
            r.reflectionProbeUsage = ReflectionProbeUsage.Off;
            r.enabled = false;
            return r;
        }

        /// <summary>Puts a new object under <paramref name="parent"/> (into the parent's scene first: a variant opened for
        /// editing lives in its own preview scene).</summary>
        static void Adopt(GameObject go, Transform parent)
        {
            if (go.scene != parent.gameObject.scene) SceneManager.MoveGameObjectToScene(go, parent.gameObject.scene);
            go.transform.SetParent(parent, false);
        }

        /// <summary>Which of the kind's state names the controller has (a missing one falls back at run time).</summary>
        static string States(Animator animator, MonsterSettings.Kind kind)
        {
            RuntimeAnimatorController rac = animator != null ? animator.runtimeAnimatorController : null;
            if (rac is AnimatorOverrideController over) rac = over.runtimeAnimatorController;
            var ctrl = rac as UnityEditor.Animations.AnimatorController;
            if (ctrl == null || ctrl.layers.Length == 0) return "NO controller to check the state names against";
            var names = new HashSet<string>();
            Collect(ctrl.layers[0].stateMachine, names);
            var missing = new List<string>();
            string[] all = { kind.idle, kind.alert, kind.walk, kind.run, kind.back, kind.attack, kind.hit, kind.hide, kind.die, kind.rest, kind.sense, kind.dizzy };
            int named = 0;
            foreach (string n in all)
            {
                if (string.IsNullOrEmpty(n)) continue;
                named++;
                if (!names.Contains(n) && !missing.Contains(n)) missing.Add(n);
            }
            return missing.Count == 0 ? $"all {named} state names found{(string.IsNullOrEmpty(kind.sense) ? " (no sense state: the alert stands in)" : "")}" : $"state names NOT in it (they fall back: sense to the alert, dizzy to the hide, the rest to the idle): {string.Join(", ", missing)}";
        }

        static void Collect(UnityEditor.Animations.AnimatorStateMachine machine, HashSet<string> names)
        {
            if (machine == null) return;
            foreach (var st in machine.states) if (st.state != null) names.Add(st.state.name);
            foreach (var sub in machine.stateMachines) Collect(sub.stateMachine, names);
        }

        // ------------------------------------------------------------------------------------------------ the cave (D-0222, D-0228)

        /// <summary>Measures the cave and places the cave's monsters at Cave Scale. Returns them in spot order.</summary>
        static List<DarkMonster> LandCave(Transform bedroom, MonsterSettings s, Dictionary<string, Made> made, RoomLights lights, StarLamp lamp)
        {
            var monsters = new List<DarkMonster>();
            Transform bed = bedroom.Find("Bed");
            Transform torchT = bedroom.Find("UnderBed/BedFlashlight");
            if (torchT == null)
            {
                Transform under = bedroom.Find("UnderBed");
                Flashlight f = under != null ? under.GetComponentInChildren<Flashlight>(true) : null;
                if (f != null) torchT = f.transform;
            }
            if (bed == null || torchT == null) { Log.AppendLine($"CAVE MONSTERS NOT placed: no {(bed == null ? "Room/Bedroom/Bed" : "Room/Bedroom/UnderBed/BedFlashlight")} to measure the cave by."); return monsters; }
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
            if (!anyCave) { Log.AppendLine($"CAVE MONSTERS NOT placed: no room under the bed found (rays up from the floor at y {floorY:F2} over the bed's footprint met the frame lower than {CaveLeastRoom})."); return monsters; }
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
            Log.AppendLine($"THE WAY IN: from {(toyBox != null ? "Room/ToyBox" : "the toy's start (no Room/ToyBox)")} at {Level(start)} to BedFlashlight at {torch}: the cave's mouth at {mouth}{(fromInside ? " (the start is already under the bed)" : "")}, {wayLength:F1} to the flashlight; a toy's capsule along it: {wayClear}. Kept open: {s.laneHalfWidth} either side of that line (no monster stands, waits, wanders or hides in it; a chase may cross it), and no body within {s.flashlightClear} of the flashlight. The cave's back (where they run from a beam): {back}. The box they keep inside: x {cave.min.x:F1}..{cave.max.x:F1}, z {cave.min.z:F1}..{cave.max.z:F1} ({cave.size.x:F0} x {cave.size.z:F0}).");

            var holder = new GameObject(CaveHolder).transform;
            holder.SetParent(bedroom, false);
            holder.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            var placed = new List<(Vector3 at, float radius)>();
            float size = Mathf.Max(0.1f, s.caveScale);
            int index = 0;
            foreach (MonsterSettings.CaveSpot spot in s.caveSpots ?? new MonsterSettings.CaveSpot[0])
            {
                index++;
                if (!made.TryGetValue(spot.kind ?? "", out Made m)) { Log.AppendLine($"CAVE SPOT {index} ({spot.kind}): NOT placed (no prefab for that kind)."); continue; }
                float radius = m.radius * size, tall = m.Tall * size;
                float least2 = s.laneHalfWidth + s.wanderRadius + radius + 0.5f;
                float apart = Mathf.Max(spot.apart, least2);
                string why = null; Vector3 at = Vector3.zero; float roomOver = 0f; bool ok = false;
                var tried = new List<string>();
                float side = spot.side >= 0f ? 1f : -1f;
                for (int sideTry = 0; sideTry < 2 && !ok; sideTry++, side = -side)
                    for (int push = 0; push < 3 && !ok; push++)
                    {
                        at = mouth + dir * (spot.along * wayLength) + left * (side * (apart + push * SpotPush));
                        why = SpotProblem(at, radius, tall, s, cave, aim, placed, bed, floorY, out roomOver, out float spotFloor);
                        if (why == null) { ok = true; at.y = spotFloor; }
                        else tried.Add($"({at.x:F1}, {at.z:F1}): {why}");
                    }
                if (!ok) { Log.AppendLine($"CAVE SPOT {index} ({spot.kind}, along {spot.along}, side {spot.side}, apart {spot.apart}): NOT placed: " + string.Join("; ", tried) + ". Change its numbers in MonsterSettings > Cave Spots (or Cave Scale) and run again."); continue; }

                var go = (GameObject)PrefabUtility.InstantiatePrefab(m.prefab, holder);
                go.name = $"{spot.kind}_{index}";
                Vector3 onWay = Closest(mouth, aim, at);
                Vector3 look = Level(onWay - at);
                go.transform.localScale = Vector3.one * size;
                go.transform.SetPositionAndRotation(new Vector3(at.x, at.y - m.body.min.y * size + 0.01f, at.z), look.sqrMagnitude > 1e-4f ? Quaternion.LookRotation(look, Vector3.up) : Quaternion.identity);
                var monster = go.GetComponent<DarkMonster>();
                var so = new SerializedObject(monster);
                so.FindProperty("mode").enumValueIndex = (int)DarkMonster.Mode.Roam;
                so.FindProperty("roomLights").objectReferenceValue = lights;
                so.FindProperty("starLamp").objectReferenceValue = lamp;
                so.FindProperty("cave").boundsValue = cave;
                so.FindProperty("caveBack").vector3Value = back;
                so.FindProperty("laneFrom").vector3Value = mouth;
                so.FindProperty("laneTo").vector3Value = aim;
                so.FindProperty("laneHalfWidth").floatValue = s.laneHalfWidth;
                so.ApplyModifiedPropertiesWithoutUndo();
                placed.Add((at, radius));
                monsters.Add(monster);
                float toWay = Level(at - onWay).magnitude;
                Log.AppendLine($"CAVE SPOT {index}: {go.name} at ({at.x:F1}, {at.y:F2}, {at.z:F1}), scale {size} ({tall:F2} tall, radius {radius:F2}){(tried.Count > 0 ? " (tried first: " + string.Join("; ", tried) + ")" : "")}: {Level(at - aim).magnitude:F1} from BedFlashlight, {Level(at - mouth).magnitude:F1} from the cave's mouth, {toWay:F1} from the way's middle line (its wander area's edge {toWay - s.wanderRadius - radius:F1} from it, the kept way {s.laneHalfWidth}; its leash {s.leash} reaches across it), {roomOver:F2} of room overhead; facing the way.");
            }
            Log.AppendLine($"CAVE MONSTERS: {placed.Count} under Room/Bedroom/{CaveHolder} (its own object: a re-run of Land Bedroom Light Beats, which remakes UnderBed, leaves them). Each notices a toy {s.sight} away in sight ({s.sight * s.sleepySight:0.#} sleepy), chases up to {s.leash} from its spot, wanders within {s.wanderRadius}, never within {s.flashlightClear} of the flashlight.");
            if (placed.Count < 3) Log.AppendLine($"CAVE WARNING: only {placed.Count} placed (3 to 5 wanted): see the spot lines above.");
            return monsters;
        }

        /// <summary>Why a monster may not stand at <paramref name="at"/> (null when it may): its wander area out of the cave's
        /// box or too near the flashlight, too near another monster, no room overhead, no floor, or something solid there.</summary>
        static string SpotProblem(Vector3 at, float radius, float tall, MonsterSettings s, Bounds cave, Vector3 torch, List<(Vector3 at, float radius)> placed, Transform bed, float floorY, out float roomOver, out float spotFloor)
        {
            roomOver = 0f; spotFloor = floorY;
            float edge = s.wanderRadius + radius;
            if (at.x < cave.min.x + edge || at.x > cave.max.x - edge || at.z < cave.min.z + edge || at.z > cave.max.z - edge)
                return $"its wander area would reach out from under the bed (keep {edge:F1} in from the cave's box)";
            float needTorch = s.wanderRadius + radius + s.flashlightClear + ToyRadius;
            float toTorch = Level(at - torch).magnitude;
            if (toTorch < needTorch) return $"{toTorch:F1} from the flashlight (wants {needTorch:F1})";
            foreach (var (other, r) in placed)
            {
                float gap = Level(at - other).magnitude - r - radius;
                if (gap < SpotsApart) return $"{gap:F1} from another monster";
            }
            spotFloor = Floor(at, floorY + 3f, bed, out bool found);
            if (!found || Mathf.Abs(spotFloor - floorY) > 0.5f) return found ? $"its floor is at y {spotFloor:F2}, not the cave's {floorY:F2}" : "no floor under it";
            Clearance(new Vector3(at.x, spotFloor, at.z), bed, out roomOver, out string over);
            if (roomOver < tall + HeadRoom) return $"{roomOver:F2} of room overhead for {tall:F2}{(over != null ? " (under " + over + ")" : "")}";
            Vector3 low = new Vector3(at.x, spotFloor + radius + 0.05f, at.z);
            Vector3 high = new Vector3(at.x, spotFloor + Mathf.Max(radius + 0.06f, tall - radius), at.z);
            foreach (Collider c in Physics.OverlapCapsule(low, high, radius, ~0, QueryTriggerInteraction.Ignore))
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

        // ------------------------------------------------------------------------------------------------ the gold star (D-0228)

        /// <summary>The star on Resources/ObjectiveSet (added once, his words kept; its rule's objects are the monsters placed
        /// now) and its hook, wired to each cave monster's On Died with that monster's number.</summary>
        static void Star(Scene scene, MonsterSettings s, List<DarkMonster> monsters)
        {
            if (string.IsNullOrEmpty(s.starId)) { Log.AppendLine("STAR: MonsterSettings > Star Id is empty: no star, nothing wired."); return; }
            var set = AssetDatabase.LoadAssetAtPath<ObjectiveSet>(ObjectivesBuilder.AssetPath);
            if (set == null) { Log.AppendLine($"STAR NOT added: no {ObjectivesBuilder.AssetPath} (run Shift Jam > Objectives > Build Objectives first, then this again)."); return; }
            if (monsters.Count == 0) { Log.AppendLine("STAR NOT wired: no cave monster was placed."); return; }

            var paths = new string[monsters.Count];
            for (int i = 0; i < monsters.Count; i++) paths[i] = Path(monsters[i].transform);

            var stars = new List<ObjectiveStar>(set.stars ?? new ObjectiveStar[0]);
            ObjectiveStar star = stars.Find(x => x != null && x.id == s.starId);
            bool fresh = star == null;
            if (fresh)
            {
                star = new ObjectiveStar { id = s.starId, title = s.starTitle, room = s.starRoom, doneWhen = new ObjectiveRule[0] };
                int at = stars.FindLastIndex(x => x != null && x.room == s.starRoom);
                stars.Insert(at >= 0 ? at + 1 : stars.Count, star);
                set.stars = stars.ToArray();
            }
            var rules = new List<ObjectiveRule>(star.doneWhen ?? new ObjectiveRule[0]);
            int ruleIndex = rules.FindIndex(r => r != null && r.when == ObjectiveEvent.KnockedDown);
            if (ruleIndex < 0) { rules.Add(new ObjectiveRule()); ruleIndex = rules.Count - 1; }
            ObjectiveRule rule = rules[ruleIndex];
            rule.when = ObjectiveEvent.KnockedDown; rule.scene = ObjectiveSet.House; rule.needed = 0; rule.objects = paths;
            star.doneWhen = rules.ToArray();
            EditorUtility.SetDirty(set);
            AssetDatabase.SaveAssetIfDirty(set);
            Log.AppendLine($"STAR '{star.id}': \"{star.title}\" ({star.room}) {(fresh ? "ADDED to " + ObjectivesBuilder.AssetPath + " (placeholder title: his to rewrite there)" : "FOUND: his title and room kept")}; star {stars.IndexOf(star) + 1} of {set.stars.Length}. Rule {ruleIndex + 1}: KnockedDown in {ObjectiveSet.House}, every one of {paths.Length}: {string.Join(", ", paths)}. A star refills the shift charge (Objectives.AwardStar), as the others.");

            // The hook, named as Build Objectives names it ("Star <id> #<rule> KnockedDown"), so that maker finds the same one.
            GameObject holder = null;
            foreach (GameObject r in scene.GetRootGameObjects()) if (r.name == ObjectivesBuilder.HooksName) { holder = r; break; }
            bool madeHolder = holder == null;
            if (madeHolder)
            {
                holder = new GameObject(ObjectivesBuilder.HooksName);
                SceneManager.MoveGameObjectToScene(holder, scene);
            }
            string hookName = $"{ObjectiveHook.Kind.Star} {star.id} #{ruleIndex + 1} {ObjectiveEvent.KnockedDown}";
            Transform child = holder.transform.Find(hookName);
            bool madeHook = child == null;
            if (madeHook)
            {
                child = new GameObject(hookName).transform;
                child.SetParent(holder.transform, false);
            }
            ObjectiveHook hook = child.GetComponent<ObjectiveHook>();
            if (hook == null) hook = child.gameObject.AddComponent<ObjectiveHook>();
            hook.Setup(star.id, ObjectiveHook.Kind.Star, paths.Length);
            EditorUtility.SetDirty(hook);

            int wired = 0;
            for (int i = 0; i < monsters.Count; i++)
            {
                var health = monsters[i].GetComponent<Damageable>();
                var so = new SerializedObject(health);
                SerializedProperty calls = so.FindProperty("onDied.m_PersistentCalls.m_Calls");
                if (calls == null) { Log.AppendLine($"  NOT WIRED: {paths[i]} has no On Died event on its Damageable."); continue; }
                for (int c = calls.arraySize - 1; c >= 0; c--)
                {
                    SerializedProperty old = calls.GetArrayElementAtIndex(c);
                    Object target = old.FindPropertyRelative("m_Target").objectReferenceValue;
                    string typeName = old.FindPropertyRelative("m_TargetAssemblyTypeName").stringValue ?? "";
                    if (target is ObjectiveHook || (target == null && typeName.StartsWith(typeof(ObjectiveHook).FullName))) calls.DeleteArrayElementAtIndex(c);
                }
                calls.arraySize++;
                SerializedProperty call = calls.GetArrayElementAtIndex(calls.arraySize - 1);
                call.FindPropertyRelative("m_Target").objectReferenceValue = hook;
                call.FindPropertyRelative("m_TargetAssemblyTypeName").stringValue = typeof(ObjectiveHook).AssemblyQualifiedName;
                call.FindPropertyRelative("m_MethodName").stringValue = nameof(ObjectiveHook.FirePart);
                call.FindPropertyRelative("m_Mode").intValue = ModeInt;
                call.FindPropertyRelative("m_CallState").intValue = RuntimeOnly;
                SerializedProperty args = call.FindPropertyRelative("m_Arguments");
                args.FindPropertyRelative("m_ObjectArgument").objectReferenceValue = null;
                args.FindPropertyRelative("m_ObjectArgumentAssemblyTypeName").stringValue = "";
                args.FindPropertyRelative("m_IntArgument").intValue = i;
                args.FindPropertyRelative("m_FloatArgument").floatValue = 0f;
                args.FindPropertyRelative("m_StringArgument").stringValue = "";
                args.FindPropertyRelative("m_BoolArgument").boolValue = false;
                so.ApplyModifiedPropertiesWithoutUndo();
                if (PrefabUtility.IsPartOfPrefabInstance(health)) PrefabUtility.RecordPrefabInstancePropertyModifications(health);
                wired++;
            }
            Log.AppendLine($"STAR HOOK: {Path(child)} ({(madeHook ? "MADE" : "found, reused")}{(madeHolder ? "; its holder was made too" : "")}): Needed {paths.Length} different parts in one visit; {wired} On Died listeners wired (each monster's Damageable -> FirePart(its number)). A later Build Objectives run finds the same hook and wiring.");
        }

        // ------------------------------------------------------------------------------------------------ the prizes (D-0223)

        /// <summary>The prizes already in the case: kept as they are (D-0229's claw game may take one out), reported.</summary>
        static void ReportPrizes(Transform holder)
        {
            var lines = new List<string>();
            int stayPut = 0, all = 0;
            foreach (DarkMonster m in holder.GetComponentsInChildren<DarkMonster>(true))
            {
                all++;
                if (m.Role == DarkMonster.Mode.StayPut) stayPut++;
                lines.Add($"{m.name} ({m.Role}, scale {m.transform.lossyScale.y:F2}{(m.isActiveAndEnabled ? "" : ", switched off")})");
            }
            Log.AppendLine($"PRIZES: KEPT as they are under Room/Bedroom/{PrizeHolder} (not moved, not re-made: D-0229's claw game may take one out): {all} found, {stayPut} Stay Put{(all > 0 ? ": " + string.Join(", ", lines) : "")}. Their prefab variants were updated in place, so each keeps its own settings; a prize never shows the glow, never moves and takes no hits; it idles in the dark and the starlight and is frozen in the wall switch's light.");
            if (stayPut < all) Log.AppendLine("PRIZE WARNING: a prize is not Stay Put: it would roam inside the case. Set its DarkMonster's Mode to Stay Put.");
        }

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
            Log.AppendLine($"PRIZES (first run: none were there): {count} under Room/Bedroom/{PrizeHolder} (its own object, not under ClawMachine: a re-run of Land Start Room Toys leaves them; later runs of this maker KEEP them){(used < 0.999f ? $"; made {used:F2} of Prize Tall to fit" : "")}. Stay Put: idling{(s.prizeGlint ? " with their eye glint" : "")} in the dark and the starlight, frozen in the wall switch's light. Every prize's box is wholly inside the glass, {margin * unit:F2} in from it, and touches nothing but the floor.");
            if (count < PrizesAtLeast) Log.AppendLine($"PRIZE WARNING: only {count} fit (6 to 8 wanted): lower MonsterSettings > Prize Tall / Prize Wide, delete Room/Bedroom/{PrizeHolder} and run again.");
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
