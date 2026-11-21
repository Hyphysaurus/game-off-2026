using TMPro;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// Who is who online (D-0117), on the NetPlayer: over every other player's head their name (their PC's user name,
    /// sent once by them), in their colour (one of four, by join order), and a small health bar under it read from
    /// NetHealth. It faces this PC's camera and grows with the player (a kid's tag is high and big). Not over our own head.
    /// </summary>
    public sealed class NetNameTag : NetworkBehaviour
    {
        public static readonly Color[] Colours =
        {
            new Color(1f, 0.55f, 0.25f),   // orange
            new Color(0.35f, 0.75f, 1f),   // sky blue
            new Color(0.55f, 0.9f, 0.35f), // lime
            new Color(1f, 0.45f, 0.75f),   // pink
        };

        private readonly NetworkVariable<FixedString32Bytes> _name = new NetworkVariable<FixedString32Bytes>(default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        private TextMeshPro _tag;
        private NetHealth _health;
        private float _nextText;

        /// <summary>This player's colour.</summary>
        public Color Colour => Colours[(int)(OwnerClientId % (ulong)Colours.Length)];

        public override void OnNetworkSpawn()
        {
            _health = GetComponent<NetHealth>();
            if (IsOwner)
            {
                string me = System.Environment.UserName;
                if (string.IsNullOrWhiteSpace(me)) me = "Player " + (OwnerClientId + 1);
                if (me.Length > 16) me = me.Substring(0, 16);
                _name.Value = new FixedString32Bytes(me);
                return;
            }
            var go = new GameObject("NameTag");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = Vector3.up * TagHeight();
            _tag = go.AddComponent<TextMeshPro>();
            _tag.alignment = TextAlignmentOptions.Center;
            _tag.fontSize = 4f;
            _tag.textWrappingMode = TextWrappingModes.NoWrap;
            _tag.outlineWidth = 0.25f; _tag.outlineColor = new Color32(0, 0, 0, 200);
            _tag.rectTransform.sizeDelta = new Vector2(6f, 1.5f);
        }

        public override void OnNetworkDespawn() { if (_tag != null) Destroy(_tag.gameObject); }

        private void LateUpdate()
        {
            if (_tag == null) return;
            var cam = Camera.main;
            if (cam != null) _tag.transform.rotation = Quaternion.LookRotation(_tag.transform.position - cam.transform.position, Vector3.up);
            if (Time.unscaledTime < _nextText) return;
            _nextText = Time.unscaledTime + 0.15f;
            string name = _name.Value.Length > 0 ? _name.Value.ToString() : "Player " + (OwnerClientId + 1);
            string colour = ColorUtility.ToHtmlStringRGB(Colour);
            string bar = "";
            if (_health != null)
            {
                float share = Mathf.Clamp01(_health.Health / Mathf.Max(1f, PlayerHealthSettings.Load().maxHealth));
                int full = Mathf.RoundToInt(share * 10f);
                bar = "\n<size=60%><color=#" + (share > 0.3f ? "7CFF6A" : "FF5A4A") + ">" + new string('|', full) + "</color><color=#00000088>" + new string('|', 10 - full) + "</color>";
                if (_health.Shield > 0.5f) bar += " <color=#5AD8FF>+</color>";
            }
            _tag.text = "<color=#" + colour + ">" + name + "</color>" + bar;
        }

        private float TagHeight()
        {
            var cc = GetComponent<CharacterController>();
            return (cc != null ? cc.height : 1.8f) * 1.15f;   // local units: grows with the player's scale
        }
    }
}
