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
