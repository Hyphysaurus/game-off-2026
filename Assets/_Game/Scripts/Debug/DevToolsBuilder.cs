#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// Editor makers for the dev tools. "Build Pocket Core" writes Pieces/Interactables/PocketCore.prefab (re-running
    /// overwrites it, same GUID): a trigger sphere for E, the ShiftSource (Toggle, unlimited), the PocketCore marker, his orb
    /// mote drawing as a warm amber billboard glow (a copy of OrbGlow.mat, so the course's cyan cores stay as they are) and a
    /// small light. Then Shift Jam > Workshop > Build Catalog lists it, and F2 / the console's `core` can make it.
    /// </summary>
    public static class DevToolsBuilder
    {
        private const string PrefabPath = "Assets/_Game/Prefabs/Pieces/Interactables/PocketCore.prefab";
        private const string GlowPath = "Assets/_Game/Data/PocketCoreGlow.mat";
        private static readonly Color Amber = new Color(1f, 0.62f, 0.22f);

        [MenuItem("Shift Jam/Dev/Build Pocket Core")]
        public static void BuildPocketCore()
        {
            var root = new GameObject("PocketCore");
            var sphere = root.AddComponent<SphereCollider>();
            sphere.isTrigger = true; sphere.radius = 1.2f;
            root.AddComponent<ShiftSource>();   // Toggle, unlimited, awake: its defaults
            root.AddComponent<PocketCore>();

            var glow = GameObject.CreatePrimitive(PrimitiveType.Quad);
            glow.name = "Glow"; glow.transform.SetParent(root.transform, false);
            glow.transform.localScale = Vector3.one * 5f;
            Object.DestroyImmediate(glow.GetComponent<Collider>());
            var r = glow.GetComponent<MeshRenderer>();
            r.sharedMaterial = GlowMaterial();
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            glow.AddComponent<Billboard>();

            var lightGo = new GameObject("Light");
            lightGo.transform.SetParent(root.transform, false);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Point; light.range = 8f; light.intensity = 1.2f; light.color = Amber; light.shadows = LightShadows.None;

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
            AssetDatabase.SaveAssets();
            Debug.Log("[Dev] Pocket Core -> " + PrefabPath + ". Next: Shift Jam > Workshop > Build Catalog, then F2 in Play.");
        }

        /// <summary>Makes Resources/VehicleCamera.asset (the vehicle camera's tuning, D-0036) if it is missing, and selects it.
        /// Every ShoulderCamera without its own settings loads it, so one asset tunes the driving camera everywhere.</summary>
        [MenuItem("Shift Jam/Dev/Create Vehicle Camera Settings")]
        public static void CreateVehicleCameraSettings()
        {
            const string path = "Assets/_Game/Resources/VehicleCamera.asset";
            var asset = AssetDatabase.LoadAssetAtPath<VehicleCameraSettings>(path);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<VehicleCameraSettings>();
                AssetDatabase.CreateAsset(asset, path);
                AssetDatabase.SaveAssets();
            }
            Selection.activeObject = asset;
            Debug.Log("[Dev] Vehicle camera settings -> " + path + " (edits apply live in Play).");
        }

        /// <summary>His orb mote, warm: a copy of OrbGlow.mat recoloured (no shader lookups).</summary>
        private static Material GlowMaterial()
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(GlowPath);
            if (mat != null) return mat;
            if (!AssetDatabase.CopyAsset("Assets/_Game/Data/OrbGlow.mat", GlowPath)) { Debug.LogWarning("[Dev] Could not copy OrbGlow.mat for the Pocket Core."); return null; }
            mat = AssetDatabase.LoadAssetAtPath<Material>(GlowPath);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Amber);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", Amber);
            EditorUtility.SetDirty(mat);
            return mat;
        }
    }
}
#endif
