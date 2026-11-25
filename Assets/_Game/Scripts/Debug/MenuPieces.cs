#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ShiftJam
{
    /// <summary>
    /// Editor maker for the title menu's model prefab (D-0196 to D-0199): Resources/Titles/TitleMenuModel.prefab. It
    /// makes the prefab if it is not there (the paged model file under a MenuModel) and FILLS WHAT IS EMPTY on it: the
    /// slab material, the MenuDarts beside the model, its dart, its two guns (the weapon pack's first pistol and first
    /// machine gun, sized to a length on the menu), their sounds. It never writes over a reference or a number that is
    /// already set, so a value tuned on the prefab stays: to have one made again, empty it and run this.
    /// The dart is Prefabs/Menu/MenuDart.prefab: the foam dart's own shape (Prefabs/Combat/FoamDart.prefab, its child
    /// "Shape") with nothing that flies or hits, made once here.
    /// The guns and the sounds are licensed files under _Imports: in a copy of the project without them the menu has no
    /// gun and chooses the plain way.
    /// </summary>
    public static class MenuPieces
    {
        public const string PrefabPath = "Assets/_Game/Resources/Titles/TitleMenuModel.prefab";
        private const string ModelPath = "Assets/_Game/Art/Menu/Exports/ShiftJam_Menu_Pages.fbx";
        private const string SlabLook = "Assets/_Game/Data/MenuSlab.mat";
        private const string DartPath = "Assets/_Game/Prefabs/Menu/MenuDart.prefab";
        private const string FoamDartPath = "Assets/_Game/Prefabs/Combat/FoamDart.prefab";
        private const string PistolWeapon = "Assets/_Game/Prefabs/Weapons/DartPistol.prefab";
        private const string Pack = "Assets/_Imports/ithappy/Cartoon_Weapons/Prefabs/Weapon_Presets/";

        // The two guns as they are first made: the model, its length on the menu (metres), and its numbers.
        private struct GunRow { public string Label, Model; public float Length, Interval, Spread, Knock, Kick, Volume; public bool Chooses, Auto; }
        private static readonly GunRow[] Guns =
        {
            new GunRow { Label = "dart blaster", Model = Pack + "Pistol/Pistol1.prefab", Length = 0.42f, Interval = 0.2f, Spread = 0f, Knock = 1f, Kick = 0.06f, Volume = 0.5f, Chooses = true, Auto = false },
            new GunRow { Label = "dart LMG", Model = Pack + "MG/MG1.prefab", Length = 0.85f, Interval = 0.07f, Spread = 0.1f, Knock = 0.35f, Kick = 0.025f, Volume = 0.3f, Chooses = false, Auto = true },
        };

        [MenuItem("Shift Jam/Menu/Build Title Menu Model")]
        public static void Build() => Debug.Log("[Menu] " + BuildNow());

        /// <summary>Makes or fills the prefab; returns what it did, in words.</summary>
        public static string BuildNow()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return "in Play: nothing made.";
            var did = new List<string>();
            WorkshopPieces.EnsureFolder("Assets/_Game/Resources/Titles");
            WorkshopPieces.EnsureFolder("Assets/_Game/Prefabs/Menu");
            GameObject dart = EnsureDart(did);

            bool there = File.Exists(PrefabPath);
            Scene stage = default;
            GameObject root;
            if (there) root = PrefabUtility.LoadPrefabContents(PrefabPath);
            else
            {
                var file = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
                if (file == null) return "no model at " + ModelPath + ": nothing made.";
                stage = EditorSceneManager.NewPreviewScene();
                root = new GameObject("TitleMenuModel");
                SceneManager.MoveGameObjectToScene(root, stage);
                ((GameObject)PrefabUtility.InstantiatePrefab(file, stage)).transform.SetParent(root.transform, false);
                did.Add("made the prefab from " + ModelPath);
            }
            try
            {
                if (!root.TryGetComponent(out MenuModel model)) { model = root.AddComponent<MenuModel>(); did.Add("added MenuModel"); }
                var m = new SerializedObject(model);
                Fill(m, "slabMaterial", AssetDatabase.LoadAssetAtPath<Material>(SlabLook), did);
                m.ApplyModifiedPropertiesWithoutUndo();

                if (!root.TryGetComponent(out MenuDarts darts)) { darts = root.AddComponent<MenuDarts>(); did.Add("added MenuDarts"); }
                var d = new SerializedObject(darts);
                Fill(d, "dartModel", dart, did);
                AudioClip[] fire = Clips(FoamDartPath, "fireClips"), land = Clips(FoamDartPath, "hitClips");
                if (fire.Length == 0) fire = Clips(PistolWeapon, "fireClip");
                SerializedProperty guns = d.FindProperty("guns");
                if (guns.arraySize == 0)
                {
                    foreach (GunRow row in Guns)
                    {
                        var shape = AssetDatabase.LoadAssetAtPath<GameObject>(row.Model);
                        if (shape == null) { did.Add("NO model for the " + row.Label + " (" + row.Model + ")"); continue; }
                        guns.arraySize++;
                        SerializedProperty g = guns.GetArrayElementAtIndex(guns.arraySize - 1);
                        float longest = Longest(shape);
                        g.FindPropertyRelative("label").stringValue = row.Label;
                        g.FindPropertyRelative("model").objectReferenceValue = shape;
                        g.FindPropertyRelative("size").floatValue = longest > 1e-4f ? row.Length / longest : 1f;
                        g.FindPropertyRelative("chooses").boolValue = row.Chooses;
                        g.FindPropertyRelative("auto").boolValue = row.Auto;
                        g.FindPropertyRelative("interval").floatValue = row.Interval;
                        g.FindPropertyRelative("spread").floatValue = row.Spread;
                        g.FindPropertyRelative("knock").floatValue = row.Knock;
                        g.FindPropertyRelative("kick").floatValue = row.Kick;
                        g.FindPropertyRelative("volume").floatValue = row.Volume;
                        SetClips(g.FindPropertyRelative("fireClips"), fire);
                        did.Add("the " + row.Label + " = " + shape.name + ", " + longest.ToString("F2") + " m long, drawn x" + (row.Length / Mathf.Max(1e-4f, longest)).ToString("F2")
                            + (Has(shape, "Muzzle") && Has(shape, "Magazine") ? "" : " (no Muzzle and Magazine in it: its longest side is taken as its barrel)") + ", " + fire.Length + " firing sounds");
                    }
                }
                SerializedProperty landed = d.FindProperty("landClips");
                if (landed.arraySize == 0 && land.Length > 0) { SetClips(landed, land); did.Add(land.Length + " landing sounds"); }
                d.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath, out bool saved);
                if (!saved) did.Add("THE PREFAB DID NOT SAVE");
            }
            finally
            {
                if (there) PrefabUtility.UnloadPrefabContents(root);
                else EditorSceneManager.ClosePreviewScene(stage);
            }
            return did.Count == 0 ? "nothing was empty: " + PrefabPath + " left as it is." : string.Join("; ", did) + ".";
        }

        private static void Fill(SerializedObject on, string field, Object with, List<string> did)
        {
            SerializedProperty p = on.FindProperty(field);
            if (p == null) { did.Add("NO field " + field); return; }
            if (p.objectReferenceValue != null || with == null) return;
            p.objectReferenceValue = with;
            did.Add(field + " = " + with.name);
        }

        private static void SetClips(SerializedProperty list, AudioClip[] clips)
        {
            list.arraySize = clips.Length;
            for (int i = 0; i < clips.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = clips[i];
        }

        /// <summary>The sounds in a field (one clip or a list of them) of any component on a prefab's root.</summary>
        private static AudioClip[] Clips(string prefab, string field)
        {
            var found = new List<AudioClip>();
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(prefab);
            if (asset == null) return found.ToArray();
            foreach (Component c in asset.GetComponents<Component>())
            {
                if (c == null) continue;
                SerializedProperty p = new SerializedObject(c).FindProperty(field);
                if (p == null) continue;
                if (p.isArray) { for (int i = 0; i < p.arraySize; i++) if (p.GetArrayElementAtIndex(i).objectReferenceValue is AudioClip a) found.Add(a); }
                else if (p.propertyType == SerializedPropertyType.ObjectReference && p.objectReferenceValue is AudioClip one) found.Add(one);
            }
            return found.ToArray();
        }

        private static bool Has(GameObject shape, string child)
        {
            foreach (Transform t in shape.GetComponentsInChildren<Transform>(true)) if (t.name.Contains(child)) return true;
            return false;
        }

        /// <summary>The longest side of a model's meshes, metres, in its own space.</summary>
        private static float Longest(GameObject shape)
        {
            bool any = false; var box = new Bounds();
            foreach (MeshFilter f in shape.GetComponentsInChildren<MeshFilter>(true))
            {
                if (f.sharedMesh == null) continue;
                Bounds m = f.sharedMesh.bounds;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 corner = m.center + Vector3.Scale(m.extents, new Vector3((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f));
                    Vector3 at = shape.transform.InverseTransformPoint(f.transform.TransformPoint(corner));
                    if (!any) { box = new Bounds(at, Vector3.zero); any = true; } else box.Encapsulate(at);
                }
            }
            return any ? Mathf.Max(box.size.x, Mathf.Max(box.size.y, box.size.z)) : 0f;
        }

        /// <summary>The menu's dart: the foam dart's shape alone. Made once; an existing one is kept.</summary>
        private static GameObject EnsureDart(List<string> did)
        {
            var have = AssetDatabase.LoadAssetAtPath<GameObject>(DartPath);
            if (have != null) return have;
            if (!File.Exists(FoamDartPath)) { did.Add("no " + FoamDartPath + ": the menu has no dart, so no gun"); return null; }
            GameObject foam = PrefabUtility.LoadPrefabContents(FoamDartPath);
            try
            {
                Transform shape = foam.transform.Find("Shape");
                if (shape == null) { did.Add("the foam dart has no child Shape: the menu has no dart, so no gun"); return null; }
                var dart = new GameObject("MenuDart");
                SceneManager.MoveGameObjectToScene(dart, foam.scene);
                dart.transform.SetPositionAndRotation(foam.transform.position, foam.transform.rotation);
                shape.SetParent(dart.transform, true);   // it keeps its place and turn about the dart's middle: +Z stays the way it flies
                foreach (Collider c in dart.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
                PrefabUtility.SaveAsPrefabAsset(dart, DartPath, out bool saved);
                did.Add(saved ? "made " + DartPath + ", " + Longest(dart).ToString("F3") + " m long" : "THE DART DID NOT SAVE");
            }
            finally { PrefabUtility.UnloadPrefabContents(foam); }
            return AssetDatabase.LoadAssetAtPath<GameObject>(DartPath);
        }
    }
}
#endif
