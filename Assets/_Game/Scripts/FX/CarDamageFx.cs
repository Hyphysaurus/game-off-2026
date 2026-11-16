using System;
using System.Collections.Generic;
using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// A car taking damage, seen and heard (D-0059). Bonnet smoke goes light below CarDamageFxSettings.lightSmokeBelow of
    /// full health and dark below darkSmokeBelow (and while wrecked), and sparks spit from the bonnet below sparksBelow.
    /// Every hit the car reports (ArcadeCar.Struck) throws sparks and paint flecks from where it landed and plays a knock,
    /// a crash or a big crash by how hard it was; a big hit may knock one of the car's add-on parts off (it tumbles away
    /// and shrinks), and the wreck throws a burst, a few parts and the loudest sound. Hits that come from elsewhere (a
    /// dart, a ram from another car) are taken from the Damageable. Repairs and resets put the parts back.
    /// The wreck also breaks the body into its pre-cut pieces (CarWreck, D-0060) where the body has been cut.
    /// The car adds this in Start and calls Bind, then Place whenever its parts change. Effects scale with the car's
    /// drawn length (the pack's effects are sized for a real car about 4 long).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CarDamageFx : MonoBehaviour
    {
        internal const string Prefix = "DamageFx ";   // every object the damage effects add to a car starts with this
        internal const float PackCarLength = 4f;      // Polygon Arsenal's effects are sized for a real car about this long
        private const float PackGravity = 9.81f;     // ...under real gravity
        private const int MostLooseParts = 16;       // across every car: older parts are not tidied early, new ones are skipped

        private static CarDamageFxSettings _settings;
        private static Mesh _chip;
        
        private ArcadeCar _car;
        private Damageable _health;
        private Transform _lightRoot, _darkRoot;
        private ParticleSystem[] _light, _dark;
        private Quaternion _lightRot, _darkRot;
        private ParticleSystem _flecks;
        private AudioSource _audio;
        private Vector3 _bonnet;                      // car space
        private float _length = PackCarLength;       // the car's drawn length, car space
        private Color _paint;
        private float _nextSpit;
        private int _shownFrame = -1;                 // the frame a hit was last shown (a collision and its damage arrive together)
        private readonly List<Transform> _parts = new List<Transform>();
        // Kept by Unity so a Build Mode copy of a car with parts off knows what it has to show again.
        [SerializeField, HideInInspector] private List<Renderer> knocked = new List<Renderer>();
        private CarWreck _wreck;
        private bool _lightOn, _darkOn;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ClearStatics() { _settings = null; LoosePart.Alive = 0; }

        private static CarDamageFxSettings Settings => _settings != null ? _settings : (_settings = CarDamageFxSettings.Load());

        /// <summary>World size of the effects against the pack's: the car's drawn length over a real car's.</summary>
        private float WorldK => Mathf.Max(0.05f, _length * Mathf.Abs(transform.lossyScale.y) / PackCarLength);

        /// <summary>Starts listening to <paramref name="car"/>. Safe to call again (a Build Mode copy arrives with the
        /// original's effects: those are dropped and made fresh).</summary>
        public void Bind(ArcadeCar car)
        {
            Unbind();
            _car = car;
            _health = car.Health;
            var old = new List<GameObject>();
            foreach (Transform child in transform) if (child.name.StartsWith(Prefix)) old.Add(child.gameObject);
            foreach (var go in old) { go.SetActive(false); Destroy(go); }

            var s = Settings;
            _light = MakeLoop(s.lightSmoke, "Light Smoke", out _lightRoot, out _lightRot);
            _dark = MakeLoop(s.darkSmoke, "Dark Smoke", out _darkRoot, out _darkRot);
            _flecks = MakeFlecks(s.fleckMaterial);
            var audioGo = new GameObject(Prefix + "Audio");
            audioGo.transform.SetParent(transform, false);
            _audio = audioGo.AddComponent<AudioSource>();
            _audio.playOnAwake = false;
            _audio.spatialBlend = 0.6f;   // mostly placed at the car, partly heard as the driver's own
            _audio.rolloffMode = AudioRolloffMode.Linear;

            _car.Struck += OnImpact;
            _health.Damaged += OnDamaged;
            _health.Died += OnDied;
            _health.Restored += OnRestored;
            if (!TryGetComponent(out _wreck)) _wreck = gameObject.AddComponent<CarWreck>();
            Place();
            // a copy made of a car with parts off or in pieces (Build Mode) starts whole, unless it is a wreck itself
            if (!_health.Dead) { PutPartsBack(); _wreck.Mend(); }
        }

        private void Unbind()
        {
            if (_car != null) _car.Struck -= OnImpact;
            if (_health != null) { _health.Damaged -= OnDamaged; _health.Died -= OnDied; _health.Restored -= OnRestored; }
        }

        private void OnDestroy()
        {
            Unbind();
            PutPartsBack();
        }

        /// <summary>Re-reads the car's size, its bonnet, its paint and its loose parts (after the garage swaps parts).</summary>
        public void Place()
        {
            if (_car == null) return;
            if (!BuildBounds.Local(transform, out Bounds b)) b = new Bounds(Vector3.zero, Vector3.one * PackCarLength);
            _length = Mathf.Max(0.1f, b.size.z);
            _bonnet = new Vector3(b.center.x, b.center.y + b.extents.y * 0.35f, b.max.z - b.size.z * 0.2f);
            float local = _length / PackCarLength;
            PlaceLoop(_lightRoot, local);
            PlaceLoop(_darkRoot, local);
            float s = Mathf.Abs(transform.lossyScale.y) * _length;
            _audio.minDistance = 1.5f * s;
            _audio.maxDistance = 40f * s;
            var body = BodyOf(transform);
            _paint = Settings.PaintOf(body != null && body.sharedMesh != null ? body.sharedMesh.name : null);
            FindParts();
        }

        // ---------- what the car reports ----------

        private void OnImpact(CarImpact hit)
        {
            if (Time.frameCount == _shownFrame) return;
            _shownFrame = Time.frameCount;
            var s = Settings;
            float min = _car.Settings != null ? _car.Settings.crashMinSpeed : 14f;
            bool big = hit.Hurt && hit.Felt >= s.bigHit;
            float k = WorldK;
            Vector3 up = (hit.Normal + Vector3.up * 0.5f).normalized;
            if (big)
            {
                Burst(s.bigHitSparks, hit.Point, up, k);
                Flecks(hit.Point, hit.Normal, s.flecks.z, 1.3f);
                Sound(s.bigCrash, 1f, 0.9f);
                if (UnityEngine.Random.value < s.partChance) KnockPart(hit.Point, hit.Normal, false);
            }
            else if (hit.Hurt)
            {
                Burst(s.hitSparks, hit.Point, up, k);
                Flecks(hit.Point, hit.Normal, s.flecks.y, 1f);
                float t = Mathf.InverseLerp(min, s.bigHit, hit.Felt);
                Sound(s.crash, Mathf.Lerp(0.6f, 0.95f, t), Mathf.Lerp(1.08f, 0.95f, t));
            }
            else
            {
                // a tap: a cone, a glance, a soft bump. A few sparks off a wall; a cone only knocks.
                if (hit.Fixed) Burst(s.hitSparks, hit.Point, up, k * 0.55f);
                Flecks(hit.Point, hit.Normal, s.flecks.x, 0.7f);
                Sound(s.knock, Mathf.Lerp(0.25f, 0.6f, Mathf.InverseLerp(0f, min, hit.Felt)), 1.12f);
            }
        }

        /// <summary>Hits that did not come from the car's own collisions: a dart, a cannonball, a ram by another car.</summary>
        private void OnDamaged(DamageInfo hit)
        {
            if (Time.frameCount == _shownFrame) return;
            _shownFrame = Time.frameCount;
            var s = Settings;
            Vector3 centre = transform.TransformPoint(Vector3.up * (_length * 0.15f));
            Vector3 point = hit.Point == Vector3.zero ? centre : hit.Point;
            Vector3 normal = point - centre;
            normal = normal.sqrMagnitude > 1e-6f ? normal.normalized : Vector3.up;
            bool big = hit.Amount >= _health.MaxHealth * 0.2f;
            Burst(big ? s.bigHitSparks : s.hitSparks, point, (normal + Vector3.up * 0.5f).normalized, WorldK);
            Flecks(point, normal, big ? s.flecks.z : s.flecks.y, big ? 1.3f : 1f);
            Sound(big ? s.bigCrash : s.crash, big ? 1f : 0.8f, big ? 0.9f : 1f);
            if (big && UnityEngine.Random.value < s.partChance) KnockPart(point, normal, false);
        }

        private void OnDied(DamageInfo hit)
        {
            var s = Settings;
            float k = WorldK;
            Vector3 centre = transform.TransformPoint(new Vector3(0f, _length * 0.15f, 0f));
            Burst(s.wreckSparks, centre, Vector3.up, k);
            Burst(s.wreckSmoke, centre, Vector3.up, k);
            Flecks(centre, Vector3.up, s.flecks.z * 2, 1.6f);
            Sound(s.bigCrash, 1f, 0.85f);
            Sound(s.wreck, 1f, 0.9f);
            for (int i = 0; i < s.wreckParts; i++) KnockPart(centre, Vector3.up, true);
            _wreck.Break(_car, s, _paint, k, centre);   // after the parts: what is left on the body goes with it
        }

        private void OnRestored()
        {
            PutPartsBack();
            _wreck.Mend();
            Switch(_light, false, true); Switch(_dark, false, true);
            _lightOn = _darkOn = false;
        }

        // ---------- smoke and spitting sparks, every frame ----------

        private void LateUpdate()
        {
            if (_car == null || _health == null) return;
            var s = Settings;
            bool wrecked = _health.Dead;
            float share = _health.Health / Mathf.Max(1f, _health.MaxHealth);
            bool dark = wrecked || share < s.darkSmokeBelow;
            bool light = !dark && share < s.lightSmokeBelow;
            if (light != _lightOn) { Switch(_light, light, false); _lightOn = light; }
            if (dark != _darkOn) { Switch(_dark, dark, false); _darkOn = dark; }
            // the smoke rises straight up however the car is turned
            if (_lightRoot != null) _lightRoot.rotation = _lightRot;
            if (_darkRoot != null) _darkRoot.rotation = _darkRot;

            if ((wrecked || share < s.sparksBelow) && s.spitSparks != null && Time.time >= _nextSpit)
            {
                if (_nextSpit > 0f)
                {
                    Vector3 dir = (transform.up * 1.5f + UnityEngine.Random.insideUnitSphere).normalized;
                    Burst(s.spitSparks, transform.TransformPoint(_bonnet), dir, WorldK * 0.6f);
                }
                _nextSpit = Time.time + UnityEngine.Random.Range(s.spitEvery.x, s.spitEvery.y);
            }
            else if (!(wrecked || share < s.sparksBelow)) _nextSpit = 0f;
        }

        // ---------- effects ----------

        private ParticleSystem[] MakeLoop(GameObject prefab, string what, out Transform root, out Quaternion rotation)
        {
            root = null; rotation = Quaternion.identity;
            if (prefab == null) return null;
            var go = Instantiate(prefab, transform, false);
            go.name = Prefix + what;
            root = go.transform;
            rotation = prefab.transform.rotation;
            foreach (var c in go.GetComponentsInChildren<Collider>(true)) Destroy(c);
            var systems = go.GetComponentsInChildren<ParticleSystem>(true);
            float gravity = GravityFix(WorldK);
            foreach (var ps in systems)
            {
                var main = ps.main;
                main.playOnAwake = false;
                main.scalingMode = ParticleSystemScalingMode.Hierarchy;   // grows with the car
                main.simulationSpace = ParticleSystemSimulationSpace.World;   // it trails behind a moving car
                main.gravityModifierMultiplier *= gravity;
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
            return systems;
        }

        private void PlaceLoop(Transform root, float local)
        {
            if (root == null) return;
            root.localPosition = _bonnet;
            root.localScale = Vector3.one * local;
        }

        /// <summary>A one-shot effect at <paramref name="at"/>, its emission turned to <paramref name="dir"/>, sized
        /// <paramref name="k"/> times the pack's, cleaned up when it is done.</summary>
        private static void Burst(GameObject prefab, Vector3 at, Vector3 dir, float k)
        {
            if (prefab == null) return;
            Quaternion own = prefab.transform.rotation;
            Vector3 emits = own * Vector3.forward;   // the pack's shapes emit along their forward
            var go = Instantiate(prefab, at, Quaternion.FromToRotation(emits, dir) * own);
            go.transform.localScale = prefab.transform.localScale * k;
            foreach (var c in go.GetComponentsInChildren<Collider>(true)) Destroy(c);
            float life = 0.5f, gravity = GravityFix(k);
            foreach (var ps in go.GetComponentsInChildren<ParticleSystem>(true))
            {
                var main = ps.main;
                main.loop = false;
                main.scalingMode = ParticleSystemScalingMode.Hierarchy;
                main.gravityModifierMultiplier *= gravity;
                life = Mathf.Max(life, main.duration + main.startLifetime.constantMax);
            }
            Destroy(go, life + 0.5f);
        }

        /// <summary>Gravity for an effect made for real gravity at full size, shown <paramref name="k"/> times as big under
        /// this project's gravity: a smaller effect falls as if from a smaller height.</summary>
        private static float GravityFix(float k) => PackGravity * k / Mathf.Max(0.1f, Physics.gravity.magnitude);

        private ParticleSystem MakeFlecks(Material material)
        {
            if (material == null) return null;
            if (_chip == null) _chip = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            var go = new GameObject(Prefix + "Flecks");
            go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true;   // never ends: chips are only ever thrown by Emit
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.4f);
            main.startSize3D = true;
            main.gravityModifier = 1f;     // paint chips drop like everything else in the room
            main.startRotation3D = true;
            main.maxParticles = 200;
            var emission = ps.emission;
            emission.enabled = false;
            var shape = ps.shape;
            shape.enabled = false;
            var spin = ps.rotationOverLifetime;
            spin.enabled = true;
            spin.separateAxes = true;
            spin.x = new ParticleSystem.MinMaxCurve(-12f, 12f);
            spin.y = new ParticleSystem.MinMaxCurve(-12f, 12f);
            spin.z = new ParticleSystem.MinMaxCurve(-12f, 12f);
            var shrink = ps.sizeOverLifetime;
            shrink.enabled = true;
            shrink.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(0.75f, 1f), new Keyframe(1f, 0f)));
            var collide = ps.collision;
            collide.enabled = true;
            collide.type = ParticleSystemCollisionType.World;
            collide.mode = ParticleSystemCollisionMode.Collision3D;
            collide.quality = ParticleSystemCollisionQuality.Low;
            collide.bounce = 0.3f;
            collide.dampen = 0.4f;
            collide.lifetimeLoss = 0f;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Mesh;
            r.mesh = _chip;
            r.alignment = ParticleSystemRenderSpace.Local;
            r.sharedMaterial = material;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ps.Play();
            return ps;
        }

        /// <summary>Throws <paramref name="count"/> chips of the car's paint off <paramref name="at"/>, mostly along
        /// <paramref name="normal"/>, carried along with the car.</summary>
        private void Flecks(Vector3 at, Vector3 normal, int count, float force)
        {
            if (_flecks == null || count <= 0) return;
            var s = Settings;
            float k = WorldK;
            Vector3 carried = _car.Body != null ? _car.Body.linearVelocity * 0.6f : Vector3.zero;
            var emit = new ParticleSystem.EmitParams();
            for (int i = 0; i < count; i++)
            {
                Vector3 dir = (normal + Vector3.up * 0.6f + UnityEngine.Random.insideUnitSphere * 0.9f).normalized;
                emit.position = at;
                emit.velocity = carried + dir * UnityEngine.Random.Range(3f, 7f) * k * force;
                float size = UnityEngine.Random.Range(s.fleckSize.x, s.fleckSize.y) * k;
                emit.startSize3D = new Vector3(size, size * UnityEngine.Random.Range(0.25f, 0.5f), size * UnityEngine.Random.Range(0.6f, 1f));
                emit.rotation3D = UnityEngine.Random.insideUnitSphere * 180f;
                Color c = _paint * UnityEngine.Random.Range(0.8f, 1.1f);
                c.a = 1f;
                emit.startColor = UnityEngine.Random.value < 0.2f ? new Color(0.75f, 0.75f, 0.78f) : c;   // a few bare die-cast chips
                _flecks.Emit(emit, 1);
            }
        }

        private void Sound(AudioClip[] clips, float volume, float pitch)
        {
            if (clips == null || clips.Length == 0 || _audio == null) return;
            var clip = clips[UnityEngine.Random.Range(0, clips.Length)];
            if (clip == null) return;
            _audio.pitch = pitch * UnityEngine.Random.Range(0.95f, 1.05f);
            _audio.PlayOneShot(clip, Mathf.Clamp01(volume * Settings.volume));
        }

        private static void Switch(ParticleSystem[] systems, bool on, bool clear)
        {
            if (systems == null) return;
            foreach (var ps in systems)
            {
                if (ps == null) continue;
                if (on) { if (!ps.isEmitting) ps.Play(false); }
                else ps.Stop(false, clear ? ParticleSystemStopBehavior.StopEmittingAndClear : ParticleSystemStopBehavior.StopEmitting);
            }
        }

        // ---------- loose parts ----------

        /// <summary>The add-on parts that can come off: the outermost transform whose name has one of the settings' part
        /// names, shown, and not under a wheel.</summary>
        private void FindParts()
        {
            _parts.Clear();
            var names = Settings.partNames;
            if (names == null) return;
            foreach (var t in GetComponentsInChildren<Transform>(true))
            {
                if (t == transform || !IsPart(t.name, names) || t.name.StartsWith(Prefix)) continue;
                bool nested = false;
                for (var p = t.parent; p != null && p != transform; p = p.parent)
                    if (IsPart(p.name, names) || IsWheel(p.name)) { nested = true; break; }
                if (!nested) _parts.Add(t);
            }
        }

        private static bool IsPart(string n, string[] names)
        {
            if (IsWheel(n)) return false;
            foreach (var part in names) if (!string.IsNullOrEmpty(part) && n.IndexOf(part, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            return false;
        }

        private static bool IsWheel(string n) => n.IndexOf("wheel", StringComparison.OrdinalIgnoreCase) >= 0;

        /// <summary>Knocks off the shown part nearest <paramref name="at"/> (any one at random for a wreck): its renderers
        /// are hidden and a copy tumbles away and shrinks.</summary>
        private void KnockPart(Vector3 at, Vector3 normal, bool anyOne)
        {
            if (LoosePart.Alive >= MostLooseParts) return;
            Transform pick = null; float best = float.MaxValue;
            var shown = new List<Transform>();
            foreach (var p in _parts)
            {
                if (p == null || !p.gameObject.activeInHierarchy || !HasShown(p)) continue;
                shown.Add(p);
                float d = (p.position - at).sqrMagnitude;
                if (d < best) { best = d; pick = p; }
            }
            if (shown.Count == 0) return;
            if (anyOne) pick = shown[UnityEngine.Random.Range(0, shown.Count)];

            var loose = new GameObject("Loose " + pick.name);
            loose.transform.SetPositionAndRotation(pick.position, pick.rotation);
            var colliders = new List<Collider>();
            foreach (var r in pick.GetComponentsInChildren<MeshRenderer>())
            {
                if (!r.enabled || !r.TryGetComponent(out MeshFilter mf) || mf.sharedMesh == null) continue;
                var piece = new GameObject(r.name);
                piece.transform.SetPositionAndRotation(r.transform.position, r.transform.rotation);
                piece.transform.localScale = r.transform.lossyScale;
                piece.transform.SetParent(loose.transform, true);
                piece.AddComponent<MeshFilter>().sharedMesh = mf.sharedMesh;
                piece.AddComponent<MeshRenderer>().sharedMaterials = r.sharedMaterials;
                colliders.Add(piece.AddComponent<BoxCollider>());
                r.enabled = false;
                knocked.Add(r);
            }
            if (colliders.Count == 0) { Destroy(loose); return; }
            foreach (var own in GetComponentsInChildren<Collider>())
                foreach (var c in colliders) Physics.IgnoreCollision(own, c);
            var rb = loose.AddComponent<Rigidbody>();
            rb.mass = 0.3f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            float k = WorldK;
            Vector3 away = (pick.position - transform.TransformPoint(Vector3.up * (_length * 0.15f)));
            away = away.sqrMagnitude > 1e-6f ? away.normalized : normal;
            Vector3 kick = (away + normal * 0.5f + Vector3.up * 0.8f).normalized * UnityEngine.Random.Range(5f, 9f) * k;
            rb.linearVelocity = (_car.Body != null ? _car.Body.linearVelocity * 0.8f : Vector3.zero) + kick;
            rb.angularVelocity = UnityEngine.Random.insideUnitSphere * 12f;
            loose.AddComponent<LoosePart>().Begin(Settings.partLife);
        }

        private static bool HasShown(Transform part)
        {
            foreach (var r in part.GetComponentsInChildren<MeshRenderer>()) if (r.enabled) return true;
            return false;
        }

        private void PutPartsBack()
        {
            foreach (var r in knocked) if (r != null) r.enabled = true;
            knocked.Clear();
        }

        /// <summary>The car's body: the biggest drawn mesh that is not a wheel or an effect. The damage builder bakes paint
        /// colours by this same mesh's name.</summary>
        public static MeshFilter BodyOf(Transform car)
        {
            MeshFilter best = null; float most = 0f;
            foreach (var mf in car.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null || mf.name.StartsWith(Prefix) || mf.name.IndexOf("decal", StringComparison.OrdinalIgnoreCase) >= 0) continue;
                bool wheel = false;
                for (var t = mf.transform; t != null && t != car; t = t.parent) if (IsWheel(t.name)) { wheel = true; break; }
                if (wheel || !mf.TryGetComponent(out MeshRenderer _)) continue;
                Vector3 size = Vector3.Scale(mf.sharedMesh.bounds.size, mf.transform.lossyScale);
                float v = Mathf.Abs(size.x * size.y * size.z);
                if (v > most) { most = v; best = mf; }
            }
            return best;
        }
    }

    /// <summary>One hit the car felt (ArcadeCar.Struck): how hard it went INTO the surface (units per second at scale 1),
    /// where, the surface's face (pointing out toward the car), whether it hurt, and whether what it hit is fixed (a wall)
    /// rather than loose.</summary>
    public readonly struct CarImpact
    {
        public readonly float Felt;
        public readonly Vector3 Point;
        public readonly Vector3 Normal;
        public readonly bool Hurt;
        public readonly bool Fixed;

        public CarImpact(float felt, Vector3 point, Vector3 normal, bool hurt, bool isFixed)
        { Felt = felt; Point = point; Normal = normal; Hurt = hurt; Fixed = isFixed; }
    }
}
