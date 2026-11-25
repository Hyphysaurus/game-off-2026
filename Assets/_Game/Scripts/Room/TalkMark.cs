using TMPro;
using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// The "!" over a house character who has something new to say (D-0221, Mario 64's Toad): a gold "!" in the HUD's
    /// font with a dark outline, floating over its head, bobbing, always turned to the camera. The character (HouseCharacter)
    /// makes it the first time it is wanted and switches it on and off; it goes once the character has been talked to for
    /// the current step. Its size, gap, bob and colour are on Resources/ObjectiveSet (shares of the character's height, so a
    /// character scaled up wears a bigger one). Made at run time, nothing in a scene; nothing allocates per frame.
    /// </summary>
    public sealed class TalkMark : MonoBehaviour
    {
        private const string FontPath = "HUD/Fredoka SDF";
        private const string Mark = "!";
        private const float FontSize = 10f, OutlineWidth = 0.25f;
        private static readonly Color Ink = new Color32(0x1f, 0x1b, 0x2e, 0xff);   // the stickers' outline

        private TextMeshPro _text;
        private Collider _body;
        private float _phase, _fitSize = -1f, _markLocal = 1f;
        private Color _colour = Color.clear;

        /// <summary>A mark over <paramref name="over"/>, standing on top of <paramref name="body"/> (its solid capsule).</summary>
        public static TalkMark Make(Transform over, Collider body)
        {
            var go = new GameObject("StepMark (D-0221)");
            go.transform.SetParent(over, false);
            var mark = go.AddComponent<TalkMark>();
            mark._body = body;
            mark.Build();
            return mark;
        }

        private void Build()
        {
            _text = gameObject.AddComponent<TextMeshPro>();
            var font = Resources.Load<TMP_FontAsset>(FontPath);
            if (font != null) _text.font = font;
            _text.text = Mark;
            _text.fontSize = FontSize;
            _text.fontStyle = FontStyles.Bold;
            _text.alignment = TextAlignmentOptions.Center;
            _text.textWrappingMode = TextWrappingModes.NoWrap;
            _text.outlineWidth = OutlineWidth;   // its own material copy, made once
            _text.outlineColor = Ink;
            if (TryGetComponent(out MeshRenderer drawn))
            {
                drawn.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;   // a sign over a head casts nothing
                drawn.receiveShadows = false;
            }
            _phase = Random.value * Mathf.PI * 2f;   // each character bobs on its own beat
        }

        private void LateUpdate()
        {
            ObjectiveSet set = Objectives.Set;
            Transform over = transform.parent;
            if (over == null || _text == null) return;
            if (set.stepMarkColour != _colour) { _colour = set.stepMarkColour; _text.color = _colour; }

            // The character's height in the world: its capsule, or its scale when it has none.
            float tall = _body != null && _body.enabled ? _body.bounds.size.y : Mathf.Abs(over.lossyScale.y) * 1.8f;
            float headTop = _body != null && _body.enabled ? _body.bounds.max.y : over.position.y + tall;
            if (_fitSize != set.stepMarkSize)
            {
                // Measured once per size: the "!" at Font Size, then scaled so it stands Step Mark Size of a unit tall.
                _fitSize = set.stepMarkSize;
                float drawn = _text.GetPreferredValues(Mark).y;
                _markLocal = drawn > 0.0001f ? 1f / drawn : 1f;
            }
            float height = tall * set.stepMarkSize;
            float parent = Mathf.Max(0.0001f, Mathf.Abs(over.lossyScale.y));
            float scale = height * _markLocal / parent;
            if (Mathf.Abs(transform.localScale.x - scale) > 0.0001f) transform.localScale = new Vector3(scale, scale, scale);

            float bob = Mathf.Sin(Time.time * set.stepMarkBobsPerSecond * Mathf.PI * 2f + _phase) * 0.5f + 0.5f;
            Vector3 at = over.position;
            at.y = headTop + tall * (set.stepMarkGap + set.stepMarkBob * bob) + height * 0.5f;
            Camera cam = Camera.main;
            Quaternion facing = cam != null ? Quaternion.LookRotation(cam.transform.forward, cam.transform.up) : transform.rotation;
            transform.SetPositionAndRotation(at, facing);
        }
    }
}
