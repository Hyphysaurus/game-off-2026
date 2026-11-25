#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ShiftJam
{
    /// <summary>
    /// The starting room's two new toys (25 Nov), placed into the OPEN Greybox; it never opens or saves a scene.
    /// D-0218, his idea: the bedroom's star lamp, woken by the flashlight ("definitely the flashlight direction"): the
    /// hand-placed NightLamp (Night_Lamp_01, the pack's star-dome projector) gets a StarLamp, a LightTarget and a
    /// StarProjector (a turning field of star-cookie spot lights and a breathing glow, KeepLit); awake, it lifts the dark
    /// room to a soft starlight (DarkZone.Soften). Saved per slot (bedroom:stars).
    /// D-0219, his "might as well drop the claw machine into the starting room as well": the free ithappy Claw Machine as a
    /// PROP. Its whole-machine mesh prefab (Toy_Claw_01: meshes only, none of the pack's scripts, lights or cameras), sized
    /// against the room's real door, on a free floor spot clear of the toy box -> beam -> core -> switch -> shelf switch ->
    /// toy door route, with mesh colliders on every part (a toy stands and climbs on its real shape). NOT the pack's
    /// PlayableClawMachine: that is its demo set (its own camera, HUD, studio lights, backdrop and music, and the Claw*
    /// scripts that read the keyboard and mouse).
    /// Also reports D-0217's numbers: where the hallway's Flat Zone puts a toy stuck where he was (x -18, z -143.8).
    /// Menu: Shift Jam / House / Land Start Room Toys (D-0218, D-0219). A second run replaces what the first placed.
    /// </summary>
    public static partial class CourseBuilder
    {
        const string StartTag = "[Start Room]";
        /// <summary>The star lamp, remembered per save slot (SaveGame seen list).</summary>
        public const string BedroomStarsKey = "bedroom:stars";
        const string NightLampPath = Pack + "/Prefabs/Other/Night_Lamp_01.prefab";      // the star dome (its icon: a starry dome with a moon)
        const string StarCookiePath = Pack + "/Textures/Stars_BaseColor.png";           // coloured stars and a moon on black, 2048
        const string StarWakePath = Pack + "/Playroom_Sound_FX/Toys/Star_Light_On.wav";
        const string ClawMachinePath = "Assets/ithappy/Claw_Machine_Free/Prefabs/Toy_Claw/Toy_Claw_01.prefab";
        const string HallZoneName = "FlatZone_Hall";

        // ---- the star lamp (D-0218)
        const int StarSides = 4;                 // spots round the dome, a quarter turn apart, plus one straight up
        const float StarSideAngle = 90f, StarUpAngle = 110f, StarInnerShare = 0.85f;
        // The side spots look this far over the level: their cone's lower edge 3 degrees over it. The spots cast no
        // shadows (light goes through walls), so no star may point down: from the bedside (dome about 40 up) nothing
        // reaches the toy hallway beyond the south wall (under y 14) or the floor under the bed.
        const float StarTilt = StarSideAngle * 0.5f + 3f;
        const float StarBright = 0.4f, StarBrightAt = 100f;   // each spot's light where it lands 100 away (half the room): intensity 0.4 x 100 squared
        const float StarRange = 260f;            // most of the room from the bedside (the room is 270 across); short of the playroom
        const float GlowBright = 0.6f;           // the sleeping glow's light one lamp-height from the dome
        const float GlowRangeLamps = 3f;         // its reach, in lamp heights
        static readonly Color GlowColour = new Color(0.72f, 0.6f, 1f);   // the dome's soft violet
        const float StarFillSeconds = 0.5f, StarDrainSeconds = 1f;      // his "about 0.5 s", any light
        const float StarWakeVolume = 0.6f;

        // ---- the claw machine (D-0219)
        const float ClawOverDoor = 0.85f;        // a 1.8 m arcade claw machine beside a 2.1 m door
        const float ClawFallbackTall = 85f;      // no RealDoor found: about 1.35 kids (63) tall
        const float ClawWallGap = 4f;            // the free test's margin round its footprint
        const float ClawRouteClear = 25f;        // at least this far from every piece of the route (a kid is 21 across)
        const float ClawLedgeUp = 0.7f;          // a surface a toy stands on faces up at least this much (45 degrees)
        // Floor spots tried in turn (the bedroom's north half: the route keeps to the toy box, the south wall's switch and
        // doors and the west wall's shelf switch). The first that is free is used.
        static readonly Vector2[] ClawSpots = { new Vector2(-95f, 95f), new Vector2(-55f, 105f), new Vector2(95f, 100f), new Vector2(-105f, 60f), new Vector2(-70f, 70f) };
        static readonly string[] RouteParts = { "Bed", "BedsideTable", "NightLamp", "StepBox", "StepBook_1", "StepBook_2", "StepBook_3", "Shelf", "ShelfSwitch", "ToyDoor", "RealDoor", "DoorGlow", "Window", "ShiftCore2", "DarkBedroom/LightSwitch" };

        [MenuItem("Shift Jam/House/Land Start Room Toys (D-0218, D-0219)")]
        public static void LandStartRoomMenu()
        {
            string report = LandStartRoom();
            if (report.StartsWith(StartTag + " NOT ")) Debug.LogWarning(report); else Debug.Log(report);
        }

        /// <summary>
        /// Places the star lamp's parts (D-0218) and the claw machine (D-0219) into the OPEN Greybox and marks it changed; it
        /// does not save it (Ctrl+S keeps it). A second run replaces the StarProjector and the ClawMachine it placed (no
        /// duplicates) and wires the lamp again; the StarLamp's own numbers (spin, starlight, glow) keep what he set.
        /// Returns the report (one fact a line). A report that starts "[Start Room] NOT " means nothing was changed.
        /// </summary>
        public static string LandStartRoom()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return $"{StartTag} NOT landed: this does not run in Play. Stop the game first. Nothing was changed.";
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded || scene.path != ScenePath)
                return $"{StartTag} NOT landed: the active scene is '{scene.path}', and the bedroom is in {ScenePath}. Open Greybox and run this again (it never opens a scene by itself). Nothing was changed.";
            var bedroomGo = GameObject.Find("Room/Bedroom");
            if (bedroomGo == null) return $"{StartTag} NOT landed: no Room/Bedroom in {ScenePath}. Nothing was changed.";
            Transform bedroom = bedroomGo.transform;

            Log.Clear();
            bool again = bedroom.Find("ClawMachine") != null || FindDeep(bedroom, "StarProjector") != null;
            Log.AppendLine($"{StartTag} Land Start Room Toys (D-0218 star lamp, D-0219 claw machine): {(again ? "SECOND run: what the first placed is replaced" : "first run")}. Greybox is MARKED CHANGED and NOT SAVED: Ctrl+S keeps it.");
            Physics.SyncTransforms();

            var shift = AssetDatabase.LoadAssetAtPath<ShiftSettings>(ShiftSettingsPath);
            var moves = AssetDatabase.LoadAssetAtPath<MovementSettings>(MovementSettingsPath);
            float toyS = shift != null ? shift.toyScale : 1f, kidS = shift != null ? shift.kidScale : 35f;
            float jump = (moves != null ? moves.jumpHeight : 2f) * toyS;
            GameObject player = GameObject.Find("Player");
            CharacterController body = player != null ? player.GetComponent<CharacterController>() : null;

            LandStarLamp(bedroom);
            LandClawMachine(bedroom, body, toyS, kidS, jump);
            BooksSpot(player, body);

            EditorSceneManager.MarkSceneDirty(scene);
            Log.AppendLine("Greybox MARKED CHANGED, not saved. Online: the lamp is not networked, so each player's stars are their own; the claw machine is a still prop.");
            return Log.ToString();
        }

        // ------------------------------------------------------------------------------------------------ D-0218

        /// <summary>The star lamp: the bedroom's own NightLamp (or, when there is none, the star dome placed on the
        /// bedside table), its projector, glow, LightTarget and StarLamp.</summary>
        static void LandStarLamp(Transform bedroom)
        {
            Transform lamp = bedroom.Find("NightLamp");
            string lampFrom = lamp != null ? $"Room/Bedroom/NightLamp FOUND ({SourceOf(lamp.gameObject)})" : null;
            if (lamp == null)
                foreach (Transform t in bedroom)
                    if (SourceOf(t.gameObject).Contains("/Night_Lamp_0")) { lamp = t; lampFrom = $"{t.name} FOUND ({SourceOf(t.gameObject)})"; break; }
            if (lamp == null)
            {
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(NightLampPath);
                Transform table = bedroom.Find("BedsideTable");
                if (asset == null || table == null) { Log.AppendLine($"STAR LAMP NOT placed: no night lamp in the bedroom and {(asset == null ? "no " + NightLampPath : "no BedsideTable to stand one on")}. The room is as it was."); return; }
                Bounds tb = B(table.gameObject);
                var go = (GameObject)PrefabUtility.InstantiatePrefab(asset, bedroom);
                go.name = "NightLamp";
                go.transform.SetPositionAndRotation(new Vector3(tb.center.x, tb.max.y, tb.center.z), Quaternion.Euler(0f, -90f, 0f));
                go.transform.position += Vector3.up * (tb.max.y + 0.01f - B(go).min.y);
                lamp = go.transform;
                lampFrom = $"NO night lamp in the bedroom: Night_Lamp_01 (the star dome: the pack's only lamp that IS a star projector) PLACED on the BedsideTable's top at {lamp.position}";
            }
            if (!SourceOf(lamp.gameObject).EndsWith("Night_Lamp_01.prefab")) lampFrom += ": NOT the star dome (Night_Lamp_01); used as it is";
            Log.AppendLine($"STAR LAMP: {lampFrom}. It stays where it was placed (by hand: no builder places it).");

            Bounds lb = B(lamp.gameObject);
            Transform sphere = FindDeep(lamp, "Night_Lamp_Sphere_01");
            Vector3 dome = sphere != null ? B(sphere.gameObject).center : lb.center + Vector3.up * (lb.extents.y * 0.4f);
            float tall = Mathf.Max(0.1f, lb.size.y);
            float floorY = Physics.Raycast(new Vector3(dome.x, lb.min.y - 0.5f, dome.z), Vector3.down, out RaycastHit under, 200f, ~0, QueryTriggerInteraction.Ignore) ? under.point.y : 0f;
            Log.AppendLine($"LAMP SIZE: {lb.size.x:F1} x {lb.size.y:F1} x {lb.size.z:F1}, standing on y {lb.min.y:F1}; the dome's middle {(sphere != null ? "(Night_Lamp_Sphere_01)" : "(no Night_Lamp_Sphere_01 found: estimated)")} at {dome}, {dome.y - floorY:F1} over the floor under it.");

            // The projector: one KeepLit over every light it makes (no Dark Zone dims them).
            Remove(lamp, "StarProjector");
            var proj = new GameObject("StarProjector").transform;
            proj.SetParent(lamp, true);
            proj.SetPositionAndRotation(dome, Quaternion.identity);
            proj.gameObject.AddComponent<KeepLit>();
            var point = new GameObject("LightPoint").transform;
            point.SetParent(proj, false);

            var glowGo = new GameObject("Glow");
            glowGo.transform.SetParent(proj, false);
            var glow = glowGo.AddComponent<Light>();
            glow.type = LightType.Point; glow.color = GlowColour; glow.shadows = LightShadows.None;
            glow.range = tall * GlowRangeLamps; glow.intensity = GlowBright * tall * tall;

            var cookie = AssetDatabase.LoadAssetAtPath<Texture2D>(StarCookiePath);
            var spinner = new GameObject("Stars").transform;
            spinner.SetParent(proj, false);
            var stars = new List<Light>();
            float bright = StarBright * StarBrightAt * StarBrightAt;
            stars.Add(StarSpot(spinner, "StarsUp", Quaternion.LookRotation(Vector3.up, Vector3.forward), StarUpAngle, bright, cookie));
            for (int i = 0; i < StarSides; i++)
                stars.Add(StarSpot(spinner, "Stars" + i, Quaternion.Euler(-StarTilt, i * 360f / StarSides, 0f), StarSideAngle, bright, cookie));
            Log.AppendLine($"STARS: {stars.Count} spot lights under NightLamp/StarProjector/Stars (one straight up at {StarUpAngle} degrees, {StarSides} round the dome {StarTilt} degrees over the level at {StarSideAngle}), each {bright:F0} bright (about {StarBright} where it lands {StarBrightAt} away; {bright / (22f * 22f):F1} on the east wall 22 from the bedside), reach {StarRange}, no shadows (none points down: nothing reaches past the walls under the dome's height), "
                + (cookie != null ? $"the cookie {StarCookiePath} ({cookie.width}x{cookie.height}, the pack's own picture, used in place: nothing copied out of the git-ignored folder; URP's cookie atlas is ON at 2048)" : $"NO {StarCookiePath}: the spots are plain colourless light (no stars): check the pack")
                + ". Off as placed: the StarLamp switches them on when it wakes. Turning at the StarLamp's Spin Speed.");
            Log.AppendLine($"GLOW: NightLamp/StarProjector/Glow, a soft violet point at the dome, {glow.intensity:F1} bright (about {GlowBright} one lamp height away), reach {glow.range:F1}: breathing while asleep, 3 times as bright and steady awake (StarLamp Glow Awake).");

            // What wakes it, and the lamp's own part.
            var target = Target(lamp.gameObject, point, StarFillSeconds, LightTarget.FocusRule.Any, StarDrainSeconds, true);
            if (!lamp.TryGetComponent(out StarLamp starLamp)) starLamp = lamp.gameObject.AddComponent<StarLamp>();
            Transform zoneT = bedroom.Find("DarkBedroom/BedroomDark");
            DarkZone zone = zoneT != null ? zoneT.GetComponent<DarkZone>() : null;
            var so = new SerializedObject(starLamp);
            so.FindProperty("key").stringValue = BedroomStarsKey;
            so.FindProperty("target").objectReferenceValue = target;
            so.FindProperty("zone").objectReferenceValue = zone;
            so.FindProperty("spinner").objectReferenceValue = spinner;
            so.FindProperty("glow").objectReferenceValue = glow;
            var list = so.FindProperty("stars");
            list.arraySize = stars.Count;
            for (int i = 0; i < stars.Count; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = stars[i];
            var wake = AssetDatabase.LoadAssetAtPath<AudioClip>(StarWakePath);
            so.FindProperty("wakeSound.clip").objectReferenceValue = wake;
            if (so.FindProperty("wakeSound.volume").floatValue <= 0f) so.FindProperty("wakeSound.volume").floatValue = StarWakeVolume;
            so.ApplyModifiedPropertiesWithoutUndo();
            var now = new SerializedObject(starLamp);
            Log.AppendLine($"WAKES IT: a LightTarget on NightLamp (any light, {StarFillSeconds} s to fill, drains over {StarDrainSeconds} s when the light leaves first; its point the dome's middle). StarLamp on NightLamp: save key '{BedroomStarsKey}' (per slot: Continue and a respawn find it awake, New Game asleep), the room's zone {(zone != null ? "BedroomDark (lifted to lamps " + now.FindProperty("lampsTo").floatValue + ", ambient " + now.FindProperty("ambientTo").floatValue + " while awake: the dark room is 0.08 / 0.14, lights-on 1 / 1)" : "NOT FOUND (run Land Dark Bedroom first): the stars come up but the room is not lifted")}, spin {now.FindProperty("spinSpeed").floatValue} degrees a second, rise {now.FindProperty("wakeSeconds").floatValue} s, {now.FindProperty("inLitRoom").floatValue} of the stars once the room is lit, wake sound {(wake != null ? wake.name + " (" + wake.length.ToString("F2") + " s)" : "NONE (no " + StarWakePath + ")")}.");

            // Can a flashlight reach it? (the beam's own reach as placed; a kid's light reaches 35 times a toy's)
            Transform bedTorch = bedroom.Find("UnderBed/BedFlashlight");
            Flashlight torch = bedTorch != null ? bedTorch.GetComponent<Flashlight>() : null;
            Transform bed = bedroom.Find("Bed");
            string reach = torch != null && torch.Beam != null ? $"the toy's own flashlight (UnderBed/BedFlashlight) reaches {torch.Beam.range:F1} as placed" : "no UnderBed/BedFlashlight found to measure";
            if (bed != null)
            {
                Bounds bb = B(bed.gameObject);
                Vector3 edge = new Vector3(Mathf.Clamp(dome.x, bb.min.x, bb.max.x), bb.max.y, Mathf.Clamp(dome.z, bb.min.z, bb.max.z));
                reach += $"; the dome is {Vector3.Distance(edge, dome):F1} from the bed's nearest top point ({edge}; the bed's top y {bb.max.y:F1}, its frame's highest point, maybe the headboard)";
            }
            Log.AppendLine($"REACH: {reach}; from the floor under the bedside table its top is in the way. A kid holding a light stands {dome.y - floorY:F1} under the dome's height easily. Not measured in Play.");
        }

        /// <summary>One of the projector's spots: no shadows, the star cookie, off (the StarLamp turns it on).</summary>
        static Light StarSpot(Transform spinner, string name, Quaternion look, float angle, float bright, Texture2D cookie)
        {
            var go = new GameObject(name);
            go.transform.SetParent(spinner, false);
            go.transform.localRotation = look;
            var l = go.AddComponent<Light>();
            l.type = LightType.Spot; l.spotAngle = angle; l.innerSpotAngle = angle * StarInnerShare;
            l.range = StarRange; l.intensity = bright; l.color = Color.white; l.shadows = LightShadows.None;
            l.cookie = cookie;
            l.enabled = false;
            return l;
        }

        /// <summary>The asset a placed prefab came from ("" for a plain object).</summary>
        static string SourceOf(GameObject go)
        {
            Object source = PrefabUtility.GetCorrespondingObjectFromSource(go);
            return source != null ? AssetDatabase.GetAssetPath(source) : "";
        }

        // ------------------------------------------------------------------------------------------------ D-0219

        /// <summary>The claw machine prop: the whole-machine mesh prefab, sized against the real door, on the first free
        /// floor spot clear of the route, mesh colliders on every part.</summary>
        static void LandClawMachine(Transform bedroom, CharacterController body, float toyS, float kidS, float jump)
        {
            Remove(bedroom, "ClawMachine");
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(ClawMachinePath);
            if (asset == null) { Log.AppendLine($"CLAW MACHINE NOT placed: no {ClawMachinePath} (the free ithappy Claw Machine pack, git-ignored: import it on this PC)."); return; }

            var holder = new GameObject("ClawMachine").transform;
            holder.SetParent(bedroom, false);
            holder.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            var machine = (GameObject)PrefabUtility.InstantiatePrefab(asset, holder);
            machine.name = "Toy_Claw_01";
            machine.transform.localPosition = Vector3.zero; machine.transform.localRotation = Quaternion.identity; machine.transform.localScale = Vector3.one;

            // A prop: nothing of the pack's may read input or run. The mesh prefab has none; anything found is switched off.
            var off = new List<string>();
            foreach (var mb in machine.GetComponentsInChildren<MonoBehaviour>(true)) if (mb != null && mb.enabled) { mb.enabled = false; off.Add(mb.GetType().FullName); }
            foreach (var c in machine.GetComponentsInChildren<Camera>(true)) if (c.enabled) { c.enabled = false; off.Add("Camera"); }
            foreach (var a in machine.GetComponentsInChildren<AudioSource>(true)) if (a.enabled) { a.enabled = false; off.Add("AudioSource"); }
            foreach (var a in machine.GetComponentsInChildren<Animator>(true)) if (a.enabled) { a.enabled = false; off.Add("Animator"); }
            foreach (var rb in machine.GetComponentsInChildren<Rigidbody>(true)) if (!rb.isKinematic) { rb.isKinematic = true; off.Add("Rigidbody (made kinematic)"); }
            var lights = machine.GetComponentsInChildren<Light>(true);
            Log.AppendLine($"CLAW MACHINE: {ClawMachinePath} (the whole machine as meshes: cabinet, glass, claw, lamps, lever, button, logo). The pack's PlayableClawMachine is NOT placed (its demo set: camera, HUD canvas, studio lights, backdrop, music, and ClawMachine / ClawInput / ClawHud / ClawMechanism and the rest, which read the keyboard and mouse with the old Input class: this project is Input System only, so they would also throw every frame). "
                + (off.Count == 0 ? "On the placed copy: NO scripts, cameras, sounds or animators (nothing to switch off)." : "Switched OFF on the placed copy: " + string.Join(", ", off) + ".")
                + $" Lights in it: {lights.Length}.");

            // Its size: against the room's real door (a claw machine is 1.8 m, a door 2.1).
            Bounds native = B(machine);
            Transform door = bedroom.Find("RealDoor");
            float doorTall = door != null ? B(door.gameObject).size.y : 0f;
            float tall = doorTall > 1f ? doorTall * ClawOverDoor : ClawFallbackTall;
            float scale = tall / Mathf.Max(0.01f, native.size.y);
            machine.transform.localScale = Vector3.one * scale;
            Bounds sized = B(machine);
            Transform bed = bedroom.Find("Bed");
            float bedTop = bed != null ? B(bed.gameObject).max.y : 0f;
            float kidTall = (body != null ? body.height : 1.8f) * kidS;
            Log.AppendLine($"CLAW SIZE: the pack's machine is {native.size.x:F2} x {native.size.y:F2} x {native.size.z:F2}; scaled {scale:F2} to {sized.size.x:F1} x {sized.size.y:F1} x {sized.size.z:F1}: "
                + (doorTall > 1f ? $"{ClawOverDoor} of the RealDoor's {doorTall:F1}" : $"NO RealDoor measured: {ClawFallbackTall}")
                + $". Beside it: a kid {kidTall:F0} tall, the bed's highest point {bedTop:F1}, a toy {(body != null ? body.height : 1.8f) * toyS:F1}.");

            // Its front: the lever and button side.
            Transform panel = FindDeep(machine.transform, "Lever_01");
            if (panel == null) panel = FindDeep(machine.transform, "Button_01");
            Vector3 front = Vector3.forward;
            if (panel != null) { Vector3 f = panel.position - sized.center; f.y = 0f; if (f.sqrMagnitude > 1e-6f) front = f.normalized; }

            // The first free floor spot, facing the room's middle.
            Bounds room = RoomBox(bedroom);
            var anchors = new List<(string, Bounds)>();
            foreach (string part in RouteParts) { Transform t = bedroom.Find(part); if (t != null) anchors.Add((part, B(t.gameObject))); }
            GameObject toyBox = GameObject.Find("Room/ToyBox");
            if (toyBox != null) anchors.Add(("ToyBox", B(toyBox)));
            var tried = new List<string>();
            bool placed = false;
            foreach (Vector2 spot in ClawSpots)
            {
                Vector3 at = new Vector3(spot.x, 0f, spot.y);
                Vector3 toMiddle = new Vector3(room.center.x - at.x, 0f, room.center.z - at.z);
                float yaw = toMiddle.sqrMagnitude > 1e-4f ? Vector3.SignedAngle(front, toMiddle.normalized, Vector3.up) : 0f;
                holder.SetPositionAndRotation(at, Quaternion.Euler(0f, yaw, 0f));
                float floorY = Physics.Raycast(at + Vector3.up * 60f, Vector3.down, out RaycastHit fl, 120f, ~0, QueryTriggerInteraction.Ignore) ? fl.point.y : 0f;
                Bounds b = B(machine);
                holder.position += new Vector3(at.x - b.center.x, floorY + 0.01f - b.min.y, at.z - b.center.z);
                Physics.SyncTransforms();
                b = B(machine);
                string why = Blocked(b, holder, anchors);
                if (why == null)
                {
                    placed = true;
                    Log.AppendLine($"CLAW PLACE: floor spot ({spot.x}, {spot.y}) " + (tried.Count > 0 ? "(tried first: " + string.Join("; ", tried) + ")" : "(the first tried)")
                        + $": footprint x {b.min.x:F1}..{b.max.x:F1}, z {b.min.z:F1}..{b.max.z:F1}, on the floor at y {floorY:F2}, turned {yaw:F0} degrees so its lever side faces the room's middle{(panel == null ? " (no Lever_01 / Button_01: its own forward taken as the front)" : "")}. At least {ClawRouteClear} from every route piece: " + RouteGaps(b, anchors) + ".");
                    break;
                }
                tried.Add($"({spot.x}, {spot.y}): {why}");
            }
            if (!placed)
            {
                Object.DestroyImmediate(holder.gameObject);
                Log.AppendLine("CLAW MACHINE NOT placed: no free spot. Tried " + string.Join("; ", tried) + ". Place Toy_Claw_01 by hand or add a spot to ClawSpots.");
                return;
            }

            // Colliders on its real shape: a toy stands on it and climbs what its parts allow.
            int colliders = 0;
            foreach (var mf in machine.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null || mf.GetComponent<Collider>() != null || mf.GetComponent<MeshRenderer>() == null) continue;
                var mc = mf.gameObject.AddComponent<MeshCollider>();
                mc.sharedMesh = mf.sharedMesh;
                colliders++;
            }
            Physics.SyncTransforms();
            Log.AppendLine($"CLAW COLLIDERS: {colliders} mesh colliders (its parts' own meshes, not convex, static). {Ledges(machine.transform, holder, jump)}");

            // Its materials: URP or not, and what glows in the dark room.
            var seen = new HashSet<Material>();
            var mats = new List<string>(); var glows = new List<string>(); var notUrp = new List<string>();
            foreach (var r in machine.GetComponentsInChildren<Renderer>(true))
                foreach (var m in r.sharedMaterials)
                {
                    if (m == null || !seen.Add(m)) continue;
                    string sh = m.shader != null ? m.shader.name : "NO SHADER";
                    mats.Add($"{m.name} ({sh})");
                    if (!(sh.StartsWith("Universal Render Pipeline/") || sh.StartsWith("Shader Graphs/"))) notUrp.Add(m.name);
                    if (m.IsKeywordEnabled("_EMISSION") && m.HasProperty("_EmissionColor") && m.GetColor("_EmissionColor").maxColorComponent > 0.01f)
                        glows.Add($"{m.name} (emission {m.GetColor("_EmissionColor").maxColorComponent:F2})");
                }
            Log.AppendLine($"CLAW MATERIALS ({mats.Count}): {string.Join("; ", mats)}. "
                + (notUrp.Count == 0 ? "All URP: nothing to convert (the pack's Render_Pipeline_Convert URP package is NOT needed)." : "NOT URP (they draw pink): " + string.Join(", ", notUrp) + ": import Render_Pipeline_Convert/Unity_6_URP_source.unitypackage."));
            Log.AppendLine(glows.Count == 0 ? "CLAW GLOW: no emissive material: it is as dark as the room." : $"CLAW GLOW: emissive (bright in the dark room: emission is not a light, the Dark Zone does not dim it, and it lights nothing round it): {string.Join("; ", glows)}.");
        }

        /// <summary>Why this footprint is not free (null when it is): a solid thing in it (walls, furniture; the floor and
        /// triggers aside), or a route piece nearer than Claw Route Clear.</summary>
        static string Blocked(Bounds b, Transform holder, List<(string, Bounds)> anchors)
        {
            var centre = new Vector3(b.center.x, (b.min.y + 1f + b.max.y) * 0.5f, b.center.z);
            var half = new Vector3(b.extents.x + ClawWallGap, Mathf.Max(0.1f, (b.size.y - 1f) * 0.5f), b.extents.z + ClawWallGap);
            foreach (Collider c in Physics.OverlapBox(centre, half, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore))
                if (!c.transform.IsChildOf(holder)) return $"{c.name} stands in it";
            foreach (var (name, a) in anchors)
                if (Gap(b, a) < ClawRouteClear) return $"{Gap(b, a):F1} from {name}";
            return null;
        }

        /// <summary>The level gap between two boxes (0 when they overlap seen from above).</summary>
        static float Gap(Bounds a, Bounds b)
        {
            float x = Mathf.Max(0f, Mathf.Max(a.min.x - b.max.x, b.min.x - a.max.x));
            float z = Mathf.Max(0f, Mathf.Max(a.min.z - b.max.z, b.min.z - a.max.z));
            return Mathf.Sqrt(x * x + z * z);
        }

        static string RouteGaps(Bounds b, List<(string, Bounds)> anchors)
        {
            var parts = new List<string>();
            foreach (var (name, a) in anchors) parts.Add($"{name} {Gap(b, a):F0}");
            return string.Join(", ", parts);
        }

        /// <summary>The heights a toy can stand on, over the machine's floor: rays straight down on a grid over its
        /// footprint, the up-facing surfaces of its own colliders, and what that means for a toy's jump.</summary>
        static string Ledges(Transform machine, Transform holder, float jump)
        {
            Bounds b = B(machine.gameObject);
            float floorY = b.min.y;
            float stepXZ = Mathf.Max(0.5f, Mathf.Max(b.size.x, b.size.z) / 24f);
            var heights = new SortedSet<float>();
            for (float x = b.min.x + stepXZ * 0.5f; x < b.max.x; x += stepXZ)
                for (float z = b.min.z + stepXZ * 0.5f; z < b.max.z; z += stepXZ)
                    foreach (RaycastHit h in Physics.RaycastAll(new Vector3(x, b.max.y + 1f, z), Vector3.down, b.size.y + 2f, ~0, QueryTriggerInteraction.Ignore))
                        if (h.collider.transform.IsChildOf(holder) && h.normal.y >= ClawLedgeUp) heights.Add(Mathf.Round((h.point.y - floorY) * 2f) / 2f);
            if (heights.Count == 0) return "NO up-facing surface found on it (nothing to stand on).";
            var list = new List<string>(); float lowest = float.MaxValue, prev = 0f, worst = 0f;
            foreach (float h in heights) { list.Add(h.ToString("F1")); lowest = Mathf.Min(lowest, h); worst = Mathf.Max(worst, h - prev); prev = h; }
            return $"Surfaces a toy can stand on, over the floor (to 0.5): {string.Join(", ", list)}. A toy jumps {jump:F1}: the lowest is {lowest:F1} ({(lowest <= jump ? "a toy hops onto it from the floor" : "OUT of a toy's jump from the floor: it needs a leg-up (a book, a block)")}); the biggest rise between one height and the next is {worst:F1} (the heights need not be next to each other: look in the Scene view).";
        }

        // ------------------------------------------------------------------------------------------------ D-0217's numbers

        /// <summary>Where the hallway's Flat Zone puts a toy stuck where he was (x -18, z -143.8, on the floor), by the
        /// zone's own tests (FlatZone.WayToLaneBlocked, LaneSpot), out of Play.</summary>
        static void BooksSpot(GameObject player, CharacterController body)
        {
            GameObject zoneGo = GameObject.Find(HallZoneName);
            FlatZone zone = zoneGo != null ? zoneGo.GetComponent<FlatZone>() : null;
            if (zone == null || body == null) { Log.AppendLine($"BOOKS SPOT (D-0217): not measured ({(zone == null ? "no " + HallZoneName : "no Player with a CharacterController")} in the scene)."); return; }
            float radius = body.radius, height = Mathf.Max(body.height, 2f * radius);   // the toy, at scale 1
            int mask = FlatZone.CollisionMaskOf(player.layer);
            var feet = new Vector3(-18f, 0f, -143.8f);
            FlatRail rail = zone.Rail;
            float off = Vector3.Dot(feet - rail.point, rail.normal);
            bool blocked = zone.WayToLaneBlocked(feet, radius, height, body.stepOffset, 1f, mask, player.transform, out float clear);
            bool found = zone.LaneSpot(feet, radius, height, body.slopeLimit, 1f, mask, player.transform, out Vector3 spot);
            Log.AppendLine($"BOOKS SPOT (D-0217, his stuck place {feet}, the toy's capsule radius {radius}, height {body.height}, step {body.stepOffset}): {Mathf.Abs(off):F2} off the lane (z {rail.point.z:F2}); the straight way in is {(blocked ? $"BLOCKED after {clear:F2}" : "clear")}; "
                + (found ? $"the spot on the lane: {spot} ({spot.y - feet.y:F2} over his feet): he is put there at once, facing along the lane." : "NO free spot on the lane within reach there: the old way (the lock's ease) for him.")
                + " (Expected from the builder's numbers: Book1's tread x -19.6 to -17.2, top 2.21.)");
        }
    }
}
#endif
