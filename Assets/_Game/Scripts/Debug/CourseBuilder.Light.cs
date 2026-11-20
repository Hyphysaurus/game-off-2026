#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace ShiftJam
{
    /// <summary>
    /// The course's room light, as a starting point for his eye (D-0089): the bedroom warm, the skirting lane cold and dim
    /// with its warm lantern half-way, the wardrobe dark with its night lamp and an ember on the player, the playroom
    /// bright, the window's garden sunlit. Plain values on the lights each room already has, set in place (running it
    /// again changes nothing). LandSlice calls it.
    /// - Why the rooms read flat: every room sits in one scene under one ambient and one sky reflection. The ambient was
    ///   the default sky at full strength and the plain course materials (smoothness 0.5) mirrored that bright sky, so every
    ///   face looked the same from all sides; and URP's point lights fall off with the square of the distance, so the
    ///   room lamps as built (intensity 2 to 5, 12 to 95 above the floor) added almost nothing at this world's size (a kid
    ///   is 63 tall). Now: a lower three-colour ambient, the sky's reflection at a fifth, and each lamp set by the light it
    ///   should give where it lands (intensity = wanted light x distance squared: 1.0 on a floor 78 below is about 6000).
    /// </summary>
    public static partial class CourseBuilder
    {
        // The whole scene: a lower fill, a touch warmer from above than from below; the sky's reflection at a fifth.
        static readonly Color AmbientSky = new Color(0.30f, 0.30f, 0.34f), AmbientEquator = new Color(0.24f, 0.22f, 0.20f), AmbientGround = new Color(0.12f, 0.10f, 0.09f);
        const float SkyReflection = 0.2f, SunIntensity = 0.15f;
        // R1 / R2 the bedroom: warm. The ceiling lamp 78 up: about 1.0 on the floor under it.
        static readonly Color BedroomWarm = new Color(1f, 0.85f, 0.66f); const float BedroomIntensity = 6000f;
        // R3 the skirting lane: cold and dim (lamps 12 over the floor, about 0.3 under each, dark between), one warm
        // lantern half-way (about 1.2 on the floor round it), and the warm glow at the far end.
        static readonly Color LaneCold = new Color(0.6f, 0.72f, 1f); const float LaneIntensity = 45f;
        static readonly Color LanternWarm = new Color(1f, 0.7f, 0.4f); const float LanternIntensity = 80f, LanternRange = 40f;
        static readonly Color ExitWarm = new Color(1f, 0.85f, 0.55f); const float ExitIntensity = 30f;
        // R4 the playroom: bright, near white. The lamp 95 up (about 1.2 under it), the two fills about 0.3 where they land.
        static readonly Color PlayroomBright = new Color(1f, 0.96f, 0.9f); const float PlayroomIntensity = 10800f, PlayroomFill = 5000f;
        // R5 the wardrobe: dark; the night lamp 10 over shelf 2 (a warm pool there, a glimmer on the floor 90 below) and
        // the player's ember.
        static readonly Color NightLampWarm = new Color(1f, 0.7f, 0.42f); const float NightLampIntensity = 200f, NightLampRange = 120f;
        static readonly Color EmberWarm = new Color(1f, 0.74f, 0.45f); const float EmberIntensity = 0.8f, EmberRange = 6f, EmberUp = 1.3f;
        const float TorchBeamIntensity = 3000f;   // about 0.3 on core C, 95 along the beam
        // R6 the window: the garden in sun (the sun 280 from the window: about 0.75 there, more on the garden).
        static readonly Color GardenSun = new Color(1f, 0.93f, 0.78f); const float GardenIntensity = 60000f;

        static Light L(string path) { var g = GameObject.Find(path); return g != null ? g.GetComponent<Light>() : null; }
        static void Tune(Light l, Color c, float intensity, float range = -1f, LightShadows? shadows = null)
        {
            if (l == null) return;
            l.color = c; l.intensity = intensity; if (range > 0f) l.range = range; if (shadows.HasValue) l.shadows = shadows.Value;
            EditorUtility.SetDirty(l);
        }

        static void LightRooms()
        {
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = AmbientSky; RenderSettings.ambientEquatorColor = AmbientEquator; RenderSettings.ambientGroundColor = AmbientGround;
            RenderSettings.reflectionIntensity = SkyReflection;
            var sun = L("Directional Light"); if (sun != null) { sun.intensity = SunIntensity; EditorUtility.SetDirty(sun); }
            Log.AppendLine($"light: ambient trilight {AmbientSky} / {AmbientEquator} / {AmbientGround} (was the default sky at 1); sky reflection {SkyReflection} (was 1); sun {SunIntensity}");

            var bed = L("Room/Bedroom/RoomLight"); Tune(bed, BedroomWarm, BedroomIntensity);
            Log.AppendLine($"light R2: bedroom lamp {(bed != null ? "warm " + BedroomIntensity : "NOT FOUND")}");

            var r3 = GameObject.Find("Room/Course/R3_SkirtingRun");
            if (r3 != null)
            {
                var lane = r3.GetComponentsInChildren<Light>(true).Where(l => l.name == "LaneLight" || l.name == "LanternLight").OrderBy(l => l.transform.position.x).ToList();
                var lantern = r3.transform.Find("Lantern");
                // the lane light nearest the lantern (BuildLane's at x 170) becomes the lantern's own light, sat on its top;
                // the others stay on their spots along the ceiling (x 0, 90, 250), cold
                Light half = lane.FirstOrDefault(l => l.name == "LanternLight");
                if (half == null && lantern != null) half = lane.OrderBy(l => Mathf.Abs(l.transform.position.x - lantern.position.x)).FirstOrDefault();
                var others = lane.Where(l => l != half).ToList();
                float[] spots = { 0f, 90f, 250f };
                for (int i = 0; i < others.Count; i++)
                {
                    if (i < spots.Length) others[i].transform.position = new Vector3(spots[i], 12f, LaneZ);
                    others[i].name = "LaneLight";
                    Tune(others[i], LaneCold, LaneIntensity, 45f, LightShadows.None);
                    EditorUtility.SetDirty(others[i].transform);
                }
                if (half != null)
                {
                    var lb = B(lantern.gameObject);
                    half.transform.position = new Vector3(lb.center.x, lb.max.y + 0.5f, lb.center.z);
                    half.name = "LanternLight";
                    Tune(half, LanternWarm, LanternIntensity, LanternRange, LightShadows.Soft);
                    EditorUtility.SetDirty(half.transform);
                }
                var exit = r3.transform.Find("ExitLight"); if (exit != null) Tune(exit.GetComponent<Light>(), ExitWarm, ExitIntensity);
                Log.AppendLine($"light R3: {others.Count} lane lights cold {LaneIntensity} at x {string.Join(", ", others.Select(l => l.transform.position.x.ToString("F0")))}; lantern light {(half != null ? "at " + half.transform.position : "NOT FOUND")}");
            }
            else Log.AppendLine("light R3: NO R3_SkirtingRun");

            Tune(L("Room/Course/R4_Playroom/RoomLight"), PlayroomBright, PlayroomIntensity);
            Tune(L("Room/Course/R4_Playroom/FillNW"), PlayroomBright, PlayroomFill);
            Tune(L("Room/Course/R4_Playroom/FillSE"), PlayroomBright, PlayroomFill);
            Log.AppendLine($"light R4: playroom lamp {PlayroomIntensity}, fills {PlayroomFill}");

            var night = L("Room/Course/R5_Wardrobe/DimLamp"); Tune(night, NightLampWarm, NightLampIntensity, NightLampRange, LightShadows.Soft);
            Log.AppendLine($"light R5: night lamp {(night != null ? NightLampIntensity + " at " + night.transform.position : "NOT FOUND")}");
            // the wardrobe torch's beam: built white at 1 with no shadows (its settings sat inside a comment, D-0081), now as the
            // toy box torch's warm beam. The Flashlight switches it on and off; the sensor reads only its range and cone.
            var beam = L("Room/Course/R5_Wardrobe/WardrobeTorch/Beam"); Tune(beam, new Color(1f, 0.9f, 0.745f), TorchBeamIntensity, -1f, LightShadows.Soft);
            Log.AppendLine($"light R5: torch beam {(beam != null ? TorchBeamIntensity + ", soft shadows, range " + beam.range : "NOT FOUND")}");

            var motor = Object.FindFirstObjectByType<PlayerMotor>(FindObjectsInactive.Include);
            if (motor != null)
            {
                var t = motor.transform.Find("Ember");
                if (t == null) { t = new GameObject("Ember").transform; t.SetParent(motor.transform, false); }
                t.localPosition = Vector3.up * EmberUp;
                var e = t.GetComponent<Light>(); if (e == null) e = t.gameObject.AddComponent<Light>();
                e.type = LightType.Point; e.shadows = LightShadows.None;
                Tune(e, EmberWarm, EmberIntensity, EmberRange);
                EditorUtility.SetDirty(t);
                Log.AppendLine($"light: the player's ember (range {EmberRange} at toy size: it only shows in the dark rooms)");
            }
            else Log.AppendLine("light: NO PlayerMotor for the ember");

            Tune(L("Room/Course/R6_Window/GardenSun"), GardenSun, GardenIntensity);
            Log.AppendLine($"light R6: garden sun {GardenIntensity}");
        }
    }
}
#endif
