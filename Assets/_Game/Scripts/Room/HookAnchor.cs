using System.Collections.Generic;
using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// A target the Dart Line's dart holds on to (D-0109): a suction-cup pad on furniture. Two kinds:
    /// - Pull-up: the line reels the toy up to it, through its Via points in order, to its Land point on the ledge.
    /// - Zip start: the start of a ZipLine; the toy is reeled to the line and rides it to its end.
    /// Only these hold a dart: anything else and the dart drops (every route is placed, not climbed anywhere). It glows
    /// faintly, brighter while it is the one the Dart Line would hit.
    /// </summary>
    public sealed class HookAnchor : MonoBehaviour
    {
        public enum Kind { PullUp, ZipStart }

        /// <summary>Every anchor in the loaded scenes.</summary>
        public static readonly List<HookAnchor> All = new List<HookAnchor>();

        [SerializeField] private Kind kind = Kind.PullUp;
        [SerializeField, Tooltip("Pull-up: where the toy's feet end up (on the ledge, clear of its edge).")] private Transform land;
        [SerializeField, Tooltip("Pull-up: points the toy is reeled through on the way, in order (round an edge, up through a hole).")] private Transform[] via = new Transform[0];
        [SerializeField, Tooltip("Zip start: the line it starts.")] private ZipLine zip;
        [SerializeField, Tooltip("The glow quad: grows while targeted.")] private Transform glow;
        [SerializeField] private float glowRest = 1f, glowTargeted = 1.8f;

        private bool _targeted;
        private Vector3 _glowScale;

        public Kind Type => kind;
        public ZipLine Zip => zip;
        /// <summary>Where the dart sticks.</summary>
        public Vector3 Point => transform.position;

        private void Awake() { if (glow != null) _glowScale = glow.localScale; }
        private void OnEnable() { if (!All.Contains(this)) All.Add(this); }
        private void OnDisable() { All.Remove(this); SetTargeted(false); }

        /// <summary>The points a pull-up reels the toy's feet through, ending at the landing.</summary>
        public void Path(List<Vector3> into)
        {
            into.Clear();
            foreach (var v in via) if (v != null) into.Add(v.position);
            into.Add(land != null ? land.position : transform.position);
        }

        /// <summary>Glows brighter while the Dart Line is pointed at it.</summary>
        public void SetTargeted(bool on)
        {
            if (_targeted == on) return;
            _targeted = on;
            if (glow != null) glow.localScale = _glowScale * (on ? glowTargeted : glowRest);
        }
    }
}
