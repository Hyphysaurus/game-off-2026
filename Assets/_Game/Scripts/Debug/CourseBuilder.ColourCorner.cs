#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace ShiftJam
{
    /// <summary>
    /// The Colour Corner: a room of its own (ColourCorner.unity, a sandbox like the Workshop and the Raceway), the
    /// five-minute colouring test ported from the Godot test room's colour_corner.tscn. Four play mats in the middle of
    /// a small walled floor, 27 grey Playroom toys on them where the Godot level has them, three soldiers that rub the
    /// colour off, and the player with a blaster and paint. The scene is made from the Workshop scene the first time
    /// (its player, camera and menus); the room itself is rebuilt on every run. Called by PaintBuilder.Build.
    /// The tables below are in the Godot level's own coordinates. Unity's copy of the pack is the same pieces mirrored
    /// left to right (measured: a piece's x bounds change sign, its z bounds do not), so x and the turns change sign
    /// here (<see cref="CcAt"/>, <see cref="CcTurn"/>) and the corner is the Godot one as the player sees it.
    /// </summary>
    public static partial class CourseBuilder
    {
        const string ColourCornerScenePath = "Assets/_Game/Scenes/ColourCorner.unity";
        const string ColourCornerGradePath = "Assets/_Game/Data/ColourCornerGrade.asset";
        const string CcTemplateScenePath = "Assets/_Game/Scenes/Workshop.unity";   // its player, camera and menus
        const string CcWeapons = "Assets/_Game/Prefabs/Weapons/";

        const int CcTiles = 2;              // floor tiles along each wall (a tile is 65): the mats (51 across) sit in the middle
        const float CcTile = 65f;
        const float CcMatLift = 0.05f;      // the mats sit this far over the floor sheet, as in the Workshop
        const float CcPlayerLift = 0.1f, CcSoldierLift = 0.05f;
        static readonly Vector3 CcPlayerAt = new Vector3(0f, 0f, 21f);   // Godot: (0, 1, 21), looking at the toys (-z)
        const float CcPlayerTurn = 180f;
        static readonly string[] CcStartWeapons = { "DartPistol", "DartSMG" };
        const int CcPaintAmmo = 1;          // the AmmoBelt's rows: 0 darts, 1 paint

        // The room's lights (the Workshop's three, brought down to this room's size): height, range, strength.
        static readonly Vector3 CcRoomLight = new Vector3(70f, 300f, 4f), CcFillLight = new Vector3(55f, 180f, 1.5f);
        const float CcFillOut = 40f;
        static readonly Color CcWarm = new Color(1f, 0.93f, 0.82f), CcFillWarm = new Color(1f, 0.9f, 0.75f);

        // name, pack prefab, x, z (Godot)
        static readonly (string name, string prefab, float x, float z)[] CcMats =
        {
            ("MatYellow", "Mat_01", -12.75f, -12.75f), ("MatGreen", "Mat_02", 12.75f, -12.75f),
            ("MatRed", "Mat_03", -12.75f, 12.75f), ("MatBlue", "Mat_04", 12.75f, 12.75f),
        };

        // name, pack prefab, x, z, turn in degrees (Godot)
        static readonly (string name, string prefab, float x, float z, float turn)[] CcProps =
        {
            ("Castle", "Castle_01", -13f, -13f, 0f), ("CastleTower", "Castle_Tower_01", -21f, 2f, 0f), ("CastleWall", "Castle_Wall_01", -11f, -1f, 0f),
            ("CubeA", "Cube_Alphabet_01", 7f, -17f, 10f), ("CubeB", "Cube_Alphabet_02", 13f, -18f, -15f),
            ("CubeC", "Cube_Alphabet_03", 9f, -11f, 30f), ("CubeD", "Cube_Alphabet_04", 19f, -12f, 0f),
            ("WoodCylinder", "Wooden_Cylinder_01", 20f, -4f, 0f), ("WoodPyramid", "Wooden_Pyramid_01", 13f, -4f, 20f),
            ("WoodCone", "Wooden_Cone_01", 4f, -4f, 0f), ("WoodCube", "Wooden_Cube_01", -3f, -20f, 25f),
            ("WoodSphere", "Wooden_Sphere_01", -2f, -8f, 0f), ("Train", "Wooden_Train_01", 6f, 6f, -60f),
            ("Rocket", "Wooden_Rocket_01", 18f, 15f, 0f),
            ("SkittleA", "Wooden_Skittle_01", -6f, 9f, 0f), ("SkittleB", "Wooden_Skittle_02", -11f, 12f, 0f), ("SkittleC", "Wooden_Skittle_01", -7f, 16f, 0f),
            ("Bear", "Toy_Bear_02", -18f, 16f, 150f),
            ("ChessKing", "Chess_White_King_01", 1f, -13f, 0f), ("ChessHorse", "Chess_Black_Hoss_01", 3f, -9f, 40f),
            ("BlockA", "Color_Cube_01", -1f, 3f, 10f), ("BlockB", "Color_Cube_02", 1f, 4.5f, 35f), ("BlockC", "Color_Cube_03", -2.5f, 5.5f, 0f),
            ("Car", "Car_01", 1f, 13f, -30f),
            ("CanA", "Can_01", -21f, -6f, 0f), ("CanB", "Can_02", -17f, 6f, 0f), ("Cup", "Cup_01", 20f, 5f, -160f),
        };
        const string CcSoftProp = "Bear";   // a plush toy: paint soaks in (PaintSoft)

        // x, z (Godot); all three face the same way
        static readonly Vector2[] CcSoldiers = { new Vector2(-4f, -3f), new Vector2(14f, 2f), new Vector2(-12f, 6f) };
        const float CcSoldierTurn = 180f;

        static Vector3 CcAt(float x, float z) => new Vector3(-x, 0f, z);
        static Vector3 CcTurn(float degrees) => new Vector3(0f, -degrees, 0f);

        /// <summary>Makes (or remakes) the Colour Corner scene, wires it and adds it to the build list. Every check goes
        /// through PaintBuilder.Check.</summary>
        internal static void LandColourCorner(string settingsPath, string paintBallPath, string soldierPath)
        {
            Log.Clear();
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ColourCornerScenePath) == null && !AssetDatabase.CopyAsset(CcTemplateScenePath, ColourCornerScenePath))
            { PaintBuilder.Check(false, "make " + ColourCornerScenePath + " from " + CcTemplateScenePath); return; }
            Scene scene = EditorSceneManager.OpenScene(ColourCornerScenePath);
            // Loaded after the scene is open: opening a scene can unload assets, and a reference held from before goes empty.
            var settings = AssetDatabase.LoadAssetAtPath<PaintSettings>(settingsPath);
            var paintBall = AssetDatabase.LoadAssetAtPath<PaintBall>(paintBallPath);
            var soldierPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(soldierPath);
            PaintBuilder.Check(settings != null && paintBall != null && soldierPrefab != null, "the paint settings, the paint ball and the mischief soldier load from their assets");
            foreach (var name in new[] { "Workshop", "ColourCorner" }) { var old = GameObject.Find(name); if (old != null) Object.DestroyImmediate(old); }

            var root = new GameObject("ColourCorner").transform;
            Transform shell = Child(root, "Shell"), mats = Child(root, "Mats"), props = Child(root, "Props"), soldiers = Child(root, "Soldiers"), level = Child(root, "Level");
            float half = CcTiles * CcTile * 0.5f;
            var lights = CcShell(shell, half);

            // ---- the mats, then the toys on them ----
            var things = new List<Colourable>();
            int unlit = 0;
            float matTop = CcMatLift;
            foreach (var m in CcMats)
            {
                var mat = Rest(Spawn(m.prefab, m.name, mats, CcAt(m.x, m.z), Vector3.zero, 1f), CcMatLift, null);
                matTop = Mathf.Max(matTop, B(mat).max.y);
                things.Add(mat.AddComponent<Colourable>());   // the mats lose their colour too, as in the Godot level
                if (!CcLit(mat)) { unlit++; Log.AppendLine("no Lit renderer on " + m.name); }
            }
            foreach (var p in CcProps)
            {
                var go = Rest(Spawn(p.prefab, p.name, props, CcAt(p.x, p.z), CcTurn(p.turn), 1f), matTop, null);
                foreach (var body in go.GetComponentsInChildren<Rigidbody>()) Object.DestroyImmediate(body);   // they stand still, as in the Godot level
                things.Add(go.AddComponent<Colourable>());
                if (p.name == CcSoftProp) go.AddComponent<PaintSoft>();
                if (!CcLit(go)) { unlit++; Log.AppendLine("no Lit renderer on " + p.name); }
            }
            int wanted = CcMats.Length + CcProps.Length;
            PaintBuilder.Check(things.Count == wanted && !Log.ToString().Contains("MISSING"), $"{things.Count} of {wanted} grey things placed from the Playroom pack" + (Log.Length > 0 ? ": " + Log.ToString().Trim().Replace("\n", "; ") : ""));
            PaintBuilder.Check(unlit == 0, $"every Colourable has a renderer with a URP Lit material to turn grey ({unlit} without)");
            PaintBuilder.Check(things.All(t => t.GetComponentInChildren<Collider>() != null), "every Colourable has a collider for paint to hit");

            // ---- the soldiers ----
            var minds = new List<MischiefSoldier>();
            if (soldierPrefab != null)
                for (int i = 0; i < CcSoldiers.Length; i++)
                {
                    var go = (GameObject)PrefabUtility.InstantiatePrefab(soldierPrefab, soldiers);
                    go.name = "Soldier" + (i + 1);
                    go.transform.SetPositionAndRotation(CcAt(CcSoldiers[i].x, CcSoldiers[i].y) + Vector3.up * (matTop + CcSoldierLift), Quaternion.Euler(CcTurn(CcSoldierTurn)));
                    minds.Add(go.GetComponent<MischiefSoldier>());
                }
            PaintBuilder.Check(minds.Count == CcSoldiers.Length && minds.All(m => m != null), $"{minds.Count} of {CcSoldiers.Length} mischief soldiers placed");

            // ---- the level: paint keeper (no skips here), the grade, the corner, its readout ----
            var paint = Child(level, "PaintSplats").gameObject.AddComponent<PaintSplats>();
            Set(paint, "skips", false);   // paint balls always break here: a skipped shot would colour nothing
            var grade = Child(level, "Grade").gameObject.AddComponent<Volume>();
            grade.isGlobal = true; grade.priority = 10f; grade.sharedProfile = CcGradeProfile(settings);
            var reset = Object.FindFirstObjectByType<WorkshopReset>();
            if (reset == null) reset = new GameObject("WorkshopReset").AddComponent<WorkshopReset>();
            foreach (var light in Object.FindObjectsByType<Light>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                if (!lights.Contains(light) && light.GetComponentInParent<PlayerMotor>() == null && light.GetComponentInParent<Weapon>() == null) lights.Add(light);   // the scene's own sun brightens too
            var corner = level.gameObject.AddComponent<ColourCorner>();
            SetArray(corner, "things", things.Cast<Object>().ToArray());
            SetArray(corner, "soldiers", minds.Cast<Object>().ToArray());
            SetArray(corner, "lights", lights.Cast<Object>().ToArray());
            Set(corner, "grade", grade); Set(corner, "reset", reset); Set(corner, "paint", paint);
            var hud = level.gameObject.AddComponent<ColourCornerHud>();
            Set(hud, "corner", corner);

            // ---- the player: on the near mats, facing the toys, with a blaster and paint ----
            var player = GameObject.Find("Player");
            bool armed = player != null && player.GetComponent<WeaponHolder>() != null;
            PaintBuilder.Check(armed, "the scene has a Player with a WeaponHolder");
            if (armed)
            {
                player.transform.SetPositionAndRotation(CcPlayerAt + Vector3.up * (matTop + CcPlayerLift), Quaternion.Euler(0f, CcPlayerTurn, 0f));
                if (!player.TryGetComponent(out AmmoBelt belt)) belt = player.AddComponent<AmmoBelt>();
                var so = new SerializedObject(belt);
                var kinds = so.FindProperty("kinds"); kinds.arraySize = 2;
                CcKind(kinds.GetArrayElementAtIndex(0), "Darts", null, null);
                CcKind(kinds.GetArrayElementAtIndex(CcPaintAmmo), "Paint", paintBall, settings != null ? settings.fireClips : null);
                so.FindProperty("startWith").intValue = CcPaintAmmo;
                so.ApplyModifiedPropertiesWithoutUndo();
                if (!player.TryGetComponent(out Loadout loadout)) loadout = player.AddComponent<Loadout>();
                var weapons = CcStartWeapons.Select(n => AssetDatabase.LoadAssetAtPath<Weapon>(CcWeapons + n + ".prefab")).Where(w => w != null).ToArray();
                SetArray(loadout, "weapons", weapons.Cast<Object>().ToArray());
                Set(loadout, "ammo", CcPaintAmmo);
                Set(hud, "ammo", belt);
                PaintBuilder.Check(weapons.Length > 0 && weapons[0] is BlasterWeapon && paintBall != null, $"the player starts with {weapons.Length} of {CcStartWeapons.Length} blasters and paint loaded");
            }
            var view = Camera.main;
            if (view != null) view.GetUniversalAdditionalCameraData().renderPostProcessing = true;   // the grade is a post effect

            EditorSceneManager.MarkSceneDirty(scene);
            bool saved = EditorSceneManager.SaveScene(scene);
            var scenes = EditorBuildSettings.scenes.ToList();
            if (!scenes.Any(s => s.path == ColourCornerScenePath)) { scenes.Add(new EditorBuildSettingsScene(ColourCornerScenePath, true)); EditorBuildSettings.scenes = scenes.ToArray(); }
            PaintBuilder.Check(saved && EditorBuildSettings.scenes.Any(s => s.path == ColourCornerScenePath && s.enabled), ColourCornerScenePath + " saved and in Build Settings");
            PaintBuilder.Say($"Colour Corner built: {things.Count} grey things on mats {matTop:F2} high, {minds.Count} soldiers, {lights.Count} lights, a {half * 2f:F0} x {half * 2f:F0} room");
        }

        /// <summary>The corner's shell, the sandbox's on a smaller floor: CcTiles x CcTiles floor tiles, Wall_01 walls,
        /// corner pillars, no ceiling, three warm lights. Returns the lights.</summary>
        static List<Light> CcShell(Transform shell, float h)
        {
            for (int i = 0; i < CcTiles; i++) for (int j = 0; j < CcTiles; j++)
                Spawn("Floor_01", "Floor", shell, new Vector3(-h + CcTile * (i + 0.5f), 0f, -h + CcTile * (j + 0.5f)), Vector3.zero, 1f);
            for (int i = 0; i < CcTiles; i++)
            {
                float a = -h + CcTile * (i + 0.5f);
                var s = Spawn("Wall_01", "Wall_S", shell, new Vector3(a, 0, -h), Vector3.zero, 1f); var bs = B(s); s.transform.position += new Vector3(0, -bs.min.y, -h - bs.max.z);
                var n = Spawn("Wall_01", "Wall_N", shell, new Vector3(a, 0, h), new Vector3(0, 180, 0), 1f); var bn = B(n); n.transform.position += new Vector3(0, -bn.min.y, h - bn.min.z);
                var w = Spawn("Wall_01", "Wall_W", shell, new Vector3(-h, 0, a), new Vector3(0, 90, 0), 1f); var bw = B(w); w.transform.position += new Vector3(-h - bw.max.x, -bw.min.y, 0);
                var e = Spawn("Wall_01", "Wall_E", shell, new Vector3(h, 0, a), new Vector3(0, -90, 0), 1f); var be = B(e); e.transform.position += new Vector3(h - be.min.x, -be.min.y, 0);
            }
            foreach (float x in new[] { -h - 2.5f, h + 2.5f }) foreach (float z in new[] { -h - 2.5f, h + 2.5f })
            { var p = Spawn("Wall_05", "Pillar", shell, new Vector3(x, 0, z), Vector3.zero, 1f); var bp = B(p); p.transform.position += new Vector3(x - bp.center.x, -bp.min.y, z - bp.center.z); }
            return new List<Light>
            {
                PointLight("RoomLight", shell, new Vector3(0f, CcRoomLight.x, 0f), CcRoomLight.y, CcRoomLight.z, CcWarm, true),
                PointLight("FillNW", shell, new Vector3(-CcFillOut, CcFillLight.x, CcFillOut), CcFillLight.y, CcFillLight.z, CcFillWarm),
                PointLight("FillSE", shell, new Vector3(CcFillOut, CcFillLight.x, -CcFillOut), CcFillLight.y, CcFillLight.z, CcFillWarm),
            };
        }

        /// <summary>True if the thing has a mesh drawn with URP Lit: what Colourable can turn grey.</summary>
        static bool CcLit(GameObject go) =>
            go.GetComponentsInChildren<MeshRenderer>().Any(r => r.sharedMaterials.Any(m => m != null && m.shader != null && m.shader.name == "Universal Render Pipeline/Lit"));

        static void CcKind(SerializedProperty kind, string name, Projectile shot, AudioClip[] clips)
        {
            kind.FindPropertyRelative("name").stringValue = name;
            kind.FindPropertyRelative("shot").objectReferenceValue = shot;
            var list = kind.FindPropertyRelative("fireClips");
            list.arraySize = clips != null ? clips.Length : 0;
            for (int i = 0; i < list.arraySize; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = clips[i];
        }

        /// <summary>The corner's grade: a volume profile with Color Adjustments whose saturation the corner drives. Made
        /// once, at the grey end.</summary>
        static VolumeProfile CcGradeProfile(PaintSettings settings)
        {
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ColourCornerGradePath);
            if (profile == null) { profile = ScriptableObject.CreateInstance<VolumeProfile>(); AssetDatabase.CreateAsset(profile, ColourCornerGradePath); }
            if (!profile.TryGet(out ColorAdjustments adjust))
            {
                adjust = profile.Add<ColorAdjustments>(false);
                adjust.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
                AssetDatabase.AddObjectToAsset(adjust, profile);
                adjust.saturation.Override(((settings != null ? settings.saturation.x : 1f) - 1f) * 100f);
                EditorUtility.SetDirty(adjust);
                EditorUtility.SetDirty(profile);
            }
            return profile;
        }
    }
}
#endif
