using UnityEngine;

namespace ShiftJam
{
    /// <summary>Which hand something is held in (D-0144). Set on the thing (a Carryable's Hand; a weapon is Right, or Both
    /// when two-handed), never decided in code by what it is.</summary>
    public enum HandUse { Right, Left, Both }

    /// <summary>
    /// The player's two hands (D-0144, his "independent hands, we should be able to shoot and aim the flashlight at once.
    /// This opens up other future interactables where we use both hands"). One owner for what each hand may hold, so the
    /// weapons (WeaponHolder), the hand tools (Carrier) and the slots (Inventory) agree instead of each taking the right
    /// hand:
    /// - A right-hand thing (a pistol, a knife) and a left-hand thing (the flashlight) are held together.
    /// - Taking out something for a hand already in use puts the old thing in that hand away.
    /// - Taking out something two-handed (a rifle, a plank) puts the left-hand thing away; it comes back to the left hand
    ///   when the two-handed thing goes, unless the slot was chosen again or it left the bar meanwhile.
    /// Added to every player by the WeaponHolder (as the Inventory is); also on Prefabs/Player.
    /// </summary>
    [DefaultExecutionOrder(85)]   // before the Inventory (90) and the WeaponHolder (100)
    public sealed class PlayerHands : MonoBehaviour
    {
        private WeaponHolder _holder;
        private Carrier _carrier;
        private Inventory _inventory;
        private Carryable _waiting;   // a left-hand tool put away for a two-handed thing: back when that goes

        /// <summary>The hands of <paramref name="player"/>, added if it has none.</summary>
        public static PlayerHands On(GameObject player)
        {
            if (player == null) return null;
            if (!player.TryGetComponent(out PlayerHands hands)) hands = player.AddComponent<PlayerHands>();
            return hands;
        }

        public static HandUse Of(Weapon w) => w != null && w.TwoHanded ? HandUse.Both : HandUse.Right;
        public static HandUse Of(Carryable c) => c == null ? HandUse.Right : c.TwoHanded ? HandUse.Both : c.Hand;

        /// <summary>True when the two can be held at once: one in each hand.</summary>
        public static bool Together(HandUse a, HandUse b) => (a == HandUse.Right && b == HandUse.Left) || (a == HandUse.Left && b == HandUse.Right);

        private void Awake() => Find();

        /// <summary>Its neighbours; the Inventory may be added after this (the WeaponHolder adds it).</summary>
        private void Find()
        {
            if (_holder == null) _holder = GetComponent<WeaponHolder>();
            if (_carrier == null) _carrier = GetComponent<Carrier>();
            if (_inventory == null) _inventory = GetComponent<Inventory>();
        }

        private Weapon GunOut => _holder != null && _holder.Armed ? _holder.Current : null;
        private Carryable ToolOut => _carrier != null ? _carrier.Held : null;

        /// <summary>True while the right hand (or both) is busy with something that is not a weapon: a right-hand tool,
        /// a plank. Moves that need the hands (a roll, a punch) wait for it; a left-hand tool does not stop them.</summary>
        public bool RightHandTaken => ToolOut != null && Of(ToolOut) != HandUse.Left;

        /// <summary>Before <paramref name="w"/> comes to hand: a tool that cannot be held with it is put away (a left-hand
        /// tool waits for a two-handed weapon to go).</summary>
        public void MakeRoomFor(Weapon w)
        {
            Find();
            Carryable tool = ToolOut;
            if (tool == null || Together(Of(w), Of(tool))) return;
            if (Of(tool) == HandUse.Left) _waiting = tool;
            _carrier.Stow();
            if (_carrier.Held != null) _carrier.PutDown();   // not a slot tool (a plank): it is set down
        }

        /// <summary>Before <paramref name="tool"/> comes to hand: a weapon that cannot be held with it is put away.</summary>
        public void MakeRoomFor(Carryable tool)
        {
            if (tool == _waiting) _waiting = null;   // chosen again by hand
            Find();
            Weapon gun = GunOut;
            if (gun != null && !Together(Of(tool), Of(gun))) _holder.SetDrawn(false);
        }

        /// <summary>The player put a left-hand tool away on purpose (its slot again, X): it does not come back by itself.</summary>
        public void Forget(Carryable tool) { if (tool == _waiting) _waiting = null; }

        private void Update()
        {
            if (_waiting == null) return;
            Find();
            if (_inventory == null || !_inventory.Holds(_waiting)) { _waiting = null; return; }   // left the bar
            if (ToolOut != null) return;
            Weapon gun = GunOut;
            if (gun != null && Of(gun) == HandUse.Both) return;   // still in both hands
            Carryable back = _waiting;
            _waiting = null;
            _inventory.TakeOut(back);
        }
    }
}
