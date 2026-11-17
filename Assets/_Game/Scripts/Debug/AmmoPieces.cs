#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ShiftJam
{
    /// <summary>
    /// Editor maker for the ammo pickups (D-0070), after the test room's two: an ammo box (the Cartoon Weapons pack's
    /// Ammo_AR_01, five times its size, turning over the floor: one magazine for every gun carried, back after 15 s) and
    /// a supply crate (the pack's Container_05, twice its size, with an AMMO sign: fills every reserve, never used up).
    /// Both go to Prefabs/Pieces/Weapons, referenced from the pack by path, never copied. It also makes
    /// Resources/WeaponAmmo (never overwritten once it exists) and gives it the four licensed sounds when they are in
    /// _Imports. "Place Workshop Ammo" stands a box one step past the last gun in the Workshop's row of pickups.
    /// The Colour Corner's crate is placed by its own builder (CourseBuilder.CcSupply).
    /// </summary>
    public static class AmmoPieces
    {
        private const string Pieces = "Assets/_Game/Prefabs/Pieces/Weapons";
        public const string BoxPath = Pieces + "/AmmoBox.prefab", CratePath = Pieces + "/SupplyCrate.prefab";
        private const string SettingsPath = "Assets/_Game/Resources/WeaponAmmo.asset";
        private const string Pack = "Assets/_Imports/ithappy/Cartoon_Weapons/Prefabs/";
        private const string Sounds = "Assets/_Imports/CasePortman/Weapons/";
        private const string WorkshopScene = "Assets/_Game/Scenes/Workshop.unity";

        // The test room's sizes (ammo_pickup.tscn, supply_crate.tscn). The pack's models are the same size in both engines.
        private const float BoxSize = 5f, BoxHover = 0.55f, BoxReach = 0.9f, BoxReachHeight = 0.6f, BoxSpin = 70f, BoxRespawn = 15f;
        private const float CrateSize = 2f, CrateSignHeight = 1.3f;
        private static readonly Vector3 CrateReach = new Vector3(2.6f, 1.6f, 2.4f);   // the test room's; its height is replaced by ReachUp
        private const float RowStep = 18f;   // the Workshop's pickups stand this far apart along their row
        private const string ShiftPath = "Assets/_Game/Data/ShiftSettings.asset";
        // The Player's CharacterController: its radius, and the skin it rides over the floor on (both grow with the shift).
        private const float PlayerRadius = 0.3f, PlayerSkin = 0.03f;

        /// <summary>How tall a pickup's reach stands. A kid's body rides over the floor on its skin and curves up from
        /// there: under its rim its underside is (radius + skin) x the kid's scale up (11.6 at 35). The reach stands that
        /// tall, so a kid whose body is over a pickup touches it, as the test room's kid does; a toy still has to
        /// walk into it.</summary>
        private static float ReachUp()
        {
            var shift = AssetDatabase.LoadAssetAtPath<ShiftSettings>(ShiftPath);
            float biggest = shift != null ? Mathf.Max(shift.kidScale, shift.toyScale) : 1f;
            return Mathf.Max(BoxReachHeight + BoxReach, (PlayerRadius + PlayerSkin) * biggest + 0.5f);
        }

        [MenuItem("Shift Jam/Workshop/Build Ammo Pieces")]
        public static void Build()
        {
            WorkshopPieces.EnsureFolder(Pieces);
            WorkshopPieces.EnsureFolder("Assets/_Game/Resources");
            var settings = WorkshopPieces.SettingsAsset<WeaponAmmoSettings>(SettingsPath);
            int clips = Wire(settings, "dryClip", "dry_click") + Wire(settings, "reloadStartClip", "reload_start") + Wire(settings, "reloadSeatClip", "reload_seat") + Wire(settings, "pickupClip", "pickup");
            bool box = BuildBox(), crate = BuildCrate();
            AssetDatabase.SaveAssets();
            Debug.Log($"[Ammo] pieces: AmmoBox {(box ? "built" : "NOT built")}, SupplyCrate {(crate ? "built" : "NOT built")}; settings {SettingsPath}, {clips} of 4 sounds set (they live under {Sounds}, git-ignored).");
        }

        /// <summary>Gives the settings a licensed clip when the field is empty and the clip is in the project.</summary>
        private static int Wire(WeaponAmmoSettings settings, string field, string file)
        {
            var so = new SerializedObject(settings);
            var p = so.FindProperty(field);
            if (p.objectReferenceValue != null) return 1;
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(Sounds + file + ".ogg");
            if (clip == null) return 0;
            p.objectReferenceValue = clip;
            so.ApplyModifiedPropertiesWithoutUndo();
            return 1;
        }

        private static bool BuildBox()
        {
            var pack = WorkshopPieces.Load(Pack + "Ammo/Ammo_AR_01.prefab");
            if (pack == null) return false;
            var root = new GameObject("AmmoBox");
            var model = new GameObject("Model").transform;
            model.SetParent(root.transform, false);
            model.localPosition = Vector3.up * BoxHover;
            var inst = WorkshopPieces.Place(pack, model);
            // Turn about its own middle, as big as the test room's.
            Bounds b = WorkshopPieces.LocalBounds(model, inst);
            inst.transform.localPosition = -b.center;
            model.localScale = Vector3.one * BoxSize;
            // The test room's reach is a ball 0.9 across at 0.6 up; here it is a column of the same width (see ReachUp).
            float tall = ReachUp();
            var reach = root.AddComponent<CapsuleCollider>();
            reach.isTrigger = true; reach.direction = 1; reach.radius = BoxReach; reach.height = tall; reach.center = Vector3.up * (tall * 0.5f - (BoxReach - BoxReachHeight));
            var ammo = root.AddComponent<AmmoBox>();
            WorkshopPieces.Set(ammo, "magazines", 1);
            WorkshopPieces.Set(ammo, "respawnSeconds", BoxRespawn);
            WorkshopPieces.Set(ammo, "spin", BoxSpin);
            WorkshopPieces.Set(ammo, "model", model);
            WorkshopPieces.Save(root, BoxPath);
            Debug.Log($"[Ammo] AmmoBox: the pack's Ammo_AR_01 ({b.size.x:F3} x {b.size.y:F3} x {b.size.z:F3}) at {BoxSize} times its size, {BoxHover} over the floor; reach {BoxReach} wide, {tall:F1} tall.");
            return true;
        }

        private static bool BuildCrate()
        {
            var pack = WorkshopPieces.Load(Pack + "Containers/Container_05.prefab");
            if (pack == null) return false;
            var root = new GameObject("SupplyCrate");
            var model = new GameObject("Model").transform;
            model.SetParent(root.transform, false);
            var inst = WorkshopPieces.Place(pack, model);
            Bounds b = WorkshopPieces.LocalBounds(model, inst);
            inst.transform.localPosition = new Vector3(-b.center.x, -b.min.y, -b.center.z);   // standing on the floor, centred
            model.localScale = Vector3.one * CrateSize;
            // Solid, so it is walked up to, not through; the reach is wider than the crate.
            var solid = new GameObject("Solid").AddComponent<BoxCollider>();
            solid.transform.SetParent(root.transform, false);
            solid.size = b.size * CrateSize; solid.center = Vector3.up * (b.size.y * CrateSize * 0.5f);
            var reach = root.AddComponent<BoxCollider>();
            float tall = ReachUp();
            reach.isTrigger = true; reach.center = Vector3.up * (tall * 0.5f);
            reach.size = Vector3.Max(new Vector3(CrateReach.x, tall, CrateReach.z), solid.size + new Vector3(0.8f, 0f, 0.8f));
            var sign = new GameObject("Sign", typeof(RectTransform));
            sign.transform.SetParent(root.transform, false);
            sign.transform.localPosition = Vector3.up * Mathf.Max(CrateSignHeight, solid.size.y + 0.35f);
            var text = sign.AddComponent<TMPro.TextMeshPro>();
            text.text = "AMMO"; text.fontSize = 4f; text.alignment = TMPro.TextAlignmentOptions.Center; text.fontStyle = TMPro.FontStyles.Bold;
            text.rectTransform.sizeDelta = new Vector2(3f, 0.6f);
            var turn = sign.AddComponent<Billboard>();
            WorkshopPieces.Set(turn, "spin", 0f);
            WorkshopPieces.Set(turn, "breathe", 0f);
            var ammo = root.AddComponent<AmmoBox>();
            WorkshopPieces.Set(ammo, "magazines", 0);
            WorkshopPieces.Set(ammo, "respawnSeconds", 0f);
            WorkshopPieces.Set(ammo, "spin", 0f);
            WorkshopPieces.Set(ammo, "model", model);
            Vector3 body = solid.size, outer = reach.size;   // read before the save: it clears the object away
            WorkshopPieces.Save(root, CratePath);
            Debug.Log($"[Ammo] SupplyCrate: the pack's Container_05 at {CrateSize} times its size = {body.x:F2} x {body.y:F2} x {body.z:F2}; reach {outer.x:F1} x {outer.y:F1} x {outer.z:F1}.");
            return true;
        }

        /// <summary>One ammo box in the Workshop, one step past the last gun in the row of pickups (the first clear spot
        /// of: past the far end, past the near end). Run again, it replaces the one it placed (named AmmoBox_Row).</summary>
        [MenuItem("Shift Jam/Workshop/Place Workshop Ammo")]
        public static void PlaceWorkshopAmmo()
        {
            var scene = SceneManager.GetActiveScene();
            if (scene.name != "Workshop") { Debug.LogError("[Ammo] Open Workshop.unity first."); return; }
            var layout = GameObject.Find("Workshop/Layout");
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(BoxPath);
            if (layout == null || prefab == null) { Debug.LogError("[Ammo] No Workshop/Layout, or no AmmoBox piece (run Build Ammo Pieces)."); return; }
            foreach (Transform old in layout.transform.Cast<Transform>().Where(t => t.name == "AmmoBox_Row").ToList()) Object.DestroyImmediate(old.gameObject);

            var guns = layout.GetComponentsInChildren<WeaponPickup>(true).Where(p => p.Weapon is BlasterWeapon).Select(p => p.transform.position).ToList();
            if (guns.Count == 0) { Debug.LogError("[Ammo] The Workshop has no blaster pickup to stand the box by."); return; }
            Vector3 far = guns.OrderByDescending(p => p.z).First(), near = guns.OrderBy(p => p.z).First();
            var solids = new List<Bounds>();
            foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                if (r is ParticleSystemRenderer || !r.enabled || !r.gameObject.activeInHierarchy) continue;
                if (r.bounds.max.y < 0.6f) continue;   // floor tiles, rugs and floor glows
                solids.Add(r.bounds);
            }
            bool Clear(Vector3 p)
            {
                var box = new Bounds(p + Vector3.up * 1.5f, new Vector3(4f, 3f, 4f));
                return !solids.Any(s => s.Intersects(box));
            }
            foreach (Vector3 spot in new[] { far + Vector3.forward * RowStep, near + Vector3.back * RowStep, far + Vector3.right * RowStep })
            {
                if (!Clear(spot)) continue;
                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, layout.transform);
                go.name = "AmmoBox_Row";
                go.transform.position = new Vector3(spot.x, far.y, spot.z);
                EditorSceneManager.MarkSceneDirty(scene);
                Debug.Log($"[Ammo] Workshop: AmmoBox_Row at ({spot.x:0.#}, {far.y:0.#}, {spot.z:0.#}), by {guns.Count} blaster pickups.");
                return;
            }
            Debug.LogError("[Ammo] Workshop: no clear floor by the row of guns for the ammo box.");
        }

        /// <summary>Batch: -executeMethod ShiftJam.AmmoPieces.Land. Builds the pieces and the settings, stands the box
        /// in the Workshop and the crate in the Colour Corner, saves both scenes, and exits (0 = all placed).</summary>
        public static void Land()
        {
            Build();
            bool ok = AssetDatabase.LoadAssetAtPath<GameObject>(BoxPath) != null && AssetDatabase.LoadAssetAtPath<GameObject>(CratePath) != null;
            EditorSceneManager.OpenScene(WorkshopScene);
            PlaceWorkshopAmmo();
            ok &= GameObject.Find("Workshop/Layout/AmmoBox_Row") != null;
            ok &= EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            ok &= CourseBuilder.LandColourCornerAmmo();
            AssetDatabase.SaveAssets();
            Debug.Log(ok ? "[Ammo] LANDED: the box is in the Workshop, the crate in the Colour Corner." : "[Ammo] NOT LANDED: see the errors above.");
            EditorApplication.Exit(ok ? 0 : 1);
        }
    }
}
#endif
