#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace ShiftJam
{
    /// <summary>
    /// Shift Jam / Workshop / Build Boost Pieces (D-0037): the boost set's track pieces as Build Mode prefabs, made from the
    /// licensed packs by asset path (never copied), sized for a toy's car (3.8 long at toy scale), pivot at the bottom
    /// centre, +Z the way cars go. The packs' own colliders and animation scripts are switched off; our colliders go on.
    /// Pieces/Traversal: BoostArrow (three Cyber floor chevrons on a dark plate; the kid can grab and place it), BoostArch
    /// (the inflatable gate), BoostRing (the inflatable hoop, its lower rim sunk in the floor so cars drive through),
    /// JumpPad (the Cyber ramp), BounceTramp (the round trampoline), FanPad (the propeller pad, sunk to its blades).
    /// Pieces/Interactables: NitroStar, RepairHeart and ShieldRing (CarPickup, D-0038). Re-running overwrites the same
    /// prefabs (GUIDs kept) and keeps the materials it makes once (BoostPlate, NitroGlow, RepairGlow, ShieldGlow,
    /// ShieldBubble, the star colours in Data/Stars). Run Build Catalog afterwards so Build Mode lists them.
    /// The nitro star is his own model (Art/Star/NitroStar.fbx, D-0068) and has a menu of its own, Build Nitro Star,
    /// for a change of colour without rebuilding the other eleven.
    /// </summary>
    public static class RacingPieces
    {
        private const string Traversal = "Assets/_Game/Prefabs/Pieces/Traversal";
        private const string Interactables = "Assets/_Game/Prefabs/Pieces/Interactables";
        private const string Data = "Assets/_Game/Data";
        private const string BS = "Assets/_Imports/ithappy/Battle_Stadium/Prefabs/";
        private const string CY = "Assets/_Imports/ithappy/Modular_Cyber_Racing_Cars/Prefabs/Track Props/";
        private const string ToySounds = "Assets/_Imports/ithappy/Playroom/Playroom_Sound_FX/Toys/";
        private const string PlayroomSounds = "Assets/_Imports/ithappy/Playroom/Playroom_Sound_FX/";
        private const float CarLength = 3.8f;   // a toy's car at toy scale (WorkshopPieces sizes every Cyber car to it)
        private const float CarMiddle = 1.05f;  // height of a toy car's body middle above the floor
        private static readonly Color BoostColour = new Color(0.35f, 0.9f, 1f);
        private const string StarModel = "Assets/_Game/Art/Star/NitroStar.fbx";   // his star, modelled in Blender (D-0068)
        private const string StarLooks = "Assets/_Game/Data/Stars";               // one glowing material a colour, made once
        private const string StarFirstLook = "StarOrange";                        // on the star until he drags another on
        private const float StarGlow = 0.5f;                                      // emission = the glow colour times this, when made (1.2 washed cyan to white)
        // His seven from star.blend, by their hex: the colour of the star, and the colour it glows (Bold glows gold on orange).
        private static readonly (string file, Color colour, Color glow)[] StarColours =
        {
            ("StarGold", new Color32(0xF2, 0xC8, 0x4B, 255), new Color32(0xF2, 0xC8, 0x4B, 255)),
            ("StarOrange", new Color32(0xFF, 0x7A, 0x1A, 255), new Color32(0xFF, 0x7A, 0x1A, 255)),
            ("StarPink", new Color32(0xFF, 0x4F, 0xA3, 255), new Color32(0xFF, 0x4F, 0xA3, 255)),
            ("StarLime", new Color32(0xB6, 0xFF, 0x3C, 255), new Color32(0xB6, 0xFF, 0x3C, 255)),
            ("StarCyan", new Color32(0x9E, 0xE7, 0xFF, 255), new Color32(0x9E, 0xE7, 0xFF, 255)),
            ("StarViolet", new Color32(0x9B, 0x5C, 0xFF, 255), new Color32(0x9B, 0x5C, 0xFF, 255)),
            ("StarBold", new Color32(0xFF, 0x7A, 0x1A, 255), new Color32(0xFF, 0xC7, 0x40, 255)),
        };

        [MenuItem("Shift Jam/Workshop/Build Boost Pieces")]
        public static void Build()
        {
            EnsureFolder(Traversal);
            EnsureFolder(Interactables);
            AudioClip whoosh = null;   // the car plays its own whoosh (boostClip) when it takes a burst: a pad sound would double it
            var boing = Clip("Spring_Boing.wav");
            var chime = Clip("Star_Light_Activate.wav");
            var made = new List<string>();
            if (BuildBoostArrow(whoosh)) made.Add("BoostArrow");
            if (BuildBoostArch(whoosh)) made.Add("BoostArch");
            if (BuildBoostRing(whoosh)) made.Add("BoostRing");
            if (BuildJumpPad(boing)) made.Add("JumpPad");
            if (BuildBounceTramp(boing)) made.Add("BounceTramp");
            if (BuildFanPad(boing)) made.Add("FanPad");
            if (BuildNitroStar(chime)) made.Add("NitroStar");
            if (BuildRepairHeart()) made.Add("RepairHeart");
            if (BuildShieldRing()) made.Add("ShieldRing");
            made.AddRange(BuildCarPortals());
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[RacingPieces] Built {made.Count} of 12: {string.Join(", ", made)}. Run Shift Jam > Workshop > Build Catalog so Build Mode lists them.");
        }

        /// <summary>The nitro star alone (D-0068): after dragging another Data/Stars material onto the star in
        /// NitroStar.prefab, this rebuilds it in that colour and turns the floor glow to match. Same prefab, GUID kept.</summary>
        [MenuItem("Shift Jam/Workshop/Build Nitro Star")]
        public static void BuildStar()
        {
            EnsureFolder(Interactables);
            bool made = BuildNitroStar(Clip("Star_Light_Activate.wav"));
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log(made ? "[RacingPieces] Built NitroStar." : "[RacingPieces] NitroStar NOT built: no model (see the warnings above).");
        }

        // ---------- car portals (D-0039; the look is his drawn portal, D-0073) ----------

        private const float PortalHole = 7f;   // the opening, across: the wooden car (4.2 wide) with room
        private const string PortalModel = "Assets/_Game/Art/Portals/PortalRing.fbx";   // his portal drawing as a model (portal_ring.blend, D-0073)
        private const string PortalShader = "Assets/_Game/Shaders/PortalStroke.shader";
        private const float PortalDrawnHole = 1.9f;   // the drawn opening across, in the model's metres: the inner edge of his strokes
        private const float PortalTurn = -40f;        // the swirl's turn when its material is made, degrees a second (below 0: the arms trail)
        private const string PortalSwirlMesh = "Swirl", PortalCentreMesh = "Void", PortalMarksMesh = "Marks_";   // the model's mesh names
        private static readonly (PortalColour colour, Color tint)[] PortalColours =
        {
            (PortalColour.Red, new Color(1f, 0.32f, 0.3f)),
            (PortalColour.Blue, new Color(0.3f, 0.62f, 1f)),
            (PortalColour.Green, new Color(0.35f, 1f, 0.45f)),
        };

        [MenuItem("Shift Jam/Workshop/Build Car Portals")]
        public static void BuildCarPortalsMenu()
        {
            EnsureFolder(Traversal);
            var made = BuildCarPortals();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[RacingPieces] Car portals: {string.Join(", ", made)}. Run Build Catalog so Build Mode lists them.");
        }

        /// <summary>
        /// CarPortal_Red / _Blue / _Green: his portal drawing standing up (D-0073), sized by its OPENING (7 across, so the
        /// wooden car fits), the opening's lowest point just under the floor (a RestSink keeps it there in Build Mode) so
        /// cars drive straight through along +Z. The swirl turns; a warp flashes it and sends the drawing's small marks
        /// outward. A soft glow in the pair's colour lies on the floor under it and the flash is that colour, so the pairs
        /// read at a glance (the drawing itself is the same on all three). Without his model the Cyber pack's hoop with
        /// his flat swirl inside stands in, as before D-0073.
        /// </summary>
        private static List<string> BuildCarPortals()
        {
            var made = new List<string>();
            var drawn = AssetDatabase.LoadAssetAtPath<GameObject>(PortalModel);
            Material swirlLook = drawn != null ? PortalStrokeMaterial("PortalSwirl", PortalTurn) : null;
            Material marksLook = drawn != null ? PortalStrokeMaterial("PortalMarks", 0f) : null;
            bool hisDrawing = drawn != null && swirlLook != null && marksLook != null;
            if (drawn == null) Debug.LogWarning("[RacingPieces] No " + PortalModel + ": the pack's hoop stands in.");
            else if (!hisDrawing) Debug.LogWarning("[RacingPieces] No " + PortalShader + ": the pack's hoop stands in.");
            var hoop = hisDrawing ? null : Load(CY + "Circle_01.prefab");
            if (!hisDrawing && hoop == null) return made;
            var swirlTex = hisDrawing ? null : AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/_Game/Art/Portals/warp_mask_swirl.png");
            if (!hisDrawing && swirlTex == null) Debug.LogWarning("[RacingPieces] No warp_mask_swirl.png: the portals get a plain glow.");
            var sound = ClipAt("Toys/Star_Light_On.wav");
            float middle = PortalHole * 0.5f - 0.4f;   // the opening's lowest point 0.4 under the floor: a car rolls straight in
            foreach (var (colour, tint) in PortalColours)
            {
                string name = "CarPortal_" + colour;
                var root = new GameObject(name);
                Transform spins = null; PortalRipple ripple = null; float depth; List<Object> glow;
                if (hisDrawing)
                {
                    if (!DrawnPortal(root.transform, drawn, swirlLook, marksLook, middle, out glow, out ripple, out depth))
                    {
                        Debug.LogError($"[RacingPieces] {PortalModel} has no '{PortalSwirlMesh}' mesh: {name} NOT built.");
                        Object.DestroyImmediate(root);
                        continue;
                    }
                }
                else HoopPortal(root.transform, hoop, colour, tint, swirlTex, middle, out spins, out glow, out depth);
                Quad("FloorGlow", root.transform, new Vector3(0f, 0.05f, 0f), PortalHole * 1.3f, Glow("CarPortalGlow_" + colour, new Color(tint.r, tint.g, tint.b, 0.7f)));

                var trigger = Trigger(root.transform, "Portal", new Vector3(0f, middle, 0f), new Vector3(PortalHole * 0.9f, PortalHole * 0.9f, Mathf.Max(2.5f, depth * 2f)));
                var portal = trigger.AddComponent<CarPortal>();
                Set(portal, "colour", (int)colour);
                Set(portal, "swirl", spins);
                Set(portal, "ripple", ripple);
                SetArray(portal, "glow", glow);
                var light = FlashLight(root.transform, new Vector3(0f, middle, 0f), PortalHole * 2f);
                light.color = tint;
                Set(portal, "flashLight", light);
                Set(portal, "sound", sound);
                Save(root, Traversal + "/" + name + ".prefab");
                made.Add(name);
            }
            return made;
        }

        /// <summary>
        /// His drawing as the portal (D-0073). The model's meshes go on our own objects (never a copy of the model): Swirl
        /// (his strokes and the centre arms) and Centre (the dark disc) under "Swirl", and the rings of small marks under
        /// "Ripple", switched off until a warp. The swirl turns in its material (Data/PortalSwirl, Spin speed), not by
        /// its transform, so what Build Mode measures never moves. False when the model has no swirl mesh.
        /// </summary>
        private static bool DrawnPortal(Transform root, GameObject model, Material swirlLook, Material marksLook, float middle,
            out List<Object> glow, out PortalRipple ripple, out float depth)
        {
            glow = new List<Object>(); ripple = null; depth = 0f;
            var look = Child(root, "Model");
            look.localPosition = new Vector3(0f, middle, 0f);
            look.localScale = Vector3.one * (PortalHole / PortalDrawnHole);
            var swirl = Child(look, "Swirl");
            var rings = new List<Object>();
            Transform marks = null;
            bool any = false;
            Matrix4x4 toModel = model.transform.worldToLocalMatrix;
            var parts = new List<MeshFilter>(model.GetComponentsInChildren<MeshFilter>(true));
            parts.Sort((a, b) => string.CompareOrdinal(a.name, b.name));   // Marks_1, Marks_2, Marks_3: inside ring first
            foreach (var part in parts)
            {
                if (part.sharedMesh == null) continue;
                bool isSwirl = part.name == PortalSwirlMesh, isCentre = part.name == PortalCentreMesh, isMarks = part.name.StartsWith(PortalMarksMesh);
                if (!isSwirl && !isCentre && !isMarks) continue;
                if (isMarks && marks == null) marks = Child(look, "Ripple");
                var go = Child(isMarks ? marks : swirl, isSwirl ? "Strokes" : isCentre ? "Centre" : "Ring_" + (rings.Count + 1));
                Matrix4x4 pose = toModel * part.transform.localToWorldMatrix;   // the mesh's place in the model, kept
                go.localPosition = pose.GetColumn(3);
                go.localRotation = pose.rotation;
                go.localScale = pose.lossyScale;
                go.gameObject.AddComponent<MeshFilter>().sharedMesh = part.sharedMesh;
                var r = go.gameObject.AddComponent<MeshRenderer>();
                r.sharedMaterial = isMarks ? marksLook : swirlLook;
                r.shadowCastingMode = ShadowCastingMode.Off;   // a glow in the air: no dark ring on the road
                r.receiveShadows = false;
                if (isMarks) { rings.Add(r); go.gameObject.SetActive(false); }
                else glow.Add(r);
                any |= isSwirl;
            }
            if (!any) return false;
            if (marks != null)
            {
                ripple = marks.gameObject.AddComponent<PortalRipple>();
                SetArray(ripple, "rings", rings);
            }
            Bounds b = LocalBounds(root, swirl.gameObject);   // the swirl alone: the marks are off between ripples
            depth = b.size.z;
            root.gameObject.AddComponent<RestSink>().Set(Mathf.Max(0f, -b.min.y));
            return true;
        }

        /// <summary>The stand-in from before D-0073: the Cyber pack's Circle_01 hoop standing up, sized by its opening,
        /// solid, with his flat swirl drawing (warp_mask_swirl) in the portal's colour spinning inside it.</summary>
        private static void HoopPortal(Transform root, GameObject model, PortalColour colour, Color tint, Texture2D swirlTex, float middle,
            out Transform swirl, out List<Object> glow, out float depth)
        {
            var holder = Child(root, "Model");
            var inst = Place(model, holder);

            // stand it up with its opening along Z: the thinnest side of the hoop is its depth
            Bounds b = LocalBounds(holder, inst);
            if (b.size.y <= b.size.x && b.size.y <= b.size.z) inst.transform.localRotation = Quaternion.Euler(90f, 0f, 0f) * inst.transform.localRotation;
            else if (b.size.x <= b.size.y && b.size.x <= b.size.z) inst.transform.localRotation = Quaternion.Euler(0f, 90f, 0f) * inst.transform.localRotation;
            b = LocalBounds(holder, inst);
            inst.transform.localPosition -= b.center;
            float hole = HoleAcross(inst, holder);
            if (hole < 0.1f) hole = Mathf.Min(b.size.x, b.size.y) * 0.7f;
            inst.transform.localScale *= PortalHole / hole;
            b = LocalBounds(holder, inst);
            inst.transform.localPosition -= b.center;
            inst.transform.localPosition += Vector3.up * middle;
            b = LocalBounds(holder, inst);
            depth = b.size.z;
            root.gameObject.AddComponent<RestSink>().Set(Mathf.Max(0f, -b.min.y));
            Solid(root, inst, false, null);

            // the swirl: two quads back to back (the glow shader draws one side), filling the opening
            swirl = Child(root, "Swirl");
            swirl.localPosition = new Vector3(0f, middle, 0f);
            var swirlMat = PortalSwirlMaterial(colour, tint, swirlTex);
            var front = Quad("Front", swirl, Vector3.zero, PortalHole * 0.98f, swirlMat);
            front.transform.localRotation = Quaternion.identity;
            var back = Quad("Back", swirl, Vector3.zero, PortalHole * 0.98f, swirlMat);
            back.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            glow = new List<Object> { front.GetComponent<Renderer>(), back.GetComponent<Renderer>() };
        }

        /// <summary>The hoop's opening across, in <paramref name="space"/>: twice the nearest any vertex comes to the middle,
        /// measured in the hoop's plane (x, y).</summary>
        private static float HoleAcross(GameObject inst, Transform space)
        {
            Bounds b = LocalBounds(space, inst);
            float nearest = float.MaxValue;
            foreach (var mf in inst.GetComponentsInChildren<MeshFilter>())
            {
                if (mf.sharedMesh == null) continue;
                foreach (var v in mf.sharedMesh.vertices)
                {
                    Vector3 p = space.InverseTransformPoint(mf.transform.TransformPoint(v)) - b.center;
                    nearest = Mathf.Min(nearest, new Vector2(p.x, p.y).magnitude);
                }
            }
            return nearest < float.MaxValue ? nearest * 2f : 0f;
        }

        /// <summary>A material for his drawn portal (ShiftJam/PortalStroke: the colours are in the model). Made once and
        /// then his to tune: Data/PortalSwirl turns (Spin speed), Data/PortalMarks does not.</summary>
        private static Material PortalStrokeMaterial(string file, float turn)
        {
            string path = Data + "/" + file + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null) return mat;
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(PortalShader);
            if (shader == null) return null;
            mat = new Material(shader) { name = file };
            mat.SetFloat("_SpinSpeed", turn);
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        /// <summary>His swirl drawing as an additive glow in a portal's colour: a copy of OrbGlow.mat. Made once.
        /// The stand-in hoop's (HoopPortal).</summary>
        private static Material PortalSwirlMaterial(PortalColour colour, Color tint, Texture2D swirl)
        {
            string path = Data + "/CarPortalSwirl_" + colour + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null) return mat;
            var orb = AssetDatabase.LoadAssetAtPath<Material>(Data + "/OrbGlow.mat");
            if (orb == null) { Debug.LogWarning("[RacingPieces] No OrbGlow.mat to copy for the portal swirl."); return null; }
            mat = new Material(orb) { name = "CarPortalSwirl_" + colour };
            if (swirl != null && mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", swirl);
            mat.SetColor("_BaseColor", new Color(tint.r, tint.g, tint.b, 0.9f));
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        // ---------- boosts ----------

        /// <summary>
        /// Three Cyber floor chevrons (the pack's cyan Arrow_01, its purple Arrow_02 in the middle) a car length long on a
        /// dark plate. The pack draws them flat at y 0 pointing -Z, so they are turned and lifted a hair off the plate. The
        /// plate is what Build Mode rests the piece by and what the kid's grab takes hold of (too big for the toy's hands).
        /// </summary>
        private static bool BuildBoostArrow(AudioClip whoosh)
        {
            var cyan = Load(CY + "Arrow_01.prefab");
            if (cyan == null) return false;
            var purple = Load(CY + "Arrow_02.prefab");
            if (purple == null) purple = cyan;
            const float Width = 3f, Length = 4.2f, Plate = 0.04f, ChevronWidth = 2.6f, Lift = 0.012f;
            var root = new GameObject("BoostArrow");
            Cube("Plate", root.transform, new Vector3(0f, Plate * 0.5f, 0f), new Vector3(Width, Plate, Length), PlateMaterial());
            var solid = root.AddComponent<BoxCollider>();
            solid.center = new Vector3(0f, Plate * 0.5f, 0f);
            solid.size = new Vector3(Width, Plate, Length);

            var arrows = Child(root.transform, "Arrows");
            var glow = new List<Object>();
            for (int i = 0; i < 3; i++)
            {
                var holder = Child(arrows, "Chevron" + (i + 1));
                holder.localRotation = Quaternion.Euler(0f, 180f, 0f);   // the pack's chevrons point -Z
                var inst = Place(i == 1 ? purple : cyan, holder);
                Bounds b = LocalBounds(holder, inst);
                inst.transform.localScale *= ChevronWidth / Mathf.Max(0.01f, b.size.x);   // flat: sized by its width
                b = LocalBounds(holder, inst);
                inst.transform.localPosition += new Vector3(-b.center.x, -b.min.y, -b.center.z);
                float step = (CarLength - b.size.z) * 0.5f;   // three in a row, front tip to back wings one car length
                holder.localPosition = new Vector3(0f, Plate + Lift, step * (1 - i));
                foreach (var r in inst.GetComponentsInChildren<Renderer>()) { r.shadowCastingMode = ShadowCastingMode.Off; glow.Add(r); }
            }

            var boost = Trigger(root.transform, "Boost", new Vector3(0f, 0.75f, 0f), new Vector3(Width, 1.5f, Length));
            var pad = boost.AddComponent<BoostPad>();
            SetArray(pad, "glow", glow);
            Flash(pad, root.transform, new Vector3(0f, 0.9f, 0f), 6f);
            Set(pad, "sound", whoosh);
            root.AddComponent<Carryable>();
            root.AddComponent<Carried>();
            Save(root, Traversal + "/BoostArrow.prefab");
            return true;
        }

        /// <summary>
        /// The Battle Stadium's inflatable gate (checkpoint_002: two pillars and a rounded top band, orange) sized so a car
        /// drives under it. The pack's arch_001..006 are not gates: 001/002 are low hurdles with no opening, 003 a hump with a
        /// crawl hole, 004..006 tunnels with a raised floor. Solid where it is drawn; the boost fills the middle of the opening.
        /// </summary>
        private static bool BuildBoostArch(AudioClip whoosh)
        {
            var model = Load(BS + "Inflatable/checkpoint_002.prefab");
            if (model == null) return false;
            const float Width = 6.6f;
            var root = new GameObject("BoostArch");
            var holder = Child(root.transform, "Model");
            var inst = Place(model, holder);
            FitWidth(inst, holder, Width, out Bounds b);
            Solid(root.transform, inst, false, null);
            // The opening, measured from the mesh: each pillar is the outer 37% of its half, and the top band comes down to
            // 64% of the height at the opening's sides (75% in the middle). The trigger keeps inside that, so a car passing
            // outside a pillar gets nothing.
            var boost = Trigger(root.transform, "Boost", new Vector3(0f, b.size.y * 0.3f, 0f), new Vector3(b.size.x * 0.5f, b.size.y * 0.6f, Mathf.Max(2.5f, b.size.z * 1.5f)));
            var pad = boost.AddComponent<BoostPad>();
            SetArray(pad, "glow", Renderers(inst));
            Flash(pad, root.transform, new Vector3(0f, b.size.y * 0.45f, 0f), Width * 1.2f);
            Set(pad, "sound", whoosh);
            Save(root, Traversal + "/BoostArch.prefab");
            return true;
        }

        /// <summary>
        /// The Battle Stadium's inflatable hoop (ring_002, standing, hole along Z), 6 across so a car fits through its hole
        /// (3.8 wide). Its middle sits at a car's middle height, so the lower rim is under the floor (a RestSink keeps it
        /// there in Build Mode) and cars drive through; Build Mode can raise it for a jump-through hoop.
        /// </summary>
        private static bool BuildBoostRing(AudioClip whoosh)
        {
            var model = Load(BS + "Inflatable/ring_002.prefab");
            if (model == null) return false;
            const float Across = 6f;
            var root = new GameObject("BoostRing");
            var holder = Child(root.transform, "Model");
            var inst = Place(model, holder);
            Bounds b = LocalBounds(holder, inst);
            inst.transform.localScale *= Across / Mathf.Max(0.01f, b.size.x);
            b = LocalBounds(holder, inst);
            inst.transform.localPosition += new Vector3(-b.center.x, CarMiddle - b.center.y, -b.center.z);
            b = LocalBounds(holder, inst);
            root.AddComponent<RestSink>().Set(Mathf.Max(0f, -b.min.y));
            Solid(root.transform, inst, false, null);
            float hole = b.size.x * 0.5f * 0.637f;   // the hole's radius, measured from the mesh: 0.637 of the outer radius
            var boost = Trigger(root.transform, "Boost", new Vector3(0f, CarMiddle, 0f), new Vector3(hole * 1.3f, hole * 1.3f, Mathf.Max(2.5f, b.size.z * 2f)));
            var pad = boost.AddComponent<BoostPad>();
            SetArray(pad, "glow", Renderers(inst));
            Flash(pad, root.transform, new Vector3(0f, CarMiddle, 0f), Across);
            Set(pad, "sound", whoosh);
            Save(root, Traversal + "/BoostRing.prefab");
            return true;
        }

        // ---------- launchers ----------

        /// <summary>
        /// The Cyber pack's Trampoline_01 (a wedge with its arrow decal; the pack nests Trampoline_01_Arrow in it), turned so
        /// it climbs along +Z (the pack's climbs toward -Z). Solid ramp; the throw waits at its top edge.
        /// </summary>
        private static bool BuildJumpPad(AudioClip boing)
        {
            var model = Load(CY + "Trampoline_01.prefab");
            if (model == null) return false;
            const float Width = 4.2f;
            Func<MeshFilter, bool> ramp = mf => !mf.name.Contains("Arrow");   // the arrow decal floats on the slope: not solid
            var root = new GameObject("JumpPad");
            var visual = Child(root.transform, "Visual");
            var inst = Place(model, visual);
            if (!ClimbsAlongZ(inst, visual, ramp)) inst.transform.localRotation = Quaternion.Euler(0f, 180f, 0f) * inst.transform.localRotation;
            FitWidth(inst, visual, Width, out Bounds b);
            Solid(root.transform, inst, false, ramp);
            float h = b.size.y;
            var jump = Trigger(root.transform, "Jump", new Vector3(0f, (h * 1.5f + 2f) * 0.5f, b.max.z - 0.9f), new Vector3(b.size.x * 0.9f, h * 0.5f + 2f, 1.8f));
            var pad = jump.AddComponent<CarJumpPad>();
            Set(pad, "visual", visual);
            Set(pad, "height", 1.5f);
            Set(pad, "minForwardSpeed", 12f);
            Set(pad, "directional", true);
            Set(pad, "squash", 0.85f);
            Set(pad, "sound", boing);
            Save(root, Traversal + "/JumpPad.prefab");
            return true;
        }

        /// <summary>
        /// The Battle Stadium's round trampoline (trampoline_1_001 with its membrane, trampoline_1_002), about a car length
        /// across. Its drum is taller than a car's wheels, so the throw reaches a little past its edge: a car is thrown as it
        /// arrives, and again when it comes down on it (the trigger stops just above the drum, so that is on contact). Each
        /// throw carries a car at least 6 units/s on, so it bounces off rather than in place.
        /// </summary>
        private static bool BuildBounceTramp(AudioClip boing)
        {
            var model = Load(BS + "Obstacles/trampoline_1_001.prefab");
            if (model == null) return false;
            const float Width = 4f, Margin = 0.8f, Above = 0.6f;   // a toy car's body rides 0.4 over its wheels
            var root = new GameObject("BounceTramp");
            var visual = Child(root.transform, "Visual");
            var inst = Place(model, visual);
            FitWidth(inst, visual, Width, out Bounds b);
            Solid(root.transform, inst, true, null);   // convex: the drum and the dome of its membrane
            var jump = Trigger(root.transform, "Jump", new Vector3(0f, (b.size.y + Above) * 0.5f, 0f), new Vector3(b.size.x + Margin * 2f, b.size.y + Above, b.size.z + Margin * 2f));
            var pad = jump.AddComponent<CarJumpPad>();
            Set(pad, "visual", visual);
            Set(pad, "height", 1.2f);
            Set(pad, "minForwardSpeed", 6f);
            Set(pad, "directional", false);
            Set(pad, "squash", 0.6f);
            Set(pad, "sound", boing);
            Save(root, Traversal + "/BounceTramp.prefab");
            return true;
        }

        /// <summary>
        /// The Battle Stadium's propeller pad (propeller_trampoline_1_001: a spinning propeller, propeller_trampoline_2_001,
        /// in a rim on a stand made to hover). As a floor pad the blades sit just above the floor and the stand goes under
        /// it (a RestSink keeps it there in Build Mode). The pack's spin is kept. Throws up, higher than the others, with a
        /// small nudge on: cars only drive on the ground, so a car stopped on it would otherwise bounce in place for good.
        /// </summary>
        private static bool BuildFanPad(AudioClip boing)
        {
            var model = Load(BS + "Obstacles/propeller_trampoline_1_001.prefab");
            if (model == null) return false;
            const float Width = 5.6f, Clear = 0.03f, Above = 1f;   // the trigger stops just over the hub
            Func<MeshFilter, bool> blades = mf => mf.name.Contains("propeller_trampoline_2");
            var root = new GameObject("FanPad");
            var visual = Child(root.transform, "Visual");
            var inst = Place(model, visual, true);
            Bounds b = LocalBounds(visual, inst);
            inst.transform.localScale *= Width / Mathf.Max(0.01f, b.size.x);
            b = LocalBounds(visual, inst);
            var propeller = FindChild(inst.transform, "propeller_trampoline_2");
            float bottom = propeller != null ? LocalBounds(visual, propeller.gameObject).min.y : b.min.y;
            inst.transform.localPosition += new Vector3(-b.center.x, Clear - bottom, -b.center.z);
            b = LocalBounds(visual, inst);
            if (b.min.y < 0f) root.AddComponent<RestSink>().Set(-b.min.y);
            if (propeller != null)
            {
                Solid(root.transform, inst, false, mf => !blades(mf), "Solid");   // the rim and hub as drawn (the stand is under the floor)
                Solid(root.transform, inst, true, blades, "Blades");               // convex: the spinning blades make a disc
            }
            else Solid(root.transform, inst, false, null, "Solid");
            var jump = Trigger(root.transform, "Jump", new Vector3(0f, Above * 0.5f, 0f), new Vector3(b.size.x + 1f, Above, b.size.z + 1f));
            var pad = jump.AddComponent<CarJumpPad>();
            Set(pad, "visual", visual);
            Set(pad, "height", 2f);
            Set(pad, "minForwardSpeed", 4f);
            Set(pad, "directional", false);
            Set(pad, "squash", 0.8f);
            Set(pad, "sound", boing);
            Save(root, Traversal + "/FanPad.prefab");
            return true;
        }

        // ---------- pickups ----------

        /// <summary>
        /// His star (Art/Star/NitroStar.fbx, D-0068) in a glowing colour: a third of the nitro meter. The colour is the
        /// Data/Stars material the prefab's star wears now (so his pick in the editor outlives a rebuild), orange the first
        /// time; the floor glow is turned to the same colour. Without his model the Battle Stadium's gold star (star_001)
        /// stands in, as before, on a gold glow.
        /// </summary>
        private static bool BuildNitroStar(AudioClip chime)
        {
            var star = AssetDatabase.LoadAssetAtPath<GameObject>(StarModel);
            Material look = star != null ? StarLook() : null;
            if (star == null) Debug.LogWarning("[RacingPieces] No " + StarModel + ": the pack's gold star stands in.");
            else if (look == null) Debug.LogWarning("[RacingPieces] No star material could be made: the star keeps its imported one.");
            Color spot = look != null ? look.GetColor("_BaseColor") : new Color(1f, 0.78f, 0.25f);
            spot.a = 0.85f;
            var glow = Glow("NitroGlow", spot);
            if (glow != null && glow.GetColor("_BaseColor") != spot)
            {
                glow.SetColor("_BaseColor", spot);
                EditorUtility.SetDirty(glow);
            }
            var pickup = BuildPickup("NitroStar", star != null ? star : Load(BS + "Props/star_001.prefab"), CarPickup.Kind.Nitro, 0.35f, 8f, glow, chime);
            if (pickup == null) return false;
            var visual = pickup.transform.Find("Visual");
            if (look != null && visual != null)
                foreach (var r in visual.GetComponentsInChildren<MeshRenderer>(true))
                {
                    var mats = r.sharedMaterials;
                    for (int i = 0; i < mats.Length; i++) mats[i] = look;
                    r.sharedMaterials = mats;
                    r.shadowCastingMode = ShadowCastingMode.Off;   // a light in the air: no dark star on the road under it
                }
            Save(pickup.gameObject, Interactables + "/NitroStar.prefab");
            return true;
        }

        /// <summary>The star material to build with: the one from Data/Stars on NitroStar.prefab's star now, else the first
        /// look (orange). Makes any of the colours that are missing (never touches one that exists: they are his to tune).</summary>
        private static Material StarLook()
        {
            EnsureFolder(StarLooks);
            Material first = null;
            foreach (var (file, colour, glow) in StarColours)
            {
                var mat = StarMaterial(file, colour, glow);
                if (file == StarFirstLook) first = mat;
            }
            var now = AssetDatabase.LoadAssetAtPath<GameObject>(Interactables + "/NitroStar.prefab");
            var visual = now != null ? now.transform.Find("Visual") : null;
            if (visual != null)
                foreach (var r in visual.GetComponentsInChildren<MeshRenderer>(true))
                {
                    var worn = r.sharedMaterial;
                    if (worn != null && AssetDatabase.GetAssetPath(worn).StartsWith(StarLooks + "/")) return worn;
                }
            return first;
        }

        /// <summary>One glowing star colour: a copy of our plain lit material (BuildShape) in that colour, lit from inside
        /// by its glow colour. Made once.</summary>
        private static Material StarMaterial(string file, Color colour, Color glow)
        {
            string path = StarLooks + "/" + file + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null) return mat;
            if (!AssetDatabase.CopyAsset(Data + "/BuildShape.mat", path)) { Debug.LogWarning("[RacingPieces] Could not copy BuildShape.mat for " + file + " (run Build Catalog once)."); return null; }
            mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            mat.SetColor("_BaseColor", colour);
            mat.SetColor("_EmissionColor", glow.linear * StarGlow);
            mat.EnableKeyword("_EMISSION");
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;   // the default flag would switch the glow off again
            EditorUtility.SetDirty(mat);
            return mat;
        }

        /// <summary>The Battle Stadium's heart (heart_001): mends a third of a car's health (D-0038).</summary>
        private static bool BuildRepairHeart()
        {
            var pickup = BuildPickup("RepairHeart", Load(BS + "Props/heart_001.prefab"), CarPickup.Kind.Repair, 0.35f, 10f, Glow("RepairGlow", new Color(1f, 0.38f, 0.48f, 0.85f)), ClipAt("UI/UI_Confirm_v1.wav"));
            if (pickup == null) return false;
            Save(pickup.gameObject, Interactables + "/RepairHeart.prefab");
            return true;
        }

        /// <summary>The Battle Stadium's inflatable life ring (lifebuoy_001), stood on its edge: an 8 s shield bubble round the
        /// car with the same ring circling it (CarShield, D-0038).</summary>
        private static bool BuildShieldRing()
        {
            var ring = Load(BS + "Inflatable/lifebuoy_001.prefab");
            var pickup = BuildPickup("ShieldRing", ring, CarPickup.Kind.Shield, 0f, 12f, Glow("ShieldGlow", new Color(0.4f, 0.85f, 1f, 0.85f)), ClipAt("Toys/Toy_Squeak_v1.wav"), true);
            if (pickup == null) return false;
            Set(pickup, "shieldSeconds", 8f);
            Set(pickup, "shieldRing", ring);
            Set(pickup, "shieldMesh", SphereMesh());
            Set(pickup, "shieldBubble", BubbleMaterial());
            Set(pickup, "shieldBlocked", ClipAt("Toys/Ball_Bounce_v1.wav"));
            Set(pickup, "shieldEnd", ClipAt("Props/Balloon_Deflation_Squeak_v1.wav"));
            Save(pickup.gameObject, Interactables + "/ShieldRing.prefab");
            return true;
        }

        /// <summary>
        /// A model (a Battle Stadium prop, or his star) as a car pickup (CarPickup), 1.2 across, floating at a car's middle height over a soft glow on
        /// the floor. The glow marks the spot while the model is away and is what Build Mode rests the piece by (so the model
        /// keeps floating); a thin pad under it is the only solid part (Build Mode picks by solid colliders). The pack's spin,
        /// bob and scale-pulse scripts are off: CarPickup spins and bobs it. <paramref name="standUp"/> stands a model that lies
        /// flat (the life ring) on its edge, so it reads as a ring while it spins. Not saved: the caller adds its own fields.
        /// </summary>
        private static CarPickup BuildPickup(string name, GameObject prefab, CarPickup.Kind kind, float amount, float respawn, Material glow, AudioClip sound, bool standUp = false)
        {
            if (prefab == null) return null;
            const float Size = 1.2f, Reach = 1f, Spot = 1.8f;
            var root = new GameObject(name);
            Quad("Spot", root.transform, new Vector3(0f, 0.02f, 0f), Spot, glow != null ? glow : PlateMaterial());
            var solid = root.AddComponent<BoxCollider>();
            solid.center = new Vector3(0f, 0.025f, 0f);
            solid.size = new Vector3(1.2f, 0.05f, 1.2f);
            var visual = Child(root.transform, "Visual");
            visual.localPosition = Vector3.up * CarMiddle;
            var inst = Place(prefab, visual);
            Bounds b = LocalBounds(visual, inst);
            if (standUp && b.size.y <= Mathf.Min(b.size.x, b.size.z))
            {
                inst.transform.localRotation = Quaternion.Euler(90f, 0f, 0f) * inst.transform.localRotation;
                b = LocalBounds(visual, inst);
            }
            inst.transform.localScale *= Size / Mathf.Max(0.01f, Mathf.Max(b.size.x, b.size.y));
            b = LocalBounds(visual, inst);
            inst.transform.localPosition -= b.center;   // spins about its middle
            var trigger = root.AddComponent<SphereCollider>();
            trigger.isTrigger = true;
            trigger.center = Vector3.up * CarMiddle;
            trigger.radius = Reach;
            var pickup = root.AddComponent<CarPickup>();
            Set(pickup, "kind", (int)kind);
            Set(pickup, "amount", amount);
            Set(pickup, "respawnSeconds", respawn);
            Set(pickup, "trigger", trigger);
            Set(pickup, "visual", visual);
            Set(pickup, "hover", CarMiddle);
            Set(pickup, "sound", sound);
            return pickup;
        }

        /// <summary>Unity's own sphere mesh (a built-in asset a prefab can point at) for the shield bubble.</summary>
        private static Mesh SphereMesh()
        {
            var tmp = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            var mesh = tmp.GetComponent<MeshFilter>().sharedMesh;
            Object.DestroyImmediate(tmp);
            return mesh;
        }

        // ---------- materials ----------

        /// <summary>Dark slate for the boost arrow's plate: a copy of our plain lit material (BuildShape), recoloured.</summary>
        private static Material PlateMaterial()
        {
            const string path = Data + "/BoostPlate.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null) return mat;
            if (!AssetDatabase.CopyAsset(Data + "/BuildShape.mat", path)) { Debug.LogWarning("[RacingPieces] Could not copy BuildShape.mat for the boost plate (run Build Catalog once)."); return null; }
            mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            mat.SetColor("_BaseColor", new Color(0.12f, 0.14f, 0.2f));
            EditorUtility.SetDirty(mat);
            return mat;
        }

        /// <summary>A soft floor glow for a pickup's spot: a copy of our orb glow (his mote drawing), recoloured. Made once.</summary>
        private static Material Glow(string file, Color colour)
        {
            string path = Data + "/" + file + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null) return mat;
            var orb = AssetDatabase.LoadAssetAtPath<Material>(Data + "/OrbGlow.mat");
            if (orb == null) { Debug.LogWarning($"[RacingPieces] No OrbGlow.mat to copy for {file}: the plate colour stands in."); return null; }
            mat = new Material(orb) { name = file };
            mat.SetColor("_BaseColor", colour);
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        /// <summary>The shield bubble: our orb glow without its mote (an even, additive cyan film), faint. Made once.</summary>
        private static Material BubbleMaterial()
        {
            const string path = Data + "/ShieldBubble.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null) return mat;
            var orb = AssetDatabase.LoadAssetAtPath<Material>(Data + "/OrbGlow.mat");
            if (orb == null) { Debug.LogWarning("[RacingPieces] No OrbGlow.mat to copy for the shield bubble: the ring shows without it."); return null; }
            mat = new Material(orb) { name = "ShieldBubble" };
            if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", null);
            if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", null);
            mat.SetColor("_BaseColor", new Color(0.45f, 0.85f, 1f, 0.22f));
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        private static AudioClip ClipAt(string folderAndFile)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(PlayroomSounds + folderAndFile);
            if (clip == null) Debug.LogWarning("[RacingPieces] Missing sound, left empty: " + PlayroomSounds + folderAndFile);
            return clip;
        }

        // ---------- helpers ----------

        private static AudioClip Clip(string file)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(ToySounds + file);
            if (clip == null) Debug.LogWarning("[RacingPieces] Missing sound, left empty: " + ToySounds + file);
            return clip;
        }

        private static GameObject Load(string path)
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (go == null) Debug.LogWarning("[RacingPieces] Missing pack prefab, skipped: " + path);
            return go;
        }

        /// <summary>
        /// An instance of a pack prefab under <paramref name="parent"/>. Its own colliders are switched off (ours replace
        /// them), and so are the pack's animation scripts (the Battle Stadium's spin, bob and a scale pulse that adds to the
        /// scale, so it blows up a resized prop); <paramref name="keepSpin"/> keeps its plain spin (RotationScript).
        /// </summary>
        private static GameObject Place(GameObject prefab, Transform parent, bool keepSpin = false)
        {
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            inst.transform.localPosition = Vector3.zero;
            inst.transform.localRotation = Quaternion.identity;
            foreach (var c in inst.GetComponentsInChildren<Collider>(true)) c.enabled = false;
            foreach (var r in inst.GetComponentsInChildren<Rigidbody>(true)) r.isKinematic = true;
            foreach (var m in inst.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (m == null) continue;
                var type = m.GetType();
                if (type.Namespace == null || !type.Namespace.StartsWith("ithappy")) continue;
                m.enabled = keepSpin && type.Name == "RotationScript";
            }
            return inst;
        }

        /// <summary>Scales <paramref name="inst"/> evenly so its drawn width (x, in <paramref name="space"/>) is
        /// <paramref name="width"/>, and stands it on the pivot: bottom at 0, centred.</summary>
        private static void FitWidth(GameObject inst, Transform space, float width, out Bounds b)
        {
            b = LocalBounds(space, inst);
            inst.transform.localScale *= width / Mathf.Max(0.01f, b.size.x);
            b = LocalBounds(space, inst);
            inst.transform.localPosition += new Vector3(-b.center.x, -b.min.y, -b.center.z);
            b = LocalBounds(space, inst);
        }

        /// <summary>True when a ramp's high end is toward +Z in <paramref name="space"/>: the tallest vertex near each end.</summary>
        private static bool ClimbsAlongZ(GameObject inst, Transform space, Func<MeshFilter, bool> part)
        {
            Bounds b = LocalBounds(space, inst);
            float front = float.MinValue, back = float.MinValue, band = b.size.z * 0.2f;
            foreach (var mf in inst.GetComponentsInChildren<MeshFilter>())
            {
                if (mf.sharedMesh == null || (part != null && !part(mf))) continue;
                foreach (var v in mf.sharedMesh.vertices)   // pack meshes are not Read/Write, but the editor reads them
                {
                    Vector3 p = space.InverseTransformPoint(mf.transform.TransformPoint(v));
                    if (p.z > b.max.z - band) front = Mathf.Max(front, p.y);
                    else if (p.z < b.min.z + band) back = Mathf.Max(back, p.y);
                }
            }
            return front >= back;
        }

        /// <summary>
        /// Our own colliders for a pack model: a MeshCollider per drawn mesh (all, or those <paramref name="part"/> picks), on
        /// a "Solid" child of <paramref name="root"/> copying each mesh's pose, so a squashing or spinning visual never moves
        /// them. Convex for round pads (a drum, a disc of blades); exact for gates, rings and ramps.
        /// </summary>
        private static void Solid(Transform root, GameObject inst, bool convex, Func<MeshFilter, bool> part, string name = "Solid")
        {
            var holder = Child(root, name);
            foreach (var mf in inst.GetComponentsInChildren<MeshFilter>())
            {
                if (mf.sharedMesh == null || (part != null && !part(mf))) continue;
                var piece = new GameObject(mf.name).transform;
                piece.SetParent(holder, false);
                piece.SetPositionAndRotation(mf.transform.position, mf.transform.rotation);
                piece.localScale = mf.transform.lossyScale;   // the root is at scale 1 while building; pack scales are even
                var mc = piece.gameObject.AddComponent<MeshCollider>();
                mc.sharedMesh = mf.sharedMesh;
                mc.convex = convex;
            }
        }

        /// <summary>A trigger box child of <paramref name="parent"/>, for a BoostPad or CarJumpPad to go on.</summary>
        private static GameObject Trigger(Transform parent, string name, Vector3 at, Vector3 size)
        {
            var t = Child(parent, name);
            t.localPosition = at;
            var box = t.gameObject.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = size;
            return t.gameObject;
        }

        /// <summary>A boost's flash: a point light, off until a car takes the boost, in the boost's colour.</summary>
        private static void Flash(BoostPad pad, Transform parent, Vector3 at, float range)
        {
            Set(pad, "flashLight", FlashLight(parent, at, range));
            Set(pad, "lightRange", range);
            Set(pad, "flashColour", BoostColour);
        }

        private static Light FlashLight(Transform parent, Vector3 at, float range)
        {
            var t = Child(parent, "FlashLight");
            t.localPosition = at;
            var light = t.gameObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = BoostColour;
            light.range = range;
            light.intensity = 0f;
            light.shadows = LightShadows.None;
            light.enabled = false;
            return light;
        }

        private static Transform Child(Transform parent, string name)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            return t;
        }

        private static GameObject Cube(string name, Transform parent, Vector3 at, Vector3 size, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = at;
            go.transform.localScale = size;
            var r = go.GetComponent<MeshRenderer>();
            if (mat != null) r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            return go;
        }

        /// <summary>A flat square lying on the floor, facing up.</summary>
        private static GameObject Quad(string name, Transform parent, Vector3 at, float size, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = at;
            go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            go.transform.localScale = Vector3.one * size;
            var r = go.GetComponent<MeshRenderer>();
            if (mat != null) r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            return go;
        }

        private static List<Object> Renderers(GameObject go)
        {
            var list = new List<Object>();
            foreach (var r in go.GetComponentsInChildren<Renderer>()) list.Add(r);
            return list;
        }

        private static Bounds LocalBounds(Transform space, GameObject go)
        {
            Bounds b = new Bounds(); bool any = false;
            foreach (var r in go.GetComponentsInChildren<Renderer>())
            {
                if (r is ParticleSystemRenderer) continue;
                Bounds rb = RendererBounds(space, r);
                if (!any) { b = rb; any = true; } else b.Encapsulate(rb);
            }
            return any ? b : new Bounds(Vector3.zero, Vector3.one * 0.1f);
        }

        /// <summary>A renderer's mesh box, measured in <paramref name="space"/> (tighter than the world AABB).</summary>
        private static Bounds RendererBounds(Transform space, Renderer r)
        {
            Bounds local; Transform from = r.transform;
            var mf = r is MeshRenderer ? r.GetComponent<MeshFilter>() : null;
            if (mf != null && mf.sharedMesh != null) local = mf.sharedMesh.bounds;
            else if (r is SkinnedMeshRenderer smr) { local = smr.localBounds; if (smr.rootBone != null) from = smr.rootBone; }
            else
            {
                var wb = r.bounds;
                local = new Bounds(r.transform.InverseTransformPoint(wb.center), r.transform.InverseTransformVector(wb.size));
            }
            Vector3 c = local.center, e = local.extents;
            Bounds result = new Bounds(space.InverseTransformPoint(from.TransformPoint(c + new Vector3(-e.x, -e.y, -e.z))), Vector3.zero);
            for (int i = 1; i < 8; i++)
            {
                var corner = c + new Vector3((i & 1) != 0 ? e.x : -e.x, (i & 2) != 0 ? e.y : -e.y, (i & 4) != 0 ? e.z : -e.z);
                result.Encapsulate(space.InverseTransformPoint(from.TransformPoint(corner)));
            }
            return result;
        }

        private static Transform FindChild(Transform root, string contains)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true)) if (t.name.Contains(contains)) return t;
            return null;
        }

        private static void Save(GameObject root, string path)
        {
            PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int slash = path.LastIndexOf('/');
            string parent = path.Substring(0, slash);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, path.Substring(slash + 1));
        }

        private static void SetArray(Object target, string field, List<Object> values)
        {
            var so = new SerializedObject(target);
            var p = so.FindProperty(field);
            if (p == null || !p.isArray) { Debug.LogError($"[RacingPieces] {target.GetType().Name} has no array '{field}'."); return; }
            p.arraySize = values.Count;
            for (int i = 0; i < values.Count; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>Sets a serialized (private) field by name.</summary>
        private static void Set(Object target, string field, object value)
        {
            var so = new SerializedObject(target);
            var p = so.FindProperty(field);
            if (p == null) { Debug.LogError($"[RacingPieces] {target.GetType().Name} has no field '{field}'."); return; }
            switch (value)
            {
                case float f: p.floatValue = f; break;
                case int i: p.intValue = i; break;
                case bool b: p.boolValue = b; break;
                case Color c: p.colorValue = c; break;
                case Object o: p.objectReferenceValue = o; break;
                case null: p.objectReferenceValue = null; break;
                default: Debug.LogError($"[RacingPieces] Unsupported value for '{field}'."); return;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
#endif
