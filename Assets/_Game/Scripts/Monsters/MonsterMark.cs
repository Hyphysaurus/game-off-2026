using TMPro;
using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// The "!" that pops over a monster's head when it spots the toy (D-0228, as a guard in a stealth game does): the HUD's
    /// font with a dark outline, always turned to the camera; it pops up past its size and settles, holds a moment, then
    /// shrinks away and switches itself off. Its text, colour, size, gap and timings are on Resources/MonsterSettings
    /// (shares of the monster's height, so a bigger monster wears a bigger one). The monster (DarkMonster) makes it the
    /// first time it is wanted and calls <see cref="Pop"/>; made at run time, nothing in a scene, nothing allocated per frame.
    /// </summary>
    public sealed class MonsterMark : MonoBehaviour
    {
        private const string FontPath = "HUD/Fredoka SDF";
        private const float FontSize = 10f, OutlineWidth = 0.25f;
        private static readonly Color Ink = new Color32(0x1f, 0x1b, 0x2e, 0xff);   // the stickers' outline, as the step mark's

        private TextMeshPro _text;
        private Collider _body;
        private MonsterSettings _s;
        private float _poppedAt, _markLocal = 1f;
        private string _fitText;

        /// <summary>A mark over <paramref name="over"/>, standing on top of <paramref name="body"/> (its capsule). Switched off
        /// until the first <see cref="Pop"/>.</summary>
        public static MonsterMark Make(Transform over, Collider body)
        {
            var go = new GameObject("SpotMark (D-0228)");
            go.transform.SetParent(over, false);
            var mark = go.AddComponent<MonsterMark>();
            mark._body = body;
            mark._s = MonsterSettings.Load();
            mark.Build();
            go.SetActive(false);
            return mark;
        }

        private void Build()
        {
            _text = gameObject.AddComponent<TextMeshPro>();
            var font = Resources.Load<TMP_FontAsset>(FontPath);
            if (font != null) _text.font = font;
            _text.text = _s.markText;
            _text.fontSize = FontSize;
            _text.fontStyle = FontStyles.Bold;
            _text.alignment = TextAlignmentOptions.Center;
            _text.textWrappingMode = TextWrappingModes.NoWrap;
            _text.outlineWidth = OutlineWidth;   // its own material copy, made once
            _text.outlineColor = Ink;
            _text.color = _s.markColour;
            if (TryGetComponent(out MeshRenderer drawn))
            {
                drawn.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                drawn.receiveShadows = false;
            }
        }

        /// <summary>Pops it up now (again from the start if it is showing).</summary>
        public void Pop()
        {
            _poppedAt = Time.time;
            if (!gameObject.activeSelf) gameObject.SetActive(true);
            Place();
        }

        /// <summary>Gone at once (the monster was knocked down, frozen or put back).</summary>
        public void Hide()
        {
            if (gameObject.activeSelf) gameObject.SetActive(false);
        }

        private void LateUpdate() => Place();

        private void Place()
        {
            Transform over = transform.parent;
            if (over == null || _text == null) return;
            float t = Time.time - _poppedAt;
            float pop = Mathf.Max(0.01f, _s.markPopSeconds), hold = _s.markHoldSeconds, fade = Mathf.Max(0.01f, _s.markOutSeconds);
            if (t >= pop + hold + fade) { gameObject.SetActive(false); return; }

            // Up past its size and back (a quick sine over the pop), steady, then down to nothing.
            float k;
            if (t < pop)
            {
                float u = t / pop;
                k = u < 0.6f ? Mathf.Lerp(0f, _s.markOvershoot, u / 0.6f) : Mathf.Lerp(_s.markOvershoot, 1f, (u - 0.6f) / 0.4f);
            }
            else if (t < pop + hold) k = 1f;
            else k = 1f - (t - pop - hold) / fade;

            if (_s.markColour != _text.color) _text.color = _s.markColour;
            if (_fitText != _s.markText)
            {
                // Measured once per text: the mark at Font Size, then scaled so it stands Mark Size of a unit tall.
                _fitText = _s.markText;
                _text.text = _fitText;
                float drawn = _text.GetPreferredValues(_fitText).y;
                _markLocal = drawn > 0.0001f ? 1f / drawn : 1f;
            }
            bool solid = _body != null && _body.enabled;
            float tall = solid ? _body.bounds.size.y : Mathf.Abs(over.lossyScale.y);
            float headTop = solid ? _body.bounds.max.y : over.position.y + tall;
            float height = tall * _s.markSize;
            float parent = Mathf.Max(0.0001f, Mathf.Abs(over.lossyScale.y));
            float scale = Mathf.Max(0.0001f, height * _markLocal / parent * k);
            transform.localScale = new Vector3(scale, scale, scale);

            Vector3 at = solid ? _body.bounds.center : over.position;
            at.y = headTop + tall * _s.markGap + height * 0.5f;
            Camera cam = Camera.main;
            Quaternion facing = cam != null ? Quaternion.LookRotation(cam.transform.forward, cam.transform.up) : transform.rotation;
            transform.SetPositionAndRotation(at, facing);
        }
    }
}
