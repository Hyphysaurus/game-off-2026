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
    /// hand out go to Prefabs/Weapons), practice targets and a toy soldier into Pieces/Interactables, the Cyber Racing cars
    /// and Battle Stadium's racing cars (ToyRacer01..10) set up as ArcadeCars at toy scale into Pieces/Vehicles, the car
    /// cannon into Pieces/Traversal, plus the settings assets. Every car gets the boost set's parts (D-0037). Re-running
    /// overwrites the same prefabs in place (GUIDs kept) and never overwrites settings. "Wire Workshop Player" adds first
    /// person, weapons, health and the reset key to the open scene's Player.
    /// </summary>
    public static class WorkshopPieces
    {
        private const string Prefabs = "Assets/_Game/Prefabs/";
        private const string PieceWeapons = Prefabs + "Pieces/Weapons";
        private const string PieceInteractables = Prefabs + "Pieces/Interactables";
        private const string PieceVehicles = Prefabs + "Pieces/Vehicles";
        private const string PieceTraversal = Prefabs + "Pieces/Traversal";
        private const string HeldWeapons = Prefabs + "Weapons";
        private const string Combat = Prefabs + "Combat";
        private const string Data = "Assets/_Game/Data";
        private const string CW = "Assets/_Imports/ithappy/Cartoon_Weapons/Prefabs/";
        private const string BS = "Assets/_Imports/ithappy/Battle_Stadium/Prefabs/";
        private const string PR = "Assets/_Imports/ithappy/Playroom/Prefabs/";
        private const string CY = "Assets/_Imports/ithappy/Modular_Cyber_Racing_Cars/Prefabs/";
        private const string CyberDemo = "Assets/_Imports/ithappy/Modular_Cyber_Racing_Cars/Demonstration/Demonstration_Prefabs/";
        private const string ToySounds = "Assets/_Imports/ithappy/Playroom/Playroom_Sound_FX/Toys/";
        private const string PropSounds = "Assets/_Imports/ithappy/Playroom/Playroom_Sound_FX/Props/";

        private enum Kind { Melee, Blaster }

        private struct WeaponSpec
        {
            public string Name, Label, Model; public Kind Kind; public float Length;
            public float Damage, Interval, Spread, Knock; public int Pellets; public bool Auto;
            /// <summary>A long gun: held in both hands, carried across the body, raised to the shoulder to aim.</summary>
            public bool TwoHands;
            /// <summary>A blaster from the Cartoon Weapons pack, kept at the pack's own size (Length is not used): the
            /// middle of its grip, in the model's own space (measured in the test room from the model: the part that
            /// hangs below the trigger). The weapon's root is put there, so the fist closes on the grip.</summary>
            public Vector3 Grip; public bool PackSize;
            /// <summary>How far what is fitted on the muzzle (a suppressor) reaches past the muzzle marker.</summary>
            public float MuzzleReach;
            /// <summary>A model standing up (longest side Y) with its tip at the bottom: -Y becomes the tip instead of +Y.</summary>
            public bool TipDown;
            /// <summary>Melee: where the fist closes, as a share of the length from the back end. 0 = 0.1 (a cuff, a pommel).</summary>
            public float GripAt;
            /// <summary>A blaster's magazine (D-0070, the test room's numbers): rounds in a full one, the reserve it starts
            /// with, the most the reserve holds, and the seconds a reload takes.</summary>
            public int Rounds, Reserve, ReserveMost; public float ReloadSeconds;
            /// <summary>A blaster's feel (D-0071, the test room's numbers): how hard it kicks (1 = the pistol), the share
            /// of the ammo's own speed its shots leave at, and the share of normal speed you move at with it out.
            /// 0 = 1 for each.</summary>
            public float Recoil, ShotSpeed, Walk;
            /// <summary>A blaster's sight for first person (D-0074): the point aim brings to the middle of the view, in
            /// the weapon's own axes from the middle of its grip. Zero (every gun so far): found from the model, the
            /// glass of the sight on its Place_..._Sight mount (Combat/GunSight). SightRoll: degrees the gun rolls over
            /// as the sight comes to the eye, for a sight that stands at a slant on the gun (the SMG's offset sight).</summary>
            public Vector3 Sight; public float SightRoll;
            /// <summary>The hands on this gun (D-0078), set so that nothing of a hand is drawn inside the model (measured
            /// against the drawn meshes, STATUS 19 Nov). Support: where the support hand holds a long gun, in the
            /// weapon's own axes from the middle of its grip (zero: the model's under-barrel marker, which on a fat
            /// fore-end is inside it). SupportRoll: degrees the support palm sits round the barrel to the support side
            /// (0: the shared number). SupportCurl: degrees each bone of its fingers bends (0: shared). GripCurl:
            /// degrees each bone of the gun hand's fingers bends, x the three gripping fingers, y the trigger finger,
            /// z the thumb (0 in a part: shared). Hand: the gun's slide from the palm, in the weapon's own axes.
            /// SightDistance: first person, how far in front of the eye the sight sits while aimed (0: the shared
            /// 0.307); further out brings the hand on the grip up into view.</summary>
            public Vector3 Support, GripCurl, Hand; public float SupportRoll, SupportCurl, SightDistance;
        }

        private static readonly WeaponSpec[] Weapons =
        {
            // Knife_02 stands ring up, blade tip down: the fist takes the handle just under the finger ring.
            new WeaponSpec { Name = "ToyKnife", Label = "Toy Knife", Model = CW + "Knives/Knife_02.prefab", Kind = Kind.Melee, Length = 0.6f, Damage = 30f, Knock = 7f, TipDown = true, GripAt = 0.35f },
            new WeaponSpec { Name = "FoamFinger", Label = "Foam Finger", Model = BS + "Other/foam_finger_001.prefab", Kind = Kind.Melee, Length = 0.9f, Damage = 20f, Knock = 14f },
            new WeaponSpec { Name = "DartPistol", Label = "Dart Pistol", Model = CW + "Weapon_Presets/Pistol/Pistol1.prefab", Kind = Kind.Blaster, Length = 0.4f, Damage = 15f, Interval = 0.2f, Spread = 0f, Pellets = 1, Knock = 8f, Recoil = 1f, ShotSpeed = 1f, Walk = 1f, PackSize = true, Grip = new Vector3(0f, -0.0412f, -0.0258f), MuzzleReach = 0.1726f, Hand = new Vector3(0f, 0.016f, 0f), GripCurl = new Vector3(0f, 20f, 0f), SightDistance = 0.55f, Rounds = 12, Reserve = 48, ReserveMost = 96, ReloadSeconds = 1.3f },   // the test room's pistol: 5 shots a second, damage 1 (15 here), push 8
            new WeaponSpec { Name = "DartShotgun", Label = "Dart Shotgun", Model = CW + "Weapon_Presets/Shotgun/Shotgun1.prefab", Kind = Kind.Blaster, Length = 0.85f, Damage = 7.5f, Interval = 0.8333f, Spread = 2.5f, Pellets = 8, Knock = 2.5f, Recoil = 2.2f, ShotSpeed = 0.9f, Walk = 0.9f, TwoHands = true, PackSize = true, Grip = new Vector3(0f, -0.0305f, -0.006f), Hand = new Vector3(0.006f, 0.016f, 0f), GripCurl = new Vector3(0f, 8f, 0f), Support = new Vector3(0f, 0.026f, 0.286f), SupportRoll = 50f, Rounds = 6, Reserve = 24, ReserveMost = 48, ReloadSeconds = 2.2f },   // the test room's shotgun: 1.2 shots a second, 8 pellets of damage 0.5 (7.5 here) and push 2.5 within 2.5 degrees
            new WeaponSpec { Name = "DartSMG", Label = "Dart SMG", Model = CW + "Weapon_Presets/SMG/SMG1.prefab", Kind = Kind.Blaster, Length = 0.55f, Damage = 15f, Interval = 0.1429f, Spread = 0.3f, Pellets = 1, Knock = 7f, Recoil = 0.85f, ShotSpeed = 1.1f, Walk = 0.96f, Auto = true, TwoHands = true, PackSize = true, Grip = new Vector3(0f, -0.0556f, -0.0267f), MuzzleReach = 0.0985f, SightRoll = 45f, Hand = new Vector3(-0.006f, 0f, 0f), GripCurl = new Vector3(0f, 20f, 1f), Support = new Vector3(-0.02f, 0.065f, 0.2731f), SupportRoll = 90f, SightDistance = 0.46f, Rounds = 20, Reserve = 60, ReserveMost = 120, ReloadSeconds = 1.5f },   // no SMG in the test room: every number half way between its pistol (12 / 48 / 96, 1.3 s; 5 a second, push 8, kick 1) and its rifle (30 / 90 / 180, 1.8 s; 9 a second, push 6, spread 0.6, speed 1.2, kick 0.7, walk 0.92)
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
        // No car's body sits lower than this over the ground. The body box starts at the lowest wheel hub, and a dragster's
        // small front wheels put that at 0.22: it scraped the road going up the raceway's loop where cars at 0.36 did not.
        private const float BodyClearance = 0.36f;

        /// <summary>Battle Stadium's racing cars, car_1..car_10, built as ToyRacer01..10. Not in the garage's body list: its
        /// part swaps (CarLook) know only the Cyber pack's part names.</summary>
        private const int ToyRacers = 10;

        [MenuItem("Shift Jam/Workshop/Build Combat & Vehicle Pieces")]
        public static void Build()
        {
            foreach (var f in new[] { PieceWeapons, PieceInteractables, PieceVehicles, PieceTraversal, HeldWeapons, Combat }) EnsureFolder(f);
            var fp = SettingsAsset<FirstPersonSettings>(Data + "/FirstPersonSettings.asset");
            var car = SettingsAsset<ArcadeCarSettings>(Data + "/ArcadeCarSettings.asset");
            var dartMat = DartMaterial();
            var dart = DartPieces.BuildDart(dartMat);   // the foam dart has its own maker (Build Foam Dart)
            var made = new List<string>();
            MissingFields.Clear();
            var extras = LoadCarExtras();
            foreach (var w in Weapons) if (BuildWeapon(w, dart)) made.Add(w.Name);
            foreach (var t in Targets) if (BuildTarget(t.name, t.model, t.height)) made.Add(t.name);
            if (BuildSoldier(dart)) made.Add("ToySoldier");
            foreach (var c in Cars) if (BuildCar(c.name, c.model, car, extras)) made.Add(c.name);
            for (int i = 1; i <= ToyRacers; i++) if (BuildToyRacer(i, car, extras)) made.Add($"ToyRacer{i:00}");
            if (BuildGarage()) made.Add("Garage");
            if (BuildCannon()) made.Add("CarCannon");
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[Workshop] Built {made.Count} pieces: {string.Join(", ", made)}. Settings: {AssetDatabase.GetAssetPath(fp)}, {AssetDatabase.GetAssetPath(car)}.");
            if (MissingFields.Count > 0)
                Debug.LogWarning($"[Workshop] ArcadeCar has no {string.Join(", ", MissingFields)} yet (the boost change adds them), so the cars were built without them. Run Shift Jam > Workshop > Build Combat & Vehicle Pieces again once it has landed.");
        }

        [MenuItem("Shift Jam/Workshop/Build Car Cannon")]
        public static void BuildCannonOnly()
        {
            EnsureFolder(PieceTraversal);
            bool ok = BuildCannon();
            AssetDatabase.SaveAssets();
            Debug.Log(ok ? "[Workshop] Built CarCannon into " + PieceTraversal + "." : "[Workshop] CarCannon not built (see the error above).");
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
                // With the ported player PlayerHurt plays the fall and asks the Respawn itself (D-0064).
                if (respawn != null && player.GetComponent<PlayerHurt>() == null) UnityEventTools.AddVoidPersistentListener(health.OnDiedEvent, respawn.Reset);
            }
            if (Object.FindFirstObjectByType<WorkshopReset>() == null)
            {
                var go = new GameObject("WorkshopReset");
                go.AddComponent<WorkshopReset>();
                Undo.RegisterCreatedObjectUndo(go, "Workshop Reset");
            }
            EditorSceneManager.MarkSceneDirty(player.scene);
            Debug.Log("[Workshop] Player wired: FirstPersonMode (V), WeaponHolder, Damageable (death = the fall, then Respawn), SafeGround; WorkshopReset (F9) in the scene. Save the scene.");
        }

        // ---------- pieces ----------

        /// <summary>Fills a list of clips from _Imports/CasePortman/Weapons: name1.ogg, name2.ogg, name3.ogg, those that are there.</summary>
        internal static void WireClips(Object target, string field, string name)
        {
            var so = new SerializedObject(target);
            var list = so.FindProperty(field);
            if (list == null) { Debug.LogError($"[Workshop] {target.GetType().Name} has no field '{field}'."); return; }
            list.ClearArray();
            for (int i = 1; i <= 3; i++)
            {
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Imports/CasePortman/Weapons/" + name + i + ".ogg");
                if (clip == null) continue;
                list.InsertArrayElementAtIndex(list.arraySize);
                list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = clip;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
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
                Set(b, "shotSpeed", spec.ShotSpeed > 0f ? spec.ShotSpeed : 1f);
                Set(b, "recoil", spec.Recoil > 0f ? spec.Recoil : 1f);
                Set(b, "moveSpeed", spec.Walk > 0f ? spec.Walk : 1f);
                Set(b, "aimsWithView", true);
                Set(b, "twoHanded", spec.TwoHands);
                // First person: where the grip sits from the camera. At the pack's size these are the test room's hip
                // positions (a long gun is held closer), taken to the middle of the grip.
                Set(b, "viewOffset", !spec.PackSize ? new Vector3(0.2f, -0.2f, 0.42f) : spec.TwoHands ? new Vector3(0.119f, -0.218f, 0.206f) : new Vector3(0.139f, -0.228f, 0.265f));
                Set(b, "sightPoint", spec.Sight);
                Set(b, "sightRoll", spec.SightRoll);
                Set(b, "magazine.size", spec.Rounds);
                Set(b, "magazine.reserveStart", spec.Reserve);
                Set(b, "magazine.reserveMax", spec.ReserveMost);
                Set(b, "magazine.reloadSeconds", spec.ReloadSeconds);
                weapon = b;
            }
            Set(weapon, "displayName", spec.Label);
            FitHold(weapon, spec);

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
            trigger.radius = Mathf.Max(0.5f, (spec.PackSize ? db.size.z : spec.Length) * 0.7f);
            var pickup = stand.AddComponent<WeaponPickup>();
            Set(pickup, "weapon", heldPrefab);
            Set(pickup, "display", display.transform);
            Set(pickup, "respawnSeconds", 2f);
            Save(stand, PieceWeapons + "/" + spec.Name + "Pickup.prefab");
            return true;
        }

        /// <summary>Writes a weapon's own hold numbers (the hands on this gun) from its row.</summary>
        private static void FitHold(Weapon weapon, WeaponSpec spec)
        {
            Set(weapon, "handOffset", spec.Hand);
            Set(weapon, "supportPoint", spec.Support);
            Set(weapon, "supportRoll", spec.SupportRoll > 0f ? spec.SupportRoll : -1f);
            Set(weapon, "supportCurl", spec.SupportCurl);
            Set(weapon, "gripCurl", spec.GripCurl);
            Set(weapon, "sightDistance", spec.SightDistance);
        }

        /// <summary>Writes only the hold numbers onto the held weapons that are there, in place (ids kept, nothing else
        /// of a prefab is rebuilt). The full build writes the same numbers through the same FitHold.
        /// Batch: -executeMethod ShiftJam.WorkshopPieces.FitHolds -quit.</summary>
        [MenuItem("Shift Jam/Workshop/Fit Weapon Holds (hands on the guns)")]
        public static void FitHolds()
        {
            var done = new List<string>();
            foreach (WeaponSpec spec in Weapons)
            {
                string path = HeldWeapons + "/" + spec.Name + ".prefab";
                if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null) { Debug.LogWarning("[Workshop] " + path + " is not there: skipped."); continue; }
                GameObject root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    if (!root.TryGetComponent(out Weapon weapon)) { Debug.LogWarning("[Workshop] " + path + " has no Weapon on its root: skipped."); continue; }
                    FitHold(weapon, spec);
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    done.Add(spec.Name);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"[Workshop] hold numbers written on {done.Count} held weapons: {string.Join(", ", done)}.");
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
                else if (s.y >= s.x && s.y >= s.z)   // the top (+Y) becomes the tip, or the bottom (-Y) for a tip-down model
                    inst.transform.localRotation = Quaternion.Euler(spec.TipDown ? -90f : 90f, 0f, 0f) * inst.transform.localRotation;
            }
            Bounds b = LocalBounds(space, inst);
            if (spec.PackSize)
            {
                // The pack's own size, as the test room has it; the root goes to the middle of the grip (the hand held
                // the magazine before, 6 to 15 cm from the grip), the muzzle to the end of what is fitted on it.
                grip = inst.transform.localPosition + inst.transform.localRotation * spec.Grip;
                tip = (muzzleSocket != null ? space.InverseTransformPoint(muzzleSocket.position) : new Vector3(b.center.x, b.center.y, b.max.z)) + Vector3.forward * spec.MuzzleReach;
                return;
            }
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
            else grip = new Vector3(b.center.x, b.center.y, b.min.z + b.size.z * (spec.GripAt > 0f ? spec.GripAt : 0.1f));
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
            Set(health, "invulnerability", 0f);   // every pellet and dart counts; a swing already hits each target once
            Set(health, "squash", 0.15f);         // the test room's target: squashes by 0.15 when hit, back in 0.3 s
            Set(health, "squashSeconds", 0.3f);
            var target = root.AddComponent<PracticeTarget>();
            Set(target, "pivot", pivot.transform);
            HitShapePieces.Fit(root);   // shots land on the drawn mesh, not on the capsule (which stays what is bumped into)
            Save(root, PieceInteractables + "/" + name + ".prefab");
            return true;
        }

        private static bool BuildSoldier(Projectile dart)
        {
            // A standing rifleman, rifle along +Z at chest height (the way ToySoldier faces). 01 and 02 are lying down,
            // 05 carries a bazooka.
            var model = Load(PR + "Army/Army_Soldier_06.prefab");
            if (model == null) return false;
            var root = new GameObject("ToySoldier");
            var visual = new GameObject("Visual");
            visual.transform.SetParent(root.transform, false);
            var inst = Place(model, visual.transform);
            FitHeight(inst, visual.transform, 1.6f, out Bounds b);
            // Stand on the figure's own pivot (its body), not the middle of its bounds: the rifle reaches far out in front.
            inst.transform.localPosition = new Vector3(0f, inst.transform.localPosition.y, 0f);
            b = LocalBounds(visual.transform, inst);
            var cc = root.AddComponent<CharacterController>();
            cc.height = 1.6f; cc.radius = Mathf.Clamp(b.extents.x, 0.2f, 0.4f); cc.center = Vector3.up * 0.8f;   // side to side: the rifle is not body
            cc.stepOffset = 0.3f; cc.skinWidth = 0.03f;
            var health = root.AddComponent<Damageable>();
            Set(health, "maxHealth", 60f);
            Set(health, "showNumbers", true);
            Set(health, "takeKnockback", false);
            Set(health, "invulnerability", 0f);   // every pellet and SMG dart counts
            Set(health, "squash", 0.1f);          // the test room's figure: squashes by 0.1 when hit, back in 0.25 s
            Set(health, "squashSeconds", 0.25f);
            var soldier = root.AddComponent<ToySoldier>();
            Set(soldier, "projectile", dart);
            Set(soldier, "visual", visual.transform);
            if (FrontPoint(visual.transform, inst, out Vector3 tip))
            {
                var muzzle = new GameObject("Muzzle").transform;   // shots leave the rifle's tip
                muzzle.SetParent(root.transform, false);
                muzzle.localPosition = tip + Vector3.forward * 0.03f;
                Set(soldier, "muzzle", muzzle);
            }
            HitShapePieces.Fit(root);   // shots land on the drawn mesh, not on the walking capsule
            Save(root, PieceInteractables + "/ToySoldier.prefab");
            return true;
        }

        private static bool BuildCar(string name, string modelPath, ArcadeCarSettings settings, CarExtras extras)
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
            var arcade = MakeDrivable(root, shell, hub, settings);
            SetBoostParts(arcade, extras, null);
            root.AddComponent<CarLook>();
            Save(root, PieceVehicles + "/" + name + ".prefab");
            return true;
        }

        /// <summary>
        /// The body box over the wheel hubs (so only the springs touch the ground), the rigidbody, health, the ArcadeCar and
        /// the seat: what turns a model under <paramref name="root"/> into a car. <paramref name="shell"/> is the drawn body
        /// without its wheels, in root space; <paramref name="hub"/> the lowest hub height (float.MaxValue: none known).
        /// </summary>
        private static ArcadeCar MakeDrivable(GameObject root, Bounds shell, float hub, ArcadeCarSettings settings)
        {
            if (hub == float.MaxValue) hub = shell.min.y + shell.size.y * 0.25f;
            float bottom = Mathf.Max(shell.min.y, hub, BodyClearance);
            var box = root.AddComponent<BoxCollider>();
            box.center = new Vector3(shell.center.x, (bottom + shell.max.y) * 0.5f, shell.center.z);
            box.size = new Vector3(shell.size.x, Mathf.Max(0.1f, shell.max.y - bottom), shell.size.z);
            box.sharedMaterial = CarBodyMaterial();

            var rbody = root.AddComponent<Rigidbody>();
            rbody.mass = 4f;
            var health = root.AddComponent<Damageable>();
            Set(health, "maxHealth", settings != null ? settings.health : 150f);
            Set(health, "invulnerability", 0f);   // every pellet counts; rams and crashes have their own cooldowns
            var arcade = root.AddComponent<ArcadeCar>();
            Set(arcade, "settings", settings);
            root.AddComponent<CarSeat>();
            return arcade;
        }

        /// <summary>
        /// One of Battle Stadium's racing cars (car_N_001) as ToyRacerNN, driven like the Cyber cars at the same toy size.
        /// The pack's body prefab already holds its parts at their places: wheels on their axles, car 4's wind-up key, car
        /// 3's fans and flaps (the part prefabs do not share one origin; each pivots on itself). A pack wheel pivots on its
        /// axle but at the rim's inner face, so each wheel moves onto a hub of ours at its middle (Wheel_FL / FR / RL / RR:
        /// steering then turns it about its middle) and the body's own copy is switched off. Car 3 has no wheels (a fan
        /// boat): it rides four hidden hover points. The hubs and which of them steer are written into the ArcadeCar.
        /// </summary>
        private static bool BuildToyRacer(int number, ArcadeCarSettings settings, CarExtras extras)
        {
            string name = $"ToyRacer{number:00}";
            var model = Load(BS + $"Racing_Cars/car_{number}_001.prefab");
            if (model == null) return false;
            var root = new GameObject(name);
            var inst = Place(model, root.transform);
            inst.name = "Body";
            Bounds full = LocalBounds(root.transform, inst);
            float k = CarLength / Mathf.Max(1e-4f, full.size.z);
            inst.transform.localScale *= k;
            full = LocalBounds(root.transform, inst);
            inst.transform.localPosition = new Vector3(-full.center.x, -full.min.y, -full.center.z);   // wheels on the ground, centred

            var hubs = new List<Transform>();
            Transform key = null;
            foreach (var t in inst.GetComponentsInChildren<Transform>(true))
            {
                if (t == inst.transform) continue;
                if (key == null && Has(t.name, "_key_")) key = t;
                if (!Has(t.name, "_wheel_") || (t.parent != null && Has(t.parent.name, "_wheel_"))) continue;
                var hub = new GameObject("Wheel").transform;
                hub.SetParent(root.transform, false);
                var mf = t.GetComponent<MeshFilter>();
                hub.position = mf != null && mf.sharedMesh != null ? t.TransformPoint(mf.sharedMesh.bounds.center) : t.position;
                var source = WheelSource(t);
                GameObject wheel;
                if (source != null) wheel = Place(source, hub);
                else
                {
                    wheel = Object.Instantiate(t.gameObject, hub);
                    foreach (var c in wheel.GetComponentsInChildren<Collider>(true)) c.enabled = false;
                }
                wheel.name = t.name;
                wheel.transform.SetPositionAndRotation(t.position, t.rotation);
                wheel.transform.localScale = t.lossyScale;   // the hub is unscaled under an unscaled root
                t.gameObject.SetActive(false);
                hubs.Add(hub);
            }
            if (hubs.Count == 0)
            {
                // A fan boat: four hover points under its corners, held up by the springs like wheels (hover gap = the
                // settings' wheel radius, as nothing is drawn under them).
                Bounds hull = LocalBounds(root.transform, inst);
                for (int i = 0; i < 4; i++)
                {
                    var hub = new GameObject("Wheel").transform;
                    hub.SetParent(root.transform, false);
                    hub.localPosition = new Vector3(hull.center.x + (i % 2 == 0 ? -0.6f : 0.6f) * hull.extents.x, hull.min.y,
                        hull.center.z + (i < 2 ? 0.6f : -0.6f) * hull.extents.z);
                    hubs.Add(hub);
                }
            }

            // Corner names and steering: the pack's cars face +Z (its demo drives them nose first), front = ahead of the middle.
            float midZ = 0f;
            foreach (var h in hubs) midZ += h.localPosition.z;
            midZ /= hubs.Count;
            var steers = new List<bool>();
            var names = new HashSet<string>();
            float hubY = float.MaxValue;
            foreach (var h in hubs)
            {
                bool front = h.localPosition.z > midZ + 0.01f;
                float x = h.localPosition.x;
                string corner = "Wheel_" + (front ? "F" : "R") + (Mathf.Abs(x) < 0.05f ? "C" : x < 0f ? "L" : "R");
                string n = corner;
                for (int i = 2; !names.Add(n); i++) n = corner + i;
                h.name = n;
                steers.Add(front);
                hubY = Mathf.Min(hubY, h.localPosition.y);
            }

            var shell = new Bounds(); bool any = false;
            foreach (var r in inst.GetComponentsInChildren<Renderer>())   // the switched-off wheel parts drop out
            {
                if (r is ParticleSystemRenderer || (key != null && r.transform.IsChildOf(key))) continue;   // the key stands above the roof
                Bounds rb = RendererBounds(root.transform, r);
                if (!any) { shell = rb; any = true; } else shell.Encapsulate(rb);
            }
            if (!any) shell = LocalBounds(root.transform, inst);
            var arcade = MakeDrivable(root, shell, hubY, settings);
            SetArray(arcade, "wheels", hubs.ConvertAll(h => (Object)h));
            SetBools(arcade, "steers", steers);
            SetBoostParts(arcade, extras, key);
            SpinFans(inst);
            Save(root, PieceVehicles + "/" + name + ".prefab");
            return true;
        }

        /// <summary>The pack prefab a body's wheel part is an instance of (car_N_wheel_00M).</summary>
        private static GameObject WheelSource(Transform part)
        {
            string path = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(part.gameObject);
            if (string.IsNullOrEmpty(path) || !Has(path, "_wheel_")) path = BS + "Racing_Cars/" + part.name + ".prefab";
            return AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }

        /// <summary>Car 3's two fans turn with the pack's own RotationScript, looked up by name (our code does not build
        /// against the pack's scripts).</summary>
        private static void SpinFans(GameObject body)
        {
            System.Type spin = null;
            foreach (var t in body.GetComponentsInChildren<Transform>(true))
            {
                if (!Has(t.name, "_propeller_")) continue;
                if (spin == null) spin = System.Type.GetType("ithappy.Battle_Stadium.RotationScript, ithappy.Battle_Stadium");
                if (spin == null) { Debug.LogWarning("[Workshop] Battle Stadium's RotationScript is missing: car 3's fans stand still."); return; }
                var c = t.gameObject.GetComponent(spin);
                if (c == null) c = t.gameObject.AddComponent(spin);
                var so = new SerializedObject(c);
                var axis = so.FindProperty("rotationAxis");
                if (axis != null) axis.enumValueIndex = 2;   // Z: the fans face backwards
                var speed = so.FindProperty("rotationSpeed");
                if (speed != null) speed.floatValue = 900f;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private static bool Has(string s, string part) => s.IndexOf(part, System.StringComparison.OrdinalIgnoreCase) >= 0;

        // ---------- the boost set's parts (D-0037) ----------

        /// <summary>What every car carries for the boosts: the Cyber pack demo's exhaust flames and tyre smoke, and three toy
        /// sounds.</summary>
        private sealed class CarExtras
        {
            public GameObject BoostFx, DriftFx;
            public AudioClip BoostClip, WindClip, LaunchClip;
        }

        private static CarExtras LoadCarExtras() => new CarExtras
        {
            BoostFx = Load(CyberDemo + "BoostFX.prefab"),
            DriftFx = Load(CyberDemo + "DriftFX.prefab"),
            BoostClip = Clip(ToySounds + "Steam_Burst.wav"),     // the nitro whoosh
            WindClip = Clip(PropSounds + "Safe_Dial_v1.wav"),    // a dial's clicking ratchet: the toy's key being wound (his ear decides)
            LaunchClip = Clip(ToySounds + "Spring_Boing.wav"),   // the spring let go
        };

        /// <summary>Fields ArcadeCar does not have yet, met during this build (the boost change adds them).</summary>
        private static readonly HashSet<string> MissingFields = new HashSet<string>();

        /// <summary>The boost set's parts on a car, set by field name so this builds before ArcadeCar has them; a missing
        /// field is skipped and listed once at the end of the build.</summary>
        private static void SetBoostParts(Component car, CarExtras x, Transform windKey)
        {
            var so = new SerializedObject(car);
            Put(so, "boostFxPrefab", x.BoostFx);
            Put(so, "driftFxPrefab", x.DriftFx);
            Put(so, "boostClip", x.BoostClip);
            Put(so, "windClip", x.WindClip);
            Put(so, "launchClip", x.LaunchClip);
            Put(so, "windKey", windKey);
            so.ApplyModifiedPropertiesWithoutUndo();
            if (car is ArcadeCar arcade) FitEngine(arcade);
        }

        /// <summary>The car's engine: a looping AudioSource on a child named Engine, which ArcadeCar starts when someone drives
        /// and pitches by speed. It carries no clip: the car gives it ArcadeCarSettings.engineClip or the hum made by code
        /// (the pack's motor loops whirr above 1 kHz and cannot hum). Heard in the world (3D). Safe to call again.</summary>
        private static void FitEngine(ArcadeCar car)
        {
            var holder = car.transform.Find("Engine");
            if (holder == null) { holder = new GameObject("Engine").transform; holder.SetParent(car.transform, false); }
            if (!holder.TryGetComponent(out AudioSource source)) source = holder.gameObject.AddComponent<AudioSource>();
            source.clip = null; source.loop = true; source.playOnAwake = false;
            source.spatialBlend = 1f; source.dopplerLevel = 0f; source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = CarLength * 2f; source.maxDistance = CarLength * 40f;
            Set(car, "engine", source);
        }

        /// <summary>Brings the car pieces already built up to date without rebuilding them: every ArcadeCar piece gets its
        /// engine sound and a body box no lower than <see cref="BodyClearance"/>. Nothing else on a piece is touched.</summary>
        [MenuItem("Shift Jam/Workshop/Refit Cars (engine sound, body clearance)")]
        public static void RefitCars()
        {
            int cars = 0, raised = 0, engines = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { PieceVehicles }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    if (!root.TryGetComponent(out ArcadeCar car)) continue;
                    cars++;
                    if (root.TryGetComponent(out BoxCollider box) && !box.isTrigger)
                    {
                        float bottom = box.center.y - box.size.y * 0.5f, top = box.center.y + box.size.y * 0.5f;
                        if (bottom < BodyClearance - 1e-4f)
                        {
                            box.center = new Vector3(box.center.x, (BodyClearance + top) * 0.5f, box.center.z);
                            box.size = new Vector3(box.size.x, Mathf.Max(0.1f, top - BodyClearance), box.size.z);
                            raised++;
                            Debug.Log($"[Workshop] {root.name}: body raised from {bottom:F2} to {BodyClearance:F2} over the ground.");
                        }
                    }
                    FitEngine(car); engines++;
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"[Workshop] REFIT CARS: {cars} cars, {raised} bodies raised, {engines} engines fitted.");
        }

        /// <summary>The boost set's parts on one car (an ArcadeCar or the wooden ToyCar). CourseBuilder calls it for the
        /// wooden cars it makes.</summary>
        internal static void WireBoostParts(Component car, Transform windKey = null)
        {
            MissingFields.Clear();
            SetBoostParts(car, LoadCarExtras(), windKey);
            if (MissingFields.Count > 0) Debug.LogWarning($"[Workshop] {car.GetType().Name} has no {string.Join(", ", MissingFields)}: boost parts not set.");
        }

        /// <summary>Boost parts on the cars in the open scenes that are not piece copies: the wooden ToyCars (scene objects)
        /// and any ArcadeCar built straight into a scene. Piece copies take theirs from the prefab. Safe to run again.</summary>
        [MenuItem("Shift Jam/Workshop/Wire Car Boost Parts (open scenes)")]
        public static void WireSceneCars()
        {
            var x = LoadCarExtras(); MissingFields.Clear();
            var names = new List<string>();
            for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
            {
                var s = UnityEngine.SceneManagement.SceneManager.GetSceneAt(i); if (!s.isLoaded) continue;
                bool any = false;
                foreach (var root in s.GetRootGameObjects())
                {
                    foreach (var c in root.GetComponentsInChildren<ToyCar>(true)) { SetBoostParts(c, x, null); names.Add(s.name + "/" + c.name); any = true; }
                    foreach (var c in root.GetComponentsInChildren<ArcadeCar>(true))
                    {
                        if (PrefabUtility.IsPartOfPrefabInstance(c)) continue;
                        SetBoostParts(c, x, null); names.Add(s.name + "/" + c.name); any = true;
                    }
                }
                if (any) EditorSceneManager.MarkSceneDirty(s);
            }
            if (MissingFields.Count > 0) Debug.LogWarning($"[Workshop] Some cars have no {string.Join(", ", MissingFields)} yet: land the boost change first.");
            Debug.Log($"[Workshop] Boost parts on {names.Count} scene car(s): {string.Join(", ", names)}. Save the scene(s).");
        }

        private static void Put(SerializedObject so, string field, Object value)
        {
            var p = so.FindProperty(field);
            if (p == null) { MissingFields.Add(field); return; }
            if (p.propertyType != SerializedPropertyType.ObjectReference) { Debug.LogWarning($"[Workshop] {so.targetObject.GetType().Name}.{field} is not an object field: left alone."); return; }
            p.objectReferenceValue = value;
            if (value != null && p.objectReferenceValue != value) Debug.LogWarning($"[Workshop] {so.targetObject.GetType().Name}.{field} does not take a {value.GetType().Name}: left empty.");
        }

        private static AudioClip Clip(string path)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            if (clip == null) Debug.LogWarning("[Workshop] Missing pack sound, skipped: " + path);
            return clip;
        }

        // ---------- the car cannon (D-0037, boost E) ----------

        private const float CannonScale = 1.25f;   // the pack's cannon_001 at this size: a toy car fits its bore
        private const float CannonPitch = 35f;     // barrel elevation, degrees
        private const float BoreBottom = 1.46f;    // where the bore ends inside the barrel, in the barrel mesh's z (measured 6 Nov; the mouth is the mesh's front)

        /// <summary>
        /// Battle Stadium's cannon_001 as a Build Mode piece: barrel up 35 degrees, resting on its back end with the trunnions
        /// over the pivot and the mouth facing +Z. A trigger zone on the floor under and ahead of the mouth loads a driven
        /// car (CarCannon); a capsule each for the barrel and the trunnion axle make it solid. The turned barrel's box reaches
        /// below its real bottom, so a RestSink keeps Build Mode from standing it on that box.
        /// </summary>
        private static bool BuildCannon()
        {
            var model = Load(BS + "Other/cannon_001.prefab");
            if (model == null) return false;
            var root = new GameObject("CarCannon");
            var holder = new GameObject("Model").transform;
            holder.SetParent(root.transform, false);
            var inst = Place(model, holder);
            inst.transform.localScale *= CannonScale;
            Transform barrelModel = null;
            foreach (var t in inst.GetComponentsInChildren<Transform>(true))
                if (t != inst.transform && t.name.StartsWith("cannon_002")) { barrelModel = t; break; }
            var barrelMesh = barrelModel != null ? barrelModel.GetComponent<MeshFilter>() : null;
            if (barrelMesh == null || barrelMesh.sharedMesh == null)
            {
                Debug.LogError("[Workshop] cannon_001 has no cannon_002 barrel mesh: CarCannon not built.");
                Object.DestroyImmediate(root);
                return false;
            }
            barrelModel.localRotation = Quaternion.Euler(-CannonPitch, 0f, 0f) * barrelModel.localRotation;   // mouth up

            // Its real lowest point (the barrel's back end) on the floor, the trunnions over the pivot.
            if (!LowestPoint(root.transform, inst, out float low)) low = LocalBounds(root.transform, inst).min.y;
            inst.transform.localPosition = new Vector3(0f, -low, 0f);
            float drawnBottom = LocalBounds(root.transform, inst).min.y;   // what Build Mode stands it on
            if (drawnBottom < -0.01f) root.AddComponent<RestSink>().Set(-drawnBottom);

            // The barrel's frame at the trunnions, unscaled: the muzzle and the barrel's collider live in it.
            var barrel = new GameObject("Barrel").transform;
            barrel.SetParent(root.transform, false);
            barrel.SetPositionAndRotation(barrelModel.position, barrelModel.rotation);
            Bounds mb = barrelMesh.sharedMesh.bounds;
            float k = barrelModel.lossyScale.z;
            var muzzle = new GameObject("Muzzle").transform;
            muzzle.SetParent(barrel, false);
            muzzle.localPosition = new Vector3(0f, 0f, mb.max.z * k);
            var shell = barrel.gameObject.AddComponent<CapsuleCollider>();
            shell.direction = 2;
            shell.center = new Vector3(0f, 0f, mb.center.z * k);
            shell.radius = Mathf.Min(mb.extents.x, mb.extents.y) * 0.9f * k;   // between the fat breech and the mouth
            shell.height = mb.size.z * k;
            var solid = new List<Object> { shell };
            var axleMesh = inst.GetComponent<MeshFilter>();
            if (axleMesh != null && axleMesh.sharedMesh != null)
            {
                Bounds ab = axleMesh.sharedMesh.bounds;   // the trunnion axle and its two rims, across the barrel (X)
                var axle = new GameObject("Axle").transform;
                axle.SetParent(root.transform, false);
                axle.position = inst.transform.TransformPoint(ab.center);
                var cap = axle.gameObject.AddComponent<CapsuleCollider>();
                cap.direction = 0;
                cap.radius = ab.extents.y * CannonScale;
                cap.height = ab.size.x * CannonScale;
                solid.Add(cap);
            }

            // The loading zone: on the floor, from a car length behind the mouth (under the barrel, where a car that ran
            // into it stops) to a bit over a car length ahead, as wide as the barrel, taller than any toy car.
            Vector3 mouth = muzzle.position;   // root space: the root sits at the origin, unturned
            float back = mouth.z - CarLength, front = mouth.z + CarLength * 1.2f, height = 3.2f;
            var zone = root.AddComponent<BoxCollider>();
            zone.isTrigger = true;
            zone.center = new Vector3(0f, height * 0.5f, (back + front) * 0.5f);
            zone.size = new Vector3(2f * mb.extents.x * k, height, front - back);

            var audio = root.AddComponent<AudioSource>();
            audio.playOnAwake = false;
            audio.spatialBlend = 0.7f;
            audio.rolloffMode = AudioRolloffMode.Linear;
            audio.minDistance = 4f;
            audio.maxDistance = 300f;

            var cannon = root.AddComponent<CarCannon>();
            Set(cannon, "muzzle", muzzle);
            Set(cannon, "boreDepth", (mb.max.z - BoreBottom) * k);
            Set(cannon, "barrelVisual", barrelModel);
            SetArray(cannon, "solid", solid);
            Set(cannon, "smokePrefab", Load(CarDamageFxBuilder.CannonSmoke));   // D-0059: the Playroom smoke drew pink in URP
            Set(cannon, "loadClip", Clip(ToySounds + "Chess_Drop_In_Box.wav"));   // dropped into the barrel
            Set(cannon, "fuseClip", Clip(ToySounds + "Star_Light_On.wav"));       // a rising shimmer while the fuse burns
            Set(cannon, "boomClip", Clip(ToySounds + "Toy_Tank_Shot_v1.wav"));    // a toy tank's shot
            Save(root, PieceTraversal + "/CarCannon.prefab");
            return true;
        }

        /// <summary>The lowest vertex of the model's meshes in <paramref name="space"/> (a turned mesh's box reaches lower
        /// than the mesh). Pack meshes are not Read/Write, but the editor can read them.</summary>
        private static bool LowestPoint(Transform space, GameObject go, out float lowest)
        {
            lowest = 0f; bool any = false;
            foreach (var f in go.GetComponentsInChildren<MeshFilter>())
            {
                if (f.sharedMesh == null) continue;
                Matrix4x4 m = space.worldToLocalMatrix * f.transform.localToWorldMatrix;
                foreach (var v in f.sharedMesh.vertices)
                {
                    float y = m.MultiplyPoint3x4(v).y;
                    if (!any || y < lowest) { lowest = y; any = true; }
                }
            }
            return any;
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

        internal static void SetArray(Object target, string field, List<Object> values)
        {
            var so = new SerializedObject(target);
            var p = so.FindProperty(field);
            if (p == null || !p.isArray) { Debug.LogError($"[Workshop] {target.GetType().Name} has no array '{field}'."); return; }
            p.arraySize = values.Count;
            for (int i = 0; i < values.Count; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetBools(Object target, string field, List<bool> values)
        {
            var so = new SerializedObject(target);
            var p = so.FindProperty(field);
            if (p == null || !p.isArray) { Debug.LogError($"[Workshop] {target.GetType().Name} has no array '{field}'."); return; }
            p.arraySize = values.Count;
            for (int i = 0; i < values.Count; i++) p.GetArrayElementAtIndex(i).boolValue = values[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ---------- helpers ----------

        internal static GameObject Load(string path)
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (go == null) Debug.LogWarning("[Workshop] Missing pack prefab, skipped: " + path);
            return go;
        }

        /// <summary>An instance of a pack prefab under <paramref name="parent"/>, its own colliders switched off (ours replace them).</summary>
        internal static GameObject Place(GameObject prefab, Transform parent)
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

        internal static Bounds LocalBounds(Transform space, GameObject go)
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

        /// <summary>The frontmost vertex (largest z) of the model's meshes, in <paramref name="space"/>: a rifle's tip.
        /// Pack meshes are not Read/Write, but the editor can read them (CourseBuilder.Wedge does too).</summary>
        private static bool FrontPoint(Transform space, GameObject go, out Vector3 front)
        {
            front = Vector3.zero; bool any = false;
            foreach (var f in go.GetComponentsInChildren<MeshFilter>())
            {
                if (f.sharedMesh == null) continue;
                foreach (var v in f.sharedMesh.vertices)
                {
                    Vector3 p = space.InverseTransformPoint(f.transform.TransformPoint(v));
                    if (!any || p.z > front.z) { front = p; any = true; }
                }
            }
            return any;
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

        internal static GameObject Save(GameObject root, string path)
        {
            var saved = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return saved;
        }

        internal static T SettingsAsset<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) return asset;
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        /// <summary>Orange foam, for the dart's plain capsule: a copy of our own lit material (ShiftCore), recoloured. No shader lookups.</summary>
        internal static Material DartMaterial()
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

        internal static void EnsureFolder(string path)
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
        internal static void Set(Object target, string field, object value)
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
