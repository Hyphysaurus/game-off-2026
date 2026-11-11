using UnityEngine;
using UnityEngine.InputSystem;

namespace ShiftJam
{
    /// <summary>
    /// The controller's rumble while driving (DrivingFeelSettings, "Controller rumble"): a light engine hum that grows
    /// with speed on the heavy motor, more on the light motor while a boost pushes or the tyres slide, and a kick that
    /// dies away on a boost start, a landing and a crash. Only while a pad is the device in hand: pressing a key on the
    /// keyboard rests the motors until the pad is touched again, so a pad lying on the desk does not buzz. The motors
    /// are always put back to zero when the driver gets out, on pause (time stopped), when this is switched off and
    /// when the game quits. DrivingFeel ticks it.
    /// </summary>
    public sealed class DrivingRumble : MonoBehaviour
    {
        private const float Step = 0.02f;       // a change smaller than this is not worth sending to the pad
        private const float Resend = 0.2f;      // seconds after which the level is sent again anyway (another script may have zeroed the motors)
        private const float StickUse = 0.2f, TriggerUse = 0.1f;   // how far a stick or trigger must move to count as the pad being used
        private const float KickOnLight = 0.6f; // share of a jolt that also goes to the light motor

        private DrivingFeelSettings _settings;
        private Gamepad _pad;                   // the pad whose motors we set last
        private bool _running;
        private float _low, _high, _sentAt;
        private float _kick, _kickLeft;
        private float _padUsed, _keysUsed;      // unscaled times each device was last touched

        /// <summary>Hands over the tuning.</summary>
        public void Setup(DrivingFeelSettings settings) => _settings = settings;

        /// <summary>A jolt of <paramref name="strength"/> 0..1 on both motors that dies away over rumbleKickSeconds.
        /// A weaker jolt never cuts a stronger one short.</summary>
        public void Kick(float strength)
        {
            if (_settings == null) return;
            strength = Mathf.Clamp01(strength);
            if (strength <= KickNow) return;
            _kick = strength;
            _kickLeft = Mathf.Max(0.01f, _settings.rumbleKickSeconds);
        }

        private float KickNow => _kickLeft > 0f && _settings != null ? _kick * _kickLeft / Mathf.Max(0.01f, _settings.rumbleKickSeconds) : 0f;

        /// <summary>Once a frame: sets the motors from what the car is doing, or rests them.</summary>
        public void Tick(in DriveState state, float dt)
        {
            if (_kickLeft > 0f) _kickLeft = Mathf.Max(0f, _kickLeft - dt);
            var pad = Gamepad.current;
            if (_settings == null || !_settings.rumble || !state.Live || pad == null) { Stop(); return; }

            float now = Time.unscaledTime;
            if (Touched(pad)) _padUsed = now;
            var keys = Keyboard.current;
            if (keys != null && keys.anyKey.isPressed) _keysUsed = now;
            if (_keysUsed > _padUsed) { Stop(); return; }   // driving on the keyboard

            var s = _settings;
            float kick = KickNow;
            float low = Mathf.Clamp01(s.rumbleEngine * Mathf.Clamp01(state.Speed) + kick);
            float high = (state.Boosting ? s.rumbleBoost : 0f) + kick * KickOnLight;
            if (state.Grounded) high += s.rumbleSkid * Mathf.InverseLerp(s.skidFrom, Mathf.Max(s.skidFrom + 0.01f, s.skidFull), state.Slip);
            high = Mathf.Clamp01(high);

            if (pad != _pad) { Rest(); _pad = pad; }   // a different pad took over: rest the old one
            if (_running && Mathf.Abs(low - _low) < Step && Mathf.Abs(high - _high) < Step && now - _sentAt < Resend) return;
            pad.SetMotorSpeeds(low, high);
            _low = low; _high = high; _sentAt = now; _running = true;
        }

        /// <summary>Motors to zero now (and any jolt forgotten). Safe to call at any time, any number of times.</summary>
        public void Stop()
        {
            _kick = 0f; _kickLeft = 0f;
            Rest();
        }

        /// <summary>Zeroes the motors of the pad we last drove, if they are running.</summary>
        private void Rest()
        {
            if (!_running) return;
            _running = false; _low = 0f; _high = 0f;
            if (_pad != null && _pad.added) _pad.SetMotorSpeeds(0f, 0f);
        }

        private void OnDisable() => Stop();
        private void OnDestroy() => Stop();
        private void OnApplicationQuit() => Stop();
        private void OnApplicationFocus(bool focused) { if (!focused) Stop(); }

        /// <summary>The pad is being used right now: a stick pushed, a trigger pulled or a face or shoulder button down.</summary>
        private static bool Touched(Gamepad pad)
        {
            return pad.leftStick.ReadValue().sqrMagnitude > StickUse * StickUse
                || pad.rightStick.ReadValue().sqrMagnitude > StickUse * StickUse
                || pad.leftTrigger.ReadValue() > TriggerUse || pad.rightTrigger.ReadValue() > TriggerUse
                || pad.buttonSouth.isPressed || pad.buttonEast.isPressed || pad.buttonWest.isPressed || pad.buttonNorth.isPressed
                || pad.leftShoulder.isPressed || pad.rightShoulder.isPressed;
        }
    }
}
