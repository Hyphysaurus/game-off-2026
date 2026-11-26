using UnityEngine;
using Art = Artifice;

namespace ShiftJam
{
    /// <summary>
    /// A found S-H-I-F-T letter bursts into its cubes (D-0230): when the toy takes a Flat Zone letter (FlatLetter) a copy of
    /// it, standing exactly where the letter stands and wearing its colours at that moment, is cut into its pixel cubes by
    /// Artificer (the cut the title uses: one piece a pixel) and burst (the Explode style: each cube flies from the
    /// letter's middle, tumbles, falls, bounces off the floor and shrinks away); the letter itself shows its ghost at once.
    /// The copy is one object: its renderer, its Artificer, and this, which throws it all away (the cut, its cubes'
    /// meshes, the object) when the last cube is gone. One-off allocations when a letter is taken; nothing every frame.
    /// It runs on game time. OFF SWITCH: BuildFx > Letter Burst On (the letter pops as before).
    /// </summary>
    public sealed class LetterBurst : MonoBehaviour
    {
        private Art.Artificer _art;
        private float _endBy;

        /// <summary>
        /// Bursts a copy of the letter drawn by <paramref name="look"/> (its mesh must be readable; the title's letters are),
        /// in its materials and its per-slot property blocks as they are now. <paramref name="playerLayer"/> (below 0 for
        /// none) is left out of what the cubes bounce off. True when it plays; false when it does not (off, or nothing to
        /// cut), and the letter pops as before.
        /// </summary>
        public static bool Play(Renderer look, int playerLayer)
        {
            BuildFxSettings fx = BuildFxSettings.Load();
            if (!fx.letterBurstOn || look == null || !look.TryGetComponent(out MeshFilter shapeOf)) return false;
            Mesh shape = shapeOf.sharedMesh;
            if (shape == null || !shape.isReadable) return false;
            Transform from = look.transform;
            float height = look.bounds.size.y;
            if (height <= 1e-4f) return false;

            // The copy: one object, the renderer on the Artificer's own object (so Artificer's burst, which sizes a cube by
            // its object's scale and not by anything above it, sees the letter's whole size there), at the letter's place.
            var go = new GameObject("Letter Burst (D-0230)");
            go.layer = look.gameObject.layer;
            go.transform.SetPositionAndRotation(from.position, from.rotation);
            go.transform.localScale = from.lossyScale;
            go.AddComponent<MeshFilter>().sharedMesh = shape;
            var copy = go.AddComponent<MeshRenderer>();
            copy.sharedMaterials = look.sharedMaterials;
            copy.shadowCastingMode = look.shadowCastingMode;
            copy.receiveShadows = look.receiveShadows;
            int slots = copy.sharedMaterials.Length;
            var blocks = new MaterialPropertyBlock[Mathf.Max(1, slots)];
            for (int s = 0; s < blocks.Length; s++)
            {
                blocks[s] = new MaterialPropertyBlock();
                look.GetPropertyBlock(blocks[s], s);
                copy.SetPropertyBlock(blocks[s], s);
            }

            var burst = go.AddComponent<LetterBurst>();
            Art.Artificer a = burst._art = go.AddComponent<Art.Artificer>();
            a.target = go;
            TitleBuildIn.CutByPixel(a);
            a.useUnscaledTime = false;
            a.handleParticles = a.handleLights = a.handleLines = a.handleAudio = false;
            a.boundsMode = Art.BoundsMode.Calc;
            a.seed = Random.Range(1, int.MaxValue);
            a.buildOnAwake = false;
            a.startMode = Art.StartMode.AsIs;
            a.buildMode = Art.BuildMode.Finished;     // Init makes every cube's mesh, standing in place
            a.dismantleStyle = Art.DismantleStyle.Explode;
            a.dismantleTime = Mathf.Max(0.01f, fx.letterBurstSeconds);
            a.dismantleTo = 0f;
            a.removeTimeRange = TitleBuildIn.Range(fx.letterCubeSeconds, 0.05f);
            a.minExplodeForce = Mathf.Min(fx.letterSpeed.x, fx.letterSpeed.y) * height;
            a.maxExplodeForce = Mathf.Max(fx.letterSpeed.x, fx.letterSpeed.y) * height;
            a.explodeOrigin = shape.bounds.center;    // the letter's middle, in its own space
            a.dismantleProjection = Vector3.one;
            a.angVelRange = new Vector3(Mathf.Abs(fx.letterSpin.x), Mathf.Abs(fx.letterSpin.y), Mathf.Abs(fx.letterSpin.z));
            float gravity = Physics.gravity.magnitude;
            a.gravityModifier = gravity > 0.01f ? fx.letterGravity * height / gravity : 0f;
            a.forceRange = new Art.Vector3Range(Vector3.zero);
            a.collisionMode = Art.CollisionMode.Raycast;
            int mask = fx.apartBounceOn.value & ~(1 << go.layer);
            if (playerLayer >= 0 && playerLayer < 32) mask &= ~(1 << playerLayer);
            a.layers = mask;
            a.bounce = Mathf.Clamp01(fx.letterBounce);
            a.linearDrag = 0.1f;
            a.angularDrag = 0.4f;
            a.dismantleRotate = Vector3.zero;
            a.useDisPlaceCurve = false; a.useDisPlaceRotCurve = false;
            a.useDisPlaceScaleCurve = true;
            float shrink = Mathf.Clamp(fx.letterShrinkFrom, 0.01f, 0.99f);
            a.disPlaceScaleCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(shrink, 1f), new Keyframe(1f, 0f));

            bool cut;
            try
            {
                a.Init();
                cut = a.buildData != null && a.buildData.meshes != null && a.buildData.meshes.Count > 0;
            }
            catch (System.Exception e)
            {
                Debug.LogException(e, look);
                cut = false;
            }
            if (!cut)
            {
                Destroy(go);   // OnDestroy throws away whatever the cut made
                return false;
            }
            // Artificer draws a cube with the letter's material only: give every cube the letter's colours (his sheet and
            // the frame it showed) through the blocks above.
            foreach (Art.MeshElement cube in a.buildData.meshes)
            {
                if (cube.rp == null || cube.draw == null) continue;
                for (int m = 0; m < cube.rp.Length && m < cube.draw.Count; m++)
                {
                    int slot = cube.draw[m];
                    if (slot >= 0 && slot < blocks.Length) cube.rp[m].matProps = blocks[slot];
                }
            }
            a.StartDismantle();
            burst._endBy = Time.time + a.dismantleTime + Mathf.Max(fx.letterCubeSeconds.x, fx.letterCubeSeconds.y) + 1f;
            return true;
        }

        private void Update()
        {
            // Every cube gone (or a backstop, should Artificer never say so): the copy goes.
            if (_art == null || _art.IsDismantled() || Time.time > _endBy) Destroy(gameObject);
        }

        private void OnDestroy()
        {
            if (_art == null) return;
            Art.BuildData data = _art.buildData;
            if (data == null) return;
            if (data.meshes != null) foreach (Art.MeshElement cube in data.meshes) if (cube.mesh != null) Destroy(cube.mesh);
            Destroy(data);
            _art.buildData = null;
        }
    }
}
