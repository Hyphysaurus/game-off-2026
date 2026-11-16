using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// What a player starts a scene with: these weapons in hand (copies of the prefabs, the first one drawn) and this
    /// kind of ammo loaded. Sits beside the WeaponHolder. The Colour Corner starts with a blaster and paint.
    /// </summary>
    [RequireComponent(typeof(WeaponHolder))]
    public sealed class Loadout : MonoBehaviour
    {
        [SerializeField, Tooltip("Weapon prefabs handed over at the start, in order. The holder keeps as many as it can carry.")] private Weapon[] weapons = new Weapon[0];
        [SerializeField, Tooltip("The AmmoBelt's kind loaded at the start (0 = the first: darts).")] private int ammo;

        private void Start()
        {
            var holder = GetComponent<WeaponHolder>();
            Weapon first = null;
            foreach (Weapon prefab in weapons)
            {
                if (prefab == null || holder.Full) continue;
                Weapon taken = holder.Equip(prefab);
                if (first == null) first = taken;
            }
            if (first != null) holder.Equip(first);   // back to the first one: Equip draws whatever it was last handed
            if (TryGetComponent(out AmmoBelt belt)) belt.Select(ammo);
        }
    }
}
