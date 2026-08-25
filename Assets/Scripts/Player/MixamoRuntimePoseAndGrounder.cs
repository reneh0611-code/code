using System.Collections;
using UnityEngine;

namespace CheatOnYourDayOnes.Player
{
    /// <summary>
    /// Aligns the whole rendered AJ model to the CharacterController bottom once.
    /// It never edits bones, Animator state, animation curves or pose data.
    /// </summary>
    public sealed class MixamoRuntimePoseAndGrounder : MonoBehaviour
    {
        [SerializeField] private Animator animator;
        [SerializeField] private Transform modelRoot;
        [SerializeField] private CharacterController characterController;
        [SerializeField, Min(0)] private int settleFrames = 2;
        [SerializeField] private float soleOffset = 0f;

        private IEnumerator Start()
        {
            ResolveReferences();

            if (modelRoot == null || characterController == null)
                yield break;

            // Wait until the Animator has evaluated the real Idle pose and renderer bounds are current.
            for (int i = 0; i < settleFrames; i++)
                yield return null;

            AlignWholeVisualToControllerBottom();

            // Important: never touch the animation again after the one-time visual offset.
            enabled = false;
        }

        private void ResolveReferences()
        {
            if (animator == null)
                animator = FindAjAnimator();

            if (characterController == null)
                characterController = GetComponent<CharacterController>();

            if (modelRoot == null && animator != null)
                modelRoot = FindModelRoot(animator.transform);
        }

        private void AlignWholeVisualToControllerBottom()
        {
            Renderer[] renderers = modelRoot.GetComponentsInChildren<Renderer>(true);
            if (renderers == null || renderers.Length == 0)
            {
                Debug.LogWarning("[CYDOY] Ground alignment skipped: AJ has no renderers.", this);
                return;
            }

            bool hasBounds = false;
            Bounds combined = default;

            foreach (Renderer renderer in renderers)
            {
                if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy)
                    continue;

                if (!hasBounds)
                {
                    combined = renderer.bounds;
                    hasBounds = true;
                }
                else
                {
                    combined.Encapsulate(renderer.bounds);
                }
            }

            if (!hasBounds)
                return;

            float controllerBottomY = transform.position.y
                + characterController.center.y
                - characterController.height * 0.5f;

            float desiredVisualBottomY = controllerBottomY + soleOffset;
            float deltaY = desiredVisualBottomY - combined.min.y;

            modelRoot.position += Vector3.up * deltaY;

            Debug.Log(
                $"[CYDOY] AJ visual grounded once. VisualBottom={combined.min.y:F4}, " +
                $"ControllerBottom={controllerBottomY:F4}, DeltaY={deltaY:F4}. Animation untouched.",
                this);
        }

        private Animator FindAjAnimator()
        {
            Animator[] animators = GetComponentsInChildren<Animator>(true);
            foreach (Animator candidate in animators)
            {
                if (candidate == null)
                    continue;

                Transform t = candidate.transform;
                while (t != null && t != transform)
                {
                    if (t.name == "Mixamo_AJ")
                        return candidate;
                    t = t.parent;
                }
            }

            return animators.Length > 0 ? animators[0] : null;
        }

        private Transform FindModelRoot(Transform animatorTransform)
        {
            Transform t = animatorTransform;
            while (t != null && t != transform)
            {
                if (t.name == "Mixamo_AJ")
                    return t;
                t = t.parent;
            }

            return animatorTransform;
        }
    }
}
