using UnityEngine;
using UnityEngine.Rendering;

namespace ShiftJam
{
    /// <summary>
    /// The claw machine's moving parts (D-0229) and the three numbers they stand for: where the claw's middle is over the
    /// case (<see cref="At"/>: the machine's own x and z), how far the claw has gone down (<see cref="Depth"/>) and the
    /// fingers' angle (<see cref="Fingers"/>). Set a number and the parts follow, the way the claw pack's own ClawMechanism
    /// moves them: the gantry (Claw_Mechanism_01) slides on its own z, the carriage (Claw_Mechanism_02) along it on its own
    /// x, the head (Claw_Piston_02) moves down out of the rod (Claw_Piston_01) and a stretched copy of the rod's own mesh
    /// fills the gap, and each finger (Claw_01) turns about its own x from where the model has it.
    /// On top, a small swing (<see cref="Swing"/>): the rod and everything under it tilt against every change of the claw's
    /// speed (steering and tweens alike) and settle back, never past the settings' Max Swing; the lever tilts with the
    /// steering and the button goes in on the drop.
    /// Lengths are the machine's own units (the pack's metres). Not a component: ClawGame owns one (its tweens drive the
    /// numbers), and the maker makes one to measure the claw with; only <see cref="MakeStretch"/> adds anything to the
    /// scene, and only ClawGame calls it, at run time.
    /// </summary>
    internal sealed class ClawGameClaw
    {
        private const float AngleStep = 0.5f;   // degrees between the finger positions looked at when measuring

        private readonly Transform _machine, _rail, _carriage, _rod, _head, _lever, _button;
        private readonly Transform[] _fingers;
        private readonly Vector3 _railRest, _carriageRest, _headRest, _buttonRest;
        private readonly Quaternion _rodRest, _leverRest;
        private readonly Quaternion[] _fingerRest;
        private readonly Vector3[] _fingerAt, _fingerScale;
        private readonly Vector3 _axis;          // the fingers' hinges' middle, in the head's own space
        private readonly float _headUnit;        // one of the head's own units, in machine units
        private readonly Vector3 _tip;           // a fingertip, in a finger's own space
        private readonly float _close;           // +1: a finger closes at positive angles

        private Vector2 _at;
        private float _depth, _angle;
        private Vector2 _swing, _swingSpeed, _lastAt, _lastVelocity;
        private bool _swingReady, _swingStill;
        private Transform _stretch;
        private MeshRenderer _stretchRenderer;
        private float _rodTop, _rodLength;

        /// <summary>False when a part is missing: nothing is moved.</summary>
        public bool Valid { get; }
        /// <summary>The claw's middle at rest (the machine's own x, z): where the model has it, and where it goes home to.</summary>
        public Vector2 Home { get; }
        /// <summary>The fingers' hinges' height at rest, in the machine's own space.</summary>
        public float GripRestY { get; }
        /// <summary>+1 when a finger closes at positive angles (the pack's), -1 the other way.</summary>
        public float CloseSign => _close;

        /// <summary>Takes the parts as they stand now as their rest. <paramref name="tip"/>: a fingertip in a finger's own
        /// space; <paramref name="closeSign"/>: +1 when the fingers close at positive angles.</summary>
        public ClawGameClaw(Transform machine, Transform rail, Transform carriage, Transform rod, Transform head, Transform[] fingers,
            Transform lever, Transform button, Vector3 tip, float closeSign)
        {
            _machine = machine; _rail = rail; _carriage = carriage; _rod = rod; _head = head; _lever = lever; _button = button;
            _fingers = fingers ?? new Transform[0];
            _tip = tip;
            _close = closeSign < 0f ? -1f : 1f;
            Valid = machine != null && rail != null && carriage != null && rod != null && head != null && _fingers.Length > 0;
            foreach (Transform f in _fingers) if (f == null) Valid = false;
            _fingerRest = new Quaternion[_fingers.Length];
            _fingerAt = new Vector3[_fingers.Length];
            _fingerScale = new Vector3[_fingers.Length];
            if (!Valid) return;

            _railRest = rail.localPosition;
            _carriageRest = carriage.localPosition;
            _headRest = head.localPosition;
            _rodRest = rod.localRotation;
            if (lever != null) _leverRest = lever.localRotation;
            if (button != null) _buttonRest = button.localPosition;
            Vector3 sum = Vector3.zero;
            for (int i = 0; i < _fingers.Length; i++)
            {
                _fingerRest[i] = _fingers[i].localRotation;
                _fingerAt[i] = _fingers[i].localPosition;
                _fingerScale[i] = _fingers[i].localScale;
                sum += _fingerAt[i];
            }
            _axis = sum / _fingers.Length;
            _headUnit = head.lossyScale.y / Mathf.Max(1e-6f, machine.lossyScale.y);
            Vector3 grip = machine.InverseTransformPoint(head.TransformPoint(_axis));
            Home = new Vector2(grip.x, grip.z);
            GripRestY = grip.y;
            _at = Home;
            _lastAt = Home;
        }

        // ------------------------------------------------------------------------------------------------ the three numbers

        /// <summary>The claw's middle over the case, the machine's own x and z. Set: the gantry and the carriage slide there.</summary>
        public Vector2 At
        {
            get => _at;
            set
            {
                _at = value;
                if (!Valid) return;
                _rail.localPosition = _railRest + Vector3.forward * (value.y - Home.y);
                _carriage.localPosition = _carriageRest + Vector3.right * (value.x - Home.x);
            }
        }

        /// <summary>How far the claw has gone down from its rest, machine units. Set: the head moves down out of the rod and
        /// the rod's stretched copy reaches it.</summary>
        public float Depth
        {
            get => _depth;
            set
            {
                _depth = Mathf.Max(0f, value);
                if (!Valid) return;
                _head.localPosition = _headRest + Vector3.down * (_depth / Mathf.Max(1e-6f, _headUnit));
                Stretch();
            }
        }

        /// <summary>The fingers' angle, degrees from where the model has them (0): the settings' Finger Open is open, Finger
        /// Closed fully closed.</summary>
        public float Fingers
        {
            get => _angle;
            set
            {
                _angle = value;
                if (!Valid) return;
                for (int i = 0; i < _fingers.Length; i++) _fingers[i].localRotation = _fingerRest[i] * Quaternion.AngleAxis(value * _close, Vector3.right);
            }
        }

        /// <summary>The fingers' hinges' middle in the world now (the claw's middle, swing and all).</summary>
        public Vector3 GripWorld => Valid ? _head.TransformPoint(_axis) : Vector3.zero;

        /// <summary>The head: a prize held by the claw hangs from it.</summary>
        public Transform Head => _head;

        /// <summary>Everything back where the model has it: home, all the way up, fingers as modelled, hanging straight.</summary>
        public void Rest()
        {
            if (!Valid) return;
            At = Home; Depth = 0f; Fingers = 0f;
            _swing = _swingSpeed = Vector2.zero; _lastAt = Home; _lastVelocity = Vector2.zero; _swingReady = false; _swingStill = true;
            _rod.localRotation = _rodRest;
            if (_lever != null) _lever.localRotation = _leverRest;
            if (_button != null) _button.localPosition = _buttonRest;
        }

        // ------------------------------------------------------------------------------------------------ measuring

        /// <summary>A fingertip in the head's own space at <paramref name="angle"/>.</summary>
        private Vector3 TipAt(int finger, float angle) =>
            _fingerAt[finger] + _fingerRest[finger] * (Quaternion.AngleAxis(angle * _close, Vector3.right) * Vector3.Scale(_fingerScale[finger], _tip));

        /// <summary>How many fingers the claw has.</summary>
        public int FingerCount => _fingers.Length;

        /// <summary>Where finger <paramref name="finger"/>'s tip would be in the world at <paramref name="angle"/>, the claw
        /// where it is now.</summary>
        public Vector3 TipWorld(int finger, float angle) => Valid ? _head.TransformPoint(TipAt(finger, angle)) : Vector3.zero;

        /// <summary>How far the fingertips are from the claw's middle at <paramref name="angle"/>, machine units: the
        /// farthest one (<paramref name="widest"/>) or the nearest.</summary>
        public float TipRadius(float angle, bool widest)
        {
            if (!Valid) return 0f;
            float best = widest ? 0f : float.MaxValue;
            for (int i = 0; i < _fingers.Length; i++)
            {
                Vector3 t = TipAt(i, angle) - _axis;
                float r = new Vector2(t.x, t.z).magnitude * _headUnit;
                best = widest ? Mathf.Max(best, r) : Mathf.Min(best, r);
            }
            return best;
        }

        /// <summary>How far the lowest fingertip is under the hinges at <paramref name="angle"/>, machine units.</summary>
        public float TipBelow(float angle)
        {
            if (!Valid) return 0f;
            float lowest = 0f;
            for (int i = 0; i < _fingers.Length; i++) lowest = Mathf.Max(lowest, (_axis.y - TipAt(i, angle).y) * _headUnit);
            return lowest;
        }

        /// <summary>The lowest a fingertip goes under the hinges while the fingers turn from <paramref name="from"/> to
        /// <paramref name="to"/> (a closing finger sweeps lower than it starts), machine units.</summary>
        public float LowestBetween(float from, float to)
        {
            float lowest = TipBelow(to);
            float step = from <= to ? AngleStep : -AngleStep;
            for (float a = from; from <= to ? a < to : a > to; a += step) lowest = Mathf.Max(lowest, TipBelow(a));
            return lowest;
        }

        /// <summary>The angle, from <paramref name="open"/> toward <paramref name="closed"/>, at which the nearest fingertip
        /// comes within <paramref name="radius"/> of the claw's middle (machine units): where the fingers meet the sides of a
        /// prize that wide. <paramref name="closed"/> when they never do.</summary>
        public float StopAngle(float open, float closed, float radius)
        {
            float step = open <= closed ? AngleStep : -AngleStep;
            for (float a = open; open <= closed ? a < closed : a > closed; a += step)
                if (TipRadius(a, false) <= radius) return a;
            return closed;
        }

        /// <summary>The maker's check of the hinge: +1 when turning the fingers to positive angles brings the tips in (as
        /// the pack's), -1 when it takes them out. Measured with this claw's own sign, so it is the sign to multiply by.</summary>
        public float MeasuredCloseSign() => TipRadius(10f, false) <= TipRadius(-10f, false) ? 1f : -1f;

        // ------------------------------------------------------------------------------------------------ the rod's stretch

        /// <summary>The stretched copy of the rod's own mesh that fills the gap as the head goes down (the pack's "extending
        /// rod visual"): made at run time under the rod, hidden while the claw is all the way up. Nothing is saved.</summary>
        public void MakeStretch()
        {
            if (!Valid || _stretch != null) return;
            MeshFilter mesh = _rod.GetComponent<MeshFilter>();
            MeshRenderer look = _rod.GetComponent<MeshRenderer>();
            if (mesh == null || look == null || mesh.sharedMesh == null) return;
            Bounds b = mesh.sharedMesh.bounds;
            _rodTop = b.max.y;
            _rodLength = b.size.y;
            if (_rodLength < 1e-5f) return;
            var go = new GameObject("ClawRodStretch (run time)");
            go.layer = _rod.gameObject.layer;
            _stretch = go.transform;
            _stretch.SetParent(_rod, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh.sharedMesh;
            _stretchRenderer = go.AddComponent<MeshRenderer>();
            _stretchRenderer.sharedMaterials = look.sharedMaterials;
            _stretchRenderer.shadowCastingMode = look.shadowCastingMode;
            _stretchRenderer.receiveShadows = look.receiveShadows;
            _stretchRenderer.lightProbeUsage = LightProbeUsage.Off;
            _stretchRenderer.reflectionProbeUsage = look.reflectionProbeUsage;
            Stretch();
        }

        /// <summary>The copy reaches from the rod's top to the head: its top kept where the rod's is.</summary>
        private void Stretch()
        {
            if (_stretch == null) return;
            float grow = (_rodLength + _depth / Mathf.Max(1e-6f, _headUnit)) / _rodLength;
            bool show = _depth > 1e-4f;
            if (_stretchRenderer.enabled != show) _stretchRenderer.enabled = show;
            _stretch.localPosition = new Vector3(0f, _rodTop * (1f - grow), 0f);
            _stretch.localRotation = Quaternion.identity;
            _stretch.localScale = new Vector3(1f, grow, 1f);
        }

        // ------------------------------------------------------------------------------------------------ the swing, lever, button

        /// <summary>Once a frame, after the tweens: the rod tilts against every change of the claw's speed across the case
        /// (the bottom lags behind a start and runs on past a stop) and springs back, damped, never past Max Swing.</summary>
        public void Swing(float dt, ClawGameSettings s)
        {
            if (!Valid || dt <= 0f || s == null) return;
            Vector2 velocity = (_at - _lastAt) / dt;
            if (!_swingReady) { _lastVelocity = velocity; _swingReady = true; }
            Vector2 change = velocity - _lastVelocity;
            _lastVelocity = velocity; _lastAt = _at;
            // Hanging still and nothing changed: the rod is already straight, nothing to write.
            if (_swingStill && change.sqrMagnitude < 1e-10f && _swingSpeed.sqrMagnitude < 1e-8f) return;
            _swingStill = false;
            // About the rod's x, a speed-up toward +z leaves the bottom behind (toward -z: positive); about its z, a speed-up
            // toward +x leaves it toward -x (negative).
            _swingSpeed += new Vector2(change.y, -change.x) * s.swingKick;
            _swingSpeed += (-s.swingSpring * _swing - s.swingDamping * _swingSpeed) * dt;
            _swing += _swingSpeed * dt;
            if (_swing.magnitude > s.maxSwing)
            {
                _swing = _swing.normalized * s.maxSwing;
                _swingSpeed *= 0.5f;   // it met its limit: most of its way is spent
            }
            if (_swing.sqrMagnitude < 1e-6f && _swingSpeed.sqrMagnitude < 1e-4f)
            {
                _swing = _swingSpeed = Vector2.zero;   // settled: straight, and left alone until the claw moves again
                _swingStill = true;
            }
            _rod.localRotation = _rodRest * Quaternion.Euler(_swing.x, 0f, _swing.y);
        }

        /// <summary>A jolt to the swing (the fingers taking hold), degrees a second about the rod's x and z.</summary>
        public void Kick(Vector2 degreesPerSecond) { _swingSpeed += degreesPerSecond; _swingStill = false; }

        /// <summary>The lever tilted by the steering (x right, y away), each -1..1, at most <paramref name="degrees"/> (the
        /// pack's ClawInput turns it the same way).</summary>
        public void Lever(Vector2 steer, float degrees)
        {
            if (_lever != null) _lever.localRotation = _leverRest * Quaternion.Euler(-steer.y * degrees, 0f, steer.x * degrees);
        }

        /// <summary>The button pushed in this far, machine units (0 = out).</summary>
        public void Button(float depth)
        {
            if (_button != null) _button.localPosition = _buttonRest + Vector3.down * depth;
        }
    }
}
