using System.Collections;
using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// A door between the house's rooms that are scenes of their own (D-0091): the playroom's doors to the Workshop, the
    /// Raceway and the Colour Corner, and each room's door home. E (any size) loads the other scene; the door there named
    /// Arrive At puts the player down at its Arrive point with the size, the weapons in hand and the shift charge they
    /// had (HouseCarry). Needs a collider for the Interactor to find (a trigger over the door's front). Placed by
    /// CourseBuilder.LandHouse.
    /// </summary>
    public sealed class HubDoor : MonoBehaviour, IInteractable
    {
        [SerializeField, Tooltip("This door's name. The door on the other side names it in its Arrive At.")] private string id = "";
        [SerializeField, Tooltip("The scene this door opens into.")] private string scene = "";
        [SerializeField, Tooltip("The id of the door in that scene where the player comes out.")] private string arriveAt = "";
        [SerializeField, Tooltip("Where a player coming through to this door stands, and which way they face.")] private Transform arrive;
        [SerializeField, Tooltip("The prompt next to E. Placeholder wording.")] private string prompt = "Go through";
        [SerializeField, Tooltip("Seconds of the black fade before the next room loads (0 = at once).")] private float fade = 0.25f;

        private bool _going;

        public string Id => id;
        public string Scene => scene;
        public string ArriveAt => arriveAt;
        public Transform ArrivePoint => arrive;

        private IEnumerator Start()
        {
            if (HouseCarry.ArriveAt != id) yield break;
            yield return null;   // after every Start: the scene's own spawn, a sandbox's full charge, a Loadout's blaster
            var player = GameObject.Find("Player");
            HouseCarry.Arrive(id, player, arrive != null ? arrive : transform);
        }

        public string PromptFor(Interactor who)
        {
            if (NetSession.InParty) return "Online: this door opens later";   // the group travels together (D-0125 step 3)
            if (_going || who == null || who.Shifter == null || who.Shifter.Shifting) return "";
            return prompt;
        }

        public void Interact(Interactor who)
        {
            if (_going || who == null || who.Shifter == null || who.Shifter.Shifting || string.IsNullOrEmpty(scene)) return;
            if (NetSession.InParty) return;   // one player alone may not take the scene away from the party
            _going = true;
            GameObject player = who.gameObject;
            ScreenFade screen = fade > 0f ? ScreenFade.Find() : null;
            if (screen != null) screen.Cover(fade, () => HouseCarry.Go(player, scene, arriveAt));
            else HouseCarry.Go(player, scene, arriveAt);
        }
    }
}
