#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// Lands the menu icons and the button-prompt glyphs (D-0226), and the pixel HUD's icons and key glyphs (D-0243, the
    /// same folders, the same asset: the HUD ids and Keys are filled the same way). Menu: Shift Jam > UI > Land Menu Icons (D-0226).
    /// (1) Sets every PNG in Art/UI/Icons (his pixel icons: Point filter) and Art/UI/Prompts (the Kenney glyphs: Bilinear,
    /// they are smooth drawings shown at half their size) to: Sprite, no compression, no mipmaps, alpha is transparency,
    /// clamped. (2) Makes Resources/IconSet, or refreshes it: an id the defaults have and the asset lacks is added; an
    /// empty sprite or glyph is filled from its file; a sprite he has set is never changed. (3) Reports every icon id
    /// with its file and where it shows, and every glyph. The report's first line says whether anything was missing;
    /// one that starts "[Icons] NOT" changed nothing.
    /// </summary>
    public static class IconsBuilder
    {
        public const string Tag = "[Icons]";
        public const string IconsFolder = "Assets/_Game/Art/UI/Icons";
        public const string PromptsFolder = "Assets/_Game/Art/UI/Prompts";
        public const string AssetPath = "Assets/_Game/Resources/IconSet.asset";

        /// <summary>The icons the menus use: id, file name in Art/UI/Icons, where it shows.</summary>
        private static readonly (string id, string file, string where)[] Defaults =
        {
            (IconSet.Workshop, "icon_workshop.png", "title: Playground page, Workshop (old title: its button when the word leaves room)"),
            (IconSet.Raceway, "icon_raceway.png", "title: Playground page, Raceway (old title: as Workshop)"),
            (IconSet.Colours, "icon_colours.png", "title: Playground page, Colours (old title: as Workshop)"),
            (IconSet.Online, "icon_online.png", "title: main page, Online (old title: as Workshop)"),
            (IconSet.Parties, "icon_parties.png", "title: Online page, Parties (old title: as Workshop)"),
            (IconSet.Options, "icon_options.png", "title: main page, Options; pause: Options (both menus)"),
            (IconSet.Quit, "icon_quit.png", "title: main page, Quit; pause: Quit (both menus)"),
            (IconSet.Resume, "icon_resume.png", "pause: Resume (both menus)"),
            (IconSet.Title, "icon_title.png", "pause: Back to Title (both menus)"),
            (IconSet.LeaveParty, "icon_leave-party.png", "pause: Leave Party (online; both menus)"),
            (IconSet.StickerChart, "icon_sticker-chart.png", "pause: Sticker Chart (both menus); the Sticker Chart's heading"),
            (IconSet.Playground, "icon_playground.png", "title: main page, Playground"),
            (IconSet.QuickPlay, "icon_quick-play.png", "title: Online page, Quick Play"),
            (IconSet.Volume, "icon_volume.png", "nowhere yet (kept for the Options page's Volume)"),
            // The pixel HUD's (D-0243), shown only while PixelKit > Pixel On is ticked.
            (IconSet.Heart, "icon_heart.png", "HUD: the status panel's health row"),
            (IconSet.Shift, "icon_shift.png", "HUD: the status panel's shift row; a core toast"),
            (IconSet.Torch, "icon_torch.png", "nowhere yet (kept for the flashlight)"),
            (IconSet.Gun, "icon_gun.png", "HUD: the ammo panel, left of the count"),
            (IconSet.BatteryHigh, "icon_battery-high.png", "nowhere yet (the flashlight has no battery to show)"),
            (IconSet.BatteryLow, "icon_battery-low.png", "nowhere yet (the flashlight has no battery to show)"),
            (IconSet.Star, "icon_star.png", "HUD: a reward toast; the Sticker Chart's stars earned and its total"),
            (IconSet.StarLocked, "icon_star-locked.png", "HUD: the Sticker Chart's stars not earned yet"),
            (IconSet.Ghost, "icon_ghost.png", "nowhere yet (a warning toast has a pink \"!\" badge)"),
            (IconSet.Lock, "icon_lock.png", "HUD: a refused toast"),
            (IconSet.Bulb, "icon_bulb.png", "nowhere yet"),
            (IconSet.Hand, "icon_hand.png", "nowhere yet"),
            (IconSet.Trophy, "icon_trophy.png", "HUD: the claw machine panel's heading"),
            (IconSet.Box, "icon_box.png", "nowhere yet"),
            (IconSet.Door, "icon_door.png", "nowhere yet"),
            (IconSet.Sticker, "icon_sticker.png", "HUD: the objective card's star count"),
        };

        /// <summary>The key glyphs the HUD's prompts show (D-0243): the names the game's words use, the file in Art/UI/Prompts.</summary>
        private static readonly (string names, string file)[] KeyDefaults =
        {
            ("e", "prompt_key_e.png"), ("f", "prompt_key_f.png"), ("r", "prompt_key_r.png"), ("x", "prompt_key_x.png"),
            ("c", "prompt_key_c.png"), ("q", "prompt_key_q.png"), ("g", "prompt_key_g.png"), ("tab", "prompt_key_tab.png"),
            ("esc,escape", "prompt_key_esc.png"), ("space,spacebar", "prompt_key_space.png"), ("shift", "prompt_key_shift.png"),
            ("enter,return", "prompt_key_enter.png"), ("lmb,click,left click", "prompt_key_lmb.png"), ("rmb,right click", "prompt_key_rmb.png"),
            ("wheel,scroll", "prompt_key_wheel.png"),
        };

        [MenuItem("Shift Jam/UI/Land Menu Icons (D-0226)")]
        public static void LandMenu()
        {
            string report = Land();
            if (report.StartsWith(Tag + " NOT")) Debug.LogWarning(report); else Debug.Log(report);
        }

        /// <summary>Sets the PNGs' import, makes or refreshes Resources/IconSet, and returns the report (one fact a line).</summary>
        public static string Land()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return Tag + " NOT landed: this does not run in Play. Stop the game first. Nothing was changed.";
            if (!AssetDatabase.IsValidFolder(IconsFolder) || !AssetDatabase.IsValidFolder(PromptsFolder))
                return Tag + " NOT landed: copy the PNGs first (" + IconsFolder + " and " + PromptsFolder + " must exist). Nothing was changed.";

            var log = new StringBuilder();
            var body = new StringBuilder();
            int icons = Import(IconsFolder, FilterMode.Point, body);
            int prompts = Import(PromptsFolder, FilterMode.Bilinear, body);

            IconSet set = AssetDatabase.LoadAssetAtPath<IconSet>(AssetPath);
            bool made = set == null;
            if (made)
            {
                set = ScriptableObject.CreateInstance<IconSet>();
                AssetDatabase.CreateAsset(set, AssetPath);
            }
            var filled = new List<string>();
            var list = new List<IconSet.Icon>(set.icons ?? new IconSet.Icon[0]);
            foreach ((string id, string file, string where) in Defaults)
            {
                int at = list.FindIndex(i => i.id == id);
                if (at < 0) { list.Add(new IconSet.Icon { id = id }); at = list.Count - 1; filled.Add(id + " (added)"); }
                if (list[at].sprite != null) continue;
                Sprite s = LoadSprite(IconsFolder + "/" + file);
                if (s == null) { body.AppendLine($"  MISSING FILE: {IconsFolder}/{file} (icon '{id}' stays empty)"); continue; }
                list[at] = new IconSet.Icon { id = id, sprite = s };
                filled.Add(id);
            }
            set.icons = list.ToArray();
            Glyph(ref set.keyboard.move, "prompt_kb_move.png", "keyboard move", filled, body);
            Glyph(ref set.keyboard.select, "prompt_kb_select.png", "keyboard select", filled, body);
            Glyph(ref set.keyboard.back, "prompt_kb_back.png", "keyboard back", filled, body);
            Glyph(ref set.xbox.move, "prompt_xbox_move.png", "xbox move", filled, body);
            Glyph(ref set.xbox.select, "prompt_xbox_select.png", "xbox select", filled, body);
            Glyph(ref set.xbox.back, "prompt_xbox_back.png", "xbox back", filled, body);
            Glyph(ref set.playstation.move, "prompt_ps_move.png", "playstation move", filled, body);
            Glyph(ref set.playstation.select, "prompt_ps_select.png", "playstation select", filled, body);
            Glyph(ref set.playstation.back, "prompt_ps_back.png", "playstation back", filled, body);
            // The HUD's key glyphs (D-0243): a key the asset lacks is added; an empty glyph is filled; one he set is kept.
            var keys = new List<IconSet.Key>(set.keys ?? new IconSet.Key[0]);
            foreach ((string names, string file) in KeyDefaults)
            {
                int at = keys.FindIndex(k => k.names == names);
                if (at < 0) { keys.Add(new IconSet.Key { names = names }); at = keys.Count - 1; filled.Add("key " + names + " (added)"); }
                if (keys[at].sprite != null) continue;
                Sprite s = LoadSprite(PromptsFolder + "/" + file);
                if (s == null) { body.AppendLine($"  MISSING FILE: {PromptsFolder}/{file} (key '{names}' shows as a key cap)"); continue; }
                keys[at] = new IconSet.Key { names = names, sprite = s };
                filled.Add("key " + names);
            }
            set.keys = keys.ToArray();
            EditorUtility.SetDirty(set);
            AssetDatabase.SaveAssets();

            log.AppendLine($"{Tag} Land Menu Icons (D-0226): {(made ? "MADE " + AssetPath : AssetPath + " kept as he left it")}; {(filled.Count > 0 ? "filled: " + string.Join(", ", filled) : "nothing was empty")}.");
            log.AppendLine($"IMPORT: {icons} icon PNGs in {IconsFolder} (Point), {prompts} glyph PNGs in {PromptsFolder} (Bilinear): Sprite, no compression, no mipmaps, alpha is transparency.");
            log.Append(body);
            log.AppendLine($"SWITCHES: Icons On {(set.iconsOn ? "ticked" : "UNTICKED: no icons show")}, Footer On {(set.footerOn ? "ticked" : "UNTICKED: no footer shows")}.");
            foreach (IconSet.Icon icon in set.icons)
            {
                string where = "not used by any menu (an id the menus do not ask for)";
                foreach ((string id, string file, string w) in Defaults) if (id == icon.id) { where = w; break; }
                string path = icon.sprite != null ? AssetDatabase.GetAssetPath(icon.sprite) : "EMPTY";
                string size = icon.sprite != null ? $" {icon.sprite.rect.width:0}x{icon.sprite.rect.height:0}" : "";
                log.AppendLine($"ICON '{icon.id}': {path}{size} -> {where}");
            }
            Line(log, "keyboard", set.keyboard);
            Line(log, "xbox", set.xbox);
            Line(log, "playstation", set.playstation);
            log.AppendLine($"FOOTER words: \"{set.moveWords}\" / \"{set.selectWords}\" / \"{set.backWords}\"{(set.capitals ? " (in capitals)" : "")}, glyphs {set.glyphSize:0} px, {set.belowHint:0} px under the hint line on the model menus.");
            foreach (IconSet.Key key in set.keys) log.AppendLine($"KEY '{key.names}': {Name(key.sprite)}");
            return log.ToString().TrimEnd();
        }

        /// <summary>Sets every PNG in a folder (not its subfolders) to the menus' sprite import; returns how many.</summary>
        private static int Import(string folder, FilterMode filter, StringBuilder body)
        {
            int count = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { folder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.EndsWith(".png") || System.IO.Path.GetDirectoryName(path).Replace('\\', '/') != folder) continue;
                if (!(AssetImporter.GetAtPath(path) is TextureImporter ti)) continue;
                count++;
                bool changed = ti.textureType != TextureImporterType.Sprite || ti.spriteImportMode != SpriteImportMode.Single || ti.filterMode != filter
                    || ti.textureCompression != TextureImporterCompression.Uncompressed || ti.mipmapEnabled || !ti.alphaIsTransparency
                    || ti.wrapMode != TextureWrapMode.Clamp || !ti.sRGBTexture || ti.npotScale != TextureImporterNPOTScale.None;
                if (!changed) continue;
                ti.textureType = TextureImporterType.Sprite;
                ti.spriteImportMode = SpriteImportMode.Single;
                ti.filterMode = filter;
                ti.textureCompression = TextureImporterCompression.Uncompressed;
                ti.mipmapEnabled = false;
                ti.alphaIsTransparency = true;
                ti.alphaSource = TextureImporterAlphaSource.FromInput;
                ti.wrapMode = TextureWrapMode.Clamp;
                ti.sRGBTexture = true;
                ti.npotScale = TextureImporterNPOTScale.None;
                ti.SaveAndReimport();
                body.AppendLine($"  imported: {path}");
            }
            return count;
        }

        private static Sprite LoadSprite(string path) => AssetDatabase.LoadAssetAtPath<Sprite>(path);

        private static void Glyph(ref Sprite slot, string file, string name, List<string> filled, StringBuilder body)
        {
            if (slot != null) return;
            Sprite s = LoadSprite(PromptsFolder + "/" + file);
            if (s == null) { body.AppendLine($"  MISSING FILE: {PromptsFolder}/{file} (glyph '{name}' stays empty)"); return; }
            slot = s;
            filled.Add(name);
        }

        private static void Line(StringBuilder log, string device, IconSet.Glyphs g)
            => log.AppendLine($"GLYPHS {device}: move {Name(g.move)}, select {Name(g.select)}, back {Name(g.back)}");

        private static string Name(Sprite s) => s != null ? AssetDatabase.GetAssetPath(s) : "EMPTY";
    }
}
#endif
