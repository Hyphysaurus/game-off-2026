#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// Editor makers for the Workshop's combat and vehicle pieces. "Build Combat &amp; Vehicle Pieces" writes prefabs from the
    /// licensed packs (referenced by asset path, never copied): weapon pickups into Pieces/Weapons (the held weapons they
    /// hand out go to Prefabs/Weapons), practice targets and a toy soldier into Pieces/Interactables, and Cyber Racing cars
    /// set up as ArcadeCars at toy scale into Pieces/Vehicles, plus the settings assets. Re-running overwrites the same
    /// prefabs in place (GUIDs kept) and never overwrites settings. "Wire Workshop Player" adds first person, weapons, health
    /// and the reset key to the open scene's Player.
    /// </summary>
    public static class WorkshopPieces
    {
        private const string Prefabs = "Assets/_Game/Prefabs/";
        private const string PieceWeapons = Prefabs + "Pieces/Weapons";
        private const string PieceInteractables = Prefabs + "Pieces/Interactables";
        private const string PieceVehicles = Prefabs + "Pieces/Vehicles";
        private const string HeldWeapons = Prefabs + "Weapons";
        private const string Combat = Prefabs + "Combat";
        private const string Data = "Assets/_Game/Data";
        private const string CW = "Assets/_Imports/ithappy/Cartoon_Weapons/Prefabs/";
        private const string BS = "Assets/_Imports/ithappy/Battle_Stadium/Prefabs/";
        private const string PR = "Assets/_Imports/ithappy/Playroom/Prefabs/";
        private const string CY = "Assets/_Imports/ithappy/Modular_Cyber_Racing_Cars/Prefabs/";

        private enum Kind { Melee, Blaster }

        private struct WeaponSpec
        {
            public string Name, Label, Model; public Kind Kind; public float Length;
            public float Damage, Interval, Spread, Knock; public int Pellets; public bool Auto;
        }

        private static readonly WeaponSpec[] Weapons =
        {
            new WeaponSpec { Name = "ToyKnife", Label = "Toy Knife", Model = CW + "Knives/Knife_02.prefab", Kind = Kind.Melee, Length = 0.6f, Damage = 30f, Knock = 7f },
            new WeaponSpec { Name = "FoamFinger", Label = "Foam Finger", Model = BS + "Other/foam_finger_001.prefab", Kind = Kind.Melee, Length = 0.9f, Damage = 20f, Knock = 14f },
            new WeaponSpec { Name = "DartPistol", Label = "Dart Pistol", Model = CW + "Weapon_Presets/Pistol/Pistol1.prefab", Kind = Kind.Blaster, Length = 0.4f, Damage = 15f, Interval = 0.18f, Spread = 1f, Pellets = 1, Knock = 3f },
            new WeaponSpec { Name = "DartShotgun", Label = "Dart Shotgun", Model = CW + "Weapon_Presets/Shotgun/Shotgun1.prefab", Kind = Kind.Blaster, Length = 0.85f, Damage = 9f, Interval = 0.7f, Spread = 6f, Pellets = 7, Knock = 4f },
            new WeaponSpec { Name = "DartSMG", Label = "Dart SMG", Model = CW + "Weapon_Presets/SMG/SMG1.prefab", Kind = Kind.Blaster, Length = 0.55f, Damage = 7f, Interval = 0.08f, Spread = 3f, Pellets = 1, Knock = 1.5f, Auto = true },
        };

        private static readonly (string name, string model, float height)[] Targets =
        {
            ("PracticeSkittle", PR + "Wooden_Toys/Wooden_Skittle_01.prefab", 1.8f),
            ("PracticeBear", PR + "Toys/Toy_Bear_01.prefab", 1.6f),
        };

        /// <summary>All fifteen Cyber Racing cars (CyberCar01..15); the garage's body button cycles through them.</summary>
        private static (string name, string model)[] Cars
        {
            get
            {
                var list = new (string, string)[15];
                for (int i = 0; i < 15; i++) list[i] = ($"CyberCar{i + 1:00}", CY + $"Car_{i + 1:00}/Car_{i + 1:00}.prefab");
                return list;
            }
        }

        private const float CarLength = 3.8f;   // a toy's car, at toy scale (the toy is 1.8 tall)

        [MenuItem("Shift Jam/Workshop/Build Combat & Vehicle Pieces")]
        public static void Build()
        {
            foreach (var f in new[] { PieceWeapons, PieceInteractables, PieceVehicles, HeldWeapons, Combat }) EnsureFolder(f);
            var fp = SettingsAsset<FirstPersonSettings>(Data + "/FirstPersonSettings.asset");
            var car = SettingsAsset<ArcadeCarSettings>(Data + "/ArcadeCarSettings.asset");
            var dartMat = DartMaterial();
            var dart = BuildDart(dartMat);
            var made = new List<string>();
            foreach (var w in Weapons) if (BuildWeapon(w, dart)) made.Add(w.Name);
            foreach (var t in Targets) if (BuildTarget(t.name, t.model, t.height)) made.Add(t.name);
            if (BuildSoldier(dart)) made.Add("ToySoldier");
            foreach (var c in Cars) if (BuildCar(c.name, c.model, car)) made.Add(c.name);
            if (BuildGarage()) made.Add("Garage");
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[Workshop] Built {made.Count} pieces: {string.Join(", ", made)}. Settings: {AssetDatabase.GetAssetPath(fp)}, {AssetDatabase.GetAssetPath(car)}.");
        }

        [MenuItem("Shift Jam/Workshop/Wire Workshop Player (first person, weapons, health, reset)")]
        public static void WirePlayer()
        {
            var player = GameObject.Find("Player");
            if (player == null || player.GetComponent<PlayerMotor>() == null) { Debug.LogError("[Workshop] No 'Player' with a PlayerMotor in the open scene."); return; }
            var fp = SettingsAsset<FirstPersonSettings>(Data + "/FirstPersonSettings.asset");
            Undo.RegisterFullObjectHierarchyUndo(player, "Wire Workshop Player");
            var mode = Get<FirstPersonMode>(player);
            Set(mode, "settings", fp);
            Get<WeaponHolder>(player);
            Get<SafeGround>(player);
            bool newHealth = player.GetComponent<Damageable>() == null;
            var health = Get<Damageable>(player);
            if (newHealth)
            {
                Set(health, "maxHealth", 100f);
                Set(health, "invulnerability", 0.6f);
                Set(health, "refillAfterDeath", 1f);
                var respawn = player.GetComponent<Respawn>();
                if (respawn != null) UnityEventTools.AddVoidPersistentListener(health.OnDiedEvent, respawn.Reset);
            }
            if (Object.FindFirstObjectByType<WorkshopReset>() == null)
            {
                var go = new GameObject("WorkshopReset");
                go.AddComponent<WorkshopReset>();
                Undo.RegisterCreatedObjectUndo(go, "Workshop Reset");
            }
            EditorSceneManager.MarkSceneDirty(player.scene);
            Debug.Log("[Workshop] Player wired: FirstPersonMode (F4), WeaponHolder, Damageable (death = Respawn.Reset), SafeGround; WorkshopReset (F9) in the scene. Save the scene.");
        }

        // ---------- pieces ----------

        private static Projectile BuildDart(Material mat)
        {
            var root = new GameObject("FoamDart");
            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            Object.DestroyImmediate(body.GetComponent<Collider>());
            body.name = "Dart";
            body.transform.SetParent(root.transform, false);
            body.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);   // long axis along +Z, the way it flies
            body.transform.localScale = new Vector3(0.05f, 0.09f, 0.05f);
            var r = body.GetComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            root.AddComponent<Projectile>();
            var saved = Save(root, Combat + "/FoamDart.prefab");
            return saved.GetComponent<Projectile>();
        }

        private static bool BuildWeapon(WeaponSpec spec, Projectile dart)
        {
            var model = Load(spec.Model);
            if (model == null) return false;

            // The held weapon: root = grip, +Z = barrel / blade.
            var root = new GameObject(spec.Name);
            var holder = new GameObject("Model");
            holder.transform.SetParent(root.transform, false);
            var inst = Place(model, holder.transform);
            Vector3 grip, tip;
            OrientAndSize(inst, holder.transform, spec, out grip, out tip);
            holder.transform.localPosition = -grip;
            var muzzle = new GameObject("Muzzle").transform;
            muzzle.SetParent(root.transform, false);
            muzzle.localPosition = tip - grip;

            Weapon weapon;
            if (spec.Kind == Kind.Melee)
            {
                var m = root.AddComponent<MeleeWeapon>();
                Set(m, "damage", spec.Damage);
                Set(m, "knockback", spec.Knock);
                Set(m, "reach", spec.Length * 0.9f + 0.3f);
                Set(m, "hitRadius", spec.Length * 0.8f + 0.25f);
                Set(m, "aimsWithView", false);
                Set(m, "handEuler", new Vector3(-35f, 0f, 0f));
                Set(m, "viewOffset", new Vector3(0.3f, -0.32f, 0.5f));
                Set(m, "viewEuler", new Vector3(-35f, -12f, 0f));
                weapon = m;
            }
            else
            {
                var b = root.AddComponent<BlasterWeapon>();
                Set(b, "projectile", dart);
                Set(b, "muzzle", muzzle);
                Set(b, "damage", spec.Damage);
                Set(b, "interval", spec.Interval);
                Set(b, "spread", spec.Spread);
                Set(b, "pellets", spec.Pellets);
                Set(b, "knockback", spec.Knock);
                Set(b, "automatic", spec.Auto);
                Set(b, "aimsWithView", true);
                Set(b, "viewOffset", new Vector3(0.2f, -0.2f, 0.42f));
                weapon = b;
            }
            Set(weapon, "displayName", spec.Label);

            // Loose-in-the-world parts, switched off while held.
            var box = root.AddComponent<BoxCollider>();
            Bounds lb = LocalBounds(root.transform, inst);
            box.center = lb.center; box.size = Vector3.Max(lb.size, Vector3.one * 0.05f);
            box.enabled = false;
            var rb = root.AddComponent<Rigidbody>();
            rb.mass = 0.5f; rb.isKinematic = true;
            root.AddComponent<WeaponLooks>();
            var pickupSelf = root.AddComponent<WeaponPickup>();
            Set(pickupSelf, "weapon", weapon);
            Set(pickupSelf, "respawnSeconds", 0f);
            pickupSelf.enabled = false;
            var heldPrefab = Save(root, HeldWeapons + "/" + spec.Name + ".prefab").GetComponent<Weapon>();

            // The catalogue piece: a stand whose display spins above the floor; E (trigger) hands out a copy.
            var stand = new GameObject(spec.Name + "Pickup");
            var display = new GameObject("Display");
            display.transform.SetParent(stand.transform, false);
            display.transform.localPosition = Vector3.up * 0.7f;
            var dm = new GameObject("Model");
            dm.transform.SetParent(display.transform, false);
            var dinst = Place(model, dm.transform);
            OrientAndSize(dinst, dm.transform, spec, out Vector3 g2, out Vector3 t2);
            Bounds db = LocalBounds(dm.transform, dinst);
            dm.transform.localPosition = -db.center;                 // spin about its middle
            dm.transform.localRotation = Quaternion.identity;
            display.transform.localRotation = Quaternion.Euler(0f, 0f, spec.Kind == Kind.Melee ? 35f : 0f);
            var trigger = stand.AddComponent<SphereCollider>();
            trigger.isTrigger = true;
            trigger.center = Vector3.up * 0.7f;
            trigger.radius = Mathf.Max(0.5f, spec.Length * 0.7f);
            var pickup = stand.AddComponent<WeaponPickup>();
            Set(pickup, "weapon", heldPrefab);
            Set(pickup, "display", display.transform);
            Set(pickup, "respawnSeconds", 2f);
            Save(stand, PieceWeapons + "/" + spec.Name + "Pickup.prefab");
            return true;
        }

        /// <summary>Turns the model so its barrel (muzzle socket, else its longest side) runs along +Z and sizes it to the
        /// spec's length. Outputs the grip and the tip in the holder's space.</summary>
        private static void OrientAndSize(GameObject inst, Transform space, WeaponSpec spec, out Vector3 grip, out Vector3 tip)
        {
            Transform muzzleSocket = FindChild(inst.transform, "Muzzle");
            Transform magSocket = FindChild(inst.transform, "Magazine");
            if (muzzleSocket != null && magSocket != null)
            {
                Vector3 along = space.InverseTransformPoint(muzzleSocket.position) - space.InverseTransformPoint(magSocket.position);
                along.y = 0f;
                if (along.sqrMagnitude > 1e-6f) inst.transform.localRotation = Quaternion.FromToRotation(along.normalized, Vector3.forward) * inst.transform.localRotation;
            }
            else
            {
                Bounds b0 = LocalBounds(space, inst);
                Vector3 s = b0.size;
                if (s.x >= s.y && s.x >= s.z) inst.transform.localRotation = Quaternion.Euler(0f, -90f, 0f) * inst.transform.localRotation;
                else if (s.y >= s.x && s.y >= s.z) inst.transform.localRotation = Quaternion.Euler(90f, 0f, 0f) * inst.transform.localRotation;   // +Y (the top) becomes the tip
            }
            Bounds b = LocalBounds(space, inst);
            float k = spec.Length / Mathf.Max(1e-4f, b.size.z);
            inst.transform.localScale *= k;
            inst.transform.localPosition *= k;
            b = LocalBounds(space, inst);
            if (spec.Kind == Kind.Blaster && magSocket != null)
            {
                grip = space.InverseTransformPoint(magSocket.position);
                grip.x = b.center.x;
            }
            else if (spec.Kind == Kind.Blaster) grip = new Vector3(b.center.x, b.min.y + b.size.y * 0.3f, b.min.z + b.size.z * 0.3f);
            else grip = new Vector3(b.center.x, b.center.y, b.min.z + b.size.z * 0.1f);
            tip = muzzleSocket != null ? space.InverseTransformPoint(muzzleSocket.position) : new Vector3(b.center.x, b.center.y, b.max.z);
        }

        private static bool BuildTarget(string name, string modelPath, float height)
        {
            var model = Load(modelPath);
            if (model == null) return false;
            var root = new GameObject(name);
            var pivot = new GameObject("Pivot");
            pivot.transform.SetParent(root.transform, false);
            var inst = Place(model, pivot.transform);
            FitHeight(inst, pivot.transform, height, out Bounds b);
            var cap = root.AddComponent<CapsuleCollider>();
            cap.height = b.size.y; cap.center = new Vector3(0f, b.size.y * 0.5f, 0f);
            cap.radius = Mathf.Clamp(Mathf.Max(b.extents.x, b.extents.z), 0.15f, b.size.y * 0.5f);
            var health = root.AddComponent<Damageable>();
            Set(health, "maxHealth", 120f);
            Set(health, "showNumbers", true);
            Set(health, "takeKnockback", false);
            Set(health, "invulnerability", 0.05f);
            var target = root.AddComponent<PracticeTarget>();
            Set(target, "pivot", pivot.transform);
            Save(root, PieceInteractables + "/" + name + ".prefab");
            return true;
        }

        private static bool BuildSoldier(Projectile dart)
        {
            var model = Load(PR + "Army/Army_Soldier_01.prefab");
            if (model == null) return false;
            var root = new GameObject("ToySoldier");
            var visual = new GameObject("Visual");
            visual.transform.SetParent(root.transform, false);
            var inst = Place(model, visual.transform);
            FitHeight(inst, visual.transform, 1.6f, out Bounds b);
            var cc = root.AddComponent<CharacterController>();
            cc.height = 1.6f; cc.radius = Mathf.Clamp(Mathf.Max(b.extents.x, b.extents.z), 0.2f, 0.4f); cc.center = Vector3.up * 0.8f;
            cc.stepOffset = 0.3f; cc.skinWidth = 0.03f;
            var health = root.AddComponent<Damageable>();
            Set(health, "maxHealth", 60f);
            Set(health, "showNumbers", true);
            Set(health, "takeKnockback", false);
            var soldier = root.AddComponent<ToySoldier>();
            Set(soldier, "projectile", dart);
            Set(soldier, "visual", visual.transform);
            Save(root, PieceInteractables + "/ToySoldier.prefab");
            return true;
        }

        private static bool BuildCar(string name, string modelPath, ArcadeCarSettings settings)
        {
            var model = Load(modelPath);
            if (model == null) return false;
            if (!HasWheels(model))
            {
                // The pack swaps two names for car 3: Car_03.prefab is the bare shell, Car_03_Body.prefab the whole car.
                var whole = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath.Replace(".prefab", "_Body.prefab"));
                if (whole == null || !HasWheels(whole)) { Debug.LogError($"[Workshop] {modelPath} has no wheels (nor a _Body version with them): {name} not built."); return false; }
                model = whole;
            }
            var root = new GameObject(name);
            var inst = Place(model, root.transform);
            inst.name = "Body";
            Bounds full = LocalBounds(root.transform, inst);
            float k = CarLength / Mathf.Max(1e-4f, full.size.z);
            inst.transform.localScale *= k;
            full = LocalBounds(root.transform, inst);
            inst.transform.localPosition = new Vector3(-full.center.x, -full.min.y, -full.center.z);   // wheels on the ground, centred

            // Body box: the shell above the wheel hubs, so only the springs touch the ground.
            float hub = float.MaxValue;
            var shell = new Bounds(); bool any = false;
            foreach (var r in inst.GetComponentsInChildren<Renderer>())
            {
                bool wheel = false;
                for (var t = r.transform; t != null && t != root.transform; t = t.parent) if (t.name.Contains("Wheel") && !t.name.Contains("Steering")) { wheel = true; hub = Mathf.Min(hub, root.transform.InverseTransformPoint(t.position).y); break; }
                if (wheel) continue;
                Bounds rb = RendererBounds(root.transform, r);
                if (!any) { shell = rb; any = true; } else shell.Encapsulate(rb);
            }
            if (!any) shell = LocalBounds(root.transform, inst);
            if (hub == float.MaxValue) hub = shell.min.y + shell.size.y * 0.25f;
            float bottom = Mathf.Max(shell.min.y, hub);
            var box = root.AddComponent<BoxCollider>();
            box.center = new Vector3(shell.center.x, (bottom + shell.max.y) * 0.5f, shell.center.z);
            box.size = new Vector3(shell.size.x, Mathf.Max(0.1f, shell.max.y - bottom), shell.size.z);
            box.sharedMaterial = CarBodyMaterial();

            var rbody = root.AddComponent<Rigidbody>();
            rbody.mass = 4f;
            var health = root.AddComponent<Damageable>();
            Set(health, "maxHealth", settings != null ? settings.health : 150f);
            Set(health, "invulnerability", 0.05f);
            var arcade = root.AddComponent<ArcadeCar>();
            Set(arcade, "settings", settings);
            root.AddComponent<CarSeat>();
            root.AddComponent<CarLook>();
            Save(root, PieceVehicles + "/" + name + ".prefab");
            return true;
        }

        /// <summary>
        /// A drive-through bay at toy scale: a dark floor (no collider, so no lip), the pack's straight barriers end to end
        /// down both sides, its chequered finish line flat across the way out, and a gate over the way out (two posts and a
        /// bar with the sign on both faces). The Garage trigger fills the bay; cars face +Z (the way out) when docked.
        /// </summary>
        private static bool BuildGarage()
        {
            const float W = 5.2f, L = 7f, WallH = 1.1f, GateH = 4.2f, Post = 0.3f;   // bay, toy units (cars are 3.8 long)
            var root = new GameObject("Garage");
            var lift = new GameObject("Lift").transform; lift.SetParent(root.transform, false);
            var dark = GarageMaterial();

            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.DestroyImmediate(floor.GetComponent<Collider>());
            floor.name = "BayFloor"; floor.transform.SetParent(root.transform, false);
            floor.transform.localScale = new Vector3(W, 0.02f, L); floor.transform.localPosition = new Vector3(0f, 0.011f, 0f);
            floor.GetComponent<MeshRenderer>().sharedMaterial = dark;

            // Walls: straight barrier segments end to end along each side, each stretched to an equal share of the length.
            var wall = Load(CY + "Track Props/Barrier_04.prefab");
            if (wall != null)
                for (int side = -1; side <= 1; side += 2)
                {
                    int n = 1;
                    for (int i = 0; i < n; i++)
                    {
                        var holder = new GameObject(side < 0 ? "WallL" : "WallR").transform; holder.SetParent(root.transform, false);
                        var b = Place(wall, holder);
                        FitHeight(b, holder, WallH, out Bounds bb);
                        bool alongX = bb.size.x >= bb.size.z;
                        float len = Mathf.Max(0.1f, alongX ? bb.size.x : bb.size.z), thick = alongX ? bb.size.z : bb.size.x;
                        if (i == 0) n = Mathf.Max(1, Mathf.CeilToInt(L / len - 0.01f));
                        if (alongX) holder.localRotation = Quaternion.Euler(0f, 90f, 0f);   // run along the bay
                        var s = Vector3.one; if (alongX) s.x = (L / n) / len; else s.z = (L / n) / len; holder.localScale = s;
                        holder.localPosition = new Vector3(side * (W + thick) * 0.5f, 0f, -L * 0.5f + (L / n) * (i + 0.5f));
                        var col = holder.gameObject.AddComponent<BoxCollider>();
                        col.center = bb.center; col.size = bb.size;
                    }
                }

            // The way out: the pack's finish line is a flat strip (12 x 2, no height), laid across the bay floor's end.
            var line = Load(CY + "Track Props/Finish_Line_01.prefab");
            if (line != null)
            {
                var holder = new GameObject("FinishLine").transform; holder.SetParent(root.transform, false);
                var f = Place(line, holder);
                Bounds fb = LocalBounds(holder, f);
                if (fb.size.z > fb.size.x) holder.localRotation = Quaternion.Euler(0f, 90f, 0f);   // its long side across the bay
                f.transform.localScale *= W / Mathf.Max(0.01f, Mathf.Max(fb.size.x, fb.size.z));
                fb = LocalBounds(holder, f);
                f.transform.localPosition = new Vector3(-fb.center.x, -fb.min.y, -fb.center.z);
                float depth = Mathf.Min(fb.size.x, fb.size.z);
                holder.localPosition = new Vector3(0f, 0.03f, L * 0.5f - depth * 0.5f);
            }

            // The gate: two posts outside the walls and a bar across the top, the sign on both faces of the bar.
            for (int side = -1; side <= 1; side += 2)
            {
                var post = GameObject.CreatePrimitive(PrimitiveType.Cube);
                post.name = side < 0 ? "GatePostL" : "GatePostR"; post.transform.SetParent(root.transform, false);
                post.transform.localScale = new Vector3(Post, GateH, Post);
                post.transform.localPosition = new Vector3(side * (W * 0.5f + 0.6f), GateH * 0.5f, L * 0.5f);
                post.GetComponent<MeshRenderer>().sharedMaterial = dark;
            }
            var bar = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bar.name = "GateBar"; bar.transform.SetParent(root.transform, false);
            bar.transform.localScale = new Vector3(W + 1.2f + Post, 0.9f, Post);
            bar.transform.localPosition = new Vector3(0f, GateH - 0.45f, L * 0.5f);
            bar.GetComponent<MeshRenderer>().sharedMaterial = dark;
            for (int face = -1; face <= 1; face += 2)
            {
                var sign = new GameObject(face > 0 ? "SignOut" : "SignIn", typeof(RectTransform));
                sign.transform.SetParent(root.transform, false);
                sign.transform.localPosition = new Vector3(0f, GateH - 0.45f, L * 0.5f + face * (Post * 0.5f + 0.02f));
                sign.transform.localRotation = Quaternion.Euler(0f, face > 0 ? 180f : 0f, 0f);   // text faces away from the bar
                var tmp = sign.AddComponent<TMPro.TextMeshPro>();
                tmp.text = "GARAGE"; tmp.fontSize = 7f; tmp.alignment = TMPro.TextAlignmentOptions.Center; tmp.fontStyle = TMPro.FontStyles.Bold;
                tmp.rectTransform.sizeDelta = new Vector2(W, 0.9f);
            }

            var trigger = root.AddComponent<BoxCollider>();
            trigger.isTrigger = true; trigger.center = new Vector3(0f, 1.2f, 0f); trigger.size = new Vector3(W, 2.4f, L);
            var garage = root.AddComponent<Garage>();
            Set(garage, "lift", lift);
            var bodies = new List<Object>();
            foreach (var c in Cars) { var p = AssetDatabase.LoadAssetAtPath<GameObject>(PieceVehicles + "/" + c.name + ".prefab"); if (p != null) bodies.Add(p); }
            var wheels = new List<Object>();
            for (int i = 1; i <= 15; i++) { var w = AssetDatabase.LoadAssetAtPath<GameObject>(CY + $"Wheels/Wheel_{i:00}.prefab"); if (w != null) wheels.Add(w); }
            SetArray(garage, "bodies", bodies);
            SetArray(garage, "wheels", wheels);
            Save(root, PieceVehicles + "/Garage.prefab");
            return true;
        }

        /// <summary>Dark slate for the garage's floor and gate: a copy of our plain lit material (BuildShape), recoloured.</summary>
        private static Material GarageMaterial()
        {
            const string path = Data + "/GarageDark.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null) return mat;
            if (!AssetDatabase.CopyAsset(Data + "/BuildShape.mat", path)) { Debug.LogWarning("[Workshop] Could not copy BuildShape.mat for the garage."); return null; }
            mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            mat.SetColor("_BaseColor", new Color(0.16f, 0.18f, 0.22f));
            EditorUtility.SetDirty(mat);
            return mat;
        }

        /// <summary>True when a pack car has wheels the ArcadeCar can find (its wheel naming, steering wheels excluded).</summary>
        private static bool HasWheels(GameObject model)
        {
            foreach (var t in model.GetComponentsInChildren<Transform>(true))
                if (t.name.Contains("Wheel") && !t.name.Contains("Steering") && !t.name.Contains("steering")) return true;
            return false;
        }

        private static void SetArray(Object target, string field, List<Object> values)
        {
            var so = new SerializedObject(target);
            var p = so.FindProperty(field);
            if (p == null || !p.isArray) { Debug.LogError($"[Workshop] {target.GetType().Name} has no array '{field}'."); return; }
            p.arraySize = values.Count;
            for (int i = 0; i < values.Count; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ---------- helpers ----------

        private static GameObject Load(string path)
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (go == null) Debug.LogWarning("[Workshop] Missing pack prefab, skipped: " + path);
            return go;
        }

        /// <summary>An instance of a pack prefab under <paramref name="parent"/>, its own colliders switched off (ours replace them).</summary>
        private static GameObject Place(GameObject prefab, Transform parent)
        {
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            inst.transform.localPosition = Vector3.zero;
            inst.transform.localRotation = Quaternion.identity;
            foreach (var c in inst.GetComponentsInChildren<Collider>(true)) c.enabled = false;
            foreach (var r in inst.GetComponentsInChildren<Rigidbody>(true)) r.isKinematic = true;
            return inst;
        }

        private static void FitHeight(GameObject inst, Transform space, float height, out Bounds b)
        {
            b = LocalBounds(space, inst);
            // A flat prop (a finish line, a decal) has no height to fit: scaling it would blow it up to kilometres.
            if (b.size.y < 0.01f) Debug.LogWarning($"[Workshop] {inst.name} is flat: left at its own size.");
            else inst.transform.localScale *= height / b.size.y;
            b = LocalBounds(space, inst);
            inst.transform.localPosition = new Vector3(-b.center.x, -b.min.y, -b.center.z);   // feet at the pivot, centred
            b = LocalBounds(space, inst);
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
            if (r is MeshRenderer && r.TryGetComponent(out MeshFilter mf) && mf.sharedMesh != null) local = mf.sharedMesh.bounds;
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

        private static GameObject Save(GameObject root, string path)
        {
            var saved = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return saved;
        }

        private static T SettingsAsset<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) return asset;
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        /// <summary>Orange foam: a copy of our own lit material (ShiftCore), recoloured. No shader lookups.</summary>
        private static Material DartMaterial()
        {
            const string path = Data + "/FoamDart.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null) return mat;
            if (!AssetDatabase.CopyAsset(Data + "/ShiftCore.mat", path)) { Debug.LogWarning("[Workshop] Could not copy ShiftCore.mat for the dart."); return null; }
            mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            mat.SetColor("_BaseColor", new Color(1f, 0.45f, 0.15f));
            if (mat.HasProperty("_EmissionColor")) mat.SetColor("_EmissionColor", new Color(0.5f, 0.15f, 0.03f));
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static PhysicsMaterial CarBodyMaterial()
        {
            const string path = Data + "/CarBody.physicMaterial";
            var mat = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(path);
            if (mat != null) return mat;
            mat = new PhysicsMaterial("CarBody") { dynamicFriction = 0.1f, staticFriction = 0.1f, bounciness = 0.05f, frictionCombine = PhysicsMaterialCombine.Minimum };
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int slash = path.LastIndexOf('/');
            string parent = path.Substring(0, slash);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, path.Substring(slash + 1));
        }

        private static T Get<T>(GameObject go) where T : Component =>
            go.TryGetComponent(out T c) ? c : Undo.AddComponent<T>(go);

        /// <summary>Sets a serialized (private) field by name.</summary>
        private static void Set(Object target, string field, object value)
        {
            var so = new SerializedObject(target);
            var p = so.FindProperty(field);
            if (p == null) { Debug.LogError($"[Workshop] {target.GetType().Name} has no field '{field}'."); return; }
            switch (value)
            {
                case float f: p.floatValue = f; break;
                case int i: p.intValue = i; break;
                case bool b: p.boolValue = b; break;
                case string s: p.stringValue = s; break;
                case Vector3 v: p.vector3Value = v; break;
                case Color c: p.colorValue = c; break;
                case Object o: p.objectReferenceValue = o; break;
                case null: p.objectReferenceValue = null; break;
                default: Debug.LogError($"[Workshop] Unsupported value for '{field}'."); return;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
#endif
