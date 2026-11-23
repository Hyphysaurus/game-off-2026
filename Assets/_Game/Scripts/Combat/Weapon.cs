using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// Something a WeaponHolder carries and fires with Attack. The weapon's root is the grip: the holder puts it in the right
    /// hand (third person) or in front of the camera (first person), and the weapon adds its own motion on top (a swing, a
    /// recoil kick) through PoseRotation and PosePosition. Its barrel runs along its own z, its top is its y.
    /// Sizes are authored for a holder at scale 1 (the toy); the holder scales it.
    /// When dropped it becomes a loose physics prop that can be taken again with E.
    /// </summary>
    public abstract class Weapon : MonoBehaviour
    {
        [SerializeField] private string displayName = "Weapon";
        [SerializeField, Tooltip("The keys, on the toast the first time it is taken (D-0162). <b>Key</b> shows as a key cap. Empty: a blaster says fire / aim / reload, anything else only its name.")] private string howTo;
        [Header("Hold")]
        [SerializeField, Tooltip("Extra turn of the weapon in the hand, on top of the hold the hand itself gives (a blaster) or of pointing along the arm (melee).")] private Vector3 handEuler;
        [SerializeField, Tooltip("Slide from the palm, in the weapon's own axes, units at scale 1.")] private Vector3 handOffset;
        [SerializeField, Tooltip("First person: where the grip sits in camera space, units at scale 1.")] private Vector3 viewOffset = new Vector3(0.22f, -0.22f, 0.45f);
        [SerializeField, Tooltip("First person: extra turn in camera space.")] private Vector3 viewEuler;
        [SerializeField, Tooltip("On: a blaster. It sits fixed in the hand, and the arm points it at the crosshair while aiming or just after a shot. Off: melee, which follows the arm on the body's heading.")] private bool aimsWithView = true;
        [SerializeField, Tooltip("On: a long gun. Both hands stay on it, it is carried low across the body and comes up to the shoulder to aim (third person).")] private bool twoHanded;
        [SerializeField, Tooltip("A long gun: where the support hand holds, in the weapon's own axes (z along the barrel), units at scale 1. Zero: the model's under-barrel marker (Place_..._BarBottom) if it has one, else a spot ahead of the grip.")] private Vector3 supportPoint;
        [SerializeField, Tooltip("A long gun: degrees the support palm sits round the barrel towards the support side: 0 flat underneath, 90 flat against the gun's side. A fat fore-end, or a gun aimed rolled over, is held more from the side. Below 0 = the shared number (Resources/WeaponHold > Palm Roll Degrees).")] private float supportRoll = -1f;
        [SerializeField, Tooltip("A long gun: degrees each bone of the support hand's four fingers bends round THIS gun's fore-end. A fat fore-end wants less, or the fingers sink into it. 0 = the shared number (WeaponHold > Support Curl).")] private float supportCurl;
        [SerializeField, Tooltip("A blaster: degrees each bone of the gun hand's fingers bends round THIS gun's grip: x the three gripping fingers, y the trigger finger, z the thumb. A thick grip wants less, or the fingers sink into it. 0 in a part = the shared number (WeaponHold > The fingers).")] private Vector3 gripCurl;
        [SerializeField, Tooltip("First person: the middle of this gun's sight, in the weapon's own axes (z along the barrel), units at scale 1. Aim brings this point to the middle of the view. Zero: found from the model (the glass of the sight on its Place_..._Sight mount; a sight without glass: its mount; no mount: the line of the barrel).")] private Vector3 sightPoint;
        [SerializeField, Tooltip("First person: degrees the gun rolls over to its left as the sight comes to the eye. For a sight that stands at a slant on the gun's right side (an offset sight, made to be used with the gun rolled over): the slant, so the sight stands upright at the eye. 0 = a sight on top.")] private float sightRoll;
        [SerializeField, Tooltip("Field of view while this weapon is aimed in first person, degrees (a scope zooms further). 0 = the first-person settings' aim field of view.")] private float sightFieldOfView;
        [SerializeField, Tooltip("First person: how far in front of the eye this gun's sight sits while aimed, units at scale 1. Further out, the gun is held out more and the hand holding it comes up into view (a pistol at arm's length). 0 = the shared number (Resources/WeaponHold > Sight Distance, the test room's 0.307).")] private float sightDistance;
        [SerializeField, Tooltip("The reticle this gun's sight shows: a material on the Sight Overlay shader (Data/Sights; its colour and size are set on it). Empty: the sight's own kind decides (Resources/SightLook).")] private Material reticle;
        [SerializeField, Tooltip("Its picture in the inventory bar (a thumbnail from Art/Thumbs).")] private Texture2D icon;
        [SerializeField, Tooltip("Melee: worn on the hand instead of gripped (a foam finger). The hand is folded away inside it and the fingers do not close.")] private bool worn;
        [SerializeField, Range(0.5f, 1f), Tooltip("How fast you move with this weapon out, as a share of normal. A heavy gun is a little slower (the test room: rifle 0.92, shotgun 0.9, sniper 0.82). 1 = no slower.")] private float moveSpeed = 1f;

        public string DisplayName => displayName;
        /// <summary>How to use it, for the first-pickup toast.</summary>
        public virtual string HowTo => howTo;
        public WeaponHolder Holder { get; private set; }
        public bool AimsWithView => aimsWithView;
        /// <summary>True for a long gun: held in both hands, carried across the body, raised to the shoulder to aim.</summary>
        public bool TwoHanded => twoHanded;
        /// <summary>This weapon's own field of view while aimed in first person; 0 = none of its own.</summary>
        public float SightFieldOfView => sightFieldOfView;
        /// <summary>This gun's own reticle (a material on the Sight Overlay shader), or null: the sight's own kind decides (SightLook).</summary>
        public Material Reticle => reticle;
        /// <summary>Degrees the gun rolls over as its sight comes to the eye in first person (an offset sight); 0 = none.</summary>
        public float SightRoll => sightRoll;
        /// <summary>First person: how far in front of the eye the sight sits while aimed: this gun's own, else the shared one.</summary>
        public float SightDistance(WeaponHoldSettings shared) => sightDistance > 0f ? sightDistance : shared.sightDistance;
        /// <summary>How fast the holder moves with this weapon out, as a share of normal (1 = no slower).</summary>
        public float MoveSpeed => moveSpeed;
        public Quaternion HandRotation => Quaternion.Euler(handEuler);
        public Vector3 HandOffset => handOffset;
        /// <summary>How far round the barrel the support palm sits on this gun, degrees: its own number, else the shared one.</summary>
        public float SupportRoll(WeaponHoldSettings shared) => supportRoll >= 0f ? supportRoll : shared.palmRollDegrees;
        /// <summary>Degrees each bone of the support hand's fingers bends round this gun: its own number, else the shared one.</summary>
        public float SupportCurl(WeaponHoldSettings shared) => supportCurl > 0f ? supportCurl : shared.supportCurl;
        /// <summary>Degrees each bone of the gun hand's fingers bends round this grip (x the three gripping fingers, y the
        /// trigger finger, z the thumb): its own number for each, else the shared one.</summary>
        public Vector3 GripCurl(WeaponHoldSettings shared) => new Vector3(gripCurl.x > 0f ? gripCurl.x : shared.fingerCurl, gripCurl.y > 0f ? gripCurl.y : shared.triggerCurl, gripCurl.z > 0f ? gripCurl.z : shared.thumbCurl);
        /// <summary>Melee: worn on the hand (a foam finger), the hand folded away inside it.</summary>
        public bool Worn => worn;
        /// <summary>Its picture in the inventory bar, if it has one.</summary>
        public Texture2D Icon => icon;
        /// <summary>Only a toy can use it (the Dart Line: at kid size the heights are a step). The inventory greys it for a kid.</summary>
        public virtual bool ToyOnly => false;
        /// <summary>A handle (melee): degrees each finger bone bends round it, this weapon's own number for each part
        /// (its Grip Curl), else the shared handle numbers.</summary>
        public Vector3 HandleCurl(WeaponHoldSettings shared) => new Vector3(gripCurl.x > 0f ? gripCurl.x : shared.handleCurl.x, gripCurl.y > 0f ? gripCurl.y : shared.handleCurl.y, gripCurl.z > 0f ? gripCurl.z : shared.handleCurl.z);
        public Vector3 ViewOffset => viewOffset;
        public Quaternion ViewRotation => Quaternion.Euler(viewEuler);
        /// <summary>The weapon's own motion this frame (swing, recoil), applied in the hold frame.</summary>
        public virtual Quaternion PoseRotation => Quaternion.identity;
        /// <summary>The weapon's own slide this frame, hold-frame units at scale 1.</summary>
        public virtual Vector3 PosePosition => Vector3.zero;
        /// <summary>True while the weapon is mid-action and should not be swapped out.</summary>
        public virtual bool Busy => false;
        /// <summary>From the weapon's root to the end of its barrel, in the weapon's own axes, world units.</summary>
        public virtual Vector3 MuzzleOffset => Vector3.zero;

        /// <summary>Where this weapon is aimed: the point in the world the holder's crosshair lands on.</summary>
        public virtual Vector3 AimPoint() => Holder != null ? Holder.AimOrigin + Holder.AimForward * (100f * Scale) : transform.position + transform.forward;

        /// <summary>Attack was pressed.</summary>
        public abstract void TriggerPressed();
        /// <summary>Attack is held this frame (automatic fire).</summary>
        public virtual void TriggerHeld() { }
        /// <summary>Put away or dropped mid-action: stop cleanly.</summary>
        public virtual void Cancel() { }

        /// <summary>Every weapon, as it wakes: its sight's lens and reticle take the game's look (SightLook, D-0166).
        /// A weapon with an Awake of its own calls this one first (base.Awake()).</summary>
        protected virtual void Awake()
        {
            SightLook look = SightLook.Load();
            if (look != null) look.Apply(transform, reticle);
        }

        private bool _fitted; private float _stock; private Vector3 _support;
        private bool _sighted, _sightOnMount; private Vector3 _sight;

        /// <summary>The middle of this gun's sight, in the weapon's own (root) space: the point aim brings to the middle
        /// of the first-person view. <paramref name="onMount"/> says it is only where a sight is mounted (or the line
        /// of the barrel), so the sight's middle is a little above it. Found once: the prefab's own point if it has
        /// one, else from the model.</summary>
        public void SightFit(out Vector3 point, out bool onMount)
        {
            if (!_sighted)
            {
                _sighted = true;
                _sight = sightPoint;
                if (_sight == Vector3.zero && !GunSight.Find(transform, out _sight, out _sightOnMount))
                {
                    // No sight on the model: you look along the top of the barrel.
                    _sight = new Vector3(0f, MuzzleOffset.y / Mathf.Max(1e-5f, transform.lossyScale.y), 0f);
                    _sightOnMount = true;
                }
            }
            point = _sight;
            onMount = _sightOnMount;
        }

        /// <summary>A long gun measured for the hold, in the weapon's own (root) space: how far it runs back from the
        /// grip to the end of its stock, and where the support hand holds, never further forward than
        /// <paramref name="longestReach"/>. Measured once from the model.</summary>
        public void LongGunFit(float longestReach, out float stock, out Vector3 support)
        {
            if (!_fitted)
            {
                _fitted = true;
                // The stock: the furthest any part of the model reaches behind the root.
                float back = 0f;
                Matrix4x4 toRoot = transform.worldToLocalMatrix;
                foreach (MeshFilter part in GetComponentsInChildren<MeshFilter>(true))
                {
                    if (part.sharedMesh == null) continue;
                    Bounds b = part.sharedMesh.bounds;
                    Matrix4x4 m = toRoot * part.transform.localToWorldMatrix;
                    for (int corner = 0; corner < 8; corner++)
                    {
                        var p = new Vector3((corner & 1) == 0 ? b.min.x : b.max.x, (corner & 2) == 0 ? b.min.y : b.max.y, (corner & 4) == 0 ? b.min.z : b.max.z);
                        back = Mathf.Min(back, m.MultiplyPoint3x4(p).z);
                    }
                }
                _stock = Mathf.Max(-back, 0.1f);
                // The support hand: where the prefab says, else the pack's marker under the barrel, else ahead of the grip.
                _support = supportPoint;
                if (_support == Vector3.zero)
                {
                    // Ahead of the model's own origin (the builder hangs the pack's model under "Model"): the grip, which
                    // is this weapon's root, sits below that origin.
                    Transform model = transform.Find("Model");
                    Vector3 origin = model != null && model.childCount > 0 ? toRoot.MultiplyPoint3x4(model.GetChild(0).position) : Vector3.zero;
                    _support = origin + new Vector3(0f, 0.06f, 0.24f);
                    foreach (Transform child in GetComponentsInChildren<Transform>(true))
                        if (child.name.StartsWith("Place_") && child.name.EndsWith("_BarBottom")) { _support = toRoot.MultiplyPoint3x4(child.position); break; }
                }
            }
            stock = _stock;
            support = _support;
            support.z = Mathf.Min(support.z, longestReach);
        }

        internal void SetHolder(WeaponHolder holder)
        {
            if (Holder == holder) return;
            if (holder == null) Cancel();
            Holder = holder;
        }

        protected float Scale => Holder != null ? Holder.Scale : Mathf.Max(0.001f, transform.lossyScale.y);
    }
}
