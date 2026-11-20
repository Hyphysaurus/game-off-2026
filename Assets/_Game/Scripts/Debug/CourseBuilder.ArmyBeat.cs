#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ShiftJam
{
    /// <summary>
    /// The playroom's one weapons beat (D-0081). A dart pistol on its stand by the toy's arrival (the mouse hole by the
    /// Hoop), and four toy soldiers standing guard in the Toy Army's sandbag maze between the toy and core A: one at each
    /// turn of the walk round the rings and one by the core. They are the Workshop's ToySoldier piece as it is (sight
    /// cone, fire, tip over when knocked out, up again at their post). The sandbags (2 high) are cover: a soldier's eye
    /// is at its chest, so it cannot see over them, and neither can its shots. An ammo box at the maze's way in. In the
    /// course a soldier's dart shoves the toy and never hurts it (the course player has no Damageable), so the maze can
    /// always be won. At kid
    /// size the soldiers are ankle-high and the kid steps over the sandbags. Everything lives under R4_Playroom/ArmyBeat;
    /// running it again replaces it. Built with the course (Build), or alone: -executeMethod ShiftJam.CourseBuilder.LandArmyBeat.
    /// </summary>
    public static partial class CourseBuilder
    {
        /// <summary>Batch: -executeMethod ShiftJam.CourseBuilder.LandWalkFixes (-nographics is fine). The walk check's fixes
        /// (D-0081) without a full course rebuild: the skirting run laid again (even book risers, a landing after the hop,
        /// the 5.5 sprint gap) and wired to the bedroom and the playroom, the wardrobe laid again (even book risers, the
        /// trampoline pillow nearer shelf 2) and wired to the playroom, the maze's sandbags laid again two high. Greybox saved. The soldiers beat lands on its own (LandArmyBeat).</summary>
        public static void LandWalkFixes()
        {
            Log.Clear();
            EditorSceneManager.OpenScene(ScenePath);
            var course = GameObject.Find("Room/Course");
            var r3 = course != null ? course.transform.Find("R3_SkirtingRun") : null;
            var r4 = course != null ? course.transform.Find("R4_Playroom") : null;
            var entry = r4 != null ? r4.Find("Entry") : null;
            if (r3 == null || r4 == null || entry == null) { Debug.LogError("D-0081: no R3 / R4 / R4 Entry in " + ScenePath); EditorApplication.Exit(1); return; }
            Material dark = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Game/Data/Greybox_Dark.mat");
            Material glowMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Game/Data/DoorGlow.mat");
            foreach (Transform t in r3.Cast<Transform>().ToArray()) Object.DestroyImmediate(t.gameObject);
            Transform r3Start = BuildLane(r3, dark, glowMat);
            WireLane(r3, r3Start, r4, entry);
            // the wardrobe (R5): its book flight had uneven risers too, and its trampoline pillow sat too far from shelf 2
            var r5 = course.transform.Find("R5_Wardrobe"); var front = r4.Find("WardrobeFront");
            if (r5 != null && front != null)
            {
                foreach (Transform t in r5.Cast<Transform>().ToArray()) Object.DestroyImmediate(t.gameObject);
                Material wood = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Game/Data/Wardrobe_Wood.mat");
                Material coreMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Game/Data/ShiftCore.mat");
                Transform r5Start = BuildWardrobe(r5, wood, glowMat, coreMat);
                WireWardrobe(r4, r5, r5Start, front);
            }
            else Log.AppendLine("R5 NOT laid again: no R5_Wardrobe or no WardrobeFront");
            int bags = 0; foreach (Transform t in r4.Cast<Transform>().Where(t => t.name == "Sandbag").ToArray()) { Object.DestroyImmediate(t.gameObject); bags++; }
            SandbagRing(r4, MazeCentre, 36f, 90f);
            SandbagRing(r4, MazeCentre, 18f, 270f);
            Log.AppendLine($"maze: {bags} sandbags replaced by {r4.Cast<Transform>().Count(t => t.name == "Sandbag")} (two high)");
            PuzzleCues();
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
            Debug.Log("D-0081 WALK FIXES LANDED\n" + Log);
        }

        const string SoldierPiece = "Assets/_Game/Prefabs/Pieces/Interactables/ToySoldier.prefab";
        const string PistolPiece = "Assets/_Game/Prefabs/Pieces/Weapons/DartPistolPickup.prefab";
        const string AmmoPiece = "Assets/_Game/Prefabs/Pieces/Weapons/AmmoBox.prefab";

        /// <summary>The maze's middle (core A), as BuildPlayroom lays it.</summary>
        static Vector3 MazeCentre => C4 + new Vector3(125f, 0f, 112f);

        /// <summary>
        /// The soldiers, one row each: where it stands from the maze's middle, and the way it faces. The walk in is the
        /// outer ring's east gap, round the north side between the rings (the corridor is 18 wide, its middle 27 out),
        /// down the west side to the inner ring's west gap, then the core.
        /// </summary>
        static readonly (string name, Vector3 post, Vector3 faces)[] Guards =
        {
            ("Soldier_NE", new Vector3(27f, 0f, 24f), Vector3.back),      // at the first turn, looking down the east side at the gate
            ("Soldier_NW", new Vector3(-24f, 0f, 27f), Vector3.right),    // at the far end of the north side, looking along it
            ("Soldier_W", new Vector3(-27f, 0f, -16f), Vector3.forward),  // on the west side past the inner gap, looking up it
            ("Soldier_Core", new Vector3(9f, 0f, -9f), Vector3.left),     // inside the inner ring, beside the core, looking at its gap
        };

        static void ArmyBeat(Transform r4)
        {
            var old = r4.Find("ArmyBeat"); if (old != null) Object.DestroyImmediate(old.gameObject);
            var beat = Child(r4, "ArmyBeat");
            Vector3 mz = MazeCentre;
            var soldier = AssetDatabase.LoadAssetAtPath<GameObject>(SoldierPiece);
            var pistol = AssetDatabase.LoadAssetAtPath<GameObject>(PistolPiece);
            var ammo = AssetDatabase.LoadAssetAtPath<GameObject>(AmmoPiece);
            if (soldier == null || pistol == null || ammo == null)
            {
                Log.AppendLine("ARMY BEAT SKIPPED: missing " + (soldier == null ? SoldierPiece + " " : "") + (pistol == null ? PistolPiece + " " : "") + (ammo == null ? AmmoPiece : "") + " (run Shift Jam > Workshop > Build Combat & Vehicle Pieces / Build Ammo Pieces)");
                return;
            }
            Physics.SyncTransforms();

            foreach (var g in Guards)
            {
                var s = (GameObject)PrefabUtility.InstantiatePrefab(soldier, beat); s.name = g.name;
                Vector3 at = mz + g.post;
                s.transform.SetPositionAndRotation(new Vector3(at.x, FloorUnder(at, s.transform) + 0.02f, at.z), Quaternion.LookRotation(g.faces));
                Log.AppendLine($"army beat: {g.name} at {s.transform.position} facing {s.transform.eulerAngles.y:F0}");
            }

            // the pistol on its stand: just in from the mouse hole, on the Hoop's side, square in the toy's first view
            Vector3 hole = new Vector3(C4.x - R4HalfX + 8f, 0f, LaneZ);
            var stand = (GameObject)PrefabUtility.InstantiatePrefab(pistol, beat); stand.name = "PistolStand";
            PlaceFree(stand, hole + new Vector3(12f, 0f, -6f), Quaternion.LookRotation(Vector3.left));
            Log.AppendLine($"army beat: pistol stand at {stand.transform.position}, {(stand.transform.position - hole).magnitude:F1} from the hole");

            // an ammo box by the maze's way in (the east gap), for a toy that has sprayed its darts
            Vector3 gate = mz + new Vector3(44f, 0f, -4.5f);
            var box = (GameObject)PrefabUtility.InstantiatePrefab(ammo, beat); box.name = "MazeAmmo";
            PlaceFree(box, gate + new Vector3(2f, 0f, -9f), Quaternion.identity);
            Log.AppendLine($"army beat: ammo box at {box.transform.position}");

            // the course player takes weapons: the Workshop's WeaponHolder as it is (its defaults find everything). No
            // Damageable: in the course a soldier's dart only shoves the toy (Projectile's knockback), it never hurts.
            var player = GameObject.Find("Player");
            if (player != null && !player.TryGetComponent(out WeaponHolder _)) { player.AddComponent<WeaponHolder>(); Log.AppendLine("army beat: the Player gets a WeaponHolder"); }
            EditorSceneManager.MarkSceneDirty(r4.gameObject.scene);
        }

        /// <summary>The top of the solid thing under a point (not <paramref name="self"/>), or 0.</summary>
        static float FloorUnder(Vector3 at, Transform self)
        {
            foreach (var h in Physics.RaycastAll(new Vector3(at.x, at.y + 20f, at.z), Vector3.down, 40f, ~0, QueryTriggerInteraction.Ignore).OrderBy(h => h.distance))
                if (self == null || !h.collider.transform.IsChildOf(self)) return h.point.y;
            return 0f;
        }

        /// <summary>Stands a piece on the floor at <paramref name="want"/>, nudged outward in a small spiral until nothing solid
        /// stands inside its drawn box (thin things it rests on do not count).</summary>
        static void PlaceFree(GameObject go, Vector3 want, Quaternion facing)
        {
            Vector3 at = want;
            for (int k = 0; k < 24; k++)
            {
                if (k > 0) { float a = k * 2.4f, rad = 10f * k / 23f; at = want + new Vector3(Mathf.Cos(a) * rad, 0f, Mathf.Sin(a) * rad); }
                go.transform.SetPositionAndRotation(new Vector3(at.x, 0f, at.z), facing);
                if (RideFits(go, out float ground)) { go.transform.position = new Vector3(at.x, ground + 0.02f, at.z); return; }
            }
            go.transform.SetPositionAndRotation(new Vector3(want.x, FloorUnder(want, go.transform) + 0.02f, want.z), facing);
            Log.AppendLine("army beat: NO FREE SPOT for " + go.name + ", left at " + want);
        }

        /// <summary>Batch: -executeMethod ShiftJam.CourseBuilder.LandArmyBeat (-nographics is fine). D-0081 without a full
        /// course rebuild: the soldiers, the pistol stand, the ammo box and the player's WeaponHolder in Greybox, saved.</summary>
        public static void LandArmyBeat()
        {
            Log.Clear();
            EditorSceneManager.OpenScene(ScenePath);
            var r4 = GameObject.Find("Room/Course/R4_Playroom");
            if (r4 == null) { Debug.LogError("D-0081: no Room/Course/R4_Playroom in " + ScenePath); EditorApplication.Exit(1); return; }
            ArmyBeat(r4.transform);
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
            Debug.Log("D-0081 LANDED\n" + Log);
        }
    }
}
#endif
