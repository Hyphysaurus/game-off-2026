using DG.Tweening;
using UnityEngine;
using UnityEngine.Events;

namespace ShiftJam
{
    /// <summary>
    /// The lost toy in the garden's flowerbed (D-0239). This PC's player touching it (either size) finds it, once a visit:
    /// it hops and turns round (DOTween), a sound plays, and On Found fires: the backyard maker wires it to an
    /// ObjectiveHook that earns the gold star "Lost in the garden" (the star is saved per slot; the hop is not). Online
    /// nothing is earned (objectives are off in a party) and another player's copy never finds it. The hop's numbers and
    /// sound are on Resources/BackyardSettings.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public sealed class LostToy : MonoBehaviour
    {
        [SerializeField, Tooltip("The toy's model: what hops when it is found.")] private Transform model;
        [SerializeField, Tooltip("Fired once when it is found: the maker wires the star's ObjectiveHook here.")] private UnityEvent onFound;

        private bool _found;
        private AudioSource _audio;

        /// <summary>True once it has been found this visit.</summary>
        public bool Found => _found;

        private void Reset() => GetComponent<Collider>().isTrigger = true;
        private void Awake() => GetComponent<Collider>().isTrigger = true;

        private void OnTriggerEnter(Collider other)
        {
            if (_found) return;
            Shifter who = PlayerTouch.Find(other, out _);   // on foot, or the first-person body
            if (who == null) return;
            ShiftCharge charge = who.GetComponent<ShiftCharge>();
            if (charge == null || !charge.HasHud) return;   // this PC's own player only
            _found = true;
            onFound?.Invoke();
            BackyardSettings s = BackyardSettings.Load();
            Play(s.lostToyFound);
            if (model == null) return;
            float seconds = Mathf.Max(0.05f, s.lostToyHopSeconds);
            float parent = model.parent != null ? Mathf.Max(1e-4f, Mathf.Abs(model.parent.lossyScale.y)) : 1f;
            model.DOLocalJump(model.localPosition, s.lostToyHop / parent, 1, seconds).SetLink(gameObject);
            model.DOLocalRotate(new Vector3(0f, 360f, 0f), seconds, RotateMode.LocalAxisAdd).SetEase(Ease.OutQuad).SetLink(gameObject);
        }

        private void Play(CourseSounds.Cue cue)
        {
            if (cue.clip == null || cue.volume <= 0f) return;
            if (_audio == null)
            {
                _audio = gameObject.AddComponent<AudioSource>();
                _audio.playOnAwake = false;
                _audio.spatialBlend = 0f;
            }
            _audio.pitch = 1f + Random.Range(-cue.pitchSpread, cue.pitchSpread);
            _audio.PlayOneShot(cue.clip, cue.volume);
        }
    }
}
