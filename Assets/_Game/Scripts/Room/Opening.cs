using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace ShiftJam
{
    /// <summary>
    /// New Game's opening in the toy box (D-0097). Dark; the box's flaps open and warm light pours in with dust in it; a
    /// spark (a tiny shift orb, the game's own) swirls down onto the lying toy and strikes: a white flash, a beat of
    /// slow motion, a small shake; the toy gets up (WakeUp) while the camera rises from beside it to behind it, then
    /// hands over to the player's own camera. Hold any key or button to skip (Esc skips at once). Only New Game plays
    /// it (TitleMenu); Continue, the sandboxes and coming back through a door start standing. Numbers: OpeningSettings.
    /// Runs after the ShoulderCamera each frame, so the hand-over eases from the last shot into the camera's own pose.
    /// </summary>
    [DefaultExecutionOrder(200)]
    public sealed class Opening : MonoBehaviour
    {
        /// <summary>The opening is on: the player has no control yet (the pause menu waits too).</summary>
        public static bool Playing { get; private set; }
        /// <summary>How the last opening ended: true if it was skipped. For checks.</summary>
        public static bool LastSkipped { get; private set; }

        /// <summary>Plays the opening for this player (an opening already playing is replaced).</summary>
        public static Opening Play(GameObject player)
        {
            if (player == null) return null;
            var old = FindFirstObjectByType<Opening>();
            if (old != null) DestroyImmediate(old.gameObject);
            var o = new GameObject("Opening").AddComponent<Opening>();
            o.Begin(player);
            return o;
        }

        private OpeningSettings _s;
        private GameObject _player;
        private WakeUp _wake;
        private PlayerInput _input;
        private ShoulderCamera _rig;
        private Transform _cam;
        private Animator _anim;
        private BoxBurst _box;
        private Vector3 _feet, _fwd, _right, _top;
        private float _unit = 1.8f;

        private Image _black, _white;
        private TMP_Text _hint;
        private Light _beam;
        private ParticleSystem _dust;
        private Transform _spark;
        private ShiftOrb _orb;
        private Vector3 _sparkFrom, _sparkScale;
        private readonly List<Canvas> _hudOff = new List<Canvas>();

        private float _t, _tStrike, _handT, _holdT, _slowT = -1f, _shakeLeft, _sparkGone = -1f, _fadeAfter = -1f, _skipFade = -1f;
        private bool _lid, _sparkOut, _struck, _handing, _handed, _released;
        private Vector3 _camFromPos;
        private Quaternion _camFromRot, _look;

        private void Begin(GameObject player)
        {
            _s = OpeningSettings.Load();
            _player = player;
            Playing = true;
            LastSkipped = false;

            _wake = player.GetComponent<WakeUp>();
            _input = player.GetComponent<PlayerInput>();
            _anim = player.GetComponentInChildren<Animator>();
            _rig = FindFirstObjectByType<ShoulderCamera>();
            _cam = _rig != null ? _rig.transform : Camera.main != null ? Camera.main.transform : null;
            if (_input != null) _input.enabled = false;
            if (_rig != null) _rig.enabled = false;

            Transform pt = player.transform;
            _feet = pt.position;
            _fwd = Vector3.ProjectOnPlane(pt.forward, Vector3.up);
            _fwd = _fwd.sqrMagnitude < 0.0001f ? Vector3.forward : _fwd.normalized;
            _right = Vector3.Cross(Vector3.up, _fwd);
            if (player.TryGetComponent(out Collider body) && body.bounds.size.y > 0.01f) _unit = body.bounds.size.y;

            FindBox();
            if (_wake != null) _wake.Lie();
            HideHud();
            BuildOverlay();
            BuildLight();
            if (_s.dustMotes > 0) BuildDust();
            _look = Quaternion.LookRotation(Vector3.Lerp(Chest(), _top, _s.lookUp) - LowShot());
            PlaceCamera(0f);
        }

        /// <summary>The toy box the player lies in: the BoxBurst whose renderers' bounds hold the player's feet (or the
        /// nearest). No box: the light and the spark come from straight above.</summary>
        private void FindBox()
        {
            _top = _feet + Vector3.up * 8f * _unit;
            float best = float.MaxValue;
            foreach (var box in FindObjectsByType<BoxBurst>(FindObjectsSortMode.None))
            {
                if (box.Burst) continue;
                var rs = box.GetComponentsInChildren<Renderer>();
                if (rs.Length == 0) continue;
                Bounds b = rs[0].bounds;
                foreach (var r in rs) b.Encapsulate(r.bounds);
                Vector3 c = b.center; c.y = _feet.y;
                float d = (c - _feet).sqrMagnitude;
                bool holds = _feet.x >= b.min.x && _feet.x <= b.max.x && _feet.z >= b.min.z && _feet.z <= b.max.z;
                if (holds) d = -1f;
                if (d >= best) continue;
                best = d;
                _box = box;
                _top = new Vector3(b.center.x, b.max.y, b.center.z);
            }
            if (_box != null && best > 0f && best > 100f * _unit * _unit) { _box = null; _top = _feet + Vector3.up * 8f * _unit; }   // too far to be ours
        }

        private void HideHud()
        {
            var hud = GameObject.Find("HUD");
            if (hud == null) return;
            foreach (var c in hud.GetComponentsInChildren<Canvas>())
                if (c.enabled) { c.enabled = false; _hudOff.Add(c); }
        }

        private void ShowHud()
        {
            foreach (var c in _hudOff) if (c != null) c.enabled = true;
            _hudOff.Clear();
        }

        private void BuildOverlay()
        {
            var go = new GameObject("Opening Overlay", typeof(RectTransform));
            go.transform.SetParent(transform, false);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 60;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            _white = FullScreen(go.transform, "Flash", new Color(1f, 1f, 1f, 0f));
            _black = FullScreen(go.transform, "Black", Color.black);

            var hint = new GameObject("Skip Hint", typeof(RectTransform));
            hint.transform.SetParent(go.transform, false);
            var rt = (RectTransform)hint.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(1f, 0f);
            rt.anchoredPosition = new Vector2(-48f, 40f);
            rt.sizeDelta = new Vector2(600f, 60f);
            _hint = hint.AddComponent<TextMeshProUGUI>();
            _hint.text = "Hold to skip";
            _hint.fontSize = 30f;
            _hint.alignment = TextAlignmentOptions.BottomRight;
            _hint.color = new Color(1f, 1f, 1f, 0f);
            _hint.raycastTarget = false;
        }

        private static Image FullScreen(Transform parent, string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
            var img = go.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        private void BuildLight()
        {
            var go = new GameObject("Opening Light");
            go.transform.SetParent(transform, false);
            go.transform.SetPositionAndRotation(_top + Vector3.up * _unit, Quaternion.LookRotation(Vector3.down, _fwd));
            _beam = go.AddComponent<Light>();
            _beam.type = LightType.Spot;
            _beam.color = _s.lightColor;
            _beam.intensity = 0f;
            _beam.range = (_top.y - _feet.y) * 1.6f + 3f * _unit;
            _beam.spotAngle = _s.lightAngle;
            _beam.innerSpotAngle = _s.lightAngle * 0.5f;
            _beam.shadows = LightShadows.Soft;
        }

        private void BuildDust()
        {
            var go = new GameObject("Opening Dust");
            go.transform.SetParent(transform, false);
            float h = Mathf.Max(_unit, _top.y - _feet.y);
            go.transform.position = new Vector3(_top.x, _feet.y + h * 0.5f, _top.z);
            _dust = go.AddComponent<ParticleSystem>();
            _dust.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            float life = 7f;
            var main = _dust.main;
            main.loop = true;
            main.duration = life;
            main.startLifetime = life;
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.012f * _unit, 0.03f * _unit);
            main.startColor = new Color(1f, 0.95f, 0.85f, 0.6f);
            main.maxParticles = _s.dustMotes;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = 0f;
            main.playOnAwake = false;
            var emission = _dust.emission;
            emission.rateOverTime = _s.dustMotes / life;
            var shape = _dust.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            float w = Mathf.Tan(_s.lightAngle * 0.5f * Mathf.Deg2Rad) * h * 0.8f;
            shape.scale = new Vector3(w, h, w);
            var noise = _dust.noise;
            noise.enabled = true;
            noise.strength = 0.08f * _unit;
            noise.frequency = 0.25f;
            var fade = _dust.colorOverLifetime;
            fade.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.25f), new GradientAlphaKey(1f, 0.75f), new GradientAlphaKey(0f, 1f) });
            fade.color = g;
            var mat = Resources.Load<Material>("FX/BoxDust");
            if (mat != null) go.GetComponent<ParticleSystemRenderer>().sharedMaterial = mat;
        }

        private void SpawnSpark()
        {
            _sparkOut = true;
            _sparkFrom = _top + Vector3.down * 0.5f * _unit;
            var prefab = Resources.Load<GameObject>("FX/ShiftOrbVisual");
            GameObject go = prefab != null ? Instantiate(prefab) : GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "Opening Spark";
            go.transform.SetParent(transform, true);
            foreach (var c in go.GetComponentsInChildren<Collider>()) Destroy(c);
            go.transform.position = _sparkFrom;
            // Fit it to the size wanted, whatever the model's own size.
            Bounds b = new Bounds(go.transform.position, Vector3.zero);
            bool any = false;
            foreach (var r in go.GetComponentsInChildren<Renderer>()) { if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds); }
            float across = any ? Mathf.Max(b.size.x, b.size.y, b.size.z) : 1f;
            go.transform.localScale *= _s.sparkSize * _unit / Mathf.Max(0.0001f, across);
            _sparkScale = go.transform.localScale;
            _spark = go.transform;
            _orb = go.GetComponentInChildren<ShiftOrb>();
            if (_orb != null) _orb.SetAwake(true);
            var lightGo = new GameObject("Spark Light");
            lightGo.transform.SetParent(_spark, false);
            var l = lightGo.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = _s.sparkLightColor;
            l.intensity = _s.sparkLightIntensity;
            l.range = _s.sparkLightRange * _unit;
            GameEvents.RaiseOpeningSpark(false);
        }

        private void Strike()
        {
            _struck = true;
            _tStrike = _t;
            if (_orb != null) { _orb.Strike(); _orb.Bolts(); }
            _sparkGone = 0f;
            _slowT = 0f;
            _shakeLeft = _s.shakeSeconds;
            GameEvents.RaiseOpeningSpark(true);
            if (_wake != null) _wake.Begin();
            GameEvents.RaiseOpeningGetUp();
        }

        private void Update()
        {
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.1f);   // a hitch (the first frame after the click) never skips a beat
            _t += dt;
            if (!_handing) Skipping(dt);

            if (_skipFade >= 0f)
            {
                _skipFade += dt;
                _black.color = new Color(0f, 0f, 0f, 1f - Mathf.Clamp01(_skipFade / 0.35f));
            }
            else
            {
                float lift = Mathf.Clamp01((_t - _s.blackHold) / Mathf.Max(0.01f, _s.fadeIn));
                _black.color = new Color(0f, 0f, 0f, 1f - Smooth(lift));
            }

            if (!_lid && _t >= _s.lidAt)
            {
                _lid = true;
                if (_box != null) _box.OpenFlaps(_s.lidSeconds, _s.lidStagger);
                if (_dust != null) _dust.Play();
                GameEvents.RaiseOpeningLid();
            }
            if (_beam != null)
            {
                float on = _s.lightIntensity * Smooth(Mathf.Clamp01((_t - _s.lidAt) / Mathf.Max(0.01f, _s.lightRamp)));
                if (_fadeAfter >= 0f)
                {
                    _fadeAfter += dt;
                    on = _s.lightIntensity * (1f - Smooth(Mathf.Clamp01(_fadeAfter / Mathf.Max(0.01f, _s.lightFadeAfter))));
                    if (_dust != null && _fadeAfter > 0f) { var em = _dust.emission; em.enabled = false; }
                }
                _beam.intensity = on;
            }

            if (!_sparkOut && !_struck && _t >= _s.sparkAt) SpawnSpark();
            if (_sparkOut && !_struck && _spark != null)
            {
                float k = Mathf.Clamp01((_t - _s.sparkAt) / Mathf.Max(0.01f, _s.sparkFall));
                float e = Smooth(k);
                float angle = _s.sparkTurns * Mathf.PI * 2f * e;
                Vector3 swirl = (_right * Mathf.Cos(angle) + _fwd * Mathf.Sin(angle)) * _s.sparkSwirl * _unit * (1f - e);
                _spark.position = Vector3.Lerp(_sparkFrom, Chest(), e) + swirl;
                if (k >= 1f) Strike();
            }
            if (_sparkGone >= 0f && _spark != null)
            {
                _sparkGone += dt;
                float k = Mathf.Clamp01(_sparkGone / 0.35f);
                _spark.position = Chest();
                _spark.localScale = _sparkScale * (1f + 0.6f * k) * (1f - k);
                if (k >= 1f) { Destroy(_spark.gameObject); _spark = null; }
            }

            if (_struck) _white.color = new Color(1f, 1f, 1f, _s.flash * (1f - Mathf.Clamp01((_t - _tStrike) / Mathf.Max(0.01f, _s.flashSeconds))));
            SlowMotion(dt);

            if (!_handing && _struck && (_wake == null ? _t - _tStrike >= 1.7f : _wake.Awake_)) HandOver(false);
            if (_handed && _fadeAfter >= _s.lightFadeAfter && (_skipFade < 0f || _skipFade >= 0.35f)) Destroy(gameObject);
        }

        private void LateUpdate()
        {
            if (_cam == null) return;
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.1f);
            if (!_handing) { PlaceCamera(dt); return; }
            if (_handed) return;
            _handT += dt;
            float k = Smooth(Mathf.Clamp01(_handT / Mathf.Max(0.01f, _s.handOver)));
            // The ShoulderCamera has placed itself this frame (it runs first): ease from the last shot into that.
            _cam.SetPositionAndRotation(Vector3.Lerp(_camFromPos, _cam.position, k), Quaternion.Slerp(_camFromRot, _cam.rotation, k));
            if (k >= 1f) _handed = true;
        }

        private void PlaceCamera(float dt)
        {
            Vector3 chest = Chest();
            Vector3 pos;
            Vector3 target;
            if (!_struck)
            {
                pos = LowShot();
                target = _sparkOut && _spark != null ? Vector3.Lerp(_spark.position, chest, 0.3f) : Vector3.Lerp(chest, _top, _s.lookUp);
            }
            else
            {
                float rise = _wake != null ? Mathf.Max(0.1f, _wake.Duration) : 1.7f;
                float e = Smooth(Mathf.Clamp01((_t - _tStrike) / rise));
                pos = Vector3.Lerp(LowShot(), Shot(_s.riseShot), e);
                target = chest + Vector3.up * 0.15f * _unit;
            }
            Vector3 look = target - pos;
            if (look.sqrMagnitude > 0.0001f)
            {
                Quaternion want = Quaternion.LookRotation(look, Vector3.up);
                _look = dt <= 0f ? want : Quaternion.Slerp(_look, want, 1f - Mathf.Exp(-5f * dt));
            }
            Quaternion rot = _look;
            if (_shakeLeft > 0f)
            {
                _shakeLeft -= dt;
                float a = _s.shakeDegrees * Mathf.Clamp01(_shakeLeft / Mathf.Max(0.01f, _s.shakeSeconds));
                float n = Time.unscaledTime * 30f;
                rot *= Quaternion.Euler((Mathf.PerlinNoise(n, 0.1f) - 0.5f) * 2f * a, (Mathf.PerlinNoise(0.7f, n) - 0.5f) * 2f * a, (Mathf.PerlinNoise(n, 0.9f) - 0.5f) * a);
            }
            _cam.SetPositionAndRotation(pos, rot);
        }

        private Vector3 LowShot() => Shot(_s.lowShot);
        private Vector3 Shot(Vector3 local) => _feet + (_right * local.x + Vector3.up * local.y + _fwd * local.z) * _unit;

        /// <summary>The toy's chest as it lies and as it rises (its bone, so the spark and the camera follow the get-up).</summary>
        private Vector3 Chest()
        {
            if (_anim != null && _anim.isHuman)
            {
                Transform b = _anim.GetBoneTransform(HumanBodyBones.Chest);
                if (b == null) b = _anim.GetBoneTransform(HumanBodyBones.Spine);
                if (b == null) b = _anim.GetBoneTransform(HumanBodyBones.Hips);
                if (b != null) return b.position;
            }
            return _feet + Vector3.up * 0.35f * _unit;
        }

        private void SlowMotion(float dt)
        {
            if (_slowT < 0f) return;
            _slowT += dt;
            float hold = Mathf.Max(0f, _s.slowSeconds);
            float scale = _slowT < hold ? _s.slowScale : Mathf.Lerp(_s.slowScale, 1f, Smooth(Mathf.Clamp01((_slowT - hold) / Mathf.Max(0.01f, hold))));
            if (_slowT >= hold * 2f) { scale = 1f; _slowT = -1f; }
            Time.timeScale = Mathf.Clamp(scale, 0.05f, 1f);
        }

        private void Skipping(float dt)
        {
            var k = Keyboard.current; var m = Mouse.current; var pad = Gamepad.current;
            if (k != null && k.escapeKey.wasPressedThisFrame) { Skip(); return; }
            bool held = (k != null && k.anyKey.isPressed)
                        || (m != null && (m.leftButton.isPressed || m.rightButton.isPressed))
                        || (pad != null && (pad.buttonSouth.isPressed || pad.buttonEast.isPressed || pad.startButton.isPressed));
            if (!_released) { if (!held) _released = true; return; }   // the click on New Game is still down
            _holdT = held ? _holdT + dt : 0f;
            float share = Mathf.Clamp01(_holdT / Mathf.Max(0.01f, _s.skipHold));
            float a = held ? 0.5f + 0.5f * share : Mathf.Max(0f, _hint.color.a - dt * 2f);
            _hint.color = new Color(1f, 1f, 1f, a);
            if (held && _holdT >= _s.skipHold) Skip();
        }

        private void Skip()
        {
            if (_handing) return;
            LastSkipped = true;
            if (_box != null) _box.OpenFlaps(0.01f, 0f);
            if (_dust != null && !_dust.isPlaying) _dust.Play();
            _lid = true;
            _struck = true;
            _tStrike = _t - 10f;   // no flash
            if (_spark != null) { Destroy(_spark.gameObject); _spark = null; }
            _sparkOut = true;
            _sparkGone = -1f;
            _slowT = -1f;
            Time.timeScale = 1f;
            _white.color = new Color(1f, 1f, 1f, 0f);
            _hint.color = new Color(1f, 1f, 1f, 0f);
            if (_wake != null) _wake.FinishNow();
            _skipFade = 0f;
            HandOver(true);
            _handed = true;   // straight to the player's camera, under the black
        }

        private void HandOver(bool skipped)
        {
            _handing = true;
            _handT = 0f;
            if (_cam != null) { _camFromPos = _cam.position; _camFromRot = _cam.rotation; }
            if (_rig != null)
            {
                Vector3 f = Vector3.ProjectOnPlane(_player.transform.forward, Vector3.up);
                _rig.Warp(f.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(f).eulerAngles.y : _rig.Yaw);
                _rig.enabled = true;
            }
            if (_input != null) _input.enabled = true;
            ShowHud();
            _fadeAfter = 0f;
            Playing = false;
            GameEvents.RaiseOpeningEnded(skipped);
        }

        private void OnDestroy()
        {
            if (!Playing) return;
            // Gone mid-way (a scene change): give everything back.
            Playing = false;
            if (_slowT >= 0f) Time.timeScale = 1f;
            if (_wake != null) _wake.FinishNow();
            if (_rig != null) _rig.enabled = true;
            if (_input != null) _input.enabled = true;
            ShowHud();
        }

        private static float Smooth(float x) => x * x * (3f - 2f * x);
    }
}
