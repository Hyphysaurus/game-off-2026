using System.Collections;
using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// The winner's podium (D-0037). When a race is won, rockets go up from its top one after another and burst into
    /// sparks above it, each with a pop; the finish line plays the win jingle. It can stand anywhere: best where the finish
    /// looks at it. The sparks are one world-space particle system that only this script emits into. Sizes and speeds are
    /// at scale 1 and grow with the podium.
    /// </summary>
    public sealed class RacePodium : MonoBehaviour
    {
        [SerializeField, Tooltip("World-space particles the show emits into (nothing plays by itself); rockets leave from here.")]
        private ParticleSystem sparks;
        [SerializeField, Tooltip("The middle of where rockets burst, local units above the podium's foot.")] private Vector3 burstCentre = new Vector3(0f, 16f, 0f);
        [SerializeField, Tooltip("How far around that middle they burst: a box, local units.")] private Vector3 burstSpread = new Vector3(18f, 6f, 18f);
        [SerializeField, Min(1)] private int rockets = 8;
        [SerializeField, Min(0f), Tooltip("Seconds over which the rockets go up.")] private float showSeconds = 3f;
        [SerializeField, Min(0.1f), Tooltip("Seconds from launch to burst.")] private float riseSeconds = 0.7f;
        [SerializeField, Min(1)] private int sparksPerBurst = 60;
        [SerializeField, Tooltip("Burst speed, units per second.")] private float sparkSpeed = 6f;
        [SerializeField] private float sparkSize = 1.1f;
        [SerializeField, Tooltip("Seconds a spark lives.")] private float sparkLife = 1.3f;
        [SerializeField] private Color[] colours =
        {
            new Color(1f, 0.8f, 0.25f), new Color(1f, 0.35f, 0.6f), new Color(0.3f, 0.85f, 1f), new Color(0.45f, 1f, 0.4f), new Color(0.75f, 0.45f, 1f),
        };
        [SerializeField, Tooltip("Played at each burst.")] private AudioClip popClip;
        [SerializeField, Range(0f, 1f)] private float popVolume = 0.5f;

        private AudioSource _audio;
        private Coroutine _show;

        private float Scale => Mathf.Max(0.001f, transform.lossyScale.y);

        private void Awake() { _audio = GetComponent<AudioSource>(); }
        private void OnEnable() { RaceState.Finished += Celebrate; }

        private void OnDisable()
        {
            RaceState.Finished -= Celebrate;
            StopAllCoroutines();
            _show = null;
        }

        /// <summary>Starts the show (a race was won); one already going starts over.</summary>
        public void Celebrate()
        {
            if (!isActiveAndEnabled || sparks == null) return;
            if (_show != null) StopCoroutine(_show);
            _show = StartCoroutine(Show());
        }

        private IEnumerator Show()
        {
            if (!sparks.isPlaying) sparks.Play();
            var gap = new WaitForSeconds(rockets > 1 ? showSeconds / (rockets - 1) : 0f);
            for (int i = 0; i < rockets; i++)
            {
                StartCoroutine(Rocket());
                if (i < rockets - 1) yield return gap;
            }
            _show = null;
        }

        private IEnumerator Rocket()
        {
            float s = Scale, t = Mathf.Max(0.1f, riseSeconds);
            Vector3 from = sparks.transform.position;
            Vector3 jitter = new Vector3(Random.value - 0.5f, Random.value - 0.5f, Random.value - 0.5f);
            Vector3 to = transform.TransformPoint(burstCentre + Vector3.Scale(burstSpread, jitter));
            Color c = colours != null && colours.Length > 0 ? colours[Random.Range(0, colours.Length)] : Color.white;
            // The rocket falls under the particles' own gravity, so it is aimed to arrive at the burst point on time.
            Vector3 g = Physics.gravity * sparks.main.gravityModifier.constant;
            Emit(from, (to - from) / t - 0.5f * t * g, Color.Lerp(c, Color.white, 0.6f), sparkSize * 1.4f * s, t);
            yield return new WaitForSeconds(t);
            for (int i = 0; i < sparksPerBurst; i++)
                Emit(to, Random.onUnitSphere * (sparkSpeed * s * Random.Range(0.75f, 1f)), c, sparkSize * s * Random.Range(0.7f, 1.1f), sparkLife * Random.Range(0.8f, 1.2f));
            if (popClip != null && _audio != null)
            {
                _audio.pitch = Random.Range(0.85f, 1.15f);
                _audio.PlayOneShot(popClip, popVolume);
            }
        }

        private void Emit(Vector3 at, Vector3 velocity, Color colour, float size, float life)
        {
            var p = new ParticleSystem.EmitParams
            {
                position = at, velocity = velocity, startColor = colour, startSize = size, startLifetime = life, applyShapeToPosition = false,
            };
            sparks.Emit(p, 1);
        }
    }
}
