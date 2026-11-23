using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ShiftJam
{
    /// <summary>
    /// The look on ONE gun (D-0167, the armory looks bench): which skin its colour parts are drawn with, and which
    /// part of each kind hangs on it. It works on any object that holds a pack gun model, with or without a Weapon:
    /// the gun in a hand, and the copy that turns above the bench.
    /// It is never on a prefab. It is added the first time a look is put on a gun, so a gun nobody changed stays
    /// exactly as it came (no component, no material written). It has no fields to set: everything comes from the
    /// parts list (ArmoryCatalog) and from the saved look (ArmoryLooks).
    /// It is switched off almost always. It only runs (LateUpdate) while a skin waits for somebody else to give the
    /// gun's materials back: the body fade (camera inside the body) and the size shift's bend both swap a renderer's
    /// materials for a moment and put the old ones back after, which would undo a skin written meanwhile.
    /// This is the base working parts and full builds grow from: the rows, the choices and the saved text are
    /// already per kind of part; a part's Fit and Stats numbers are in the list and are not read yet.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WeaponBuild : MonoBehaviour
    {
        // Scratch lists, emptied after every use: putting a look on allocates nothing that lasts.
        private static readonly Dictionary<string, string> Pairs = new Dictionary<string, string>();
        private static readonly List<Renderer> Parts = new List<Renderer>();
        private static readonly List<Material> Looks = new List<Material>();
        private static readonly List<Transform> Nodes = new List<Transform>();

        private ArmoryCatalog _catalog;
        private ArmoryCatalog.Gun _gun;
        private Weapon _weapon;              // the weapon on this object, if it is one (the bench's copy is not)
        private string _skin = "";           // the chosen skin's id; "" = the gun's own
        private bool _skinned;               // this component has written a skin at least once
        private bool _measured;              // the weapon measured itself before anything was hung on it
        private bool _pending;               // a skin waits for the gun's materials to be given back
        private BodyFade _fade;              // the body fade a waiting skin was held back by: it holds this gun's materials until that body is shown
        private readonly List<ArmoryCatalog.PartType> _types = new List<ArmoryCatalog.PartType>();   // the kinds this gun has parts of, in the list's order
        private readonly Dictionary<string, List<ArmoryCatalog.Part>> _options = new Dictionary<string, List<ArmoryCatalog.Part>>();   // kind -> its parts, in the gun's order
        private readonly Dictionary<string, string> _chosen = new Dictionary<string, string>();      // kind -> the part fitted
        private readonly Dictionary<string, List<GameObject>> _hung = new Dictionary<string, List<GameObject>>();   // kind -> what this component hung on the gun

        /// <summary>The parts list's row for this gun.</summary>
        public ArmoryCatalog.Gun Gun => _gun;

        /// <summary>What is chosen now, as the text that is saved ("" = as the gun came).</summary>
        public string Look
        {
            get
            {
                string look = ArmoryLooks.With("", ArmoryLooks.SkinKey, _skin);
                for (int i = 0; i < _types.Count; i++) look = ArmoryLooks.With(look, _types[i].id, Chosen(_types[i].id));
                return look;
            }
        }

        /// <summary>How many rows the bench shows for this gun: the skin, then one for each kind of part it can take.</summary>
        public int RowCount => _gun == null ? 0 : 1 + _types.Count;

        /// <summary>The WeaponBuild on <paramref name="gun"/>: the one it has, or a new one when the parts list knows
        /// the model under it. Null when there is no list or the list does not know this gun (a melee weapon).</summary>
        public static WeaponBuild For(Transform gun)
        {
            if (gun == null) return null;
            WeaponBuild build = gun.GetComponent<WeaponBuild>();
            if (build != null && build._gun != null) return build;
            ArmoryCatalog catalog = ArmoryCatalog.Load();
            if (catalog == null) return null;
            ArmoryCatalog.Gun entry = catalog.GunOf(gun);
            if (entry == null) return null;
            if (build == null) build = gun.gameObject.AddComponent<WeaponBuild>();
            build.enabled = false;   // at once: the gun may be switched off, so nothing is left to a later wake-up
            build.Setup(catalog, entry);
            return build;
        }

        /// <summary>Puts the saved look of this gun's type on it. Returns the WeaponBuild, or null when nothing was
        /// done. WHY nothing is done for a gun with nothing saved and no WeaponBuild yet: a player who never used a
        /// bench must get the gun exactly as it comes, with no component added and no material written (D-0167).</summary>
        public static WeaponBuild PutSavedLookOn(Weapon weapon)
        {
            if (weapon == null) return null;
            WeaponBuild build = weapon.GetComponent<WeaponBuild>();
            string look;
            if (build == null || build._gun == null)
            {
                ArmoryCatalog catalog = ArmoryCatalog.Load();
                if (catalog == null) return null;
                ArmoryCatalog.Gun entry = catalog.GunOf(weapon.transform);
                if (entry == null) return null;
                look = ArmoryLooks.Get(entry.id);
                if (string.IsNullOrEmpty(look)) return null;
                build = For(weapon.transform);
                if (build == null) return null;
            }
            else look = ArmoryLooks.Get(build._gun.id);   // it may be empty now: the look was put back to "as it came"
            build.Apply(look);
            return build;
        }

        /// <summary>Puts a look (the saved text) on this gun: the parts first, then the skin over everything, the
        /// order the pack uses, so a part that carries the gun's colour is skinned too. A part or skin the text names
        /// that this gun cannot take is read as "as the gun came".</summary>
        public void Apply(string look)
        {
            if (_gun == null) return;
            ArmoryLooks.Read(look, Pairs);
            for (int i = 0; i < _types.Count; i++)
            {
                ArmoryCatalog.PartType type = _types[i];
                string wanted = "";
                if (Pairs.TryGetValue(type.id, out string part) && OptionIndex(type.id, part) >= 0) wanted = part;
                if (wanted != Chosen(type.id)) Fit(type, wanted);
            }
            string skin = "";
            if (Pairs.TryGetValue(ArmoryLooks.SkinKey, out string named) && named != _gun.skin && _catalog.SkinById(named) != null) skin = named;
            _skin = skin;
            Pairs.Clear();
            PutSkinOn();
        }

        /// <summary>The name of a bench row: row 0 is the skin, the others are the kinds of part. "" out of range.</summary>
        public string RowLabel(int row)
        {
            if (_gun == null || row < 0 || row > _types.Count) return "";
            return row == 0 ? _catalog.skinLabel : _types[row - 1].label;
        }

        /// <summary>What a bench row shows now: the name of the skin or part, and a small "3/10" after it so the
        /// player sees how many there are. "" out of range.</summary>
        public string RowValue(int row)
        {
            if (_gun == null || row < 0 || row > _types.Count) return "";
            if (row == 0)
            {
                int at = _catalog.SkinIndex(SkinInEffect);
                if (at < 0) return _catalog.noneLabel;
                return Named(_catalog.skins[at].label, _catalog.skins[at].id) + Counter(at + 1, _catalog.skins.Count);
            }
            ArmoryCatalog.PartType type = _types[row - 1];
            List<ArmoryCatalog.Part> options = _options[type.id];
            int chosen = OptionIndex(type.id, Chosen(type.id));
            if (chosen < 0) return _catalog.noneLabel;
            return Named(options[chosen].label, options[chosen].id) + Counter(chosen + 1, options.Count);
        }

        /// <summary>Moves a bench row one step (<paramref name="direction"/> below 0: back), going round at the ends.
        /// A part row goes through "nothing fitted" too. Out of range: nothing happens.</summary>
        public void Step(int row, int direction)
        {
            if (_gun == null || row < 0 || row > _types.Count) return;
            int step = direction < 0 ? -1 : 1;
            if (row == 0)
            {
                int count = _catalog.skins.Count;
                if (count == 0) return;
                int at = _catalog.SkinIndex(SkinInEffect);
                string id = "";
                // A row with no id cannot be chosen (nothing could save it): pass over it, at most once round.
                for (int tries = 0; tries < count && id.Length == 0; tries++)
                {
                    at = at < 0 ? (step > 0 ? 0 : count - 1) : (at + step + count) % count;
                    ArmoryCatalog.Skin next = _catalog.skins[at];
                    if (next != null && !string.IsNullOrEmpty(next.id)) id = next.id;
                }
                if (id.Length == 0) return;
                // The gun's own skin is stored as nothing, so going back to it clears the saved choice.
                _skin = id == _gun.skin ? "" : id;
                PutSkinOn();
                return;
            }
            ArmoryCatalog.PartType type = _types[row - 1];
            List<ArmoryCatalog.Part> options = _options[type.id];
            int places = options.Count + 1;   // place 0 is "nothing fitted"
            int place = (OptionIndex(type.id, Chosen(type.id)) + 1 + step + places) % places;
            Fit(type, place == 0 ? "" : options[place - 1].id);
            PutSkinOn();   // after the part, so a part that carries the gun's colour takes the skin
        }

        /// <summary>Reads what this gun may take out of the parts list, once.</summary>
        private void Setup(ArmoryCatalog catalog, ArmoryCatalog.Gun entry)
        {
            _catalog = catalog;
            _gun = entry;
            _weapon = GetComponent<Weapon>();
            _types.Clear(); _options.Clear(); _chosen.Clear(); _hung.Clear();
            if (catalog.types == null || entry.parts == null) return;
            for (int t = 0; t < catalog.types.Count; t++)
            {
                ArmoryCatalog.PartType type = catalog.types[t];
                if (type == null || string.IsNullOrEmpty(type.id) || _options.ContainsKey(type.id)) continue;
                List<ArmoryCatalog.Part> options = null;
                for (int p = 0; p < entry.parts.Length; p++)
                {
                    ArmoryCatalog.Part part = catalog.PartById(entry.parts[p]);
                    if (part == null || part.type != type.id) continue;
                    if (options == null) options = new List<ArmoryCatalog.Part>();
                    if (!options.Contains(part)) options.Add(part);
                }
                if (options == null) continue;   // this gun has no part of this kind: no row for it
                _types.Add(type);
                _options.Add(type.id, options);
                _hung.Add(type.id, new List<GameObject>());
            }
        }

        private string SkinInEffect => _skin.Length > 0 ? _skin : _gun.skin;

        private string Chosen(string typeId) => _chosen.TryGetValue(typeId, out string part) ? part : "";

        /// <summary>Where a part is among this gun's parts of that kind; -1 when it is not one of them (or empty).</summary>
        private int OptionIndex(string typeId, string partId)
        {
            if (string.IsNullOrEmpty(partId)) return -1;
            List<ArmoryCatalog.Part> options = _options[typeId];
            for (int i = 0; i < options.Count; i++) if (options[i].id == partId) return i;
            return -1;
        }

        private static string Named(string label, string id) => string.IsNullOrEmpty(label) ? id : label;

        private static string Counter(int place, int count) => "   <size=70%><alpha=#88>" + place + "/" + count;

        /// <summary>Takes off what this component hung for one kind of part and hangs the new part (none when
        /// <paramref name="partId"/> is empty). Only ever removes what it made itself: what the pack built into the
        /// gun (its sight, its magazine) is never touched.</summary>
        private void Fit(ArmoryCatalog.PartType type, string partId)
        {
            // Measure first, once. The weapon works out how far its stock runs back by walking every mesh under it and
            // remembers the answer (Weapon.LongGunFit). Asked before anything is hung, a part can never change the
            // length the hold was fitted to (D-0167: looks only).
            if (!_measured)
            {
                _measured = true;
                if (_weapon != null) _weapon.LongGunFit(float.PositiveInfinity, out _, out _);
            }

            List<GameObject> hung = _hung[type.id];
            for (int i = 0; i < hung.Count; i++)
            {
                GameObject old = hung[i];
                if (old == null) continue;
                // Destroy waits for the end of the frame. Taken out of the gun at once, nothing that walks the gun later
                // this frame (the skin, the search for a mount, a measure) meets a part that is already on its way out.
                old.SetActive(false);
                old.transform.SetParent(null, false);
                Destroy(old);
            }
            hung.Clear();

            if (string.IsNullOrEmpty(partId)) { _chosen.Remove(type.id); return; }
            _chosen[type.id] = partId;
            ArmoryCatalog.Part part = _catalog.PartById(partId);
            if (part == null || part.pieces == null) return;
            // A gun in a first-person view casts no shadow (a floating gun's shadow looks wrong). The holder sets that
            // only when a gun is taken, stowed, dropped or the view changes, so a part hung in between is set here.
            // Not copied from another renderer: the body fade may have that one at "shadows only" for the moment.
            bool inView = _weapon != null && _weapon.Holder != null && _weapon.Holder.FirstPerson;
            for (int i = 0; i < part.pieces.Length; i++)
            {
                ArmoryCatalog.Piece piece = part.pieces[i];
                if (piece == null || piece.prefab == null) continue;
                Transform mount = FindMount(piece.slot);
                if (mount == null) continue;
                // Made under its parent, so it keeps the pose the prefab was authored with, as the pack does it.
                GameObject made;
                if (type.hook != null)
                {
                    made = Instantiate(type.hook, mount);
                    hung.Add(made);   // the piece under it goes when the hook goes
                    Instantiate(piece.prefab, ChildNamed(made.transform, type.hookMount));
                }
                else
                {
                    made = Instantiate(piece.prefab, mount);
                    hung.Add(made);
                }
                Dress(made, mount, inView);
            }
        }

        /// <summary>The mount for a slot: the first child (switched off or not) whose name starts with the list's
        /// mount prefix and ends with the slot's name. The pack's own rule; matching the two ends is what finds a
        /// mount whose middle is misspelt. A mount inside something this component hung is not the gun's.</summary>
        private Transform FindMount(string slot)
        {
            if (string.IsNullOrEmpty(slot)) return null;
            string prefix = _catalog.mountPrefix != null ? _catalog.mountPrefix : "";
            Transform found = null;
            GetComponentsInChildren(true, Nodes);
            for (int i = 0; i < Nodes.Count; i++)
            {
                Transform node = Nodes[i];
                string name = node.name;
                if (!name.StartsWith(prefix, StringComparison.Ordinal) || !name.EndsWith(slot, StringComparison.Ordinal)) continue;
                if (UnderHung(node)) continue;
                found = node;
                break;
            }
            Nodes.Clear();
            return found;
        }

        private bool UnderHung(Transform node)
        {
            foreach (List<GameObject> hung in _hung.Values)
                for (int i = 0; i < hung.Count; i++)
                    if (hung[i] != null && node.IsChildOf(hung[i].transform)) return true;
            return false;
        }

        /// <summary>The child of <paramref name="root"/> with this name (switched off or not), or the root itself.</summary>
        private static Transform ChildNamed(Transform root, string name)
        {
            Transform found = root;
            if (string.IsNullOrEmpty(name)) return found;
            root.GetComponentsInChildren(true, Nodes);
            for (int i = 0; i < Nodes.Count; i++)
                if (Nodes[i].name == name) { found = Nodes[i]; break; }
            Nodes.Clear();
            return found;
        }

        /// <summary>A part just hung is drawn like the gun it hangs on: the mount's layer (a first-person camera may
        /// draw the gun's layer on its own), and the gun's shadow rule.</summary>
        private static void Dress(GameObject made, Transform mount, bool inView)
        {
            int layer = mount.gameObject.layer;
            made.GetComponentsInChildren(true, Nodes);
            for (int i = 0; i < Nodes.Count; i++) Nodes[i].gameObject.layer = layer;
            Nodes.Clear();
            made.GetComponentsInChildren(true, Parts);
            for (int i = 0; i < Parts.Count; i++) Parts[i].shadowCastingMode = inView ? ShadowCastingMode.Off : ShadowCastingMode.On;
            Parts.Clear();
        }

        /// <summary>The ONE place a material is written (D-0167). Every colour material on the gun (its name holds
        /// the list's skin mark) becomes the skin's material; lenses, reticles, glass, metal and the parts' own
        /// materials do not carry the mark and are left alone, so the sight look (D-0166) stays. Shared materials
        /// only: nothing is copied, so nothing has to be cleaned up.</summary>
        private void PutSkinOn()
        {
            // No skin was ever chosen for this gun: it stays exactly as it came.
            if (_skin.Length == 0 && !_skinned) { _pending = false; _fade = null; enabled = false; return; }
            ArmoryCatalog.Skin skin = _catalog.SkinById(SkinInEffect);
            if (skin == null || skin.material == null) { _pending = false; _fade = null; enabled = false; return; }
            // Somebody else holds the gun's materials for the moment and will put the old ones back: wait (LateUpdate).
            if (Borrowed()) { _pending = true; enabled = true; return; }
            Material shown = skin.material;
            GetComponentsInChildren(true, Parts);
            for (int p = 0; p < Parts.Count; p++)
            {
                Renderer part = Parts[p];
                part.GetSharedMaterials(Looks);
                bool changed = false;
                for (int i = 0; i < Looks.Count; i++)
                {
                    Material now = Looks[i];
                    if (now == null || now == shown || !_catalog.IsSkin(now)) continue;
                    Looks[i] = shown;
                    changed = true;
                }
                if (changed) part.SetSharedMaterials(Looks);
            }
            Parts.Clear();
            Looks.Clear();
            _skinned = true;
            _pending = false;
            enabled = false;
        }

        /// <summary>True while somebody else has swapped this gun's materials and will put the old ones back, which
        /// would undo a skin written now: (a) the body fade, while the body is not fully shown (it holds nothing
        /// exactly when Shown is 1); (b) the size shift's bend, known by the ending of its copies' names. The fade is
        /// looked up every time, because a gun changes hands. The fade a skin waited on is remembered too: a gun
        /// dropped while it waits is no longer under that body, but that fade still holds its old materials and
        /// writes them back when the body is shown again, so the skin waits that out as well.</summary>
        private bool Borrowed()
        {
            BodyFade fade = GetComponentInParent<BodyFade>(true);
            if (fade != null && fade.Shown < 1f) { _fade = fade; return true; }
            if (_fade != null && _fade.Shown < 1f) return true;   // dropped while it waited (a destroyed fade reads as null)
            _fade = null;
            bool bent = false;
            GetComponentsInChildren(true, Parts);
            for (int p = 0; p < Parts.Count && !bent; p++)
            {
                Parts[p].GetSharedMaterials(Looks);
                for (int i = 0; i < Looks.Count; i++)
                {
                    if (Looks[i] == null || !Looks[i].name.EndsWith(WorldBend.CopySuffix, StringComparison.Ordinal)) continue;
                    bent = true;
                    break;
                }
            }
            Parts.Clear();
            Looks.Clear();
            return bent;
        }

        /// <summary>Runs only while a skin waits (the component is switched off the rest of the time, so it costs
        /// nothing per frame): as soon as the materials are the gun's own again, the skin goes on.</summary>
        private void LateUpdate()
        {
            if (!_pending) { enabled = false; return; }
            if (Borrowed()) return;
            PutSkinOn();
        }
    }
}
