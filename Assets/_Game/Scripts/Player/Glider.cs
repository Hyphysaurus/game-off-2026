using UnityEngine;
using UnityEngine.SceneManagement;

namespace ShiftJam
{
    /// <summary>
    /// The paper-plane glider (D-0095), the toy's way down from height. Once taken (the paper plane lying on the playroom's
    /// desk, GliderPickup), Jump in the air opens it and the toy rides on top of the plane like a surfer; Jump again, or
    /// landing, folds it. A kid is too heavy for paper: a kid never opens it, and growing folds it. While it is open the
    /// motor sinks at a fixed rate and the stick steers (GlideSettings); the fall rule reads the landing speed (D-0096), so
    /// a glide lands soft by itself. It goes through the house's doors (HouseCarry), is not saved (Continue and New Game
    /// start without it), and in the Workshop, his sandbox, it is yours from the start. Added to every shifting player by
    /// the Shifter: nothing to place.
    /// </summary>
    [RequireComponent(typeof(PlayerMotor))]
    public sealed class Glider : MonoBehaviour
    {
        // Placeholder wording (his to change).
        public const string TakeHint = "<b>Jump</b> in the air to glide (Space / A). A paper plane holds a toy, not a kid.";
        public const string FindHint = "Falling toys break. Find a glider.";

        private GlideSettings _settings;
        private PlayerMotor _motor; private Shifter _shifter; private PlayerMoves _moves; private PlayerAnimation _animation;
        private bool _owned, _free;
        private Transform _rig;                      // the plane under the feet, made the first time it opens
        private Transform _plane; private Vector3 _planeBase;   // the plane model, and where it sits before the live ride offset
        private Transform _model;                    // the body, banked with the plane
        private Quaternion _modelRot; private Vector3 _modelPos; private bool _modelHeld;
        private float _bank, _pitch, _lastYaw;
        private AudioSource _wind, _cue;
        private readonly RaycastHit[] _hits = new RaycastHit[8];

        /// <summary>The player has a glider: taken from the desk, carried through a door, or the Workshop's.</summary>
        public bool Owned => _owned || _free;
        /// <summary>True while the plane is open under the toy.</summary>
        public bool Gliding { get; private set; }
        /// <summary>The camera's pull-back share while gliding.</summary>
        public float PullBack => _settings != null ? _settings.cameraPullBack : 0f;
        public GlideSettings Settings => _settings;
        /// <summary>Where and when the last glide opened (the play log reads them).</summary>
        public Vector3 OpenedAt { get; private set; }
        public float OpenedTime { get; private set; }
        /// <summary>Why the last glide ended: "landed", "jump", "grew", "stopped".</summary>
        public string LastFold { get; private set; } = "";

        private void Awake()
        {
            _settings = GlideSettings.Load();
            _motor = GetComponent<PlayerMotor>();
            TryGetComponent(out _shifter); TryGetComponent(out _moves); TryGetComponent(out _animation);
            _free = SceneManager.GetActiveScene().name == TitleMenu.WorkshopScene;   // his sandbox: yours from the start
        }

        private void OnEnable()
        {
            _motor.AirJump = OnAirJump;
            _motor.Landed += OnLanded;
            _motor.Warped += OnWarped;
        }

        private void OnDisable()
        {
            if (_motor.AirJump == (System.Func<bool>)OnAirJump) _motor.AirJump = null;
            _motor.Landed -= OnLanded;
            _motor.Warped -= OnWarped;
            Fold("stopped", false, true);
            ReleaseModel();
        }

        /// <summary>The paper plane is taken: the glider is yours, and the hint says how to use it.</summary>
        public void Give()
        {
            if (Owned) return;
            _owned = true;
            PlayCue(_settings != null ? _settings.open : default);
            if (TryGetComponent(out ShiftCharge charge)) charge.ShowHint(TakeHint);
            GameEvents.RaiseGliderTaken(this);
        }

        /// <summary>Has one or not, quietly: a door carried it in (HouseCarry), New Game or a loaded save starts without it.</summary>
        public void SetOwned(bool owned)
        {
            if (!owned) Fold("stopped", false, true);
            _owned = owned;
        }

        /// <summary>The motor asks on a Jump press in the air (past the coyote time) and on every press while gliding.
        /// True: the press was the glider's (it opened or folded) and is no jump.</summary>
        private bool OnAirJump()
        {
            if (Gliding) { Fold("jump", true, false); return true; }
            if (!CanOpen()) return false;
            Open();
            return true;
        }

        private bool CanOpen()
        {
            if (!Owned || _settings == null || !_motor.isActiveAndEnabled || _motor.Grounded) return false;
            if (_shifter != null && _shifter.Size != ShiftSize.Toy) return false;   // a kid is too heavy for paper
            if (GameMode.Current != GameMode.Mode.Play || PauseMenu.IsOpen || TitleMenu.IsOpen || Time.timeScale <= 0f) return false;
            if (_moves != null && !_moves.Free) return false;
            // Not this near the ground: the press stays a jump on landing (the jump buffer).
            float scale = Mathf.Max(1e-4f, _motor.Scale), reach = _settings.minOpenHeight * scale;
            int n = Physics.RaycastNonAlloc(transform.position + Vector3.up * (0.2f * scale), Vector3.down, _hits, reach + 0.2f * scale, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++) if (!_hits[i].transform.IsChildOf(transform)) return false;
            return true;
        }

        private void Open()
        {
            Gliding = true;
            OpenedAt = transform.position; OpenedTime = Time.time;
            _motor.Glide = _settings;
            EnsureRig();
            if (_rig != null) _rig.gameObject.SetActive(true);
            if (_animation != null) _animation.Ride(true);
            Vector3 v = _motor.Velocity;
            _lastYaw = new Vector2(v.x, v.z).sqrMagnitude > 1e-6f ? Mathf.Atan2(v.x, v.z) * Mathf.Rad2Deg : transform.eulerAngles.y;
            PlayCue(_settings.open);
            EnsureSound();
            if (_wind != null && _wind.clip != null && !_wind.isPlaying) _wind.Play();
            GameEvents.RaiseGlideChanged(this, true);
        }

        /// <summary>Closes the plane from outside (the Dart Line latching on, D-0109), as a Jump in the air does.</summary>
        public void FoldNow(string why) => Fold(why, ease: true, silent: false);

        /// <summary>Closes the plane. <paramref name="ease"/>: the body tips back upright over a moment (folded in the air by
        /// Jump); otherwise at once (landed, grew, parked: first person must find the body as it was).</summary>
        private void Fold(string why, bool ease, bool silent)
        {
            if (!Gliding) return;
            Gliding = false;
            LastFold = why;
            if (_motor != null) _motor.Glide = null;
            if (_rig != null) _rig.gameObject.SetActive(false);
            if (_animation != null) _animation.Ride(false);
            if (!ease) { _bank = _pitch = 0f; ReleaseModel(); }
            if (!silent && _settings != null) PlayCue(_settings.fold);
            GameEvents.RaiseGlideChanged(this, false);
        }

        private void OnLanded(float speed) { if (Gliding) Fold("landed", false, false); }
        private void OnWarped(Vector3 position, Quaternion rotation) => Fold("stopped", false, true);

        private void Update()
        {
            if (Gliding)
            {
                // Folded by what happens to the body: growing (a kid is too heavy), parked (driving, first person, a menu), a hit.
                if (_shifter != null && _shifter.Size != ShiftSize.Toy) Fold("grew", false, false);
                else if (!_motor.isActiveAndEnabled || GameMode.Current != GameMode.Mode.Play || (_moves != null && !_moves.Free)) Fold("stopped", false, true);
            }
            if (_wind == null || _settings == null) return;
            float scale = Mathf.Max(1e-4f, _motor.Scale);
            Vector3 v = _motor.Velocity / scale;
            float speed = new Vector2(v.x, v.z).magnitude;
            float want = Gliding ? _settings.windVolume * Mathf.Lerp(0.5f, 1f, Mathf.Clamp01(speed / Mathf.Max(0.01f, _settings.glideSpeed))) : 0f;
            _wind.volume = Mathf.MoveTowards(_wind.volume, want, Time.unscaledDeltaTime * (Gliding ? 0.8f : 1.6f));
            _wind.pitch = Mathf.Lerp(0.9f, 1.15f, Mathf.Clamp01(speed / Mathf.Max(0.01f, _settings.glideSpeed)));
            if (!Gliding && _wind.isPlaying && _wind.volume <= 0f) _wind.Stop();
        }

        /// <summary>The bank into turns and the nose along the flight path, on the plane and the body together (about the feet).</summary>
        private void LateUpdate()
        {
            if (_settings == null) return;
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            // The plane points the way it flies (the body may turn on it, to shoot); the bank follows how fast that turns.
            Vector3 v = _motor.Velocity / Mathf.Max(1e-4f, _motor.Scale);
            Vector3 flat = new Vector3(v.x, 0f, v.z);
            float yaw = flat.sqrMagnitude > 1e-4f ? Mathf.Atan2(flat.x, flat.z) * Mathf.Rad2Deg : transform.eulerAngles.y;
            float wantBank = 0f, wantPitch = 0f;
            if (Gliding)
            {
                float turn = Mathf.DeltaAngle(_lastYaw, yaw) / dt;   // degrees a second, + to the right
                wantBank = -Mathf.Clamp(turn / Mathf.Max(1f, _settings.turnRate), -1f, 1f) * _settings.bankDegrees;
                float path = Mathf.Atan2(-v.y, Mathf.Max(0.01f, flat.magnitude)) * Mathf.Rad2Deg;   // + nose down
                wantPitch = Mathf.Clamp(path * _settings.pitchShare, -_settings.maxPitch, _settings.maxPitch);
            }
            _lastYaw = yaw;
            if (!Gliding && !_modelHeld) return;
            float k = 1f - Mathf.Exp(-(Gliding ? 6f : 14f) * dt);
            _bank = Mathf.Lerp(_bank, wantBank, k);
            _pitch = Mathf.Lerp(_pitch, wantPitch, k);
            if (!Gliding && Mathf.Abs(_bank) < 0.3f && Mathf.Abs(_pitch) < 0.3f) { _bank = _pitch = 0f; ReleaseModel(); return; }
            Quaternion tilt = Quaternion.Euler(_pitch, 0f, _bank);
            if (_rig != null && Gliding) _rig.rotation = Quaternion.Euler(0f, yaw, 0f) * tilt;
            if (_plane != null) _plane.localPosition = _planeBase - _settings.rideOffset;   // live: the rider against the plane
            TiltModel(Gliding ? tilt * Quaternion.Euler(_settings.rideLean, 0f, 0f) : tilt);
        }

        private void TiltModel(Quaternion tilt)
        {
            if (_model == null)
            {
                var body = GetComponent<PlayerBody>();
                Animator a = body != null && body.Animator != null ? body.Animator : GetComponentInChildren<Animator>();
                _model = a != null && a.transform != transform ? a.transform : null;
                if (_model == null) return;
            }
            if (!_modelHeld) { _modelRot = _model.localRotation; _modelPos = _model.localPosition; _modelHeld = true; }
            _model.localRotation = tilt * _modelRot;
            _model.localPosition = tilt * _modelPos;
        }

        private void ReleaseModel()
        {
            if (!_modelHeld || _model == null) { _modelHeld = false; return; }
            _model.localRotation = _modelRot;
            _model.localPosition = _modelPos;
            _modelHeld = false;
        }

        /// <summary>The plane, made once: the pack's model with its colliders taken off (the capsule must never meet its
        /// own glider), turned nose-ahead, sized to Plane Length, its top under the feet.</summary>
        private void EnsureRig()
        {
            if (_rig != null || _settings == null || _settings.planePrefab == null) return;
            _rig = new GameObject("GlideRig").transform;
            _rig.SetParent(transform, false);
            _rig.gameObject.SetActive(false);   // no frame with a collider under the feet
            GameObject plane = Instantiate(_settings.planePrefab, _rig, false);
            plane.name = "PaperPlane";
            foreach (Collider c in plane.GetComponentsInChildren<Collider>(true)) DestroyImmediate(c);
            Transform t = plane.transform;
            t.localRotation = Quaternion.Euler(_settings.planeEuler);
            t.localScale = Vector3.one;
            t.localPosition = Vector3.zero;
            Bounds b = BoundsIn(t, _rig);
            float s = b.size.z > 1e-4f ? _settings.planeLength / b.size.z : 1f;
            float length = _settings.planeLength;
            t.localScale = Vector3.one * s;
            // The deck: the mesh's top right under the feet (the box's top is the wing tips', well above the body of a
            // paper plane, and the toy stood on air). Taken from the vertices near the standing spot.
            Vector3 mid = b.center * s; float standZ = mid.z - _settings.standBack * length;
            float deck = DeckHeight(t, _rig, s, mid.x, standZ, length * 0.14f, out bool found);
            if (!found) deck = b.max.y * s;
            _planeBase = new Vector3(-mid.x, _settings.sinkIn * length - deck, _settings.standBack * length - mid.z);
            t.localPosition = _planeBase;
            _plane = t;
        }

        /// <summary>The highest point of the plane's meshes within <paramref name="half"/> of (x, z), in the rig's space
        /// with the plane scaled by <paramref name="s"/> but not yet moved.</summary>
        private static float DeckHeight(Transform plane, Transform rig, float s, float x, float z, float half, out bool found)
        {
            found = false; float top = float.MinValue;
            Matrix4x4 toRig = rig.worldToLocalMatrix;
            Vector3 shift = plane.localPosition;   // zero while measuring
            foreach (MeshFilter mf in plane.GetComponentsInChildren<MeshFilter>(true))
            {
                Mesh m = mf.sharedMesh; if (m == null || (!m.isReadable && !Application.isEditor)) continue;   // a build reads only readable meshes
                Matrix4x4 toLocal = toRig * mf.transform.localToWorldMatrix;
                foreach (Vector3 v in m.vertices)
                {
                    Vector3 p = toLocal.MultiplyPoint3x4(v) - shift;
                    if (Mathf.Abs(p.x - x) > half || Mathf.Abs(p.z - z) > half) continue;
                    if (p.y > top) { top = p.y; found = true; }
                }
            }
            return top;
        }

        /// <summary>The meshes' box under <paramref name="root"/>, in <paramref name="space"/>'s own coordinates.</summary>
        private static Bounds BoundsIn(Transform root, Transform space)
        {
            Bounds b = default; bool any = false;
            Matrix4x4 toSpace = space.worldToLocalMatrix;
            foreach (MeshFilter mf in root.GetComponentsInChildren<MeshFilter>(true))
            {
                Mesh m = mf.sharedMesh;
                if (m == null) continue;
                Matrix4x4 toLocal = toSpace * mf.transform.localToWorldMatrix;
                Bounds mb = m.bounds;
                for (int k = 0; k < 8; k++)
                {
                    Vector3 corner = mb.center + Vector3.Scale(mb.extents, new Vector3((k & 1) == 0 ? -1f : 1f, (k & 2) == 0 ? -1f : 1f, (k & 4) == 0 ? -1f : 1f));
                    Vector3 p = toLocal.MultiplyPoint3x4(corner);
                    if (!any) { b = new Bounds(p, Vector3.zero); any = true; } else b.Encapsulate(p);
                }
            }
            return b;
        }

        private void EnsureSound()
        {
            if (_wind != null || _settings == null) return;
            var go = new GameObject("GlideSound");
            go.transform.SetParent(transform, false);
            _wind = go.AddComponent<AudioSource>();
            _wind.playOnAwake = false; _wind.loop = true; _wind.spatialBlend = 0f; _wind.volume = 0f; _wind.clip = _settings.windLoop;
            _cue = go.AddComponent<AudioSource>();
            _cue.playOnAwake = false; _cue.spatialBlend = 0f;
        }

        private void PlayCue(CourseSounds.Cue cue)
        {
            if (cue.clip == null || cue.volume <= 0f) return;
            EnsureSound();
            if (_cue == null) return;
            _cue.pitch = 1f + Random.Range(-cue.pitchSpread, cue.pitchSpread);
            _cue.PlayOneShot(cue.clip, cue.volume);
        }
    }
}
