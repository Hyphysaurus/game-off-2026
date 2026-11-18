using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ShiftJam
{
    /// <summary>
    /// The player's own body as first person sees it, after the test room's: the same body, with its head shrunk to
    /// nothing so the eye inside it can see out, stood so the head is just under and behind the eye. First person hides
    /// the whole player (FirstPersonMode); while a gun is in the hands this draws the body again, so the arms that hold
    /// the gun are in view. It casts no shadow (a headless one would show). Hidden again, everything is as it was found.
    /// Not a component: the WeaponHolder owns one, shows it while a blaster is out in first person and stands it each
    /// frame after the Animator and the camera.
    /// </summary>
    public sealed class ViewBody
    {
        private const float Gone = 0.001f;   // the head's size while hidden: small enough to be nothing, never zero

        /// <summary>One drawn part of the body and how it was before it was shown.</summary>
        private struct Part { public Renderer renderer; public ShadowCastingMode shadows; public bool offscreen; }

        private readonly Animator _animator;
        private readonly Transform _model, _head;
        private readonly List<Part> _parts = new List<Part>();
        private readonly List<Renderer> _found = new List<Renderer>();
        private Vector3 _modelWas, _headWas;
        private AnimatorCullingMode _cullingWas;

        /// <summary>False when the body has no humanoid head to hide: it is never shown.</summary>
        public bool Ready => _model != null && _head != null;
        /// <summary>True while the body is drawn for the first-person view.</summary>
        public bool Shown { get; private set; }
        /// <summary>The head bone: the eye is a little above and in front of it.</summary>
        public Transform Head => _head;

        public ViewBody(Animator animator, Transform player)
        {
            if (animator == null || !animator.isHuman) return;
            _animator = animator;
            _head = animator.GetBoneTransform(HumanBodyBones.Head);
            // The part of the player that carries this body: the Animator's own object, or what holds it under the player.
            _model = animator.transform;
            while (_model.parent != null && _model.parent != player) _model = _model.parent;
            if (_model.parent != player) _model = null;
        }

        /// <summary>Draws the body for the first-person view (head hidden, no shadow), or hides it again and puts
        /// everything back. Only the parts first person had hidden are touched.</summary>
        public void Show(bool on)
        {
            if (on == Shown || !Ready) return;
            Shown = on;
            if (on)
            {
                _model.GetComponentsInChildren(false, _found);
                foreach (Renderer r in _found)
                {
                    if (!r.forceRenderingOff) continue;
                    var skin = r as SkinnedMeshRenderer;
                    _parts.Add(new Part { renderer = r, shadows = r.shadowCastingMode, offscreen = skin != null && skin.updateWhenOffscreen });
                    r.forceRenderingOff = false;
                    r.shadowCastingMode = ShadowCastingMode.Off;
                    // The body's box is measured from its real pose: arms held up in front of an eye that looks away
                    // from the rest of the body are still drawn.
                    if (skin != null) skin.updateWhenOffscreen = true;
                }
                _modelWas = _model.localPosition;
                _headWas = _head.localScale;
                _head.localScale = _headWas * Gone;
                _cullingWas = _animator.cullingMode;
                _animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;   // the arms are bent on top of a fresh pose every frame
                return;
            }
            foreach (Part part in _parts)
            {
                if (part.renderer == null) continue;
                part.renderer.forceRenderingOff = true;
                part.renderer.shadowCastingMode = part.shadows;
                if (part.renderer is SkinnedMeshRenderer skin) skin.updateWhenOffscreen = part.offscreen;
            }
            _parts.Clear();
            _model.localPosition = _modelWas;
            _head.localScale = _headWas;
            _animator.cullingMode = _cullingWas;
        }

        /// <summary>Stands the body so its head is under and behind <paramref name="eye"/>, whatever the animation does
        /// with it (a crouch, a step): the shoulders keep their place against the view. <paramref name="lookDown"/> is
        /// how far down the view looks (0 level or up, 1 straight down): the body steps back with it.</summary>
        public void Stand(Vector3 eye, Vector3 facing, float lookDown, WeaponHoldSettings s, float scale)
        {
            if (!Shown) return;
            Vector3 flat = Vector3.ProjectOnPlane(facing, Vector3.up);
            flat = flat.sqrMagnitude > 1e-8f ? flat.normalized : Vector3.forward;
            Vector3 head = eye - Vector3.up * (s.eyeAboveHead * scale) - flat * ((s.eyeForward + s.backLookingDown * Mathf.Clamp01(lookDown)) * scale);
            _model.position += head - _head.position;
        }

        /// <summary>Moves the whole body by <paramref name="by"/>: towards a gun its arm cannot reach.</summary>
        public void Slide(Vector3 by)
        {
            if (Shown) _model.position += by;
        }
    }
}
