#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ShiftJam
{
    /// <summary>
    /// D-0155, his "what else we can intro in the very first bedroom... wasted potential", then "proceed with 1 and 2".
    /// Made only of parts, placed once into Greybox (then the scene is the truth). Safe to run again.
    /// 1. The toy box: its grow core starts dark; the giant flashlight lying lit beside the toy is pushable (a physics body
    ///    held level, Pushable Light), so the toy shoves it round until its beam lands on the core (LightTarget -> Awaken).
    ///    The light's first lesson, in the first minute.
    /// 2. Under the bed, a toy's cave: a DarkZone, a toy-sized flashlight (the player's own, earlier than the hallway's),
    ///    darts and a grenade crate, and a note on the wall only a light shows (LightTarget + LightReveal).
    /// Menu: Shift Jam / House / Land Bedroom Light Beats (D-0155).
    /// D-0214 / D-0215 (his playtest 25 Nov: "darken the ENTIRE room... perhaps they must navigate to ANOTHER switch to turn on
    /// the lights permanently", and "the EXTRA flashlight and EXTRA shift orbs inside of the hallway"): LandDarkBedroom below.
    /// </summary>
    public static partial class CourseBuilder
    {
        const string AmmoBoxPrefab = "Assets/_Game/Prefabs/Pieces/Weapons/AmmoBox.prefab";
        const string SupplyCratePrefab = "Assets/_Game/Prefabs/Pieces/Weapons/SupplyCrate.prefab";
        const string TurntablePrefab = "Assets/_Imports/ithappy/Battle_Stadium/Prefabs/Obstacles/obstacle_14_001.prefab";

        /// <summary>A child anywhere under <paramref name="root"/> by name (the light may already ride a turntable).</summary>
        static Transform FindDeep(Transform root, string name)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true)) if (t.name == name) return t;
            return null;
        }

        [MenuItem("Shift Jam/House/Land Bedroom Light Beats (D-0155)")]
        public static void LandBedroomLight()
        {
            Log.Clear();
            var active = SceneManager.GetActiveScene();
            var scene = active.path == ScenePath ? active : EditorSceneManager.OpenScene(ScenePath);
            var toyBox = GameObject.Find("Room/ToyBox");
            var bedroom = GameObject.Find("Room/Bedroom");
            if (toyBox == null || bedroom == null) { Debug.LogError("D-0155: no Room/ToyBox or Room/Bedroom"); return; }

            // 1. The giant flashlight rides a turntable (D-0158, his "C would be cool if we have the assets": Battle Stadium's
            // spinning disc, its own spin off). The toy grabs the rim (E) and turns it: look where the beam should go. The
            // disc only turns on its hub, so the light cannot slide, roll or jam. The box's core stays dark until lit.
            Transform torch = FindDeep(toyBox.transform, "Flashlight_01");
            var core = toyBox.transform.Find("ShiftCore") != null ? toyBox.transform.Find("ShiftCore").GetComponent<ShiftSource>() : null;
            Transform floor = toyBox.transform.Find("Floor");
            float floorTop = floor != null ? B(floor.gameObject).max.y : 1f;
            Vector3 hub = new Vector3(-1f, floorTop, -21f);
            if (torch != null) torch.SetParent(toyBox.transform, true);   // out of an old turntable before it goes
            foreach (string name in new[] { "Constructor_01", "Constructor_02" }) { Transform blk = FindDeep(toyBox.transform, name); if (blk != null) blk.SetParent(toyBox.transform, true); }
            Remove(toyBox.transform, "Turntable");
            foreach (var (name, at) in new[] { ("Constructor_01", new Vector3(-12f, 1f, -29f)), ("Constructor_02", new Vector3(-12f, 1f, -25.5f)) })
            {
                Transform block = toyBox.transform.Find(name);
                if (block != null) block.position = at;   // the toy blocks go to the back-left corner, out of the way
            }
            var table = new GameObject("Turntable").transform;
            table.SetParent(toyBox.transform, true);
            table.SetPositionAndRotation(hub, Quaternion.identity);
            var discAsset = AssetDatabase.LoadAssetAtPath<GameObject>(TurntablePrefab);
            if (discAsset != null && torch != null)
            {
                var disc = (GameObject)PrefabUtility.InstantiatePrefab(discAsset, table);
                disc.name = "Disc";
                float across = 14f;
                disc.transform.localScale = Vector3.one * (across / 8.5f);   // the pack's disc is 8.5 across: the 12-long light lies on it
                disc.transform.localPosition = Vector3.zero;
                disc.transform.position += Vector3.up * (floorTop - B(disc).min.y);
                foreach (var spin in disc.GetComponentsInChildren<MonoBehaviour>(true))
                    if (spin != null && spin.GetType().Name == "RotationScript") { var rso = new SerializedObject(spin); rso.FindProperty("rotationSpeed").floatValue = 0f; rso.ApplyModifiedPropertiesWithoutUndo(); }
                float top = B(disc).max.y;
                var body = table.gameObject.AddComponent<Rigidbody>();
                body.isKinematic = true; body.interpolation = RigidbodyInterpolation.Interpolate;
                // The light lies across the disc, riding it: no loose body of its own, no grab of its own.
                if (torch.TryGetComponent(out Grabbable oldGrab)) Object.DestroyImmediate(oldGrab);
                if (torch.TryGetComponent(out Pushable shove)) Object.DestroyImmediate(shove);
                if (torch.TryGetComponent(out Rigidbody loose)) Object.DestroyImmediate(loose);
                torch.SetParent(table, true);
                torch.rotation = Quaternion.Euler(0f, 33f, 0f);
                torch.position = new Vector3(hub.x, torch.position.y, hub.z);
                Bounds lb = B(torch.gameObject);
                torch.position += new Vector3(hub.x - lb.center.x, top + 0.02f - lb.min.y, hub.z - lb.center.z);
                var grab = table.gameObject.AddComponent<Grabbable>();
                var gso = new SerializedObject(grab);
                gso.FindProperty("prompt").stringValue = "Grab (A / D turn)";
                gso.FindProperty("anchored").boolValue = true;
                gso.FindProperty("canPush").boolValue = false;
                gso.FindProperty("turnSpeed").floatValue = 40f;
                gso.FindProperty("steer").enumValueIndex = (int)Grabbable.Steer.Stick;   // A / D turn it; nothing turns it by itself (D-0161)
                var torchLight = torch.GetComponent<Flashlight>();
                gso.FindProperty("aim").objectReferenceValue = torchLight != null && torchLight.Beam != null ? torchLight.Beam.transform : torch;
                gso.ApplyModifiedPropertiesWithoutUndo();
                // The two toy blocks prop its handle up (D-0160, his "the flashlight is situated in a suspended state... put the
                // two blocks underneath as support on the handle"): under the handle's two points, each as tall as the gap
                // there, riding the disc.
                Physics.SyncTransforms();
                Vector3 axis = torchLight != null && torchLight.Beam != null ? Vector3.ProjectOnPlane(torchLight.Beam.transform.forward, Vector3.up).normalized : torch.forward;
                Bounds tl = B(torch.gameObject);
                float halfLen = Mathf.Abs(Vector3.Dot(tl.extents, new Vector3(Mathf.Abs(axis.x), 0f, Mathf.Abs(axis.z))));
                Vector3 tail = new Vector3(tl.center.x, top, tl.center.z) - axis * halfLen;
                Collider torchCol = torch.GetComponent<Collider>();
                int b = 0;
                foreach (var (name, along) in new[] { ("Constructor_01", 0.18f), ("Constructor_02", 0.45f) })
                {
                    Transform block = toyBox.transform.Find(name) ?? FindDeep(table, name);
                    if (block == null || torchCol == null) continue;
                    Vector3 at = tail + axis * (halfLen * 2f * along);
                    if (!torchCol.Raycast(new Ray(at + Vector3.up * 0.01f, Vector3.up), out RaycastHit belly, 20f)) continue;
                    float gap = belly.point.y - top;
                    if (gap < 0.05f) continue;   // the light rests on the disc there already
                    block.SetParent(table, true);
                    block.rotation = Quaternion.LookRotation(Vector3.Cross(Vector3.up, axis), Vector3.up);   // across the handle
                    Bounds bb0 = B(block.gameObject);
                    block.localScale *= gap / Mathf.Max(0.01f, bb0.size.y);
                    Bounds bb1 = B(block.gameObject);
                    block.position += new Vector3(at.x - bb1.center.x, top - bb1.min.y, at.z - bb1.center.z);
                    b++;
                    Log.AppendLine($"{name}: under the handle at {block.position}, {gap:F2} tall");
                }
                Log.AppendLine($"turntable at {hub} ({across} across), the giant flashlight riding it at {torch.position}, {b} block(s) under its handle");
            }
            else Log.AppendLine("NO turntable prefab or NO Flashlight_01");
            if (core != null)
            {
                var so = new SerializedObject(core);
                so.FindProperty("startDormant").boolValue = true;
                so.FindProperty("dormantPrompt").stringValue = "Dark. Grab the turntable (E), turn the light onto this (A / D).";
                so.ApplyModifiedPropertiesWithoutUndo();
                Transform middle = core.transform.Find("LightPoint");
                if (middle == null) { middle = new GameObject("LightPoint").transform; middle.SetParent(core.transform, false); }
                middle.position = B(core.gameObject).center;
                var target = Target(core.gameObject, middle, 0.6f, LightTarget.FocusRule.Any, 0f, false);
                UnityEventTools.AddVoidPersistentListener(target.OnFilled, core.Awaken);
                Log.AppendLine("toy box core: starts dark; LightTarget, On Filled -> Awaken");
            }
            else Log.AppendLine("NO ShiftCore in the toy box");

            // 2. Under the bed.
            var bed = bedroom.transform.Find("Bed");
            if (bed == null) { Debug.LogError("D-0155: no Bed"); return; }
            Bounds bb = B(bed.gameObject);
            float under = 8.5f;   // the frame's underside is 8.6 to 9.8 above the floor
            Remove(bedroom.transform, "UnderBed");
            var cave = new GameObject("UnderBed").transform;
            cave.SetParent(bedroom.transform, true);
            cave.position = new Vector3(bb.center.x, 0f, bb.center.z);
            var zone = new GameObject("UnderBedDark").AddComponent<DarkZone>();
            zone.transform.SetParent(cave, false);
            zone.transform.localPosition = Vector3.up * (under * 0.5f);
            var zso = new SerializedObject(zone);
            zso.FindProperty("size").vector3Value = new Vector3(bb.size.x - 2f, under, bb.size.z - 2f);
            zso.FindProperty("lightsTo").floatValue = CaveLightsTo;     // under the dark room's (D-0214): the cave stays the darkest
            zso.FindProperty("ambientTo").floatValue = CaveAmbientTo;   // was 0.15 (D-0155)
            zso.ApplyModifiedPropertiesWithoutUndo();

            var torchAsset = AssetDatabase.LoadAssetAtPath<GameObject>(FlashlightPrefabBuilder.PrefabPath);
            var shift = AssetDatabase.LoadAssetAtPath<ShiftSettings>(ShiftSettingsPath);
            float toy = shift != null && shift.kidScale > 0f ? shift.toyScale / shift.kidScale : 1f;
            var mine = (GameObject)PrefabUtility.InstantiatePrefab(torchAsset, cave);
            mine.name = "BedFlashlight";
            mine.transform.localScale = Vector3.one * toy;
            mine.transform.SetPositionAndRotation(new Vector3(bb.center.x + 10f, 0f, bb.center.z - 20f), Quaternion.Euler(0f, 130f, 0f));
            mine.transform.position += Vector3.up * (0.005f - B(mine).min.y);
            var cso = new SerializedObject(mine.GetComponent<Carryable>());
            cso.FindProperty("madeAt").enumValueIndex = (int)Carryable.MadeAt.Toy;
            cso.ApplyModifiedPropertiesWithoutUndo();
            Log.AppendLine($"BedFlashlight (toy-sized) at {mine.transform.position}");

            PlaceOnFloor(AmmoBoxPrefab, "BedAmmo", cave, new Vector3(bb.center.x - 4f, 0f, bb.center.z + 25f), 30f);
            PlaceOnFloor(SupplyCratePrefab, "BedGrenades", cave, new Vector3(bb.center.x + 20f, 0f, bb.center.z + 38f), -15f);

            // The note on the wall at the bed's head, shown only by a light.
            Vector3 from = new Vector3(bb.max.x - 6f, 3f, bb.center.z - 5f);
            if (Physics.Raycast(from, Vector3.right, out RaycastHit wall, 20f))
            {
                var note = TapeSign("BedNote", cave, wall.point - Vector3.right * 0.08f, wall.normal, "Lights out at 8!\n- Mom", 6f, 2.6f);
                note.transform.SetParent(cave, true);
                if (!note.TryGetComponent(out LightReveal reveal)) reveal = note.AddComponent<LightReveal>();
                var target = Target(note, note.transform, 0.3f, LightTarget.FocusRule.Any, 1.5f, true);
                while (target.OnFill.GetPersistentEventCount() > 0) UnityEventTools.RemovePersistentListener(target.OnFill, 0);
                UnityEventTools.AddPersistentListener(target.OnFill, reveal.Show);
                Log.AppendLine($"BedNote on the wall at {wall.point}: shown by a light");
            }
            else Log.AppendLine("NO wall found east of the bed for the note");

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("BEDROOM LIGHT BEATS (D-0155)\n" + Log);
        }

        // ---------------------------------------------------------------------------------------------------------------
        // D-0214 the dark bedroom and its lights; D-0215 the hallway's dark stretch as the no-sign test.
        // ---------------------------------------------------------------------------------------------------------------

        const string DarkTag = "[Dark Bedroom]";
        /// <summary>The bedroom's lights, remembered per save slot (SaveGame seen list).</summary>
        public const string BedroomLightsKey = "bedroom:lights";
        const float RoomLightsTo = 0.08f, RoomAmbientTo = 0.14f;   // his brief: lamps to about 0.08, an ambient floor of 0.12 to 0.15 (never 0)
        const float CaveLightsTo = 0.05f, CaveAmbientTo = 0.10f;   // the cave under the bed stays the darkest (the darkest zone wins)
        const float RoomRiseSeconds = 2.5f;                       // the lights-on rise (the playroom reveal's lights take 2.8)
        const float LightSwitchHeight = 40f;                      // its middle over the floor: 0.63 of a 63-tall kid (a wall switch on a grown-up)
        const float SwitchGlowIntensity = 12f, SwitchGlowRange = 16f, SwitchGlowOut = 3f;
        const float MoonIntensity = 1800f, MoonRange = 260f, MoonAngle = 75f, MoonInner = 35f, MoonAimOut = 95f, MoonAimSide = 30f;   // about 0.12 where it lands, near the dimmed lamp's 0.08
        static readonly Color SwitchGlowColour = new Color(1f, 0.84f, 0.6f);
        static readonly Color MoonColour = new Color(0.62f, 0.72f, 1f);
        const string SwitchPrefabPath = Pack + "/Prefabs/Other/Switch_01.prefab";             // the shelf switch's own model
        const string SleepHumPath = Pack + "/Playroom_Sound_FX/Toys/Star_Light_Glow_Loop.wav"; // 5.44 s, stereo, peak -16.4 dBFS (measured 25 Nov)
        const string OrbVisualPath = "Assets/_Game/Resources/FX/ShiftOrbVisual.prefab";
        const string MovementSettingsPath = "Assets/_Game/Data/MovementSettings.asset";
        // Measured in Greybox (25 Nov): used only when the piece is not found by name.
        static readonly Vector3 RoomMin = new Vector3(-135f, -1f, -135f), RoomMax = new Vector3(135f, 131f, 135f);
        static readonly Vector3 WindowAt = new Vector3(0f, 55f, 133.7f);
        const float RealDoorEastX = -32f;   // estimated: the kid-sized doorway at x -55, about 46 wide (the toy door's glow is 1.6 at 0.035)
        const float ToyDoorX = -15f, SouthWallFaceZ = -130f;

        [MenuItem("Shift Jam/House/Land Dark Bedroom + Hallway Test (D-0214, D-0215)")]
        public static void LandDarkBedroomMenu()
        {
            string report = LandDarkBedroom();
            if (report.StartsWith(DarkTag + " NOT ")) Debug.LogWarning(report); else Debug.Log(report);
        }

        /// <summary>
        /// Places D-0214 and D-0215 into the OPEN Greybox (it never opens a scene) and marks it changed; it does not save it
        /// (Ctrl+S keeps it). A second run replaces what the first placed (no duplicates) and sets the same numbers again.
        /// A, the bedroom: one Dark Zone over the whole room (Room/Bedroom/DarkBedroom/BedroomDark, its own object: the toy
        /// box's burst switches off only ToyBoxDark); the cave's zone set darker; KeepLit on the door's light; a light switch
        /// (the shelf switch's model, a kid's height, south wall beside the real door, "Lights", its own small glow); a cool
        /// moonlight through the window; RoomLights to tie them (save key bedroom:lights). The orb prefab's sleeping hum is
        /// filled once when empty (that prefab IS saved: an asset, not the scene).
        /// C, the hallway: the spare light's sign shows only without a light; the stretch's charge core says nothing until
        /// the player lingers by it (20 s); one extra sleeping charge orb off the lane past the pencil bridge.
        /// Returns the report (one fact a line). A report that starts "[Dark Bedroom] NOT " means nothing was changed.
        /// </summary>
        public static string LandDarkBedroom()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return $"{DarkTag} NOT landed: this does not run in Play. Stop the game first. Nothing was changed.";
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded || scene.path != ScenePath)
                return $"{DarkTag} NOT landed: the active scene is '{scene.path}', and the bedroom is in {ScenePath}. Open Greybox and run this again (it never opens a scene by itself). Nothing was changed.";
            var bedroomGo = GameObject.Find("Room/Bedroom");
            if (bedroomGo == null) return $"{DarkTag} NOT landed: no Room/Bedroom in {ScenePath}. Nothing was changed.";
            Transform bedroom = bedroomGo.transform;

            Log.Clear();
            bool again = bedroom.Find("DarkBedroom") != null;
            Log.AppendLine($"{DarkTag} Land Dark Bedroom + Hallway Test (D-0214, D-0215): {(again ? "SECOND run: what the first placed is replaced" : "first run")}. Greybox is MARKED CHANGED and NOT SAVED: Ctrl+S keeps it.");
            Physics.SyncTransforms();

            // ---- the orb's sleeping hum (an asset: the prefab every core wears)
            OrbHum();

            // ---- A1 the room's own Dark Zone
            Bounds room = RoomBox(bedroom);
            Remove(bedroom, "DarkBedroom");
            var root = new GameObject("DarkBedroom").transform;
            root.SetParent(bedroom, false);
            root.position = new Vector3(room.center.x, 0f, room.center.z);
            var zoneGo = new GameObject("BedroomDark");
            zoneGo.transform.SetParent(root, false);
            zoneGo.transform.position = room.center;
            var zone = zoneGo.AddComponent<DarkZone>();
            var zso = new SerializedObject(zone);
            zso.FindProperty("centre").vector3Value = Vector3.zero;
            zso.FindProperty("size").vector3Value = room.size;
            zso.FindProperty("lightsTo").floatValue = RoomLightsTo;
            zso.FindProperty("ambientTo").floatValue = RoomAmbientTo;
            zso.FindProperty("fade").floatValue = 0.6f;
            zso.FindProperty("reachingIn").boolValue = true;
            zso.FindProperty("dimSun").boolValue = true;
            zso.ApplyModifiedPropertiesWithoutUndo();
            Log.AppendLine($"ZONE BedroomDark: box x {room.min.x:F1}..{room.max.x:F1}, y {room.min.y:F1}..{room.max.y:F1}, z {room.min.z:F1}..{room.max.z:F1}; lamps to {RoomLightsTo}, ambient to {RoomAmbientTo} (the scene's trilight 0.30 / 0.24 / 0.12 becomes {0.30f * RoomAmbientTo:F3} / {0.24f * RoomAmbientTo:F3} / {0.12f * RoomAmbientTo:F3}), fade 0.6 s. Its own object: BoxBurst's On Burst still switches off only ToyBoxDark.");

            // ---- the other zones in the room: the darkest wins
            var toyBoxDark = GameObject.Find("ToyBoxDark");
            var boxZone = toyBoxDark != null ? toyBoxDark.GetComponent<DarkZone>() : null;
            Log.AppendLine(boxZone != null
                ? $"ZONE ToyBoxDark: not changed (lamps {boxZone.LightsTo}, ambient {boxZone.AmbientTo}; off at the box's burst). Inside the room the darker room zone wins."
                : "ZONE ToyBoxDark: NOT FOUND (nothing changed for it)");
            Transform caveT = bedroom.Find("UnderBed/UnderBedDark");
            var cave = caveT != null ? caveT.GetComponent<DarkZone>() : null;
            if (cave != null)
            {
                var cso = new SerializedObject(cave);
                float wasLights = cso.FindProperty("lightsTo").floatValue, wasAmbient = cso.FindProperty("ambientTo").floatValue;
                cso.FindProperty("lightsTo").floatValue = CaveLightsTo;
                cso.FindProperty("ambientTo").floatValue = CaveAmbientTo;
                cso.ApplyModifiedPropertiesWithoutUndo();
                Log.AppendLine($"ZONE UnderBedDark (the cave): lamps {wasLights} -> {CaveLightsTo}, ambient {wasAmbient} -> {CaveAmbientTo}: darker than the room, so under the bed it is darker still.");
            }
            else Log.AppendLine("ZONE UnderBedDark: NOT FOUND (run Land Bedroom Light Beats first for the cave)");

            // ---- A2 what still gives light: the door's light kept; the switch's glow and the moon are new
            Transform doorLight = bedroom.Find("DoorLight");
            if (doorLight != null)
            {
                bool had = doorLight.GetComponent<KeepLit>() != null;
                if (!had) doorLight.gameObject.AddComponent<KeepLit>();
                Log.AppendLine($"KEPT DoorLight: KeepLit {(had ? "was there" : "ADDED")} (it comes on as the toy door opens)");
            }
            else Log.AppendLine("DoorLight NOT FOUND");
            Log.AppendLine(bedroom.Find("DoorGlow") != null ? "KEPT DoorGlow: an unlit picture (Data/DoorGlow.mat), no light to dim" : "DoorGlow NOT FOUND");
            Switch lightSwitch = PlaceLightSwitch(bedroom, root, out Light switchGlow);
            Light moon = MoonFill(bedroom, root, room);

            // ---- A5 / A6 the lights' part
            var lights = root.gameObject.AddComponent<RoomLights>();
            var lso = new SerializedObject(lights);
            lso.FindProperty("key").stringValue = BedroomLightsKey;
            lso.FindProperty("zone").objectReferenceValue = zone;
            lso.FindProperty("lightSwitch").objectReferenceValue = lightSwitch;
            lso.FindProperty("riseSeconds").floatValue = RoomRiseSeconds;
            var darkOnly = lso.FindProperty("darkOnly");
            darkOnly.arraySize = switchGlow != null ? 1 : 0;
            if (switchGlow != null) darkOnly.GetArrayElementAtIndex(0).objectReferenceValue = switchGlow;
            lso.ApplyModifiedPropertiesWithoutUndo();
            Log.AppendLine($"ROOM LIGHTS on DarkBedroom: save key '{BedroomLightsKey}' (per slot; Continue and a respawn start lit, New Game dark), rise {RoomRiseSeconds} s, the switch {(lightSwitch != null ? "wired" : "MISSING: no way to turn them on, the room stays dark (the door still works)")}, the player's glow made at run time (toy: 1.2 bright, 6 reach, grown with the size; KeepLit).");

            // ---- the lights in and round the room, by the zone's own rule
            ReportLights(scene, zone, moon, switchGlow);

            // ---- C the hallway
            var r3 = GameObject.Find("Room/Course/R3_SkirtingRun");
            if (r3 != null) DarkStretchTest(r3.transform);
            else Log.AppendLine("HALL: NO Room/Course/R3_SkirtingRun: the hallway was not changed");

            EditorSceneManager.MarkSceneDirty(scene);
            Log.AppendLine("Greybox MARKED CHANGED, not saved. Online: switches are not networked, so the lights are each player's own.");
            return Log.ToString();
        }

        /// <summary>The room's box: its walls, pillars, floor and ceiling modules put together, one unit more all round. The
        /// measured box when none is found.</summary>
        static Bounds RoomBox(Transform bedroom)
        {
            bool any = false; Bounds box = default;
            foreach (Transform t in bedroom)
            {
                if (!(t.name.StartsWith("Wall_") || t.name.StartsWith("Pillar_") || t.name.StartsWith("Floor_") || t.name.StartsWith("Ceiling_"))) continue;
                Bounds part = B(t.gameObject);
                if (part.size == Vector3.zero) continue;
                if (!any) { box = part; any = true; } else box.Encapsulate(part);
            }
            if (!any)
            {
                box.SetMinMax(RoomMin, RoomMax);
                Log.AppendLine("ROOM: no Wall_ / Floor_ / Ceiling_ modules found under Room/Bedroom: the measured box is used");
                return box;
            }
            box.Expand(2f);
            return box;
        }

        /// <summary>The room's light switch (A5): the shelf switch's model on the south wall, just east of the real door, its
        /// middle a kid's height up (out of a toy's reach), with a small warm glow in front of it (KeepLit; shown only while dark).</summary>
        static Switch PlaceLightSwitch(Transform bedroom, Transform root, out Light glow)
        {
            glow = null;
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(SwitchPrefabPath);
            if (asset == null) { Log.AppendLine($"SWITCH: NO {SwitchPrefabPath}: no switch placed"); return null; }
            Transform real = bedroom.Find("RealDoorway"), toyDoor = bedroom.Find("ToyDoorway");
            float doorEast = RealDoorEastX;
            if (real != null) { Bounds rb = B(real.gameObject); if (rb.size != Vector3.zero) doorEast = rb.max.x; }
            float toyX = toyDoor != null ? toyDoor.position.x : ToyDoorX;
            float x = Mathf.Min(doorEast + 7f, toyX - 8f);   // beside the real door's frame, clear of the toy door
            Vector3 face = new Vector3(x, LightSwitchHeight, SouthWallFaceZ), normal = Vector3.forward;
            bool onWall = Physics.Raycast(new Vector3(x, LightSwitchHeight, SouthWallFaceZ + 40f), Vector3.back, out RaycastHit hit, 80f, ~0, QueryTriggerInteraction.Ignore);
            if (onWall)
            {
                face = hit.point;
                Vector3 n = new Vector3(hit.normal.x, 0f, hit.normal.z);
                if (n.sqrMagnitude > 0.01f) normal = n.normalized;
            }
            var go = (GameObject)PrefabUtility.InstantiatePrefab(asset, root);
            go.name = "LightSwitch";
            go.transform.SetPositionAndRotation(face, Quaternion.LookRotation(normal, Vector3.up));   // faces the room, as the shelf switch faces it from its wall
            Bounds sb = B(go);
            go.transform.position += Vector3.up * (LightSwitchHeight - sb.center.y);
            sb = B(go);
            float rear = Vector3.Dot(sb.center - face, normal) - (Mathf.Abs(sb.extents.x * normal.x) + Mathf.Abs(sb.extents.z * normal.z));
            go.transform.position += normal * (0.02f - rear);   // its back flat on the wall
            sb = B(go);

            var sw = go.AddComponent<Switch>();
            var so = new SerializedObject(sw);
            so.FindProperty("anySize").boolValue = false;
            so.FindProperty("requiredSize").intValue = (int)ShiftSize.Kid;
            so.FindProperty("once").boolValue = true;
            so.FindProperty("prompt").stringValue = "Lights";
            so.FindProperty("doors").arraySize = 0;
            so.ApplyModifiedPropertiesWithoutUndo();

            var glowGo = new GameObject("Glow");
            glowGo.transform.SetParent(go.transform, true);
            glowGo.transform.position = sb.center + normal * SwitchGlowOut;
            glowGo.AddComponent<KeepLit>();
            glow = glowGo.AddComponent<Light>();
            glow.type = LightType.Point; glow.range = SwitchGlowRange; glow.intensity = SwitchGlowIntensity; glow.color = SwitchGlowColour; glow.shadows = LightShadows.None;

            // The reach, from the assets: a toy can never touch it, a kid standing in front of it does.
            var moves = AssetDatabase.LoadAssetAtPath<MovementSettings>(MovementSettingsPath);
            var shift = AssetDatabase.LoadAssetAtPath<ShiftSettings>(ShiftSettingsPath);
            float jump = moves != null ? moves.jumpHeight : 2f;
            float toyS = shift != null ? shift.toyScale : 1f, kidS = shift != null ? shift.kidScale : 35f;
            float reach = 1.6f, height = 0.9f, body = 1.8f;
            var hand = Object.FindFirstObjectByType<Interactor>(FindObjectsInactive.Include);
            if (hand != null)
            {
                var iso = new SerializedObject(hand);
                reach = iso.FindProperty("reach").floatValue; height = iso.FindProperty("height").floatValue;
                var capsule = hand.GetComponent<CharacterController>();
                if (capsule != null) body = capsule.height;
            }
            float toyTop = (jump + Mathf.Max(body, height + reach * 0.5f)) * toyS;   // feet up a jump, then the taller of its body and its E search
            float kidRadius = reach * kidS * 0.5f;
            float kidLow = Mathf.Min(height * kidS, kidRadius * 0.8f) - kidRadius, kidHigh = height * kidS + kidRadius;
            Log.AppendLine($"SWITCH LightSwitch (Playroom Switch_01, as the shelf switch) on the {(onWall ? "south wall (found by a ray)" : "measured south wall face (NO wall hit)")} at x {x:F1} (the real door's frame ends at x {doorEast:F1}{(real == null ? ", estimated" : "")}; the toy door is at x {toyX:F1}), prompt \"E  Lights\" (the HUD puts the key first), a kid's hand only, once.");
            Log.AppendLine($"SWITCH HEIGHT: its middle {sb.center.y:F1} up, from {sb.min.y:F1} to {sb.max.y:F1}. A toy (x{toyS:G3}): body {body * toyS:F1}, jump {jump * toyS:F1}, its E search tops out {(height + reach * 0.5f) * toyS:F1} over its feet: it touches nothing over {toyTop:F1}, so the switch is {sb.min.y - toyTop:F1} over a toy's highest reach (a first-person crouch-jump adds 0.9: still {sb.min.y - toyTop - 0.9f:F1} short). A kid (x{kidS:F0}): its E search reaches from {kidLow:F1} to {kidHigh:F1} over its feet and {kidRadius * 2f:F0} ahead: in easy reach standing in front of it.");
            Log.AppendLine($"KEPT (new) LightSwitch/Glow: point {SwitchGlowIntensity}, reach {SwitchGlowRange}, warm, {SwitchGlowOut} in front of the switch, KeepLit; RoomLights shows it only while the room is dark.");
            return sw;
        }

        /// <summary>The soft cool fill (A2): moonlight through the bedroom's window (the room HAS one: Window_01 in the north
        /// wall), a wide spot from just inside it down onto the floor, KeepLit. It stays when the lamps come on (the moon).</summary>
        static Light MoonFill(Transform bedroom, Transform root, Bounds room)
        {
            Transform window = bedroom.Find("Window");
            Vector3 w = WindowAt;
            if (window != null) { Bounds wb = B(window.gameObject); if (wb.size != Vector3.zero) w = wb.center; }
            Vector3 inward = new Vector3(room.center.x - w.x, 0f, room.center.z - w.z);
            inward = inward.sqrMagnitude > 0.01f ? inward.normalized : Vector3.back;
            var go = new GameObject("MoonFill");
            go.transform.SetParent(root, false);
            go.transform.position = w + inward * 8f + Vector3.up * 10f;
            // It casts no shadow, so it would light the floor under the bed through the bed: aimed to the side away from
            // the bed, the cone's edge stops short of it and the cave stays the darkest.
            Vector3 side = Vector3.Cross(Vector3.up, inward);
            Transform bed = bedroom.Find("Bed");
            float away = -MoonAimSide;
            if (bed != null) away = Vector3.Dot(B(bed.gameObject).center - w, side) > 0f ? -MoonAimSide : MoonAimSide;
            Vector3 aim = new Vector3(w.x, 0f, w.z) + inward * MoonAimOut + side * away;
            go.transform.rotation = Quaternion.LookRotation(aim - go.transform.position, Vector3.up);
            go.AddComponent<KeepLit>();
            var l = go.AddComponent<Light>();
            l.type = LightType.Spot; l.range = MoonRange; l.spotAngle = MoonAngle; l.innerSpotAngle = MoonInner;
            l.intensity = MoonIntensity; l.color = MoonColour; l.shadows = LightShadows.None;
            Log.AppendLine($"KEPT (new) MoonFill: a cool spot through the window ({(window != null ? "Window found" : "Window NOT FOUND: the measured place")} at {w}), from {go.transform.position} onto the floor at {aim}, {MoonIntensity} bright (about {MoonIntensity / (aim - go.transform.position).sqrMagnitude:F2} where it lands), {MoonAngle} degrees, reach {MoonRange}, no shadows, KeepLit. Chosen over a plug-in nightlight: the window is there and needs no new prop.");
            return l;
        }

        /// <summary>Every light in the scene the room's zone dims, and every light it keeps that reaches the room.</summary>
        static void ReportLights(Scene scene, DarkZone zone, Light moon, Light switchGlow)
        {
            var torches = new HashSet<Light>();
            foreach (var f in Object.FindObjectsByType<Flashlight>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                foreach (var l in f.GetComponentsInChildren<Light>(true)) torches.Add(l);
            var dimmed = new List<string>(); var kept = new List<string>(); int beyond = 0;
            foreach (Light l in Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (l.gameObject.scene != scene || l == moon || l == switchGlow) continue;
                bool near = l.type == LightType.Directional || zone.Contains(l.transform.position) || zone.Gap(l.transform.position) < l.range;
                string what = $"{PathOf(l.transform)} ({l.type}, {l.intensity:G4}, reach {l.range:G4}{(l.gameObject.activeInHierarchy ? "" : ", off for now")})";
                if (torches.Contains(l)) { if (near) kept.Add(what + ": a flashlight's beam"); continue; }
                if (DarkZone.Guides(l)) { if (near) kept.Add(what + ": a guide (checkpoint, core or KeepLit)"); continue; }
                if (zone.Dims(l)) dimmed.Add(what);
                else beyond++;
            }
            Log.AppendLine($"DIMMED to {RoomLightsTo} while the view is in the room ({dimmed.Count}): " + string.Join("; ", dimmed) + ".");
            Log.AppendLine($"KEPT LIT ({kept.Count} found in the scene now): " + string.Join("; ", kept) + ". Made at run time and kept too: every core's orb light (under its core), the player's glow (KeepLit). The ShelfSwitch's gold glint is a picture (no light): it shows as before.");
            Log.AppendLine($"Not reaching the room (left alone): {beyond} light(s).");
        }

        static string PathOf(Transform t)
        {
            var sb = new StringBuilder(t.name);
            for (Transform p = t.parent; p != null; p = p.parent) sb.Insert(0, p.name + "/");
            return sb.ToString();
        }

        /// <summary>The sleeping hum on the orb every core wears (A4): filled once when empty; a hum he sets stays.</summary>
        static void OrbHum()
        {
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(SleepHumPath);
            if (clip == null) { Log.AppendLine($"ORB HUM: NO {SleepHumPath}: the sleeping orbs breathe but stay silent"); return; }
            if (AssetDatabase.LoadAssetAtPath<GameObject>(OrbVisualPath) == null) { Log.AppendLine($"ORB HUM: NO {OrbVisualPath} (Shift Jam > Dev > Build Shift Orb): nothing set"); return; }
            GameObject prefab = PrefabUtility.LoadPrefabContents(OrbVisualPath);
            try
            {
                var orb = prefab.GetComponent<ShiftOrb>();
                if (orb == null) { Log.AppendLine($"ORB HUM: no ShiftOrb on {OrbVisualPath}: nothing set"); return; }
                var so = new SerializedObject(orb);
                var hum = so.FindProperty("sleepHum");
                if (hum.objectReferenceValue != null) { Log.AppendLine($"ORB HUM: kept {hum.objectReferenceValue.name} (already set on the orb prefab)"); return; }
                hum.objectReferenceValue = clip;
                so.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(prefab, OrbVisualPath);
                Log.AppendLine($"ORB HUM: {clip.name} ({clip.length:F2} s, {clip.channels} ch) SET on {OrbVisualPath} and that prefab SAVED. Unheard: his ears. Breath 0.45 a second, halo up to 2.6 times its dormant glow, light 1.5, hum 0.5 loud, full within 2 and silent past 14 of a toy (x9.5 for a kid).");
            }
            finally { PrefabUtility.UnloadPrefabContents(prefab); }
        }

        static void PlaceOnFloor(string prefabPath, string name, Transform parent, Vector3 at, float yaw)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (asset == null) { Log.AppendLine($"NO {prefabPath}"); return; }
            var go = (GameObject)PrefabUtility.InstantiatePrefab(asset, parent);
            go.name = name;
            go.transform.SetPositionAndRotation(at, Quaternion.Euler(0f, yaw, 0f));
            go.transform.position += Vector3.up * (0.005f - B(go).min.y);
            Log.AppendLine($"{name} at {go.transform.position}, {B(go).size}");
        }
    }
}
#endif
