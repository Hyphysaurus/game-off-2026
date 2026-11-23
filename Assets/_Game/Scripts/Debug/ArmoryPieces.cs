#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// Editor makers for the armory's looks bench (D-0167). Two menus:
    /// "Bake Parts List" copies what the game needs out of the weapons pack's own configs into the game's parts list
    /// (Resources/ArmoryCatalog). WHY a copy: the pack's config scripts exist only inside the editor and the pack is
    /// not in the public repo, so a built game cannot read them. The pack's assets are read as saved data, by the
    /// names of their saved fields, so this file never names a pack type and compiles without the pack.
    /// "Make Workbench Prefab" writes the bench piece (Prefabs/Pieces/Weapons/WeaponBench), once.
    /// Guns are not listed here: the bake writes the guns it finds inside the game's own weapon prefabs. The only
    /// thing named here is which KINDS of part the game can fit today (BakedTypes).
    /// </summary>
    public static class ArmoryPieces
    {
        private const string Pack = "Assets/_Imports/ithappy/Cartoon_Weapons";
        private const string Configs = Pack + "/Configs";
        private const string Presets = Pack + "/Prefabs/Weapon_Presets";
        private const string CatalogPath = "Assets/_Game/Resources/ArmoryCatalog.asset";
        private const string HeldWeapons = "Assets/_Game/Prefabs/Weapons";
        private const string BenchPath = "Assets/_Game/Prefabs/Pieces/Weapons/WeaponBench.prefab";
        private const string BenchModel = Pack + "/Prefabs/Containers/Container_01.prefab";
        private const float BenchSize = 2f;

        // The pack's saved field names, as they stand in its .asset files. A gun config's file is named
        // GunEntry_<gun>.asset. The pack's other saved fields (its bare gun prefab, its pictures) are never read: the
        // bare gun is a second prefab with the same name as the ready-made build, and pictures would only add size.
        private const string GunFilePrefix = "GunEntry_";
        private const string SlotsField = "<Slots>k__BackingField";
        private const string TypeField = "<AttachmentType>k__BackingField";
        private const string ElementsField = "<Elements>k__BackingField";
        private const string ElementSlotField = "SlotType";
        private const string ElementPrefabField = "Prefab";
        private const string MaterialField = "<Material>k__BackingField";
        private const string DisplayNameField = "<DisplayName>k__BackingField";

        /// <summary>One kind of part the bake writes: the pack's type name, the bench row's name, and the pack's own
        /// hook the part hangs from (empty: straight on the mount).</summary>
        private struct TypeSpec
        {
            public string Type, Label, Hook, HookMount;
        }

        /// <summary>The kinds of part the game can fit today (D-0167: looks only). The bake writes parts of these kinds and holds
        /// every other kind back, so their meshes stay out of the built game. Hook and HookMount are the pack's own rule
        /// (PreviewManager.cs:399-417).
        /// Working parts adds a row here, but a row alone is NOT enough. The game does not carry three of the pack's rules
        /// yet, and without them a new kind fails with no warning (the bench shows the part's name and the gun does not
        /// change, or two parts sit on one mount):
        /// (1) the slot Buttstock sits on mounts whose names end in Gunstock (the pack's alias, SlotTypeExtensions.cs:7-10),
        ///     and WeaponBuild.FindMount matches the slot's own name only;
        /// (2) the ready-made builds already carry a sight, a magazine, a muzzle and a stock under their mounts. The pack
        ///     empties every mount before it hangs anything (PreviewManager.cs:387-393); WeaponBuild.Fit only takes off
        ///     what it hung itself, so what the gun came with must be hidden first;
        /// (3) a pistol's flashlight goes on the mount BarBottom though its config says BarRight (PreviewManager.cs:343-351).
        /// And the gun must measure itself again after a swap (Weapon.LongGunFit keeps its first answer).</summary>
        private static readonly TypeSpec[] BakedTypes =
        {
            new TypeSpec { Type = "Charms", Label = "Charm", Hook = Pack + "/Prefabs/Charms/Charm_Hook_Main.prefab", HookMount = "Place_Charms" },
        };

        /// <summary>Writes Resources/ArmoryCatalog from the pack. Run again, it gives the same asset (same order,
        /// same ids); a label changed by hand and a part's Fit and Stats are kept. Batch:
        /// -executeMethod ShiftJam.ArmoryPieces.Bake</summary>
        [MenuItem("Shift Jam/Armory/Bake Parts List")]
        public static void Bake()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[Armory] Bake: stop Play first (the running game is reading the parts list); nothing was changed.");
                return;
            }
            if (!AssetDatabase.IsValidFolder(Configs))
            {
                Debug.LogError("[Armory] Bake: the weapons pack is not in the project (" + Configs + "); nothing was changed.");
                return;
            }
            WorkshopPieces.EnsureFolder("Assets/_Game/Resources");
            ArmoryCatalog catalog = WorkshopPieces.SettingsAsset<ArmoryCatalog>(CatalogPath);

            // What was there, by id: a label changed by hand is kept, and so are a part's Fit and Stats.
            var oldSkinLabels = new Dictionary<string, string>();
            var oldTypeLabels = new Dictionary<string, string>();
            var oldParts = new Dictionary<string, ArmoryCatalog.Part>();
            var oldGuns = new List<string>();
            if (catalog.skins != null) foreach (ArmoryCatalog.Skin row in catalog.skins) if (row != null && !string.IsNullOrEmpty(row.id)) oldSkinLabels[row.id] = row.label;
            if (catalog.types != null) foreach (ArmoryCatalog.PartType row in catalog.types) if (row != null && !string.IsNullOrEmpty(row.id)) oldTypeLabels[row.id] = row.label;
            if (catalog.parts != null) foreach (ArmoryCatalog.Part row in catalog.parts) if (row != null && !string.IsNullOrEmpty(row.id)) oldParts[row.id] = row;
            if (catalog.guns != null) foreach (ArmoryCatalog.Gun row in catalog.guns) if (row != null && !string.IsNullOrEmpty(row.id)) oldGuns.Add(row.id);

            int skipped = 0;   // files that would not load or lacked the field they were read for

            // The skins: every skin config with a material, in file order (the pack's order).
            var skins = new List<ArmoryCatalog.Skin>();
            foreach (string path in Files(Configs + "/Skins", ".asset"))
            {
                SerializedObject config = Read(path);
                var material = Reference(config != null ? config.FindProperty(MaterialField) : null) as Material;
                if (material == null) { skipped++; continue; }
                string id = Path.GetFileNameWithoutExtension(path);
                SerializedProperty shown = config.FindProperty(DisplayNameField);
                string label = shown != null && shown.propertyType == SerializedPropertyType.String ? shown.stringValue : "";
                if (string.IsNullOrEmpty(label)) label = id;
                skins.Add(new ArmoryCatalog.Skin { id = id, label = Kept(oldSkinLabels, id, label), material = material });
            }

            // The kinds of part: one bench row for each row of BakedTypes.
            var types = new List<ArmoryCatalog.PartType>();
            var baked = new HashSet<string>();
            foreach (TypeSpec spec in BakedTypes)
            {
                GameObject hook = null;
                if (!string.IsNullOrEmpty(spec.Hook))
                {
                    hook = AssetDatabase.LoadAssetAtPath<GameObject>(spec.Hook);
                    if (hook == null)
                    {
                        Debug.LogError("[Armory] Bake: the hook for " + spec.Type + " is missing (" + spec.Hook + "); that kind of part was left out.");
                        continue;
                    }
                }
                if (!baked.Add(spec.Type)) continue;
                types.Add(new ArmoryCatalog.PartType { id = spec.Type, label = Kept(oldTypeLabels, spec.Type, spec.Label), hook = hook, hookMount = spec.HookMount });
            }

            // The pack's gun configs, by gun name.
            var configs = new Dictionary<string, string>();
            foreach (string path in Files(Configs, ".asset"))
            {
                string file = Path.GetFileNameWithoutExtension(path);
                if (file.Length <= GunFilePrefix.Length || !file.StartsWith(GunFilePrefix, System.StringComparison.Ordinal)) continue;
                SerializedObject config = Read(path);
                SerializedProperty slots = config != null ? config.FindProperty(SlotsField) : null;
                if (slots == null || !slots.isArray) { skipped++; continue; }
                configs[file.Substring(GunFilePrefix.Length)] = path;
            }

            // The guns the game uses: a pack gun's name found on an object inside one of the game's own weapon prefabs.
            var used = new List<string>();
            foreach (string path in Files(HeldWeapons, ".prefab"))
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null || prefab.GetComponent<Weapon>() == null) continue;
                foreach (Transform node in prefab.GetComponentsInChildren<Transform>(true))
                    if (configs.ContainsKey(node.name) && !used.Contains(node.name)) used.Add(node.name);
            }
            used.Sort(string.CompareOrdinal);

            // The pack's ready-made builds, by file name, looked for under the presets folder only.
            var presets = new Dictionary<string, string>();
            foreach (string path in Files(Presets, ".prefab"))
            {
                string file = Path.GetFileNameWithoutExtension(path);
                if (!presets.ContainsKey(file)) presets.Add(file, path);
            }

            var parts = new List<ArmoryCatalog.Part>();
            var partById = new Dictionary<string, ArmoryCatalog.Part>();
            var guns = new List<ArmoryCatalog.Gun>();
            var heldBack = new List<string>();
            foreach (string id in used)
            {
                var gun = new ArmoryCatalog.Gun { id = id, skin = "" };
                if (presets.TryGetValue(id, out string presetPath)) gun.display = AssetDatabase.LoadAssetAtPath<GameObject>(presetPath);
                if (gun.display == null) Debug.LogWarning("[Armory] Bake: no ready-made build named " + id + ".prefab under " + Presets + "; the bench will show no copy of it.");
                else gun.skin = SkinOn(gun.display, skins);
                if (gun.display != null && gun.skin.Length == 0) Debug.LogWarning("[Armory] Bake: none of the skins is on " + id + "'s ready-made build; the bench cannot tell which skin it comes in.");

                var own = new List<string>();
                int held = 0;
                SerializedProperty slots = Read(configs[id]).FindProperty(SlotsField);
                for (int i = 0; i < slots.arraySize; i++)
                {
                    Object attachment = Reference(slots.GetArrayElementAtIndex(i));
                    if (attachment == null) continue;
                    var config = new SerializedObject(attachment);
                    if (!EnumName(config.FindProperty(TypeField), out string kind)) { skipped++; continue; }
                    if (!baked.Contains(kind)) { held++; continue; }   // a kind the game cannot fit yet: held back
                    string partId = attachment.name;
                    if (!partById.ContainsKey(partId))
                    {
                        ArmoryCatalog.Piece[] pieces = PiecesOf(config.FindProperty(ElementsField));
                        if (pieces.Length == 0) continue;
                        var part = new ArmoryCatalog.Part { id = partId, label = pieces[0].prefab.name.Replace('_', ' '), type = kind, pieces = pieces };
                        if (oldParts.TryGetValue(partId, out ArmoryCatalog.Part old))
                        {
                            if (!string.IsNullOrEmpty(old.label)) part.label = old.label;
                            part.fit = old.fit;
                            part.stats = old.stats;
                        }
                        partById.Add(partId, part);
                        parts.Add(part);
                    }
                    if (!own.Contains(partId)) own.Add(partId);
                }
                gun.parts = own.ToArray();
                guns.Add(gun);
                heldBack.Add(id + " " + held);
            }

            // Rows that were in the list and were not found this run are dropped, and named.
            var dropped = new List<string>();
            foreach (string id in oldSkinLabels.Keys) if (!skins.Exists(row => row.id == id)) dropped.Add("skin " + id);
            foreach (string id in oldTypeLabels.Keys) if (!baked.Contains(id)) dropped.Add("kind " + id);
            foreach (string id in oldParts.Keys) if (!partById.ContainsKey(id)) dropped.Add("part " + id);
            foreach (string id in oldGuns) if (!used.Contains(id)) dropped.Add("gun " + id);
            dropped.Sort(string.CompareOrdinal);

            // The four name fields at the top of the asset (mount prefix, skin mark, the two labels) are never touched.
            catalog.skins = skins;
            catalog.types = types;
            catalog.parts = parts;
            catalog.guns = guns;
            catalog.Forget();
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();

            var perKind = new List<string>();
            foreach (ArmoryCatalog.PartType type in types)
            {
                int count = 0;
                foreach (ArmoryCatalog.Part part in parts) if (part.type == type.id) count++;
                perKind.Add(type.id + ": " + count);
            }
            Debug.Log("[Armory] Baked " + skins.Count + " skins, " + parts.Count + " parts (" + string.Join(", ", perKind) + "), " + guns.Count + " guns (" + string.Join(", ", used) + ") into " + CatalogPath
                + ". Held back: other kinds of part per gun (" + string.Join(", ", heldBack) + "), " + (configs.Count - used.Count) + " pack guns the game does not use. "
                + skipped + " files skipped." + (dropped.Count > 0 ? " Dropped from the old list: " + string.Join(", ", dropped) + "." : ""));
        }

        /// <summary>Writes the workbench piece, once. A bench already there is left alone, so changes made to it by
        /// hand are kept. Made on the pattern of the supply crate (AmmoPieces.BuildCrate).</summary>
        [MenuItem("Shift Jam/Armory/Make Workbench Prefab")]
        public static void MakeBench()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(BenchPath) != null)
            {
                Debug.Log("[Armory] WeaponBench is already there and left alone (your changes to it are kept); delete it to have it made again: " + BenchPath);
                return;
            }
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[Armory] Make Workbench Prefab: stop Play first; nothing was made.");
                return;
            }
            GameObject pack = WorkshopPieces.Load(BenchModel);
            if (pack == null)
            {
                Debug.LogError("[Armory] WeaponBench was NOT made: the pack's model is missing (" + BenchModel + ").");
                return;
            }

            var root = new GameObject("WeaponBench");
            Transform model = new GameObject("Model").transform;
            model.SetParent(root.transform, false);
            GameObject inst = WorkshopPieces.Place(pack, model);
            Bounds b = WorkshopPieces.LocalBounds(model, inst);
            inst.transform.localPosition = new Vector3(-b.center.x, -b.min.y, -b.center.z);   // standing on the floor, centred
            model.localScale = Vector3.one * BenchSize;

            // ONE solid box, on the root, and no trigger anywhere. WHY no wide trigger: with an interactable on it, a
            // trigger wins E over every weapon stand beside it (a player inside a trigger is at distance 0 from it,
            // and in first person the centre ray stops at it). WHY on the root: the size shift bends the renderers
            // under a collider, so the whole bench bends with the room.
            var solid = root.AddComponent<BoxCollider>();
            solid.size = b.size * BenchSize;
            solid.center = Vector3.up * (b.size.y * BenchSize * 0.5f);

            // The copy of the gun turns above a toy's head (1.8 up), so the player's own body does not hide it.
            float displayHeight = Mathf.Max(2.0f, solid.size.y + 0.6f);
            Transform display = new GameObject("Display").transform;
            display.SetParent(root.transform, false);
            display.localPosition = Vector3.up * displayHeight;

            var sign = new GameObject("Sign", typeof(RectTransform));
            sign.transform.SetParent(root.transform, false);
            sign.transform.localPosition = Vector3.up * (displayHeight + 0.9f);
            var text = sign.AddComponent<TMPro.TextMeshPro>();
            text.text = "WORKBENCH";
            text.fontSize = 4f;
            text.alignment = TMPro.TextAlignmentOptions.Center;
            text.fontStyle = TMPro.FontStyles.Bold;
            text.textWrappingMode = TMPro.TextWrappingModes.NoWrap;   // a longer word than the crate's sign: one line
            text.rectTransform.sizeDelta = new Vector2(6f, 0.6f);
            var turn = sign.AddComponent<Billboard>();
            WorkshopPieces.Set(turn, "spin", 0f);
            WorkshopPieces.Set(turn, "breathe", 0f);

            var bench = root.AddComponent<ArmoryBench>();
            WorkshopPieces.Set(bench, "displayPoint", display);

            Vector3 body = solid.size;   // read before the save: it clears the object away
            WorkshopPieces.EnsureFolder(BenchPath.Substring(0, BenchPath.LastIndexOf('/')));
            WorkshopPieces.Save(root, BenchPath);
            AssetDatabase.SaveAssets();
            Debug.Log("[Armory] WeaponBench was made at " + BenchPath + ": the pack's Container_01 at " + BenchSize + " times its size = "
                + body.x.ToString("F2") + " x " + body.y.ToString("F2") + " x " + body.z.ToString("F2") + " (one solid box on the root); the gun's copy turns "
                + displayHeight.ToString("F2") + " up, the sign " + (displayHeight + 0.9f).ToString("F2") + " up.");
        }

        // ---------- reading the pack ----------

        /// <summary>Every file with this ending under a folder, with forward slashes, in a fixed order (so two bakes
        /// give the same list).</summary>
        private static List<string> Files(string folder, string ending)
        {
            var found = new List<string>();
            if (!Directory.Exists(folder)) return found;
            foreach (string file in Directory.GetFiles(folder, "*" + ending, SearchOption.AllDirectories))
                if (file.EndsWith(ending, System.StringComparison.OrdinalIgnoreCase)) found.Add(file.Replace('\\', '/'));
            found.Sort(string.CompareOrdinal);
            return found;
        }

        /// <summary>An asset opened as saved data, or null when it will not load.</summary>
        private static SerializedObject Read(string path)
        {
            Object asset = AssetDatabase.LoadMainAssetAtPath(path);
            return asset != null ? new SerializedObject(asset) : null;
        }

        /// <summary>What a saved reference points at, or null (no such field, not a reference, or empty).</summary>
        private static Object Reference(SerializedProperty p)
        {
            return p != null && p.propertyType == SerializedPropertyType.ObjectReference ? p.objectReferenceValue : null;
        }

        /// <summary>A saved enum read as its NAME (the game keeps names, never the pack's numbers). False when there
        /// is no such field or its value is not one of the names.</summary>
        private static bool EnumName(SerializedProperty p, out string name)
        {
            name = null;
            if (p == null || p.propertyType != SerializedPropertyType.Enum) return false;
            string[] names = p.enumNames;
            int at = p.enumValueIndex;
            if (names == null || at < 0 || at >= names.Length) return false;
            name = names[at];
            return !string.IsNullOrEmpty(name);
        }

        /// <summary>What an attachment hangs on the gun and where: its elements with both a slot and a prefab.</summary>
        private static ArmoryCatalog.Piece[] PiecesOf(SerializedProperty elements)
        {
            var pieces = new List<ArmoryCatalog.Piece>();
            if (elements == null || !elements.isArray) return pieces.ToArray();
            for (int i = 0; i < elements.arraySize; i++)
            {
                SerializedProperty element = elements.GetArrayElementAtIndex(i);
                var prefab = Reference(element.FindPropertyRelative(ElementPrefabField)) as GameObject;
                if (prefab == null) continue;
                if (!EnumName(element.FindPropertyRelative(ElementSlotField), out string slot)) continue;
                pieces.Add(new ArmoryCatalog.Piece { slot = slot, prefab = prefab });
            }
            return pieces.ToArray();
        }

        /// <summary>The id of the skin a ready-made build comes in: the first of the skins whose material is on one
        /// of its renderers (in the order they sit in the prefab, switched off or not). "" when none is.</summary>
        private static string SkinOn(GameObject display, List<ArmoryCatalog.Skin> skins)
        {
            foreach (Renderer part in display.GetComponentsInChildren<Renderer>(true))
                foreach (Material look in part.sharedMaterials)
                {
                    if (look == null) continue;
                    foreach (ArmoryCatalog.Skin skin in skins) if (skin.material == look) return skin.id;
                }
            return "";
        }

        /// <summary>The label a row had before, when it had one (a label changed by hand is kept); else the new one.</summary>
        private static string Kept(Dictionary<string, string> old, string id, string fresh)
        {
            return old.TryGetValue(id, out string label) && !string.IsNullOrEmpty(label) ? label : fresh;
        }
    }
}
#endif
