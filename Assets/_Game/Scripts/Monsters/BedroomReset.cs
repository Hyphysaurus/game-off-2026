using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// The console's "bedroom reset" (D-0228, for testing without leaving Play): the active save slot forgets the wall
    /// switch's lights and the star lamp (the bedroom's RoomLights and StarLamp keys, "bedroom:lights" and "bedroom:stars"
    /// when none is found), so both read the slot again at once (SaveGame.Changed): the room goes dark, the switch can be
    /// pressed again, the lamp sleeps and wants the flashlight again. Every monster under the bed stands whole at its spot
    /// again, awake in the dark, as on a New Game. D-0241: it forgets the bedroom gate too ("bedroom:monsters-cleared"
    /// when no gate is found): the shelf switch, the wall switch and the claw machine are held shut again and the knockout
    /// count starts again. Nothing else in the slot changes (its save point, its stars, its steps, the gun snatch, the
    /// other rooms' moments).
    /// </summary>
    public static class BedroomReset
    {
        /// <summary>The keys used when the scene has no RoomLights, StarLamp or BedroomGate to read them from (D-0214, D-0218, D-0241).</summary>
        public const string LightsKey = "bedroom:lights", StarsKey = "bedroom:stars", GateKey = "bedroom:monsters-cleared";

        /// <summary>Does it now and returns what it did, in one line for the console.</summary>
        public static string Run()
        {
            int slot = SaveGame.Slot;
            // The lights and lamp the cave's monsters answer to; any in the scene when there is no monster.
            DarkMonster monster = DarkMonster.FirstRoamer;
            RoomLights lights = monster != null && monster.Lights != null ? monster.Lights : Object.FindFirstObjectByType<RoomLights>();
            StarLamp lamp = monster != null && monster.Lamp != null ? monster.Lamp : Object.FindFirstObjectByType<StarLamp>();
            BedroomGate gate = Object.FindFirstObjectByType<BedroomGate>();   // D-0241
            string lightsKey = lights != null && !string.IsNullOrEmpty(lights.Key) ? lights.Key : LightsKey;
            string starsKey = lamp != null && !string.IsNullOrEmpty(lamp.Key) ? lamp.Key : StarsKey;
            string gateKey = gate != null && !string.IsNullOrEmpty(gate.Key) ? gate.Key : GateKey;
            bool wasLit = SaveGame.Seen(slot, lightsKey), wasStars = SaveGame.Seen(slot, starsKey), wasOpen = SaveGame.Seen(slot, gateKey);
            int knocked = gate != null ? gate.KnockedOut : 0;
            SaveGame.Forget(slot, lightsKey);   // each raises SaveGame.Changed when it was there: the lights, the lamp and the gate read it
            SaveGame.Forget(slot, starsKey);
            SaveGame.Forget(slot, gateKey);
            if (gate != null) gate.CountAgain();   // a gate that was not open yet keeps no count either
            int back = DarkMonster.ReviveAll();
            string said = "bedroom reset (slot " + (slot + 1) + "): '" + lightsKey + "' " + (wasLit ? "forgotten, the room is dark" : "was not set")
                + "; '" + starsKey + "' " + (wasStars ? "forgotten, the lamp sleeps" : "was not set")
                + "; '" + gateKey + "' " + (wasOpen ? "forgotten, the bedroom gate is shut again" : "was not set") + (gate != null ? " (the knockout count, " + knocked + " of " + gate.Needed + ", starts again)" : " (no BedroomGate in the scene)")
                + "; " + back + (back == 1 ? " monster" : " monsters") + " back at their spots, whole"
                + (lights == null ? " (no RoomLights in the scene)" : "") + (lamp == null ? " (no StarLamp in the scene)" : "");
            Playtest.PlayLog.Tell(said);
            return said;
        }
    }
}
