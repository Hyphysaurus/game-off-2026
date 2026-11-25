using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Rendering;

namespace ShiftJam
{
    /// <summary>
    /// A Flat Zone (D-0198): a stretch of the house played side-on, as a side-scroller, while the 3D room stays round
    /// it. While a local player's body is inside the box:
    /// - the body keeps to the lane's plane (PlayerMotor.Flat: the stick's depth part is gone),
    /// - its camera rides the rail side-on (ShoulderCamera.SetRail), mouse look dropped,
    /// - what stands between the lane and the camera is cut away (not drawn, its shadow kept: the room's light is
    ///   unchanged), and put back exactly as it was once the camera has swung back,
    /// - a drawn gun is put away (aiming means nothing side-on) and comes back on the way out,
    /// - the shift warp plays as the camera swings (D-0203: outward on the way in, inward on the way out, over the
    ///   blend; a size shift running wins), a big card says so ("FLAT SHIFT", "BACK TO 3D"), and a fill light the zone
    ///   owns rides with that player's camera, fading in and out with the swing, so the toy and the far wall read.
    /// Whether the body is in the box is tested every frame (no trigger events), so a respawn, a Continue or a portal
    /// that lands inside works. Not held: in first person, in Build Mode, in a car, or (Toy Only) at the wrong size or
    /// mid-shift; the player is let go and taken back once it fits again. A pause holds everything as it is.
    /// Clear (the exit gate calls it, FlatGate) lets a player go at once, with the exit card, and the zone leaves that
    /// player alone until its body has left the box: walking back in starts a new visit.
    /// The zone's own pieces (anything under this object: its gates, its fill light) are never cut away.
    /// OFF SWITCH: switch this object off (or untick the script) and the stretch plays exactly as it did without it.
    /// Local players only (one per camera rig: single player has one, a party one each); online, another player's copy
    /// on this PC has no rig and is not held, and what the zone does to this PC's player is not sent to the others.
    /// Drop one at the top level of a scene; Shift Jam > Flat > Land Hallway Scroller places the hallway's.
    /// </summary>
    [DefaultExecutionOrder(-50)]   // before the moves, the rope and the motor: a body that walks in is held from its first step
    public sealed class FlatZone : MonoBehaviour
    {
        [Header("The stretch")]
        [SerializeField, Tooltip("The camera's and the hold's numbers. Resources/FlatView if empty.")] private FlatViewSettings settings;
        [SerializeField, Tooltip("The box, in this object's own space: its middle.")] private Vector3 centre = Vector3.zero;
        [SerializeField, Tooltip("The box's size, in this object's own space. The body's middle inside it = in the zone. Take it down past any pit the player can fall in, or the zone lets go half-way down the fall.")] private Vector3 size = new Vector3(110f, 16f, 22f);
        [SerializeField, Min(0f), Tooltip("A body that is in the zone stays in it until it is this far outside the box, so standing on the box's edge does not flick the view in and out.")] private float edgeSlack = 0.3f;
        [SerializeField, Tooltip("Its position is on the lane's plane; its forward (blue arrow) points to the camera's side. This object if empty.")] private Transform lane;
        [SerializeField, Tooltip("How far along the lane the camera's middle may go at this end: from the lane's point, to the camera's right (a minus = to its left).")] private float railStart = -40f;
        [SerializeField, Tooltip("How far along the lane the camera's middle may go at the other end.")] private float railEnd = 40f;
        [SerializeField, Tooltip("World height the frame never shows under, where it fits. A little under the walking floor puts ground under the feet.")] private float floor = 0f;
        [SerializeField, Tooltip("World height the frame never shows over, where it fits. At or under Floor: no limits.")] private float ceiling = 14f;
        [SerializeField, Tooltip("Only a toy is held, and not in the middle of a shift.")] private bool toyOnly = true;

        [Header("The cutaway")]
        [SerializeField, Tooltip("What stands between the lane and the camera is not drawn while the zone is on (its shadow stays). Off: nothing is hidden.")] private bool cutaway = true;
        [SerializeField, Min(0f), Tooltip("Things whose near side is further than this from the lane, towards the camera, are cut away. Things that cross the lane (the floor, the steps) never are.")] private float cutFrom = 3f;
        [SerializeField, Min(1f), Tooltip("The widest screen the cutaway is found for, width over height: 2.4 is an ultrawide monitor. Things past the rail's ends by more than half such a screen are left alone.")] private float cutAspect = 2.4f;
        [SerializeField, Tooltip("Always cut away, wherever they stand (the near wall).")] private Renderer[] alwaysCut;

        [Header("Moments")]
        [SerializeField, Tooltip("The big card shown on the way in. Empty: none.")] private string card = "FLAT SHIFT";
        [SerializeField, Tooltip("The way-in card's colour (his title's candy pink).")] private Color cardColour = new Color32(0xFF, 0x7E, 0xB9, 0xFF);
        [SerializeField, Min(0f), Tooltip("Seconds before the card may show again (a respawn inside, a step out and back in).")] private float cardEvery = 20f;
        [SerializeField, Tooltip("The big card shown when a player goes out through the exit (Clear). Empty: none.")] private string clearText = "BACK TO 3D";
        [SerializeField, Tooltip("The way-out card's colour (the orb's cyan).")] private Color clearColour = new Color(0.55f, 0.93f, 1f);
        [SerializeField, Tooltip("The fill light: while a player is held it rides with that player's camera, as bright as FlatView > Fill Light says at the lane, fading in and out with the swing. A spot light is aimed to cover the screen. Empty: no fill (as before D-0203).")] private Light fill;
        [SerializeField, Tooltip("A player was taken: the view went side-on.")] private UnityEvent onEntered;
        [SerializeField, Tooltip("A player was let go: walked out, went out through the exit, or stopped fitting (first person, a car, a shift).")] private UnityEvent onExited;
        [SerializeField, Tooltip("A player went out through the exit (Clear).")] private UnityEvent onCleared;

        /// <summary>One local player: its camera rig and what the zone needs of its body.</summary>
        private sealed class Rider
        {
            public ShoulderCamera rig; public PlayerMotor motor; public Transform body; public CharacterController capsule;
            public Shifter shifter; public WeaponHolder holder;
            public ShiftWarp warp;   // the course player's screen warp (a party's copies have none: no warp)
            public bool inside;    // held now
            public bool putAway;   // we put its gun away: it comes back on the way out
            public bool cleared;   // it went out through the exit: left alone until its body has left the box
            public bool leaving;   // let go, and its camera is still swinging back: the cutaway waits for it
        }

        /// <summary>One thing the cutaway hides, and how: its own shadow setting (read at the moment of the cut) is put back.</summary>
        private struct Cut { public Renderer drawn; public ShadowCastingMode was; public byte how; }
        private const byte Untouched = 0, ByShadowsOnly = 1, ByNotDrawing = 2;

        private readonly List<Rider> _riders = new List<Rider>();
        private readonly List<Cut> _cut = new List<Cut>();
        private readonly List<Renderer> _found = new List<Renderer>();
        private readonly HashSet<Renderer> _seen = new HashSet<Renderer>();
        private readonly FlatRail _rail = new FlatRail();
        private bool _cutFound, _cutOn;
        private float _cardAt = float.NegativeInfinity;
        private float _fillShare;   // the fill light, 0 (off) to 1 (full): eases with the camera's swing

        /// <summary>A zone some local player is in now, or null.</summary>
        public static FlatZone Current { get; private set; }
        /// <summary>Raised when a player goes out through a zone's exit (Clear), with the zone.</summary>
        public static event Action<FlatZone> Cleared;
        /// <summary>Raised when this zone takes a player (the entrance gate plays on it).</summary>
        public event Action Entered;
        /// <summary>Raised when this zone lets a player go.</summary>
        public event Action Exited;

        /// <summary>The numbers in use: this zone's, or Resources/FlatView (its defaults when there is none).</summary>
        public FlatViewSettings Settings => settings != null ? settings : FlatViewSettings.Load();
        /// <summary>True while a local player is held.</summary>
        public bool AnyInside { get { for (int i = 0; i < _riders.Count; i++) if (_riders[i].inside) return true; return false; } }
        /// <summary>The zone's name for saves (its object's name).</summary>
        public string Id => name;
        private Transform Lane => lane != null ? lane : transform;

        private void OnEnable() { GameMode.Changed += OnModeChanged; }

        private void OnDisable()
        {
            GameMode.Changed -= OnModeChanged;
            for (int i = 0; i < _riders.Count; i++) if (_riders[i].inside) Release(_riders[i], false);
            _riders.Clear();
            SetCut(false);   // at once: nobody is left to wait for
            _fillShare = 0f;
            if (fill != null) fill.enabled = false;
            if (Current == this) Current = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { Current = null; Cleared = null; }

        /// <summary>Leaving Play (first person, Build Mode): everyone is let go in the same moment, before the new mode
        /// reads the camera (first person starts looking the way the shoulder camera looks: after this, the way the
        /// body faces, not the stale view from before the rail).</summary>
        private void OnModeChanged(GameMode.Mode from, GameMode.Mode to)
        {
            if (to == GameMode.Mode.Play) return;
            for (int i = 0; i < _riders.Count; i++) if (_riders[i].inside) Release(_riders[i], false);   // no swing: another camera takes over
        }

        /// <summary>True when <paramref name="point"/> (the world) is inside the box grown by <paramref name="slack"/>.</summary>
        public bool Contains(Vector3 point, float slack = 0f)
        {
            Vector3 local = transform.InverseTransformPoint(point) - centre;
            Vector3 half = size * 0.5f;
            return Mathf.Abs(local.x) <= half.x + slack && Mathf.Abs(local.y) <= half.y + slack && Mathf.Abs(local.z) <= half.z + slack;
        }

        /// <summary>True while this zone holds the player <paramref name="who"/> belongs to (only a held player can go out
        /// through the exit).</summary>
        public bool Holds(Component who)
        {
            Rider r = RiderOf(who);
            return r != null && r.inside;
        }

        /// <summary>The way out for everyone the zone holds: each is let go at once (the camera swings back behind it),
        /// with the exit card and the cleared events. With nobody held it does nothing.</summary>
        public void Clear()
        {
            for (int i = 0; i < _riders.Count; i++) if (_riders[i].inside) ClearRider(_riders[i]);
        }

        /// <summary>The way out for the player <paramref name="who"/> belongs to (the exit gate calls this with whoever
        /// touched it). False if the zone was not holding that player: nothing happened.</summary>
        public bool Clear(Component who)
        {
            Rider r = RiderOf(who);
            if (r == null || !r.inside) return false;
            ClearRider(r);
            return true;
        }

        private void ClearRider(Rider r)
        {
            r.cleared = true;   // left alone until its body has left the box
            PlayerMotor motor = r.motor;
            Release(r, true);
            Toasts.CardFor(motor, clearText, clearColour);
            onCleared?.Invoke();
            Cleared?.Invoke(this);
        }

        private Rider RiderOf(Component who)
        {
            if (who == null) return null;
            PlayerMotor motor = who.GetComponentInParent<PlayerMotor>();
            if (motor == null) return null;
            for (int i = 0; i < _riders.Count; i++) if (ReferenceEquals(_riders[i].motor, motor)) return _riders[i];
            return null;
        }

        private void Update()
        {
            SyncRiders();
            bool any = false, hold = false;
            for (int i = 0; i < _riders.Count; i++)
            {
                Rider r = _riders[i];
                bool inBox = Contains(Middle(r), r.inside ? edgeSlack : 0f);
                if (r.cleared && !inBox) r.cleared = false;   // it has left: the next walk in is a new visit
                bool fits = inBox && !r.cleared && Fits(r);
                if (fits && !r.inside) Take(r);
                else if (!fits && r.inside) Release(r, true);
                if (r.inside) { any = true; Keep(r); }
                // Let go: the wall stays cut until this camera is back off the rail's side (it is outside the wall for
                // most of the swing back). A camera that is switched off (first person, a menu) is not waited for.
                if (r.leaving && !(r.rig != null && r.rig.isActiveAndEnabled && r.rig.LeavingRail)) r.leaving = false;
                hold |= r.inside || r.leaving;
            }
            if (any) Current = this; else if (Current == this) Current = null;
            SetCut(hold);
            KeepFill(any);
        }

        /// <summary>The local players, one per camera rig (ShoulderCamera.All: no search). A rig that is gone, or that
        /// follows another body now (online, the player is bound to the rig after it spawns), is dropped and found again.</summary>
        private void SyncRiders()
        {
            for (int i = _riders.Count - 1; i >= 0; i--)
            {
                Rider r = _riders[i];
                if (r.rig != null && r.motor != null && ReferenceEquals(r.rig.Target, r.body)) continue;
                if (r.inside) Release(r, false);
                _riders.RemoveAt(i);
            }
            IReadOnlyList<ShoulderCamera> rigs = ShoulderCamera.All;
            for (int i = 0; i < rigs.Count; i++)
            {
                ShoulderCamera rig = rigs[i];
                if (rig == null || rig.Target == null || HasRider(rig)) continue;
                if (!rig.Target.TryGetComponent(out PlayerMotor motor)) continue;
                var r = new Rider { rig = rig, motor = motor, body = rig.Target };
                motor.TryGetComponent(out r.capsule); motor.TryGetComponent(out r.shifter); motor.TryGetComponent(out r.holder);
                motor.TryGetComponent(out r.warp);
                _riders.Add(r);
            }
        }

        private bool HasRider(ShoulderCamera rig)
        {
            for (int i = 0; i < _riders.Count; i++) if (ReferenceEquals(_riders[i].rig, rig)) return true;
            return false;
        }

        /// <summary>On foot in Play, in charge of its own body, and the right size. A pause, the console or a menu that
        /// only switches the input or the camera off changes nothing here: the zone holds on through them.</summary>
        private bool Fits(Rider r)
        {
            if (!r.motor.isActiveAndEnabled) return false;                // a car seat, Build Mode, the wake-up
            if (GameMode.Current != GameMode.Mode.Play) return false;     // first person is a free look: no rail, no hold
            if (toyOnly && r.shifter != null && (r.shifter.Size != ShiftSize.Toy || r.shifter.Shifting)) return false;
            return true;
        }

        /// <summary>The body's middle (the capsule's own, so a crouch counts).</summary>
        private static Vector3 Middle(Rider r) => r.capsule != null ? r.body.TransformPoint(r.capsule.center) : r.body.position;

        private void Take(Rider r)
        {
            // Another zone has this body (two boxes that overlap): first come, first served.
            if (r.motor.Flat.HasValue) { UnityEngine.Object owner = r.motor.Flat.Value.owner; if (owner != null && !ReferenceEquals(owner, this)) return; }
            r.inside = true; r.leaving = false;
            FillRail();
            r.motor.Flat = LockFor(r.motor);
            r.rig.SetRail(_rail);
            if (r.warp != null) r.warp.Play(true, _rail.View.blendSeconds);   // outward, as the camera swings out (a shift running wins)
            if (!string.IsNullOrEmpty(card) && Time.unscaledTime - _cardAt >= cardEvery)
            {
                _cardAt = Time.unscaledTime;
                Toasts.CardFor(r.motor, card, cardColour);
            }
            onEntered?.Invoke();
            Entered?.Invoke();
        }

        /// <summary>Every frame a player is held: the hold and the rail follow this object's numbers and FlatView as they
        /// stand (so both can be tuned in Play), and a gun that comes out is put away again (X, a slot key, a pick-up).</summary>
        private void Keep(Rider r)
        {
            FillRail();
            r.motor.Flat = LockFor(r.motor);
            if (r.holder != null && r.holder.Drawn && r.holder.Current != null) { r.holder.SetDrawn(false); r.putAway = true; }
        }

        /// <summary>Gives back everything the zone took from this player: the hold, the rail, the gun. The cutaway goes
        /// back in Update, once the camera has swung back. <paramref name="swing"/>: the camera swings back behind the
        /// player on foot (walked out, went out through the exit), so the warp plays inward with it; not when another
        /// camera takes over (first person, a car seat) or the zone is closing. Safe when the player or its camera is
        /// already gone (a scene closing).</summary>
        private void Release(Rider r, bool swing)
        {
            r.inside = false;
            if (r.motor != null && r.motor.Flat.HasValue && ReferenceEquals(r.motor.Flat.Value.owner, this)) r.motor.Flat = null;
            if (r.rig != null && ReferenceEquals(r.rig.Rail, _rail))
            {
                r.rig.ClearRail(); r.leaving = true;
                if (swing && r.warp != null && r.rig.isActiveAndEnabled && r.motor != null && r.motor.isActiveAndEnabled && GameMode.Current == GameMode.Mode.Play)
                    r.warp.Play(false, _rail.View.blendSeconds);   // inward, as it swings back (a shift that let it go wins)
            }
            // The gun comes back out, unless the player's own scene is closing (nothing there should be woken).
            if (r.putAway && r.holder != null && r.holder.gameObject.scene.isLoaded) r.holder.SetDrawn(true);
            r.putAway = false;
            onExited?.Invoke();
            Exited?.Invoke();
        }

        private FlatLock LockFor(PlayerMotor motor)
        {
            FlatViewSettings v = _rail.View;
            return new FlatLock(_rail.point, _rail.normal, v.laneSnap, v.laneSnapSpeed * motor.Scale, v.keepStickAngle, this);
        }

        /// <summary>The rail as this object's numbers stand now.</summary>
        private void FillRail()
        {
            Transform l = Lane;
            Vector3 n = l.forward; n.y = 0f;
            _rail.point = l.position;
            _rail.normal = n.sqrMagnitude > 1e-6f ? n.normalized : Vector3.forward;
            _rail.start = railStart; _rail.end = railEnd;
            _rail.floor = floor; _rail.ceiling = ceiling;
            _rail.view = Settings;
        }

        // ------------------------------------------------------------------ the fill light

        /// <summary>The fill light (D-0203): it eases on over the blend while anyone is held and off over the blend after,
        /// stands where the first held (or still swinging back) player's camera stands and looks the way it looks, and
        /// is as bright as FlatView > Fill Light at the lane: its brightness grows with the square of its distance from
        /// the lane, so it is the same at the lane wherever the camera is on its swing (near the toy, nearly nothing).
        /// No shadows: it shines through the cut wall. A spot is opened to cover an ultrawide screen at the rail camera's
        /// field of view. Switched off while it is at 0, so it costs nothing outside the zone.</summary>
        private void KeepFill(bool any)
        {
            if (fill == null) return;
            FlatViewSettings v = _rail.View;
            bool want = any && v.fillLight > 0f;
            if (!want && _fillShare <= 0f) { if (fill.enabled) fill.enabled = false; return; }
            _fillShare = Mathf.MoveTowards(_fillShare, want ? 1f : 0f, Time.unscaledDeltaTime / Mathf.Max(0.01f, v.blendSeconds));
            Rider lit = null;
            for (int i = 0; i < _riders.Count && lit == null; i++)
            {
                Rider r = _riders[i];
                if ((r.inside || r.leaving) && r.rig != null && r.rig.isActiveAndEnabled) lit = r;
            }
            if (lit != null) fill.transform.SetPositionAndRotation(lit.rig.transform.position, lit.rig.transform.rotation);
            bool on = _fillShare > 0f;
            if (fill.enabled != on) fill.enabled = on;
            if (!on) return;
            float scale = lit != null ? lit.motor.Scale : 1f;
            float toLane = Mathf.Max(scale, Mathf.Abs(Vector3.Dot(fill.transform.position - _rail.point, _rail.normal)));
            fill.intensity = v.fillLight * _fillShare * toLane * toLane;
            fill.range = Mathf.Max(toLane * 3f, _rail.Frame(scale) * 2f);   // 3 times: the range's own fade takes under 3% at the lane, a tenth at the far wall
            fill.color = v.fillColour;
            if (fill.shadows != LightShadows.None) fill.shadows = LightShadows.None;   // through the cut wall, which still casts
            if (fill.type == LightType.Spot)
            {
                // the screen's corners at an ultrawide 2.4: the diagonal is 2.6 times the half-height
                float cone = 2f * Mathf.Atan(Mathf.Tan(_rail.FieldOfView * 0.5f * Mathf.Deg2Rad) * 2.8f) * Mathf.Rad2Deg;
                fill.spotAngle = Mathf.Min(170f, cone);
                fill.innerSpotAngle = fill.spotAngle * 0.8f;
            }
        }

        // ------------------------------------------------------------------ to check without Play

        /// <summary>The rail this zone hands the camera, as its numbers stand now. Works in the editor, out of Play.</summary>
        public FlatRail Rail { get { FillRail(); return _rail; } }

        /// <summary>The rail camera's settled view of a body whose feet stand at <paramref name="feet"/> (world):
        /// where the camera stands, how it is turned, its field of view, and how many world units of the lane the
        /// screen shows top to bottom. <paramref name="lead"/>: how far ahead along the lane it looks (0 = standing
        /// still). These are the camera's own sums (FlatRail, which ShoulderCamera.RailView uses every frame), so the
        /// framing can be checked in the editor against numbers on paper.</summary>
        public void ViewFor(Vector3 feet, float lead, float scale, out Vector3 position, out Quaternion rotation, out float fieldOfView, out float frameHeight)
        {
            FillRail();
            _rail.Settled(feet, lead, scale, out position, out rotation);
            fieldOfView = _rail.FieldOfView;
            frameHeight = _rail.Frame(scale);
        }

        // ------------------------------------------------------------------ the cutaway

        /// <summary>Hides (true) or puts back (false) what stands between the lane and the camera. Each thing's own
        /// shadow setting is read at the moment it is hidden and put back as it was: one that casts shadows is drawn
        /// as its shadow only (the light in the room does not change); one that casts none is simply not drawn, so it
        /// does not start casting one. Materials are never touched.</summary>
        private void SetCut(bool on)
        {
            if (!cutaway) on = false;
            if (on == _cutOn) return;
            _cutOn = on;
            if (on && !_cutFound)
            {
                _cutFound = true;
                CutList(_found);
                for (int i = 0; i < _found.Count; i++) _cut.Add(new Cut { drawn = _found[i] });
                _found.Clear();
            }
            for (int i = 0; i < _cut.Count; i++)
            {
                Cut c = _cut[i];
                Renderer r = c.drawn;
                if (r == null) continue;
                if (on)
                {
                    c.was = r.shadowCastingMode;
                    c.how = Untouched;
                    if (c.was == ShadowCastingMode.Off) { if (!r.forceRenderingOff) { r.forceRenderingOff = true; c.how = ByNotDrawing; } }
                    else if (c.was != ShadowCastingMode.ShadowsOnly) { r.shadowCastingMode = ShadowCastingMode.ShadowsOnly; c.how = ByShadowsOnly; }
                    _cut[i] = c;
                }
                else if (c.how == ByShadowsOnly) { if (r.shadowCastingMode == ShadowCastingMode.ShadowsOnly) r.shadowCastingMode = c.was; }   // changed by someone else meanwhile: theirs stands
                else if (c.how == ByNotDrawing) r.forceRenderingOff = false;
            }
        }

        /// <summary>True when these bounds lie in the stretch the rail camera can see (along the lane and in height) and
        /// reach the camera's side of the lane past Cut From. <paramref name="near"/>: how far their nearest side is
        /// from the lane, towards the camera (under 0: they cross the lane). Call FillRail first.</summary>
        private bool InTheWay(Bounds b, out float near)
        {
            FlatViewSettings v = _rail.View;
            Vector3 along = _rail.Along, n = _rail.normal;
            float frame = v.frameHeight;   // toy size (the hallway); a kid's zone would be found for its own size
            float halfWide = frame * 0.5f * cutAspect;
            float aLo = Mathf.Min(railStart, railEnd) - halfWide, aHi = Mathf.Max(railStart, railEnd) + halfWide;
            Vector3 c = b.center - _rail.point, e = b.extents;
            float ac = Vector3.Dot(c, along), ae = Mathf.Abs(e.x * along.x) + Mathf.Abs(e.z * along.z);
            float nc = Vector3.Dot(c, n), ne = Mathf.Abs(e.x * n.x) + Mathf.Abs(e.z * n.z);
            near = nc - ne;
            if (ac + ae < aLo || ac - ae > aHi) return false;
            if (ceiling > floor && (b.max.y < floor || b.min.y > ceiling)) return false;   // wholly under the frame's floor or over its ceiling: never in the picture
            return nc + ne > cutFrom;
        }

        /// <summary>Never cut away: players, this zone's own pieces (and any other zone's), cores, checkpoints, and anything the player uses.</summary>
        private static bool Spared(Renderer r)
            => r.GetComponentInParent<FlatZone>() != null || r.GetComponentInParent<PlayerMotor>() != null
               || r.GetComponentInParent<ShiftSource>() != null || r.GetComponentInParent<Checkpoint>() != null
               // and anything the player uses (a weapon on its stand, a pickup, an ammo box): seen side-on it is the
               // next thing to go to, and a stand with nothing on it reads as taken
               || r.GetComponentInParent<IInteractable>() != null || r.GetComponentInParent<ISharedPickup>() != null;

        /// <summary>What the cutaway takes in the scene as it stands, into <paramref name="into"/> (emptied first): the
        /// renderers given by hand (Always Cut), and every renderer that stands wholly on the camera's side of the
        /// lane, past Cut From and short of where the rail camera stands, across the stretch the camera can see.
        /// Particles, trails and lines are left alone. The game finds this once, the first time anyone walks in (it
        /// reads every renderer in the scene: a few milliseconds, once); it also works in the editor, out of Play, so
        /// the list can be looked at before anyone plays.</summary>
        public void CutList(List<Renderer> into)
        {
            into.Clear();
            _seen.Clear();
            if (alwaysCut != null) foreach (Renderer r in alwaysCut) if (r != null && _seen.Add(r)) into.Add(r);
            FillRail();
            float distance = _rail.Distance(1f);
            foreach (Renderer r in FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                if (r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;
                if (!InTheWay(r.bounds, out float near) || near < cutFrom || near > distance) continue;   // the cheap test first: it throws out nearly everything
                if (Spared(r) || !_seen.Add(r)) continue;
                into.Add(r);
            }
            _seen.Clear();
        }

        /// <summary>What is NOT cut away although it reaches the camera's side of the lane in the stretch the camera
        /// sees, into <paramref name="into"/> (emptied first): things that cross the lane (the floor, the ceiling, the
        /// steps: as it should be) and anything spared. For a look in the editor before playing: a thing in this list
        /// that is neither is in front of the picture. The game itself never calls this.</summary>
        public void KeptInTheWay(List<Renderer> into)
        {
            into.Clear();
            FillRail();
            float distance = _rail.Distance(1f);
            foreach (Renderer r in FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                if (r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;
                if (!InTheWay(r.bounds, out float near) || near > distance) continue;
                if (near >= cutFrom && !Spared(r)) continue;   // that one is cut
                if (r.GetComponentInParent<PlayerMotor>() != null) continue;
                into.Add(r);
            }
        }

        private void OnDrawGizmos()
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = new Color(0.95f, 0.75f, 0.2f, 0.9f);
            Gizmos.DrawWireCube(centre, size);
            Gizmos.matrix = Matrix4x4.identity;
            FillRail();
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(_rail.point, _rail.point + _rail.normal * 4f);                                      // to the camera's side
            Gizmos.DrawLine(_rail.point + _rail.Along * railStart, _rail.point + _rail.Along * railEnd);       // where the camera's middle may go
        }

        /// <summary>Selected: the box filled in, and the rail camera's view from the lane's own point (a 16:9 screen).</summary>
        private void OnDrawGizmosSelected()
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = new Color(0.95f, 0.75f, 0.2f, 0.1f);
            Gizmos.DrawCube(centre, size);
            FillRail();
            _rail.Settled(new Vector3(_rail.point.x, floor, _rail.point.z), 0f, 1f, out Vector3 position, out Quaternion rotation);
            Gizmos.matrix = Matrix4x4.TRS(position, rotation, Vector3.one);
            Gizmos.color = Color.cyan;
            Gizmos.DrawFrustum(Vector3.zero, _rail.FieldOfView, _rail.Distance(1f), 0.5f, 16f / 9f);
            Gizmos.matrix = Matrix4x4.identity;
        }
    }
}
