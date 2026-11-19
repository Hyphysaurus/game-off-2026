#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// Editor maker for the foam dart (D-0079, its shape D-0080): Prefabs/Combat/FoamDart.prefab, the shot every
    /// blaster and the toy soldier fire. An existing prefab is changed in place, so its ids stay and every gun that
    /// names it still does; a plain Projectile left by an older build becomes a FoamDart with its numbers kept. What
    /// it makes: the dart's shape (child "Shape": the test room's own foam dart, a blue foam body and a round orange
    /// head, from Unity's plain cylinder and ball: no pack model, so it is there in any copy of the project), the
    /// older plain capsule (child "Plain", switched off; shown only if the shape is taken away), the streak, and the
    /// dart's numbers: the test room's flight, bits and sticking, and our own bits and ring for a landing on
    /// something with health. The two colours are materials of their own (Data/FoamDartBody, Data/FoamDartHead):
    /// made once with the test room's colours, then kept, so a colour changed on the material stays.
    /// "Build Foam Dart" also gives Resources/WeaponAmmo its landing sounds when those are in _Imports and it has
    /// none yet (a number already in that asset is never overwritten). A number changed by hand on the prefab is
    /// written over by a rebuild: change it here too.
    /// </summary>
    public static class DartPieces
    {
        public const string PrefabPath = "Assets/_Game/Prefabs/Combat/FoamDart.prefab";
        private const string SettingsPath = "Assets/_Game/Resources/WeaponAmmo.asset";
        private const string ScriptPath = "Assets/_Game/Scripts/Combat/FoamDart.cs";
        private const string StreakLook = "Assets/_Game/Data/PaintStreak.mat";
        private const string Sounds = "Assets/_Imports/CasePortman/Weapons/";

        // The shape: the test room's foam dart (foam_dart.tscn), every length x 0.99. One shape for every gun for now;
        // another gun's dart is other numbers here, or a prefab change.
        private const string BodyLook = "Assets/_Game/Data/FoamDartBody.mat", HeadLook = "Assets/_Game/Data/FoamDartHead.mat";
        private const string LookSource = "Assets/_Game/Data/ShiftCore.mat";   // our own lit material: the two looks are recoloured copies of it
        private const float BodyLength = 0.2376f;    // the foam body: a cylinder 0.24 m long, its middle at the dart's middle
        private const float BodyRadius = 0.0376f;    // and 0.038 m in radius
        private const float HeadAhead = 0.1287f;     // the head's middle, 0.13 m ahead of the dart's middle
        // The head: a ball 0.054 m in radius across and along the dart, pressed to 0.04 m up and down (the test room's
        // SphereMesh: radius 0.054, height 0.08, not turned). Its front is 0.184 m ahead of the middle: 0.034 past the
        // point the dart sticks at, so a stuck dart's head is sunk in up to about its widest ring.
        private static readonly Vector3 HeadRadii = new Vector3(0.0535f, 0.0396f, 0.0535f);
        // Its colours as the materials are first made (the test room's: blue foam, an orange head, both glowing a
        // little so a dart in flight shows). After that the materials hold them.
        private static readonly Color BodyColour = new Color(0.15f, 0.55f, 1f), BodyGlow = new Color(0.12f, 0.4f, 0.8f);     // emission (0.15, 0.5, 1) x 0.8
        private static readonly Color HeadColour = new Color(1f, 0.45f, 0.1f), HeadGlow = new Color(1.2f, 0.48f, 0.06f);    // emission (1, 0.4, 0.05) x 1.2

        [MenuItem("Shift Jam/Workshop/Build Foam Dart")]
        public static void Build()
        {
            WorkshopPieces.EnsureFolder("Assets/_Game/Prefabs/Combat");
            WorkshopPieces.EnsureFolder("Assets/_Game/Resources");
            Projectile dart = BuildDart(WorkshopPieces.DartMaterial());
            // The landing sounds: at the ear for a hit and for a knock-down, and the two thuds of a target going down.
            var ammo = WorkshopPieces.SettingsAsset<WeaponAmmoSettings>(SettingsPath);
            if (ammo.landedClips == null || ammo.landedClips.Length == 0) WorkshopPieces.WireClips(ammo, "landedClips", "ear_hit_");
            if (ammo.downedClips == null || ammo.downedClips.Length == 0) WorkshopPieces.WireClips(ammo, "downedClips", "ear_down_");
            if (ammo.figureDownClip == null) WorkshopPieces.Set(ammo, "figureDownClip", AssetDatabase.LoadAssetAtPath<AudioClip>(Sounds + "figure_down.ogg"));
            if (ammo.targetDownClip == null) WorkshopPieces.Set(ammo, "targetDownClip", AssetDatabase.LoadAssetAtPath<AudioClip>(Sounds + "target_down.ogg"));
            EditorUtility.SetDirty(ammo);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Dart] {(dart != null ? "built " + PrefabPath + " as " + dart.GetType().Name : "NOT built")}; settings {SettingsPath}: {ammo.landedClips.Length} sounds for a hit at the ear, {ammo.downedClips.Length} for a knock-down, the figure's thud {(ammo.figureDownClip != null ? "set" : "missing")}, the target's thud {(ammo.targetDownClip != null ? "set" : "missing")} (sounds live under {Sounds}, git-ignored).");
        }

        /// <summary>Makes or re-makes the dart's prefab and hands it back. <paramref name="mat"/> is the plain capsule's.</summary>
        internal static Projectile BuildDart(Material mat)
        {
            bool there = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null;
            if (there) BecomeFoamDart();
            GameObject root = there ? PrefabUtility.LoadPrefabContents(PrefabPath) : new GameObject("FoamDart");
            try
            {
                if (!root.TryGetComponent(out FoamDart dart))
                {
                    if (root.TryGetComponent(out Projectile _))
                    {
                        Debug.LogError("[Dart] " + PrefabPath + " still holds a plain Projectile (the change to FoamDart did not take): left as it was.");
                        return null;
                    }
                    dart = root.AddComponent<FoamDart>();
                }
                Transform plain = Plain(root.transform, mat);
                Transform shape = Shape(root.transform);
                plain.gameObject.SetActive(shape == null);   // the capsule stands in only when there is no shape
                WorkshopPieces.Set(dart, "shape", shape);
                WorkshopPieces.Set(dart, "plain", plain.gameObject);

                // The test room's foam dart: 34 a second, falling at 3, gone after 4 s if it hits nothing.
                WorkshopPieces.Set(dart, "speed", 34f);
                WorkshopPieces.Set(dart, "drop", 3f);
                WorkshopPieces.Set(dart, "lifetime", 4f);
                // Its feedback (foam_dart.tscn): 8 pale blue bits where it lands, and its own sounds when the licensed
                // clips are in _Imports (a project without them plays nothing).
                WorkshopPieces.Set(dart, "burstAmount", 8);
                WorkshopPieces.Set(dart, "burstColour", new Color(0.75f, 0.88f, 1f));
                WorkshopPieces.Set(dart, "burstSpeed", 2.5f);
                WorkshopPieces.Set(dart, "burstSize", 0.035f);
                WorkshopPieces.Set(dart, "burstGravity", 9f);
                WorkshopPieces.Set(dart, "burstSeconds", 0.45f);
                // Ours: a landing on something with health throws more, bigger, warmer bits in place of those, and a
                // white ring that widens from the spot.
                WorkshopPieces.Set(dart, "targetBurstAmount", 14);
                WorkshopPieces.Set(dart, "targetBurstColour", new Color(1f, 0.86f, 0.35f));
                WorkshopPieces.Set(dart, "targetBurstSpeed", 4f);
                WorkshopPieces.Set(dart, "targetBurstSize", 0.05f);
                WorkshopPieces.Set(dart, "ringAmount", 16);
                WorkshopPieces.Set(dart, "ringColour", Color.white);
                WorkshopPieces.Set(dart, "ringReach", 0.35f);
                WorkshopPieces.Set(dart, "ringSize", 0.02f);
                WorkshopPieces.Set(dart, "ringSeconds", 0.18f);
                // It sticks where it lands (foam_dart.gd: 8 s, the middle 0.15 m back from the point), with our quiver,
                // cap and drop-off.
                WorkshopPieces.Set(dart, "stickSeconds", 8f);
                WorkshopPieces.Set(dart, "tipLength", 0.1485f);
                WorkshopPieces.Set(dart, "quiverDegrees", 12f);
                WorkshopPieces.Set(dart, "quiverSeconds", 0.4f);
                WorkshopPieces.Set(dart, "quiverSwings", 3f);
                WorkshopPieces.Set(dart, "mostStuck", 40);
                WorkshopPieces.Set(dart, "letGoSeconds", 0.35f);
                WorkshopPieces.Set(dart, "letGoFall", 9f);
                Streak(root.transform, dart);
                WorkshopPieces.WireClips(dart, "fireClips", "fire_dart_");
                WorkshopPieces.WireClips(dart, "hitClips", "hit_dart_");
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally
            {
                if (there) PrefabUtility.UnloadPrefabContents(root); else Object.DestroyImmediate(root);
            }
            return AssetDatabase.LoadAssetAtPath<Projectile>(PrefabPath);
        }

        /// <summary>An older prefab's plain Projectile becomes a FoamDart: the same component with another script, so
        /// its id and its numbers stay. Saved at once; the prefab is loaded afresh afterwards as what it now is.</summary>
        private static void BecomeFoamDart()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                if (root.TryGetComponent(out FoamDart _) || !root.TryGetComponent(out Projectile old)) return;
                var script = AssetDatabase.LoadAssetAtPath<MonoScript>(ScriptPath);
                if (script == null) { Debug.LogError("[Dart] " + ScriptPath + " is missing: the dart stays a plain Projectile."); return; }
                var so = new SerializedObject(old);
                so.FindProperty("m_Script").objectReferenceValue = script;
                so.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        /// <summary>The plain capsule (the dart before it had a shape), long axis along +Z; the older build called it "Dart".</summary>
        private static Transform Plain(Transform root, Material mat)
        {
            Transform plain = root.Find("Plain");
            if (plain == null) plain = root.Find("Dart");
            if (plain == null)
            {
                GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                Object.DestroyImmediate(body.GetComponent<Collider>());
                plain = body.transform;
                plain.SetParent(root, false);
            }
            plain.name = "Plain";
            plain.localPosition = Vector3.zero;
            plain.localRotation = Quaternion.Euler(90f, 0f, 0f);   // long axis along +Z, the way it flies
            plain.localScale = new Vector3(0.05f, 0.09f, 0.05f);
            if (plain.TryGetComponent(out MeshRenderer r))
            {
                if (mat != null) r.sharedMaterial = mat;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            return plain;
        }

        /// <summary>The drawn dart under a child "Shape", its head toward +Z (the way it flies): the foam body, Unity's
        /// plain cylinder laid along Z, and the head, Unity's plain ball pressed to its three radii.</summary>
        private static Transform Shape(Transform root)
        {
            Transform old = root.Find("Shape");
            if (old != null) Object.DestroyImmediate(old.gameObject);
            Transform shape = new GameObject("Shape").transform;
            shape.SetParent(root, false);
            // Unity's cylinder stands along Y, 2 long and 1 across; its ball is 1 across.
            Transform body = Round(PrimitiveType.Cylinder, "Body", shape, Look(BodyLook, BodyColour, BodyGlow));
            body.localRotation = Quaternion.Euler(90f, 0f, 0f);
            body.localScale = new Vector3(BodyRadius * 2f, BodyLength * 0.5f, BodyRadius * 2f);
            Transform head = Round(PrimitiveType.Sphere, "Head", shape, Look(HeadLook, HeadColour, HeadGlow));
            head.localPosition = Vector3.forward * HeadAhead;
            head.localScale = HeadRadii * 2f;
            Bounds b = WorkshopPieces.LocalBounds(shape, shape.gameObject);
            Debug.Log($"[Dart] shape: a foam body {BodyLength:F4} long and {BodyRadius * 2f:F4} across, a head {HeadRadii.x * 2f:F4} x {HeadRadii.y * 2f:F4} across and {HeadRadii.z * 2f:F4} long; in the dart it is {b.size.z:F4} long, from z {b.min.z:F4} to {b.max.z:F4} (the head end).");
            return shape;
        }

        /// <summary>One of Unity's plain round shapes as a part of the dart: no collider, no shadow.</summary>
        private static Transform Round(PrimitiveType kind, string name, Transform parent, Material look)
        {
            GameObject part = GameObject.CreatePrimitive(kind);
            Object.DestroyImmediate(part.GetComponent<Collider>());
            part.name = name;
            part.transform.SetParent(parent, false);
            var drawn = part.GetComponent<MeshRenderer>();
            if (look != null) drawn.sharedMaterial = look;
            drawn.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;   // as the capsule: a shot casts no shadow
            return part.transform;
        }

        /// <summary>One of the dart's two materials: kept as it is when it is there (its colours are its own), made as
        /// a recoloured copy of our lit material when it is not.</summary>
        private static Material Look(string path, Color colour, Color glow)
        {
            var look = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (look != null) return look;
            if (!AssetDatabase.CopyAsset(LookSource, path)) { Debug.LogWarning("[Dart] could not copy " + LookSource + " to " + path + ": that part of the dart keeps Unity's plain grey."); return null; }
            look = AssetDatabase.LoadAssetAtPath<Material>(path);
            look.SetColor("_BaseColor", colour);
            if (look.HasProperty("_Color")) look.SetColor("_Color", colour);
            if (look.HasProperty("_EmissionColor"))
            {
                look.EnableKeyword("_EMISSION");
                look.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
                look.SetColor("_EmissionColor", glow);
            }
            EditorUtility.SetDirty(look);
            return look;
        }

        /// <summary>The streak (foam_dart.tscn: 1.6 long, 0.045 thick, pale blue at 0.75), drawn with the paint streak's
        /// material (made by Shift Jam > Paint; without it the dart flies without a streak).</summary>
        private static void Streak(Transform root, FoamDart dart)
        {
            var look = AssetDatabase.LoadAssetAtPath<Material>(StreakLook);
            Transform tail = root.Find("Streak");
            if (look == null)
            {
                if (tail == null) Debug.LogWarning("[Dart] " + StreakLook + " is missing (Shift Jam > Paint makes it): the dart is built without its streak.");
                return;   // an existing streak is left as it is
            }
            if (tail == null)
            {
                tail = new GameObject("Streak").transform;
                tail.SetParent(root, false);
            }
            if (!tail.TryGetComponent(out TrailRenderer trail)) trail = tail.gameObject.AddComponent<TrailRenderer>();
            trail.sharedMaterial = look;
            trail.minVertexDistance = 0.01f;
            trail.numCapVertices = 2;
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            trail.receiveShadows = false;
            WorkshopPieces.Set(dart, "streak", trail);
            WorkshopPieces.Set(dart, "streakLength", 1.584f);
            WorkshopPieces.Set(dart, "streakWidth", 0.0446f);
            WorkshopPieces.Set(dart, "streakColour", new Color(0.9f, 0.97f, 1f, 0.75f));
        }
    }
}
#endif
