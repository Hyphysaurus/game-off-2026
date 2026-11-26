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
    /// The start hub's new order (D-0241), landed into the OPEN Greybox; it never opens or saves a scene (Ctrl+S keeps it)
    /// and never edits CourseBuilder*.cs. Menu: Shift Jam / House / Land Bedroom Gate (D-0241). Run Land Bedroom Monsters
    /// first (it makes the cave's monsters this measures by).
    /// 1. Resources/BedroomGateSettings: made if missing; an existing asset keeps every number and word.
    /// 2. MonsterSettings: on the FIRST landing (no BedroomGate yet) Stay Down is turned off (his "they should always
    ///    respawn"); every run reports Never Calm, Light Stuns, Stay Down and the stun's numbers.
    /// 3. The gate: Room/Bedroom/BedroomGate (a BedroomGate), wired to BedMonsters, the shelf switch, the wall switch and
    ///    the claw machine, and each of those three given the gate as its Held Shut By.
    /// 4. The snatch rig beside it (Room/Bedroom/GunSnatch), its two guns made again each run: the gun out by the bed (Gun Out from the
    ///    monsters' cave mouth toward the toy box; the stand's own pistol model on its side, hidden until Play shows it) and
    ///    the dart gun under the bed (a DartPistolPickup that gives it once, toy-sized, on the cave floor point a kid's E
    ///    reaches least, clear of the monsters' way, their spots and the bed's other things; LightTarget + LightGlow: the
    ///    glint, both wearing an emissive copy of the pistol's material made under Art/Bedroom). The snatcher (the cave
    ///    monster nearest the mouth) and its way out and back, measured clear for its body.
    /// 5. The steps on Resources/ObjectiveSet: the box core's words (only while they are the old placeholder), "Find
    ///    what's under the bed" and "Knock out the monsters" after it (added once, his words kept), and the gold star
    ///    "monsters" given a Gate Opened rule (added once). The opening's second line: "Am I in a box?" (his words; only
    ///    while it is an old placeholder). The star lamp's sweep sound filled from its wake sound when empty.
    /// 6. Measured and reported: the gun under the bed reachable by the toy and not by the kid's E, the snatch's way clear,
    ///    what the star lamp's sweeping spots can reach (the bed's frame over the cave), every wire.
    /// A report that starts "[Bedroom Gate] NOT " changed nothing.
    /// </summary>
    public static class BedroomGateBuilder
    {
        const string Tag = "[Bedroom Gate]";
        const string ScenePath = "Assets/_Game/Scenes/Greybox.unity";
        const string SettingsFolder = "Assets/_Game/Resources";
        const string SettingsPath = SettingsFolder + "/" + BedroomGateSettings.ResourceName + ".asset";
        const string MonsterSettingsPath = SettingsFolder + "/" + MonsterSettings.ResourceName + ".asset";
        const string OpeningPath = SettingsFolder + "/OpeningSettings.asset";
        const string ShiftSettingsPath = "Assets/_Game/Data/ShiftSettings.asset";
        const string StandPrefabPath = "Assets/_Game/Prefabs/Pieces/Weapons/DartPistolPickup.prefab";
        const string GlintFolder = "Assets/_Game/Art/Bedroom";
        const string GlintMaterialPath = GlintFolder + "/DartGunGlint.mat";
        const string GateName = "BedroomGate", SnatchName = "GunSnatch", PropName = "SnatchGun", PickupName = "BedDartGun";
        const string GlintPointName = "GlintPoint", GunLightName = "GunLight", ModelName = "Model";
        // The old placeholder words this maker may replace (his own words are never touched).
        const string OldWakeBoxText = "Shine the flashlight on the box core";
        static readonly string[] OldGoalLines = { "I need to get to the end of the garden.", "I need to get back up on that window sill." };

        // Measuring.
        const float StandTrigger = 0.5f, StandTriggerUp = 0.7f;   // the stand prefab's own trigger sphere (radius, height over its root)
        const float DisplayUp = 0.7f;                              // the stand's floating display, over its root
        const float KidSpotStep = 4f;                              // toy units between the kid's standing spots tried round the bed
        const float KidSpotGap = 0.5f;                             // and their gap from the bed's footprint, past the kid's own radius

        static readonly StringBuilder Log = new StringBuilder();

        [MenuItem("Shift Jam/House/Land Bedroom Gate (D-0241)")]
        public static void LandMenu()
        {
            string report = Land();
            if (report.StartsWith(Tag + " NOT ")) Debug.LogWarning(report); else Debug.Log(report);
        }

        /// <summary>Lands everything into the OPEN Greybox and returns the report, one fact a line. A second run reuses the
        /// gate and the snatch rig, makes their two guns again, and never duplicates a step, a rule or a wire.</summary>
        public static string Land()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return $"{Tag} NOT landed: this does not run in Play. Stop the game first. Nothing was changed.";
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded || scene.path != ScenePath)
                return $"{Tag} NOT landed: the active scene is '{scene.path}', and the bedroom is in {ScenePath}. Open Greybox and run this again (it never opens a scene by itself). Nothing was changed.";
            GameObject bedroomGo = GameObject.Find("Room/Bedroom");
            if (bedroomGo == null) return $"{Tag} NOT landed: no Room/Bedroom in {ScenePath}. Nothing was changed.";
            Transform bedroom = bedroomGo.transform;
            Transform holder = bedroom.Find("BedMonsters");
            var monsters = new List<DarkMonster>();
            if (holder != null) foreach (DarkMonster m in holder.GetComponentsInChildren<DarkMonster>(true)) if (m.Role == DarkMonster.Mode.Roam) monsters.Add(m);
            if (monsters.Count == 0) return $"{Tag} NOT landed: no Roam monster under Room/Bedroom/BedMonsters. Run Shift Jam > House > Land Bedroom Monsters (D-0222, D-0223, D-0228) first, then this. Nothing was changed.";
            var standAsset = AssetDatabase.LoadAssetAtPath<GameObject>(StandPrefabPath);
            if (standAsset == null || standAsset.GetComponent<WeaponPickup>() == null) return $"{Tag} NOT landed: no weapon stand at {StandPrefabPath}. Nothing was changed.";

            Log.Clear();
            Transform gateT = bedroom.Find(GateName);
            bool first = gateT == null;
            Log.AppendLine($"{Tag} Land Bedroom Gate (D-0241, the start hub's new order): {(first ? "FIRST run" : "SECOND run: the gate and the snatch rig are reused, their two guns made again, nothing duplicated")}. Greybox is MARKED CHANGED and NOT SAVED: Ctrl+S keeps it.");
            Physics.SyncTransforms();

            BedroomGateSettings s = Settings();
            MonsterSettings ms = Monsters(first);
            ShiftSettings shift = AssetDatabase.LoadAssetAtPath<ShiftSettings>(ShiftSettingsPath);
            float toyScale = shift != null ? shift.toyScale : 1f, kidScale = shift != null ? shift.kidScale : 35f;

            // 3. The gate and its three locks.
            if (first)
            {
                gateT = new GameObject(GateName).transform;
                gateT.SetParent(bedroom, false);
            }
            var gate = gateT.GetComponent<BedroomGate>();
            if (gate == null) gate = gateT.gameObject.AddComponent<BedroomGate>();
            Switch shelf = Find<Switch>(bedroom, "ShelfSwitch");
            Switch wall = Find<Switch>(bedroom, "DarkBedroom/LightSwitch");
            Transform clawT = bedroom.Find("ClawMachine");
            ClawGame claw = clawT != null ? clawT.GetComponentInChildren<ClawGame>(true) : null;
            var gso = new SerializedObject(gate);
            gso.FindProperty("monsters").objectReferenceValue = holder;
            gso.FindProperty("shelfSwitch").objectReferenceValue = shelf;
            gso.FindProperty("lightSwitch").objectReferenceValue = wall;
            gso.FindProperty("claw").objectReferenceValue = claw;
            gso.ApplyModifiedPropertiesWithoutUndo();
            Log.AppendLine($"GATE: {Path(gateT)} ({(first ? "MADE" : "found, reused")}): BedroomGate waits for {monsters.Count} Roam monsters under {Path(holder)} ({Names(monsters)}), each knocked out once in a visit ({(s.dartsOnly ? "DARTS ONLY: the shot that finished it is heard in the same moment" : "any knockout counts")}); saved per slot as '{s.saveKey}'. Online: always open.");
            Lock(shelf, gate, "the shelf switch (the little door)", s.shelfLine);
            Lock(wall, gate, "the wall switch (the room's lights)", s.lightsLine);
            Lock(claw, gate, "the claw machine", s.clawLine);

            // 4. The snatch rig: beside the gate, not under it (the gate's object switched off must not take the gun with it).
            var snatchT = bedroom.Find(SnatchName);
            if (snatchT == null)
            {
                snatchT = new GameObject(SnatchName).transform;
                snatchT.SetParent(bedroom, false);
            }
            var snatch = snatchT.GetComponent<GunSnatch>();
            if (snatch == null) snatch = snatchT.gameObject.AddComponent<GunSnatch>();
            Remove(snatchT, PropName);
            Remove(snatchT, PickupName);
            Physics.SyncTransforms();
            Rig(bedroom, s, ms, monsters, snatch, standAsset, toyScale, kidScale);

            // 5. The words.
            Steps(scene, s, gateT, snatchT.Find(PickupName));
            GoalLine(s);
            Lamp(bedroom, s, ms, monsters, toyScale);
            KeepGlintEmission();   // D-0245: last, after everything else this run touches

            EditorSceneManager.MarkSceneDirty(scene);
            Log.AppendLine("Greybox MARKED CHANGED, not saved: Ctrl+S keeps it.");
            Log.AppendLine("OFF SWITCHES: BedroomGateSettings > On (the lock open), Darts Only (any knockout counts), Snatch On (no moment: the gun waits under the bed), Toy Only (anyone may take it); MonsterSettings > Never Calm (the D-0228 light rule), Light Stuns (the beam sends them running again), Stay Down (they stay down for the visit); NightLamp's StarLamp > Sweep On (the D-0218 lamp), Sweep Shadows (no shadows in a sweep). Untick BedroomGate: nothing is held. Untick GunSnatch: the gun simply waits under the bed, glinting, anyone takes it.");
            Log.AppendLine("TEST without leaving Play: the console's 'bedroom reset' forgets the lights, the lamp and the gate (the count starts again; the snatch and the steps stay). New Game plays everything from the box again.");
            Log.AppendLine("RUN THIS AGAIN after Land Bedroom Monsters, Land Start Room Toys or Land Claw Game: they make the monsters, the claw machine or its parts new, and the locks, the snatcher and the measured ways go with the old ones.");
            return Log.ToString();
        }

        // ------------------------------------------------------------------------------------------------ settings

        static BedroomGateSettings Settings()
        {
            var s = AssetDatabase.LoadAssetAtPath<BedroomGateSettings>(SettingsPath);
            bool fresh = s == null;
            if (fresh)
            {
                EnsureFolder(SettingsFolder);
                s = ScriptableObject.CreateInstance<BedroomGateSettings>();
                AssetDatabase.CreateAsset(s, SettingsPath);
                AssetDatabase.SaveAssetIfDirty(s);
            }
            Log.AppendLine($"SETTINGS: {SettingsPath} {(fresh ? "MADE with its defaults" : "FOUND: every number and word kept")}. Every word on it is a PLACEHOLDER for him to rewrite, apart from the goal line (his).");
            Log.AppendLine($"WORDS: shut prompt \"{s.shutPrompt}\"; shelf \"{s.shelfLine}\"; lights \"{s.lightsLine}\"; claw \"{s.clawLine}\"; count \"{s.countFormat}\"; not counted (once a slot) \"{s.notCountedLine}\"; open \"{s.openLine}\"{(string.IsNullOrEmpty(s.openTitle) ? "" : " (title \"" + s.openTitle + "\")")}; snatch \"{s.snatchLine}\".");
            Log.AppendLine($"NUMBERS: a shot's refusal toast at most every {s.refuseEvery} s. The snatch: {s.startDelay} s after the first growth ends, only within {s.watchRadius} of the gun; the view turns in {s.turnInSeconds} s (to frame the floor from the gun to the bed's mouth at {s.frameShare} of its height plus {s.frameMargin}, never narrower than {s.minFov} degrees), holds until {s.holdAfterMouth} s after the gun is under the bed, turns back in {s.turnOutSeconds} s; the monster runs {s.runSpeed} a second, takes the gun in {s.grabSeconds} s, drags it at {s.dragSpeed} a second to the mouth and {s.carrySpeed} on under the bed ({s.gripGap} in front of its body), waits {s.dropSeconds} s; the gun's light {s.gunLightIntensity} bright, reach {s.gunLightRange}; its glow {s.snatchGlow} of the glint. The glint: full in {s.glintFillSeconds} s of any beam, gone {s.glintDrainSeconds} s after; colour {s.glintColour}. Toy Only {(s.toyOnly ? "ON" : "off")}.");
            return s;
        }

        static MonsterSettings Monsters(bool first)
        {
            var ms = AssetDatabase.LoadAssetAtPath<MonsterSettings>(MonsterSettingsPath);
            if (ms == null)
            {
                Log.AppendLine($"MONSTERS: NO {MonsterSettingsPath} (Land Bedroom Monsters makes it): the monsters use the code's defaults (Never Calm and Light Stuns on, Stay Down ON).");
                return null;
            }
            string turned = "";
            if (first && ms.stayDown)
            {
                ms.stayDown = false;
                EditorUtility.SetDirty(ms);
                AssetDatabase.SaveAssetIfDirty(ms);
                turned = " (TURNED OFF now, the first landing: his \"they should always respawn\"; tick it again to keep them down)";
            }
            Log.AppendLine($"MONSTERS ({MonsterSettingsPath}): Never Calm {(ms.neverCalm ? "ON (no freeze in the lights, no sleepiness in the starlight; still only at the game's end)" : "OFF (the D-0228 light rule)")}; Light Stuns {(ms.lightStuns ? "ON (" + ms.dizzyAfter + " s of beam = dizzy " + ms.dizzySeconds + " s, hits x" + ms.dizzyHitScale + "; no running from the beam; the kid within " + ms.kidReach + " still sends them off)" : "OFF (flinch, flee and hide)")}; Stay Down {(ms.stayDown ? "ON: a knocked-out monster stays down for the visit" : "off: up again at its spot " + ms.downSeconds + " s after a knockout")}{turned}. The bump is still a push (no health lost). Health {ms.health} (a dart 15).");
            return ms;
        }

        // ------------------------------------------------------------------------------------------------ the locks

        static void Lock(Component thing, BedroomGate gate, string what, string line)
        {
            if (thing == null) { Log.AppendLine($"LOCK NOT WIRED: {what} was not found under Room/Bedroom: it is never held."); return; }
            var so = new SerializedObject(thing);
            SerializedProperty field = so.FindProperty("heldShutBy");
            if (field == null) { Log.AppendLine($"LOCK NOT WIRED: {Path(thing.transform)}'s {thing.GetType().Name} has no Held Shut By (the D-0241 scripts are not compiled in?)."); return; }
            field.objectReferenceValue = gate;
            so.ApplyModifiedPropertiesWithoutUndo();
            if (PrefabUtility.IsPartOfPrefabInstance(thing)) PrefabUtility.RecordPrefabInstancePropertyModifications(thing);
            string extra = thing is Switch sw && sw.GetComponent<Damageable>() != null ? " It has a Damageable: a dart that hits it presses nothing while held (the toast says why)." : "";
            Log.AppendLine($"LOCK: {Path(thing.transform)} ({thing.GetType().Name}) Held Shut By -> {Path(gate.transform)}: while shut, E refused with \"{line}\".{extra}");
        }

        // ------------------------------------------------------------------------------------------------ the snatch rig

        /// <summary>The cave, the gun's two places, the snatcher and its way, the guns made, the glint wired; all reported.</summary>
        static void Rig(Transform bedroom, BedroomGateSettings s, MonsterSettings ms, List<DarkMonster> monsters, GunSnatch snatch, GameObject standAsset, float toyScale, float kidScale)
        {
            // The cave as the monsters know it (their maker measured it): the box, the way's mouth and the flashlight.
            var mso = new SerializedObject(monsters[0]);
            Bounds cave = mso.FindProperty("cave").boundsValue;
            Vector3 mouth = mso.FindProperty("laneFrom").vector3Value;
            Vector3 torch = mso.FindProperty("laneTo").vector3Value;
            float laneHalf = mso.FindProperty("laneHalfWidth").floatValue;
            float floorY = mouth.y;
            Transform bed = bedroom.Find("Bed");
            GameObject toyBoxGo = GameObject.Find("Room/ToyBox");
            Transform toyBox = toyBoxGo != null ? toyBoxGo.transform : null;
            if (cave.size.x <= 0f || bed == null || toyBox == null)
            {
                Log.AppendLine($"SNATCH RIG NOT placed: {(cave.size.x <= 0f ? "the monsters have no cave box (run Land Bedroom Monsters again)" : bed == null ? "no Room/Bedroom/Bed" : "no Room/ToyBox")}. The gate works without it; the gun is wherever the hallway has one.");
                return;
            }
            float boxReach = BoxReach(toyBox, out Vector3 boxAt, out string boxWhat);
            Vector3 outDir = Level(boxAt - mouth).normalized;
            if (outDir.sqrMagnitude < 0.5f) outDir = Vector3.left;
            Log.AppendLine($"THE CAVE (from {monsters[0].name}'s own numbers): box x {cave.min.x:F1}..{cave.max.x:F1}, z {cave.min.z:F1}..{cave.max.z:F1}, floor y {floorY:F2}; the way in from the mouth {Fmt(mouth)} to BedFlashlight {Fmt(torch)}, kept {laneHalf} either side.");

            // The snatcher: the cave monster nearest the mouth.
            DarkMonster snatcher = null;
            float best = float.MaxValue;
            foreach (DarkMonster m in monsters)
            {
                float d = Level(m.transform.position - mouth).magnitude;
                if (d < best) { best = d; snatcher = m; }
            }
            var scc = snatcher.GetComponent<CharacterController>();
            float size = Mathf.Max(0.001f, snatcher.transform.lossyScale.y);
            float bodyR = scc != null ? scc.radius * size : 0.5f * size, bodyH = scc != null ? scc.height * size : size;
            float feetY = snatcher.transform.position.y;
            Log.AppendLine($"THE SNATCHER: {Path(snatcher.transform)}, {best:F1} from the mouth (the nearest of {monsters.Count}); its body {bodyH:F2} tall, radius {bodyR:F2}. If it is down or gone in Play, the awake cave monster nearest the mouth goes instead.");

            // The gun out by the bed: Gun Out from the mouth toward the toy box, on the floor, clear of the box's fallen walls.
            Vector3 gunAt = mouth + outDir * s.gunOut;
            gunAt.y = FloorAt(gunAt, floorY + 5f, bed, out bool gunFloor);
            float fromBox = Level(gunAt - boxAt).magnitude;
            Log.AppendLine($"THE GUN OUT: {Fmt(gunAt)} ({s.gunOut} out from the mouth toward Room/ToyBox at {Fmt(boxAt)}){(gunFloor ? "" : " (NO floor found under it: floor y assumed)")}; {fromBox:F1} from the box's middle, its fallen walls reach {boxReach:F1} ({boxWhat}): {(fromBox > boxReach + 1f ? "clear of them" : "UNDER THEM: raise Gun Out or lower it in BedroomGateSettings and run again")}; {Solid(gunAt + Vector3.up * 0.5f, 0.4f, null)}.");

            // The prop: the stand's own pistol model, lying on its side, under a KeepLit with its glow and light.
            GameObject modelAsset = StandModel(standAsset, out string modelFrom, out bool modelIsPrefab, out Vector3 modelScale);
            Material glintMat = null;
            var prop = new GameObject(PropName).transform;
            prop.SetParent(snatch.transform, false);
            prop.SetPositionAndRotation(gunAt, Quaternion.LookRotation(outDir, Vector3.up));
            prop.gameObject.AddComponent<KeepLit>();
            Renderer[] propParts = new Renderer[0];
            float propHalf = 0.2f;
            if (modelAsset != null)
            {
                GameObject model;
                if (modelIsPrefab) model = (GameObject)PrefabUtility.InstantiatePrefab(modelAsset, prop);
                else
                {
                    model = UnityEngine.Object.Instantiate(modelAsset, prop);
                    foreach (MonoBehaviour behaviour in model.GetComponentsInChildren<MonoBehaviour>(true)) UnityEngine.Object.DestroyImmediate(behaviour);
                    foreach (Collider col in model.GetComponentsInChildren<Collider>(true)) UnityEngine.Object.DestroyImmediate(col);
                }
                // The size the stand shows it at under the bed (the stand is placed at the toy's scale), whatever the prop's own.
                Vector3 ps = prop.lossyScale;
                model.transform.localScale = new Vector3(modelScale.x * toyScale / Mathf.Max(1e-6f, ps.x), modelScale.y * toyScale / Mathf.Max(1e-6f, ps.y), modelScale.z * toyScale / Mathf.Max(1e-6f, ps.z));
                model.name = ModelName;
                model.transform.localPosition = Vector3.zero;
                model.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);   // on its side, the barrel toward the toy box
                Bounds mb = BoundsOf(model);
                model.transform.position += new Vector3(gunAt.x - mb.center.x, gunAt.y + 0.005f - mb.min.y, gunAt.z - mb.center.z);
                propParts = model.GetComponentsInChildren<Renderer>(true);
                glintMat = GlintMaterial(propParts, out string matLine);
                Wear(propParts, glintMat);
                mb = BoundsOf(model);
                propHalf = Mathf.Abs(Vector3.Dot(mb.extents, Abs(outDir)));
                Log.AppendLine($"THE GUN'S LOOK: {modelFrom}; {matLine}");
            }
            else Log.AppendLine($"THE GUN'S LOOK: NO model found in {StandPrefabPath}'s display: the prop is empty (the moment still plays, with nothing to see taken).");
            var propGlow = prop.gameObject.AddComponent<LightGlow>();
            var pgso = new SerializedObject(propGlow);
            SetRenderers(pgso.FindProperty("parts"), propParts);
            pgso.FindProperty("glow").colorValue = s.glintColour;
            pgso.ApplyModifiedPropertiesWithoutUndo();
            var lightGo = new GameObject(GunLightName);
            lightGo.transform.SetParent(prop, false);
            lightGo.transform.localPosition = Vector3.up * 0.6f;
            var gunLight = lightGo.AddComponent<Light>();
            gunLight.type = LightType.Point; gunLight.shadows = LightShadows.None;
            gunLight.color = s.gunLightColour; gunLight.intensity = s.gunLightIntensity; gunLight.range = s.gunLightRange;
            gunLight.enabled = false;
            prop.gameObject.SetActive(false);   // Play shows it (GunSnatch reads the slot); unticked GunSnatch = no prop

            // The gun under the bed (D-0245): with Pickup Near Mouth, the free cave floor point nearest the bed's edge that
            // the toy sees from the way in (at least Pickup Min In under); without it, the one a kid's E reaches least.
            Interactor hands = Object.FindFirstObjectByType<Interactor>(FindObjectsInactive.Include);
            float reach = 1.6f, height = 0.9f;
            if (hands != null)
            {
                var iso = new SerializedObject(hands);
                reach = iso.FindProperty("reach").floatValue; height = iso.FindProperty("height").floatValue;
            }
            GameObject player = GameObject.Find("Player");
            var pcc = player != null ? player.GetComponent<CharacterController>() : null;
            float toyR = (pcc != null ? pcc.radius : 0.3f) * toyScale, toyH = (pcc != null ? pcc.height : 1.8f) * toyScale;
            float kidR = (pcc != null ? pcc.radius : 0.3f) * kidScale, kidH = (pcc != null ? pcc.height : 1.8f) * kidScale;
            Bounds bedBox = BoundsOf(bed.gameObject);
            List<Vector3> kidSpots = KidSpots(bedBox, floorY, kidR, kidH, out int kidTried);
            Log.AppendLine($"THE KID'S E: reach {reach} and height {height} (the Interactor's, at kid size x{kidScale}: its search ball is {reach * kidScale * 0.5f:F1} round a middle {reach * kidScale * 0.5f:F1} in front of the feet, {height * kidScale:F1} up). The kid ({kidH:F0} tall, radius {kidR:F1}) can stand at {kidSpots.Count} of {kidTried} spots tried round the bed (every {KidSpotStep}, {KidSpotGap} past its radius from the bed's footprint x {bedBox.min.x:F1}..{bedBox.max.x:F1}, z {bedBox.min.z:F1}..{bedBox.max.z:F1}); a wall or furniture too near rules a spot out.");

            var things = new List<(string name, Vector3 at)>();
            Transform under = bedroom.Find("UnderBed");
            if (under != null)
                foreach (Transform t in under)
                    if (t.GetComponent<DarkZone>() == null) things.Add((t.name, t.position));
            Vector3 pick = Vector3.zero;
            float pickScore = float.NegativeInfinity, pickRoom = 0f;
            int tried = 0, kept = 0;
            var why = new Dictionary<string, int>();
            for (float x = cave.min.x + toyR + 1f; x <= cave.max.x - toyR - 1f; x += s.pickupGridStep)
                for (float z = cave.min.z + toyR + 1f; z <= cave.max.z - toyR - 1f; z += s.pickupGridStep)
                {
                    tried++;
                    var c = new Vector3(x, floorY, z);
                    string no = PickupProblem(c, s, ms, monsters, mouth, torch, laneHalf, things, bed, floorY, toyR, toyH, out float room, out float cy);
                    if (no != null) { why[no] = why.TryGetValue(no, out int n) ? n + 1 : 1; continue; }
                    c.y = cy;
                    float score;
                    if (s.pickupNearMouth)
                    {
                        // D-0245 (his "can't find the dart gun"): near the bed's edge and in view from the way in. Toy
                        // Only keeps it from the kid, so it need not hide from the kid's reach.
                        Vector3 fromMouth = Level(c - mouth);
                        float pastEdge = Vector3.Dot(fromMouth, -outDir);   // how far past the bed's edge, straight in
                        if (pastEdge < s.pickupMinIn) { TallyWhy(why, "less than Pickup Min In past the bed's edge"); continue; }
                        if (!SeenFromMouth(c, mouth, outDir, floorY, toyH, toyScale, monsters)) { TallyWhy(why, "out of view from the way in"); continue; }
                        score = -fromMouth.magnitude;   // then the nearest the way in wins
                    }
                    else
                    {
                        score = KidMargin(c, kidSpots, reach * kidScale * 0.5f, height * kidScale);
                        score += 0.001f * Level(c - mouth).magnitude;   // a tie goes deeper
                    }
                    kept++;
                    if (score > pickScore) { pickScore = score; pick = c; pickRoom = room; }
                }
            var reasons = new StringBuilder();
            foreach (var kv in why) reasons.Append(reasons.Length > 0 ? ", " : "").Append(kv.Value).Append(' ').Append(kv.Key);
            if (kept == 0)
            {
                Log.AppendLine($"THE GUN UNDER THE BED NOT placed: none of {tried} cave floor points (every {s.pickupGridStep}) is free ({reasons}). Lower Pickup Lane Clear / Monster Clear / Things Clear / Min In in BedroomGateSettings (or untick Pickup Near Mouth) and run again.");
                pick = new Vector3(cave.center.x, floorY, cave.center.z);
            }

            var stand = (GameObject)PrefabUtility.InstantiatePrefab(standAsset, snatch.transform);
            stand.name = PickupName;
            stand.transform.SetPositionAndRotation(pick, Quaternion.LookRotation(Level(mouth - pick).sqrMagnitude > 1e-4f ? Level(mouth - pick) : Vector3.forward, Vector3.up));
            stand.transform.localScale = Vector3.one * toyScale;
            var pickup = stand.GetComponent<WeaponPickup>();
            var wso = new SerializedObject(pickup);
            wso.FindProperty("respawnSeconds").floatValue = 0f;   // once
            wso.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.RecordPrefabInstancePropertyModifications(pickup);
            var displayT = wso.FindProperty("display").objectReferenceValue as Transform;
            Renderer[] standParts = displayT != null ? displayT.GetComponentsInChildren<Renderer>(true) : new Renderer[0];
            if (glintMat == null) glintMat = GlintMaterial(standParts, out _);
            Wear(standParts, glintMat);
            var point = new GameObject(GlintPointName).transform;
            point.SetParent(stand.transform, false);
            point.localPosition = Vector3.up * DisplayUp;
            var target = stand.AddComponent<LightTarget>();
            var tso = new SerializedObject(target);
            tso.FindProperty("fillSeconds").floatValue = s.glintFillSeconds;
            tso.FindProperty("focus").enumValueIndex = (int)LightTarget.FocusRule.Any;
            tso.FindProperty("drainSeconds").floatValue = s.glintDrainSeconds;
            tso.FindProperty("repeatable").boolValue = true;
            tso.FindProperty("point").objectReferenceValue = point;
            tso.ApplyModifiedPropertiesWithoutUndo();
            var glint = stand.AddComponent<LightGlow>();
            var lgso = new SerializedObject(glint);
            SetRenderers(lgso.FindProperty("parts"), standParts);
            lgso.FindProperty("glow").colorValue = s.glintColour;
            lgso.FindProperty("idlePeak").floatValue = s.glintIdlePeak;          // D-0245: a slow glint with no beam on it
            lgso.FindProperty("idleEvery").floatValue = s.glintIdleEvery;
            lgso.FindProperty("idleSeconds").floatValue = s.glintIdleSeconds;
            lgso.FindProperty("keepEmissionOn").boolValue = true;                // D-0245: the glow needs the material's emission on
            lgso.ApplyModifiedPropertiesWithoutUndo();
            while (target.OnFill.GetPersistentEventCount() > 0) UnityEventTools.RemovePersistentListener(target.OnFill, 0);
            UnityEventTools.AddPersistentListener(target.OnFill, glint.GlowOn);
            stand.SetActive(true);   // the gun under the bed as the scene stands; Play hides it until the snatch (GunSnatch reads the slot)

            // What reaches it.
            float kidMargin = KidMargin(pick, kidSpots, reach * kidScale * 0.5f, height * kidScale, out Vector3 kidFrom);
            string toyWay = Sweep(mouth + outDir * 2f, pick, toyR, toyH, floorY, stand.transform, null);
            string pickRule = s.pickupNearMouth
                ? "the nearest the way in (D-0245: in view from it, at least " + s.pickupMinIn + " past the bed's edge) of " + kept + " free of " + tried
                : "the best of " + kept + " free of " + tried;
            Log.AppendLine($"THE GUN UNDER THE BED: {Path(stand.transform)} at {Fmt(pick)} ({pickRule} cave floor points{(reasons.Length > 0 ? "; ruled out: " + reasons : "")}), a {standAsset.name} giving the gun ONCE (Respawn Seconds 0), scale {toyScale} (toy-sized). {Level(pick - mouth).magnitude:F1} from the mouth, {Level(pick - torch).magnitude:F1} from BedFlashlight, {LaneGap(pick, mouth, torch):F1} from the way's middle line, {NearestHome(pick, monsters, out string nearestName):F1} from the nearest monster's spot ({nearestName}).");
            Log.AppendLine($"  THE TOY: {pickRoom:F2} of room over it (the toy is {toyH:F1} tall); the toy's capsule from the mouth to it: {toyWay}.");
            Log.AppendLine(kidSpots.Count == 0
                ? "  THE KID: no spot round the bed where a kid can stand was found: its E cannot reach the gun."
                : kidMargin > 0f
                    ? $"  THE KID: its E falls {kidMargin:F1} short of the gun from the nearest place it can stand ({Fmt(kidFrom)}): it cannot reach it. (Toy Only {(s.toyOnly ? "holds it for the toy anyway" : "is off")}.)"
                    : $"  THE KID: its E WOULD reach the gun from {Fmt(kidFrom)} (by {-kidMargin:F1}): the bed is too narrow for any spot to be out of its reach. Toy Only {(s.toyOnly ? "ON holds it for the toy (the stand says nothing to a kid)" : "is OFF: a kid can take it from beside the bed")}.");
            Log.AppendLine($"  THE GLINT: LightTarget on it (any beam, {s.glintFillSeconds} s to full, {s.glintDrainSeconds} s to fade, its point {GlintPointName} {DisplayUp} over the floor), On Fill wired to its LightGlow (GlowOn) on the {standParts.Length} parts of its display, glow {s.glintColour}. Wired in the scene: it works with GunSnatch unticked too. While it waits it also glints by itself, {s.glintIdlePeak:F2} of full every {s.glintIdleEvery} s for {s.glintIdleSeconds} s, and switches its material's emission on at start (D-0245).");

            // The way: out from the snatcher's spot through the mouth to just short of the gun, and back.
            float grip = bodyR + s.gripGap + propHalf;
            Vector3 standAt = gunAt - outDir * grip; standAt.y = feetY;
            Vector3 mouthAt = new Vector3(mouth.x, feetY, mouth.z);
            Vector3 legDir = Level(pick - mouthAt).normalized;
            Vector3 dropAt = pick + legDir * grip; dropAt.y = feetY;
            Vector3 home = snatcher.transform.position;
            string outA = Sweep(home, mouthAt, bodyR, bodyH, feetY, null, snatcher.transform);
            string outB = Sweep(mouthAt, standAt, bodyR, bodyH, feetY, null, snatcher.transform);
            string back = Sweep(mouthAt, dropAt, bodyR, bodyH, feetY, stand.transform, snatcher.transform);
            bool dropInCave = dropAt.x >= cave.min.x && dropAt.x <= cave.max.x && dropAt.z >= cave.min.z && dropAt.z <= cave.max.z;
            float outLen = Level(mouthAt - home).magnitude + Level(standAt - mouthAt).magnitude;
            float toMouth = Level(mouthAt - standAt).magnitude, deep = Level(dropAt - mouthAt).magnitude;
            float held = s.turnInSeconds + outLen / s.runSpeed + s.grabSeconds + toMouth / s.dragSpeed + s.holdAfterMouth + s.turnOutSeconds;
            Log.AppendLine($"THE SNATCH'S WAY: out from its spot {Fmt(home)} to the mouth {Fmt(mouthAt)}: {outA}; on to {Fmt(standAt)} ({grip:F2} short of the gun: its body, the grip gap and half the gun): {outB}. Back, walking backwards with the gun in front: to the mouth, then to {Fmt(dropAt)} ({deep:F1} under the bed{(dropInCave ? "" : ", OUTSIDE the monsters' cave box: it walks home from there")}), leaving the gun on the stand's spot: {back}. The other monsters are not counted in (they move); its own collider is off on the way.");
            Log.AppendLine($"THE MOMENT'S TIMES: the player's keys held about {held:F1} s ({s.turnInSeconds} turn, {outLen / s.runSpeed:F1} run, {s.grabSeconds} take, {toMouth / s.dragSpeed:F1} drag to the mouth, {s.holdAfterMouth} hold, {s.turnOutSeconds} turn back); then {deep / s.carrySpeed:F1} s more under the bed (Carry Speed {s.carrySpeed}) before the stand appears.");

            var so = new SerializedObject(snatch);
            so.FindProperty("prop").objectReferenceValue = prop;
            so.FindProperty("propGlow").objectReferenceValue = propGlow;
            so.FindProperty("propLight").objectReferenceValue = gunLight;
            so.FindProperty("pickup").objectReferenceValue = pickup;
            so.FindProperty("glintTarget").objectReferenceValue = target;
            so.FindProperty("glint").objectReferenceValue = glint;
            so.FindProperty("snatcher").objectReferenceValue = snatcher;
            so.FindProperty("mouth").vector3Value = mouthAt;
            SetPoints(so.FindProperty("runOut"), mouthAt, standAt);
            SetPoints(so.FindProperty("dragBack"), mouthAt, dropAt);
            so.ApplyModifiedPropertiesWithoutUndo();
            Log.AppendLine($"SNATCH WIRED: {Path(snatch.transform)} (GunSnatch): prop {PropName} (hidden in the scene; Play shows it on a slot that has not seen the snatch), glow + {GunLightName} (off), pickup {PickupName} (shown in the scene; Play hides it until the snatch), glint target + glow, snatcher {snatcher.name}, the mouth, the way out (2 points) and back (2 points). Saved per slot as '{s.snatchKey}'.");
        }

        /// <summary>Why the gun may not wait at <paramref name="c"/> (null when it may).</summary>
        static string PickupProblem(Vector3 c, BedroomGateSettings s, MonsterSettings ms, List<DarkMonster> monsters, Vector3 mouth, Vector3 torch, float laneHalf,
            List<(string name, Vector3 at)> things, Transform bed, float floorY, float toyR, float toyH, out float room, out float cy)
        {
            room = 0f; cy = floorY;
            if (LaneGap(c, mouth, torch) < laneHalf + s.pickupLaneClear) return "too near the monsters' way";
            float wander = ms != null ? ms.wanderRadius : 3f;
            foreach (DarkMonster m in monsters)
            {
                var cc = m.GetComponent<CharacterController>();
                float r = cc != null ? cc.radius * m.transform.lossyScale.y : 1f;
                if (Level(c - m.transform.position).magnitude < wander + r + s.pickupMonsterClear) return "too near a monster's spot";
            }
            foreach (var (name, at) in things) if (Level(c - at).magnitude < s.pickupThingsClear) return "too near the bed's own things";
            cy = FloorAt(c, floorY + 3f, bed, out bool found);
            if (!found || Mathf.Abs(cy - floorY) > 0.5f) return "no cave floor";
            room = Headroom(new Vector3(c.x, cy, c.z), bed);
            if (room < s.pickupHeadroom) return "too little room overhead";
            float rr = toyR + 0.3f;   // the toy's body and a little room round it, its bottom just over the floor
            foreach (Collider col in Physics.OverlapCapsule(new Vector3(c.x, cy + rr + 0.05f, c.z), new Vector3(c.x, cy + Mathf.Max(rr + 0.06f, toyH - rr), c.z), rr, ~0, QueryTriggerInteraction.Ignore))
                if (col != null && col.GetComponentInParent<DarkMonster>() == null) return "something solid there";
            return null;
        }

        /// <summary>Where a kid can stand round the bed: every Kid Spot Step along each side of its footprint, its radius plus
        /// a gap out, where nothing solid (but the player and the monsters) is in its capsule.</summary>
        static List<Vector3> KidSpots(Bounds bed, float floorY, float kidR, float kidH, out int tried)
        {
            var spots = new List<Vector3>();
            tried = 0;
            float off = kidR + KidSpotGap;
            float minX = bed.min.x - off, maxX = bed.max.x + off, minZ = bed.min.z - off, maxZ = bed.max.z + off;
            var all = new List<Vector3>();
            for (float x = minX; x <= maxX; x += KidSpotStep) { all.Add(new Vector3(x, floorY, minZ)); all.Add(new Vector3(x, floorY, maxZ)); }
            for (float z = minZ + KidSpotStep; z < maxZ; z += KidSpotStep) { all.Add(new Vector3(minX, floorY, z)); all.Add(new Vector3(maxX, floorY, z)); }
            foreach (Vector3 p in all)
            {
                tried++;
                bool free = true;
                foreach (Collider col in Physics.OverlapCapsule(p + Vector3.up * (kidR + 0.3f), p + Vector3.up * Mathf.Max(kidR + 0.31f, kidH - kidR), kidR, ~0, QueryTriggerInteraction.Ignore))
                {
                    if (col == null || col.GetComponentInParent<DarkMonster>() != null || col.GetComponentInParent<Shifter>() != null) continue;
                    free = false; break;
                }
                if (free) spots.Add(p);
            }
            return spots;
        }

        static void TallyWhy(Dictionary<string, int> why, string reason) => why[reason] = why.TryGetValue(reason, out int n) ? n + 1 : 1;

        /// <summary>D-0245: the toy, standing just outside the bed's edge on the monsters' way in, sees the gun's display:
        /// nothing solid between its eyes and the display (the monsters do not count: they move).</summary>
        static bool SeenFromMouth(Vector3 at, Vector3 mouth, Vector3 outDir, float floorY, float toyH, float toyScale, List<DarkMonster> monsters)
        {
            Vector3 eye = new Vector3(mouth.x, floorY + toyH * 0.8f, mouth.z) + outDir * 2f;
            Vector3 display = new Vector3(at.x, at.y + DisplayUp * toyScale, at.z);
            Vector3 to = display - eye;
            float d = to.magnitude;
            if (d < 1e-3f) return true;
            foreach (RaycastHit h in Physics.RaycastAll(eye, to / d, d, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                bool monster = false;
                foreach (DarkMonster m in monsters) if (m != null && h.transform.IsChildOf(m.transform)) { monster = true; break; }
                if (!monster) return false;
            }
            return true;
        }

        static float KidMargin(Vector3 gun, List<Vector3> spots, float ball, float up) => KidMargin(gun, spots, ball, up, out _);

        /// <summary>How far the kid's E falls short of the stand's trigger, from the best of its standing spots (it turns to
        /// face the gun): the Interactor's search capsule (Interactor.FindInReach) against the trigger ball. Below 0 it
        /// reaches. No spot: a large margin.</summary>
        static float KidMargin(Vector3 gun, List<Vector3> spots, float ball, float up, out Vector3 from)
        {
            from = Vector3.zero;
            float least = 1e6f;
            Vector3 trigger = gun + Vector3.up * StandTriggerUp;
            foreach (Vector3 feet in spots)
            {
                Vector3 fwd = Level(gun - feet);
                if (fwd.sqrMagnitude < 1e-6f) fwd = Vector3.forward; else fwd.Normalize();
                Vector3 centre = feet + Vector3.up * up + fwd * ball;
                Vector3 low = feet + Vector3.up * Mathf.Min(up, ball * 0.8f) + fwd * ball;
                float gap = Vector3.Distance(trigger, Closest(low, centre, trigger)) - ball - StandTrigger;
                if (gap < least) { least = gap; from = feet; }
            }
            return least;
        }

        /// <summary>A capsule swept along the level way from <paramref name="from"/> to <paramref name="to"/> a little off the
        /// floor: "clear", or what stops it and where. The monsters, <paramref name="skip"/> and <paramref name="self"/> aside.</summary>
        static string Sweep(Vector3 from, Vector3 to, float radius, float tall, float floorY, Transform skip, Transform self)
        {
            Vector3 way = Level(to - from);
            float length = way.magnitude;
            if (length < 1e-3f) return "no way to test (it starts there)";
            Vector3 low = new Vector3(from.x, floorY + radius + 0.3f, from.z), high = new Vector3(from.x, floorY + Mathf.Max(radius + 0.31f, tall - radius), from.z);
            float nearest = float.MaxValue; string what = null;
            foreach (RaycastHit h in Physics.CapsuleCastAll(low, high, radius, way / length, length, ~0, QueryTriggerInteraction.Ignore))
            {
                Transform t = h.collider.transform;
                if ((skip != null && t.IsChildOf(skip)) || (self != null && t.IsChildOf(self)) || h.collider.GetComponentInParent<DarkMonster>() != null || h.collider.GetComponentInParent<Shifter>() != null) continue;
                if (h.distance < nearest) { nearest = h.distance; what = h.collider.name; }
            }
            return what == null ? $"clear ({length:F1})" : $"STOPPED by {what} {nearest:F1} along ({length:F1})";
        }

        /// <summary>How far the toy box's walls reach from its middle once they have fallen (each lands flat outward from its
        /// bottom outer edge: its distance out plus its height, flap and all). <paramref name="centre"/>: the walls' middle on
        /// the floor, as BoxBurst finds it (the box's own origin may sit off it).</summary>
        static float BoxReach(Transform toyBox, out Vector3 centre, out string what)
        {
            centre = toyBox.position;
            var burst = toyBox.GetComponentInChildren<BoxBurst>(true);
            what = burst != null ? "its BoxBurst's walls" : "no BoxBurst: its own size";
            if (burst == null) return BoundsOf(toyBox.gameObject).extents.magnitude;
            var panels = new SerializedObject(burst).FindProperty("panels");
            var boards = new List<Bounds>();
            Vector3 sum = Vector3.zero;
            for (int i = 0; i < panels.arraySize; i++)
            {
                var p = panels.GetArrayElementAtIndex(i).objectReferenceValue as Transform;
                if (p == null) continue;
                Bounds b = BoundsOf(p.gameObject);
                boards.Add(b); sum += b.center;
            }
            if (boards.Count == 0) return BoundsOf(toyBox.gameObject).extents.magnitude;
            centre = sum / boards.Count; centre.y = toyBox.position.y;
            float most = 0f;
            foreach (Bounds b in boards) most = Mathf.Max(most, Level(b.center - centre).magnitude + Mathf.Max(b.extents.x, b.extents.z) + b.size.y);
            return most;
        }

        /// <summary>The pistol model inside the stand's display (its source prefab, as the stand shows it).</summary>
        static GameObject StandModel(GameObject standAsset, out string from, out bool isPrefab, out Vector3 scaleInStand)
        {
            from = "no model";
            isPrefab = false;
            scaleInStand = Vector3.one;
            var pickup = standAsset.GetComponent<WeaponPickup>();
            var display = new SerializedObject(pickup).FindProperty("display").objectReferenceValue as Transform;
            if (display == null || display.childCount == 0) return null;
            GameObject child = display.GetChild(0).gameObject;
            scaleInStand = child.transform.lossyScale;   // its size inside the stand, the stand's root at scale 1
            GameObject source = PrefabUtility.GetCorrespondingObjectFromSource(child);
            if (source != null)
            {
                isPrefab = true;
                from = $"the stand's own display model ({AssetDatabase.GetAssetPath(source)}, referenced in place: nothing copied out of the git-ignored pack)";
                return source;
            }
            // Landing fix (26 Nov): this stand's display holds a plain object, not a placed prefab, and the first run found
            // "no model", so the snatch dragged an invisible gun. The object itself is copied instead; its meshes and
            // materials stay referenced in place.
            from = $"the stand's display child '{child.name}' (copied; its meshes and materials referenced in place: nothing copied out of the git-ignored pack)";
            return child;
        }

        /// <summary>The emissive copy of the gun's material (ours, under Art/Bedroom; the pack's texture referenced by GUID as
        /// D-0239's garden copies are): made once, its properties refreshed from the pack's material each run, emission ON and
        /// black (LightGlow lights it through a property block).</summary>
        static Material GlintMaterial(Renderer[] parts, out string line)
        {
            Material source = null;
            foreach (Renderer r in parts) if (r != null && r.sharedMaterial != null && r.sharedMaterial.name != "DartGunGlint") { source = r.sharedMaterial; break; }
            var copy = AssetDatabase.LoadAssetAtPath<Material>(GlintMaterialPath);
            if (source == null && copy == null) { line = "NO material on the gun to copy: the glint shows nothing."; return null; }
            bool made = copy == null;
            if (made)
            {
                EnsureFolder(GlintFolder);
                copy = new Material(source) { name = "DartGunGlint" };
                AssetDatabase.CreateAsset(copy, GlintMaterialPath);
            }
            // An existing glint material is kept as it is (D-0245): copying the pack's material over it again (its shader set
            // again, every property copied) made Unity switch its emission off as it imported it afterwards, and then no
            // beam could light the gun. Delete Art/Bedroom/DartGunGlint.mat to make it again from the gun's own material.
            copy.EnableKeyword("_EMISSION");
            // All but black (D-0245): an emission colour of pure black lets Unity switch the material's emission off when it
            // checks it again, and then no beam could make the gun glint. LightGlow sets the real glow per renderer.
            copy.SetColor("_EmissionColor", new Color(0.004f, 0.004f, 0.004f));
            copy.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            EditorUtility.SetDirty(copy);
            AssetDatabase.SaveAssetIfDirty(copy);
            line = $"{(made ? "MADE" : "KEPT")} {GlintMaterialPath}{(made ? " from " + (source != null ? AssetDatabase.GetAssetPath(source) : "itself") : " as it was (not copied over again, D-0245)")} ({copy.shader.name}, emission on, all but black (D-0245): the glint lights it). Worn by the two guns only.";
            return copy;
        }

        /// <summary>Every slot of every part wears <paramref name="m"/> (a prefab instance's parts: recorded as its overrides).</summary>
        static void Wear(Renderer[] parts, Material m)
        {
            if (m == null) return;
            foreach (Renderer r in parts)
            {
                if (r == null) continue;
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++) mats[i] = m;
                r.sharedMaterials = mats;
                if (PrefabUtility.IsPartOfPrefabInstance(r)) PrefabUtility.RecordPrefabInstancePropertyModifications(r);
            }
        }

        // ------------------------------------------------------------------------------------------------ the words

        /// <summary>The two new steps after wake-box (added once), wake-box's words (only while the old placeholder), the
        /// star's Gate Opened rule (added once).</summary>
        static void Steps(Scene scene, BedroomGateSettings s, Transform gate, Transform pickup)
        {
            var set = AssetDatabase.LoadAssetAtPath<ObjectiveSet>(ObjectivesBuilder.AssetPath);
            if (set == null) { Log.AppendLine($"STEPS NOT written: no {ObjectivesBuilder.AssetPath} (run Shift Jam > Objectives > Build Objectives first, then this again: that maker adds the two steps from the defaults too)."); return; }
            var steps = new List<ObjectiveStep>(set.steps ?? new ObjectiveStep[0]);
            int wake = steps.FindIndex(x => x != null && x.id == "wake-box");
            var said = new StringBuilder();
            if (wake >= 0)
            {
                ObjectiveStep w = steps[wake];
                if (w.text == OldWakeBoxText) { said.Append($"wake-box \"{OldWakeBoxText}\" -> \"{s.wakeBoxText}\""); w.text = s.wakeBoxText; }
                else said.Append($"wake-box kept as \"{w.text}\"");
            }
            else said.Append("NO wake-box step (the new steps go first)");
            string pickupPath = pickup != null ? Path(pickup) : Path(gate.parent) + "/" + SnatchName + "/" + PickupName;
            int at = wake >= 0 ? wake + 1 : 0;
            at = AddStep(steps, at, s.gunStepId, s.gunStepText, new ObjectiveRule { when = ObjectiveEvent.WeaponTaken, scene = ObjectiveSet.House, needed = 1, objects = new[] { pickupPath } }, said);
            AddStep(steps, at, s.gateStepId, s.gateStepText, new ObjectiveRule { when = ObjectiveEvent.GateOpened, scene = ObjectiveSet.House, needed = 1, objects = new[] { Path(gate) } }, said);
            set.steps = steps.ToArray();

            // The star: a Gate Opened rule beside D-0228's hook rule (any one earns it).
            var stars = new List<ObjectiveStar>(set.stars ?? new ObjectiveStar[0]);
            ObjectiveStar star = stars.Find(x => x != null && x.id == s.starId);
            string starSaid;
            if (star == null) starSaid = $"NO star '{s.starId}' on the set (Land Bedroom Monsters adds it): the opening earns no star";
            else
            {
                var rules = new List<ObjectiveRule>(star.doneWhen ?? new ObjectiveRule[0]);
                if (rules.Exists(r => r != null && r.when == ObjectiveEvent.GateOpened)) starSaid = $"star '{star.id}' (\"{star.title}\") already has its Gate Opened rule";
                else
                {
                    rules.Add(new ObjectiveRule { when = ObjectiveEvent.GateOpened, scene = ObjectiveSet.House, needed = 1, objects = new[] { Path(gate) } });
                    star.doneWhen = rules.ToArray();
                    starSaid = $"star '{star.id}' (\"{star.title}\", his title kept): Gate Opened rule ADDED (rule {rules.Count}); its D-0228 KnockedDown hook rule stays (any one earns it)";
                }
            }
            EditorUtility.SetDirty(set);
            AssetDatabase.SaveAssetIfDirty(set);
            var order = new StringBuilder();
            for (int i = 0; i < Mathf.Min(set.steps.Length, 6); i++) order.Append(i > 0 ? ", " : "").Append(set.steps[i] != null ? set.steps[i].id : "?");
            Log.AppendLine($"STEPS ({ObjectivesBuilder.AssetPath}): {said}. The route now begins {order}... {starSaid}.");
        }

        /// <summary>Adds the step at <paramref name="at"/> if the set has no step of that id. Returns the place after it.</summary>
        static int AddStep(List<ObjectiveStep> steps, int at, string id, string text, ObjectiveRule rule, StringBuilder said)
        {
            int found = steps.FindIndex(x => x != null && x.id == id);
            if (found >= 0)
            {
                said.Append($"; step '{id}' FOUND (his words kept: \"{steps[found].text}\")");
                return found + 1;
            }
            at = Mathf.Clamp(at, 0, steps.Count);
            steps.Insert(at, new ObjectiveStep { id = id, text = text, room = "Bedroom", doneWhen = new[] { rule } });
            said.Append($"; step '{id}' \"{text}\" ADDED at {at + 1} ({rule.when} {rule.objects[0]})");
            return at + 1;
        }

        static void GoalLine(BedroomGateSettings s)
        {
            var opening = AssetDatabase.LoadAssetAtPath<OpeningSettings>(OpeningPath);
            if (opening == null) { Log.AppendLine($"GOAL LINE NOT written: no {OpeningPath}."); return; }
            string was = opening.goalLine ?? "";
            if (was == s.goalLine) { Log.AppendLine($"GOAL LINE: already \"{was}\" (his words)."); return; }
            if (System.Array.IndexOf(OldGoalLines, was.Trim()) < 0) { Log.AppendLine($"GOAL LINE KEPT as \"{was}\" (not one of the old placeholders: his words?). His D-0241 line is \"{s.goalLine}\": write it on {OpeningPath} > Goal Line if wanted."); return; }
            opening.goalLine = s.goalLine;
            EditorUtility.SetDirty(opening);
            AssetDatabase.SaveAssetIfDirty(opening);
            Log.AppendLine($"GOAL LINE ({OpeningPath}): \"{was}\" -> \"{s.goalLine}\" (his words: the opening's second sticker, after the wake-up line).");
        }

        // ------------------------------------------------------------------------------------------------ the lamp

        /// <summary>The star lamp's sweep sound (from its wake sound when empty) and what its sweeping spots can reach: each
        /// monster's spot and the cave's floor, in the tilted cones' band, within reach, nothing solid between.</summary>
        static void Lamp(Transform bedroom, BedroomGateSettings s, MonsterSettings ms, List<DarkMonster> monsters, float toyScale)
        {
            StarLamp lamp = bedroom.GetComponentInChildren<StarLamp>(true);
            if (lamp == null) { Log.AppendLine("STAR LAMP: none under Room/Bedroom: no sweep."); return; }
            var so = new SerializedObject(lamp);
            SerializedProperty sweepClip = so.FindProperty("sweepSound.clip"), wakeClip = so.FindProperty("wakeSound.clip");
            string sound = "its sweep sound kept";
            if (sweepClip != null && wakeClip != null && sweepClip.objectReferenceValue == null && wakeClip.objectReferenceValue != null)
            {
                sweepClip.objectReferenceValue = wakeClip.objectReferenceValue;
                so.ApplyModifiedPropertiesWithoutUndo();
                if (PrefabUtility.IsPartOfPrefabInstance(lamp)) PrefabUtility.RecordPrefabInstancePropertyModifications(lamp);
                sound = $"its sweep sound filled from its wake sound ({wakeClip.objectReferenceValue.name})";
            }
            if (s.lampUnderBed) UnderBed(lamp, bedroom, s, ms, monsters, toyScale);   // D-0247: before the reach is measured
            Light side = null, any = null;
            foreach (Light l in lamp.Stars)
            {
                if (l == null || l.type != LightType.Spot) continue;
                any = l;
                if (Mathf.Abs((l.transform.localRotation * Vector3.forward).y) < 0.9f) { side = l; break; }
            }
            if (side == null) { Log.AppendLine($"STAR LAMP {Path(lamp.transform)}: {(any == null ? "no star spots" : "no side spot (only the straight-up one)")}: the sweep can stun nothing. {sound}."); return; }
            Vector3 dome = side.transform.position;
            float half = side.spotAngle * 0.5f, range = lamp.SweepRange > 0f ? lamp.SweepRange : side.range, pitch = lamp.SweepPitch;   // D-0247: the stun reaches as far as the sweep's light
            Log.AppendLine($"STAR LAMP {Path(lamp.transform)}: Sweep {(lamp.SweepOn ? "ON" : "OFF (the D-0218 lamp)")}; the dome at {Fmt(dome)}; a side spot's cone {side.spotAngle} degrees, reach {range}; swept {pitch} degrees under the level, so the spots cross what lies {Mathf.Max(0f, pitch - half):F0} to {Mathf.Min(90f, pitch + half):F0} degrees under the dome (turning, every way round); {sound}.");

            float tall = 2f;   // a monster's height, for the cave floor's points below (the last monster's own)
            var lines = new StringBuilder();
            int reached = 0;
            foreach (DarkMonster m in monsters)
            {
                var cc = m.GetComponent<CharacterController>();
                float scale = m.transform.lossyScale.y;
                Vector3 middle = cc != null ? m.transform.TransformPoint(cc.center) : m.transform.position + Vector3.up * scale;
                float h = cc != null ? cc.height * scale : scale;
                string a = Crosses(dome, middle, half, range, pitch, lamp.transform, m.transform);
                string b = Crosses(dome, middle + Vector3.up * (h * 0.35f), half, range, pitch, lamp.transform, m.transform);
                bool ok = a == null || b == null;
                if (ok) reached++;
                lines.Append(lines.Length > 0 ? "; " : "").Append($"{m.name} at its spot: {(ok ? "CROSSED" : "not crossed: " + a)}");
                tall = h;
            }
            // The cave's floor: a monster's middle over each grid point.
            var mso = new SerializedObject(monsters[0]);
            Bounds cave = mso.FindProperty("cave").boundsValue;
            float floorY = mso.FindProperty("laneFrom").vector3Value.y;
            int all = 0, seen = 0;
            float minZ = float.MaxValue, maxZ = float.MinValue, minX = float.MaxValue, maxX = float.MinValue;
            for (float x = cave.min.x; x <= cave.max.x; x += s.pickupGridStep)
                for (float z = cave.min.z; z <= cave.max.z; z += s.pickupGridStep)
                {
                    all++;
                    var p = new Vector3(x, floorY + tall * 0.5f, z);
                    if (Crosses(dome, p, half, range, pitch, lamp.transform, null) != null) continue;
                    seen++;
                    minX = Mathf.Min(minX, x); maxX = Mathf.Max(maxX, x); minZ = Mathf.Min(minZ, z); maxZ = Mathf.Max(maxZ, z);
                }
            Log.AppendLine($"  WHAT THE SWEEP CAN REACH: {reached} of {monsters.Count} monster spots ({lines}). The cave floor (a monster's middle over every {s.pickupGridStep}): {seen} of {all} points in a clear line from the dome{(seen > 0 ? $", within x {minX:F0}..{maxX:F0}, z {minZ:F0}..{maxZ:F0}" : "")}. {(seen == 0 ? "The bed's frame hides the whole cave from the lamp: as placed, the sweep stuns only a monster out from under the bed (the snatcher on its errand is not stunned while borrowed). His call: move the lamp low by the bed's mouth, or let the sweep ignore the bed." : seen < all / 4 ? "Only that part of the cave is open to the lamp: most of it is hidden by the bed's frame." : "")}");
            if (lamp != null)
            {
                var lso = new SerializedObject(lamp);
                SerializedProperty through = lso.FindProperty("sweepThroughFurniture");
                if (through != null && through.boolValue)
                    Log.AppendLine("  THE LAMP'S SWEEP (D-0245): Sweep Through Furniture is ON: the bed and the table do not shield the monsters, so a swept spot stuns any cave monster in its cone and reach (the counts above are the old rule's, with nothing allowed between).");
            }
        }

        /// <summary>D-0247 (his idea: stun the monsters by shining the light on the lamp, under the bed). Moves the night
        /// lamp off the bedside table onto the cave floor under the bed: Lamp Scale of its size, at least Lamp Min In past
        /// the bed's edge, clear of the monsters' way, their spots, the bed's own things and the dart gun, with room over it,
        /// in view from the way in; of those, the place whose dome sees the most monster spots, then the nearest the way in.
        /// Its stars light the bed's underside as brightly as they lit the room's ceiling (all alike: the middle of what they
        /// gave there, so a star that lit a near wall does not glare), never brighter than they were, and reach Lamp Star
        /// Range at rest; during a sweep the side stars (and the stun) reach Lamp Sweep Range. Sweep Through Furniture goes off: nothing is in the way there.
        /// Done once: a lamp already under the bed stays where it is (its settings are written again).</summary>
        static void UnderBed(StarLamp lamp, Transform bedroom, BedroomGateSettings s, MonsterSettings ms, List<DarkMonster> monsters, float toyScale)
        {
            Transform lt = lamp.transform;
            if (monsters.Count == 0) { Log.AppendLine("LAMP UNDER THE BED: no cave monsters to measure the cave by: the lamp stays where it is."); return; }
            var mso = new SerializedObject(monsters[0]);
            Bounds cave = mso.FindProperty("cave").boundsValue;
            Vector3 mouth = mso.FindProperty("laneFrom").vector3Value;
            Vector3 torch = mso.FindProperty("laneTo").vector3Value;
            float laneHalf = mso.FindProperty("laneHalfWidth").floatValue;
            float floorY = mouth.y;
            Transform bed = bedroom.Find("Bed");
            GameObject toyBoxGo = GameObject.Find("Room/ToyBox");
            if (cave.size.x <= 0f || bed == null || toyBoxGo == null) { Log.AppendLine("LAMP UNDER THE BED: no cave box, no Room/Bedroom/Bed or no Room/ToyBox: the lamp stays where it is."); return; }
            BoxReach(toyBoxGo.transform, out Vector3 boxAt, out _);
            Vector3 outDir = Level(boxAt - mouth).normalized;
            if (outDir.sqrMagnitude < 0.5f) outDir = Vector3.left;

            Vector3 lp = lt.position;
            if (lp.x > cave.min.x && lp.x < cave.max.x && lp.z > cave.min.z && lp.z < cave.max.z && lp.y < floorY + 2f)
            {
                string kept = LampSettings(lamp, s);
                Log.AppendLine($"LAMP UNDER THE BED (D-0247): already there at {Fmt(lp)}: kept where it is, its lights as they are; {kept}.");
                return;
            }

            // Its size under the bed, from what shows on the table.
            Bounds b = default; bool shows = false;
            foreach (Renderer r in lt.GetComponentsInChildren<Renderer>(true))
            {
                if (r == null || !r.enabled || r is ParticleSystemRenderer) continue;
                if (!shows) { b = r.bounds; shows = true; } else b.Encapsulate(r.bounds);
            }
            if (!shows) { Log.AppendLine("LAMP UNDER THE BED: the lamp shows nothing to measure: it stays where it is."); return; }
            float k = s.lampScale;
            float halfW = Mathf.Max(b.extents.x, b.extents.z) * k, tallNew = b.size.y * k, below = (b.min.y - lp.y) * k;
            Vector3 centreOff = Level(b.center - lp) * k;   // the footprint's middle from the pivot, once shrunk

            // What its stars and its glow light on the table, before it moves (their brightness keeps that look).
            Physics.SyncTransforms();
            IReadOnlyList<Light> stars = lamp.Stars;
            var tableHit = new float[stars.Count];
            for (int i = 0; i < stars.Count; i++)
                if (stars[i] != null) tableHit[i] = FirstHit(stars[i].transform.position, stars[i].transform.forward, stars[i].range, lt);
            Light glow = new SerializedObject(lamp).FindProperty("glow").objectReferenceValue as Light;
            float glowTable = glow != null ? FirstHit(glow.transform.position, Vector3.down, 200f, lt) : 0f;

            GameObject player = GameObject.Find("Player");
            var pcc = player != null ? player.GetComponent<CharacterController>() : null;
            float toyH = (pcc != null ? pcc.height : 1.8f) * toyScale;
            var things = new List<(string name, Vector3 at)>();
            Transform under = bedroom.Find("UnderBed");
            if (under != null) foreach (Transform t in under) if (t.GetComponent<DarkZone>() == null) things.Add((t.name, t.position));
            Transform gun = bedroom.Find(SnatchName + "/" + PickupName);
            float wander = ms != null ? ms.wanderRadius : 3f;
            Vector3 eye = new Vector3(mouth.x, floorY + toyH * 0.8f, mouth.z) + outDir * 2f;

            Vector3 best = Vector3.zero; float bestY = floorY, bestNear = float.MaxValue; int bestSees = -1, tried = 0, keptN = 0;
            var why = new Dictionary<string, int>();
            float margin = halfW + 1f;
            for (float x = cave.min.x + margin; x <= cave.max.x - margin; x += s.pickupGridStep)
                for (float z = cave.min.z + margin; z <= cave.max.z - margin; z += s.pickupGridStep)
                {
                    tried++;
                    var foot = new Vector3(x, floorY, z);   // the footprint's middle
                    if (Vector3.Dot(Level(foot - mouth), -outDir) < s.lampMinIn + halfW) { TallyWhy(why, "less than Lamp Min In past the bed's edge"); continue; }
                    if (LaneGap(foot, mouth, torch) < laneHalf + s.pickupLaneClear + halfW) { TallyWhy(why, "too near the monsters' way"); continue; }
                    bool nearMonster = false;
                    foreach (DarkMonster m in monsters)
                    {
                        var cc = m.GetComponent<CharacterController>();
                        float mr = cc != null ? cc.radius * m.transform.lossyScale.y : 1f;
                        if (Level(foot - m.transform.position).magnitude < wander + mr + s.pickupMonsterClear + halfW) { nearMonster = true; break; }
                    }
                    if (nearMonster) { TallyWhy(why, "too near a monster's spot"); continue; }
                    bool nearThing = false;
                    foreach (var (name, at) in things) if (Level(foot - at).magnitude < s.pickupThingsClear + halfW) { nearThing = true; break; }
                    if (nearThing) { TallyWhy(why, "too near the bed's own things"); continue; }
                    if (gun != null && Level(foot - gun.position).magnitude < s.lampGunClear + halfW) { TallyWhy(why, "too near the dart gun"); continue; }
                    float cy = FloorAt(foot, floorY + 3f, bed, out bool found);
                    if (!found || Mathf.Abs(cy - floorY) > 0.5f) { TallyWhy(why, "no cave floor"); continue; }
                    if (Headroom(new Vector3(x, cy, z), bed) < tallNew + 0.5f) { TallyWhy(why, "too little room overhead"); continue; }
                    bool solid = false;
                    foreach (Collider col in Physics.OverlapBox(new Vector3(x, cy + tallNew * 0.5f + 0.1f, z), new Vector3(halfW, tallNew * 0.5f, halfW), Quaternion.identity, ~0, QueryTriggerInteraction.Ignore))
                        if (col != null && col.GetComponentInParent<DarkMonster>() == null && !col.transform.IsChildOf(lt)) { solid = true; break; }
                    if (solid) { TallyWhy(why, "something solid there"); continue; }
                    Vector3 dome = new Vector3(x, cy + tallNew * 0.85f, z);
                    if (!ClearLine(eye, dome, monsters, lt)) { TallyWhy(why, "out of view from the way in"); continue; }
                    keptN++;
                    int sees = 0;
                    foreach (DarkMonster m in monsters) if (ClearLine(dome, MonsterMiddle(m), monsters, lt)) sees++;
                    float near = Level(foot - mouth).magnitude;
                    if (sees > bestSees || (sees == bestSees && near < bestNear)) { bestSees = sees; bestNear = near; best = foot; bestY = cy; }
                }
            var reasons = new StringBuilder();
            foreach (var kv in why) reasons.Append(reasons.Length > 0 ? ", " : "").Append(kv.Value).Append(' ').Append(kv.Key);
            if (keptN == 0)
            {
                Log.AppendLine($"LAMP UNDER THE BED NOT moved: none of {tried} cave floor points is free for a lamp {halfW * 2f:F1} across and {tallNew:F1} tall ({reasons}). Lower Lamp Scale, Lamp Min In or Lamp Gun Clear, or untick Lamp Under Bed.");
                return;
            }

            // The move: shrunk, its footprint's middle on the place, its bottom on the floor, its turn kept.
            Vector3 tablePos = lt.position, tableScale = lt.localScale, tableTurn = lt.eulerAngles;
            lt.localScale = tableScale * k;
            lt.position = new Vector3(best.x, bestY + 0.01f - below, best.z) - centreOff;
            PrefabUtility.RecordPrefabInstancePropertyModifications(lt);
            Physics.SyncTransforms();

            // One light on the bed's underside for every star: the middle of what they gave on the table.
            var onTable = new List<float>();
            for (int i = 0; i < stars.Count; i++) if (stars[i] != null) onTable.Add(stars[i].intensity / Mathf.Max(1e-4f, tableHit[i] * tableHit[i]));
            onTable.Sort();
            float lightOn = onTable.Count > 0 ? onTable[onTable.Count / 2] : 0f;
            var lines = new StringBuilder();
            for (int i = 0; i < stars.Count; i++)
            {
                Light l = stars[i];
                if (l == null) continue;
                float now = FirstHit(l.transform.position, l.transform.forward, s.lampStarRange, lt);
                float was = Mathf.Max(0.01f, tableHit[i]);
                float iWas = l.intensity, rWas = l.range;
                l.intensity = Mathf.Min(iWas, lightOn * now * now);
                l.range = s.lampStarRange;
                PrefabUtility.RecordPrefabInstancePropertyModifications(l);
                lines.Append(lines.Length > 0 ? "; " : "").Append($"{l.name}: lit {was:F1} away on the table, {now:F1} now, brightness {iWas:F1} -> {l.intensity:F2}, reach {rWas:F0} -> {l.range:F0}");
            }
            string glowLine = "no glow light";
            if (glow != null)
            {
                float now = FirstHit(glow.transform.position, Vector3.down, 200f, lt);
                float f = Mathf.Clamp(now * now / Mathf.Max(0.01f, glowTable * glowTable), 0.0005f, 1f);
                float iWas = glow.intensity, rWas = glow.range;
                glow.intensity = iWas * f;
                glow.range = Mathf.Min(glow.range, s.lampStarRange);
                PrefabUtility.RecordPrefabInstancePropertyModifications(glow);
                glowLine = $"its glow {glow.name}: {glowTable:F1} over the table, {now:F1} over the floor now, brightness {iWas:F1} x {f:F3} = {glow.intensity:F2}, reach {rWas:F0} -> {glow.range:F0}";
            }
            string settings = LampSettings(lamp, s);
            float pastEdge = Vector3.Dot(Level(best - mouth), -outDir);
            Log.AppendLine($"LAMP UNDER THE BED (D-0247): moved {Path(lt)} off the bedside table {Fmt(tablePos)} (scale {tableScale.x:F2}, turned {tableTurn.y:F0}) to {Fmt(lt.position)} (scale {lt.localScale.x:F2}: {halfW * 2f:F1} across, {tallNew:F1} tall under {Headroom(new Vector3(best.x, bestY, best.z), bed):F1} of room), its middle {pastEdge:F1} past the bed's edge and {bestNear:F1} from the way in{(gun != null ? $", {Level(best - gun.position).magnitude:F1} from the dart gun" : "")}; its dome sees {bestSees} of {monsters.Count} monster spots; the best of {keptN} free of {tried} cave floor points (ruled out: {reasons}).");
            Log.AppendLine($"  ITS STARS light the bed's underside as they lit the ceiling, all alike ({lightOn:F3} on what they light: the middle of the table's): {lines}. {glowLine}. {settings}.");
            Log.AppendLine($"  BACK ON THE TABLE, if he wants it: untick Lamp Under Bed, then put Room/Bedroom/NightLamp at {Fmt(tablePos)}, turned {tableTurn.y:F0}, scale {tableScale.x:F2}, and set each star's brightness and reach back to what they were (above).");
        }

        /// <summary>D-0245: the glint material ends a run with its emission on. Something during a run of this maker switches
        /// it off again (not found: GlintMaterial switches it on and saves it, and a plain switch-on and save afterwards
        /// holds), so it is switched on and saved again as the run's last step. The gun's LightGlow also switches it on at
        /// start (Keep Emission On).</summary>
        static void KeepGlintEmission()
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(GlintMaterialPath);
            if (m == null) return;
            bool wasOn = m.IsKeywordEnabled("_EMISSION");
            m.EnableKeyword("_EMISSION");
            EditorUtility.SetDirty(m);
            AssetDatabase.SaveAssetIfDirty(m);
            Log.AppendLine($"GLINT MATERIAL (D-0245): emission {(wasOn ? "on" : "had been switched OFF during the run")}: switched on and saved last.");
        }

        /// <summary>D-0247: the lamp under the bed's own settings: the sweep (and the stun) reaches Lamp Sweep Range, and Sweep
        /// Through Furniture goes off (nothing is in the way there).</summary>
        static string LampSettings(StarLamp lamp, BedroomGateSettings s)
        {
            var so = new SerializedObject(lamp);
            so.FindProperty("sweepRange").floatValue = s.lampSweepRange;
            so.FindProperty("sweepThroughFurniture").boolValue = false;
            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.RecordPrefabInstancePropertyModifications(lamp);
            return $"Sweep Range {s.lampSweepRange} (the stun reaches as far), Sweep Through Furniture off";
        }

        /// <summary>The nearest thing a ray meets, not part of <paramref name="skip"/>, or <paramref name="max"/> when nothing.</summary>
        static float FirstHit(Vector3 from, Vector3 dir, float max, Transform skip)
        {
            float best = max;
            foreach (RaycastHit h in Physics.RaycastAll(from, dir, max, ~0, QueryTriggerInteraction.Ignore))
                if (!h.collider.transform.IsChildOf(skip) && h.distance < best) best = h.distance;
            return best;
        }

        /// <summary>Nothing solid between the two points (the monsters and <paramref name="skip"/> do not count).</summary>
        static bool ClearLine(Vector3 from, Vector3 to, List<DarkMonster> monsters, Transform skip)
        {
            Vector3 d = to - from;
            float len = d.magnitude;
            if (len < 1e-3f) return true;
            foreach (RaycastHit h in Physics.RaycastAll(from, d / len, len, ~0, QueryTriggerInteraction.Ignore))
            {
                if (skip != null && h.collider.transform.IsChildOf(skip)) continue;
                if (h.collider.GetComponentInParent<DarkMonster>() != null) continue;
                return false;
            }
            return true;
        }

        /// <summary>A monster's middle, from its CharacterController.</summary>
        static Vector3 MonsterMiddle(DarkMonster m)
        {
            var cc = m.GetComponent<CharacterController>();
            return cc != null ? m.transform.TransformPoint(cc.center) : m.transform.position + Vector3.up * m.transform.lossyScale.y;
        }

        /// <summary>Null when a swept spot crosses <paramref name="point"/> (in the band, in reach, nothing solid between);
        /// else why not.</summary>
        static string Crosses(Vector3 dome, Vector3 point, float half, float range, float pitch, Transform lamp, Transform monster)
        {
            Vector3 to = point - dome;
            float distance = to.magnitude;
            if (distance < 1e-3f) return null;
            float under = Mathf.Atan2(-to.y, Level(to).magnitude) * Mathf.Rad2Deg;   // degrees under the level
            if (distance > range) return $"{distance:F0} away, past the reach {range}";
            if (Mathf.Abs(under - pitch) > half) return $"{under:F0} degrees under the dome, outside the swept band";
            float nearest = float.MaxValue; string what = null;
            foreach (RaycastHit h in Physics.RaycastAll(dome, to / distance, distance, ~0, QueryTriggerInteraction.Ignore))
            {
                Transform t = h.collider.transform;
                if (t.IsChildOf(lamp) || (monster != null && t.IsChildOf(monster)) || h.collider.GetComponentInParent<DarkMonster>() != null) continue;
                if (h.distance < nearest) { nearest = h.distance; what = h.collider.name; }
            }
            return what == null ? null : $"{what} in the way";
        }

        // ------------------------------------------------------------------------------------------------ helpers

        static T Find<T>(Transform root, string path) where T : Component
        {
            Transform t = root.Find(path);
            return t != null ? t.GetComponent<T>() : null;
        }

        /// <summary>The floor's height under <paramref name="at"/>: the first up-facing hit going down from
        /// <paramref name="fromY"/>, not the bed, a monster or anything with a body.</summary>
        static float FloorAt(Vector3 at, float fromY, Transform bed, out bool found)
        {
            found = false;
            float best = float.MaxValue, y = at.y;
            foreach (RaycastHit h in Physics.RaycastAll(new Vector3(at.x, fromY, at.z), Vector3.down, fromY + 30f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (h.normal.y < 0.7f || h.collider.attachedRigidbody != null || h.collider.GetComponentInParent<DarkMonster>() != null) continue;
                if (bed != null && h.collider.transform.IsChildOf(bed)) continue;
                if (h.distance < best) { best = h.distance; y = h.point.y; found = true; }
            }
            return y;
        }

        /// <summary>The room over a floor point up to the bed's frame (0 when the bed is not met within 60).</summary>
        static float Headroom(Vector3 floor, Transform bed)
        {
            float room = 0f, nearest = float.MaxValue;
            foreach (RaycastHit h in Physics.RaycastAll(floor + Vector3.up * 0.2f, Vector3.up, 60f, ~0, QueryTriggerInteraction.Ignore))
                if (h.collider.transform.IsChildOf(bed) && h.distance < nearest) { nearest = h.distance; room = h.distance + 0.2f; }
            return room;
        }

        /// <summary>Anything solid in a small ball at <paramref name="at"/> (the monsters and <paramref name="skip"/> aside).</summary>
        static string Solid(Vector3 at, float radius, Transform skip)
        {
            foreach (Collider c in Physics.OverlapSphere(at, radius, ~0, QueryTriggerInteraction.Ignore))
            {
                if (c == null || (skip != null && c.transform.IsChildOf(skip)) || c.GetComponentInParent<DarkMonster>() != null) continue;
                return $"{c.name} lies there (the gun is drawn over it)";
            }
            return "nothing else lies there";
        }

        static float LaneGap(Vector3 p, Vector3 a, Vector3 b) => Level(p - Closest(Level(a), Level(b), Level(p))).magnitude;

        static float NearestHome(Vector3 p, List<DarkMonster> monsters, out string name)
        {
            name = "none"; float best = float.MaxValue;
            foreach (DarkMonster m in monsters)
            {
                float d = Level(p - m.transform.position).magnitude;
                if (d < best) { best = d; name = m.name; }
            }
            return best;
        }

        static Vector3 Closest(Vector3 a, Vector3 b, Vector3 p)
        {
            Vector3 ab = b - a;
            float t = ab.sqrMagnitude > 1e-6f ? Mathf.Clamp01(Vector3.Dot(p - a, ab) / ab.sqrMagnitude) : 0f;
            return a + ab * t;
        }

        static void SetPoints(SerializedProperty list, Vector3 a, Vector3 b)
        {
            list.arraySize = 2;
            list.GetArrayElementAtIndex(0).vector3Value = a;
            list.GetArrayElementAtIndex(1).vector3Value = b;
        }

        static void SetRenderers(SerializedProperty list, Renderer[] parts)
        {
            list.arraySize = parts.Length;
            for (int i = 0; i < parts.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = parts[i];
        }

        static Bounds BoundsOf(GameObject go)
        {
            bool any = false; var b = new Bounds(go.transform.position, Vector3.zero);
            foreach (Renderer r in go.GetComponentsInChildren<Renderer>(true))
            {
                if (r is ParticleSystemRenderer) continue;
                if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
            }
            return b;
        }

        static void Remove(Transform parent, string name)
        {
            for (Transform t = parent.Find(name); t != null; t = parent.Find(name)) Object.DestroyImmediate(t.gameObject);
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
            if (t == null) return "(none)";
            var sb = new StringBuilder(t.name);
            for (Transform p = t.parent; p != null; p = p.parent) sb.Insert(0, p.name + "/");
            return sb.ToString();
        }

        static string Names(List<DarkMonster> monsters)
        {
            var sb = new StringBuilder();
            foreach (DarkMonster m in monsters) sb.Append(sb.Length > 0 ? ", " : "").Append(m.name);
            return sb.ToString();
        }

        static string Fmt(Vector3 v) => $"({v.x:F1}, {v.y:F2}, {v.z:F1})";
        static Vector3 Level(Vector3 v) { v.y = 0f; return v; }
        static Vector3 Abs(Vector3 v) => new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));
    }
}
#endif
