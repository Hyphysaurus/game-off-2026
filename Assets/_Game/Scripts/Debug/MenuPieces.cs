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
    /// The title's letters (D-0200): each run it bakes five shapes, one per letter, from his title sheet (a small box for
    /// every pixel) into Art/Title/Letters (one file a letter) and writes them and their places on the prefab's MenuTitle; these are
    /// worked out from the sheet, so they ARE written again every run. It also makes the colour sets that are missing
    /// (Art/Title/Sets/title_set_N.png: the sheet with every hue turned; a file that is there is never written over, so
    /// one he repaints stays) and keeps their import settings (point filter, no mip maps, not rescaled, uncompressed).
    /// The menu's own sounds (a let-go, the shift, a page, a letter's colour change) are given to the slots that have
    /// no clip, from the playroom sound pack, chosen by their names.
    /// The paint (D-0201): it makes the menu's paint ball (Prefabs/Menu/MenuPaintBall.prefab: the game's ball's child
    /// "Ball" alone, nothing that flies or hits) and the splats' see-through unlit material (Data/MenuSplat.mat, drawn
    /// before the words) when they are missing, adds the MenuSplats beside the model, and gives MenuDarts its paint row
    /// (the first row that bursts; one is added when the list is empty) whatever of these it has none of: the ball, the
    /// paint's six colours and its shell colours, the head-on splat pictures, the paint's firing and landing sounds, all
    /// from Resources/PaintSettings. Without the paint (no settings, no pictures or no ball) no row is made and the menu
    /// keeps its darts only.
    /// The guns' colours (D-0204): a gun row whose Colour Set is empty is given the weapon pack's ColorSet_03_Cyber, whose
    /// two strong colours are a cyan (#7fe7f0) and a magenta pink (#e46be0): the game's orb cyan and candy pink, on the
    /// pack's light grey. The pistol came in the pack's grey Ash (off the palette); the LMG already wore Cyber.
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
        private const string GunColours = "Assets/_Imports/ithappy/Cartoon_Weapons/Materials/ColorSet_03_Cyber.mat";   // cyan and pink: the game's own (D-0204)

        // ---- the paint (D-0201)
        private const string PaintSettingsPath = "Assets/_Game/Resources/" + PaintSettings.ResourceName + ".asset";
        private const string PaintBallPath = "Assets/_Game/Prefabs/Combat/PaintBall.prefab";
        private const string MenuBallPath = "Assets/_Game/Prefabs/Menu/MenuPaintBall.prefab";   // its root is called MenuPaintBall: a file's first object takes the file's name
        private const string SplatLook = "Assets/_Game/Data/MenuSplat.mat";                     // the material is called MenuSplat
        private const int SplatQueueOffset = -10;      // drawn at 2990: after the slabs (2000), before the words (TextMesh Pro, 3000)
        private const string PaintFamily = "head";     // the head-on splat pictures
        // The paint row as it is first made: its names, the ball's width on the menu (metres: the game's 0.02 m ball would be
        // 12 screen pixels, too small to read beside a 0.13 m button), the splat's width where there is room, its loudness.
        private const string PaintLabel = "paint balls", PaintOne = "paint ball";
        private const float BallWidth = 0.05f, SplatWidth = 0.24f, PaintFireVolume = 0.45f, PaintLandVolume = 0.5f;

        // The two guns as they are first made: the model, its length on the menu (metres), and its numbers.
        private struct GunRow { public string Label, Model; public float Length, Interval, Spread, Knock, Kick, Volume; public bool Chooses, Auto; }
        private static readonly GunRow[] Guns =
        {
            new GunRow { Label = "dart blaster", Model = Pack + "Pistol/Pistol1.prefab", Length = 0.42f, Interval = 0.2f, Spread = 0f, Knock = 1f, Kick = 0.06f, Volume = 0.5f, Chooses = true, Auto = false },
            new GunRow { Label = "dart LMG", Model = Pack + "MG/MG1.prefab", Length = 0.85f, Interval = 0.07f, Spread = 0.1f, Knock = 0.35f, Kick = 0.025f, Volume = 0.3f, Chooses = false, Auto = true },
        };

        // ---- the title's letters (D-0200), from his title sheet
        // These are his title's own measurements: 64 frames of 75 by 35 side by side, one outlined block whose five letters
        // share their outline columns (20, 34, 40 and 54 belong to both neighbours, so each letter keeps a whole outline
        // when the one beside it is knocked away). If the lettering is redrawn, measure it again and change these.
        private const string TitleSheet = "Assets/_Game/Art/Title/ShiftJamTitlePixelRiftCandySprite.png";
        private const string TitleLetters = "Assets/_Game/Art/Title/Letters";      // one file a letter (Title_S.asset and so on): a file's first object takes the file's name
        private const string TitleSets = "Assets/_Game/Art/Title/Sets";
        private const int TitleFrameWidth = 75;
        private const float TitlePixel = 0.02f;     // metres of the model to one pixel of the art: 12 screen pixels on a 1080-high screen, as the flat title was drawn
        private const float TitleDepth = 0.08f;     // how thick the letters are, metres
        private static readonly string[] LetterNames = { "Title_S", "Title_H", "Title_I", "Title_F", "Title_T" };
        private static readonly int[] LetterFrom = { 6, 20, 34, 40, 54 }, LetterTo = { 20, 34, 40, 54, 68 };   // each letter's columns in a frame, both ends in
        // The further colour sets: the sheet with every hue turned this many degrees.
        private static readonly float[] SetHues = { 70f, 150f, 215f, 290f };

        // The menu's own sounds as they are first given: the slot, the file, its loudness, how much its pitch varies. They
        // were chosen by their NAMES in the playroom sound pack, unheard: swap them on the prefab.
        private const string Fx = "Assets/_Imports/ithappy/Playroom/Playroom_Sound_FX/";
        private struct SoundRow { public string Slot, File; public float Volume, Spread; }
        private static readonly SoundRow[] ModelSounds =
        {
            new SoundRow { Slot = "letGoSound", File = Fx + "Toys/Spring_Boing.wav", Volume = 0.45f, Spread = 0.08f },
            new SoundRow { Slot = "shiftSound", File = Fx + "Toys/Star_Light_On.wav", Volume = 0.5f, Spread = 0f },
            new SoundRow { Slot = "shiftBackSound", File = Fx + "Toys/Star_Light_Off.wav", Volume = 0.5f, Spread = 0f },
            new SoundRow { Slot = "pageSound", File = Fx + "Props/Book_Page_Turn_v1.wav", Volume = 0.35f, Spread = 0.05f },
        };
        private static readonly SoundRow[] TitleSounds =
        {
            new SoundRow { Slot = "changeSound", File = Fx + "Toys/Toy_Squeak_Single_v1.wav", Volume = 0.4f, Spread = 0.2f },
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
            var paint = AssetDatabase.LoadAssetAtPath<PaintSettings>(PaintSettingsPath);
            GameObject ball = paint != null ? EnsureBall(did) : null;
            var facts = new List<string>();     // what it found, said every run (not changes)
            bool haveTitle = BakeTitle(did, facts, out Mesh[] letters, out Vector2[] middles, out int frames, out Texture2D[] sets);

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
                foreach (SoundRow row in ModelSounds) FillCue(m, row, did);
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
                // The guns' colour set (D-0204), on every row that has none.
                var toyColours = AssetDatabase.LoadAssetAtPath<Material>(GunColours);
                if (toyColours == null) did.Add("no " + GunColours + ": the guns keep their own colours");
                else
                    for (int i = 0; i < guns.arraySize; i++)
                    {
                        SerializedProperty g = guns.GetArrayElementAtIndex(i), set = g.FindPropertyRelative("colourSet");
                        if (set == null) { did.Add("NO field colourSet"); break; }
                        if (set.objectReferenceValue != null) continue;
                        set.objectReferenceValue = toyColours;
                        did.Add("the " + g.FindPropertyRelative("label").stringValue + "'s colour set = " + toyColours.name);
                    }
                SerializedProperty landed = d.FindProperty("landClips");
                if (landed.arraySize == 0 && land.Length > 0) { SetClips(landed, land); did.Add(land.Length + " landing sounds"); }
                FillPaint(d, paint, ball, did);
                d.ApplyModifiedPropertiesWithoutUndo();

                if (!root.TryGetComponent(out MenuSplats splats)) { splats = root.AddComponent<MenuSplats>(); did.Add("added MenuSplats"); }
                var sp = new SerializedObject(splats);
                SerializedProperty splatLook = sp.FindProperty("material");
                if (splatLook != null && splatLook.objectReferenceValue == null)
                {
                    Material look = EnsureSplatLook(AssetDatabase.LoadAssetAtPath<Material>(SlabLook), did);
                    if (look != null) { splatLook.objectReferenceValue = look; did.Add("the splats' material = " + look.name); }
                }
                sp.ApplyModifiedPropertiesWithoutUndo();

                if (haveTitle)
                {
                    if (!root.TryGetComponent(out MenuTitle title)) { title = root.AddComponent<MenuTitle>(); did.Add("added MenuTitle"); }
                    var t = new SerializedObject(title);
                    // Worked out from the sheet: written every run, said only when they change.
                    bool changed = SetObjects(t.FindProperty("letters"), letters);
                    changed |= SetPlaces(t.FindProperty("letterAt"), middles);
                    SerializedProperty count = t.FindProperty("frames");
                    if (count.intValue != frames) { count.intValue = frames; changed = true; }
                    if (changed) did.Add("the title's letters and their places written");
                    SerializedProperty list = t.FindProperty("colourSets");
                    if (list.arraySize == 0) { SetObjects(list, sets); did.Add(sets.Length + " colour sets (his sheet first)"); }
                    foreach (SoundRow row in TitleSounds) FillCue(t, row, did);
                    t.ApplyModifiedPropertiesWithoutUndo();
                }
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath, out bool saved);
                if (!saved) did.Add("THE PREFAB DID NOT SAVE");
            }
            finally
            {
                if (there) PrefabUtility.UnloadPrefabContents(root);
                else EditorSceneManager.ClosePreviewScene(stage);
            }
            return (did.Count == 0 ? "nothing was empty: " + PrefabPath + " left as it is." : string.Join("; ", did) + ".")
                + (facts.Count > 0 ? " [" + string.Join("; ", facts) + "]" : "");
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

        /// <summary>Gives a sound slot its clip, loudness and pitch spread, if it has no clip yet. In a copy of the project
        /// without the sound pack the slot stays empty, which is silent.</summary>
        private static void FillCue(SerializedObject on, SoundRow row, List<string> did)
        {
            SerializedProperty cue = on.FindProperty(row.Slot);
            if (cue == null) { did.Add("NO field " + row.Slot); return; }
            SerializedProperty clip = cue.FindPropertyRelative("clip");
            if (clip.objectReferenceValue != null) return;
            var sound = AssetDatabase.LoadAssetAtPath<AudioClip>(row.File);
            if (sound == null) return;
            clip.objectReferenceValue = sound;
            cue.FindPropertyRelative("volume").floatValue = row.Volume;
            cue.FindPropertyRelative("pitchSpread").floatValue = row.Spread;
            did.Add(row.Slot + " = " + sound.name);
        }

        /// <summary>Writes a list of objects; true when it changed anything.</summary>
        private static bool SetObjects(SerializedProperty list, Object[] values)
        {
            bool changed = list.arraySize != values.Length;
            list.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
            {
                SerializedProperty at = list.GetArrayElementAtIndex(i);
                if (at.objectReferenceValue == values[i]) continue;
                at.objectReferenceValue = values[i]; changed = true;
            }
            return changed;
        }

        /// <summary>Writes a list of places; true when it changed anything.</summary>
        private static bool SetPlaces(SerializedProperty list, Vector2[] values)
        {
            bool changed = list.arraySize != values.Length;
            list.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
            {
                SerializedProperty at = list.GetArrayElementAtIndex(i);
                if ((at.vector2Value - values[i]).sqrMagnitude < 1e-10f) continue;
                at.vector2Value = values[i]; changed = true;
            }
            return changed;
        }

        /// <summary>Bakes the title's letters from the sheet and makes the colour sets that are missing. False, with the
        /// reason in <paramref name="did"/>, when the sheet is not there or is no longer what was measured: the title then
        /// stays the flat art.</summary>
        private static bool BakeTitle(List<string> did, List<string> facts, out Mesh[] letters, out Vector2[] middles, out int frames, out Texture2D[] sets)
        {
            letters = null; middles = null; frames = 0; sets = null;
            if (!File.Exists(TitleSheet)) { did.Add("no title sheet at " + TitleSheet + ": the title stays flat art"); return false; }
            var own = AssetDatabase.LoadAssetAtPath<Texture2D>(TitleSheet);
            if (own == null) { did.Add("the title sheet is not a picture Unity has: the title stays flat art"); return false; }
            // The file itself is read (the asset is not readable, and its import settings are not to be touched).
            var sheet = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                if (!sheet.LoadImage(File.ReadAllBytes(TitleSheet))) { did.Add("the title sheet could not be read: the title stays flat art"); return false; }
                int w = sheet.width, h = sheet.height;
                if (w % TitleFrameWidth != 0 || LetterTo[LetterTo.Length - 1] >= TitleFrameWidth)
                {
                    did.Add("the title sheet is " + w + " wide, not whole frames of " + TitleFrameWidth + ": measure it again (MenuPieces); the title stays flat art");
                    return false;
                }
                Color32[] px = sheet.GetPixels32();     // row 0 is the BOTTOM row of the picture
                frames = w / TitleFrameWidth;

                WorkshopPieces.EnsureFolder(TitleLetters);
                letters = new Mesh[LetterNames.Length]; middles = new Vector2[LetterNames.Length];
                var counts = new List<string>();
                for (int i = 0; i < LetterNames.Length; i++)
                {
                    // A shape that is there already is filled again in place, so whatever points at it still does.
                    string file = TitleLetters + "/" + LetterNames[i] + ".asset";
                    Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(file);
                    bool fresh = mesh == null;
                    if (fresh) mesh = new Mesh { name = LetterNames[i] };
                    int pixels = BakeLetter(mesh, px, w, h, LetterFrom[i], LetterTo[i], out middles[i]);
                    if (pixels == 0)
                    {
                        did.Add("no pixels in the columns of " + LetterNames[i] + " (" + LetterFrom[i] + " to " + LetterTo[i] + "): the lettering changed, measure it again (MenuPieces); the title stays flat art");
                        if (fresh) Object.DestroyImmediate(mesh);
                        return false;
                    }
                    if (fresh) { AssetDatabase.CreateAsset(mesh, file); did.Add("made " + file); }
                    else EditorUtility.SetDirty(mesh);
                    letters[i] = mesh;
                    counts.Add(LetterNames[i] + " " + pixels + " px");
                }
                AssetDatabase.SaveAssets();
                facts.Add("letters: " + string.Join(", ", counts));

                WorkshopPieces.EnsureFolder(TitleSets);
                var list = new List<Texture2D> { own };
                for (int n = 0; n < SetHues.Length; n++)
                {
                    string path = TitleSets + "/title_set_" + (n + 1) + ".png";
                    if (!File.Exists(path))
                    {
                        var turned = new Color32[px.Length];
                        for (int k = 0; k < px.Length; k++)
                        {
                            Color.RGBToHSV(px[k], out float hue, out float strength, out float light);
                            Color c = Color.HSVToRGB(Mathf.Repeat(hue + SetHues[n] / 360f, 1f), strength, light);
                            c.a = px[k].a / 255f;
                            turned[k] = c;
                        }
                        var picture = new Texture2D(w, h, TextureFormat.RGBA32, false);
                        picture.SetPixels32(turned); picture.Apply();
                        File.WriteAllBytes(path, picture.EncodeToPNG());
                        Object.DestroyImmediate(picture);
                        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                        did.Add("made " + path + " (every hue turned " + SetHues[n].ToString("F0") + " degrees)");
                    }
                    if (SetLook(path)) did.Add("import settings of " + path + " set");
                    var set = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                    if (set != null) list.Add(set);
                }
                sets = list.ToArray();
                facts.Add(sets.Length + " colour sets on disk");
                return true;
            }
            finally { Object.DestroyImmediate(sheet); }
        }

        /// <summary>Fills a shape with one letter: a box for every solid pixel of its columns in the sheet's first frame, each
        /// box's corners carrying that pixel's own place in the sheet. Faces between two solid pixels are left out; the
        /// faces toward and away from the viewer are slot 0, the sides slot 1. In the shape, x runs across, y up, and the
        /// viewer is on the -z side; the letter's own middle column is x = 0, the art's middle row y = 0.
        /// Returns how many pixels it holds; <paramref name="middle"/> is the letter's middle, metres from the title's.</summary>
        private static int BakeLetter(Mesh mesh, Color32[] px, int w, int h, int from, int to, out Vector2 middle)
        {
            float cx = (from + to + 1) * 0.5f, cy = h * 0.5f, half = TitleDepth * 0.5f;
            middle = new Vector2((cx - TitleFrameWidth * 0.5f) * TitlePixel, 0f);
            var points = new List<Vector3>(); var normals = new List<Vector3>(); var places = new List<Vector2>();
            var faces = new List<int>(); var sides = new List<int>();
            bool Solid(int x, int y) => x >= from && x <= to && y >= 0 && y < h && px[y * w + x].a > 10;
            void Quad(List<int> into, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 normal, Vector2 place)
            {
                int at = points.Count;
                points.Add(a); points.Add(b); points.Add(c); points.Add(d);
                for (int k = 0; k < 4; k++) { normals.Add(normal); places.Add(place); }
                // Which way round its corners go is checked, not trusted: the side that is drawn is the one that
                // (b - a) x (c - a) points out of.
                if (Vector3.Dot(Vector3.Cross(b - a, c - a), normal) >= 0f) { into.Add(at); into.Add(at + 1); into.Add(at + 2); into.Add(at); into.Add(at + 2); into.Add(at + 3); }
                else { into.Add(at); into.Add(at + 2); into.Add(at + 1); into.Add(at); into.Add(at + 3); into.Add(at + 2); }
            }
            int pixels = 0;
            for (int y = 0; y < h; y++)
                for (int x = from; x <= to; x++)
                {
                    if (!Solid(x, y)) continue;
                    pixels++;
                    float x0 = (x - cx) * TitlePixel, x1 = x0 + TitlePixel, y0 = (y - cy) * TitlePixel, y1 = y0 + TitlePixel;
                    var place = new Vector2((x + 0.5f) / w, (y + 0.5f) / h);
                    Quad(faces, new Vector3(x0, y0, -half), new Vector3(x0, y1, -half), new Vector3(x1, y1, -half), new Vector3(x1, y0, -half), Vector3.back, place);
                    Quad(faces, new Vector3(x0, y0, half), new Vector3(x0, y1, half), new Vector3(x1, y1, half), new Vector3(x1, y0, half), Vector3.forward, place);
                    if (!Solid(x - 1, y)) Quad(sides, new Vector3(x0, y0, -half), new Vector3(x0, y1, -half), new Vector3(x0, y1, half), new Vector3(x0, y0, half), Vector3.left, place);
                    if (!Solid(x + 1, y)) Quad(sides, new Vector3(x1, y0, -half), new Vector3(x1, y1, -half), new Vector3(x1, y1, half), new Vector3(x1, y0, half), Vector3.right, place);
                    if (!Solid(x, y - 1)) Quad(sides, new Vector3(x0, y0, -half), new Vector3(x1, y0, -half), new Vector3(x1, y0, half), new Vector3(x0, y0, half), Vector3.down, place);
                    if (!Solid(x, y + 1)) Quad(sides, new Vector3(x0, y1, -half), new Vector3(x1, y1, -half), new Vector3(x1, y1, half), new Vector3(x0, y1, half), Vector3.up, place);
                }
            mesh.Clear();
            mesh.SetVertices(points); mesh.SetNormals(normals); mesh.SetUVs(0, places);
            mesh.subMeshCount = 2;
            mesh.SetTriangles(faces, 0); mesh.SetTriangles(sides, 1);
            mesh.RecalculateBounds();
            return pixels;
        }

        /// <summary>A colour set's import settings: its pixels as they are (point filter, no mip maps, not rescaled to a power
        /// of two, uncompressed, full size). True when it had to change them.</summary>
        private static bool SetLook(string path)
        {
            var import = AssetImporter.GetAtPath(path) as TextureImporter;
            if (import == null) return false;
            if (import.textureType == TextureImporterType.Default && import.sRGBTexture && import.alphaIsTransparency && !import.mipmapEnabled && import.filterMode == FilterMode.Point
                && import.wrapMode == TextureWrapMode.Clamp && import.npotScale == TextureImporterNPOTScale.None && import.textureCompression == TextureImporterCompression.Uncompressed
                && import.maxTextureSize == 8192 && !import.isReadable) return false;
            import.textureType = TextureImporterType.Default; import.sRGBTexture = true; import.alphaIsTransparency = true; import.mipmapEnabled = false;
            import.filterMode = FilterMode.Point; import.wrapMode = TextureWrapMode.Clamp; import.npotScale = TextureImporterNPOTScale.None;
            import.textureCompression = TextureImporterCompression.Uncompressed; import.maxTextureSize = 8192; import.isReadable = false;
            import.SaveAndReimport();
            return true;
        }

        /// <summary>Gives MenuDarts its paint row whatever it has none of (D-0201). The paint row is the first row that bursts;
        /// with the list empty one is made, its numbers as first made here. A row he has changed keeps every value that is set.</summary>
        private static void FillPaint(SerializedObject darts, PaintSettings paint, GameObject ball, List<string> did)
        {
            SerializedProperty rows = darts.FindProperty("ammo");
            if (rows == null) { did.Add("NO field ammo"); return; }
            Texture2D[] pictures = paint != null ? Pictures(paint) : new Texture2D[0];
            SerializedProperty row = null;
            for (int i = 0; i < rows.arraySize; i++)
                if (rows.GetArrayElementAtIndex(i).FindPropertyRelative("bursts").boolValue) { row = rows.GetArrayElementAtIndex(i); break; }
            if (row == null)
            {
                if (rows.arraySize > 0) return;      // he has kinds of his own and none that bursts: left as it is
                if (paint == null || ball == null || pictures.Length == 0)
                {
                    did.Add("no paint for the menu (" + (paint == null ? "no " + PaintSettingsPath : ball == null ? "no ball" : "no '" + PaintFamily + "' splat pictures in the paint settings") + "): darts only");
                    return;
                }
                rows.arraySize = 1;
                row = rows.GetArrayElementAtIndex(0);
                // A new element holds no defaults: every value is set here.
                float longest = Longest(ball);
                row.FindPropertyRelative("label").stringValue = PaintLabel;
                row.FindPropertyRelative("one").stringValue = PaintOne;
                row.FindPropertyRelative("model").objectReferenceValue = null;
                row.FindPropertyRelative("size").floatValue = longest > 1e-5f ? BallWidth / longest : 1f;
                row.FindPropertyRelative("bursts").boolValue = true;
                row.FindPropertyRelative("colours").arraySize = 0;
                row.FindPropertyRelative("shellColours").arraySize = 0;
                row.FindPropertyRelative("shellHueApart").floatValue = paint.shellHueApart;
                row.FindPropertyRelative("shellPlainBelow").floatValue = paint.shellPlainBelow;
                row.FindPropertyRelative("shellFallback").colorValue = paint.shellFallback;
                row.FindPropertyRelative("pictures").arraySize = 0;
                row.FindPropertyRelative("splatSize").floatValue = SplatWidth;
                row.FindPropertyRelative("fireClips").arraySize = 0;
                row.FindPropertyRelative("fireVolume").floatValue = PaintFireVolume;
                row.FindPropertyRelative("landClips").arraySize = 0;
                row.FindPropertyRelative("landVolume").floatValue = PaintLandVolume;
                float spread = Mathf.Max(1f, paint.pitchSpread);        // the game's paint plays spread^-1 to spread^1
                row.FindPropertyRelative("pitch").vector2Value = new Vector2(1f / spread, spread);
                did.Add("the ammo row '" + PaintLabel + "' (the ball drawn " + BallWidth.ToString("F2") + " m wide, splats " + SplatWidth.ToString("F2") + " m)");
            }
            if (paint == null) return;
            string named = row.FindPropertyRelative("label").stringValue;
            SerializedProperty model = row.FindPropertyRelative("model");
            if (model.objectReferenceValue == null && ball != null) { model.objectReferenceValue = ball; did.Add(named + ": the ball = " + ball.name); }
            SerializedProperty colours = row.FindPropertyRelative("colours");
            if (colours.arraySize == 0 && paint.fillColours.Length > 0) { SetColours(colours, paint.fillColours); did.Add(named + ": " + paint.fillColours.Length + " paint colours"); }
            SerializedProperty shells = row.FindPropertyRelative("shellColours");
            if (shells.arraySize == 0 && paint.shellColours.Length > 0) { SetColours(shells, paint.shellColours); did.Add(named + ": " + paint.shellColours.Length + " shell colours"); }
            SerializedProperty looks = row.FindPropertyRelative("pictures");
            if (looks.arraySize == 0 && pictures.Length > 0) { SetObjects(looks, pictures); did.Add(named + ": " + pictures.Length + " '" + PaintFamily + "' splat pictures"); }
            SerializedProperty fire = row.FindPropertyRelative("fireClips");
            if (fire.arraySize == 0 && Count(paint.fireClips) > 0) { SetClips(fire, Present(paint.fireClips)); did.Add(named + ": " + fire.arraySize + " firing sounds"); }
            SerializedProperty land = row.FindPropertyRelative("landClips");
            if (land.arraySize == 0 && Count(paint.hitClips) > 0) { SetClips(land, Present(paint.hitClips)); did.Add(named + ": " + land.arraySize + " landing sounds"); }
        }

        /// <summary>The paint's head-on splat pictures (the family called "head", else the one drawn nearest square-on).</summary>
        private static Texture2D[] Pictures(PaintSettings paint)
        {
            PaintSettings.Family family = null;
            foreach (PaintSettings.Family f in paint.families) if (f != null && f.name == PaintFamily) { family = f; break; }
            if (family == null) foreach (PaintSettings.Family f in paint.families) if (f != null && (family == null || Mathf.Abs(90f - f.drawnAt) < Mathf.Abs(90f - family.drawnAt))) family = f;
            var found = new List<Texture2D>();
            if (family != null && family.looks != null) foreach (PaintSettings.Look look in family.looks) if (look.albedo != null) found.Add(look.albedo);
            return found.ToArray();
        }

        private static int Count(AudioClip[] clips) { int n = 0; if (clips != null) foreach (AudioClip c in clips) if (c != null) n++; return n; }

        private static AudioClip[] Present(AudioClip[] clips)
        {
            var found = new List<AudioClip>();
            foreach (AudioClip c in clips) if (c != null) found.Add(c);
            return found.ToArray();
        }

        private static void SetColours(SerializedProperty list, Color[] colours)
        {
            list.arraySize = colours.Length;
            for (int i = 0; i < colours.Length; i++) list.GetArrayElementAtIndex(i).colorValue = colours[i];
        }

        /// <summary>The menu's paint ball: the game's ball's child "Ball" alone (its sphere and its look), nothing that flies or
        /// hits. Made once; an existing one is kept.</summary>
        private static GameObject EnsureBall(List<string> did)
        {
            var have = AssetDatabase.LoadAssetAtPath<GameObject>(MenuBallPath);
            if (have != null) return have;
            if (!File.Exists(PaintBallPath)) { did.Add("no " + PaintBallPath + ": the menu has no paint ball"); return null; }
            GameObject game = PrefabUtility.LoadPrefabContents(PaintBallPath);
            try
            {
                Transform shape = game.transform.Find("Ball");
                if (shape == null) { did.Add("the paint ball has no child Ball: the menu has no paint ball"); return null; }
                var ball = new GameObject("MenuPaintBall");
                SceneManager.MoveGameObjectToScene(ball, game.scene);
                ball.transform.SetPositionAndRotation(game.transform.position, game.transform.rotation);
                shape.SetParent(ball.transform, true);
                foreach (Collider c in ball.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
                foreach (MonoBehaviour b in ball.GetComponentsInChildren<MonoBehaviour>(true)) Object.DestroyImmediate(b);
                PrefabUtility.SaveAsPrefabAsset(ball, MenuBallPath, out bool saved);
                did.Add(saved ? "made " + MenuBallPath + ", " + Longest(ball).ToString("F3") + " m wide" : "THE PAINT BALL DID NOT SAVE");
            }
            finally { PrefabUtility.UnloadPrefabContents(game); }
            return AssetDatabase.LoadAssetAtPath<GameObject>(MenuBallPath);
        }

        /// <summary>The splats' material: URP Unlit (the slab material's own shader), see-through, alpha blended, both sides,
        /// no depth write, no shadows, at render queue 2990 (after the slabs, before the words), as the build ghost's
        /// see-through unlit material is made. Made once; an existing one is kept as it is, so his tuning stays.</summary>
        private static Material EnsureSplatLook(Material slab, List<string> did)
        {
            var have = AssetDatabase.LoadAssetAtPath<Material>(SplatLook);
            if (have != null) return have;
            Shader unlit = slab != null ? slab.shader : Shader.Find("Universal Render Pipeline/Unlit");   // the editor only: nothing at run time looks a shader up
            if (unlit == null) { did.Add("no unlit shader for " + SplatLook + ": the menu's paint leaves no splats"); return null; }
            var m = new Material(unlit) { name = "MenuSplat" };
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_SrcBlendAlpha", (float)UnityEngine.Rendering.BlendMode.One);
            m.SetFloat("_DstBlendAlpha", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f);
            m.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Off);
            m.SetFloat("_AlphaClip", 0f);
            m.SetFloat("_QueueOffset", SplatQueueOffset);     // the material inspector works the queue out again from this: it stays 2990
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.DisableKeyword("_ALPHATEST_ON");
            m.SetOverrideTag("RenderType", "Transparent");
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent + SplatQueueOffset;
            m.SetShaderPassEnabled("ShadowCaster", false);
            m.SetShaderPassEnabled("DepthOnly", false);
            m.SetShaderPassEnabled("MotionVectors", false);
            m.SetColor("_BaseColor", Color.white);
            AssetDatabase.CreateAsset(m, SplatLook);
            did.Add("made " + SplatLook + " (see-through, unlit, render queue " + m.renderQueue + ")");
            return m;
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
