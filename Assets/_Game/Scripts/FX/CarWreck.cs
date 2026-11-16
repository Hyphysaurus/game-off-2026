using System.Collections.Generic;
using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// A wrecked car in pieces (D-0060). The body is swapped for the pieces Fraggo cut it into beforehand (Shift Jam >
    /// Workshop > Build Car Wrecks; CarDamageFxSettings.cuts): most fly apart at the car's own speed, the lowest few stay
    /// on the car as its burnt hulk, the wheels pop off and bounce, and everything else the car draws is hidden. Mend
    /// puts the car back whole and shrinks the pieces away. A body that was never cut breaks nothing: the car stays the
    /// dark whole wreck it was before.
    /// CarDamageFx owns one of these and calls Break when the car dies and Mend when its health is restored.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CarWreck : MonoBehaviour
    {
        private const string HulkName = CarDamageFx.Prefix + "Hulk";
        private const float PieceMass = 0.3f, WheelMass = 0.2f;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly List<LoosePart> Flying = new List<LoosePart>();   // every car's pieces, for the budget
        private static PhysicsMaterial _bouncy;

        // Kept by Unity so a Build Mode copy of a broken car knows what it has to show again.
        [SerializeField, HideInInspector] private List<Renderer> hidden = new List<Renderer>();

        private readonly List<LoosePart> _own = new List<LoosePart>();
        private readonly List<Collider> _made = new List<Collider>();
        private GameObject _hulk;
        private MaterialPropertyBlock _block;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ClearStatics() { Flying.Clear(); _bouncy = null; }

        /// <summary>The car is in pieces now.</summary>
        public bool Broken => hidden.Count > 0;

        /// <summary>
        /// Breaks <paramref name="car"/> up. <paramref name="paint"/> colours the cut faces, <paramref name="worldK"/> is
        /// the car's drawn size against a car 4 long (speeds scale with it) and <paramref name="centre"/> the point the
        /// pieces fly away from. False when the body was never cut (nothing changes).
        /// </summary>
        public bool Break(ArcadeCar car, CarDamageFxSettings s, Color paint, float worldK, Vector3 centre)
        {
            if (Broken || car == null || s == null) return false;
            MeshFilter body = CarDamageFx.BodyOf(transform);
            if (body == null || body.sharedMesh == null || !body.TryGetComponent(out MeshRenderer bodyLook) || !bodyLook.enabled) return false;
            CarDamageFxSettings.Cut cut = s.CutOf(body.sharedMesh.name);
            if (cut == null) return false;

            if (_block == null) _block = new MaterialPropertyBlock();
            Flying.RemoveAll(p => p == null);
            int room = Mathf.Max(0, s.mostPieces - Flying.Count);
            Vector3 carried = car.Body != null ? car.Body.linearVelocity * s.pieceCarry : Vector3.zero;
            Material[] materials = MaterialsOf(bodyLook, cut, s.wreckInside);
            Transform frame = body.transform;
            _made.Clear();

            // the body: the pieces nearest its underside's middle stay as the hulk, the rest fly
            bool[] stays = Hulk(cut, body.sharedMesh.bounds, s.hulkPieces);
            for (int i = 0; i < cut.pieces.Length; i++)
            {
                Mesh mesh = cut.pieces[i];
                if (mesh == null) continue;
                Vector3 at = frame.TransformPoint(cut.centres[i]);
                if (stays[i]) { AddToHulk(mesh, materials, at, frame, s.hulkTint); continue; }
                if (room <= 0) continue;
                room--;
                GameObject piece = Piece("Wreck piece", mesh, materials, at, frame.rotation, frame.lossyScale);
                var look = piece.GetComponent<MeshRenderer>();
                Color inside = paint * s.insideShade; inside.a = 1f;
                _block.Clear(); _block.SetColor(BaseColorId, inside);
                look.SetPropertyBlock(_block, cut.subMeshes);   // the cut faces only
                _made.Add(piece.AddComponent<BoxCollider>());
                Throw(piece, PieceMass, carried, at - centre, s.pieceBurst, worldK, 0.6f, 10f, s.pieceLife);
            }
            bodyLook.enabled = false;
            hidden.Add(bodyLook);

            // the wheels pop off and bounce
            foreach (Transform wheel in car.Wheels)
            {
                if (wheel == null) continue;
                bool fly = room > 0;
                GameObject loose = fly ? new GameObject("Wreck " + wheel.name) : null;
                if (fly) loose.transform.SetPositionAndRotation(wheel.position, wheel.rotation);
                Bounds box = default; bool any = false;
                foreach (var r in wheel.GetComponentsInChildren<MeshRenderer>())
                {
                    if (!r.enabled) continue;
                    if (fly && r.TryGetComponent(out MeshFilter mf) && mf.sharedMesh != null)
                    {
                        GameObject part = Piece(r.name, mf.sharedMesh, r.sharedMaterials, r.transform.position, r.transform.rotation, r.transform.lossyScale);
                        part.transform.SetParent(loose.transform, true);
                        if (any) box.Encapsulate(r.bounds); else { box = r.bounds; any = true; }
                    }
                    r.enabled = false;
                    hidden.Add(r);
                }
                if (!fly) continue;
                if (!any) { Destroy(loose); continue; }
                room--;
                var ball = loose.AddComponent<SphereCollider>();
                ball.center = loose.transform.InverseTransformPoint(box.center);
                ball.radius = Mathf.Max(box.extents.x, box.extents.y, box.extents.z);
                ball.sharedMaterial = Bouncy(s.wheelBounce);
                _made.Add(ball);
                Throw(loose, WheelMass, carried, wheel.position - centre, s.wheelBurst, worldK, 1f, 14f, s.pieceLife);
            }

            // everything else the car draws goes with the body (parts left on it would hang in the air)
            foreach (var r in GetComponentsInChildren<Renderer>())
            {
                if (!r.enabled || !(r is MeshRenderer || r is SkinnedMeshRenderer)) continue;
                if (IsEffect(r.transform) || r.GetComponentInParent<PlayerMotor>(true) != null) continue;
                r.enabled = false;
                hidden.Add(r);
            }

            // the pieces pass through the car they came off and through each other: they only land on the world
            Collider[] own = GetComponentsInChildren<Collider>();
            for (int i = 0; i < _made.Count; i++)
            {
                foreach (var c in own) Physics.IgnoreCollision(c, _made[i]);
                for (int j = i + 1; j < _made.Count; j++) Physics.IgnoreCollision(_made[i], _made[j]);
            }
            _made.Clear();

            Shake(s, centre, worldK);
            return true;
        }

        /// <summary>The car whole again: its own looks shown, the hulk gone, the pieces shrinking away.</summary>
        public void Mend()
        {
            foreach (var r in hidden) if (r != null) r.enabled = true;
            hidden.Clear();
            if (_hulk != null) Destroy(_hulk);
            _hulk = null;
            foreach (var p in _own) if (p != null) p.ShrinkNow();
            _own.Clear();
        }

        private void OnDestroy()
        {
            foreach (var p in _own) if (p != null) p.ShrinkNow();   // the car is gone: its pieces do not outstay it
        }

        // ---------- pieces ----------

        private static Material[] MaterialsOf(MeshRenderer body, CarDamageFxSettings.Cut cut, Material inside)
        {
            Material[] own = body.sharedMaterials;
            int n = Mathf.Max(1, cut.subMeshes);
            var materials = new Material[n + 1];
            for (int i = 0; i < n; i++)
                materials[i] = i < own.Length && own[i] != null ? own[i] : own.Length > 0 ? own[0] : null;
            materials[n] = inside != null ? inside : materials[0];
            return materials;
        }

        /// <summary>Which pieces stay as the hulk: the <paramref name="count"/> whose centres lie nearest the middle of
        /// the body's underside.</summary>
        private static bool[] Hulk(CarDamageFxSettings.Cut cut, Bounds body, int count)
        {
            int n = cut.pieces.Length;
            var stays = new bool[n];
            Vector3 foot = new Vector3(body.center.x, body.min.y, body.center.z);
            for (int k = 0; k < Mathf.Min(count, n - 1); k++)   // at least one piece always flies
            {
                int best = -1; float least = float.MaxValue;
                for (int i = 0; i < n; i++)
                {
                    if (stays[i] || cut.pieces[i] == null) continue;
                    float d = (cut.centres[i] - foot).sqrMagnitude;
                    if (d < least) { least = d; best = i; }
                }
                if (best < 0) break;
                stays[best] = true;
            }
            return stays;
        }

        private static GameObject Piece(string name, Mesh mesh, Material[] materials, Vector3 at, Quaternion rotation, Vector3 scale)
        {
            var go = new GameObject(name);
            go.transform.SetPositionAndRotation(at, rotation);
            go.transform.localScale = scale;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterials = materials;
            return go;
        }

        private void AddToHulk(Mesh mesh, Material[] materials, Vector3 at, Transform frame, Color tint)
        {
            if (_hulk == null)
            {
                _hulk = new GameObject(HulkName);
                _hulk.transform.SetParent(transform, false);
            }
            GameObject piece = Piece(HulkName + " piece", mesh, materials, at, frame.rotation, frame.lossyScale);
            piece.transform.SetParent(_hulk.transform, true);
            _block.Clear(); _block.SetColor(BaseColorId, tint);
            piece.GetComponent<MeshRenderer>().SetPropertyBlock(_block);
        }

        /// <summary>Sends <paramref name="go"/> off: the car's own speed, plus a throw away from the wreck's centre and
        /// upward, tumbling.</summary>
        private void Throw(GameObject go, float mass, Vector3 carried, Vector3 away, Vector2 burst, float worldK, float lift, float spin, float life)
        {
            var rb = go.AddComponent<Rigidbody>();
            rb.mass = mass;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            away = away.sqrMagnitude > 1e-6f ? away.normalized : Random.onUnitSphere;
            Vector3 dir = (away + Vector3.up * lift + Random.insideUnitSphere * 0.35f).normalized;
            rb.linearVelocity = carried + dir * Random.Range(burst.x, burst.y) * worldK;
            rb.angularVelocity = Random.insideUnitSphere * spin;
            var part = go.AddComponent<LoosePart>();
            part.Begin(life, false);
            _own.Add(part);
            Flying.Add(part);
        }

        private static PhysicsMaterial Bouncy(float bounce)
        {
            if (_bouncy == null) _bouncy = new PhysicsMaterial("Wreck wheel") { bounceCombine = PhysicsMaterialCombine.Maximum };
            _bouncy.bounciness = Mathf.Clamp01(bounce);
            return _bouncy;
        }

        /// <summary>Under one of the damage effects' own objects (the smoke, the flecks, the hulk).</summary>
        private bool IsEffect(Transform t)
        {
            for (; t != null && t != transform; t = t.parent)
                if (t.name.StartsWith(CarDamageFx.Prefix)) return true;
            return false;
        }

        /// <summary>Rattles the view by how near the camera is: full at the wreck, nothing past the settings' range.</summary>
        private static void Shake(CarDamageFxSettings s, Vector3 centre, float worldK)
        {
            if (s.wreckShake <= 0f) return;
            Camera cam = Camera.main;
            if (cam == null || !cam.TryGetComponent(out ShoulderCamera view)) return;
            float reach = Mathf.Max(0.01f, s.wreckShakeRange * CarDamageFx.PackCarLength * worldK);
            float near = 1f - Mathf.Clamp01(Vector3.Distance(cam.transform.position, centre) / reach);
            if (near > 0f) view.Shake(s.wreckShake * near, 0.5f);
        }
    }
}
