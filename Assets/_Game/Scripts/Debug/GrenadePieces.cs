#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// Editor maker for the grenade (D-0077), after the test room's grenade.tscn: the Cartoon Weapons pack's
    /// Grenade_HE_01 at the pack's own size on a bouncing ball (radius 0.085, weight 0.4, bounce 0.35, grip 0.9, drag
    /// 0.6 and 2.1; flown by BallFlight), referenced from the pack by path, never copied. It goes to Prefabs/Combat/Grenade.prefab. It also
    /// makes Resources/Grenades (the thrower's numbers; never overwritten once it exists), points it at the prefab and
    /// gives both their licensed sounds when those are in _Imports. Every number written here is the test room's.
    /// </summary>
    public static class GrenadePieces
    {
        private const string PrefabPath = "Assets/_Game/Prefabs/Combat/Grenade.prefab";
        private const string SettingsPath = "Assets/_Game/Resources/Grenades.asset";
        private const string Model = "Assets/_Imports/ithappy/Cartoon_Weapons/Prefabs/Explosives/Grenade_HE_01.prefab";
        private const string Sounds = "Assets/_Imports/CasePortman/Weapons/";

        // grenade.tscn. The pack's models are the same size in both engines, so the ball is number for number.
        private const float Radius = 0.085f, Weight = 0.4f, Bounce = 0.35f, Grip = 0.9f;
        // Its drag: grenade.tscn's 0.5 and 2 are added to the 0.1 the test room's engine gives every body (Godot's default damping).
        private const float Drag = 0.6f, SpinDrag = 2.1f;
        private static readonly Vector3 ModelOffset = new Vector3(-0.01f, -0.022f, 0f);   // the test room's, its x mirrored

        [MenuItem("Shift Jam/Workshop/Build Grenade")]
        public static void Build()
        {
            WorkshopPieces.EnsureFolder("Assets/_Game/Prefabs/Combat");
            WorkshopPieces.EnsureFolder("Assets/_Game/Resources");
            Grenade grenade = BuildGrenade();
            var settings = WorkshopPieces.SettingsAsset<GrenadeSettings>(SettingsPath);
            WorkshopPieces.Set(settings, "grenade", grenade);
            AudioClip thrown = AssetDatabase.LoadAssetAtPath<AudioClip>(Sounds + "throw.ogg");
            if (thrown != null && settings.throwClip == null) WorkshopPieces.Set(settings, "throwClip", thrown);
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Grenade] {(grenade != null ? "built " + PrefabPath : "NOT built (the pack's model is missing)")}; settings {SettingsPath}; the throw sound {(settings.throwClip != null ? "set" : "missing")} (sounds live under {Sounds}, git-ignored).");
        }

        private static Grenade BuildGrenade()
        {
            GameObject model = WorkshopPieces.Load(Model);
            if (model == null) return null;
            var root = new GameObject("Grenade");
            var holder = new GameObject("Model");
            holder.transform.SetParent(root.transform, false);
            holder.transform.localPosition = ModelOffset;
            WorkshopPieces.Place(model, holder.transform);

            var ball = root.AddComponent<SphereCollider>();
            ball.radius = Radius;
            var body = root.AddComponent<Rigidbody>();
            body.mass = Weight;
            body.isKinematic = true;   // BallFlight moves it, step by step
            body.useGravity = false;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            var flight = root.AddComponent<BallFlight>();   // the flight's numbers, the test room's
            WorkshopPieces.Set(flight, "bounce", Bounce);
            WorkshopPieces.Set(flight, "grip", Grip);
            WorkshopPieces.Set(flight, "drag", Drag);
            WorkshopPieces.Set(flight, "spinDrag", SpinDrag);
            var grenade = root.AddComponent<Grenade>();   // its numbers are the component's own defaults: the test room's

            // The boom: boom_1.ogg, boom_2.ogg, those that are there.
            var so = new SerializedObject(grenade);
            SerializedProperty clips = so.FindProperty("blastClips");
            clips.ClearArray();
            for (int i = 1; i <= 2; i++)
            {
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(Sounds + "boom_" + i + ".ogg");
                if (clip == null) continue;
                clips.InsertArrayElementAtIndex(clips.arraySize);
                clips.GetArrayElementAtIndex(clips.arraySize - 1).objectReferenceValue = clip;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            return WorkshopPieces.Save(root, PrefabPath).GetComponent<Grenade>();
        }
    }
}
#endif
