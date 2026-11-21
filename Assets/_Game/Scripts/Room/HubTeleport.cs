using Unity.Netcode;
using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// A gate on foot between two places in one scene (D-0116): walk in and a short black cut puts the player at the
    /// target, facing its way. The Stadium's arena and its race track are joined by a pair. Online, only this PC's own
    /// player goes through (another player's copy walking in moves nothing here).
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public sealed class HubTeleport : MonoBehaviour
    {
        [SerializeField, Tooltip("Where the player comes out, facing its forward.")] private Transform target;
        [SerializeField, Min(0f)] private float cutOut = 0.12f, cutIn = 0.25f;

        private float _busyUntil;

        public void SetTarget(Transform t) => target = t;

        private void OnTriggerEnter(Collider other)
        {
            if (target == null || Time.unscaledTime < _busyUntil) return;
            Shifter who = PlayerTouch.Find(other, out _);
            if (who == null || !who.TryGetComponent(out PlayerMotor motor)) return;
            if (who.TryGetComponent(out NetPlayer net) && net.IsSpawned && !net.IsOwner) return;   // another player's copy
            _busyUntil = Time.unscaledTime + cutOut + cutIn + 0.3f;
            Vector3 at = target.position; Quaternion facing = Quaternion.Euler(0f, target.eulerAngles.y, 0f);
            var fade = ScreenFade.Find();
            if (fade != null) fade.Cut(cutOut, () => motor.Warp(at, facing), cutIn);
            else motor.Warp(at, facing);
            if (who.TryGetComponent(out Respawn respawn)) respawn.SetPoint(at, facing);   // a fall here comes back here
        }
    }
}
