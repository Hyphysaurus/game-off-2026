using System;
using UnityEngine;
using UnityEngine.UI;

namespace ShiftJam
{
    /// <summary>
    /// White, anti-aliased sprites drawn once in code and tinted by the images that use them (the HUD style, D-0162): a
    /// rounded square for 9-slicing and a few icon glyphs. Shared by every HUD so the panels match; no texture assets.
    /// </summary>
    public static class HudShapes
    {
        public const int RoundRadius = 32;   // corner radius of the 9-slice sprite, pixels
        private static Sprite _rounded, _check, _cross, _ring;

        /// <summary>A white rounded square for 9-slicing: corners of RoundRadius pixels around 2 pixels of straight middle.</summary>
        public static Sprite Rounded
        {
            get
            {
                if (_rounded != null) return _rounded;
                int r = RoundRadius, size = r * 2 + 2;
                float c = size * 0.5f, straight = c - r;
                _rounded = Bake("Rounded", size, (x, y) =>
                {
                    float qx = Mathf.Abs(x - c) - straight, qy = Mathf.Abs(y - c) - straight;
                    float outside = new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude + Mathf.Min(Mathf.Max(qx, qy), 0f) - r;
                    return 0.5f - outside;
                }, new Vector4(r, r, r, r));
                return _rounded;
            }
        }

        /// <summary>A tick mark.</summary>
        public static Sprite Check => _check != null ? _check : _check = Bake("Check", 64, (x, y) =>
            Stroke(Mathf.Min(Segment(x, y, 14f, 33f, 27f, 19f), Segment(x, y, 27f, 19f, 51f, 46f)), 5.5f), Vector4.zero);

        /// <summary>An X.</summary>
        public static Sprite Cross => _cross != null ? _cross : _cross = Bake("Cross", 64, (x, y) =>
            Stroke(Mathf.Min(Segment(x, y, 17f, 17f, 47f, 47f), Segment(x, y, 17f, 47f, 47f, 17f)), 6f), Vector4.zero);

        /// <summary>A ring with a dot: a core.</summary>
        public static Sprite Ring => _ring != null ? _ring : _ring = Bake("Ring", 64, (x, y) =>
        {
            float d = new Vector2(x - 32f, y - 32f).magnitude;
            return Mathf.Max(Stroke(Mathf.Abs(d - 20f), 4.5f), Mathf.Clamp01(7f - d + 0.5f));
        }, Vector4.zero);

        /// <summary>An image showing the rounded sprite at <paramref name="radius"/> units, sliced so the corners keep their shape at any size.</summary>
        public static Image RoundedImage(RectTransform rt, Color color, float radius)
        {
            var img = rt.gameObject.AddComponent<Image>();
            img.raycastTarget = false;
            img.sprite = Rounded; img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = RoundRadius / Mathf.Max(1f, radius);
            img.color = color;
            return img;
        }

        private static float Stroke(float distance, float halfWidth) => Mathf.Clamp01(halfWidth - distance + 0.5f);

        private static float Segment(float x, float y, float ax, float ay, float bx, float by)
        {
            var p = new Vector2(x, y); var a = new Vector2(ax, ay); var ab = new Vector2(bx - ax, by - ay);
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
            return Vector2.Distance(p, a + ab * t);
        }

        private static Sprite Bake(string name, int size, Func<float, float, float> alpha, Vector4 border)
        {
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    px[y * size + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(alpha(x + 0.5f, y + 0.5f)) * 255f + 0.5f));
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "HudShapes " + name, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.DontSave };
            tex.SetPixels32(px);
            tex.Apply(false, true);
            var sprite = Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
            sprite.name = tex.name; sprite.hideFlags = HideFlags.DontSave;
            return sprite;
        }
    }
}
