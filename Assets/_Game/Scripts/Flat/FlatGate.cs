using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace ShiftJam
{
    /// <summary>
    /// A Flat Zone's gate (D-0203): the way in (Entrance) or the way out (Exit, "a portal to shift back"), standing
    /// across the lane. In the hallway both are his portal drawing (the car portal's own look, D-0073: the swirl turns
    /// in its material all the time, the small marks go out as a ripple), in a colour of each gate's own: the glow on
    /// the floor under it and its flash.
    /// - ENTRANCE: when its zone takes a player (the camera swings side-on), the gate flashes, the ripple goes out and
    ///   its sound plays. It does nothing else: the zone's box, which starts just past it, is what takes the player.
    /// - EXIT: a player the zone holds runs into its trigger and is let go at once (FlatZone.Clear: the camera swings
    ///   back behind it, the stretch is 3D again), and the gate flashes, ripples and sounds. WHEN IT COUNTS: only for a
    ///   player the zone is holding at that moment; the zone then leaves that player alone until its body has left the
    ///   box, so each visit goes out once. Anyone else (first person, a kid, a player walking in from the far side)
    ///   runs through it and nothing happens. An entrance's trigger is switched off.
    /// The body has no collider: the toy runs straight through. It lives under its zone's object, so the zone's
    /// cutaway never hides it. Its LOOK is all in its prefab (Prefabs/Flat/FlatGate_Portal, made by Shift Jam > Flat >
    /// Land Hallway Scroller): another look (a rift) is another prefab with this script and its own parts in these
    /// fields, put where this one stands; no code changes.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FlatGate : MonoBehaviour
    {
        public enum Role { Entrance, Exit }

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [SerializeField, Tooltip("The zone this gate belongs to. Found on a parent if empty.")] private FlatZone zone;
        [SerializeField, Tooltip("Entrance: plays when the zone takes a player. Exit: its trigger lets a held player go (the zone's Clear) and plays.")] private Role role = Role.Entrance;
        [SerializeField, Tooltip("The gate's own colour: the glow on the floor under it, its flash light, and the wash over the drawing in a flash. Keep it apart from the car portals' red, green and blue.")] private Color colour = new Color32(0xFF, 0x4F, 0xA3, 0xFF);

        [Header("Its look (the prefab's parts)")]
        [SerializeField, Tooltip("The ripple of small marks sent out when the gate plays. May be empty.")] private PortalRipple ripple;
        [SerializeField, Tooltip("What brightens in a flash (the drawing's swirl and its dark middle). Their material's _BaseColor is the calm colour.")] private Renderer[] glow;
        [SerializeField, Tooltip("What wears the gate's colour all the time (the glow on the floor), laid over its material's colour; the material's own alpha is kept.")] private Renderer[] tinted;
        [SerializeField, Tooltip("A light in the gate's colour, on only while it flashes. May be empty.")] private Light flashLight;

        [Header("The flash")]
        [SerializeField, Min(0f), Tooltip("Share of its colour added at the peak of a flash (the car portals use 2.5).")] private float flashBoost = 2.5f;
        [SerializeField, Range(0f, 1f), Tooltip("How far the drawing is washed toward the gate's colour at the peak of a flash. 0 = brighter only.")] private float flashWash = 0.5f;
        [SerializeField, Min(0.01f), Tooltip("Seconds a flash takes to die away.")] private float flashTime = 0.6f;
        [SerializeField, Min(0f), Tooltip("The flash light's brightness at the peak.")] private float lightIntensity = 4f;
        [SerializeField, Tooltip("The gate's sound when it plays, heard at any distance (the side-on camera stands far off). No clip: silent.")] private CourseSounds.Cue sound = new CourseSounds.Cue(null, 0.7f);
        [SerializeField, Tooltip("Raised when the gate plays (after the zone has taken or let go the player).")] private UnityEvent onPlayed;

        private readonly List<(Renderer r, Color calm)> _slots = new List<(Renderer, Color)>();
        private MaterialPropertyBlock _block;
        private Collider _touch;
        private float _flashUntil;
        private bool _flashing;

        private void Awake()
        {
            if (zone == null) zone = GetComponentInParent<FlatZone>();
            if (zone == null) Debug.LogWarning("FlatGate '" + name + "' has no Flat Zone above it: it does nothing. Put it under a FlatZone's object.", this);
            _block = new MaterialPropertyBlock();
            if (glow != null)
                foreach (Renderer r in glow)
                    if (r != null && r.sharedMaterial != null && r.sharedMaterial.HasProperty(BaseColorId))
                        _slots.Add((r, r.sharedMaterial.GetColor(BaseColorId)));
            _touch = GetComponent<Collider>();
            if (_touch != null) _touch.enabled = role == Role.Exit;   // only the way out is touched
            else if (role == Role.Exit) Debug.LogWarning("FlatGate '" + name + "' is an exit with no trigger on its own object: nobody can go out through it.", this);
            if (flashLight != null) { flashLight.enabled = false; flashLight.intensity = 0f; }
            Tint();
        }

        private void OnEnable() { if (zone != null && role == Role.Entrance) zone.Entered += Play; }
        private void OnDisable()
        {
            if (zone != null) zone.Entered -= Play;
            if (_flashing) { _flashing = false; Calm(); }
        }

        /// <summary>In the editor: a gate standing in a scene shows its colour on the floor glow as it is set (the prefab
        /// asset itself is left alone).</summary>
        private void OnValidate() { if (!Application.isPlaying && gameObject.scene.IsValid()) Tint(); }

        /// <summary>The gate's colour onto its tinted parts (their material's alpha kept) and its light.</summary>
        private void Tint()
        {
            _block ??= new MaterialPropertyBlock();
            if (tinted != null)
                foreach (Renderer r in tinted)
                {
                    if (r == null || r.sharedMaterial == null || !r.sharedMaterial.HasProperty(BaseColorId)) continue;
                    float alpha = r.sharedMaterial.GetColor(BaseColorId).a;
                    r.GetPropertyBlock(_block);
                    _block.SetColor(BaseColorId, new Color(colour.r, colour.g, colour.b, alpha));
                    r.SetPropertyBlock(_block);
                }
            if (flashLight != null) flashLight.color = colour;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (role != Role.Exit || zone == null) return;
            Shifter who = PlayerTouch.Find(other, out _);
            if (who == null || !zone.Clear(who)) return;   // not a player, or not one the zone holds: nothing
            Play();
        }

        /// <summary>The gate's moment: a flash, the ripple, its sound. The zone calls it for the entrance (Entered); the
        /// exit calls it itself. Safe to call at any time.</summary>
        public void Play()
        {
            _flashUntil = Time.time + flashTime;
            _flashing = true;
            if (flashLight != null) flashLight.enabled = true;
            if (ripple != null) ripple.Play();
            UiAudio.Play(sound);
            onPlayed?.Invoke();
        }

        /// <summary>The flash dying away: the drawing brightened and washed toward the gate's colour, the light with it.
        /// Nothing is done while the gate is calm (the swirl turns in its material by itself).</summary>
        private void Update()
        {
            if (!_flashing) return;
            float k = Mathf.Clamp01((_flashUntil - Time.time) / flashTime);   // 1 at the moment, 0 when calm
            if (k <= 0f) { _flashing = false; Calm(); return; }
            Color wash = Color.Lerp(Color.white, colour, flashWash * k) * (1f + flashBoost * k);
            foreach (var (r, calm) in _slots)
            {
                if (r == null) continue;
                r.GetPropertyBlock(_block);
                _block.SetColor(BaseColorId, new Color(calm.r * wash.r, calm.g * wash.g, calm.b * wash.b, calm.a));
                r.SetPropertyBlock(_block);
            }
            if (flashLight != null) flashLight.intensity = lightIntensity * k;
        }

        /// <summary>Back to calm: the drawing's own colours, the light off.</summary>
        private void Calm()
        {
            foreach (var (r, _) in _slots) if (r != null) r.SetPropertyBlock(null);
            if (flashLight != null) { flashLight.intensity = 0f; flashLight.enabled = false; }
        }
    }
}
