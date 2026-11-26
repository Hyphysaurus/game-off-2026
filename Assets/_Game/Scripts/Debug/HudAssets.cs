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
    /// The pixel HUD's font (D-0243): Pixel Operator 8 and its bold (CC0, Art/Fonts) become two BITMAP font assets in
    /// Resources/HUD ("Pixel Operator 8", "Pixel Operator 8 Bold"): printable ASCII drawn at 8 pixels, the font's own grid,
    /// hard pixels (raster, hinted), on a 256 square atlas sampled Point, so at 16, 24 and 32 each of its pixels is a whole
    /// 2, 3 or 4 screen pixels at 1080 high. The bold is the regular's bold weight (a &lt;b&gt; uses it). Then
    /// Resources/PixelKit (the pixel look's switch, PixelKit) is made, or refreshed, pointing at both; its switch is kept as
    /// he left it.
    /// </summary>
    public static class HudAssets
    {
        private const string FontFolder = "Assets/_Game/Art/Fonts", OutFolder = "Assets/_Game/Resources/HUD";
        private const string IconPath = OutFolder + "/keyboard_shift.png";
        private const int SamplingSize = 90, Padding = 9, AtlasSize = 1024;
        private static readonly string[] Sources = { "Teko-Bold.ttf", "Fredoka-SemiBold.ttf" };
        private static readonly string[] Names = { "Teko SDF", "Fredoka SDF" };   // DrivingHud loads HUD/<name>
        // The pixel font (D-0243): drawn at its own 8-pixel grid, a small padding (hard pixels need little), a small atlas.
        private const string PixelRegular = "PixelOperator8.ttf", PixelBold = "PixelOperator8-Bold.ttf";
        private const int PixelSampling = 8, PixelPadding = 2, PixelAtlas = 256;
        private const string KitPath = "Assets/_Game/Resources/" + PixelKit.ResourcePath + ".asset";

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
            // The pixel font: the bold first (the regular names it as its bold weight), then the kit's asset.
            TMP_FontAsset bold = MakePixelFont(FontFolder + "/" + PixelBold, OutFolder + "/" + PixelKit.BoldFontName + ".asset", PixelKit.BoldFontName, null);
            TMP_FontAsset regular = MakePixelFont(FontFolder + "/" + PixelRegular, OutFolder + "/" + PixelKit.FontName + ".asset", PixelKit.FontName, bold);
            string kit = MakeKit(regular, bold);
            AssetDatabase.SaveAssets();
            Debug.Log("[HUD] " + made + " of " + Sources.Length + " fonts -> " + OutFolder
                + (icon ? "; Shift key icon set to a UI sprite." : "; no " + IconPath + " (the HUD draws a plain key instead).")
                + "\n[HUD] Pixel font (D-0243): " + (regular != null && bold != null ? "both made" : "NOT made (see the error above): the pixel look stays off") + ". " + kit);
        }

        /// <summary>A bitmap font asset of <paramref name="ttfPath"/>: printable ASCII at the font's own 8-pixel grid, hard
        /// pixels, a static Point-sampled atlas. With <paramref name="bold"/>, that asset is its bold weight; without, it is
        /// its own (so a &lt;b&gt; inside bold words changes nothing).</summary>
        private static TMP_FontAsset MakePixelFont(string ttfPath, string assetPath, string assetName, TMP_FontAsset bold)
        {
            var font = AssetDatabase.LoadAssetAtPath<Font>(ttfPath);
            if (font == null) { Debug.LogError("[HUD] Missing " + ttfPath + ": copy the Pixel Operator TTFs into " + FontFolder + " first."); return null; }
            var asset = TMP_FontAsset.CreateFontAsset(font, PixelSampling, PixelPadding, GlyphRenderMode.RASTER_HINTED, PixelAtlas, PixelAtlas, AtlasPopulationMode.Dynamic, true);
            if (asset == null) { Debug.LogError("[HUD] Could not read " + ttfPath + " (its import setting Include Font Data must be on)."); return null; }
            asset.name = assetName;

            string missing;
            bool all = asset.TryAddCharacters(Printable(), out missing, true);
            if (!all && !string.IsNullOrEmpty(missing)) Debug.LogWarning("[HUD] " + assetName + " has no glyph for: " + missing);
            asset.atlasPopulationMode = AtlasPopulationMode.Static;   // fixed from here: the TTF is not needed at run time

            if (AssetDatabase.LoadAssetAtPath<Object>(assetPath) != null) AssetDatabase.DeleteAsset(assetPath);
            AssetDatabase.CreateAsset(asset, assetPath);

            var atlases = asset.atlasTextures;
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
                atlases[i].filterMode = FilterMode.Point;   // hard pixels: never blurred between the font's pixels
                atlases[i].wrapMode = TextureWrapMode.Clamp;
                AssetDatabase.AddObjectToAsset(atlases[i], asset);
            }
            asset.material.name = assetName + " Material";
            AssetDatabase.AddObjectToAsset(asset.material, asset);
            // The bold weight: the bold face for the regular, itself for the bold (no made-up thickening of hard pixels).
            TMP_FontWeightPair[] weights = asset.fontWeightTable;
            if (weights != null && weights.Length > 7) weights[7].regularTypeface = bold != null ? bold : asset;
            EditorUtility.SetDirty(asset);
            Debug.Log("[HUD] " + assetPath + ": " + asset.characterTable.Count + " characters, bitmap at " + PixelSampling + " px, Point.");
            return asset;
        }

        /// <summary>Makes Resources/PixelKit, or points the one there at the new fonts (his Pixel On and the rest are kept).</summary>
        private static string MakeKit(TMP_FontAsset regular, TMP_FontAsset bold)
        {
            var kit = AssetDatabase.LoadAssetAtPath<PixelKit>(KitPath);
            bool made = kit == null;
            if (made)
            {
                kit = ScriptableObject.CreateInstance<PixelKit>();
                AssetDatabase.CreateAsset(kit, KitPath);
            }
            kit.font = regular;
            kit.boldFont = bold;
            EditorUtility.SetDirty(kit);
            return (made ? "MADE " : "Refreshed ") + KitPath + ": Pixel On " + (kit.pixelOn ? "ticked (the pixel HUD shows)" : "UNTICKED (the old HUD shows)")
                + ", Goal Line " + (kit.goalLine ? "ticked" : "unticked") + ", Bar Cells " + kit.barCells + ".";
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
