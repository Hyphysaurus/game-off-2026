#if UNITY_EDITOR
using System.Text;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace ShiftJam
{
    /// <summary>
    /// Shift Jam > UI > Make HUD Fonts: turns the two OFL fonts in Art/Fonts (Teko for numbers, Fredoka for labels) into
    /// TextMeshPro font assets in Resources/HUD, where DrivingHud loads them. Each is a static atlas of printable ASCII
    /// (SDF, sampled at 90, 1024 square), so nothing grows or changes on disk while playing and the TTFs stay out of
    /// builds. Running it again rebuilds both (new GUIDs; only DrivingHud uses them, by path). Also sets the Shift key icon
    /// in Resources/HUD to a crisp UI sprite (no mipmaps, uncompressed).
    /// </summary>
    public static class HudAssets
    {
        private const string FontFolder = "Assets/_Game/Art/Fonts", OutFolder = "Assets/_Game/Resources/HUD";
        private const string IconPath = OutFolder + "/keyboard_shift.png";
        private const int SamplingSize = 90, Padding = 9, AtlasSize = 1024;
        private static readonly string[] Sources = { "Teko-Bold.ttf", "Fredoka-SemiBold.ttf" };
        private static readonly string[] Names = { "Teko SDF", "Fredoka SDF" };   // DrivingHud loads HUD/<name>

        [MenuItem("Shift Jam/UI/Make HUD Fonts")]
        public static void Make()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogWarning("[HUD] Stop Play first: this writes font assets to disk."); return; }
            if (TMP_Settings.instance == null) { Debug.LogError("[HUD] TMP Settings not found: import the TextMesh Pro essentials first."); return; }
            EnsureFolder(OutFolder);
            int made = 0;
            for (int i = 0; i < Sources.Length; i++)
                if (MakeFont(FontFolder + "/" + Sources[i], OutFolder + "/" + Names[i] + ".asset", Names[i])) made++;
            bool icon = MakeIconSprite();
            AssetDatabase.SaveAssets();
            Debug.Log("[HUD] " + made + " of " + Sources.Length + " fonts -> " + OutFolder
                + (icon ? "; Shift key icon set to a UI sprite." : "; no " + IconPath + " (the HUD draws a plain key instead)."));
        }

        private static bool MakeFont(string ttfPath, string assetPath, string assetName)
        {
            var font = AssetDatabase.LoadAssetAtPath<Font>(ttfPath);
            if (font == null) { Debug.LogError("[HUD] Missing " + ttfPath + ": put the HUD fonts in " + FontFolder + " first."); return false; }
            var asset = TMP_FontAsset.CreateFontAsset(font, SamplingSize, Padding, GlyphRenderMode.SDFAA, AtlasSize, AtlasSize, AtlasPopulationMode.Dynamic, true);
            if (asset == null) { Debug.LogError("[HUD] Could not read " + ttfPath + " (its import setting Include Font Data must be on)."); return false; }
            asset.name = assetName;

            string missing;
            bool all = asset.TryAddCharacters(Printable(), out missing, true);
            if (!all && !string.IsNullOrEmpty(missing)) Debug.LogWarning("[HUD] " + assetName + " has no glyph for: " + missing);
            asset.atlasPopulationMode = AtlasPopulationMode.Static;   // fixed from here: the TTF is not needed at run time

            if (AssetDatabase.LoadAssetAtPath<Object>(assetPath) != null) AssetDatabase.DeleteAsset(assetPath);
            AssetDatabase.CreateAsset(asset, assetPath);

            var atlases = asset.atlasTextures;   // multi-atlas growth doubles the array: keep only the used ones
            int used = Mathf.Clamp(asset.atlasTextureCount, 1, atlases.Length);
            if (used != atlases.Length)
            {
                var kept = new Texture2D[used];
                System.Array.Copy(atlases, kept, used);
                asset.atlasTextures = atlases = kept;
            }
            for (int i = 0; i < atlases.Length; i++)
            {
                if (atlases[i] == null) continue;
                atlases[i].name = assetName + " Atlas" + (i > 0 ? " " + i : "");
                AssetDatabase.AddObjectToAsset(atlases[i], asset);
            }
            asset.material.name = assetName + " Material";
            AssetDatabase.AddObjectToAsset(asset.material, asset);
            EditorUtility.SetDirty(asset);
            Debug.Log("[HUD] " + assetPath + ": " + asset.characterTable.Count + " characters in " + atlases.Length + " atlas" + (atlases.Length > 1 ? "es." : "."));
            return true;
        }

        private static bool MakeIconSprite()
        {
            var importer = AssetImporter.GetAtPath(IconPath) as TextureImporter;
            if (importer == null) return false;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
            return true;
        }

        private static string Printable()
        {
            var sb = new StringBuilder(95);
            for (char c = ' '; c <= '~'; c++) sb.Append(c);
            return sb.ToString();
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int cut = path.LastIndexOf('/');
            string parent = path.Substring(0, cut);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, path.Substring(cut + 1));
        }
    }
}
#endif
