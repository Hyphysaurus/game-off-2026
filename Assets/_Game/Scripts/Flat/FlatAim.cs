using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// Aiming in the side view (D-0216). While a Flat Zone holds this player (and only then), everything that aims takes
    /// its aim from here instead of from the camera, which side-on looks straight into the far wall:
    /// - THE AIM POINT lies on the lane's plane. With the mouse it is where the cursor's screen ray meets that plane; the
    ///   cursor is shown and free meanwhile (the camera rig is told it does not own the cursor, so a click is a click, not
    ///   a grab of the mouse). With a pad the right stick aims along the plane past FlatView > Stick Dead Zone; let go,
    ///   the aim is the way the toy faces, level. Whichever was used last wins: the stick when it is pushed, the mouse
    ///   when it moves (FlatView > Mouse Takes Over). At the start of a hold neither has spoken: the way the toy faces.
    /// - THE AIM RAY runs from the chest, on the lane, toward that point (Origin, Forward). The WeaponHolder hands it out
    ///   as its own aim ray, so the guns (hit scan and flying darts alike), the dart line, grenades, the gun's arm, the
    ///   flashlight's arm (the Carrier asks the holder's crosshair point) and with it the beam all aim at it.
    /// - THE TOY FACES left or right along the lane toward the aim (FlatView > Aim Turn Speed), turning through facing
    ///   the camera, never into the wall: while it has something to aim (a gun drawn, a tool up in the fist) or aim is
    ///   held. Empty-handed it turns the way it runs, as before (FlatView > Face Aim Empty Handed ticks it on). Not during
    ///   a roll, a block or a hit, nor on a rope, a grip, a line or the glider: those turn the body their own way.
    ///   It writes the motor's Face Yaw after PlayerMoves has written it (that one is the view's: the wall).
    /// - LEAVING (let go, walked out, the exit rift, a shift, a car seat, first person or Build Mode) puts the cursor back
    ///   locked and hidden in its rig's hands, and the aim back on the camera. A pause menu, the title, the console and the
    ///   key sheet keep the cursor as they always did: nothing here touches it while they are up.
    /// Local players only: another player's copy online is never held by a zone, and nothing here is sent anywhere.
    /// A party: each player aims with its own devices; only the player whose rig owned the cursor takes it.
    /// Added at run time by the WeaponHolder of a player with keys. OFF SWITCH: FlatView > Aim 2D off: everything aims
    /// along the camera, as before D-0216.
    /// </summary>
    [DefaultExecutionOrder(-8)]   // after PlayerMoves (-10) has said which way the body faces, before the rope (-5) and the motor (0) act on it
    [DisallowMultipleComponent]
    public sealed class FlatAim : MonoBehaviour
    {
        /// <summary>What gives the aim now.</summary>
        public enum Source { Rest, Mouse, Stick }

        private PlayerInput _input;
        private PlayerMotor _motor;
        private WeaponHolder _holder;
        private Carrier _carrier;
        private PlayerMoves _moves;
        private FlatZone _zone;          // the zone holding the body while Active
        private FlatViewSettings _settings;   // its numbers, read once a frame (the aim is asked several times a frame)
        private FlatLock _lane;          // its hold as it stood when last read (kept for a moment the motor's is gone)
        private ShoulderCamera _rig;     // this player's camera rig (null: none follows this body)
        private Vector2 _stick;          // the stick's way while it aims (unit, screen right and up)
        private Vector2 _pointerWas;     // where the mouse was when it last spoke (or when the hold began)
        private bool _pointerKnown;
        private bool _tookCursor;        // the rig's OwnsCursor was set false here: given back on the way out
        private bool _wroteFacing;       // the motor's Face Yaw is ours from this frame or the last
        private float _side = 1f;        // +1 the toy faces the screen's right along the lane, -1 its left

        private static FlatAim _cursorHolder;   // the one player who has taken the cursor (one mouse, one cursor)

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => _cursorHolder = null;

        /// <summary>True while some player's side-view aim has the mouse cursor free and shown on purpose: a click then is
        /// a click (a shot, a punch), not the game taking the mouse back. Read where a click is refused while the cursor
        /// is not locked. False while a menu, the console or the key sheet is up, or the game is paused: a click there
        /// belongs to that screen, never to a punch or a shot.</summary>
        public static bool CursorTaken => _cursorHolder != null && _cursorHolder.GameRunning();

        /// <summary>True while a Flat Zone holds this player and the side-view aim is in charge.</summary>
        public bool Active { get; private set; }
        /// <summary>What gives the aim now (Rest: the way the toy faces, level).</summary>
        public Source From { get; private set; }
        /// <summary>The numbers in use: the holding zone's, or Resources/FlatView.</summary>
        public FlatViewSettings Settings => _settings != null ? _settings : (_settings = FlatViewSettings.Load());

        private float Scale => _motor != null ? _motor.Scale : 1f;
        /// <summary>The lane's hold as it stands now (the motor's, else the last one read).</summary>
        private FlatLock Lane => _motor != null && _motor.Flat.HasValue ? _motor.Flat.Value : _lane;
        /// <summary>The chest, where melee and shots start (the holder's own answer), before it is put on the lane. A body
        /// with no holder (never made so: the holder adds this part) aims from one own unit over its feet.</summary>
        private Vector3 Chest => _holder != null ? _holder.AttackOrigin : transform.position + Vector3.up * Scale;
        private Camera View => _rig != null ? _rig.View : PlayerSlot.ViewFor(this);

        private void Awake()
        {
            TryGetComponent(out _input);
            TryGetComponent(out _motor);
            TryGetComponent(out _holder);
            TryGetComponent(out _carrier);
            TryGetComponent(out _moves);
        }

        private void OnDisable() { if (Active) End(); }

        private void Update()
        {
            bool held = Holding(out FlatZone zone);
            if (held && !Active) Begin(zone);
            else if (!held && Active) End();
            if (!Active) return;
            _zone = zone;
            _lane = _motor.Flat.Value;
            FlatViewSettings v = _settings;   // read by Holding this frame
            ReadDevices(v);
            KeepCursor(v);
            Face(v);
        }

        /// <summary>A zone holds this body now: the owner of the motor's hold, and it says it holds this player (a local
        /// one: another player's copy is never held), in Play, with Aim 2D on.</summary>
        private bool Holding(out FlatZone zone)
        {
            zone = null;
            if (_motor == null || !_motor.isActiveAndEnabled || !_motor.Flat.HasValue) return false;
            if (_holder != null && _holder.IsPuppet) return false;
            if (GameMode.Current != GameMode.Mode.Play) return false;
            zone = _motor.Flat.Value.owner as FlatZone;
            if (zone == null || !zone.Holds(_motor)) { zone = null; return false; }
            _settings = zone.Settings;
            return _settings.aim2D;
        }

        private void Begin(FlatZone zone)
        {
            Active = true;
            _zone = zone;
            if (_carrier == null) TryGetComponent(out _carrier);   // parts a body may be given after this one woke
            if (_moves == null) TryGetComponent(out _moves);
            _lane = _motor.Flat.Value;
            _rig = PlayerSlot.RigFor(this);
            if (_rig != null && _rig.Target != transform) _rig = null;   // not this body's camera (a party's other seat)
            From = Source.Rest;
            _pointerKnown = false;
            float facing = Vector3.Dot(transform.forward, _lane.Right);
            _side = facing < 0f ? -1f : 1f;
            // The cursor: only the player whose rig owns it (single player, the party's mouse seat), only one at a time.
            if (_cursorHolder == null && _rig != null && _rig.OwnsCursor && _input != null && _input.UsesMouse)
            {
                _rig.OwnsCursor = false;   // no click locks it again while we have it (ShoulderCamera.Update)
                _tookCursor = true;
                _cursorHolder = this;
            }
        }

        private void End()
        {
            Active = false;
            if (_wroteFacing && _motor != null) _motor.FaceYaw = null;   // PlayerMoves writes its own again next frame
            _wroteFacing = false;
            From = Source.Rest;
            GiveCursorBack();
            _zone = null;
        }

        /// <summary>The cursor goes back to the rig: locked and hidden again, as before the zone, when the game is on in
        /// Play with the rig's camera on. Otherwise whoever has it now keeps it (Build Mode, first person and the menus
        /// set it themselves; the rig locks it as it comes back on).</summary>
        private void GiveCursorBack()
        {
            if (!_tookCursor) return;
            _tookCursor = false;
            if (_cursorHolder == this) _cursorHolder = null;
            if (_rig == null) return;
            _rig.OwnsCursor = true;
            if (GameMode.Current == GameMode.Mode.Play && GameRunning())
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

        /// <summary>The game is being played: not paused, no menu, console or key sheet up, the rig's camera on and not
        /// held by the garage's menu.</summary>
        private bool GameRunning()
            => Time.timeScale > 0f && !PauseMenu.IsOpen && !TitleMenu.IsOpen && !DebugConsole.IsOpen && !KeyHelp.IsOpen
               && _rig != null && _rig.isActiveAndEnabled && !_rig.MenuHeld;

        /// <summary>While we have the cursor and the game is on: free, and shown (hidden while the pad's stick aims, if
        /// FlatView says so). Written only when it differs.</summary>
        private void KeepCursor(FlatViewSettings v)
        {
            if (!_tookCursor || !GameRunning()) return;
            bool show = !(v.hideCursorForStick && From == Source.Stick);
            if (Cursor.lockState != CursorLockMode.None) Cursor.lockState = CursorLockMode.None;
            if (Cursor.visible != show) Cursor.visible = show;
        }

        /// <summary>Which device aims: the stick past its dead zone takes it (and gives it back to rest when let go), the
        /// mouse takes it when it moves further than Mouse Takes Over from where it was when it last spoke.</summary>
        private void ReadDevices(FlatViewSettings v)
        {
            Vector2 stick = _input != null ? _input.LookStick : Vector2.zero;
            float dead = Mathf.Max(0.01f, v.stickDeadZone);
            bool pushed = stick.sqrMagnitude > dead * dead;
            if (pushed) { From = Source.Stick; _stick = stick.normalized; }
            else if (From == Source.Stick) From = Source.Rest;   // let go: the way the toy faces, level
            if (_input == null || !_input.PointerAt(out Vector2 at)) return;
            if (!_pointerKnown) { _pointerWas = at; _pointerKnown = true; return; }
            if (From == Source.Mouse) { _pointerWas = at; return; }
            float wake = v.mouseTakesOver;
            if (!pushed && (at - _pointerWas).sqrMagnitude > wake * wake) { From = Source.Mouse; _pointerWas = at; }
        }

        /// <summary>The aim now: <paramref name="origin"/> the chest on the lane, <paramref name="forward"/> the way to the
        /// aim (unit, in the lane's plane), <paramref name="point"/> the aim point itself (the cursor's place on the lane;
        /// for the stick or at rest, Stick Reach along the way). False while not Active (the camera's aim is the one).</summary>
        public bool Aim(out Vector3 origin, out Vector3 forward, out Vector3 point)
        {
            FlatLock lane = Lane;
            origin = lane.OnPlane(Chest);
            Vector3 right = lane.Right;
            forward = right * _side;
            point = origin + forward * (Settings.stickReach * Scale);
            if (!Active) return false;
            if (From == Source.Stick)
            {
                Vector3 way = right * _stick.x + Vector3.up * _stick.y;
                if (way.sqrMagnitude > 1e-6f) { forward = way.normalized; point = origin + forward * (Settings.stickReach * Scale); }
            }
            else if (From == Source.Mouse && CursorOnLane(lane, out Vector3 at))
            {
                point = at;
                Vector3 to = at - origin;
                float least = 0.01f * Scale;
                if (to.sqrMagnitude > least * least) forward = to.normalized;   // the cursor on the chest itself: the way it faces
            }
            return true;
        }

        /// <summary>Where the aim ray starts: the chest, on the lane.</summary>
        public Vector3 Origin { get { Aim(out Vector3 o, out _, out _); return o; } }
        /// <summary>The way the aim ray runs (unit, in the lane's plane).</summary>
        public Vector3 Forward { get { Aim(out _, out Vector3 f, out _); return f; } }

        /// <summary>Where the aim point is on this player's screen, pixels from the bottom left (the reticle's place). With
        /// the mouse it is the cursor. False when it is behind the camera or there is no camera.</summary>
        public bool ScreenPoint(out Vector2 pixels)
        {
            pixels = default;
            Camera view = View;
            if (!Active || view == null) return false;
            Aim(out _, out _, out Vector3 point);
            Vector3 onScreen = view.WorldToScreenPoint(point);
            if (onScreen.z <= 0f) return false;
            pixels = onScreen;
            return true;
        }

        /// <summary>Where the mouse cursor's screen ray meets the lane's plane. False with no mouse for this player, no
        /// camera, or a camera that does not see the plane from its side (still swinging in from behind).</summary>
        private bool CursorOnLane(FlatLock lane, out Vector3 at)
        {
            at = default;
            Camera view = View;
            if (view == null || _input == null || !_input.PointerAt(out Vector2 pixels)) return false;
            return lane.Meet(view.ScreenPointToRay(pixels), out at);
        }

        /// <summary>The body turns left or right along the lane toward the aim, through facing the camera, at Aim Turn
        /// Speed, by way of the motor's Face Yaw. Only while it is free (no roll, block or hit; no rope, grip, line or
        /// glider) and either aim is held / the gun's arm is up (PlayerMoves turned it to the view: the wall, side-on) or
        /// it has something to aim at a live aim (the mouse, or the stick pushed). At rest it keeps the way it faces and
        /// the aim follows that.</summary>
        private void Face(FlatViewSettings v)
        {
            bool movesOn = _moves != null && _moves.isActiveAndEnabled;
            bool viewFacing = movesOn && _motor.FaceYaw.HasValue;   // PlayerMoves wants the body on the aim: it gave the view's yaw
            if (_wroteFacing && !movesOn) _motor.FaceYaw = null;     // ours from last frame and nobody else writes it: start clean
            _wroteFacing = false;

            FlatLock lane = _lane;
            Vector3 right = lane.Right, toCamera = lane.normal;
            Vector3 body = transform.forward;
            float along = Vector3.Dot(body, right);
            if (From == Source.Rest)
            {
                if (Mathf.Abs(along) > 0.01f) _side = Mathf.Sign(along);   // the aim follows the way it faces
            }
            else
            {
                Aim(out Vector3 origin, out _, out Vector3 point);
                float aside = Vector3.Dot(point - origin, right);
                if (Mathf.Abs(aside) > v.aimTurnBand * Scale) _side = Mathf.Sign(aside);
            }

            bool free = (!movesOn || _moves.Free) && !_motor.Drive.HasValue && !_motor.HoldAt.HasValue && !_motor.Pull.HasValue
                        && _motor.Glide == null && !RopeRider.Holds(gameObject);
            bool hasAim = (_holder != null && _holder.Armed) || (_carrier != null && _carrier.ToolUp) || v.faceAimEmptyHanded;
            bool drive = free && (viewFacing || (hasAim && From != Source.Rest));
            if (!drive)
            {
                if (viewFacing) _motor.FaceYaw = null;   // a move has the body (PlayerMoves would not ask then; kept safe): never the wall
                return;
            }
            float dt = Time.deltaTime;
            // Where it faces now, as an angle from facing the camera (0) to the screen's right (+90) or left (-90); past
            // 90 either way it faces into the wall.
            float now = Mathf.Atan2(along, Vector3.Dot(body, toCamera)) * Mathf.Rad2Deg;
            float goal = 90f * _side, step = v.aimTurnSpeed * dt;
            // On the camera's side the turn goes through facing the camera (straight through 0); from the wall's side,
            // the short way round to the lane.
            float next = Mathf.Abs(now) <= 90.01f ? Mathf.MoveTowards(now, goal, step) : Mathf.MoveTowardsAngle(now, goal, step);
            float r = next * Mathf.Deg2Rad;
            Vector3 way = toCamera * Mathf.Cos(r) + right * Mathf.Sin(r);
            _motor.FaceYaw = Mathf.Atan2(way.x, way.z) * Mathf.Rad2Deg;
            _wroteFacing = true;
        }
    }
}
