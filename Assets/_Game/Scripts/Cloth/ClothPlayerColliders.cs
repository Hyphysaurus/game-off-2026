using UnityEngine;
using MC = MagicaCloth2;

namespace ShiftJam
{
    /// <summary>
    /// A player's pushing capsule for the Magica cloths (D-0233): one Magica capsule collider on a child of the player,
    /// "Cloth Pusher (D-0233)", kept on the capsule the player walks with, so the toy and the kid push the curtains' cords
    /// and the fort flags aside as they walk into them. It rides the player's shift by itself (the player's whole object
    /// grows and shrinks, the child with it) and its size is ClothSettings' share of the walking capsule's: Toy Radius /
    /// Toy Height at toy size, Kid Radius / Kid Height at kid size, blended as the shift grows. In first person with the
    /// separate body (Ride Player off) it stands on that body's capsule instead, which is the one that walks; with Ride
    /// Player on (as it ships) the player's own capsule walks, and it stays on that.
    /// Not placed by hand: ClothDresser gives it to every player it finds (a Shifter with a CharacterController: this PC's
    /// own and the others' copies, so the cloths on each PC are pushed by every body that PC sees; nothing is sent online)
    /// and hands its collider to every cloth. Its size is set again only when it changes (a crouch, a blend between the
    /// shares): nothing is made every frame.
    /// OFF SWITCH: ClothSettings > Player Pushes (the capsule is switched off: the cloth lets them through).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ClothPlayerColliders : MonoBehaviour
    {
        private Shifter _shifter;
        private CharacterController _own;
        private FirstPersonMode _firstPerson;
        private Transform _pusher;
        private MC.MagicaCapsuleCollider _capsule;
        private Vector3 _size = new Vector3(-1f, -1f, -1f);

        /// <summary>The Magica capsule the cloths are given.</summary>
        public MC.MagicaCapsuleCollider Capsule => _capsule;

        private void Awake()
        {
            TryGetComponent(out _shifter);
            TryGetComponent(out _own);
            TryGetComponent(out _firstPerson);
            var go = new GameObject("Cloth Pusher (D-0233)");
            _pusher = go.transform;
            _pusher.SetParent(transform, false);
            _capsule = go.AddComponent<MC.MagicaCapsuleCollider>();
            _capsule.direction = MC.MagicaCapsuleCollider.Direction.Y;   // upright, like the walking capsule
            _capsule.alignedOnCenter = true;
            _capsule.reverseDirection = false;
            _capsule.center = Vector3.zero;
            Follow();
            ClothDresser.AddPusher(_capsule);
        }

        private void OnDestroy()
        {
            ClothDresser.RemovePusher(_capsule);
            if (_pusher != null) Destroy(_pusher.gameObject);
        }

        private void LateUpdate() => Follow();

        /// <summary>Puts the capsule on the capsule that walks now and sizes it (only when its size changes).</summary>
        private void Follow()
        {
            if (_pusher == null || _capsule == null) return;
            ClothSettings s = ClothSettings.Load();
            if (_pusher.gameObject.activeSelf != s.playerPushes) _pusher.gameObject.SetActive(s.playerPushes);
            if (!s.playerPushes) return;
            CharacterController walks = _own;
            FirstPersonBody body = _firstPerson != null ? _firstPerson.Body : null;   // null outside first person
            if (body != null && body.Capsule != null) walks = body.Capsule;            // the player's own while it rides the player
            if (walks == null) return;

            Transform on = walks.transform;
            _pusher.position = on.TransformPoint(walks.center);
            Vector3 scale = on.lossyScale;
            float kid = KidShare();
            float radius = walks.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z)) * Mathf.Lerp(s.toyRadius, s.kidRadius, kid);
            float height = walks.height * Mathf.Abs(scale.y) * Mathf.Lerp(s.toyHeight, s.kidHeight, kid);
            height = Mathf.Max(height, 2f * radius);
            float unit = Mathf.Abs(_pusher.lossyScale.y);   // Magica sizes the capsule by its own object's scale (the player's)
            if (unit < 1e-6f) return;
            var size = new Vector3(radius / unit, radius / unit, height / unit);
            if ((size - _size).sqrMagnitude <= 1e-6f * Mathf.Max(1e-6f, size.sqrMagnitude)) return;
            _size = size;
            _capsule.SetSize(size.x, size.y, size.z);   // start radius, end radius, length tip to tip
            _capsule.UpdateParameters();
        }

        /// <summary>How far the player is from toy size (0) to kid size (1), even to the eye (the shift's own geometric blend).</summary>
        private float KidShare()
        {
            if (_shifter == null) return 0f;
            float toy = _shifter.ScaleOf(ShiftSize.Toy), kid = _shifter.ScaleOf(ShiftSize.Kid);
            if (toy <= 0f || kid <= 0f || Mathf.Approximately(toy, kid)) return 0f;
            return Mathf.Clamp01(Mathf.Log(Mathf.Max(1e-6f, _shifter.Scale) / toy) / Mathf.Log(kid / toy));
        }
    }
}
