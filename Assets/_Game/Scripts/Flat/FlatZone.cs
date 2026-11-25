#if FLAT_ZONE_WIP
// PARKED 24 Nov 2026 (the built-in assistant ran out of credits part-way through this file): it does not compile yet,
// so it is held out of the build until it is finished. It still needs: a 'cutaway' field, a 'pixelMaterial' field (the
// PixelShift pass, plan step 4), and the FlatLetter and FlagPole parts (plan steps 5 and 6). Nothing in a scene uses it.
// To pick it up: finish those, then delete this first line block and the #endif at the very end.
// The plan: Assets/Plans/flat-zone-hallway-scroller.md. What is done and compiled: FlatLock, FlatRail, FlatViewSettings,
// the motor's lock (PlayerMotor.Flat) and the rail camera (ShoulderCamera.SetRail): all do nothing until a zone uses them.
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Rendering;

namespace ShiftJam
{
    /// <summary>
    /// A Flat Zone (D-0198): a stretch of the house played side-on, as a retro side-scroller, while the 3D room stays round
    /// it (Mario Odyssey's 8-bit walls, Donkey Kong Country's vines). While a local player's body is inside the box:
    /// - the body keeps to the lane's plane (PlayerMotor.Flat; the stick's depth part is gone),
    /// - its camera rides the rail side-on (ShoulderCamera.SetRail), mouse look dropped,
    /// - what stands between the lane and the camera is cut away (drawn as shadows only: the room's light is unchanged),
    /// - a drawn gun is put away (aiming means nothing side-on) and comes back on the way out,
    /// - the picture goes to the pixel look (PixelShift full-screen pass), if this zone asks for it.
    /// Containment is tested every frame (not trigger events), so a respawn, a Continue or a portal that lands inside
    /// works. Off in first person, Build Mode, a car, or at the wrong size: the player is let go and taken back once it fits.
    /// Drop one at the top level of a scene; Shift Jam > Flat > Land Hallway Scroller places the hallway's.
    /// </summary>
    [DefaultExecutionOrder(-50)]   // before the motor and the camera: a body that walks in is held from its first step
    public sealed class FlatZone : MonoBehaviour
    {
        private static readonly int WeightId = Shader.PropertyToID("_Weight");
        private static readonly int RowsId = Shader.PropertyToID("_Rows");
        private static readonly int PaletteId = Shader.PropertyToID("_Palette");
        private static readonly int PaletteSizeId = Shader.PropertyToID("_PaletteSize");
        private static readonly int PaletteStrengthId = Shader.PropertyToID("_PaletteStrength");
        private static readonly int DitherId = Shader.PropertyToID("_Dither");

        [Header("The stretch")]
        [SerializeField, Tooltip("Resources/FlatView if empty.")] private FlatViewSettings settings;
        [SerializeField, Tooltip("The box, in this object's own space: its middle.")] private Vector3 centre = Vector3.zero;
        [SerializeField, Tooltip("The box's size, in this object's own space. The body's middle inside it = in the zone.")] private Vector3 size = new Vector3(110f, 16f, 22f);
        [SerializeField, Tooltip("Its position is on the lane's plane; its forward (blue arrow) points to the camera's side. This object if empty.")] private Transform lane;
        [SerializeField, Tooltip("How far along the lane (from the lane's point, to the camera's right) the camera's middle may go.")] private float railStart = -40f, railEnd = 40f;
        [SerializeField, Tooltip("World heights the frame stays between where it fits (the hallway: floor 0, ceiling 14). Ceiling at or under the floor: no limit.")] private float floor = 0f, ceiling = 14f;
        [SerializeField, Tooltip("Only a toy is held (the hallway is toy-only anyway).")] private bool toyOnly = true;

        [Header("Look")]
        [SerializeField, Tooltip("The pixel look while inside (PixelShift).")] private bool pixelLook = true;
        [SerializeField, Tooltip("The PixelShift Full Screen Pass Renderer Feature on PC_Renderer. Switched on only while the look shows.")] private UnityEngine.Rendering.Universal.ScriptableRendererFeature pixelFeature;
        [SerializeField, Min(0f), Tooltip("Things whose near side is further than this from the lane, towards the camera, are cut away.")] private float cutFrom = 3f;
        [SerializeField, Tooltip("Always cut away (the near wall).")] private Renderer[] alwaysCut;

        [Header("Moments")]
        [SerializeField, Tooltip("Shown on the way in (a Super Mario Bros. title card). Empty: nothing.")] private string card = "WORLD 1-1";
        [SerializeField, Tooltip("Shown when Clear is called (the flagpole).")] private string clearText = "COURSE CLEAR!";
        [SerializeField] private UnityEvent onEntered, onExited, onCleared;

        private sealed class Rider { public ShoulderCamera rig; public PlayerMotor motor; public Shifter shifter; public WeaponHolder holder; public bool inside, putAway; }
        private readonly List<Rider> _riders = new List<Rider>();
        private readonly List<(Renderer r, ShadowCastingMode mode)> _cut = new List<(Renderer, ShadowCastingMode)>();
        private bool _cutFound, _cutOn;
        private float _pixel;         // the pixel look, 0..1, eased
        private float _rigsAt = -10f;
        private float _cardAt = -100f;
        private FlatRail _rail;
        private Texture2D _madePalette;

        /// <summary>Every zone that is on.</summary>
        public static readonly List<FlatZone> All = new List<FlatZone>();
        /// <summary>A zone some local player is in now, or null (the HUD reads it).</summary>
        public static FlatZone Current { get; private set; }
        /// <summary>Raised on a Clear (the flagpole), with the zone.</summary>
        public static event Action<FlatZone> Cleared;

        public FlatViewSettings Settings => settings != null ? settings : (settings = Resources.Load<FlatViewSettings>("FlatView")) != null ? settings : (settings = ScriptableObject.CreateInstance<FlatViewSettings>());
        public bool AnyInside { get { foreach (var r in _riders) if (r.inside) return true; return false; } }
        /// <summary>The zone's name for saves (its object's name).</summary>
        public string Id => name;
        private Transform Lane => lane != null ? lane : transform;

        private void OnEnable() { if (!All.Contains(this)) All.Add(this); }

        private void OnDisable()
        {
            All.Remove(this);
            foreach (var r in _riders) if (r.inside) Exit(r);
            _riders.Clear();
            SetCut(false);
            _pixel = 0f; ApplyPixel();
            if (Current == this) Current = null;
        }

        private void OnDestroy() { if (_madePalette != null) Destroy(_madePalette); }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { All.Clear(); Current = null; Cleared = null; }

        /// <summary>True when <paramref name="point"/> (the world) is inside the box.</summary>
        public bool Contains(Vector3 point)
        {
            Vector3 local = transform.InverseTransformPoint(point) - centre;
            Vector3 half = size * 0.5f;
            return Mathf.Abs(local.x) <= half.x && Mathf.Abs(local.y) <= half.y && Mathf.Abs(local.z) <= half.z;
        }

        /// <summary>The lane's plane as a lock.</summary>
        public FlatLock Lock => new FlatLock(Lane.position, Lane.forward, Settings.laneSnap);

        /// <summary>The flagpole (or anything) calls this: "COURSE CLEAR!" and the cleared events. The camera swings back as
        /// the player walks out of the box.</summary>
        public void Clear()
        {
            Rider who = null;
            foreach (var r in _riders) if (r.inside) { who = r; break; }
            if (!string.IsNullOrEmpty(clearText)) { if (who != null) Toasts.ShowFor(who.motor, clearText, ToastKind.Core); else Toasts.Show(clearText, ToastKind.Core); }
            onCleared?.Invoke();
            Cleared?.Invoke(this);
        }

        private void Update()
        {
            FindRigs();
            bool any = false;
            foreach (var r in _riders)
            {
                bool fits = Fits(r) && Contains(BodyMiddle(r.motor));
                if (fits && !r.inside) Enter(r);
                else if (!fits && r.inside) Exit(r);
                any |= r.inside;
            }
            if (any) Current = this; else if (Current == this) Current = null;

            float was = _pixel;
            _pixel = Mathf.MoveTowards(_pixel, any && pixelLook ? 1f : 0f, Time.unscaledDeltaTime / Mathf.Max(0.01f, Settings.pixelSeconds));
            if (_pixel != was) ApplyPixel();
        }

        /// <summary>The local players: one per camera rig (a party has one each; online, only this PC's own).</summary>
        private void FindRigs()
        {
            if (Time.unscaledTime - _rigsAt < 1f && _riders.TrueForAll(r => r.rig != null && r.motor != null)) return;
            _rigsAt = Time.unscaledTime;
            for (int i = _riders.Count - 1; i >= 0; i--)
                if (_riders[i].rig == null || _riders[i].motor == null || _riders[i].rig.Target != _riders[i].motor.transform)
                {
                    if (_riders[i].inside) Exit(_riders[i]);
                    _riders.RemoveAt(i);
                }
            foreach (var rig in FindObjectsByType<ShoulderCamera>(FindObjectsSortMode.None))
            {
                if (rig.Target == null || _riders.Exists(r => r.rig == rig)) continue;
                var motor = rig.Target.GetComponent<PlayerMotor>();
                if (motor == null) continue;
                _riders.Add(new Rider { rig = rig, motor = motor, shifter = motor.GetComponent<Shifter>(), holder = motor.GetComponent<WeaponHolder>() });
            }
        }

        /// <summary>On foot, in Play, third person, in charge of its own body, and the right size.</summary>
        private bool Fits(Rider r)
        {
            if (r.motor == null || !r.motor.isActiveAndEnabled || r.rig == null || !r.rig.isActiveAndEnabled) return false;
            if (GameMode.Current != GameMode.Mode.Play) return false;
            if (r.holder != null && r.holder.FirstPerson) return false;
            if (toyOnly && r.shifter != null && r.shifter.Size != ShiftSize.Toy && !r.shifter.Shifting) return false;
            return true;
        }

        private static Vector3 BodyMiddle(PlayerMotor m) => m.transform.position + Vector3.up * (0.9f * m.Scale);

        private void Enter(Rider r)
        {
            r.inside = true;
            r.motor.Flat = Lock;
            r.rig.SetRail(Rail());
            if (r.holder != null && r.holder.Drawn && r.holder.Current != null) { r.holder.SetDrawn(false); r.putAway = true; }
            SetCut(true);
            if (!string.IsNullOrEmpty(card) && Time.unscaledTime - _cardAt > 20f) { _cardAt = Time.unscaledTime; Toasts.ShowFor(r.motor, null, ToastKind.Core, card); }
            onEntered?.Invoke();
        }

        private void Exit(Rider r)
        {
            r.inside = false;
            if (r.motor != null) r.motor.Flat = null;
            if (r.rig != null && r.rig.Rail == _rail) r.rig.ClearRail();
            if (r.putAway && r.holder != null) r.holder.SetDrawn(true);
            r.putAway = false;
            if (!AnyInside) SetCut(false);
            onExited?.Invoke();
        }

        private FlatRail Rail()
        {
            if (_rail == null) _rail = new FlatRail();
            Transform l = Lane;
            Vector3 n = l.forward; n.y = 0f;
            _rail.point = l.position;
            _rail.normal = n.sqrMagnitude > 1e-6f ? n.normalized : Vector3.forward;
            _rail.start = railStart; _rail.end = railEnd;
            _rail.floor = floor; _rail.ceiling = ceiling;
            _rail.view = Settings;
            return _rail;
        }

        // ------------------------------------------------------------------ the cutaway

        private void SetCut(bool on)
        {
            if (!cutaway) on = false;
            if (on == _cutOn) return;
            if (on && !_cutFound) FindCut();
            _cutOn = on;
            foreach (var (r, mode) in _cut) if (r != null) r.shadowCastingMode = on ? ShadowCastingMode.ShadowsOnly : mode;
        }

        /// <summary>What to cut: the near wall(s) given, and every renderer between the lane (past Cut From) and where the
        /// rail camera stands, across the stretch the camera can see. Players, cores, checkpoints and this zone's own
        /// pieces are never cut. Found once, the first time anyone walks in.</summary>
        private void FindCut()
        {
            _cutFound = true;
            var seen = new HashSet<Renderer>();
            if (alwaysCut != null) foreach (var r in alwaysCut) if (r != null && seen.Add(r)) _cut.Add((r, r.shadowCastingMode));

            FlatViewSettings v = Settings;
            FlatRail rail = Rail();
            Vector3 along = rail.Along, n = rail.normal;
            float frame = v.frameHeight;   // toy size (the hallway); a kid zone would need its own numbers
            float distance = frame * 0.5f / Mathf.Tan(Mathf.Clamp(v.fieldOfView, 1f, 120f) * 0.5f * Mathf.Deg2Rad);
            float halfWide = frame * 0.5f * 2.4f;   // as wide as an ultrawide frame
            float aLo = Mathf.Min(railStart, railEnd) - halfWide, aHi = Mathf.Max(railStart, railEnd) + halfWide;
            float yLo = ceiling > floor ? floor - 2f : -1e6f, yHi = ceiling > floor ? ceiling + 2f : 1e6f;
            foreach (var r in FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                if (seen.Contains(r) || r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;
                if (r.GetComponentInParent<PlayerMotor>() != null || r.GetComponentInParent<FlatZone>() != null || r.GetComponentInParent<FlatLetter>() != null
                    || r.GetComponentInParent<FlagPole>() != null || r.GetComponentInParent<ShiftSource>() != null || r.GetComponentInParent<Checkpoint>() != null) continue;
                Bounds b = r.bounds;
                Vector3 c = b.center - rail.point, e = b.extents;
                float ac = Vector3.Dot(c, along), ae = Mathf.Abs(e.x * along.x) + Mathf.Abs(e.z * along.z);
                float nc = Vector3.Dot(c, n), ne = Mathf.Abs(e.x * n.x) + Mathf.Abs(e.z * n.z);
                if (ac + ae < aLo || ac - ae > aHi) continue;
                if (b.max.y < yLo || b.min.y > yHi) continue;
                if (nc - ne < cutFrom || nc - ne > distance) continue;   // its near side: past Cut From, short of the camera
                seen.Add(r);
                _cut.Add((r, r.shadowCastingMode));
            }
        }

        // ------------------------------------------------------------------ the pixel look

        private void ApplyPixel()
        {
            if (pixelMaterial == null) return;
            FlatViewSettings v = Settings;
            float k = _pixel * _pixel * (3f - 2f * _pixel);
            pixelMaterial.SetFloat(WeightId, k);
            if (k > 0f)
            {
                Texture2D palette = v.palette != null ? v.palette : MadePalette();
                pixelMaterial.SetFloat(RowsId, v.pixelRows);
                pixelMaterial.SetTexture(PaletteId, palette);
                pixelMaterial.SetFloat(PaletteSizeId, palette.width);
                pixelMaterial.SetFloat(PaletteStrengthId, v.paletteStrength);
                pixelMaterial.SetFloat(DitherId, v.dither);
            }
            if (pixelFeature != null) pixelFeature.SetActive(k > 0f);
        }

        /// <summary>16 colours from his orb palette (white > cyan > blue > purple, a gold rim) and the toy room's warm
        /// woods and greys, so the hallway still reads. A stand-in until he paints a strip of his own (FlatView > Palette).</summary>
        private Texture2D MadePalette()
        {
            if (_madePalette != null) return _madePalette;
            Color32[] c =
            {
                new Color32(0x0d, 0x07, 0x1a, 255), new Color32(0x2a, 0x12, 0x4a, 255), new Color32(0x4a, 0x1f, 0x86, 255), new Color32(0x6b, 0x3c, 0xc9, 255),
                new Color32(0x2b, 0x4f, 0xd6, 255), new Color32(0x3f, 0x9b, 0xf0, 255), new Color32(0x6f, 0xe6, 0xf7, 255), new Color32(0xf4, 0xfb, 0xff, 255),
                new Color32(0xf2, 0xc8, 0x4b, 255), new Color32(0xc2, 0x7a, 0x2c, 255), new Color32(0x7a, 0x47, 0x24, 255), new Color32(0x3e, 0x26, 0x1a, 255),
                new Color32(0xe8, 0x6a, 0x8a, 255), new Color32(0x5d, 0x8c, 0x3a, 255), new Color32(0x8a, 0x86, 0x90, 255), new Color32(0x45, 0x42, 0x4c, 255),
            };
            _madePalette = new Texture2D(c.Length, 1, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = "FlatPalette (made)" };
            _madePalette.SetPixels32(c);
            _madePalette.Apply(false, true);
            return _madePalette;
        }

        private void OnDrawGizmos()
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = new Color(0.95f, 0.75f, 0.2f, 0.12f);
            Gizmos.DrawCube(centre, size);
            Gizmos.color = new Color(0.95f, 0.75f, 0.2f, 0.9f);
            Gizmos.DrawWireCube(centre, size);
            Gizmos.matrix = Matrix4x4.identity;
            Transform l = Lane;
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(l.position, l.position + l.forward * 4f);
            Vector3 a = Vector3.Cross(Vector3.up, -l.forward).normalized;
            Gizmos.DrawLine(l.position + a * railStart, l.position + a * railEnd);
        }
    }
}
#endif
