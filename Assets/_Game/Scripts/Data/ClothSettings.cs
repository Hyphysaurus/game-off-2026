using System;
using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// Every number of the Magica Cloth 2 curtains and flags (D-0233), on one asset: Resources/ClothSettings, made by
    /// Shift Jam > House > Land Cloth (D-0233) when it is missing (an existing one keeps his numbers; a missing place is
    /// added). Each ClothDresser reads it when it builds its cloth, and again whenever the asset is changed in the
    /// Inspector, in Play too: most numbers then take hold at once; Hold, Hold Distance, Join Pieces and Units Per Metre
    /// build the cloth again.
    /// Magica Cloth works in metres. Units Per Metre says how many of our units it takes as one metre for a kind: the house
    /// is about 50 units a metre (the kid is 63 tall), so a curtain at 50 moves like a real curtain; a flag at 1 moves like
    /// a big flag as the toy sees it. Gravity, breeze, speed and thickness below are in those metres (Magica's own numbers,
    /// which its presets use: the defaults come from its Long Hair preset for the curtain's cord and its Cape preset for
    /// the flags, with less gravity on the flags so a breeze lifts them).
    /// The Playroom pack's Curtain_01 is a raised blind with a pull cord: its headrail, its folded slats and its tapes stay
    /// as modelled and the cord is the cloth (see Hold Distance).
    /// OFF SWITCHES: Cloth On (every curtain and flag stands still as modelled, no cloth is made), a kind's On, a place's
    /// On, Player Pushes, Shots Poke, and a place's Breeze at 0.
    /// </summary>
    [CreateAssetMenu(menuName = "Shift Jam/Cloth Settings", fileName = "ClothSettings")]
    public sealed class ClothSettings : ScriptableObject
    {
        public const string ResourceName = "ClothSettings";

        /// <summary>What a ClothDresser dresses: a curtain (held at the top of its model) or a flag (held at its pole).</summary>
        public enum ClothKind { Curtain, Flag }

        /// <summary>Which points of a model are held still: those within Hold Distance of its top (a curtain hangs from its
        /// rod), or of its pole (the upright line through the model's own pivot: a flag flies from its pole).</summary>
        public enum HoldFrom { Top, Pole }

        /// <summary>The numbers of one kind of cloth.</summary>
        [Serializable]
        public sealed class Kind
        {
            [Tooltip("This kind is a cloth. Off = every one of them stands still as modelled.")]
            public bool on = true;
            [Tooltip("Where the cloth is held: Top = the points near the top of the model (a curtain on its rod); Pole = the points near the upright line through the model's own pivot (a flag on its pole).")]
            public HoldFrom hold = HoldFrom.Top;
            [Min(0f), Tooltip("How far from the top (or the pole) a point is held still, in the model's own units. A separate piece of the model wholly inside this stays as modelled; a piece that reaches past it hangs from its points inside it and moves; a loose piece wholly outside stays still too (it would fall away). Curtain 16.5: the headrail, the rod, the brackets, the folded slats and the lift tapes (down to 14.0 under the top) stay; the pull cord (to 65.2 under the top) hangs from its top ring. Flag 0.6: the pole and its knob stay, the flag's edge along the pole is held (0.27 to 0.47 from the pole line), the rest flies (from 0.68 out). Changing it builds the cloth again.")]
            public float holdDistance = 16.5f;
            [Range(0f, 0.2f), Tooltip("Magica's joining of near points (its reduction, a share of the model's size). 0 = off: the model's pieces are kept apart and a loose piece stays still. Above 0, near pieces are joined into one cloth and a loose piece moves (it then hangs from its neighbours, or falls if it reaches none): to try the curtain's slats moving, lower Hold Distance to 4 and set this to about 0.02, and look. Changing it builds the cloth again.")]
            public float joinPieces;
            [Min(0.01f), Tooltip("How many of our units Magica takes as one metre for this kind (it builds the cloth that much smaller for an instant, so every force, speed and thickness scales with it). Curtain 50 (the house: the kid is 63 tall), so it moves like a real curtain; Flag 1, so it moves like a big flag seen by the toy. Changing it builds the cloth again.")]
            public float unitsPerMetre = 50f;
            [Range(0f, 20f), Tooltip("Magica's gravity, metres a second each second (its presets use 0 to 7; it allows up to 20).")]
            public float gravity = 5f;
            [Range(0f, 1f), Tooltip("Magica's air resistance (0 = none, 1 = very thick). Its presets use 0.05 to 0.3.")]
            public float damping = 0.1f;
            [Range(0f, 1f), Tooltip("How hard the cloth keeps its length between points (Magica's distance stiffness). 1 = it does not stretch.")]
            public float stretchStiffness = 1f;
            [Range(0f, 1f), Tooltip("How hard the cloth keeps its folds and its flatness (Magica's triangle bending stiffness). Lower = it crumples and ripples more.")]
            public float bendStiffness = 1f;
            [Range(0f, 1f), Tooltip("How hard the cloth turns back to its modelled shape near where it is held (Magica's angle restoration). 0 = off: only gravity and the breeze shape it.")]
            public float shapeKeep = 0.2f;
            [Range(0f, 1f), Tooltip("Shape Keep at the far end, as a share of the held end's (Magica's curve from 1 to this). Low = the far end swings freely.")]
            public float shapeKeepAtEnd = 0.5f;
            [Range(0f, 1f), Tooltip("How far the cloth may bunch up toward where it is held (Magica's tether compression: 0 = not at all, 1 = all the way).")]
            public float tetherSquash = 0.4f;
            [Range(0.001f, 1f), Tooltip("How thick each point of the cloth is when something pushes it, metres (Magica's particle radius; times Units Per Metre in our units: the curtain's 0.02 is 1 unit, its cord is 0.8 thick).")]
            public float thickness = 0.02f;
            [Tooltip("The cloth's points never move faster than Speed Limit (Magica's particle speed limit). Off = no limit.")]
            public bool limitSpeed = true;
            [Range(0f, 10f), Tooltip("The fastest a point of the cloth moves, metres a second (times Units Per Metre in our units).")]
            public float speedLimit = 3f;
            [Tooltip("The player pushes the cloth's edges, not only its points (Magica's Edge collision). On for the curtain: its cord is one long edge from its top ring to its tassel, with no points between.")]
            public bool edgeCollision = true;
            [Range(0f, 0.5f), Tooltip("How much the cloth sticks to the player as it is pushed (Magica's collider friction).")]
            public float friction = 0.05f;
            [Range(0f, 2f), Tooltip("How much the breeze moves this kind (Magica's wind influence: 1 = all of it).")]
            public float windInfluence = 1f;
            [Range(0f, 2f), Tooltip("How quickly the breeze comes and goes (Magica's wind frequency: higher = faster).")]
            public float windFrequency = 1f;
            [Range(0f, 2f), Tooltip("How much the breeze's way wanders (Magica's wind turbulence, times the place's Gusts).")]
            public float windTurbulence = 1f;
            [Min(0f), Tooltip("How hard a shot that passes through the moving part pushes the cloth along its flight (Magica's AddForce, metres a second each second for that frame: the far end moves most, the held end least). 0 = shots go through without a poke.")]
            public float shotPoke = 20f;
        }

        /// <summary>One place's breeze and switches.</summary>
        [Serializable]
        public sealed class Place
        {
            [Tooltip("The place's name; each ClothDresser names its place (Bedroom, Window, Downstairs, PlayroomFort, TanFort).")]
            public string id = "";
            [Tooltip("The cloths of this place move. Off = they stand still as modelled.")]
            public bool on = true;
            [Range(0f, 30f), Tooltip("The breeze's strength, metres a second in the kind's own metres (a Magica wind zone round each cloth). 0 = no breeze: the cloth only moves when pushed or poked. Against the curtain's gravity of 5, 0.6 leans its cord about 7 degrees at the most.")]
            public float breeze;
            [Range(0f, 1f), Tooltip("How gusty the breeze is (the wind zone's turbulence): 0 = steady, 1 = it comes and goes.")]
            public float gusts = 1f;
            [Range(-180f, 180f), Tooltip("Degrees the breeze is turned round the upright from its own way: a curtain's breeze blows from the window into the room; a flag's blows from its pole toward its free end.")]
            public float turn;
            [Range(-90f, 90f), Tooltip("Degrees the breeze is tipped up (positive) or down (negative).")]
            public float tilt;
            [Tooltip("The cloths of this place stop and settle back to rest when the camera is further than Far Distance from them (Magica's own distance culling).")]
            public bool offWhenFar;
        }

        [Header("D-0233 Magica Cloth: curtains and flags")]
        [Tooltip("Every curtain and flag is a Magica cloth. Off = they all stand still as modelled (no cloth is made, nothing is simulated).")]
        public bool clothOn = true;

        [Header("Curtains (Playroom Curtain_01: a raised blind; its pull cord is the cloth)")]
        [Tooltip("The numbers of every curtain.")]
        public Kind curtain = new Kind
        {
            hold = HoldFrom.Top, holdDistance = 16.5f, joinPieces = 0f, unitsPerMetre = 50f,
            gravity = 5f, damping = 0.1f, stretchStiffness = 1f, bendStiffness = 1f, shapeKeep = 0.2f, shapeKeepAtEnd = 0.5f,
            tetherSquash = 0.4f, thickness = 0.02f, limitSpeed = true, speedLimit = 3f, edgeCollision = true, friction = 0.05f,
            windInfluence = 1f, windFrequency = 1f, windTurbulence = 1f, shotPoke = 20f,
        };

        [Header("Flags (Playroom Army_Flag_01 on the forts)")]
        [Tooltip("The numbers of every flag.")]
        public Kind flag = new Kind
        {
            hold = HoldFrom.Pole, holdDistance = 0.6f, joinPieces = 0f, unitsPerMetre = 1f,
            gravity = 2f, damping = 0.05f, stretchStiffness = 1f, bendStiffness = 0.5f, shapeKeep = 0.15f, shapeKeepAtEnd = 0.1f,
            tetherSquash = 0.5f, thickness = 0.02f, limitSpeed = true, speedLimit = 4f, edgeCollision = false, friction = 0.1f,
            windInfluence = 1f, windFrequency = 1f, windTurbulence = 1f, shotPoke = 60f,
        };

        [Header("Places: each place's breeze and switches")]
        [Tooltip("One row a place, found by its id. A ClothDresser whose place is not on the list moves, with no breeze.")]
        public Place[] places = DefaultPlaces();

        [Header("The player pushes the cloth")]
        [Tooltip("The toy and the kid push the cloth aside when they walk into it (a Magica capsule on each player, following its walking capsule, or the first-person body's). Off = they pass through it.")]
        public bool playerPushes = true;
        [Min(0f), Tooltip("At toy size: the pushing capsule's radius as a share of the toy's walking capsule's (0.3 at toy size: 1.2 makes 0.36).")]
        public float toyRadius = 1.2f;
        [Min(0f), Tooltip("At toy size: the pushing capsule's height as a share of the toy's walking capsule's (1.8 at toy size).")]
        public float toyHeight = 1f;
        [Min(0f), Tooltip("At kid size: the pushing capsule's radius as a share of the kid's walking capsule's (10.5 at kid size: 1.2 makes 12.6). Between the sizes it blends as the shift grows.")]
        public float kidRadius = 1.2f;
        [Min(0f), Tooltip("At kid size: the pushing capsule's height as a share of the kid's walking capsule's (63 at kid size).")]
        public float kidHeight = 1f;
        [Min(0.25f), Tooltip("Seconds between looks for players to give a pushing capsule to (a player joining later is found within this). The look makes a small list each time, never every frame.")]
        public float lookForPlayersSeconds = 2f;

        [Header("A shot pokes the cloth")]
        [Tooltip("A dart or pellet whose flight passes through a cloth's moving part pokes it as it gets there (the shot itself flies on: the curtain has no collider; it sticks in a flag as before). Off = no pokes.")]
        public bool shotsPoke = true;
        [Min(0f), Tooltip("How near the moving part a shot must pass to poke it, in our units for the toy's shots, times the square root of the shooter's size for bigger ones (the kid's darts are 35 times bigger: about 6 times this, 3.0). The curtain's cord is only 0.8 thick.")]
        public float pokeReach = 0.5f;

        [Header("Far away, and building")]
        [Range(1f, 100f), Tooltip("For a place with Off When Far: further than this from the camera, its cloths stop and settle back to rest (Magica's distance culling; Magica allows at most 100).")]
        public float farDistance = 100f;
        [Range(0f, 1f), Tooltip("The last share of Far Distance over which such a cloth eases to rest instead of stopping at once.")]
        public float farFade = 0.2f;
        [Min(1), Tooltip("How many cloths start building in one frame as the scene starts (each build runs on worker threads; its start reads the model on the main thread).")]
        public int buildsPerFrame = 1;

        /// <summary>The places the maker knows, with their first numbers.</summary>
        public static Place[] DefaultPlaces() => new[]
        {
            new Place { id = "Bedroom", on = true, breeze = 0.6f, gusts = 1f },
            new Place { id = "Window", on = true, breeze = 0.8f, gusts = 1f },
            new Place { id = "Downstairs", on = true, breeze = 0.5f, gusts = 1f, offWhenFar = true },
            new Place { id = "PlayroomFort", on = true, breeze = 3f, gusts = 1f },
            new Place { id = "TanFort", on = true, breeze = 3f, gusts = 1f },
        };

        /// <summary>Goes up by one each time the asset is changed in the Inspector (in Play too): every cloth then fits itself
        /// to the new numbers.</summary>
        public static int Changes { get; private set; }

        private void OnValidate() => Changes++;

        private static ClothSettings _loaded;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Forget() => _loaded = null;

        /// <summary>Resources/ClothSettings, or the defaults above when there is no such asset. Loaded once and kept.</summary>
        public static ClothSettings Load()
        {
            if (_loaded != null) return _loaded;
            _loaded = Resources.Load<ClothSettings>(ResourceName);
            if (_loaded == null)
            {
                _loaded = CreateInstance<ClothSettings>();
                _loaded.name = ResourceName + " (defaults)";
                _loaded.hideFlags = HideFlags.DontSave;
            }
            return _loaded;
        }

        /// <summary>The numbers of a kind.</summary>
        public Kind For(ClothKind kind) => kind == ClothKind.Flag ? flag : curtain;

        /// <summary>The place with this id, or null.</summary>
        public Place Find(string id)
        {
            if (places == null || string.IsNullOrEmpty(id)) return null;
            for (int i = 0; i < places.Length; i++)
                if (places[i] != null && places[i].id == id) return places[i];
            return null;
        }

        /// <summary>Adds the default places whose ids are not on the list yet (the maker; his rows are kept as they are).
        /// Returns how many were added.</summary>
        public int AddMissingPlaces()
        {
            Place[] wanted = DefaultPlaces();
            int added = 0;
            foreach (Place w in wanted)
            {
                if (Find(w.id) != null) continue;
                int n = places != null ? places.Length : 0;
                var grown = new Place[n + 1];
                for (int i = 0; i < n; i++) grown[i] = places[i];
                grown[n] = w;
                places = grown;
                added++;
            }
            return added;
        }
    }
}
