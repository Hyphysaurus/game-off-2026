#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace ShiftJam
{
    /// <summary>
    /// The Sticker Chart's maker (D-0224). Menu: Shift Jam > Objectives > Land Sticker Chart (D-0224).
    /// (1) Resources/ObjectiveSet: any default zone whose id is missing is added, and every main step whose Room is empty
    /// gets the default for its id; nothing he has written is changed (the chart's new words are written with their
    /// defaults the first time; made with every default if there is no asset yet). (2) In the OPEN scene (Greybox): a
    /// top-level "StickerChart (D-0224)" holding the no-card zone boxes (ZoneBox triggers on the Ignore Raycast layer) for
    /// the bedroom (its walls', floor's and ceiling's box, measured) and the window (the sill's box, measured, with
    /// headroom over it). The hallway, the playroom, the wardrobe and downstairs use their arrival card's trigger (Land
    /// Helpers, D-0221). A second run deletes the holder and makes it again (no duplicates). Marks the scene changed;
    /// never saves it (Ctrl+S keeps it) and never opens a scene. (3) Reports every zone (its box or its scene, its steps
    /// and its stars), every star's zone and every step's zone. The report's first line says whether this was the first
    /// run or a second; one that starts "[Chart] NOT" changed nothing.
    /// </summary>
    public static class ChartBuilder
    {
        public const string Tag = "[Chart]";
        public const string RootName = "StickerChart (D-0224)";
        private const string CoursePath = "Room/Course", BedroomPath = "Room/Bedroom", SillPath = "Room/Course/R6_Window/Sill";
        private const string BedroomDarkPath = "Room/Bedroom/DarkBedroom/BedroomDark";
        private const string BedroomId = "bedroom", WindowId = "window";
        private const int IgnoreRaycast = 2;

        [MenuItem("Shift Jam/Objectives/Land Sticker Chart (D-0224)")]
        public static void LandMenu()
        {
            string report = Land();
            if (report.StartsWith(Tag + " NOT")) Debug.LogWarning(report); else Debug.Log(report);
        }

        /// <summary>Refreshes the asset, places the zone boxes in the open scene, and returns the report (one fact a line).</summary>
        public static string Land()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return Tag + " NOT landed: this does not run in Play. Stop the game first. Nothing was changed.";
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded)
                return Tag + " NOT landed: no scene is open. Open Greybox and run this again. Nothing was changed.";
            if (Objectives.Resolve(scene, CoursePath) == null)
                return Tag + $" NOT landed: '{scene.name}' has no {CoursePath} (the house). Open Greybox and run this again. Nothing was changed.";

            var log = new StringBuilder();
            GameObject old = FindRoot(scene, RootName);
            bool again = old != null;
            log.AppendLine($"{Tag} Land Sticker Chart (D-0224) in '{scene.name}': {(again ? "SECOND run: '" + RootName + "' deleted and made again (no duplicates)" : "first run")}.");

            ObjectiveSet set = Asset(log);

            if (old != null) Object.DestroyImmediate(old);
            var root = new GameObject(RootName);
            SceneManager.MoveGameObjectToScene(root, scene);
            Physics.SyncTransforms();

            Bedroom(scene, set, root.transform, log);
            Window(scene, set, root.transform, log);
            Report(scene, set, log);

            EditorSceneManager.MarkSceneDirty(scene);
            log.AppendLine($"'{scene.name}' MARKED CHANGED, not saved: Ctrl+S keeps it.");
            log.AppendLine("OFF: ObjectiveSet > Chart On (no chart, no slab, no hint line); Card Always On (the old always-shown card); Pad Peek (D-pad up, off by default).");
            return log.ToString();
        }

        // ---------- 1. the asset ----------

        private static ObjectiveSet Asset(StringBuilder log)
        {
            ObjectiveSet set = AssetDatabase.LoadAssetAtPath<ObjectiveSet>(ObjectivesBuilder.AssetPath);
            if (set == null)
            {
                set = ScriptableObject.CreateInstance<ObjectiveSet>();
                FillStepRooms(set, new StringBuilder());
                AssetDatabase.CreateAsset(set, ObjectivesBuilder.AssetPath);
                AssetDatabase.SaveAssets();
                log.AppendLine($"ASSET: made {ObjectivesBuilder.AssetPath} with every default: run Build Objectives (D-0220) and Land Helpers (D-0221) for their parts. Every word in it is a PLACEHOLDER.");
                return set;
            }
            var added = new StringBuilder();
            var zones = new List<ChartZone>(set.zones ?? new ChartZone[0]);
            foreach (ChartZone d in ChartWords.DefaultZones())
            {
                if (zones.Exists(z => z != null && z.id == d.id)) continue;
                zones.Add(d);
                added.Append(added.Length > 0 ? ", " : "").Append("zone " + d.id);
            }
            set.zones = zones.ToArray();
            var rooms = new StringBuilder();
            FillStepRooms(set, rooms);
            // Written either way: an asset saved before D-0224 holds the new fields' defaults only in memory until saved.
            EditorUtility.SetDirty(set);
            AssetDatabase.SaveAssets();
            log.AppendLine($"ASSET: {ObjectivesBuilder.AssetPath} kept as he left it ({set.zones.Length} zones){(added.Length > 0 ? "; added from the defaults: " + added : "; no zone missing")}"
                           + $"{(rooms.Length > 0 ? "; step rooms filled where empty: " + rooms : "; every step already has a room")}; written.");
            if (!set.on) log.AppendLine("NOTE: ObjectiveSet's On is UNTICKED: no card, no chart, no zone is tracked in Play.");
            if (!set.chartOn) log.AppendLine("NOTE: ObjectiveSet's Chart On is UNTICKED: no chart, no pause menu slab, no hint line.");
            log.AppendLine($"CARD: {(set.cardAlwaysOn ? "Card Always On is TICKED: the old always-shown card" : "hidden until there is news, " + set.showSeconds + " s each time")}; hint \"{set.hintKeys}\" / pad \"{(set.padPeek ? set.hintPad : set.hintPadPause)}\"; Pad Peek {(set.padPeek ? "TICKED (D-pad up held also works the light after 0.35 s)" : "unticked (the pad's way is the pause menu's slab)")}.");
            return set;
        }

        /// <summary>Every step with an empty Room gets the default for its id (an unknown id stays empty: no row).</summary>
        private static void FillStepRooms(ObjectiveSet set, StringBuilder filled)
        {
            foreach (ObjectiveStep s in set.steps ?? new ObjectiveStep[0])
            {
                if (s == null || !string.IsNullOrWhiteSpace(s.room)) continue;
                string room = ChartWords.DefaultStepRoom(s.id);
                if (room.Length == 0) continue;
                s.room = room;
                filled.Append(filled.Length > 0 ? ", " : "").Append(s.id + " = " + room);
            }
        }

        // ---------- 2. the no-card zone boxes ----------

        /// <summary>The bedroom: its Wall_, Pillar_, Floor_ and Ceiling_ modules' box (as the dark bedroom's maker measures
        /// it), Bedroom Zone Grow bigger all round; else the dark bedroom's own box (BedroomDark); else none.</summary>
        private static void Bedroom(Scene scene, ObjectiveSet set, Transform root, StringBuilder log)
        {
            Transform bedroom = Objectives.Resolve(scene, BedroomPath);
            if (bedroom == null) { log.AppendLine($"ZONE BOX '{BedroomId}': NOT PLACED: no {BedroomPath}. The house starts in the bedroom zone anyway (From Start) until another box holds the player."); return; }
            bool any = false;
            Bounds box = default;
            int parts = 0;
            foreach (Transform t in bedroom)
            {
                if (!(t.name.StartsWith("Wall_") || t.name.StartsWith("Pillar_") || t.name.StartsWith("Floor_") || t.name.StartsWith("Ceiling_"))) continue;
                if (!RendererBox(t.gameObject, out Bounds part)) continue;
                if (!any) { box = part; any = true; } else box.Encapsulate(part);
                parts++;
            }
            string from;
            if (any)
            {
                box.Expand(set.bedroomZoneGrow * 2f);
                from = $"{parts} Wall_ / Pillar_ / Floor_ / Ceiling_ modules under {BedroomPath}, {set.bedroomZoneGrow} more all round";
            }
            else
            {
                Transform dark = Objectives.Resolve(scene, BedroomDarkPath);
                DarkZone zone = dark != null ? dark.GetComponent<DarkZone>() : null;
                if (zone == null) { log.AppendLine($"ZONE BOX '{BedroomId}': NOT PLACED: no wall modules under {BedroomPath} and no {BedroomDarkPath}. The house starts in the bedroom zone anyway (From Start)."); return; }
                var so = new SerializedObject(zone);
                SerializedProperty centre = so.FindProperty("centre");
                Vector3 middle = zone.transform.TransformPoint(centre != null ? centre.vector3Value : Vector3.zero);
                box = new Bounds(middle, Vector3.Scale(zone.Size, zone.transform.lossyScale));
                from = $"no wall modules found: the dark bedroom's box ({BedroomDarkPath})";
            }
            Place(root, BedroomId, box, from, log);
        }

        /// <summary>The window: the sill's box, Window Zone Margin more across and in depth, from the sill's bottom to Window
        /// Zone Headroom over its top (a player standing on it).</summary>
        private static void Window(Scene scene, ObjectiveSet set, Transform root, StringBuilder log)
        {
            Transform sill = Objectives.Resolve(scene, SillPath);
            if (sill == null || !(RendererBox(sill.gameObject, out Bounds box) || ColliderBox(sill.gameObject, out box)))
            {
                log.AppendLine($"ZONE BOX '{WindowId}': NOT PLACED: no {SillPath} (or it has no renderer or collider). The window zone is then reached only by name (its star still counts on the chart).");
                return;
            }
            float margin = set.windowZoneMargin;
            Vector3 min = box.min - new Vector3(margin, 0f, margin), max = box.max + new Vector3(margin, set.windowZoneHeadroom, margin);
            var zoneBox = new Bounds();
            zoneBox.SetMinMax(min, max);
            Place(root, WindowId, zoneBox, $"{SillPath} (sill box {box.min} to {box.max}), {margin} more across and in depth, {set.windowZoneHeadroom} over its top", log);
        }

        private static void Place(Transform root, string id, Bounds box, string from, StringBuilder log)
        {
            var go = new GameObject("Zone " + id) { layer = IgnoreRaycast };
            go.transform.SetParent(root, false);
            go.transform.position = box.center;
            var collider = go.AddComponent<BoxCollider>();
            collider.isTrigger = true;
            collider.size = box.size;
            go.AddComponent<ZoneBox>().Setup(id);
            log.AppendLine($"ZONE BOX '{id}': '{RootName}/{go.name}' at {box.center} size {box.size} (x {box.min.x:F1}..{box.max.x:F1}, y {box.min.y:F1}..{box.max.y:F1}, z {box.min.z:F1}..{box.max.z:F1}); from {from}.");
        }

        // ---------- 3. the report ----------

        private static void Report(Scene scene, ObjectiveSet set, StringBuilder log)
        {
            // Every zone box in the scene by its id: the arrival cards' triggers and the zone boxes (inactive ones too).
            var boxes = new Dictionary<string, List<Bounds>>();
            foreach (GameObject top in scene.GetRootGameObjects())
            {
                foreach (ArrivalCard card in top.GetComponentsInChildren<ArrivalCard>(true)) AddBox(boxes, card.Id, card.GetComponent<BoxCollider>());
                foreach (ZoneBox zone in top.GetComponentsInChildren<ZoneBox>(true)) AddBox(boxes, zone.Id, zone.GetComponent<BoxCollider>());
            }
            ChartZone[] zones = set.zones ?? new ChartZone[0];
            var order = new List<int>();
            for (int i = 0; i < zones.Length; i++) if (zones[i] != null) order.Add(i);
            order.Sort((a, b) => zones[a].order != zones[b].order ? zones[a].order.CompareTo(zones[b].order) : a.CompareTo(b));
            foreach (int i in order)
            {
                ChartZone z = zones[i];
                string where;
                if (z.wholeScene) where = $"the whole scene '{z.scene}'";
                else if (boxes.TryGetValue(z.id, out List<Bounds> found))
                {
                    var sb = new StringBuilder();
                    foreach (Bounds b in found) sb.Append(sb.Length > 0 ? " AND " : "").Append($"box x {b.min.x:F1}..{b.max.x:F1}, y {b.min.y:F1}..{b.max.y:F1}, z {b.min.z:F1}..{b.max.z:F1}");
                    where = $"in '{z.scene}': {sb}{(found.Count > 1 ? " (" + found.Count + " boxes with this id: the smallest holding the player wins)" : "")}";
                }
                else where = $"in '{z.scene}': NO BOX with the id '{z.id}' in this scene{(z.id == BedroomId || z.id == WindowId ? "" : " (run Land Helpers (D-0221): its arrival card's trigger is its box)")}";
                var steps = new StringBuilder();
                foreach (ObjectiveStep s in set.steps ?? new ObjectiveStep[0])
                    if (s != null && ChartWords.SameRoom(ChartWords.StepRoom(s), z.room)) steps.Append(steps.Length > 0 ? ", " : "").Append(s.id);
                var stars = new StringBuilder();
                foreach (ObjectiveStar s in set.stars ?? new ObjectiveStar[0])
                    if (s != null && ChartWords.SameRoom(s.room, z.room)) stars.Append(stars.Length > 0 ? ", " : "").Append($"{s.id} \"{s.title}\"");
                log.AppendLine($"ZONE {z.order} '{z.id}' \"{z.title}\" (room word '{z.room}'){(z.fromStart ? ", visited from the start" : "")}: {where}; steps: {(steps.Length > 0 ? steps.ToString() : "none")}; stars: {(stars.Length > 0 ? stars.ToString() : "NONE")}.");
            }
            var starZones = new StringBuilder();
            int lost = 0;
            foreach (ObjectiveStar s in set.stars ?? new ObjectiveStar[0])
            {
                if (s == null) continue;
                ChartZone z = ZoneOf(zones, s.room);
                if (z == null) lost++;
                starZones.Append(starZones.Length > 0 ? ", " : "").Append(s.id + " -> " + (z != null ? z.id : "NO ZONE (room '" + s.room + "')"));
            }
            log.AppendLine($"STARS: {starZones}.{(lost > 0 ? " " + lost + " star(s) with NO ZONE are not on the chart's rows (only in its total)." : "")}");
            var stepZones = new StringBuilder();
            foreach (ObjectiveStep s in set.steps ?? new ObjectiveStep[0])
            {
                if (s == null) continue;
                ChartZone z = ZoneOf(zones, ChartWords.StepRoom(s));
                stepZones.Append(stepZones.Length > 0 ? ", " : "").Append(s.id + " -> " + (z != null ? z.id : "NO ZONE (room '" + ChartWords.StepRoom(s) + "')"));
            }
            log.AppendLine($"STEPS: {stepZones}.");
        }

        private static void AddBox(Dictionary<string, List<Bounds>> boxes, string id, BoxCollider box)
        {
            if (string.IsNullOrEmpty(id) || box == null) return;
            Transform t = box.transform;
            Vector3 size = Vector3.Scale(box.size, t.lossyScale);
            var b = new Bounds(t.TransformPoint(box.center), new Vector3(Mathf.Abs(size.x), Mathf.Abs(size.y), Mathf.Abs(size.z)));
            if (!boxes.TryGetValue(id, out List<Bounds> list)) boxes[id] = list = new List<Bounds>();
            list.Add(b);
        }

        private static ChartZone ZoneOf(ChartZone[] zones, string room)
        {
            foreach (ChartZone z in zones) if (z != null && ChartWords.SameRoom(z.room, room)) return z;
            return null;
        }

        // ---------- helpers ----------

        /// <summary>The box round every renderer under <paramref name="go"/> (particles left out). False when it has none.</summary>
        private static bool RendererBox(GameObject go, out Bounds box)
        {
            box = default;
            bool any = false;
            foreach (Renderer r in go.GetComponentsInChildren<Renderer>(true))
            {
                if (r is ParticleSystemRenderer) continue;
                if (!any) { box = r.bounds; any = true; } else box.Encapsulate(r.bounds);
            }
            return any && box.size != Vector3.zero;
        }

        /// <summary>The box round every collider under <paramref name="go"/> that is not a trigger. False when it has none.</summary>
        private static bool ColliderBox(GameObject go, out Bounds box)
        {
            box = default;
            bool any = false;
            foreach (Collider c in go.GetComponentsInChildren<Collider>(true))
            {
                if (c.isTrigger) continue;
                if (!any) { box = c.bounds; any = true; } else box.Encapsulate(c.bounds);
            }
            return any && box.size != Vector3.zero;
        }

        private static GameObject FindRoot(Scene scene, string name)
        {
            foreach (GameObject root in scene.GetRootGameObjects()) if (root.name == name) return root;
            return null;
        }
    }
}
#endif
