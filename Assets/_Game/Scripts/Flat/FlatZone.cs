using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ShiftJam
{
    /// <summary>
    /// A Flat Zone (D-0198): a stretch of the house played side-on, as a side-scroller, while the 3D room stays round
    /// it. While a local player's body is inside the box:
    /// - the body keeps to the lane's plane (PlayerMotor.Flat: the stick's depth part is gone),
    /// - its camera rides the rail side-on (ShoulderCamera.SetRail), mouse look dropped,
    /// - what stands between the lane and the camera is cut away (not drawn, its shadow kept: the room's light is
    ///   unchanged), and put back exactly as it was once the camera has swung back,
    /// - a drawn gun stays out and aims inside the side view (D-0216: FlatAim; it was put away before),
    /// - the shift warp plays as the camera swings (D-0203: outward on the way in, inward on the way out, over the
    ///   blend; a size shift running wins), a big card says so ("FLAT SHIFT", "BACK TO 3D"), and a fill light the zone
    ///   owns rides with that player's camera, fading in and out with the swing, so the toy and the far wall read.
    /// WHO IT TAKES (D-0212, his "optional unless designed for specific beats"): a player is taken when its body WALKS
    /// INTO the box across the way-in side (the end the rail starts at: the hallway's west face). Before this save slot
    /// has DONE the stretch, that is all it takes (the first run stays mandatory); once it is done, only a player who
    /// walked through the way-in gate (the entrance rift's own trigger, moving on: FlatGate calls Invite) is taken, and
    /// one who walks in beside the rift stays in 3D. Never taken: a body that comes in across any other side (the far
    /// end, the top, the bottom: walking back from the playroom no longer flips the view at the exit), or one that is
    /// put inside (a respawn, a portal, the console: a move longer than Walked Most in a frame). Once taken, a player
    /// stays the zone's until its body leaves the box or it goes out through the exit: a respawn that lands inside keeps
    /// it; first person, Build Mode, a car, or (Toy Only) the wrong size or a shift let it go only while they last, and
    /// it is taken back once it fits again. A pause holds everything as it is. Whether the body is in the box is tested
    /// every frame (no trigger events).
    /// DONE (per save slot, in SaveGame's seen list: "flat:FlatZone_Hall:done" for the hallway, so Continue and a
    /// respawn keep it and New Game clears it) is set when a held player goes out through the exit; walking out of the
    /// box or falling out of it is not done.
    /// Clear (the exit gate calls it, FlatGate) lets a player go at once, with the exit card, and the zone leaves that
    /// player alone until its body has walked into the box again as above.
    /// THE LINK BACK (D-0212): the exit gate carries a player the zone does not hold who walks into it toward the
    /// entrance back to just behind the entrance (CarryBack: on the floor, facing away, clear of its trigger), with the
    /// shift warp; the gates flash and sound and rest (FlatView > Link Cooldown, Link Landing).
    /// The zone's own pieces (anything under this object: its gates, its fill light, its letters) are never cut away.
    /// LETTERS (D-0208): the S-H-I-F-T letters under this object (FlatLetter) are taken by a player the zone holds and
    /// saved per save slot under this zone's Id; the HUD's strip (FlatHud) shows them while the zone holds the player.
    /// When the last one that is switched on is found, the zone gives its reward once per slot (a shift charge, a big
    /// card, a sound). A zone with no letters works as before.
    /// PIXEL LOOK (D-0209, off unless ticked): while a player is held the picture goes to big square pixels in his
    /// title's palette (the PixelShift full-screen pass, FlatView > Pixel Rows, Palette Strength, Dither), easing in
    /// and out with the camera's swing; the renderer feature is switched on only while the look is above 0.
    /// ONTO THE LANE (D-0217, his play 25 Nov: taken beside the book stairs, the pull could not get him past the books;
    /// four seconds stuck behind them, hidden from the side camera). While a held player is not yet on the lane, the
    /// zone draws it straight in at the lane's own snap (FlatView > Lane Snap, Lane Snap Speed) and the stick does not
    /// walk it along the lane meanwhile (the motor's grip hold, PlayerMotor.HoldAt: a grip the player has already, a
    /// Grabbable, comes first and the zone waits). When that straight way is blocked (a capsule sweep, stepping over what
    /// the capsule's Step Offset steps over), it is put on the lane at once: the highest free standing spot at its place
    /// along the lane within Reach Up over and Reach Down under its feet, facing along the lane, under the zone's own
    /// warp. No such spot: the old way (the lock's ease, the stick free). Onto Lane off: the old way always.
    /// OFF SWITCH: switch this object off (or untick the script) and the stretch plays exactly as it did without it.
    /// Optional Once Done off: the first run's rule always (anyone walking in at the way-in side is taken, done or not).
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

        [Header("Who it takes (D-0212)")]
        [SerializeField, Tooltip("Once this save slot has gone through the stretch and out of its exit (done), the zone takes only a player who walks in through its entrance gate (the rift); walking in beside it, or coming back from the far end, stays 3D. OFF: anyone who walks in across the way-in side is taken, done or not (the first run's rule, always).")] private bool optionalOnceDone = true;
        [SerializeField, Min(0.1f), Tooltip("A body that moves further than this in one frame (own units: toy units at toy size) was put there (a respawn, a portal, the console), not walked in: such a move never takes it. A sprint is 11 a second, so even a quarter-second hitch moves under 3.")] private float walkedMost = 6f;

        [Header("Onto the lane (D-0217)")]
        [SerializeField, Tooltip("A held player off the lane is drawn straight in with the stick not walking it along the lane, and put on the lane at once when that straight way is blocked (the book stairs). OFF: the old way (the lock eases it over while it walks).")] private bool ontoLane = true;
        [SerializeField, Min(0.01f), Tooltip("A held body this close to the lane (own units) is on it: the draw stops and the stick is its own again (the lock eases the rest).")] private float onLaneWithin = 0.15f;
        [SerializeField, Min(0f), Tooltip("Blocked: the spot on the lane may stand at most this far over the feet (own units). The hallway's stuck spot needed 2.21 (Book1's top at x -18); a toy jumps 2.")] private float reachUp = 4f;
        [SerializeField, Min(0f), Tooltip("Blocked: the spot on the lane may stand at most this far under the feet (own units).")] private float reachDown = 4f;

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

        [Header("The letters (D-0208)")]
        [SerializeField, Tooltip("The big card when the last of this zone's letters is found (once per save slot). Empty: none.")] private string allFoundText = "S-H-I-F-T!";
        [SerializeField, Tooltip("That card's colour (the toasts' gold: a thing got).")] private Color allFoundColour = new Color32(0xFF, 0xD1, 0x66, 0xFF);
        [SerializeField, Min(0), Tooltip("Shift charges given with it (up to the most the player can hold: with the charge already full it stays full).")] private int allFoundCharges = 1;
        [SerializeField, Tooltip("The sound when the last letter is found (it plays instead of that letter's own). No clip: silent.")] private CourseSounds.Cue allFoundSound = new CourseSounds.Cue(null, 0.5f);
        [SerializeField, Tooltip("Raised when the last of this zone's letters is found (once per save slot).")] private UnityEvent onAllFound;

        [Header("The pixel look (D-0209)")]
        [SerializeField, Tooltip("While a player is held the picture goes to big square pixels in his title's palette (FlatView > Pixel Rows, Palette Strength, Dither), coming in and going out with the camera's swing. OFF for the hallway (he liked it as it is): tick it to see it, in Play too. Needs Pixel Feature.")] private bool pixelLook;
        [SerializeField, Tooltip("The PixelShift Full Screen Pass feature on the renderer (Assets/Settings/PC_Renderer). Switched on only while the look shows, as the shift warp's is. Shift Jam > Flat > Add Pixel Look adds it to the renderer; Land Hallway Scroller sets it here. Empty: no pixel look.")] private ScriptableRendererFeature pixelFeature;

        /// <summary>One local player: its camera rig and what the zone needs of its body.</summary>
        private sealed class Rider
        {
            public ShoulderCamera rig; public PlayerMotor motor; public Transform body; public CharacterController capsule;
            public Shifter shifter; public WeaponHolder holder;
            public ShiftWarp warp;   // the course player's screen warp (a party's copies have none: no warp)
            public bool inside;    // held now
            public bool admitted;  // it walked in as the take rule allows: held whenever it fits, until it leaves the box or goes out through the exit
            public bool leaving;   // let go, and its camera is still swinging back: the cutaway waits for it
            public Vector3 last;   // its middle last frame (the take rule asks where it came from)
            public bool hasLast;   // false until a frame has been seen, and after a carry back: no walk to read
            public float invitedUntil = float.NegativeInfinity;   // it walked through the way-in gate: may be taken until then
            public int mask;       // the layers its capsule collides with (the sweep and the spot search ask only those)
            public int takenFrame = -1;   // the frame it was taken (the warp plays then already)
            public bool drawing;   // the zone is drawing it onto the lane (it set the motor's grip hold)
            public Vector3 drawAt; // the grip point it set this frame (only that one is taken back)
            public float drawYaw;  // the way it faces while drawn in
            public bool gaveUp;    // blocked with no spot on the lane: the old way until it is on the lane or taken again
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
        private float _pixelShare;  // the pixel look, 0 (off) to 1 (full): eases with the camera's swing
        private bool _pixelDriving; // this zone has the pixel pass on: only it switches it off again (another zone's is left alone)
        private bool _done;         // the active save slot has gone out through this zone's exit (read when the saves change)
        private string _doneKey;    // its name in the seen list, made once (an object's name is a new string each time it is asked)

        // The pixel pass's numbers are globals (PixelShift.shader), so playing never writes its material asset.
        private static readonly int PixelWeightId = Shader.PropertyToID("_PixelShiftWeight"), PixelRowsId = Shader.PropertyToID("_PixelShiftRows"),
            PixelDitherId = Shader.PropertyToID("_PixelShiftDither"), PixelPaletteId = Shader.PropertyToID("_PixelShiftPalette");

        // Onto the lane (D-0217): one buffer each for the sweep, the spot's ray and its room check (no allocation).
        private static readonly RaycastHit[] LaneHits = new RaycastHit[16];
        private static readonly Collider[] LaneOverlaps = new Collider[16];
        private const float LaneSkin = 0.02f;   // own units: a spot stands this hair over the surface found, the sweep's capsule is this much thinner

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
        /// <summary>The zone's name for saves (its object's name: renaming the object forgets the letters it has found).</summary>
        public string Id => name;
        /// <summary>True when the active save slot has gone out through this zone's exit (D-0212). Read in Play.</summary>
        public bool Done => _done;
        /// <summary>Once done, only the way-in gate takes a player (Optional Once Done).</summary>
        public bool OptionalOnceDone => optionalOnceDone;
        private Transform Lane => lane != null ? lane : transform;

        /// <summary>The save's name for "this zone's stretch is done" (a moment in SaveGame's seen list: "flat:FlatZone_Hall:done").</summary>
        public static string DoneKeyOf(string zoneId) => "flat:" + (zoneId ?? "").Replace("|", "/") + ":done";   // the seen list is split on "|", as the letters' keys

        /// <summary>True when save slot <paramref name="slot"/> has done this zone's stretch. Works out of Play (the maker's report).</summary>
        public bool DoneFor(int slot) => SaveGame.Seen(slot, DoneKeyOf(Id));

        private void OnEnable()
        {
            GameMode.Changed += OnModeChanged;
            SaveGame.Changed += ReadDone;
            ReadDone();
        }

        /// <summary>The active slot's done mark, read again whenever the saves change (another slot, New Game, a mark).</summary>
        private void ReadDone()
        {
            _doneKey ??= DoneKeyOf(Id);
            _done = SaveGame.Seen(SaveGame.Slot, _doneKey);
        }

        private void OnDisable()
        {
            GameMode.Changed -= OnModeChanged;
            SaveGame.Changed -= ReadDone;
            for (int i = 0; i < _riders.Count; i++) if (_riders[i].inside) Release(_riders[i], false);
            _riders.Clear();
            SetCut(false);   // at once: nobody is left to wait for
            _fillShare = 0f;
            if (fill != null) fill.enabled = false;
            StopPixel();
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
            r.admitted = false;   // left alone until it walks into the box again as the take rule allows
            PlayerMotor motor = r.motor;
            Release(r, true);
            if (!_done) { _doneKey ??= DoneKeyOf(Id); SaveGame.MarkSeen(SaveGame.Slot, _doneKey); _done = true; }   // done for this slot (D-0212)
            Toasts.CardFor(motor, clearText, clearColour);
            onCleared?.Invoke();
            Cleared?.Invoke(this);
        }

        // ------------------------------------------------------------------ the rifts (D-0212)

        /// <summary>The way-in gate was walked through, moving on, by the player <paramref name="who"/> belongs to: for
        /// FlatView > Rift Window seconds its body may walk into the box and be taken, done or not. False if that is
        /// not a local player (another player's copy online) or the zone holds it already.</summary>
        public bool Invite(Component who)
        {
            Rider r = RiderOf(who);
            if (r == null || r.inside) return false;
            r.invitedUntil = Time.time + Settings.riftWindow;
            return true;
        }

        /// <summary>The way out's link (the exit gate calls it): the player <paramref name="who"/> belongs to, when the
        /// zone does not hold it, is carried to just behind <paramref name="to"/> (the way in): FlatView > Link Landing
        /// behind its middle, on its floor, facing away from it, its respawn point there (as a portal does), its camera
        /// turned to match, and the shift warp played. Its walk starts afresh there (no walk into the box is read from
        /// the jump). False, and nothing done: not a local player, held by the zone, mid-shift, or (Toy Only) not a toy.
        /// The gates do their own flash, sound and rest.</summary>
        public bool CarryBack(Component who, FlatGate to)
        {
            Rider r = RiderOf(who);
            if (r == null || r.inside || to == null) return false;
            if (r.shifter != null && (r.shifter.Shifting || (toyOnly && r.shifter.Size != ShiftSize.Toy))) return false;
            LandingFor(to, out Vector3 at, out Quaternion facing);
            r.motor.Warp(at, facing);   // first person follows it (Warped)
            if (r.motor.TryGetComponent(out Respawn respawn)) respawn.SetPoint(at, facing);
            if (r.rig != null) r.rig.Warp(facing.eulerAngles.y);
            if (r.warp != null) r.warp.Play(true, Settings.blendSeconds);
            r.admitted = false; r.hasLast = false; r.invitedUntil = float.NegativeInfinity;
            return true;
        }

        /// <summary>Where CarryBack puts a player for the way in <paramref name="to"/>: Link Landing behind its middle, at
        /// its height (a gate stands on its floor), facing away from it. Works out of Play (the maker's report).</summary>
        public void LandingFor(FlatGate to, out Vector3 at, out Quaternion facing)
        {
            Vector3 back = -to.transform.forward; back.y = 0f;
            back = back.sqrMagnitude > 1e-6f ? back.normalized : Vector3.back;
            at = to.transform.position + back * Settings.linkLanding;
            facing = Quaternion.LookRotation(back, Vector3.up);
        }

        /// <summary>The take rule (D-0212) for a body the zone does not have, whose middle moved from
        /// <paramref name="from"/> to <paramref name="to"/> (world) in one frame: true when that move walks it into the
        /// box across the way-in side, and then before the stretch is <paramref name="done"/> (or with Optional Once Done
        /// off) always, after it only when it was <paramref name="invited"/> (it came through the way-in gate). Any other
        /// way in (the far end, the top, the bottom, or a move longer than Walked Most: a respawn, a portal) is never
        /// taken. <paramref name="scale"/>: the player's own scale (Walked Most is in its units). The game's own rule, so
        /// the maker can print what it answers out of Play.</summary>
        public bool WouldTake(Vector3 from, Vector3 to, bool done, bool invited, float scale = 1f)
        {
            float most = walkedMost * Mathf.Max(0.01f, scale);
            if ((to - from).sqrMagnitude > most * most) return false;
            if (!Contains(to) || Contains(from) || !BesideWayIn(from)) return false;
            return !done || !optionalOnceDone || invited;
        }

        /// <summary>True when <paramref name="point"/> (the world) lies past the box's way-in side and within its other
        /// extents: the side at the rail's start end (Rail Start; the hallway's west face). The box's own axes nearest
        /// the lane's direction decide which side that is.</summary>
        private bool BesideWayIn(Vector3 point)
        {
            FillRail();
            Vector3 l = transform.InverseTransformPoint(point) - centre, half = size * 0.5f;
            Vector3 along = transform.InverseTransformDirection(_rail.Along);
            float toStart = railStart <= railEnd ? -1f : 1f;   // the rail's start end lies this way along the lane
            if (Mathf.Abs(along.x) >= Mathf.Abs(along.z))
                return l.x * Mathf.Sign(along.x) * toStart > half.x && Mathf.Abs(l.y) <= half.y && Mathf.Abs(l.z) <= half.z;
            return l.z * Mathf.Sign(along.z) * toStart > half.z && Mathf.Abs(l.y) <= half.y && Mathf.Abs(l.x) <= half.x;
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
            float now = Time.time;
            for (int i = 0; i < _riders.Count; i++)
            {
                Rider r = _riders[i];
                Vector3 middle = Middle(r);
                bool inBox = Contains(middle, r.admitted ? edgeSlack : 0f);
                if (!inBox) r.admitted = false;   // it has left: the next walk in is a new visit
                else if (!r.admitted && r.hasLast && WouldTake(r.last, middle, _done, now <= r.invitedUntil, r.motor.Scale)) r.admitted = true;
                r.last = middle; r.hasLast = true;
                bool fits = r.admitted && Fits(r);
                if (fits && !r.inside) Take(r);
                else if (!fits && r.inside) Release(r, true);
                if (r.inside) { any = true; Keep(r); OntoLane(r); }
                // Let go: the wall stays cut until this camera is back off the rail's side (it is outside the wall for
                // most of the swing back). A camera that is switched off (first person, a menu) is not waited for.
                if (r.leaving && !(r.rig != null && r.rig.isActiveAndEnabled && r.rig.LeavingRail)) r.leaving = false;
                hold |= r.inside || r.leaving;
            }
            if (any) Current = this; else if (Current == this) Current = null;
            SetCut(hold);
            KeepFill(any);
            KeepPixel(any);
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
                var r = new Rider { rig = rig, motor = motor, body = rig.Target, mask = CollisionMaskOf(motor.gameObject.layer) };
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
            r.takenFrame = Time.frameCount; r.gaveUp = false;
            FillRail();
            r.motor.Flat = LockFor(r.motor);
            r.rig.SetRail(_rail);
            if (r.warp != null) r.warp.Play(true, _rail.View.blendSeconds);   // outward, as the camera swings out (a shift running wins)
            if (!string.IsNullOrEmpty(card) && Time.unscaledTime - _cardAt >= cardEvery)
            {
                _cardAt = Time.unscaledTime;
                Toasts.CardFor(r.motor, card, cardColour);
            }
            FlatHud.Get();   // the letters' strip (it shows only for a zone with letters)
            onEntered?.Invoke();
            Entered?.Invoke();
        }

        /// <summary>Every frame a player is held: the hold and the rail follow this object's numbers and FlatView as they
        /// stand (so both can be tuned in Play). A gun stays out: it aims inside the side view (D-0216).</summary>
        private void Keep(Rider r)
        {
            FillRail();
            r.motor.Flat = LockFor(r.motor);
        }

        /// <summary>Gives back everything the zone took from this player: the hold, the rail, the gun. The cutaway goes
        /// back in Update, once the camera has swung back. <paramref name="swing"/>: the camera swings back behind the
        /// player on foot (walked out, went out through the exit), so the warp plays inward with it; not when another
        /// camera takes over (first person, a car seat) or the zone is closing. Safe when the player or its camera is
        /// already gone (a scene closing).</summary>
        private void Release(Rider r, bool swing)
        {
            r.inside = false;
            StopDrawing(r);
            if (r.motor != null && r.motor.Flat.HasValue && ReferenceEquals(r.motor.Flat.Value.owner, this)) r.motor.Flat = null;
            if (r.rig != null && ReferenceEquals(r.rig.Rail, _rail))
            {
                r.rig.ClearRail(); r.leaving = true;
                if (swing && r.warp != null && r.rig.isActiveAndEnabled && r.motor != null && r.motor.isActiveAndEnabled && GameMode.Current == GameMode.Mode.Play)
                    r.warp.Play(false, _rail.View.blendSeconds);   // inward, as it swings back (a shift that let it go wins)
            }
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

        // ------------------------------------------------------------------ onto the lane (D-0217)

        /// <summary>Every frame a held player is off the lane (after Keep: the hold is this frame's): drawn straight in at
        /// the lock's own ease, the stick not walking it along the lane meanwhile; put on the lane at once when that
        /// straight way is blocked; the old way (the lock's ease, the stick free) when there is no spot for it.</summary>
        private void OntoLane(Rider r)
        {
            if (!ontoLane || r.capsule == null || !r.motor.Flat.HasValue) { StopDrawing(r); return; }
            PlayerMotor motor = r.motor;
            FlatLock hold = motor.Flat.Value;
            float scale = Mathf.Max(0.01f, motor.Scale);
            BodyCapsule(r.body, r.capsule, out Vector3 feet, out float radius, out float height, out float step);
            float off = hold.Off(feet);
            if (Mathf.Abs(off) <= onLaneWithin * scale) { StopDrawing(r); r.gaveUp = false; return; }   // on the lane: the stick is its own
            if (r.gaveUp) return;
            // A grip the player already has (a Grabbable, the armory bench) comes first, as it does in the motor: the zone waits.
            if (motor.HoldAt.HasValue && !(r.drawing && motor.HoldAt.Value == r.drawAt)) { r.drawing = false; return; }
            if (WayToLaneBlocked(feet, radius, height, step, scale, r.mask, r.body, out _))
            {
                StopDrawing(r);
                if (LaneSpot(feet, radius, height, r.capsule.slopeLimit, scale, r.mask, r.body, out Vector3 spot)) PutOnLane(r, r.body.position + (spot - feet), hold);
                else r.gaveUp = true;   // nowhere on the lane at its place: the old way, until it is on the lane or taken again
                return;
            }
            // Drawn in: the motor's grip hold moves the body to this frame's step (the lock's Lane Snap, at most Lane Snap
            // Speed) with the stick going nowhere; gravity and a jump still work. A long way in is walked facing the lane.
            Vector3 pull = hold.Correction(feet, Time.deltaTime);
            if (!r.drawing)
            {
                Vector3 toLane = off > 0f ? -hold.normal : hold.normal;
                r.drawYaw = Mathf.Abs(off) > scale ? Quaternion.LookRotation(toLane, Vector3.up).eulerAngles.y : r.body.eulerAngles.y;
            }
            Vector3 at = r.body.position;
            r.drawAt = new Vector3(at.x + pull.x, at.y, at.z + pull.z);
            motor.HoldAt = r.drawAt;
            motor.HoldYaw = r.drawYaw;
            r.drawing = true;
        }

        /// <summary>Gives the motor's grip hold back, if it is still the one the zone set (a grip that took over keeps its own).</summary>
        private static void StopDrawing(Rider r)
        {
            if (r.drawing && r.motor != null && r.motor.HoldAt.HasValue && r.motor.HoldAt.Value == r.drawAt) r.motor.HoldAt = null;
            r.drawing = false;
        }

        /// <summary>Puts the player's body at <paramref name="to"/> (its own position: the spot's feet plus its pivot's
        /// height over its feet) facing along the lane the way it faced most, with the zone's warp (unless the take's
        /// warp is playing this very frame).</summary>
        private void PutOnLane(Rider r, Vector3 to, FlatLock hold)
        {
            Vector3 way = hold.Along(r.body.forward);
            r.motor.Warp(to, Quaternion.LookRotation(way, Vector3.up));   // first person and the rope follow it (Warped)
            if (r.warp != null && Time.frameCount != r.takenFrame) r.warp.Play(true, Settings.blendSeconds);
            r.last = Middle(r); r.hasLast = true;
        }

        /// <summary>The body's capsule in the world: where its feet are (the capsule's bottom), its radius and height,
        /// and how high a step it walks up (the controller's Step Offset), all grown with the body's scale.</summary>
        private static void BodyCapsule(Transform body, CharacterController c, out Vector3 feet, out float radius, out float height, out float step)
        {
            Vector3 s = body.lossyScale;
            float across = Mathf.Max(Mathf.Abs(s.x), Mathf.Abs(s.z)), tall = Mathf.Abs(s.y);
            radius = c.radius * across;
            height = Mathf.Max(c.height * tall, 2f * radius);
            step = c.stepOffset * tall;
            feet = body.TransformPoint(c.center) - Vector3.up * (height * 0.5f);
        }

        /// <summary>The layers a body on <paramref name="layer"/> collides with (Physics settings' matrix).</summary>
        public static int CollisionMaskOf(int layer)
        {
            int mask = 0;
            for (int i = 0; i < 32; i++) if (!Physics.GetIgnoreLayerCollision(layer, i)) mask |= 1 << i;
            return mask;
        }

        /// <summary>True when a capsule (feet at <paramref name="feet"/>, world) cannot go straight across to the lane:
        /// something solid stands in the way, other than what the capsule steps over (its bottom is lifted by
        /// <paramref name="stepOffset"/>), the body itself (<paramref name="ignore"/>) or a player. <paramref name="clear"/>:
        /// how far it can go (the whole way when not blocked). Already on the lane (On Lane Within): false. The game's
        /// own test; works out of Play (the maker's report).</summary>
        public bool WayToLaneBlocked(Vector3 feet, float radius, float height, float stepOffset, float scale, int mask, Transform ignore, out float clear)
        {
            FillRail();
            float off = Vector3.Dot(feet - _rail.point, _rail.normal);
            float distance = Mathf.Abs(off) - onLaneWithin * scale;
            clear = Mathf.Max(0f, distance);
            if (distance <= 0f) return false;
            Vector3 way = off > 0f ? -_rail.normal : _rail.normal;
            float thin = Mathf.Max(0.01f, radius - LaneSkin * scale);
            Vector3 top = feet + Vector3.up * Mathf.Max(radius, height - radius);
            Vector3 bottom = feet + Vector3.up * (radius + Mathf.Max(0f, stepOffset) + LaneSkin * scale);
            if (bottom.y > top.y) bottom = top;
            int count = Physics.CapsuleCastNonAlloc(bottom, top, thin, way, LaneHits, distance, mask, QueryTriggerInteraction.Ignore);
            bool blocked = false;
            for (int i = 0; i < count; i++)
            {
                RaycastHit h = LaneHits[i];
                if (h.distance <= 0f || Ignored(h.collider, ignore)) continue;   // touching at the start: not in the way ahead
                if (h.distance < clear) { clear = h.distance; blocked = true; }
            }
            return blocked;
        }

        /// <summary>The highest free standing spot on the lane at the place along it of <paramref name="feet"/> (world),
        /// at most Reach Up over and Reach Down under them (own units, times <paramref name="scale"/>): a surface found by a
        /// ray straight down the lane's plane, no steeper than <paramref name="slopeLimit"/>, with room over it for the
        /// whole capsule. <paramref name="spot"/>: where the feet go. False: no such spot. The game's own search; works
        /// out of Play (the maker reports the books' spot with it).</summary>
        public bool LaneSpot(Vector3 feet, float radius, float height, float slopeLimit, float scale, int mask, Transform ignore, out Vector3 spot)
        {
            FillRail();
            Vector3 n = _rail.normal;
            Vector3 at = feet - n * Vector3.Dot(feet - _rail.point, n);
            float up = reachUp * scale, down = reachDown * scale, skin = LaneSkin * scale;
            int count = Physics.RaycastNonAlloc(new Vector3(at.x, feet.y + up, at.z), Vector3.down, LaneHits, up + down, mask, QueryTriggerInteraction.Ignore);
            for (int i = 1; i < count; i++)   // highest first (a few hits: a plain insertion sort, no allocation)
            {
                RaycastHit h = LaneHits[i];
                int j = i - 1;
                while (j >= 0 && LaneHits[j].point.y < h.point.y) { LaneHits[j + 1] = LaneHits[j]; j--; }
                LaneHits[j + 1] = h;
            }
            float minUp = Mathf.Cos(Mathf.Clamp(slopeLimit, 0f, 89f) * Mathf.Deg2Rad);
            for (int i = 0; i < count; i++)
            {
                RaycastHit h = LaneHits[i];
                if (h.normal.y < minUp || Ignored(h.collider, ignore)) continue;
                Vector3 standing = new Vector3(at.x, h.point.y + skin, at.z);
                if (Room(standing, radius, height, mask, ignore)) { spot = standing; return true; }
            }
            spot = feet;
            return false;
        }

        /// <summary>True when a capsule standing at <paramref name="feet"/> touches nothing solid (the body itself and
        /// players aside).</summary>
        private static bool Room(Vector3 feet, float radius, float height, int mask, Transform ignore)
        {
            float thin = Mathf.Max(0.01f, radius * 0.95f);
            Vector3 bottom = feet + Vector3.up * radius, top = feet + Vector3.up * Mathf.Max(radius, height - radius);
            int count = Physics.OverlapCapsuleNonAlloc(bottom, top, thin, LaneOverlaps, mask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++) if (!Ignored(LaneOverlaps[i], ignore)) return false;
            return true;
        }

        /// <summary>Never in the way: the body itself, or any player (they move).</summary>
        private static bool Ignored(Collider c, Transform ignore)
            => c == null || (ignore != null && c.transform.IsChildOf(ignore)) || c.GetComponentInParent<PlayerMotor>() != null;

        // ------------------------------------------------------------------ the letters (D-0208)

        /// <summary>One of this zone's letters was just taken by <paramref name="who"/> and saved (FlatLetter calls this):
        /// the HUD's strip shows. When every letter of this zone that is switched on is found now and the active save slot
        /// has not had the reward, it is given: the charges (ShiftCharge, up to its most), the big card, the sound and
        /// On All Found. True when the reward was given just now (the letter then plays no sound of its own).</summary>
        public bool LetterFound(FlatLetter letter, PlayerMotor who)
        {
            FlatHud.Flash(this);
            FlatLetter.Mask(this, out int present, out int found);
            if (present == 0 || found != present) return false;
            string key = FlatLetter.RewardKeyOf(Id);
            int slot = SaveGame.Slot;
            if (SaveGame.Seen(slot, key)) return false;
            SaveGame.MarkSeen(slot, key);
            if (who != null && allFoundCharges > 0 && who.TryGetComponent(out ShiftCharge charge)) charge.SetCharges(charge.Charges + allFoundCharges);
            Toasts.CardFor(who, allFoundText, allFoundColour);
            UiAudio.Play(allFoundSound);
            onAllFound?.Invoke();
            return true;
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

        // ------------------------------------------------------------------ the pixel look (D-0209)

        /// <summary>The pixel look: it eases on over the blend while anyone is held (and Pixel Look is ticked) and off over
        /// the blend after, smoothed at both ends like the swing. Its weight and FlatView's numbers go to the shader as
        /// globals every frame it shows (tunable in Play); the feature is on only while the weight is above 0, so the pass
        /// costs nothing outside the zone (the shift warp's way). A zone that is not showing it never touches it.</summary>
        private void KeepPixel(bool any)
        {
            if (pixelFeature == null) return;
            FlatViewSettings v = _rail.View;
            bool want = any && pixelLook && v.pixelRows > 0;
            if (!want && !_pixelDriving) return;
            _pixelShare = Mathf.MoveTowards(_pixelShare, want ? 1f : 0f, Time.unscaledDeltaTime / Mathf.Max(0.01f, v.blendSeconds));
            float weight = _pixelShare * _pixelShare * (3f - 2f * _pixelShare);
            if (weight <= 0f) { StopPixel(); return; }
            Shader.SetGlobalFloat(PixelWeightId, weight);
            Shader.SetGlobalFloat(PixelRowsId, v.pixelRows);
            Shader.SetGlobalFloat(PixelDitherId, v.dither);
            Shader.SetGlobalFloat(PixelPaletteId, v.paletteStrength);
            _pixelDriving = true;
            if (!pixelFeature.isActive) pixelFeature.SetActive(true);
        }

        /// <summary>The pixel look off at once (weight 0, the feature off), if this zone had it on.</summary>
        private void StopPixel()
        {
            _pixelShare = 0f;
            if (!_pixelDriving) return;
            _pixelDriving = false;
            Shader.SetGlobalFloat(PixelWeightId, 0f);
            if (pixelFeature != null && pixelFeature.isActive) pixelFeature.SetActive(false);
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
