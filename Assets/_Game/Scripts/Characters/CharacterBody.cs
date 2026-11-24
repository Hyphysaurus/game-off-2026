using System;
using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// A funny character's body (D-0183): the Funny Characters rig, dressed in a LOOK (one part a slot). Sits on the root
    /// of Prefabs/Characters/FunnyBody, which holds the pack's rig as a child (it works on the rig's own root too).
    ///
    /// It finds the rig's twelve slot renderers by NAME (Body, Hair, ..., Full_Body, directly under the object that has
    /// the rig's Animator) and puts each part's mesh and materials on its slot. Nothing is re-bound: every part of the pack
    /// is skinned to the same bones. On waking it dresses itself: in the look text when there is one, else in the look
    /// rolled from its seed, rules and colour (the same look on every PC). Two menus on the component (the ⋮ button):
    /// "Dress now" shows the look in the editor without Play (and keeps it with the scene), "Roll the next look" moves the
    /// seed on by one and dresses.
    ///
    /// Safe anywhere: with no parts list (Resources/CharacterCatalog) it warns once and leaves the body as it was saved; a
    /// missing slot object is warned about once and skipped. It does nothing each frame.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-200)]   // dressed before other parts' Awake read its height or bones
    public sealed class CharacterBody : MonoBehaviour
    {
        [Tooltip("The number the look is rolled from. The same seed (with the same rules and colour) gives the same look on every PC.")]
        [SerializeField] private int seed = 1;
        [Tooltip("How the look is rolled. Empty = the default rules (Resources/CharacterLookRules).")]
        [SerializeField] private CharacterLookRules rules;
        [Tooltip("A colour word (Blue, Orange, Green...) for the body, the top and the trousers. Empty = each by chance.")]
        [SerializeField] private string colour = "";
        [Tooltip("Wear exactly this look instead of rolling one: \"Slot=part;Slot=part\" (Dress now writes the look it wears to the Console, ready to paste here). Empty = roll it. \"Body=\" = nothing worn.")]
        [SerializeField] private string lookText = "";
        [Tooltip("The middle of the one box every slot is drawn within, in the hip bone's space. The camera skips a slot when this box is out of view, so it must hold any part in any standing pose.")]
        [SerializeField] private Vector3 cullCentre = new Vector3(0f, 0.8f, 0f);
        [Tooltip("The size of that box, in the hip bone's space (parts reach from the soles, 0.53 below the hips, to 2.1 above them; arms spread 1.82 wide).")]
        [SerializeField] private Vector3 cullSize = new Vector3(2.4f, 3.2f, 2.0f);
        [Tooltip("What is worn now. Written every time it dresses; change the look through the fields above, not here.")]
        [SerializeField, HideInInspector] private CharacterLook worn = new CharacterLook();

        // One warning a session for a missing parts list, however many bodies there are.
        private static bool _warnedNoCatalog;

        // Found on first use, never saved: after a script reload they are simply found again.
        [NonSerialized] private SkinnedMeshRenderer[] _slots;
        [NonSerialized] private Animator _animator;
        [NonSerialized] private bool _warnedSlots;
        [NonSerialized] private bool _warnedUnknown;
        [NonSerialized] private bool _warnedText;
        [NonSerialized] private CharacterLook _scratch;

        /// <summary>A copy of what is worn now (changing the copy changes nothing; use <see cref="Wear"/>).</summary>
        public CharacterLook Look => new CharacterLook(worn);

        /// <summary>What is worn now, as one line of text (see <see cref="CharacterLook"/>).</summary>
        public string LookText => worn != null ? worn.ToText() : "";

        /// <summary>The seed the look is rolled from.</summary>
        public int Seed => seed;

        /// <summary>The colour word the look is rolled with ("" = none).</summary>
        public string Colour => colour ?? "";

        /// <summary>The rules the look is rolled by (the default rules when none are set). Never null.</summary>
        public CharacterLookRules Rules => rules != null ? rules : CharacterLookRules.Default;

        /// <summary>The rig's Animator (on the rig's root; it keeps the pack's humanoid avatar). Null when there is no rig.</summary>
        public Animator Animator
        {
            get
            {
                if (_animator == null) _animator = GetComponentInChildren<Animator>(true);
                return _animator;
            }
        }

        /// <summary>The rig's root: the object with the Animator, whose children are the slots and the bones. This object
        /// when there is no Animator.</summary>
        public Transform Rig
        {
            get
            {
                Animator animator = Animator;
                return animator != null ? animator.transform : transform;
            }
        }

        /// <summary>The rig's bone for a human body bone (by the humanoid avatar, never by name), or null when the rig
        /// has no humanoid avatar or no such bone.</summary>
        public Transform Bone(HumanBodyBones bone)
        {
            if (bone < 0 || bone >= HumanBodyBones.LastBone) return null;
            Animator animator = Animator;
            return animator != null && animator.isHuman ? animator.GetBoneTransform(bone) : null;
        }

        /// <summary>How tall the character stands: from the soles to the top of the highest part worn (from the parts
        /// list's heights, standing, not the pose of the moment), times the rig's size in the world. With no parts list
        /// it is read from the meshes the slots show (the same numbers). 0 when nothing is worn.</summary>
        public float Height
        {
            get
            {
                float top = 0f;
                CharacterCatalog catalog = CharacterCatalog.Load();
                if (catalog != null && catalog.Ready)
                {
                    for (int i = 0; i < CharacterSlots.Count; i++)
                    {
                        CharacterCatalog.Part part = catalog.PartById(worn != null ? worn.Get((CharacterSlot)i) : "");
                        if (part != null && part.mesh != null) top = Mathf.Max(top, part.top);
                    }
                }
                else if (FindSlots())
                {
                    for (int i = 0; i < _slots.Length; i++)
                    {
                        SkinnedMeshRenderer smr = _slots[i];
                        if (smr != null && smr.enabled && smr.sharedMesh != null) top = Mathf.Max(top, smr.sharedMesh.bounds.max.y);
                    }
                }
                return top * Rig.lossyScale.y;
            }
        }

        private void Awake()
        {
            Dress();
        }

        /// <summary>Dresses the body by its own fields: the look text when there is one (a text that cannot be read is
        /// warned about once and the roll is used), else the look rolled from the seed, rules and colour. False when
        /// nothing could be put on (no parts list, or no slots).</summary>
        public bool Dress()
        {
            if (!string.IsNullOrWhiteSpace(lookText))
            {
                if (CharacterLook.TryParse(lookText, out CharacterLook written)) return Wear(written);
                if (!_warnedText)
                {
                    _warnedText = true;
                    Debug.LogWarning("[Characters] " + name + ": its look text could not be read (want \"Slot=part;Slot=part\"); it wears its rolled look instead. Text: " + lookText, this);
                }
            }
            return WearRoll();
        }

        /// <summary>Puts a look on: each slot's part mesh and materials on that slot's renderer, the renderer on; a slot
        /// with nothing worn gets no mesh and its renderer off (null = nothing worn anywhere). A part id the parts list
        /// does not know (an older save) is skipped with one warning: that slot keeps what it had. Every slot gets the
        /// one culling box. Can be called any time, as often as wanted. False when nothing could be put on (no parts
        /// list, or no slots: warned once, the body is left as it was).</summary>
        public bool Wear(CharacterLook look)
        {
            CharacterCatalog catalog = Catalog();
            if (catalog == null || !FindSlots()) return false;
            BeginEdit();
            Apply(catalog, look);
            EndEdit();
            return true;
        }

        /// <summary>Rolls a look and wears it, and keeps how it was rolled (seed, rules, colour; the look text is cleared
        /// so the roll is what it wears from now on). <paramref name="rules"/> null = the default rules. False as for
        /// <see cref="Wear"/>.</summary>
        public bool WearRolled(int seed, CharacterLookRules rules, string colour)
        {
            FindSlots();
            BeginEdit();
            this.seed = seed;
            this.rules = rules;
            this.colour = colour != null ? colour.Trim() : "";
            lookText = "";
            EndEdit();
            return WearRoll();
        }

        /// <summary>Rolls the look from the fields into the reused scratch look and wears it.</summary>
        private bool WearRoll()
        {
            CharacterCatalog catalog = Catalog();
            if (catalog == null || !FindSlots()) return false;
            if (_scratch == null) _scratch = new CharacterLook();
            CharacterLook.Roll(catalog, Rules, seed, colour, _scratch);
            BeginEdit();
            Apply(catalog, _scratch);
            EndEdit();
            return true;
        }

        /// <summary>The parts list when it can dress a body; null (warned once a session) when it is missing or has no
        /// meshes (a copy of the game without the pack).</summary>
        private CharacterCatalog Catalog()
        {
            CharacterCatalog catalog = CharacterCatalog.Load();
            if (catalog != null && catalog.Ready) return catalog;
            if (!_warnedNoCatalog)
            {
                _warnedNoCatalog = true;
                Debug.LogWarning("[Characters] " + (catalog == null ? "No parts list (Resources/CharacterCatalog)" : "The parts list has no meshes (is the Funny Characters pack in the project?)")
                    + ": funny characters keep the look they were saved in. Run Shift Jam > Characters > Build Characters.", this);
            }
            return null;
        }

        /// <summary>Finds the twelve slot renderers by name under the rig's root, once. Missing ones are warned about once
        /// (by name) and stay empty. False when not one is found.</summary>
        private bool FindSlots()
        {
            if (_slots != null && _slots.Length == CharacterSlots.Count) return HasAny();
            _slots = new SkinnedMeshRenderer[CharacterSlots.Count];
            Transform rig = Rig;
            string missing = "";
            for (int i = 0; i < _slots.Length; i++)
            {
                string objectName = CharacterSlots.ObjectName((CharacterSlot)i);
                Transform slot = rig.Find(objectName);
                if (slot != null && slot.TryGetComponent(out SkinnedMeshRenderer smr)) _slots[i] = smr;
                else missing += (missing.Length > 0 ? ", " : "") + objectName;
            }
            if (missing.Length > 0 && !_warnedSlots)
            {
                _warnedSlots = true;
                Debug.LogWarning("[Characters] " + name + ": no slot renderer named " + missing + " directly under " + rig.name + "; those slots are skipped.", this);
            }
            return HasAny();
        }

        private bool HasAny()
        {
            for (int i = 0; i < _slots.Length; i++) if (_slots[i] != null) return true;
            return false;
        }

        /// <summary>The look onto the found slots (see <see cref="Wear"/>), and <see cref="worn"/> brought up to date.</summary>
        private void Apply(CharacterCatalog catalog, CharacterLook look)
        {
            if (worn == null) worn = new CharacterLook();
            var box = new Bounds(cullCentre, cullSize);
            int unknown = 0;
            string firstUnknown = null;
            for (int i = 0; i < CharacterSlots.Count; i++)
            {
                var slot = (CharacterSlot)i;
                SkinnedMeshRenderer smr = _slots[i];
                if (smr == null)
                {
                    worn.Set(slot, "");   // no slot object: nothing is drawn there
                    continue;
                }
                string id = look != null ? look.Get(slot) : "";
                if (id.Length == 0)
                {
                    if (smr.sharedMesh != null) smr.sharedMesh = null;
                    smr.enabled = false;
                    worn.Set(slot, "");
                }
                else
                {
                    CharacterCatalog.Part part = catalog.PartById(id);
                    if (part == null || part.mesh == null || part.slot != slot)
                    {
                        unknown++;   // skipped: the slot keeps what it had
                        if (firstUnknown == null) firstUnknown = id;
                    }
                    else
                    {
                        if (smr.sharedMesh != part.mesh) smr.sharedMesh = part.mesh;
                        if (part.materials != null && part.materials.Length > 0) smr.sharedMaterials = part.materials;
                        smr.enabled = true;
                        worn.Set(slot, id);
                    }
                }
                smr.localBounds = box;
            }
            if (unknown > 0 && !_warnedUnknown)
            {
                _warnedUnknown = true;
                Debug.LogWarning("[Characters] " + name + ": " + unknown + " part(s) of its look are not in the parts list (first: " + firstUnknown + ", or on the wrong slot); those slots keep what they had.", this);
            }
        }

        [ContextMenu("Dress now")]
        private void DressNow()
        {
            if (Dress()) Debug.Log("[Characters] " + name + " wears: " + LookText, this);
        }

        [ContextMenu("Roll the next look")]
        private void RollNext()
        {
            if (!string.IsNullOrWhiteSpace(lookText))
            {
                Debug.Log("[Characters] " + name + " wears its look text, so the seed does not change what it wears. Clear Look Text to roll.", this);
                return;
            }
            FindSlots();
            BeginEdit();
            unchecked { seed++; }
            EndEdit();
            if (Dress()) Debug.Log("[Characters] " + name + " seed " + seed + " wears: " + LookText, this);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { _warnedNoCatalog = false; }

        // ---------- the editor outside Play: changes are undoable and kept with the scene or prefab ----------

        /// <summary>In the editor outside Play: records this part and the slot renderers before they change (undo), unless
        /// they sit in a prefab's own editing scene. Nothing in Play or in a built game.</summary>
        private void BeginEdit()
        {
#if UNITY_EDITOR
            if (Application.isPlaying || UnityEditor.SceneManagement.EditorSceneManager.IsPreviewSceneObject(this)) return;
            UnityEditor.Undo.RecordObjects(Changed(), "Dress " + name);
#endif
        }

        /// <summary>In the editor outside Play: marks what changed, so a scene (or a prefab) keeps the look when saved,
        /// as overrides when this is a prefab copy. Nothing in Play or in a built game.</summary>
        private void EndEdit()
        {
#if UNITY_EDITOR
            if (Application.isPlaying) return;
            foreach (UnityEngine.Object changed in Changed())
            {
                if (UnityEditor.PrefabUtility.IsPartOfPrefabInstance(changed)) UnityEditor.PrefabUtility.RecordPrefabInstancePropertyModifications(changed);
                UnityEditor.EditorUtility.SetDirty(changed);
            }
            if (gameObject.scene.IsValid() && !UnityEditor.SceneManagement.EditorSceneManager.IsPreviewScene(gameObject.scene))
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
#endif
        }

#if UNITY_EDITOR
        /// <summary>This part and every found slot renderer.</summary>
        private UnityEngine.Object[] Changed()
        {
            var list = new System.Collections.Generic.List<UnityEngine.Object> { this };
            if (_slots != null) foreach (SkinnedMeshRenderer smr in _slots) if (smr != null) list.Add(smr);
            return list.ToArray();
        }
#endif
    }
}
