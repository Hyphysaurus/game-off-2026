using System;
using System.Collections.Generic;
using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// How gun sights look in the game (D-0166, his "it's hard to see targets through those reticles or target sights").
    /// The pack's sights come with a tinted, shiny lens, a grey plate behind it, and a reticle that is a black picture
    /// lit by the room. This is the one list that says what each of those materials is shown as instead: a clear lens,
    /// nothing for the plate, a reticle that is lit by itself (so it shows the same in a bright room and a Dark Zone).
    /// Every weapon asks once as it wakes (Weapon.Awake), so a gun in a hand, dropped, or on another player gets it,
    /// and so does any sight fitted later, with nothing to set per prefab. A stand shows the bare model, not a weapon,
    /// so the stand asks for its display (WeaponPickup.Awake). A gun may name its own reticle (Weapon > Reticle);
    /// otherwise the sight's own kind decides (the rows marked Reticle).
    /// Lives at Resources/SightLook; without the asset nothing is changed. The looks are materials on the
    /// ShiftJam/SightOverlay shader (Data/Sights): colour, size and how solid are set on them.
    /// Keep the NAMES of the lens and plate materials starting with "Sight_Glass" and "Sight_Projection": GunSight
    /// finds the line of sight from the materials named that way. In the editor, out of Play, a gun still shows the
    /// pack's own look: the change is made as the game runs.
    /// </summary>
    [CreateAssetMenu(menuName = "Shift Jam/Sight Look", fileName = "SightLook")]
    public sealed class SightLook : ScriptableObject
    {
        /// <summary>The shader the looks are drawn with (WorldBend lets a sight's lens and reticle ride along unbent).</summary>
        public const string OverlayShader = "ShiftJam/SightOverlay";

        /// <summary>One material of the pack's sights and what the game shows in its place.</summary>
        [Serializable]
        public struct Swap
        {
            [Tooltip("A material the pack's sights use (the lens glass, the plate behind it, a reticle).")] public Material from;
            [Tooltip("What the game shows in its place.")] public Material to;
            [Tooltip("On: this one is a reticle, and a gun's own Reticle (Weapon > Reticle) is shown instead when it has one.")] public bool reticle;
        }

        [SerializeField, Tooltip("Each material of the pack's sights and what the game shows in its place. A material that is not listed is left as it is.")] private Swap[] swaps = Array.Empty<Swap>();

        private static SightLook _loaded;
        private static bool _looked;
        private static readonly List<Renderer> Parts = new List<Renderer>();
        private static readonly List<Material> Looks = new List<Material>();

        /// <summary>Resources/SightLook, or null when the asset is missing (sights then keep the pack's look).</summary>
        public static SightLook Load()
        {
            if (!_looked) { _looked = true; _loaded = Resources.Load<SightLook>("SightLook"); }
            return _loaded;
        }

        /// <summary>Gives every sight material under <paramref name="weapon"/> the game's look. <paramref name="reticle"/>
        /// is the weapon's own reticle, or null for the sight's own kind. Only the listed materials are touched, and
        /// only on this weapon's own renderers (their shared materials: nothing is copied).</summary>
        public void Apply(Transform weapon, Material reticle)
        {
            if (weapon == null || swaps == null || swaps.Length == 0) return;
            weapon.GetComponentsInChildren(true, Parts);
            for (int p = 0; p < Parts.Count; p++)
            {
                Renderer part = Parts[p];
                part.GetSharedMaterials(Looks);
                bool changed = false;
                for (int i = 0; i < Looks.Count; i++)
                {
                    Material now = Looks[i];
                    if (now == null) continue;
                    for (int s = 0; s < swaps.Length; s++)
                    {
                        if (swaps[s].from != now) continue;
                        Material shown = swaps[s].reticle && reticle != null ? reticle : swaps[s].to;
                        if (shown != null && shown != now) { Looks[i] = shown; changed = true; }
                        break;
                    }
                }
                if (changed) part.SetSharedMaterials(Looks);
            }
            Parts.Clear();
            Looks.Clear();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { _loaded = null; _looked = false; }
    }
}
