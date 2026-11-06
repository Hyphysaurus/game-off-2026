using System.Collections.Generic;
using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// Damage numbers that pop out of a hit, rise, face the camera and fade. A small pool of TextMeshes built on first use
    /// with Unity's built-in font, so nothing has to be set up in the scene.
    /// </summary>
    public sealed class DamageNumbers : MonoBehaviour
    {
        private const int PoolSize = 24;
        private const float Life = 0.8f;
        private const float Rise = 1.2f;          // units at scale 1 over the whole life
        private const float CharSize = 0.06f;     // at scale 1

        private struct Pop { public TextMesh Text; public Vector3 From; public float Born, Scale; }

        private static DamageNumbers _instance;
        private readonly List<Pop> _pops = new List<Pop>(PoolSize);
        private int _next;

        /// <summary>Shows the amount at a world point; <paramref name="scale"/> sizes the text for a toy or a kid.</summary>
        public static void Show(Vector3 point, float amount, float scale)
        {
            if (_instance == null)
            {
                var go = new GameObject("DamageNumbers");
                _instance = go.AddComponent<DamageNumbers>();
            }
            _instance.Spawn(point, amount, scale);
        }

        private void Awake()
        {
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            for (int i = 0; i < PoolSize; i++)
            {
                var go = new GameObject("Pop");
                go.transform.SetParent(transform, false);
                var text = go.AddComponent<TextMesh>();
                text.font = font;
                text.fontSize = 64;
                text.anchor = TextAnchor.MiddleCenter;
                text.alignment = TextAlignment.Center;
                text.fontStyle = FontStyle.Bold;
                go.GetComponent<MeshRenderer>().sharedMaterial = font.material;
                go.SetActive(false);
                _pops.Add(new Pop { Text = text, Born = -10f });
            }
        }

        private void OnDestroy() { if (_instance == this) _instance = null; }

        private void Spawn(Vector3 point, float amount, float scale)
        {
            var pop = _pops[_next];
            _next = (_next + 1) % _pops.Count;
            pop.From = point + Random.insideUnitSphere * (0.15f * scale);
            pop.Born = Time.time;
            pop.Scale = scale;
            pop.Text.text = Mathf.RoundToInt(amount).ToString();
            pop.Text.characterSize = CharSize * scale;
            pop.Text.gameObject.SetActive(true);
            _pops[(_next + _pops.Count - 1) % _pops.Count] = pop;
        }

        private void LateUpdate()
        {
            var cam = Camera.main;
            for (int i = 0; i < _pops.Count; i++)
            {
                var pop = _pops[i];
                if (!pop.Text.gameObject.activeSelf) continue;
                float t = (Time.time - pop.Born) / Life;
                if (t >= 1f) { pop.Text.gameObject.SetActive(false); continue; }
                pop.Text.transform.position = pop.From + Vector3.up * (Rise * pop.Scale * (1f - (1f - t) * (1f - t)));
                if (cam != null) pop.Text.transform.rotation = Quaternion.LookRotation(cam.transform.forward, cam.transform.up);
                pop.Text.color = new Color(1f, 0.9f, 0.3f, 1f - t * t);
            }
        }
    }
}
