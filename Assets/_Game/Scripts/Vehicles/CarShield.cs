using UnityEngine;
using UnityEngine.Rendering;

namespace ShiftJam
{
    /// <summary>
    /// The shield a life ring gives a car (D-0038): for its seconds the car takes no damage (Damageable.Protect), a faint
    /// bubble and the life ring circle it, a hit it turns away flashes the bubble, and it blinks through its last two
    /// seconds. The pickup adds it to the car; another ring tops the time back up. The look is its own object that follows
    /// the car (never the car's child), so it stays out of the car's bounds, Build Mode's picking and the garage's part
    /// swaps. A reset (F9, the garage's repair) ends it.
    /// </summary>
    public sealed class CarShield : MonoBehaviour, IResettable
    {
        private const float BlinkFor = 2f, FlashFor = 0.2f, RingSpin = 70f;
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        private Damageable _health;
        private float _until, _seconds, _flashAt = -9f, _nextBlockSound, _spin;
        private GameObject _look;
        private Transform _bubble, _ring;
        private Renderer _bubbleRenderer;
        private Renderer[] _ringRenderers = new Renderer[0];
        private MaterialPropertyBlock _block;
        private Color _bubbleColour = new Color(0.45f, 0.85f, 1f, 0.22f);
        private Quaternion _ringLay = Quaternion.identity;
        private Vector3 _ringPivot, _ringCentre;
        private float _ringBob;
        private AudioClip _blockedClip, _endClip;
        private AudioSource _audio;

        /// <summary>Seconds left (0 when down).</summary>
        public float Remaining => _health != null && enabled ? Mathf.Max(0f, _until - Time.time) : 0f;
        /// <summary>How long this bubble was given for.</summary>
        public float Seconds => _seconds;
        public bool Up => Remaining > 0f;

        /// <summary>Puts a shield on <paramref name="car"/> (it needs a Damageable) or tops up the one it has. Null when the
        /// car takes no damage (the wooden car) or is wrecked.</summary>
        public static CarShield Give(Component car, float seconds, GameObject ring, Mesh bubbleMesh, Material bubble, AudioClip blocked, AudioClip end)
        {
            if (car == null) return null;
            var health = car.GetComponent<Damageable>();
            if (health == null || health.Dead) return null;
            var shield = car.GetComponent<CarShield>();
            if (shield == null) shield = car.gameObject.AddComponent<CarShield>();
            shield.Begin(health, seconds, ring, bubbleMesh, bubble, blocked, end);
            return shield;
        }

        private void Begin(Damageable health, float seconds, GameObject ring, Mesh bubbleMesh, Material bubble, AudioClip blocked, AudioClip end)
        {
            if (_health != health)
            {
                Unhook();
                _health = health;
                _health.Blocked += OnBlocked;
                _health.Restored += OnRestored;
            }
            _blockedClip = blocked; _endClip = end;
            _seconds = Mathf.Max(0.1f, seconds);
            _until = Time.time + _seconds;
            _health.Protect(_seconds);
            if (_look == null) MakeLook(ring, bubbleMesh, bubble);
            Fit();
            _look.SetActive(true);
            enabled = true;
            LateUpdate();   // in place this frame, not one frame late
        }

        private void MakeLook(GameObject ring, Mesh bubbleMesh, Material bubble)
        {
            _block = new MaterialPropertyBlock();
            _look = new GameObject("CarShield " + name);
            _audio = _look.AddComponent<AudioSource>();
            _audio.playOnAwake = false;
            _audio.spatialBlend = 0.5f;   // half heard as the driver's own, half placed at the car
            _audio.rolloffMode = AudioRolloffMode.Linear;

            if (bubbleMesh != null && bubble != null)
            {
                var go = new GameObject("Bubble");
                go.transform.SetParent(_look.transform, false);
                go.AddComponent<MeshFilter>().sharedMesh = bubbleMesh;
                _bubbleRenderer = go.AddComponent<MeshRenderer>();
                _bubbleRenderer.sharedMaterial = bubble;
                _bubbleRenderer.shadowCastingMode = ShadowCastingMode.Off;
                _bubbleRenderer.receiveShadows = false;
                if (bubble.HasProperty(BaseColorId)) _bubbleColour = bubble.GetColor(BaseColorId);
                _bubble = go.transform;
            }
            if (ring != null)
            {
                var r = Instantiate(ring, _look.transform, false);
                r.name = "Ring";
                foreach (var c in r.GetComponentsInChildren<Collider>(true)) Destroy(c);
                foreach (var b in r.GetComponentsInChildren<MonoBehaviour>(true)) b.enabled = false;   // the pack's own spin and bob
                _ringRenderers = r.GetComponentsInChildren<Renderer>(true);
                foreach (var rr in _ringRenderers) rr.shadowCastingMode = ShadowCastingMode.Off;
                _ring = r.transform;
            }
        }

        /// <summary>The bubble stretched round the car, the ring laid flat round its middle, both in the car's own space.</summary>
        private void Fit()
        {
            if (!CarBounds(out Bounds b)) b = new Bounds(Vector3.up * 0.6f, new Vector3(2f, 1.2f, 3.8f));
            if (_bubble != null)
            {
                _bubble.localPosition = b.center;
                _bubble.localRotation = Quaternion.identity;
                _bubble.localScale = new Vector3(b.size.x * 1.35f, b.size.y * 1.7f, b.size.z * 1.2f);
            }
            if (_ring == null) return;
            _ring.localPosition = Vector3.zero; _ring.localRotation = Quaternion.identity; _ring.localScale = Vector3.one;
            if (!BuildBounds.Local(_ring, out Bounds rb)) return;
            Vector3 s = rb.size;
            // Laid flat: its thinnest side up, whichever way the pack modelled it.
            _ringLay = s.y <= s.x && s.y <= s.z ? Quaternion.identity : s.x <= s.z ? Quaternion.Euler(0f, 0f, 90f) : Quaternion.Euler(90f, 0f, 0f);
            float across = Mathf.Max(s.x, Mathf.Max(s.y, s.z));
            float want = Mathf.Sqrt(b.size.x * b.size.x + b.size.z * b.size.z) * 1.05f;   // round the car's footprint
            float k = want / Mathf.Max(0.01f, across);
            _ring.localScale = Vector3.one * k;
            _ringCentre = rb.center * k;
            _ringPivot = new Vector3(b.center.x, b.min.y + b.size.y * 0.35f, b.center.z);
            _ringBob = b.size.y * 0.06f;
        }

        private static readonly Vector3[] Corners = new Vector3[8];

        /// <summary>The car's drawn body in its own space: shown mesh parts only, so the hidden driver (skinned, rendering
        /// off) and the effects do not stretch the bubble.</summary>
        private bool CarBounds(out Bounds bounds)
        {
            bounds = new Bounds(Vector3.zero, Vector3.zero);
            bool any = false;
            Matrix4x4 toCar = transform.worldToLocalMatrix;
            foreach (var r in GetComponentsInChildren<MeshRenderer>(false))
            {
                if (!r.enabled || r.forceRenderingOff) continue;
                var mf = r.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null) continue;
                Bounds mb = mf.sharedMesh.bounds;
                Matrix4x4 m = toCar * r.transform.localToWorldMatrix;
                Vector3 c = mb.center, e = mb.extents;
                for (int i = 0; i < 8; i++)
                    Corners[i] = m.MultiplyPoint3x4(c + new Vector3((i & 1) == 0 ? -e.x : e.x, (i & 2) == 0 ? -e.y : e.y, (i & 4) == 0 ? -e.z : e.z));
                for (int i = 0; i < 8; i++)
                {
                    if (!any) { bounds = new Bounds(Corners[i], Vector3.zero); any = true; }
                    else bounds.Encapsulate(Corners[i]);
                }
            }
            return any;
        }

        private void Update()
        {
            if (_health == null || _look == null) { End(false); return; }
            float left = _until - Time.time;
            if (left <= 0f || _health.Dead) { End(left <= 0f); return; }
            bool build = GameMode.Current == GameMode.Mode.Build;   // Build Mode sees the car, not the bubble
            if (_look.activeSelf == build) _look.SetActive(!build);
            if (build) return;

            bool shown = left > BlinkFor || Mathf.Repeat(Time.time * 6f, 1f) < 0.6f;
            if (_bubbleRenderer != null)
            {
                _bubbleRenderer.enabled = shown;
                float flash = 1f - Mathf.Clamp01((Time.time - _flashAt) / FlashFor);
                Color c = _bubbleColour;
                c.a = Mathf.Lerp(_bubbleColour.a, Mathf.Max(0.6f, _bubbleColour.a), flash);
                _block.SetColor(BaseColorId, c);
                _bubbleRenderer.SetPropertyBlock(_block);
            }
            foreach (var r in _ringRenderers) if (r != null) r.enabled = shown;
            _spin = Mathf.Repeat(_spin + RingSpin * Time.deltaTime, 360f);
        }

        private void LateUpdate()
        {
            if (_look == null || !_look.activeSelf) return;
            _look.transform.SetPositionAndRotation(transform.position, transform.rotation);
            _look.transform.localScale = transform.lossyScale;   // sized in the car's own space
            if (_ring == null) return;
            Quaternion turn = Quaternion.AngleAxis(_spin, Vector3.up) * _ringLay;
            _ring.localRotation = turn;
            _ring.localPosition = _ringPivot + Vector3.up * (Mathf.Sin(Time.time * 3f) * _ringBob) - turn * _ringCentre;
        }

        private void OnBlocked(DamageInfo hit)
        {
            if (!Up) return;
            _flashAt = Time.time;
            if (Time.time < _nextBlockSound) return;
            _nextBlockSound = Time.time + 0.2f;
            Play(_blockedClip, 0.7f);
        }

        private void OnRestored() { if (Up) End(false); }   // a reset or the garage's repair clears it

        private void End(bool sound)
        {
            if (_look != null) _look.SetActive(false);
            if (sound) Play(_endClip, 0.8f);
            _until = 0f;
            enabled = false;
        }

        private void Play(AudioClip clip, float volume)
        {
            if (clip == null || _audio == null) return;
            if (!_audio.gameObject.activeInHierarchy)   // the look is hidden at the end: sound from the car instead
            {
                AudioSource.PlayClipAtPoint(clip, transform.position, volume);
                return;
            }
            _audio.PlayOneShot(clip, volume);
        }

        public void ResetState()
        {
            if (Up) End(false);
        }

        private void OnDisable()
        {
            if (_look != null) _look.SetActive(false);
        }

        private void OnDestroy()
        {
            Unhook();
            if (_look != null) Destroy(_look);
        }

        private void Unhook()
        {
            if (_health == null) return;
            _health.Blocked -= OnBlocked;
            _health.Restored -= OnRestored;
        }
    }
}
