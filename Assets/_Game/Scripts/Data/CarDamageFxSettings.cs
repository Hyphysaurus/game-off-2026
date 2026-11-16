using System;
using System.Collections.Generic;
using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// What a car shows and plays when it is hurt (D-0059): bonnet smoke and spitting sparks as its health drops, sparks,
    /// paint flecks and a knock sound on every hit, loose parts knocked off by hard ones, and the wreck's burst. The effects
    /// are Polygon Arsenal prefabs (its URP upgrade) and the sounds Case Portman's Forge &amp; Fantasy impacts, picked by
    /// Shift Jam > Workshop > Build Car Damage FX, which also bakes each car body's paint colour for the flecks.
    /// Loaded from Resources/CarDamageFx; without it a hurt car shows and plays nothing.
    /// </summary>
    [CreateAssetMenu(menuName = "Shift Jam/Car Damage FX", fileName = "CarDamageFx")]
    public sealed class CarDamageFxSettings : ScriptableObject
    {
        /// <summary>The name the asset is loaded by: Resources/CarDamageFx.</summary>
        public const string ResourceName = "CarDamageFx";

        [Header("Bonnet smoke and sparks (shares of full health)")]
        [Tooltip("Light grey wisps below this share of full health.")] [Range(0f, 1f)] public float lightSmokeBelow = 0.5f;
        [Tooltip("Dark smoke below this share (and while wrecked).")] [Range(0f, 1f)] public float darkSmokeBelow = 0.3f;
        [Tooltip("Sparks spit from the bonnet now and then below this share.")] [Range(0f, 1f)] public float sparksBelow = 0.15f;
        [Tooltip("Seconds between spits of sparks, least and most.")] public Vector2 spitEvery = new Vector2(0.35f, 1.1f);
        public GameObject lightSmoke;
        public GameObject darkSmoke;
        public GameObject spitSparks;

        [Header("Hits")]
        [Tooltip("Sparks thrown from where a hit landed.")] public GameObject hitSparks;
        [Tooltip("Sparks for a big hit (bigHit or harder).")] public GameObject bigHitSparks;
        [Tooltip("A hit felt at least this hard (units per second INTO the surface at scale 1; damage starts at the car's crashMinSpeed, 14) is a big one: big sparks, the strong sound, maybe a part knocked off.")]
        public float bigHit = 26f;
        [Tooltip("Paint flecks a hit throws: on a tap, a crash, a big hit.")] public Vector3Int flecks = new Vector3Int(3, 8, 16);
        [Tooltip("Fleck size, world units on a car 4 long (bigger and smaller cars throw bigger and smaller flecks), least and most.")] public Vector2 fleckSize = new Vector2(0.05f, 0.12f);
        [Tooltip("Fleck colour for a car whose paint was not baked.")] public Color fleckFallback = new Color(0.85f, 0.3f, 0.2f);
        [Tooltip("The flecks' material: a URP particle material that takes the particle colour (the builder makes Data/DamageFleck). None = no flecks.")]
        public Material fleckMaterial;

        [Header("Loose parts")]
        [Range(0f, 1f), Tooltip("Chance a big hit knocks one of the car's own add-on parts (spoiler, bumper, lights, pipe, turbine) off.")]
        public float partChance = 0.6f;
        [Tooltip("Parts knocked off when the car is wrecked.")] public int wreckParts = 3;
        [Tooltip("Seconds a loose part lies about before it shrinks away.")] public float partLife = 3f;
        [Tooltip("Name pieces that mark a loose part (any case). Wheels and the body never come off.")]
        public string[] partNames = { "Spoiler", "Bumper", "FogLight", "Headlight", "Pipe", "Turbine" };

        [Header("Wreck")]
        public GameObject wreckSparks;
        public GameObject wreckSmoke;

        [Header("Wreck in pieces (D-0060; the cuts are made by Shift Jam > Workshop > Build Car Wrecks)")]
        [Tooltip("Cells the builder asks Fraggo to cut each car body into (a cell that misses the body makes no piece). Change it, then run Build Car Wrecks again.")]
        [Range(3, 24)] public int cutCells = 9;
        [Tooltip("Pieces of the body that stay on the car as its burnt hulk, lowest and most central first. 0 = the whole body flies apart.")]
        [Min(0)] public int hulkPieces = 2;
        [Tooltip("The hulk's colour (it multiplies the car's own paint).")] public Color hulkTint = new Color(0.22f, 0.2f, 0.19f);
        [Tooltip("How fast the body's pieces are thrown outward, units per second on a car 4 long, least and most.")]
        public Vector2 pieceBurst = new Vector2(12f, 20f);
        [Tooltip("How fast the wheels pop off, units per second on a car 4 long, least and most.")]
        public Vector2 wheelBurst = new Vector2(10f, 16f);
        [Range(0f, 1f), Tooltip("Share of the car's own speed its pieces keep.")] public float pieceCarry = 0.8f;
        [Range(0f, 1f), Tooltip("How much a popped wheel bounces.")] public float wheelBounce = 0.55f;
        [Tooltip("Seconds the pieces lie about if nothing mends the car sooner (the tow or a repair shrinks them away at once).")]
        public float pieceLife = 8f;
        [Tooltip("Most wreck pieces (body pieces and wheels) in the scene across every car. A wreck with no room left only hides its body.")]
        [Min(0)] public int mostPieces = 60;
        [Range(0f, 1f), Tooltip("The cut faces show the car's paint this much darker (1 = the paint itself).")] public float insideShade = 0.55f;
        [Tooltip("The cut faces' material (the builder makes Data/WreckInside). None = the body's own first material.")]
        public Material wreckInside;
        [Range(0f, 1f), Tooltip("Camera shake when a car breaks up right at the camera (0 = none).")] public float wreckShake = 0.9f;
        [Tooltip("The shake fades to nothing this many car lengths from the wreck.")] public float wreckShakeRange = 12f;
        [Tooltip("Car body mesh name to its cut pieces (written by the builder).")] public List<Cut> cuts = new List<Cut>();

        [Header("Sounds (picked at random, louder and lower the harder the hit)")]
        [Tooltip("A knock that does no harm (a cone, a glance, a soft bump).")] public AudioClip[] knock;
        [Tooltip("A crash that hurts.")] public AudioClip[] crash;
        [Tooltip("A big hit.")] public AudioClip[] bigCrash;
        [Tooltip("The wreck, played over a big crash.")] public AudioClip[] wreck;
        [Range(0f, 1f)] public float volume = 0.85f;

        [Header("Paint colours (baked by the builder)")]
        [Tooltip("Body mesh name to its paint colour, for the flecks.")] public List<Paint> paints = new List<Paint>();

        [Serializable]
        public struct Paint
        {
            public string mesh;
            public Color colour;
        }

        /// <summary>One car body cut into pieces: each piece's mesh (the body's own submeshes, then one more for the cut
        /// faces) and where its centre sits in the body mesh's own space.</summary>
        [Serializable]
        public sealed class Cut
        {
            public string mesh;
            [Tooltip("Submeshes the body mesh has; every piece has one more, the cut faces.")] public int subMeshes = 1;
            public Mesh[] pieces;
            public Vector3[] centres;
        }

        /// <summary>The cut pieces of the body mesh named <paramref name="mesh"/>, or null when it was never cut (or its
        /// piece meshes are not in this copy of the project: they are made from a licensed pack and stay out of git).</summary>
        public Cut CutOf(string mesh)
        {
            if (string.IsNullOrEmpty(mesh) || cuts == null) return null;
            foreach (var c in cuts)
                if (c != null && c.mesh == mesh && c.pieces != null && c.centres != null && c.pieces.Length > 0 && c.pieces.Length == c.centres.Length) return c;
            return null;
        }

        /// <summary>The baked paint colour of the body mesh named <paramref name="mesh"/>, or the fallback.</summary>
        public Color PaintOf(string mesh)
        {
            if (!string.IsNullOrEmpty(mesh))
                foreach (var p in paints) if (p.mesh == mesh) return p.colour;
            return fleckFallback;
        }

        /// <summary>Resources/CarDamageFx, or bare defaults (nothing shown or played) when there is no such asset.</summary>
        public static CarDamageFxSettings Load()
        {
            var settings = Resources.Load<CarDamageFxSettings>(ResourceName);
            if (settings == null)
            {
                settings = CreateInstance<CarDamageFxSettings>();
                settings.name = ResourceName + " (defaults)";
            }
            return settings;
        }
    }
}
