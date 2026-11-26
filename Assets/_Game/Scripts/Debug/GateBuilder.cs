#if UNITY_EDITOR
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Art = Artifice;

namespace ShiftJam
{
    /// <summary>
    /// "Make Gate Breakable (D-0240)": the backyard's Gate Panel (made by Land Backyard, D-0239, to fill the garden wall's
    /// arch) breaks into Fraggo pieces under the kid's punches, kicks and darts, and builds itself back with Artificer.
    /// "The toy slips through, the kid smashes through."
    /// It finds the panel by its name under Room/Course/R6_Window/Backyard (D-0239), cuts its mesh once with Fraggo the
    /// props' way (BreakableBuilder: the cut kept in the git-ignored _Imports/Generated/Breakables, as it is made from a
    /// licensed mesh, and listed in Resources/Breakables) into about Break Settings > Gate Pieces pieces, and gives the
    /// panel a Damageable (Gate Health), a Breakable (back after Gate Come Back After, its own number) and a Build Back.
    /// Its per-slat boxes stay its colliders (Breakable turns them off when broken and on when back). Batching Static is
    /// cleared on it (a batched mesh can be neither found nor copied at run time). Then it reports: the mesh, the cut,
    /// the colliders and the slat gaps against the toy, the opening the broken panel leaves against the kid's capsule (a
    /// physics check of the scene with the panel's own boxes left out), the build back's pieces, and how the panel joins
    /// the online world. No panel: it says so and changes nothing. A second run changes nothing new (a changed Gate Pieces
    /// cuts again). It marks the scene changed and never saves it. Not in Play.
    /// Menu: Shift Jam > House > Make Gate Breakable (D-0240). Batch or a check: ShiftJam.GateBuilder.Land() returns the report.
    /// </summary>
    public static class GateBuilder
    {
        private const string RoomName = "Room";
        private const string BackyardPath = "Course/R6_Window/Backyard (D-0239)";   // under the scene's Room
        private const string PanelName = "Gate Panel";
        private const string PlayerPrefab = "Assets/_Game/Prefabs/Player/Player.prefab";
        private const string ShiftAsset = "Assets/_Game/Data/ShiftSettings.asset";
        private const string DartPistol = "Assets/_Game/Prefabs/Weapons/DartPistol.prefab";
        private const string UndoName = "Make Gate Breakable (D-0240)";

        /// <summary>The panel's own directions and sizes in the world: across the opening, up, through it.</summary>
        private struct Frame
        {
            public Vector3 Centre, Across, Up, Through;
            public float Width, Height, Depth;
        }

        /// <summary>The player's capsule at kid size and at toy size, in world units. Its skin width keeps it that much further
        /// from everything, so a way through must be wider by twice that.</summary>
        private struct Body
        {
            public float Radius, Skin, Tall, Step, ToyAcross, ToyTall;
            public string From;
        }

        [MenuItem("Shift Jam/House/Make Gate Breakable (D-0240)")]
        private static void Menu() => Debug.Log(Land());

        /// <summary>Makes the backyard's Gate Panel breakable and able to build itself back, and returns the report.</summary>
        public static string Land()
        {
            var log = new StringBuilder("D-0240 GATE PANEL (breaks into Fraggo pieces, builds itself back with Artificer)\n");
            if (EditorApplication.isPlayingOrWillChangePlaymode) return log.Append("In Play: stop Play and run it again. Nothing changed.").ToString();

            // ---- the panel
            Transform backyard = FindBackyard();
            if (backyard == null)
                return log.Append("NOT FOUND: no ").Append(RoomName).Append('/').Append(BackyardPath).Append(" in the open scenes. Open Greybox and run ")
                          .Append("Shift Jam > House > Land Backyard (D-0239) first. Nothing changed.").ToString();
            var panels = new List<Transform>();
            foreach (Transform t in backyard.GetComponentsInChildren<Transform>(true)) if (t.name == PanelName) panels.Add(t);
            if (panels.Count == 0)
                return log.Append("NOT FOUND: no object named \"").Append(PanelName).Append("\" under ").Append(PathOf(backyard))
                          .Append(" (Land Backyard (D-0239) makes it, filling the garden wall's arch). Nothing changed.").ToString();
            if (panels.Count > 1)
            {
                log.Append("MORE THAN ONE \"").Append(PanelName).Append("\" (").Append(panels.Count).Append("): ");
                foreach (Transform t in panels) log.Append(PathOf(t)).Append("; ");
                return log.Append("leave one and run it again. Nothing changed.").ToString();
            }
            Transform panel = panels[0];
            GameObject go = panel.gameObject;

            // ---- what it draws: one still mesh (a cut is of one mesh; Breakable flies the pieces of one)
            if (panel.GetComponentInChildren<SkinnedMeshRenderer>(true) != null)
                return log.Append("SKINNED: ").Append(PathOf(panel)).Append(" draws a skinned mesh; Breakable and Build Back take still meshes only. Nothing changed.").ToString();
            var shapes = new List<MeshFilter>();
            foreach (MeshFilter f in panel.GetComponentsInChildren<MeshFilter>(true)) if (f.sharedMesh != null) shapes.Add(f);
            if (shapes.Count != 1)
                return log.Append(shapes.Count == 0 ? "NO MESH: " + PathOf(panel) + " draws nothing to cut."
                                                    : shapes.Count + " MESHES under " + PathOf(panel) + ": a cut is of one mesh (the others would vanish without pieces); make it one mesh.")
                          .Append(" Nothing changed.").ToString();
            MeshFilter shape = shapes[0];
            Mesh mesh = shape.sharedMesh;
            if (!shape.TryGetComponent(out MeshRenderer look))
                return log.Append("NO RENDERER beside its mesh on ").Append(PathOf(shape.transform)).Append(". Nothing changed.").ToString();
            Frame frame = FrameOf(shape);
            log.Append("FOUND ").Append(PathOf(panel)).Append(", layer ").Append(LayerMask.LayerToName(go.layer))
               .Append(go.activeInHierarchy ? "" : "  <- OFF in the scene: an inactive panel is never hit nor numbered online; switch it on").Append('\n');
            log.Append("MESH ").Append(mesh.name).Append(" on ").Append(shape.name).Append(": ").Append(Triangles(mesh)).Append(" triangles, ")
               .Append(mesh.subMeshCount).Append(mesh.subMeshCount == 1 ? " submesh, " : " submeshes, ")
               .Append(mesh.isReadable ? "readable (in a built game Build Back copies its own parts)"
                                       : "NOT readable (in a built game Build Back flies its Fraggo pieces instead; tick Read/Write on its model for its own parts)")
               .Append("; ").Append(F(frame.Width)).Append(" across x ").Append(F(frame.Height)).Append(" tall x ").Append(F(frame.Depth)).Append(" thick in the world; ")
               .Append(AssetPathOf(mesh)).Append('\n');

            // ---- the cut (assets), before anything in the scene changes: no cut, no change
            BreakSettings s = BreakableBuilder.Settings();
            int asked = Mathf.Max(2, s.gatePieces);
            CarDamageFxSettings.Cut cut = s.CutOf(mesh.name);
            if (cut != null && s.gateCutAskedFor == asked)
                log.Append("CUT kept: ").Append(cut.pieces.Length).Append(" pieces (asked ").Append(asked).Append(") in ").Append(AssetDatabase.GetAssetPath(cut.pieces[0])).Append('\n');
            else
            {
                CarWreckBuilder.Fraggo fraggo = CarWreckBuilder.Fraggo.Find(out string why);
                if (fraggo == null) return log.Append("CUT: ").Append(why).Append(". Nothing changed.").ToString();
                Random.State kept = Random.state;   // the cut seeds Unity's random by the mesh's name
                CarDamageFxSettings.Cut made;
                float area;
                try { made = CarWreckBuilder.CutBody(fraggo, mesh, asked, out area); }
                finally { Random.state = kept; }
                if (made == null) return log.Append("CUT: Fraggo made no pieces of ").Append(mesh.name).Append(". Nothing changed.").ToString();
                string path = BreakableBuilder.SavePieces(made);
                var all = new List<CarDamageFxSettings.Cut>();
                if (s.cuts != null) foreach (CarDamageFxSettings.Cut c in s.cuts) if (c != null && c.mesh != made.mesh) all.Add(c);
                all.Add(made);
                s.cuts = all.ToArray();
                s.gateCutAskedFor = asked;
                EditorUtility.SetDirty(s);
                cut = made;
                log.Append("CUT made now (Fraggo, asked ").Append(asked).Append("): ").Append(made.pieces.Length).Append(" pieces, their outer faces ")
                   .Append(area.ToString("P1", CultureInfo.InvariantCulture)).Append(" of the mesh's -> ").Append(path)
                   .Append(" (git-ignored: made from a licensed mesh); listed in ").Append(AssetDatabase.GetAssetPath(s)).Append(" (").Append(s.cuts.Length).Append(" cuts in all)\n");
            }
            AssetDatabase.SaveAssets();

            // ---- the panel's pieces (scene): only what differs is written
            Undo.SetCurrentGroupName(UndoName);
            bool changed = false;
            int unbatched = 0;
            foreach (Transform t in panel.GetComponentsInChildren<Transform>(true))
            {
                StaticEditorFlags flags = GameObjectUtility.GetStaticEditorFlags(t.gameObject);
                if ((flags & StaticEditorFlags.BatchingStatic) == 0) continue;
                Undo.RecordObject(t.gameObject, UndoName);
                GameObjectUtility.SetStaticEditorFlags(t.gameObject, flags & ~StaticEditorFlags.BatchingStatic);
                unbatched++;
            }
            if (unbatched > 0)
            {
                changed = true;
                log.Append("STATIC: Batching Static cleared on ").Append(unbatched).Append(" of its objects (static batching swaps the mesh for one combined mesh at run time: nothing to cut or copy)\n");
            }

            var health = go.GetComponent<Damageable>();
            bool healthNew = health == null;
            if (healthNew) health = Undo.AddComponent<Damageable>(go);
            var hso = new SerializedObject(health);
            bool healthSet = SetFloat(hso, "maxHealth", s.gateHealth) | SetBool(hso, "takeKnockback", true) | SetBool(hso, "showNumbers", false);
            hso.ApplyModifiedProperties();
            changed |= healthNew || healthSet;

            var breakable = go.GetComponent<Breakable>();
            bool breakableNew = breakable == null;
            if (breakableNew) breakable = Undo.AddComponent<Breakable>(go);
            var bso = new SerializedObject(breakable);
            bool breakableSet = SetFloat(bso, "comeBackAfter", s.gateComeBackAfter);
            bso.ApplyModifiedProperties();
            changed |= breakableNew || breakableSet;

            bool buildNew = go.GetComponent<BuildBack>() == null;
            if (buildNew) Undo.AddComponent<BuildBack>(go);
            changed |= buildNew;

            // ---- the report
            PlayerMoveSettings moves = PlayerMoveSettings.Load();
            log.Append("HEALTH: Damageable ").Append(healthNew ? "added" : healthSet ? "set" : "kept").Append(", ").Append(F(s.gateHealth)).Append(" (Break Settings > Gate Health)");
            if (moves != null)
                log.Append(": the kid's punch takes ").Append(F(moves.punch.damage)).Append(" (").Append(Hits(s.gateHealth, moves.punch.damage)).Append(" punches), his kick ")
                   .Append(F(moves.kick.damage)).Append(" (").Append(Hits(s.gateHealth, moves.kick.damage)).Append(" kicks)");
            float dart = DartDamage();
            if (dart > 0f) log.Append("; a dart from the dart pistol ").Append(F(dart)).Append(" (").Append(Hits(s.gateHealth, dart)).Append(" darts)");
            log.Append('\n');

            float reach = Mathf.Max(0.5f, look.bounds.extents.magnitude);
            log.Append("BREAKABLE ").Append(breakableNew ? "added" : breakableSet ? "set" : "kept").Append(": it comes back after ")
               .Append(s.gateComeBackAfter > 0f ? F(s.gateComeBackAfter) + " s" : "never").Append(" (its own number; every other prop ").Append(F(s.comeBackAfter))
               .Append(" s), and waits while a player stands within ").Append(F(s.clearOfPlayer * reach)).Append(" of its foot (Clear Of Player ")
               .Append(F(s.clearOfPlayer)).Append(" x its half-diagonal ").Append(F(reach)).Append(")\n");

            GameObject player = GameObject.Find("Player");
            Transform you = player != null ? player.transform : null;   // the player in the scene: never in the way of itself
            Vector3 feet = Floor(frame, panel, you, out string floorName);
            Colliders(log, panel, moves);
            bool haveBody = BodyOf(out Body body);
            if (haveBody) Slats(log, panel, frame, feet, body);
            Opening(log, panel, you, frame, feet, floorName, body, haveBody);
            Build(log, s, mesh, cut, buildNew);
            Online(log, go);

            if (changed)
            {
                EditorSceneManager.MarkSceneDirty(go.scene);
                log.Append("Scene ").Append(go.scene.name).Append(" marked changed (not saved): save it to keep this.");
            }
            else log.Append("Nothing new in the scene: it is as it was.");
            return log.ToString();
        }

        // ---------------------------------------------------------------- finding

        /// <summary>Room/Course/R6_Window/Backyard (D-0239) in any open scene (found even when switched off), or null.</summary>
        private static Transform FindBackyard()
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded) continue;
                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    if (root.name != RoomName) continue;
                    Transform found = root.transform.Find(BackyardPath);
                    if (found != null) return found;
                }
            }
            return null;
        }

        /// <summary>The panel's directions from its mesh's box: its thinnest side is through it, the side nearest the world's
        /// up is up, the other is across.</summary>
        private static Frame FrameOf(MeshFilter shape)
        {
            Transform t = shape.transform;
            Bounds b = shape.sharedMesh.bounds;
            Vector3 scale = t.lossyScale;
            Vector3[] axes = { t.right, t.up, t.forward };
            float[] sizes = { Mathf.Abs(b.size.x * scale.x), Mathf.Abs(b.size.y * scale.y), Mathf.Abs(b.size.z * scale.z) };
            int thin = sizes[0] <= sizes[1] && sizes[0] <= sizes[2] ? 0 : sizes[1] <= sizes[2] ? 1 : 2;
            int a = (thin + 1) % 3, c = (thin + 2) % 3;
            int up = Mathf.Abs(axes[a].y) >= Mathf.Abs(axes[c].y) ? a : c;
            int across = up == a ? c : a;
            return new Frame
            {
                Centre = t.TransformPoint(b.center),
                Across = axes[across], Up = axes[up].y >= 0f ? axes[up] : -axes[up], Through = axes[thin],
                Width = sizes[across], Height = sizes[up], Depth = sizes[thin],
            };
        }

        /// <summary>The player's capsule at kid and toy size: the scene's Player (else the Player prefab) and its ShiftSettings.</summary>
        private static bool BodyOf(out Body body)
        {
            body = default;
            CharacterController capsule = null;
            ShiftSettings shift = null;
            string from = null;
            GameObject player = GameObject.Find("Player");
            if (player != null && player.TryGetComponent(out CharacterController c)) { capsule = c; from = "the scene's Player"; shift = ShiftOf(player); }
            if (capsule == null)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefab);
                if (prefab != null && prefab.TryGetComponent(out CharacterController pc)) { capsule = pc; from = PlayerPrefab; shift = ShiftOf(prefab); }
            }
            if (shift == null) shift = AssetDatabase.LoadAssetAtPath<ShiftSettings>(ShiftAsset);
            if (capsule == null || shift == null) return false;
            float tall = Mathf.Max(capsule.height, 2f * capsule.radius);
            body = new Body
            {
                Radius = capsule.radius * shift.kidScale, Skin = capsule.skinWidth * shift.kidScale, Tall = tall * shift.kidScale,
                Step = capsule.stepOffset * shift.kidScale,
                ToyAcross = 2f * (capsule.radius + capsule.skinWidth) * shift.toyScale, ToyTall = tall * shift.toyScale,
                From = from + " x kid scale " + F(shift.kidScale),
            };
            return true;
        }

        private static ShiftSettings ShiftOf(GameObject player)
        {
            if (!player.TryGetComponent(out Shifter shifter)) return null;
            SerializedProperty p = new SerializedObject(shifter).FindProperty("settings");
            return p != null ? p.objectReferenceValue as ShiftSettings : null;
        }

        /// <summary>The dart pistol's damage a dart (its prefab), or 0 when it is not there.</summary>
        private static float DartDamage()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(DartPistol);
            BlasterWeapon gun = prefab != null ? prefab.GetComponentInChildren<BlasterWeapon>(true) : null;
            SerializedProperty p = gun != null ? new SerializedObject(gun).FindProperty("damage") : null;
            return p != null ? p.floatValue : 0f;
        }

        // ---------------------------------------------------------------- the checks (physics, the panel's own boxes left out)

        /// <summary>Where the kid would stand in the opening: the first thing under the panel's middle that is not the
        /// panel's, else the panel's own foot.</summary>
        private static Vector3 Floor(Frame f, Transform panel, Transform player, out string what)
        {
            Physics.SyncTransforms();   // the scene's colliders where the makers just put them
            Vector3 middle = f.Centre;
            if (Nearest(middle, Vector3.down, f.Height * 2f + 1f, panel, player, out RaycastHit hit))
            {
                what = hit.collider.name;
                return new Vector3(middle.x, hit.point.y, middle.z);
            }
            what = "the panel's own foot (nothing found under it)";
            return middle - Vector3.up * (f.Height * 0.5f);
        }

        /// <summary>The panel's colliders: what they are and whether the kid's strikes and the darts hit them.</summary>
        private static void Colliders(StringBuilder log, Transform panel, PlayerMoveSettings moves)
        {
            int all = 0, boxes = 0, triggers = 0, off = 0, unhit = 0;
            foreach (Collider c in panel.GetComponentsInChildren<Collider>(true))
            {
                all++;
                if (c is BoxCollider) boxes++;
                if (c.isTrigger) triggers++;
                if (!c.enabled || !c.gameObject.activeInHierarchy) off++;
                int layer = c.gameObject.layer;
                bool struck = moves == null || (moves.strikeMask.value & (1 << layer)) != 0;
                bool shot = layer != Layers.CarGuard;
                if (!struck || !shot) unhit++;
            }
            log.Append("COLLIDERS: ").Append(all).Append(" (").Append(boxes).Append(" boxes, ").Append(triggers).Append(" triggers");
            if (off > 0) log.Append(", ").Append(off).Append(" off (they stay off: a return turns back on only what the break turned off)");
            log.Append("); Breakable turns them off when broken and on when back");
            if (all == 0) log.Append("  <- NONE: nothing to hit and nothing to break through");
            if (unhit > 0) log.Append("  <- ").Append(unhit).Append(" on a layer the kid's strikes or the darts leave out");
            log.Append('\n');
        }

        /// <summary>The gaps between its boxes at the toy's height, against the toy's capsule, and how much of the panel the
        /// boxes cover (a dart through a gap flies on).</summary>
        private static void Slats(StringBuilder log, Transform panel, Frame f, Vector3 feet, Body body)
        {
            Vector3 across = Vector3.ProjectOnPlane(f.Across, Vector3.up);
            if (across.sqrMagnitude < 1e-8f) return;
            across.Normalize();
            var spans = new List<Vector2>();
            foreach (Collider c in panel.GetComponentsInChildren<Collider>(false))
            {
                if (!c.enabled || c.isTrigger) continue;
                Vector2 height = Span(c, Vector3.up);
                if (height.x > feet.y + body.ToyTall || height.y < feet.y) continue;   // over the toy's head (a rail) or under the floor
                spans.Add(Span(c, across));
            }
            if (spans.Count == 0) { log.Append("SLATS: no box of it stands at the toy's height\n"); return; }
            spans.Sort((x, y) => x.x.CompareTo(y.x));
            var merged = new List<Vector2> { spans[0] };
            for (int i = 1; i < spans.Count; i++)
            {
                Vector2 last = merged[merged.Count - 1];
                if (spans[i].x <= last.y) merged[merged.Count - 1] = new Vector2(last.x, Mathf.Max(last.y, spans[i].y));
                else merged.Add(spans[i]);
            }
            float covered = 0f, least = float.MaxValue, most = 0f;
            foreach (Vector2 m in merged) covered += m.y - m.x;
            for (int i = 1; i < merged.Count; i++)
            {
                float gap = merged[i].x - merged[i - 1].y;
                least = Mathf.Min(least, gap); most = Mathf.Max(most, gap);
            }
            float width = merged[merged.Count - 1].y - merged[0].x;
            log.Append("SLATS at the toy's height: ").Append(spans.Count).Append(" boxes");
            if (merged.Count < 2) log.Append(", no gap between them (the toy cannot slip through)");
            else
                log.Append(", gaps ").Append(F(least)).Append(" to ").Append(F(most)).Append(" (the toy is ").Append(F(body.ToyAcross)).Append(" across with its skin width: ")
                   .Append(least > body.ToyAcross ? "it slips between any two" : "NOT between the narrowest").Append(')');
            log.Append("; the boxes cover ").Append(width > 0f ? Mathf.RoundToInt(100f * covered / width) : 0).Append("% of its width (a dart through a gap flies on)\n");
        }

        /// <summary>The opening the broken panel leaves: its width at the kid's middle, its height, and whether the kid's
        /// capsule stands in it and passes through it, the panel's own boxes left out.</summary>
        private static void Opening(StringBuilder log, Transform panel, Transform player, Frame f, Vector3 feet, string floor, Body body, bool haveBody)
        {
            Vector3 across = Vector3.ProjectOnPlane(f.Across, Vector3.up), through = Vector3.ProjectOnPlane(f.Through, Vector3.up);
            if (!haveBody) { log.Append("OPENING: the kid's size is unknown (no Player with a CharacterController, no ShiftSettings): not checked\n"); return; }
            if (across.sqrMagnitude < 1e-8f || through.sqrMagnitude < 1e-8f) { log.Append("OPENING: the panel is not upright: not checked\n"); return; }
            across.Normalize(); through.Normalize();
            float r = body.Radius + body.Skin, reach = 2f * (f.Width + f.Height) + 4f * body.Tall;   // r: his capsule with its skin; reach: how far the rays look
            Vector3 middle = feet + Vector3.up * (body.Tall * 0.5f);
            bool left = Nearest(middle, -across, reach, panel, player, out RaycastHit l), right = Nearest(middle, across, reach, panel, player, out RaycastHit rh);
            float lift = Mathf.Max(0.05f * r, body.Step);   // the kid steps over less than this: a bump in the lawn is no wall
            bool roof = Nearest(feet + Vector3.up * lift, Vector3.up, reach, panel, player, out RaycastHit top);

            log.Append("OPENING when broken (the panel's own boxes left out; the floor: ").Append(floor).Append("): ");
            if (left && right) log.Append(F(l.distance + rh.distance)).Append(" wide at the kid's middle (").Append(l.collider.name).Append(" to ").Append(rh.collider.name).Append(')');
            else log.Append(left || right ? "open on one side at the kid's middle" : "open on both sides at the kid's middle");
            log.Append(", ").Append(roof ? F(top.distance + lift) + " tall (to " + top.collider.name + ")" : "open above");
            log.Append("; the kid is ").Append(F(2f * body.Radius)).Append(" across (").Append(F(2f * r)).Append(" with his skin width) and ").Append(F(body.Tall))
               .Append(" tall (").Append(body.From).Append("): ");

            Vector3 low = feet + Vector3.up * (lift + r), high = feet + Vector3.up * Mathf.Max(lift + r, lift + body.Tall - r);
            var blockers = new List<string>();
            foreach (Collider c in Physics.OverlapCapsule(low, high, r, ~0, QueryTriggerInteraction.Ignore))
                if (Other(c, panel, player) && !blockers.Contains(c.name)) blockers.Add(c.name);
            float run = f.Depth * 0.5f + 2f * r + 1f;
            foreach (RaycastHit h in Physics.CapsuleCastAll(low - through * run, high - through * run, r, through, 2f * run, ~0, QueryTriggerInteraction.Ignore))
                if (Other(h.collider, panel, player) && !blockers.Contains(h.collider.name)) blockers.Add(h.collider.name);
            if (blockers.Count == 0) log.Append("he FITS: his capsule stands in the opening and passes through it from side to side\n");
            else log.Append("he does NOT pass: in the way ").Append(string.Join(", ", blockers)).Append('\n');
        }

        /// <summary>The build back: its pieces (Artificer's cut, as it is made at run time) and its look.</summary>
        private static void Build(StringBuilder log, BreakSettings s, Mesh mesh, CarDamageFxSettings.Cut cut, bool added)
        {
            log.Append("BUILD BACK ").Append(added ? "added" : "kept").Append(" (Break Settings > Build Back On: ").Append(s.buildBackOn ? "on" : "OFF, so it dissolves back in").Append("); Artificer's pieces of ");
            var shapes = new List<Mesh>();
            if (mesh.isReadable) { shapes.Add(mesh); log.Append("its mesh:"); }
            else
            {
                foreach (Mesh piece in cut.pieces) if (piece != null) shapes.Add(piece);
                log.Append("its ").Append(shapes.Count).Append(" Fraggo pieces (its model is not readable in a built game):");
            }
            float size = Mathf.Max(mesh.bounds.size.x, Mathf.Max(mesh.bounds.size.y, mesh.bounds.size.z));
            foreach (BuildFxSettings.Pieces way in new[] { BuildFxSettings.Pieces.Parts, BuildFxSettings.Pieces.TexturePatches, BuildFxSettings.Pieces.Facets })
            {
                int n = 0;
                foreach (Mesh m in shapes) n += Count(m, way, size);
                log.Append(' ').Append(way).Append(' ').Append(n).Append(way == s.buildBackPieces ? " (in use)" : "").Append(';');
            }
            log.Append(" they fly in ").Append(s.buildBackOrder).Append(" over ").Append(F(s.buildBackSeconds)).Append(" s, each in ")
               .Append(F(Mathf.Min(s.buildBackPieceSeconds.x, s.buildBackPieceSeconds.y))).Append(" to ").Append(F(Mathf.Max(s.buildBackPieceSeconds.x, s.buildBackPieceSeconds.y)))
               .Append(" s, from ").Append(F(s.buildBackDistance.x)).Append(" to ").Append(F(s.buildBackDistance.y)).Append(" of its size out; then it stands, solid\n");
        }

        /// <summary>How the panel joins the online world (D-0131): as every breakable does.</summary>
        private static void Online(StringBuilder log, GameObject go)
        {
            int shared = 0;
            foreach (GameObject root in go.scene.GetRootGameObjects()) shared += root.GetComponentsInChildren<Breakable>(false).Length;
            if (!go.activeInHierarchy) { log.Append("ONLINE: NOT numbered: CoopWorld numbers the breakables that are on when a party world loads, and the panel is off\n"); return; }
            log.Append("ONLINE: one of ").Append(shared).Append(" breakables in ").Append(go.scene.name)
               .Append(" that CoopWorld numbers by their place when a party world loads (the same on every PC, nothing added to the scene): the host runs it, ")
               .Append("its break and its return reach every PC, a hit on another PC's copy goes to the host, and each PC's copy builds itself back with its own Build Back\n");
        }

        // ---------------------------------------------------------------- helpers

        /// <summary>The nearest hit along a ray that is not the panel's (nor a trigger, nor the player's).</summary>
        private static bool Nearest(Vector3 from, Vector3 way, float reach, Transform panel, Transform player, out RaycastHit hit)
        {
            hit = default;
            bool found = false;
            foreach (RaycastHit h in Physics.RaycastAll(from, way, reach, ~0, QueryTriggerInteraction.Ignore))
                if (Other(h.collider, panel, player) && (!found || h.distance < hit.distance)) { hit = h; found = true; }
            return found;
        }

        /// <summary>A collider of something else: not the panel's, not the player's.</summary>
        private static bool Other(Collider c, Transform panel, Transform player)
            => c != null && !c.transform.IsChildOf(panel) && (player == null || !c.transform.IsChildOf(player));

        /// <summary>How far a collider reaches along <paramref name="axis"/> (least, most): a box's own corners, else its box in the world.</summary>
        private static Vector2 Span(Collider c, Vector3 axis)
        {
            float least = float.MaxValue, most = float.MinValue;
            for (int i = 0; i < 8; i++)
            {
                var sign = new Vector3((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f);
                Vector3 corner = c is BoxCollider box
                    ? box.transform.TransformPoint(box.center + Vector3.Scale(box.size * 0.5f, sign))
                    : c.bounds.center + Vector3.Scale(c.bounds.extents, sign);
                float d = Vector3.Dot(corner, axis);
                least = Mathf.Min(least, d); most = Mathf.Max(most, d);
            }
            return new Vector2(least, most);
        }

        /// <summary>How many pieces Artificer's cut makes of a shape, cut the way Build Back cuts at run time: one throwaway
        /// object carrying the shape, its renderer, Artificer's id mark and an Artificer, cut by Artificer's own splitter,
        /// gone after. Nothing is saved (Artificer's BuildData would write an asset at edit time; the splitter alone does not).</summary>
        private static int Count(Mesh shape, BuildFxSettings.Pieces way, float size)
        {
            var go = new GameObject("~GateCount") { hideFlags = HideFlags.HideAndDontSave };
            try
            {
                go.AddComponent<MeshFilter>().sharedMesh = shape;
                var look = go.AddComponent<MeshRenderer>();
                look.sharedMaterials = new Material[Mathf.Max(1, shape.subMeshCount)];
                var mark = go.AddComponent<Art.ObjectID>();
                mark.id = 0; mark.rend = look;
                var a = go.AddComponent<Art.Artificer>();
                a.target = go;
                RebuildOnReset.CutBy(a, way, size);
                a.random = new System.Random(0);
                List<Art.MeshElement> pieces = Art.GameObjectMeshSplitter.SplitGameObject(a, go, Vector3.zero, null, false);
                return pieces != null ? pieces.Count : 0;
            }
            finally { Object.DestroyImmediate(go); }
        }

        private static bool SetFloat(SerializedObject so, string field, float value)
        {
            SerializedProperty p = so.FindProperty(field);
            if (p == null || p.floatValue == value) return false;
            p.floatValue = value;
            return true;
        }

        private static bool SetBool(SerializedObject so, string field, bool value)
        {
            SerializedProperty p = so.FindProperty(field);
            if (p == null || p.boolValue == value) return false;
            p.boolValue = value;
            return true;
        }

        private static int Triangles(Mesh mesh)
        {
            long n = 0;
            for (int sub = 0; sub < mesh.subMeshCount; sub++) n += mesh.GetIndexCount(sub) / 3;
            return (int)n;
        }

        private static int Hits(float health, float damage) => Mathf.CeilToInt(health / Mathf.Max(0.01f, damage));

        private static string AssetPathOf(Object o)
        {
            string path = AssetDatabase.GetAssetPath(o);
            return string.IsNullOrEmpty(path) ? "kept in the scene (not an asset)" : path;
        }

        private static string PathOf(Transform t)
        {
            var sb = new StringBuilder(t.name);
            for (Transform p = t.parent; p != null; p = p.parent) sb.Insert(0, p.name + "/");
            return sb.ToString();
        }

        private static string F(float v) => v.ToString("0.0", CultureInfo.InvariantCulture);
    }
}
#endif
