using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// The console's "bedroom reset" (D-0228, for testing without leaving Play): the active save slot forgets the wall
    /// switch's lights and the star lamp (the bedroom's RoomLights and StarLamp keys, "bedroom:lights" and "bedroom:stars"
    /// when none is found), so both read the slot again at once (SaveGame.Changed): the room goes dark, the switch can be
    /// pressed again, the lamp sleeps and wants the flashlight again. Every monster under the bed stands whole at its spot
    /// again, awake in the dark, as on a New Game. Nothing else in the slot changes (its save point, its stars, the
    /// other rooms' moments).
    /// </summary>
    public static class BedroomReset
    {
        /// <summary>The keys used when the scene has no RoomLights or StarLamp to read them from (D-0214, D-0218).</summary>
        public const string LightsKey = "bedroom:lights", StarsKey = "bedroom:stars";

        /// <summary>Does it now and returns what it did, in one line for the console.</summary>
        public static string Run()
        {
            int slot = SaveGame.Slot;
            // The lights and lamp the cave's monsters answer to; any in the scene when there is no monster.
            DarkMonster monster = DarkMonster.FirstRoamer;
            RoomLights lights = monster != null && monster.Lights != null ? monster.Lights : Object.FindFirstObjectByType<RoomLights>();
            StarLamp lamp = monster != null && monster.Lamp != null ? monster.Lamp : Object.FindFirstObjectByType<StarLamp>();
            string lightsKey = lights != null && !string.IsNullOrEmpty(lights.Key) ? lights.Key : LightsKey;
            string starsKey = lamp != null && !string.IsNullOrEmpty(lamp.Key) ? lamp.Key : StarsKey;
            bool wasLit = SaveGame.Seen(slot, lightsKey), wasStars = SaveGame.Seen(slot, starsKey);
            SaveGame.Forget(slot, lightsKey);   // each raises SaveGame.Changed when it was there: the lights and the lamp read it
            SaveGame.Forget(slot, starsKey);
            int back = DarkMonster.ReviveAll();
            string said = "bedroom reset (slot " + (slot + 1) + "): '" + lightsKey + "' " + (wasLit ? "forgotten, the room is dark" : "was not set")
                + "; '" + starsKey + "' " + (wasStars ? "forgotten, the lamp sleeps" : "was not set")
                + "; " + back + (back == 1 ? " monster" : " monsters") + " back at their spots, whole"
                + (lights == null ? " (no RoomLights in the scene)" : "") + (lamp == null ? " (no StarLamp in the scene)" : "");
            Playtest.PlayLog.Tell(said);
            return said;
        }
    }
}
