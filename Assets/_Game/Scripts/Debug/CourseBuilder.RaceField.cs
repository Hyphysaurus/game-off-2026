#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// The raceway's race with rivals, as the builder lays it (D-0061). While the circuit is laid, every piece adds the
    /// middle of its road to a list; from that this bakes the RacePath (evenly spaced points with the road's up, its
    /// half width and a suggested speed worked out from how tight the road bends, how it leans and how it humps),
    /// stands the four grid cars behind the finish line, makes the Grid object (RaceGrid, RaceStartKey, RaceRescue) and
    /// wires it, and makes Resources/Rivals once (his tuning in it is never overwritten). It checks what it made: every
    /// path point lies on the road, every slot is on the road behind the line.
    /// </summary>
    public static partial class CourseBuilder
    {
        const string RivalsAsset = "Assets/_Game/Resources/" + RivalSettings.ResourceName + ".asset";

        const float RwPathStep = 2f;        // the baked path's spacing: half a car's length
        const float RwPathRawStep = 1f;     // how finely a straight is read before the even spacing is cut
        const int RwPathRawCuts = 32;       // how finely each swept piece is read (a 45 degree piece is up to 47 long)
        const int RwPathSmooth = 2;         // points each side a bend's tightness is averaged over (the pieces meet at joints)
        const float RwPathGrip = 18f;       // sideways pull a bend's suggested speed allows, units per second squared: 18 gives 29 on the tighter hairpin before its lean, top speed is 32
        const float RwPathBank = 1f;        // how much a lean into the bend adds to that, times the sine of the lean (20 degrees: a third more)
        const float RwPathHump = 60f;       // the same for a dip (the road curving up toward the car): 60 gives 41 round the loop (nitro reaches 48: none in the loop) and 49 at the foot of the dive
        const float RwPathCrest = 30f;      // and for a crest (the road falling away): 30 gives 35 over the top of the dive. At 40 and more a rival flew 4 high off it and landed against a wall (seen in play, D-0061); at 35 the wheels stay down
        const float RwPathFastest = 60f;    // what a straight suggests: more than any car reaches

        /// <summary>The grid: the car piece, its name on the results card (its colour), how far along the first straight
        /// it stands and how far right of the middle. Pole first. The finish line is 25 along; the steps arrive at 12 on
        /// the right edge. Every car has a lane of its own, 4 apart (a car is 2.2 wide), each a little behind the last:
        /// nobody starts behind another car, so a car left standing on the grid is in nobody's way at the start (two
        /// rows of two put a rival 4.6 from the bumper ahead, too close to steer round from a standstill: tried).</summary>
        static readonly (string piece, string name, float along, float across)[] RwGrid =
        {
            ("CyberCar14", "BLUE", 18f, -2f),
            ("CyberCar05", "YELLOW", 15f, 2f),
            ("CyberCar09", "GREEN", 12f, -6f),
            ("CyberCar01", "PINK", 9f, 6f),
        };

        /// <summary>Adds a piece's middle line to the layer's list: <paramref name="cuts"/> points from its start up to,
        /// not including, its end (the next piece starts there).</summary>
        static void RwLine(RwLayer layer, int cuts, System.Func<float, (Vector3 at, Quaternion turn)> along)
        {
            for (int k = 0; k < cuts; k++) layer.Line.Add(along((float)k / cuts));
        }

        /// <summary>Stands the grid's cars on the first straight (the cursor is at the circuit's start).</summary>
        static void RwGridCars(RwLayer layer, Transform cars)
        {
            Vector3 fwd = layer.Cursor.Turn * Vector3.forward, right = layer.Cursor.Turn * Vector3.right;
            for (int i = 0; i < RwGrid.Length; i++)
            {
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(CarPieces + RwGrid[i].piece + ".prefab");
                if (asset == null) { Log.AppendLine("  NO CAR " + RwGrid[i].piece + " (run Shift Jam > Workshop > Build Combat & Vehicle Pieces)"); continue; }
                var car = (GameObject)PrefabUtility.InstantiatePrefab(asset, cars); car.name = "Racer" + (i + 1);
                Vector3 at = layer.Cursor.Pos + fwd * RwGrid[i].along + right * RwGrid[i].across + Vector3.up * 0.02f;
                car.transform.SetPositionAndRotation(at, layer.Cursor.Turn);
                layer.GridCars.Add((car.GetComponent<ArcadeCar>(), RwGrid[i].name, at, layer.Cursor.Turn));
            }
        }

        /// <summary>Bakes the path from the laid circuit and makes the grid that races on it.</summary>
        static void RwRace(RwLayer layer, Transform root, Transform track, Transform props, Transform smash)
        {
            // ---- the path: the laid middle line, cut to an even spacing ----
            var raw = layer.Line; int n = raw.Count;
            if (n < 16) { Log.AppendLine("  RACE PATH NOT BAKED: the circuit gave no middle line"); return; }
            var run = new float[n + 1];
            for (int i = 0; i < n; i++) run[i + 1] = run[i] + (raw[(i + 1) % n].at - raw[i].at).magnitude;
            float lap = run[n];
            int count = Mathf.Max(8, Mathf.RoundToInt(lap / RwPathStep)); float step = lap / count;
            var points = new Vector3[count]; var ups = new Vector3[count]; var halves = new float[count]; var speeds = new float[count];
            for (int k = 0, j = 0; k < count; k++)
            {
                float d = k * step;
                while (j < n - 1 && run[j + 1] <= d) j++;
                float t = Mathf.Clamp01((d - run[j]) / Mathf.Max(1e-5f, run[j + 1] - run[j]));
                var a = raw[j]; var b = raw[(j + 1) % n];
                points[k] = Vector3.Lerp(a.at, b.at, t);
                ups[k] = (Quaternion.Slerp(a.turn, b.turn, t) * Vector3.up).normalized;
                halves[k] = layer.RoadHalf;
            }
            // how the road bends at each point: sideways (a corner) and along its own up (a hump or a dip)
            var side = new float[count]; var hump = new float[count]; var lean = new float[count];
            for (int i = 0; i < count; i++)
            {
                Vector3 before = points[(i - 1 + count) % count], after = points[(i + 1) % count];
                Vector3 bend = ((after - points[i]).normalized - (points[i] - before).normalized) / step;
                Vector3 right = Vector3.Cross(ups[i], (after - before).normalized);
                side[i] = Vector3.Dot(bend, right); hump[i] = Vector3.Dot(bend, ups[i]);
                lean[i] = right.y;   // the right edge higher than the left: a lean to the left
            }
            float slowest = float.MaxValue; int slowAt = 0, under = 0;
            for (int i = 0; i < count; i++)
            {
                float s = 0f, h = 0f, l = 0f; int m = 2 * RwPathSmooth + 1;
                for (int k = -RwPathSmooth; k <= RwPathSmooth; k++) { int q = ((i + k) % count + count) % count; s += side[q]; h += hump[q]; l += lean[q]; }
                s /= m; h /= m; l /= m;
                float into = Mathf.Max(0f, -Mathf.Sign(s) * l);   // leaning into the bend (a right bend wants the left edge up)
                float v = RwPathFastest;
                if (Mathf.Abs(s) > 1e-4f) v = Mathf.Min(v, Mathf.Sqrt(RwPathGrip * (1f + RwPathBank * into) / Mathf.Abs(s)));
                if (Mathf.Abs(h) > 1e-4f) v = Mathf.Min(v, Mathf.Sqrt((h < 0f ? RwPathCrest : RwPathHump) / Mathf.Abs(h)));   // negative: the road falls away
                speeds[i] = v;
                if (v < slowest) { slowest = v; slowAt = i; }
                if (v < 32f) under++;
            }
            float line = layer.FinishAt >= 0 ? run[Mathf.Min(layer.FinishAt, n)] % lap : 0f;
            var path = Child(root, "RacePath").gameObject.AddComponent<RacePath>();
            path.Bake(points, ups, halves, speeds, step, line);

            // is every point on the road, the right way up
            Physics.SyncTransforms();
            int off = 0;
            for (int i = 0; i < count; i++)
            {
                bool found = false;
                foreach (var h in Physics.RaycastAll(points[i] + ups[i] * 1.5f, -ups[i], 3f, ~0, QueryTriggerInteraction.Ignore))
                    if (h.collider.transform.IsChildOf(track) && Mathf.Abs(h.distance - 1.5f) <= 0.15f) { found = true; break; }
                if (!found) off++;
            }
            Log.AppendLine($"  race path: {count} points {step:F3} apart, {lap:F1} a lap, the line at {line:F1}; {off} points off the road; suggested speed {slowest:F1} at its slowest (point {slowAt}, {slowAt * step:F0} along), under 32 on {under} points");

            // ---- the grid ----
            var finish = props.GetComponentInChildren<RaceLoop>();
            if (finish == null) Log.AppendLine("  RACE GRID: no finish line (RaceLoop) under Props: the grid cannot start a race");
            var gridGo = Child(root, "Grid").gameObject;
            var grid = gridGo.AddComponent<RaceGrid>();
            gridGo.AddComponent<RaceStartKey>();
            gridGo.AddComponent<RaceRescue>();
            var carList = new List<Object>(); var slotList = new List<Object>(); var nameList = new List<string>();
            int bad = 0;
            foreach (var g in layer.GridCars)
            {
                if (g.car == null) { bad++; continue; }
                var slot = Child(gridGo.transform, "Slot" + (slotList.Count + 1));
                slot.SetPositionAndRotation(g.at, g.turn);
                carList.Add(g.car); slotList.Add(slot); nameList.Add(g.name);
                // on the road, between the walls, behind the line
                float where = path.Track(g.at, path.Nearest(g.at), out _, out float lateral, out float height);
                float behind = path.Delta(where, line);
                if (Mathf.Abs(height) > 0.2f || Mathf.Abs(lateral) > layer.RoadHalf - 1.5f || behind < 4f || behind > 40f)
                { bad++; Log.AppendLine($"  GRID SLOT {slotList.Count} is not on the road behind the line: {lateral:F1} off the middle, {height:F2} above the road, {behind:F1} before the line"); }
            }
            Set(grid, "path", path);
            Set(grid, "finish", finish);
            Set(grid, "putBack", smash);
            SetArray(grid, "cars", carList.ToArray());
            SetArray(grid, "slots", slotList.ToArray());
            var so = new SerializedObject(grid); var names = so.FindProperty("names");
            names.arraySize = nameList.Count;
            for (int i = 0; i < nameList.Count; i++) names.GetArrayElementAtIndex(i).stringValue = nameList[i];
            so.ApplyModifiedPropertiesWithoutUndo();
            Log.AppendLine($"  race grid: {carList.Count} cars ({string.Join(", ", nameList)}), {bad} bad slots; finish line {(finish != null ? "found, " + finish.Laps + " laps" : "MISSING")}");

            // ---- the numbers: made once, never overwritten ----
            if (AssetDatabase.LoadAssetAtPath<RivalSettings>(RivalsAsset) == null)
            {
                AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<RivalSettings>(), RivalsAsset);
                Log.AppendLine("  made " + RivalsAsset + " (the rivals' numbers; tune it there)");
            }
        }
    }
}
#endif
