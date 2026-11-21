using System.Collections.Generic;
using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// Party play (D-0110): which seat a player sits in (0 to 3), with its own camera rig and input. PartyJoin adds one
    /// to every player in a party scene. Scripts that used to take "the" camera ask RigFor / ViewFor with themselves:
    /// inside a party player that is its own camera; anywhere else (single player, no slot) it is the old answer, the
    /// first ShoulderCamera or Camera.main, so single player is unchanged.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerSlot : MonoBehaviour
    {
        /// <summary>Every seated player, in seat order.</summary>
        public static readonly List<PlayerSlot> All = new List<PlayerSlot>();

        public int Index { get; private set; }
        public ShoulderCamera Rig { get; private set; }
        public PlayerInput Input { get; private set; }

        public void Seat(int index, ShoulderCamera rig, PlayerInput input)
        {
            Index = index; Rig = rig; Input = input;
            All.Remove(this);
            int at = 0;
            while (at < All.Count && All[at].Index < index) at++;
            All.Insert(at, this);
        }

        private void OnDestroy() => All.Remove(this);

        /// <summary>The camera rig of the player <paramref name="part"/> belongs to (a weapon in its hand, its body, its
        /// own scripts); the scene's first ShoulderCamera when it belongs to no seated player.</summary>
        public static ShoulderCamera RigFor(Component part)
        {
            if (part != null)
            {
                PlayerSlot slot = part.GetComponentInParent<PlayerSlot>();
                if (slot != null && slot.Rig != null) return slot.Rig;
            }
            return FindFirstObjectByType<ShoulderCamera>();
        }

        /// <summary>The camera that player sees through; Camera.main when it belongs to no seated player.</summary>
        public static Camera ViewFor(Component part)
        {
            if (part != null)
            {
                PlayerSlot slot = part.GetComponentInParent<PlayerSlot>();
                if (slot != null && slot.Rig != null) return slot.Rig.View;
            }
            return Camera.main;
        }
    }
}
