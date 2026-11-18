#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// Editor maker for the foam dart (D-0079): Prefabs/Combat/FoamDart.prefab, the shot every blaster and the toy
    /// soldier fire. An existing prefab is changed in place, so its ids stay and every gun that names it still does;
    /// a plain Projectile left by an older build becomes a FoamDart with its numbers kept. What it makes:
    /// the dart's shape (child "Shape": the Cartoon Weapons pack's Bullet_Shotgun, scaled up, its wide striped head
    /// first, referenced from the pack by path, never copied), the plain capsule kept as a stand-in for a project
    /// without the pack (child "Plain"), the streak, and the dart's numbers: the test room's flight, bits and
    /// sticking, and our own bits and ring for a landing on something with health.
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

        // The shape. One model for every gun for now; another gun's dart is another model here, or a prefab change.
        private const string Model = "Assets/_Imports/ithappy/Cartoon_Weapons/Prefabs/Bullets/Bullet_Shotgun.prefab";
        // The pack's shell is 64 mm long with a wide striped rim at one end. That rim leads, like a toy dart's suction
        // head (a pointed bullet would lead with its narrow end instead).
        private const bool WideEndLeads = true;
        private const float ShapeLength = 0.2376f;   // the test room's dart body, 0.24 m x 0.99 (the capsule it replaces was 0.18)
        private const float NoseAhead = 0.1683f;     // the test room's nose, 0.17 m x 0.99 ahead of the dart's middle: 0.02 past the point it sticks at

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
                plain.gameObject.SetActive(shape == null);   // the capsule stands in only when the pack's model is not there
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

        /// <summary>The drawn dart: the pack's model under a child "Shape", turned head first, scaled to ShapeLength and
        /// put with its nose NoseAhead in front of the dart's middle. Null when the pack is not in the project.</summary>
        private static Transform Shape(Transform root)
        {
            Transform old = root.Find("Shape");
            if (old != null) Object.DestroyImmediate(old.gameObject);
            GameObject model = WorkshopPieces.Load(Model);
            if (model == null) return null;
            Transform shape = new GameObject("Shape").transform;
            shape.SetParent(root, false);
            GameObject inst = WorkshopPieces.Place(model, shape);
            MeshFilter mesh = inst.GetComponentInChildren<MeshFilter>(true);
            if (mesh == null || mesh.sharedMesh == null || !Head(mesh, out Vector3 headInMesh, out string measured))
            {
                Debug.LogWarning("[Dart] " + Model + " has no mesh that can be measured: the dart keeps its plain capsule.");
                Object.DestroyImmediate(shape.gameObject);
                return null;
            }
            foreach (Renderer r in inst.GetComponentsInChildren<Renderer>(true)) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;   // as the capsule: a shot casts no shadow

            Vector3 head = shape.InverseTransformDirection(mesh.transform.TransformDirection(headInMesh)).normalized;
            inst.transform.localRotation = Quaternion.FromToRotation(head, Vector3.forward) * inst.transform.localRotation;
            Bounds b = WorkshopPieces.LocalBounds(shape, inst);
            inst.transform.localScale *= ShapeLength / Mathf.Max(1e-5f, b.size.z);
            b = WorkshopPieces.LocalBounds(shape, inst);
            inst.transform.localPosition += new Vector3(-b.center.x, -b.center.y, NoseAhead - b.max.z);
            b = WorkshopPieces.LocalBounds(shape, inst);
            Debug.Log($"[Dart] shape {Model}: {measured}; in the dart it is {b.size.z:F4} long and {b.size.x:F4} x {b.size.y:F4} across, from z {b.min.z:F4} to {b.max.z:F4} (the head end).");
            return shape;
        }

        /// <summary>Which way the model's head points, in the mesh's own space: along its longest side, toward its wide
        /// end (or its narrow one), found by measuring how far the vertices of each end's fifth stand off the long axis.
        /// Pack meshes are not Read/Write, but the editor can read them outside play.</summary>
        private static bool Head(MeshFilter filter, out Vector3 head, out string measured)
        {
            head = Vector3.forward; measured = "";
            Mesh mesh = filter.sharedMesh;
            Vector3[] points = mesh.vertices;
            if (points == null || points.Length == 0) return false;
            Bounds b = mesh.bounds;
            int along = b.size.x >= b.size.y && b.size.x >= b.size.z ? 0 : b.size.y >= b.size.z ? 1 : 2;
            float low = b.min[along], length = b.size[along];
            if (length < 1e-6f) return false;
            float lowWide = 0f, highWide = 0f;
            foreach (Vector3 p in points)
            {
                Vector3 off = p - b.center; off[along] = 0f;
                float share = (p[along] - low) / length;
                if (share <= 0.2f) lowWide = Mathf.Max(lowWide, off.magnitude);
                else if (share >= 0.8f) highWide = Mathf.Max(highWide, off.magnitude);
            }
            bool highIsWide = highWide > lowWide;
            Vector3 axis = Vector3.zero; axis[along] = 1f;
            head = highIsWide == WideEndLeads ? axis : -axis;
            measured = $"the mesh is longest along its own {"XYZ"[along]} ({length:F4}); its low end is {lowWide:F4} wide (radius), its high end {highWide:F4}; the head is the {(WideEndLeads ? "wide" : "narrow")} end, toward {(head[along] > 0f ? "+" : "-")}{"XYZ"[along]}";
            return true;
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
