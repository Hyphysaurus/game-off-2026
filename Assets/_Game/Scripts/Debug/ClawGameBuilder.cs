#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace ShiftJam
{
    /// <summary>
    /// The claw machine you can play (D-0229), landed into the OPEN Greybox; it never opens or saves a scene.
    /// Menu: Shift Jam / House / Land Claw Game (D-0229); <see cref="Land"/> returns the report (one fact a line; a report
    /// that starts "[Claw Game] NOT " changed nothing).
    /// 1. Resources/ClawGameSettings: made if missing; an existing asset keeps its numbers (only an empty sound is filled
    ///    with the claw pack's own SFX, and a missing prize name added).
    /// 2. Room/Bedroom/ClawMachine's parts found by name under Toy_Claw_01 (the gantry Claw_Mechanism_01, the carriage
    ///    Claw_Mechanism_02, the rod Claw_Piston_01, the head Claw_Piston_02, its three Claw_01 fingers, the flap Cap_01, the
    ///    Lever_01 and Button_01 panel), their parents and turns checked against the claw pack's own mechanism.
    /// 3. Measured in the machine's own space: the glass case (the body's own glass sub-mesh, as the monsters' maker found
    ///    it), the prize floor and the chute's hole (rays straight down on the body's own collider), the claw (its rest, its
    ///    fingers' hinge, how wide it opens and how low its tips sweep), the steer area (the open claw, the carriage and the
    ///    gantry kept off the glass), the point over the chute.
    /// 4. Placed under ClawMachine (each replaced on a second run, never doubled): the play spot (a trigger in front of the
    ///    panel, ClawGamePlaySpot, on Ignore Raycast), the framed view of the glass (fitted at the settings' field of view,
    ///    16:9, pulled in short of any wall), the chute's view, and the exit in front of the flap.
    /// 5. ClawGame (and its ClawGameView) on ClawMachine with every part, place and measure set, and the input actions.
    /// 6. The gold star (Settings > Star Id) on Resources/ObjectiveSet, added once (his title and room never overwritten),
    ///    and an ObjectiveHook on ClawMachine (outside "ObjectiveHooks (D-0220)", so Build Objectives never deletes it),
    ///    wired to ClawGame's On Won (an old hook listener there is replaced, not doubled).
    /// The scene is marked changed and not saved (Ctrl+S keeps it).
    /// </summary>
    public static class ClawGameBuilder
    {
        const string Tag = "[Claw Game]";
        const string ScenePath = "Assets/_Game/Scenes/Greybox.unity";
        const string SettingsPath = "Assets/_Game/Resources/" + ClawGameSettings.ResourceName + ".asset";
        const string ActionsPath = "Assets/_Game/Input/InputSystem_Actions.inputactions";
        const string ShiftSettingsPath = "Assets/_Game/Data/ShiftSettings.asset";
        const string Sfx = "Assets/ithappy/Claw_Machine_Free/SFX/";
        const string LandSoundPath = "Assets/_Imports/ithappy/Playroom/Playroom_Sound_FX/Toys/Ball_Bounce_v1.wav";
        const string MachinePath = "Room/Bedroom/ClawMachine", PrizesPath = "Room/Bedroom/ClawPrizes";
        const string SpotName = "ClawPlaySpot (D-0229)", ViewName = "ClawView (D-0229)", ChuteViewName = "ClawChuteView (D-0229)", ExitName = "ClawExit (D-0229)";
        const int ModeVoid = 1, RuntimeOnly = 2;   // PersistentListenerMode.Void, UnityEventCallState.RuntimeOnly
        const int IgnoreRaycastLayer = 2;

        // The claw pack's own numbers (ClawMechanism and ClawPresentation on its PlayableClawMachine): the report's yardstick
        // and the fallback when a measure fails.
        static readonly Vector4 PackSteer = new Vector4(-0.30f, 0.30f, -0.24f, 0.226f);
        static readonly Vector2 PackChute = new Vector2(0.2596f, 0.2258f);
        static readonly Vector3 PackTip = new Vector3(0f, -0.183f, 0.011f);   // ClawFinger.surface's last point: the fingertip
        const float PackExitOut = 0.26f;      // the pack's "Hatch exit" sits this far in front of the flap (0.79 - 0.52)

        const int FloorGrid = 18;             // rays across the glass each way for the floor
        const float FloorEdge = 0.02f;        // the rays keep this far in from the glass (machine units)
        const float HoleDrop = 0.04f;         // a floor point this far under the prize floor is the chute's hole (machine units)
        const float FloorUp = 0.7f;           // a floor faces up at least this much
        const float SpotDepth = 0.28f, SpotRise = 0.22f, SpotWiden = 0.3f;   // the play spot's box past the panel (machine units)
        const float ViewAspect = 16f / 9f;

        static readonly StringBuilder Log = new StringBuilder();

        [MenuItem("Shift Jam/House/Land Claw Game (D-0229)")]
        public static void LandMenu()
        {
            string report = Land();
            if (report.StartsWith(Tag + " NOT ")) Debug.LogWarning(report); else Debug.Log(report);
        }

        /// <summary>Lands (or lands again) the playable claw machine in the OPEN Greybox and marks it changed; it does not save
        /// it. Returns the report, one fact a line; "[Claw Game] NOT " at its start means nothing was changed.</summary>
        public static string Land()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return $"{Tag} NOT landed: this does not run in Play. Stop the game first. Nothing was changed.";
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded || scene.path != ScenePath)
                return $"{Tag} NOT landed: the active scene is '{scene.path}', and the claw machine is in {ScenePath}. Open Greybox and run this again (it never opens a scene by itself). Nothing was changed.";
            GameObject holderGo = GameObject.Find(MachinePath);
            if (holderGo == null) return $"{Tag} NOT landed: no {MachinePath} in {ScenePath} (run Shift Jam > House > Land Start Room Toys first). Nothing was changed.";
            Transform holder = holderGo.transform;
            Transform machine = FindDeep(holder, "Toy_Claw_01");
            if (machine == null) return $"{Tag} NOT landed: no Toy_Claw_01 under {MachinePath}. Nothing was changed.";

            // The parts, by the names the claw pack gives them.
            Transform rail = FindDeep(machine, "Claw_Mechanism_01"), carriage = FindDeep(machine, "Claw_Mechanism_02");
            Transform rod = FindDeep(machine, "Claw_Piston_01"), head = FindDeep(machine, "Claw_Piston_02");
            Transform hatch = FindDeep(machine, "Cap_01"), lever = FindDeep(machine, "Lever_01"), button = FindDeep(machine, "Button_01");
            var fingerList = new List<Transform>();
            if (head != null) foreach (Transform c in head) if (c.name.StartsWith("Claw_01")) fingerList.Add(c);
            var missing = new List<string>();
            if (rail == null) missing.Add("Claw_Mechanism_01 (the gantry)");
            if (carriage == null) missing.Add("Claw_Mechanism_02 (the carriage)");
            if (rod == null) missing.Add("Claw_Piston_01 (the rod)");
            if (head == null) missing.Add("Claw_Piston_02 (the head)");
            if (fingerList.Count == 0) missing.Add("Claw_01 fingers under the head");
            if (hatch == null) missing.Add("Cap_01 (the chute's flap)");
            if (lever == null && button == null) missing.Add("Lever_01 / Button_01 (the panel)");
            MeshFilter bodyMesh = machine.GetComponent<MeshFilter>();
            MeshRenderer bodyLook = machine.GetComponent<MeshRenderer>();
            MeshCollider bodyCollider = machine.GetComponent<MeshCollider>();
            if (bodyMesh == null || bodyMesh.sharedMesh == null || bodyLook == null) missing.Add("the body's mesh on Toy_Claw_01");
            if (bodyCollider == null) missing.Add("the body's MeshCollider on Toy_Claw_01 (Land Start Room Toys adds it)");
            if (missing.Count > 0) return $"{Tag} NOT landed: under {MachinePath} there is no {string.Join("; no ", missing)}. Nothing was changed.";
            int glassIndex = -1;
            Material[] mats = bodyLook.sharedMaterials;
            for (int i = 0; i < mats.Length; i++) if (IsGlass(mats[i])) { glassIndex = i; break; }
            if (glassIndex < 0 || glassIndex >= bodyMesh.sharedMesh.subMeshCount)
                return $"{Tag} NOT landed: Toy_Claw_01 wears no Glass material on a sub-mesh of its own (the case is found by it). Nothing was changed.";
            GameObject prizesGo = GameObject.Find(PrizesPath);
            if (prizesGo == null) return $"{Tag} NOT landed: no {PrizesPath} (run Shift Jam > House > Land Bedroom Monsters first). Nothing was changed.";
            Transform prizes = prizesGo.transform;

            Log.Clear();
            ClawGame existing = holder.GetComponent<ClawGame>();
            Log.AppendLine($"{Tag} Land Claw Game (D-0229): {(existing != null ? "SECOND run: what the first placed is replaced (its ClawGame reused, its play spot, views and exit placed again)" : "first run")}. Greybox is MARKED CHANGED and NOT SAVED: Ctrl+S keeps it.");
            Physics.SyncTransforms();

            ClawGameSettings s = Settings();
            float unit = machine.lossyScale.y;
            Transform[] fingers = fingerList.ToArray();
            Log.AppendLine($"PARTS under {Path(machine)} (x{unit:F2} to the world): gantry {Part(rail, machine)}; carriage {Part(carriage, rail)}; rod {Part(rod, carriage)}; head {Part(head, rod)}; {fingers.Length} fingers {Fingers(fingers)}; flap {Part(hatch, machine, false)}; lever {(lever != null ? Part(lever, machine, false) : "NONE")}; button {(button != null ? Part(button, machine, false) : "NONE")}. Expected as the pack's own mechanism: each part's parent the one before, every turn and scale as the model has them (identity, 1).");

            // ---- the case and its floor
            Bounds glass = bodyMesh.sharedMesh.GetSubMesh(glassIndex).bounds;
            Log.AppendLine($"CASE: the body's glass ('{mats[glassIndex].name}', sub-mesh {glassIndex}) x {glass.min.x:F3}..{glass.max.x:F3}, y {glass.min.y:F3}..{glass.max.y:F3}, z {glass.min.z:F3}..{glass.max.z:F3} in the machine's own space = {glass.size.x * unit:F1} x {glass.size.y * unit:F1} x {glass.size.z * unit:F1} in the world, its middle at {machine.TransformPoint(glass.center)}. The machine's front is its +z (the panel's side).");
            FloorScan(machine, bodyCollider, glass, unit, out float floorY, out bool holeFound, out Vector2 holeMiddle, out float holeY, out int floorHits, out int holeHits);

            // ---- the claw
            ClawGame current = existing;
            Vector3 tip = PackTip;
            if (current != null)
            {
                SerializedProperty tipProp = new SerializedObject(current).FindProperty("fingerTip");
                if (tipProp != null && tipProp.vector3Value.sqrMagnitude > 1e-8f) tip = tipProp.vector3Value;
            }
            var claw = new ClawGameClaw(machine, rail, carriage, rod, head, fingers, lever, button, tip, 1f);
            float close = claw.MeasuredCloseSign();
            if (close < 0f) claw = new ClawGameClaw(machine, rail, carriage, rod, head, fingers, lever, button, tip, -1f);
            float openR = claw.TipRadius(s.fingerOpen, true);
            float restR = claw.TipRadius(0f, true);
            float belowOpen = claw.TipBelow(s.fingerOpen), belowMost = claw.LowestBetween(s.fingerOpen, s.fingerClosed);
            Vector2 home = claw.Home;
            Bounds fingerBox = MeshBox(fingers[0], fingers[0]);
            Log.AppendLine($"CLAW at rest: its middle at x {home.x:F3}, z {home.y:F3} (the machine's own; the pack's demo parks it at 0, 0), its fingers' hinges at y {claw.GripRestY:F3} ({machine.TransformPoint(new Vector3(home.x, claw.GripRestY, home.y)).y:F1} in the world, the prize floor {floorY * unit + machine.position.y:F1}). Fingertip used {tip} (the pack's ClawFinger measure; the finger mesh reaches down to {fingerBox.min.y:F3} in its own space). The fingers close at {(close > 0f ? "POSITIVE angles, as in the pack" : "NEGATIVE angles (the other way from the pack): Finger Close set to -1")}. Open ({s.fingerOpen} degrees) the tips are {openR:F3} from the middle ({openR * unit:F1} in the world; as modelled {restR:F3}) and {belowOpen:F3} under the hinges; closing they sweep down to {belowMost:F3} under them.");

            // ---- the steer area
            Vector4 steer = SteerArea(glass, openR, s.wallGap, home, MeshBoxIn(carriage, machine), MeshBoxIn(rail, machine), out string steerWhy);
            Log.AppendLine($"STEER AREA (the claw's middle): x {steer.x:F3}..{steer.y:F3}, z {steer.z:F3}..{steer.w:F3} ({(steer.y - steer.x) * unit:F1} x {(steer.w - steer.z) * unit:F1} in the world); {steerWhy}. The pack's own: x {PackSteer.x}..{PackSteer.y}, z {PackSteer.z}..{PackSteer.w}. The rest corner ({home.x:F3}, {home.y:F3}) is {(Inside(home, steer) ? "inside it" : "OUTSIDE it: each round starts by bringing the claw in, and it goes back there after")}.");

            // ---- over the chute
            Vector2 chute = holeFound ? holeMiddle : PackChute;
            Vector2 chuteAt = new Vector2(Mathf.Clamp(chute.x, steer.x, steer.y), Mathf.Clamp(chute.y, steer.z, steer.w));
            float underChute = FloorAt(machine, bodyCollider, glass, unit, chuteAt, out bool chuteHit);
            bool overHole = chuteHit && underChute < floorY - 0.5f * HoleDrop;
            if (!holeFound) holeY = floorY - 0.1f;
            Log.AppendLine($"CHUTE: {(holeFound ? $"the hole found at {holeHits} of {FloorGrid * FloorGrid} points, its middle x {holeMiddle.x:F3}, z {holeMiddle.y:F3}, its floor y {holeY:F3} ({holeY * unit + machine.position.y:F1} in the world)" : $"NO hole found (no floor point {HoleDrop} under the prize floor): the pack's own point {PackChute} is used, the hole's floor taken as {holeY:F3}")}; the prize floor y {floorY:F3} at {floorHits} points. A held prize is let go at x {chuteAt.x:F3}, z {chuteAt.y:F3} (the hole's middle kept inside the steer area; the pack's own {PackChute}); {(overHole ? $"the floor under it is the hole's ({underChute:F3})" : $"WARNING: the floor under that point is {(chuteHit ? underChute.ToString("F3") : "not found")}, not the hole's: a won prize sinks into the floor there")}.");

            // ---- the panel and the play spot
            Bounds panel = PanelBox(lever, button, machine);
            Vector3 spotLocal = new Vector3(panel.center.x, panel.center.y, panel.max.z + 0.5f * SpotDepth);
            Vector3 spotSize = new Vector3(Mathf.Max(panel.size.x + SpotWiden, 0.45f), Mathf.Max(panel.size.y + SpotRise, 0.4f), SpotDepth);
            Transform spot = Child(holder, SpotName);
            spot.gameObject.layer = IgnoreRaycastLayer;
            spot.SetPositionAndRotation(machine.TransformPoint(spotLocal), machine.rotation);
            spot.localScale = Vector3.one;
            Vector3 spotLossy = spot.lossyScale;
            BoxCollider box = spot.GetComponent<BoxCollider>();
            if (box == null) box = spot.gameObject.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.center = Vector3.zero;
            box.size = new Vector3(spotSize.x * unit / Mathf.Max(1e-4f, spotLossy.x), spotSize.y * unit / Mathf.Max(1e-4f, spotLossy.y), spotSize.z * unit / Mathf.Max(1e-4f, spotLossy.z));
            ClawGamePlaySpot front = spot.GetComponent<ClawGamePlaySpot>();
            if (front == null) front = spot.gameObject.AddComponent<ClawGamePlaySpot>();
            Physics.SyncTransforms();
            Log.AppendLine($"PLAY SPOT: {Path(spot)} (a trigger on Ignore Raycast with ClawGamePlaySpot: E works at the front only) in front of the panel (lever and button x {panel.min.x:F3}..{panel.max.x:F3}, y {panel.min.y:F3}..{panel.max.y:F3}, front z {panel.max.z:F3}), {spotSize.x * unit:F1} x {spotSize.y * unit:F1} x {spotSize.z * unit:F1} in the world at {spot.position}. {KidCheck(holder, machine, panel, unit, box)}");

            // ---- the exit in front of the flap
            Bounds cap = MeshBoxIn(hatch, machine) ?? new Bounds(machine.InverseTransformPoint(hatch.position), Vector3.one * 0.1f);
            Vector3 exitLocal = new Vector3(cap.center.x, cap.center.y - 0.06f * cap.size.y, cap.max.z + PackExitOut);
            Transform exitT = Child(holder, ExitName);
            exitT.SetPositionAndRotation(machine.TransformPoint(exitLocal), machine.rotation);
            Vector3 land = Below(exitT.position, holder, prizes, unit, out string landOn);
            Log.AppendLine($"EXIT: {Path(exitT)} at {exitT.position} (machine {exitLocal}): the flap Cap_01 spans x {cap.min.x:F3}..{cap.max.x:F3}, y {cap.min.y:F3}..{cap.max.y:F3} ({cap.min.y * unit + machine.position.y:F1}..{cap.max.y * unit + machine.position.y:F1} in the world), its face at z {cap.max.z:F3}; the exit is {PackExitOut} in front of it (the pack's own hatch exit). Under it: {landOn} at y {land.y:F2}, {exitT.position.y - land.y:F1} down. The flap swings {s.hatchOpenAngle} degrees about its own x (the pack's).");

            // ---- the views
            Vector3 frontWorld = machine.forward; frontWorld.y = 0f; frontWorld.Normalize();
            Quaternion facing = Quaternion.LookRotation(-frontWorld, Vector3.up);
            Quaternion glassTurn = Quaternion.AngleAxis(s.viewYaw, Vector3.up) * facing * Quaternion.Euler(s.viewPitch, 0f, 0f);
            Vector3 middle = machine.TransformPoint(glass.center);
            float fit = Fit(Corners(glass, machine), middle, glassTurn, s.viewFieldOfView, ViewAspect, s.viewMargin);
            Vector3 glassWant = middle - glassTurn * Vector3.forward * fit;
            float ball = Mathf.Max(0.001f, s.viewClearance * unit);
            float glassFree = Sweep(middle, glassWant, ball, holder, prizes, out string glassBlock);
            Transform viewT = Child(holder, ViewName);
            viewT.SetPositionAndRotation(middle + (glassWant - middle).normalized * glassFree, glassTurn);
            Log.AppendLine($"VIEW of the glass: {Path(viewT)} at {viewT.position}, looking {s.viewPitch} degrees down{(Mathf.Abs(s.viewYaw) > 0.01f ? $", turned {s.viewYaw} degrees round" : ", straight in front")}, field of view {s.viewFieldOfView} (16:9, {s.viewMargin:P0} kept free round the glass): {fit:F1} from the glass's middle; {(glassBlock == null ? "nothing in the way" : $"PULLED IN to {glassFree:F1} by {glassBlock} (the glass is cut off at the edges: lower View Pitch or widen View Field Of View, then run this again)")}. Its height {viewT.position.y:F1} (a kid stands 63).");

            Vector3 chuteLook = Vector3.Lerp(exitT.position, land, 0.5f) + Vector3.up * (0.1f * unit);
            Quaternion chuteTurn = facing * Quaternion.Euler(s.chuteViewPitch, 0f, 0f);
            float chuteDistance = 0.5f * s.chuteViewSpan * unit / Mathf.Tan(0.5f * s.viewFieldOfView * Mathf.Deg2Rad);
            Vector3 chuteWant = chuteLook - chuteTurn * Vector3.forward * chuteDistance;
            float chuteFree = Sweep(chuteLook, chuteWant, ball, holder, prizes, out string chuteBlock);
            Transform chuteT = Child(holder, ChuteViewName);
            chuteT.SetPositionAndRotation(chuteLook + (chuteWant - chuteLook).normalized * chuteFree, chuteTurn);
            Log.AppendLine($"VIEW of the chute: {Path(chuteT)} at {chuteT.position}, looking {s.chuteViewPitch} degrees down at the flap and the floor in front ({s.chuteViewSpan * unit:F1} top to bottom): {chuteDistance:F1} away; {(chuteBlock == null ? "nothing in the way" : $"PULLED IN to {chuteFree:F1} by {chuteBlock}")}. At run time both views are pulled in again if anything has come into the way.");

            // ---- the prizes
            ReportPrizes(prizes, machine, glass, unit, s);

            // ---- ClawGame
            ClawGame game = current != null ? current : holder.gameObject.AddComponent<ClawGame>();
            InputActionAsset actions = Actions();
            var so = new SerializedObject(game);
            var unset = new List<string>();
            SetRef(so, "machine", machine, unset); SetRef(so, "body", bodyCollider, unset);
            SetRef(so, "rail", rail, unset); SetRef(so, "carriage", carriage, unset); SetRef(so, "rod", rod, unset); SetRef(so, "head", head, unset);
            SetRef(so, "hatch", hatch, unset); SetRef(so, "lever", lever, unset); SetRef(so, "button", button, unset); SetRef(so, "prizes", prizes, unset);
            SetRef(so, "view", viewT, unset); SetRef(so, "chuteView", chuteT, unset); SetRef(so, "exit", exitT, unset); SetRef(so, "playSpot", spot, unset);
            SetRef(so, "actions", actions, unset);
            SerializedProperty fingerProp = so.FindProperty("fingers");
            if (fingerProp != null)
            {
                fingerProp.arraySize = fingers.Length;
                for (int i = 0; i < fingers.Length; i++) fingerProp.GetArrayElementAtIndex(i).objectReferenceValue = fingers[i];
            }
            else unset.Add("fingers");
            SetValue(so, "caseBox", glass, unset);
            SetValue(so, "steerArea", steer, unset);
            SetValue(so, "chuteAt", chuteAt, unset);
            SetValue(so, "floorY", floorY, unset);
            SetValue(so, "holeY", holeY, unset);
            SetValue(so, "fingerTip", tip, unset);
            SetValue(so, "fingerClose", close, unset);
            so.ApplyModifiedPropertiesWithoutUndo();
            var spotSo = new SerializedObject(front);
            SerializedProperty gameProp = spotSo.FindProperty("game");
            if (gameProp != null) { gameProp.objectReferenceValue = game; spotSo.ApplyModifiedPropertiesWithoutUndo(); }
            EditorUtility.SetDirty(game); EditorUtility.SetDirty(front);
            int movingColliders = rail.GetComponentsInChildren<Collider>(true).Length;
            Log.AppendLine($"CLAW GAME: {Path(holder)} {(current != null ? "ClawGame FOUND and set again" : "ClawGame ADDED")} (with its ClawGameView){(unset.Count > 0 ? "; NOT SET (no such field): " + string.Join(", ", unset) : ": every part, place and measure set")}. Input actions: {(actions != null ? AssetDatabase.GetAssetPath(actions) : "NONE FOUND (the machine will not be playable)")}. At run time the {movingColliders} colliders of the gantry, carriage, rod, head and fingers are switched off (they move; nothing can touch them inside the glass), a stretched copy of the rod's mesh is made under Claw_Piston_01, and nothing is saved from it.");

            // ---- the star
            Star(holder, game, s);

            EditorSceneManager.MarkSceneDirty(scene);
            Log.AppendLine($"Greybox MARKED CHANGED, not saved. Settings: {SettingsPath}. Online: {(s.online ? "ON (each PC's own machine, nothing networked)" : "off (no prompt in a party)")}.");
            Log.AppendLine("OFF SWITCHES: ClawGameSettings > On (a still prop again), Online; untick ClawGame on ClawMachine; untick the ObjectiveHook on ClawMachine (the star only). The pieces placed (play spot, views, exit) can be moved by hand; a second run of this places them again.");
            Log.AppendLine("TEST: as the kid, stand at the machine's front: \"E Play\". E: the view swings to the glass, WASD or the stick steers, E / Space (pad A or Y) drops, Esc (pad B) gives up, after the time limit it drops by itself. In Play, right-click ClawGame > Put Won Prizes Back In The Case to try again (New Game does the same).");
            return Log.ToString();
        }

        // ------------------------------------------------------------------------------------------------ the settings

        static ClawGameSettings Settings()
        {
            var s = AssetDatabase.LoadAssetAtPath<ClawGameSettings>(SettingsPath);
            bool made = s == null;
            if (made)
            {
                s = ScriptableObject.CreateInstance<ClawGameSettings>();
                AssetDatabase.CreateAsset(s, SettingsPath);
            }
            var filled = new List<string>();
            s.button = Fill(s.button, "Button_v4", filled);
            s.moveLoop = Fill(s.moveLoop, "Claw_Movement_v1", filled);
            s.dropLoop = Fill(s.dropLoop, "Claw_Down_v5", filled);
            s.fingerLoop = Fill(s.fingerLoop, "Claw_Movement_v2", filled);
            s.toyDrop = Fill(s.toyDrop, "Toy_Drop_v2", filled);
            s.win = Fill(s.win, "Win_Sound_v2", filled);
            s.miss = Fill(s.miss, "Lose_Sound_v2", filled);
            s.slip = Fill(s.slip, "Toy_Stuck_v3", filled);
            if (s.land.clip == null)
            {
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(LandSoundPath);
                if (clip != null) { CourseSounds.Cue c = s.land; c.clip = clip; s.land = c; filled.Add("land: Ball_Bounce_v1"); }
            }
            var names = new List<ClawGameSettings.PrizeWords>(s.prizeNames ?? new ClawGameSettings.PrizeWords[0]);
            var added = new List<string>();
            foreach (ClawGameSettings.PrizeWords d in ClawGameSettings.DefaultPrizeNames())
            {
                if (names.Exists(n => n != null && n.kind == d.kind)) continue;
                names.Add(d);
                added.Add(d.kind);
            }
            s.prizeNames = names.ToArray();
            EditorUtility.SetDirty(s);
            AssetDatabase.SaveAssetIfDirty(s);
            var empty = new List<string>();
            if (s.button.clip == null) empty.Add("Button");
            if (s.moveLoop.clip == null) empty.Add("Move Loop");
            if (s.dropLoop.clip == null) empty.Add("Drop Loop");
            if (s.win.clip == null) empty.Add("Win");
            Log.AppendLine($"SETTINGS: {SettingsPath} {(made ? "MADE with the defaults" : "kept as he left it")}{(filled.Count > 0 ? "; sounds filled from the claw pack: " + string.Join(", ", filled) : "; no sound needed filling")}{(added.Count > 0 ? "; prize names added: " + string.Join(", ", added) : "")}{(empty.Count > 0 ? "; STILL SILENT (the git-ignored claw pack is not on this PC?): " + string.Join(", ", empty) : "")}. Every word on it is a PLACEHOLDER for him to rewrite.");
            Log.AppendLine($"NUMBERS: round {s.timeLimit} s, steer {s.steerSpeed}/s (ease {s.steerEase} s), drop {s.dropSpeed}/s, rise {s.riseSpeed}/s, carry {s.carrySpeed}/s, fingers {s.fingerOpen}..{s.fingerClosed} degrees at {s.fingerSpeed}/s; grab reach {s.graspReach}, overlap needed {s.overlapNeeded}, grab chance {s.grabChance}, firm {s.firmOverlap}, slip chance {s.slipChance}; swing at most {s.maxSwing} degrees; view {s.viewFieldOfView} degrees, {s.viewPitch} down, in {s.blendIn} s, out {s.blendOut} s (lengths in machine units).");
            return s;
        }

        static CourseSounds.Cue Fill(CourseSounds.Cue cue, string clipName, List<string> filled)
        {
            if (cue.clip != null) return cue;
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(Sfx + clipName + ".wav");
            if (clip == null) return cue;
            cue.clip = clip;
            filled.Add(clipName);
            return cue;
        }

        static InputActionAsset Actions()
        {
            var asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(ActionsPath);
            if (asset != null) return asset;
            GameObject player = GameObject.Find("Player");
            PlayerInput input = player != null ? player.GetComponent<PlayerInput>() : null;
            if (input == null) return null;
            SerializedProperty p = new SerializedObject(input).FindProperty("actions");
            return p != null ? p.objectReferenceValue as InputActionAsset : null;
        }

        // ------------------------------------------------------------------------------------------------ measuring

        /// <summary>The prize floor and the chute's hole: rays straight down inside the glass on the body's own collider.
        /// The prize floor is the middle height of every up-facing hit; a hit well under it is the hole.</summary>
        static void FloorScan(Transform machine, Collider body, Bounds glass, float unit, out float floorY, out bool holeFound, out Vector2 holeMiddle, out float holeY, out int floorHits, out int holeHits)
        {
            var hits = new List<Vector3>();
            for (int ix = 0; ix < FloorGrid; ix++)
                for (int iz = 0; iz < FloorGrid; iz++)
                {
                    float x = Mathf.Lerp(glass.min.x + FloorEdge, glass.max.x - FloorEdge, (ix + 0.5f) / FloorGrid);
                    float z = Mathf.Lerp(glass.min.z + FloorEdge, glass.max.z - FloorEdge, (iz + 0.5f) / FloorGrid);
                    float y = FloorAt(machine, body, glass, unit, new Vector2(x, z), out bool hit);
                    if (hit) hits.Add(new Vector3(x, y, z));
                }
            floorY = glass.min.y; holeFound = false; holeMiddle = PackChute; holeY = glass.min.y; floorHits = 0; holeHits = 0;
            if (hits.Count == 0) { Log.AppendLine("FLOOR NOT FOUND: no ray straight down inside the glass met the body's collider facing up; the glass's foot is used."); return; }
            var heights = new List<float>();
            foreach (Vector3 h in hits) heights.Add(h.y);
            heights.Sort();
            floorY = heights[heights.Count / 2];
            Vector3 sum = Vector3.zero;
            foreach (Vector3 h in hits)
            {
                if (h.y < floorY - HoleDrop) { sum += h; holeHits++; }
                else if (Mathf.Abs(h.y - floorY) <= HoleDrop) floorHits++;
            }
            if (holeHits == 0) return;
            holeFound = true;
            sum /= holeHits;
            holeMiddle = new Vector2(sum.x, sum.z);
            holeY = sum.y;
        }

        /// <summary>The floor straight down at (x, z) in the machine's own space, from just under the glass's top.</summary>
        static float FloorAt(Transform machine, Collider body, Bounds glass, float unit, Vector2 at, out bool hit)
        {
            var ray = new Ray(machine.TransformPoint(new Vector3(at.x, glass.max.y - FloorEdge, at.y)), -machine.up);
            hit = body.Raycast(ray, out RaycastHit h, glass.size.y * 2f * unit) && Vector3.Dot(h.normal, machine.up) >= FloorUp;
            return hit ? machine.InverseTransformPoint(h.point).y : glass.min.y;
        }

        /// <summary>Where the claw's middle may go: the glass inset by the open claw's reach (and the wall gap), and further
        /// in where the carriage or the gantry would meet the glass first. The pack's own area when that leaves nothing.</summary>
        static Vector4 SteerArea(Bounds glass, float openR, float gap, Vector2 home, Bounds? carriageBox, Bounds? railBox, out string why)
        {
            float xMin = glass.min.x + openR + gap, xMax = glass.max.x - openR - gap;
            float zMin = glass.min.z + openR + gap, zMax = glass.max.z - openR - gap;
            var bounds = new List<string> { $"the open claw ({openR:F3}) and a {gap} gap off the glass" };
            if (carriageBox.HasValue)
            {
                Bounds c = carriageBox.Value;
                float a = glass.min.x + gap - (c.min.x - home.x), b = glass.max.x - gap - (c.max.x - home.x);
                float d = glass.min.z + gap - (c.min.z - home.y), e = glass.max.z - gap - (c.max.z - home.y);
                if (a > xMin || b < xMax || d > zMin || e < zMax) bounds.Add("the carriage's own size");
                xMin = Mathf.Max(xMin, a); xMax = Mathf.Min(xMax, b); zMin = Mathf.Max(zMin, d); zMax = Mathf.Min(zMax, e);
            }
            if (railBox.HasValue)
            {
                Bounds r = railBox.Value;
                float d = glass.min.z + gap - (r.min.z - home.y), e = glass.max.z - gap - (r.max.z - home.y);
                if (d > zMin || e < zMax) bounds.Add("the gantry's own depth");
                zMin = Mathf.Max(zMin, d); zMax = Mathf.Min(zMax, e);
            }
            if (xMin >= xMax || zMin >= zMax)
            {
                why = $"MEASURE FAILED ({xMin:F3}..{xMax:F3}, {zMin:F3}..{zMax:F3}): the pack's own area is used";
                return PackSteer;
            }
            // Never wider than the pack's own tuned area for this model (it keeps the claw off the case's corner posts,
            // which the glass's box does not show).
            string measured = $"measured x {xMin:F3}..{xMax:F3}, z {zMin:F3}..{zMax:F3}";
            float nx = Mathf.Max(xMin, PackSteer.x), xx = Mathf.Min(xMax, PackSteer.y), nz = Mathf.Max(zMin, PackSteer.z), xz = Mathf.Min(zMax, PackSteer.w);
            if (nx < xx && nz < xz)
            {
                bool cut = nx != xMin || xx != xMax || nz != zMin || xz != zMax;
                xMin = nx; xMax = xx; zMin = nz; zMax = xz;
                if (cut) bounds.Add("the pack's own area (never wider than it; " + measured + ")");
            }
            why = "kept in by " + string.Join(" and ", bounds);
            return new Vector4(xMin, xMax, zMin, zMax);
        }

        static bool Inside(Vector2 at, Vector4 area) => at.x >= area.x && at.x <= area.y && at.y >= area.z && at.y <= area.w;

        /// <summary>The lever and button's box in the machine's own space (the panel), from their meshes.</summary>
        static Bounds PanelBox(Transform lever, Transform button, Transform machine)
        {
            Bounds? a = lever != null ? MeshBoxIn(lever, machine) : null, b = button != null ? MeshBoxIn(button, machine) : null;
            if (a.HasValue && b.HasValue) { Bounds u = a.Value; u.Encapsulate(b.Value); return u; }
            if (a.HasValue) return a.Value;
            if (b.HasValue) return b.Value;
            Vector3 at = machine.InverseTransformPoint((lever != null ? lever : button).position);
            return new Bounds(at, Vector3.one * 0.05f);
        }

        /// <summary>Where the kid would stand to play (in front of the panel, on the floor), whether it is free, and whether
        /// the kid's Interactor search reaches the play spot from there.</summary>
        static string KidCheck(Transform holder, Transform machine, Bounds panel, float unit, BoxCollider spot)
        {
            GameObject player = GameObject.Find("Player");
            CharacterController body = player != null ? player.GetComponent<CharacterController>() : null;
            Shifter shifter = player != null ? player.GetComponent<Shifter>() : null;
            Interactor interactor = player != null ? player.GetComponent<Interactor>() : null;
            ShiftSettings shift = null;
            if (shifter != null) { SerializedProperty p = new SerializedObject(shifter).FindProperty("settings"); if (p != null) shift = p.objectReferenceValue as ShiftSettings; }
            if (shift == null) shift = AssetDatabase.LoadAssetAtPath<ShiftSettings>(ShiftSettingsPath);
            if (body == null || shift == null) return "The kid's standing spot was not checked (no Player with a CharacterController, or no ShiftSettings).";
            float kid = shift.kidScale;
            float radius = body.radius * kid, tall = Mathf.Max(body.height, 2f * body.radius) * kid;
            Vector3 front = machine.forward; front.y = 0f; front.Normalize();
            Vector3 panelFront = machine.TransformPoint(new Vector3(panel.center.x, panel.center.y, panel.max.z));
            Vector3 feet = new Vector3(panelFront.x, machine.position.y, panelFront.z) + front * (radius + 0.02f * unit);
            feet = Below(feet + Vector3.up * tall, holder, null, unit, out _);
            float lift = radius + 0.05f * radius;
            var blockers = new List<string>();
            foreach (Collider c in Physics.OverlapCapsule(feet + Vector3.up * lift, feet + Vector3.up * Mathf.Max(lift, tall - radius), radius, ~0, QueryTriggerInteraction.Ignore))
                if (c != null && !c.transform.IsChildOf(player.transform) && !blockers.Contains(c.name)) blockers.Add(c.name);
            string reach = "";
            if (interactor != null)
            {
                var iso = new SerializedObject(interactor);
                float r = iso.FindProperty("reach") != null ? iso.FindProperty("reach").floatValue : 1.6f;
                float h = iso.FindProperty("height") != null ? iso.FindProperty("height").floatValue : 0.9f;
                float searchR = r * kid * 0.5f;
                Vector3 toward = -front;
                Vector3 centre = feet + Vector3.up * (h * kid) + toward * searchR;
                Vector3 low = feet + Vector3.up * Mathf.Min(h * kid, searchR * 0.8f) + toward * searchR;
                Vector3 nearest = spot.bounds.ClosestPoint(Vector3.Lerp(low, centre, 0.5f));
                float gap = DistanceToSegment(nearest, low, centre);
                reach = gap <= searchR ? $" From there the kid's E search (radius {searchR:F1}) reaches the play spot." : $" WARNING: from there the kid's E search (radius {searchR:F1}) is {gap - searchR:F1} short of the play spot.";
            }
            return $"The kid ({tall:F0} tall, {radius:F1} round) would stand at {feet}: {(blockers.Count == 0 ? "free" : "BLOCKED by " + string.Join(", ", blockers))}.{reach}";
        }

        static float DistanceToSegment(Vector3 p, Vector3 a, Vector3 b)
        {
            Vector3 ab = b - a;
            float t = ab.sqrMagnitude > 1e-8f ? Mathf.Clamp01(Vector3.Dot(p - a, ab) / ab.sqrMagnitude) : 0f;
            return Vector3.Distance(p, a + ab * t);
        }

        /// <summary>What is straight under <paramref name="point"/> (the machine, the prizes and the player aside), and its name.</summary>
        static Vector3 Below(Vector3 point, Transform holder, Transform prizes, float unit, out string what)
        {
            GameObject player = GameObject.Find("Player");
            float best = float.MaxValue;
            Vector3 at = new Vector3(point.x, holder.position.y, point.z);
            what = "nothing (the machine's own floor height used)";
            foreach (RaycastHit h in Physics.RaycastAll(point, Vector3.down, 4f * unit, Layers.Seen(~0), QueryTriggerInteraction.Ignore))
            {
                Transform t = h.transform;
                if (t.IsChildOf(holder) || (prizes != null && t.IsChildOf(prizes)) || (player != null && t.IsChildOf(player.transform))) continue;
                if (h.distance >= best) continue;
                best = h.distance; at = h.point; what = h.collider.name;
            }
            return at;
        }

        /// <summary>How far back from <paramref name="aim"/> a camera turned <paramref name="turn"/> must stand for every corner
        /// to be in view (a pinhole camera of <paramref name="fov"/> degrees up and down, this wide), Margin kept free.</summary>
        static float Fit(Vector3[] corners, Vector3 aim, Quaternion turn, float fov, float aspect, float margin)
        {
            float tanV = Mathf.Tan(0.5f * fov * Mathf.Deg2Rad) * (1f - margin);
            float tanH = Mathf.Tan(0.5f * fov * Mathf.Deg2Rad) * aspect * (1f - margin);
            Vector3 f = turn * Vector3.forward, r = turn * Vector3.right, u = turn * Vector3.up;
            float d = 0f;
            foreach (Vector3 c in corners)
            {
                Vector3 o = c - aim;
                float along = Vector3.Dot(o, f);
                d = Mathf.Max(d, Mathf.Abs(Vector3.Dot(o, r)) / Mathf.Max(1e-4f, tanH) - along, Mathf.Abs(Vector3.Dot(o, u)) / Mathf.Max(1e-4f, tanV) - along);
            }
            return d;
        }

        /// <summary>How far a ball gets from <paramref name="from"/> toward <paramref name="to"/> before something solid (the
        /// machine, the prizes and the player aside), short of it by its own size; the thing's name in <paramref name="what"/>.</summary>
        static float Sweep(Vector3 from, Vector3 to, float ball, Transform holder, Transform prizes, out string what)
        {
            what = null;
            Vector3 way = to - from;
            float far = way.magnitude;
            if (far < 1e-4f) return far;
            way /= far;
            GameObject player = GameObject.Find("Player");
            float free = far;
            foreach (RaycastHit h in Physics.SphereCastAll(from, ball, way, far, Layers.Seen(~0), QueryTriggerInteraction.Ignore))
            {
                if (h.distance <= 0f) continue;
                Transform t = h.transform;
                if (t.IsChildOf(holder) || (prizes != null && t.IsChildOf(prizes)) || (player != null && t.IsChildOf(player.transform))) continue;
                if (h.distance - ball < free) { free = h.distance - ball; what = h.collider.name; }
            }
            return Mathf.Max(free, ball);
        }

        // ------------------------------------------------------------------------------------------------ the prizes

        static void ReportPrizes(Transform prizes, Transform machine, Bounds glass, float unit, ClawGameSettings s)
        {
            var lines = new List<string>();
            int inside = 0, all = 0;
            foreach (DarkMonster m in prizes.GetComponentsInChildren<DarkMonster>(true))
            {
                if (m.transform.parent != prizes) continue;
                all++;
                CharacterController cc = m.GetComponent<CharacterController>();
                Bounds b = cc != null ? ClawGamePrize.CapsuleBox(cc) : new Bounds(m.transform.position, Vector3.zero);
                bool inGlass = true;
                foreach (Vector3 corner in Corners(b))
                {
                    Vector3 p = machine.InverseTransformPoint(corner);
                    if (p.x < glass.min.x || p.x > glass.max.x || p.z < glass.min.z || p.z > glass.max.z || p.y > glass.max.y) { inGlass = false; break; }
                }
                if (inGlass) inside++;
                lines.Add($"{m.name} ({m.Kind}, \"{s.NameOf(m.Kind)}\", {m.Role}, {b.size.y:F1} tall, top at {b.max.y:F1}{(inGlass ? "" : ", NOT wholly inside the glass")})");
            }
            Log.AppendLine($"PRIZES under {Path(prizes)}: {all} found, {inside} wholly inside the glass: {string.Join("; ", lines)}. Each is found at run time (never assumed in a row: the claw comes down on the highest one under its open fingers). Won ones are saved per slot as \"{s.saveKey}<name>\".");
            if (all == 0) Log.AppendLine("PRIZE WARNING: no prize (a DarkMonster child) under ClawPrizes: the machine says Empty.");
        }

        // ------------------------------------------------------------------------------------------------ the star

        /// <summary>The gold star on Resources/ObjectiveSet (added once, his words kept), its ObjectiveHook on ClawMachine, and
        /// ClawGame's On Won wired to it.</summary>
        static void Star(Transform holder, ClawGame game, ClawGameSettings s)
        {
            if (string.IsNullOrEmpty(s.starId)) { Log.AppendLine("STAR: ClawGameSettings > Star Id is empty: no star, nothing wired."); return; }
            var set = AssetDatabase.LoadAssetAtPath<ObjectiveSet>(ObjectivesBuilder.AssetPath);
            if (set == null) Log.AppendLine($"STAR NOT added: no {ObjectivesBuilder.AssetPath} (run Shift Jam > Objectives > Build Objectives first, then this again). The hook below is still wired.");
            else
            {
                var stars = new List<ObjectiveStar>(set.stars ?? new ObjectiveStar[0]);
                ObjectiveStar star = stars.Find(x => x != null && x.id == s.starId);
                bool fresh = star == null;
                if (fresh)
                {
                    star = new ObjectiveStar { id = s.starId, title = s.starTitle, room = s.starRoom, doneWhen = new ObjectiveRule[0] };
                    int at = stars.FindLastIndex(x => x != null && x.room == s.starRoom);
                    stars.Insert(at >= 0 ? at + 1 : stars.Count, star);
                    set.stars = stars.ToArray();
                    EditorUtility.SetDirty(set);
                    AssetDatabase.SaveAssetIfDirty(set);
                }
                Log.AppendLine($"STAR '{star.id}': \"{star.title}\" ({star.room}) {(fresh ? "ADDED to " + ObjectivesBuilder.AssetPath + " (placeholder title: his to rewrite there)" : "FOUND: his title and room kept")}; star {stars.IndexOf(star) + 1} of {set.stars.Length}. It has no rule of its own (none of the set's kinds is a claw win): the claw game earns it through its hook, so Build Objectives lists it as \"NO RULE\" and leaves it be. A star refills the shift charge (Objectives.AwardStar), as the others.");
            }

            ObjectiveHook hook = holder.GetComponent<ObjectiveHook>();
            bool madeHook = hook == null;
            if (madeHook) hook = holder.gameObject.AddComponent<ObjectiveHook>();
            hook.Setup(s.starId, ObjectiveHook.Kind.Star, 1);
            EditorUtility.SetDirty(hook);

            var so = new SerializedObject(game);
            SerializedProperty calls = so.FindProperty("onWon.m_PersistentCalls.m_Calls");
            if (calls == null) { Log.AppendLine("STAR HOOK NOT WIRED: ClawGame has no On Won event."); return; }
            int removed = 0;
            for (int c = calls.arraySize - 1; c >= 0; c--)
            {
                SerializedProperty old = calls.GetArrayElementAtIndex(c);
                Object target = old.FindPropertyRelative("m_Target").objectReferenceValue;
                string typeName = old.FindPropertyRelative("m_TargetAssemblyTypeName").stringValue ?? "";
                if (target is ObjectiveHook || (target == null && typeName.StartsWith(typeof(ObjectiveHook).FullName))) { calls.DeleteArrayElementAtIndex(c); removed++; }
            }
            calls.arraySize++;
            SerializedProperty call = calls.GetArrayElementAtIndex(calls.arraySize - 1);
            call.FindPropertyRelative("m_Target").objectReferenceValue = hook;
            call.FindPropertyRelative("m_TargetAssemblyTypeName").stringValue = typeof(ObjectiveHook).AssemblyQualifiedName;
            call.FindPropertyRelative("m_MethodName").stringValue = nameof(ObjectiveHook.Fire);
            call.FindPropertyRelative("m_Mode").intValue = ModeVoid;
            call.FindPropertyRelative("m_CallState").intValue = RuntimeOnly;
            SerializedProperty args = call.FindPropertyRelative("m_Arguments");
            args.FindPropertyRelative("m_ObjectArgument").objectReferenceValue = null;
            args.FindPropertyRelative("m_ObjectArgumentAssemblyTypeName").stringValue = "";
            args.FindPropertyRelative("m_IntArgument").intValue = 0;
            args.FindPropertyRelative("m_FloatArgument").floatValue = 0f;
            args.FindPropertyRelative("m_StringArgument").stringValue = "";
            args.FindPropertyRelative("m_BoolArgument").boolValue = false;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(game);
            Log.AppendLine($"STAR HOOK: ObjectiveHook on {Path(holder)} ({(madeHook ? "MADE" : "found, reused")}; star '{s.starId}', 1 part), wired to ClawGame's On Won -> ObjectiveHook.Fire (each win calls it; only the first earns the star){(removed > 0 ? $"; {removed} old hook listener(s) replaced" : "")}. It is outside \"{ObjectivesBuilder.HooksName}\", so Build Objectives never deletes it.");
        }

        // ------------------------------------------------------------------------------------------------ helpers

        static bool IsGlass(Material m) => m != null && m.name.IndexOf("Glass", System.StringComparison.OrdinalIgnoreCase) >= 0;

        /// <summary>The box of the meshes on <paramref name="part"/> itself (not its children), in <paramref name="space"/>'s own space.</summary>
        static Bounds MeshBox(Transform part, Transform space)
        {
            MeshFilter mf = part.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) return new Bounds(space.InverseTransformPoint(part.position), Vector3.zero);
            Matrix4x4 m = space.worldToLocalMatrix * part.localToWorldMatrix;
            Bounds local = mf.sharedMesh.bounds;
            bool any = false; var b = new Bounds();
            foreach (Vector3 c in Corners(local))
            {
                Vector3 p = m.MultiplyPoint3x4(c);
                if (!any) { b = new Bounds(p, Vector3.zero); any = true; } else b.Encapsulate(p);
            }
            return b;
        }

        /// <summary><see cref="MeshBox"/> in the machine's space, or null when the part has no mesh of its own.</summary>
        static Bounds? MeshBoxIn(Transform part, Transform machine)
        {
            if (part == null) return null;
            MeshFilter mf = part.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) return null;
            return MeshBox(part, machine);
        }

        static Vector3[] Corners(Bounds b)
        {
            Vector3 n = b.min, x = b.max;
            return new[]
            {
                new Vector3(n.x, n.y, n.z), new Vector3(x.x, n.y, n.z), new Vector3(n.x, x.y, n.z), new Vector3(x.x, x.y, n.z),
                new Vector3(n.x, n.y, x.z), new Vector3(x.x, n.y, x.z), new Vector3(n.x, x.y, x.z), new Vector3(x.x, x.y, x.z),
            };
        }

        /// <summary>The glass box's corners in the world.</summary>
        static Vector3[] Corners(Bounds local, Transform space)
        {
            Vector3[] c = Corners(local);
            for (int i = 0; i < c.Length; i++) c[i] = space.TransformPoint(c[i]);
            return c;
        }

        /// <summary>The child of <paramref name="parent"/> called <paramref name="name"/>, made if there is none; any
        /// second one of that name is removed (no duplicates).</summary>
        static Transform Child(Transform parent, string name)
        {
            Transform keep = null;
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                Transform c = parent.GetChild(i);
                if (c.name != name) continue;
                if (keep == null) keep = c;
                else Object.DestroyImmediate(c.gameObject);   // a second one of that name: no duplicates
            }
            if (keep != null) return keep;
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        static void SetRef(SerializedObject so, string field, Object value, List<string> unset)
        {
            SerializedProperty p = so.FindProperty(field);
            if (p == null) { unset.Add(field); return; }
            p.objectReferenceValue = value;
        }

        static void SetValue(SerializedObject so, string field, Bounds value, List<string> unset)
        {
            SerializedProperty p = so.FindProperty(field);
            if (p == null) unset.Add(field); else p.boundsValue = value;
        }

        static void SetValue(SerializedObject so, string field, Vector4 value, List<string> unset)
        {
            SerializedProperty p = so.FindProperty(field);
            if (p == null) unset.Add(field); else p.vector4Value = value;
        }

        static void SetValue(SerializedObject so, string field, Vector3 value, List<string> unset)
        {
            SerializedProperty p = so.FindProperty(field);
            if (p == null) unset.Add(field); else p.vector3Value = value;
        }

        static void SetValue(SerializedObject so, string field, Vector2 value, List<string> unset)
        {
            SerializedProperty p = so.FindProperty(field);
            if (p == null) unset.Add(field); else p.vector2Value = value;
        }

        static void SetValue(SerializedObject so, string field, float value, List<string> unset)
        {
            SerializedProperty p = so.FindProperty(field);
            if (p == null) unset.Add(field); else p.floatValue = value;
        }

        /// <summary>A part's place for the report: its parent's name, local place, and (for the moving mechanism, which slides
        /// and sinks along its own axes) a warning if it is turned or scaled unlike the pack's mechanism.</summary>
        static string Part(Transform t, Transform expectedParent, bool mechanism = true)
        {
            if (t == null) return "NONE";
            bool parentOk = t.parent == expectedParent;
            bool plain = Quaternion.Angle(t.localRotation, Quaternion.identity) < 0.5f && (t.localScale - Vector3.one).sqrMagnitude < 1e-6f;
            string turn = plain ? "" : mechanism ? $" (turned {t.localEulerAngles.ToString("F1")}, scale {t.localScale.ToString("F2")}: NOT as the pack's mechanism has it, the claw may slide askew)" : $" (turned {t.localEulerAngles.ToString("F1")})";
            return $"{t.name} at {t.localPosition.ToString("F4")}{(parentOk ? "" : $" (its parent is {(t.parent != null ? t.parent.name : "none")}, NOT {expectedParent.name})")}{turn}";
        }

        static string Fingers(Transform[] fingers)
        {
            var parts = new List<string>();
            foreach (Transform f in fingers) parts.Add($"{f.localPosition.ToString("F3")} turned {f.localEulerAngles.y:F0}");
            return "(" + string.Join("; ", parts) + ")";
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

        static string Path(Transform t)
        {
            string p = t.name;
            for (Transform up = t.parent; up != null; up = up.parent) p = up.name + "/" + p;
            return p;
        }
    }
}
#endif
