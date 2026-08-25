using System.Collections;
using System.Linq;
using UnityEngine;

namespace CheatOnYourDayOnes.Player
{
    public sealed class MixamoRuntimePoseAndGrounder : MonoBehaviour
    {
        [SerializeField] private Animator animator;
        [SerializeField] private Transform modelRoot;
        [SerializeField] private CharacterController characterController;
        [SerializeField] private bool applyRelaxedPoseWithoutController = true;
        [SerializeField] private bool forceGrounding = true;
        [SerializeField] private float visualGroundOffset = 0.01f;
        [SerializeField] private float groundProbeHeight = 2.5f;
        [SerializeField] private float groundProbeDistance = 6f;

        private Transform _leftUpperArm;
        private Transform _leftLowerArm;
        private Transform _rightUpperArm;
        private Transform _rightLowerArm;
        private Transform _leftFoot;
        private Transform _rightFoot;

        private IEnumerator Start()
        {
            ResolveReferences();

            // Let the Animator initialize and enter Idle before measuring the feet.
            yield return null;
            yield return new WaitForEndOfFrame();
            yield return new WaitForEndOfFrame();

            CacheHumanoidBones();

            bool hasRealAnimation = animator != null && animator.runtimeAnimatorController != null;
            if (applyRelaxedPoseWithoutController && !hasRealAnimation)
                ApplyNaturalStandingArms();

            if (forceGrounding)
                SnapFeetToGroundOnce();
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

        private Animator FindAjAnimator()
        {
            Animator[] animators = GetComponentsInChildren<Animator>(true);
            foreach (Animator candidate in animators)
            {
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

        private void CacheHumanoidBones()
        {
            if (animator == null || animator.avatar == null || !animator.avatar.isHuman)
                return;

            _leftUpperArm = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            _leftLowerArm = animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
            _rightUpperArm = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            _rightLowerArm = animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
            _leftFoot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
            _rightFoot = animator.GetBoneTransform(HumanBodyBones.RightFoot);
        }

        private void ApplyNaturalStandingArms()
        {
            PoseArm(_leftUpperArm, _leftLowerArm, new Vector3(-0.08f, -1f, 0.07f), new Vector3(-0.03f, -1f, 0.12f));
            PoseArm(_rightUpperArm, _rightLowerArm, new Vector3(0.08f, -1f, 0.07f), new Vector3(0.03f, -1f, 0.12f));
        }

        private void PoseArm(Transform upperArm, Transform lowerArm, Vector3 upperDesiredLocal, Vector3 lowerDesiredLocal)
        {
            if (upperArm == null || lowerArm == null)
                return;

            Vector3 currentUpperDirection = (lowerArm.position - upperArm.position).normalized;
            Vector3 desiredUpperDirection = transform.TransformDirection(upperDesiredLocal.normalized);
            if (currentUpperDirection.sqrMagnitude > 0.001f)
            {
                Quaternion correction = Quaternion.FromToRotation(currentUpperDirection, desiredUpperDirection);
                upperArm.rotation = correction * upperArm.rotation;
            }

            Transform hand = lowerArm.childCount > 0 ? lowerArm.GetChild(0) : null;
            if (hand == null)
                return;

            Vector3 currentLowerDirection = (hand.position - lowerArm.position).normalized;
            Vector3 desiredLowerDirection = transform.TransformDirection(lowerDesiredLocal.normalized);
            if (currentLowerDirection.sqrMagnitude > 0.001f)
            {
                Quaternion correction = Quaternion.FromToRotation(currentLowerDirection, desiredLowerDirection);
                lowerArm.rotation = correction * lowerArm.rotation;
            }
        }

        private void SnapFeetToGroundOnce()
        {
            if (modelRoot == null)
                return;

            if (!TryGetGroundY(out float groundY))
                return;

            float currentFootY;
            if (_leftFoot != null && _rightFoot != null)
            {
                currentFootY = Mathf.Min(_leftFoot.position.y, _rightFoot.position.y);
            }
            else if (TryGetVisualBounds(out Bounds bounds))
            {
                currentFootY = bounds.min.y;
            }
            else
            {
                return;
            }

            float deltaY = (groundY + visualGroundOffset) - currentFootY;
            modelRoot.position += Vector3.up * deltaY;

            Debug.Log($"[CYDOY] AJ grounded once by {deltaY:F3}m. No per-frame height correction will run.", this);
        }

        private bool TryGetGroundY(out float groundY)
        {
            groundY = 0f;

            Vector3 probeOrigin = transform.position + Vector3.up * groundProbeHeight;
            RaycastHit[] hits = Physics.RaycastAll(
                    probeOrigin,
                    Vector3.down,
                    groundProbeDistance,
                    ~0,
                    QueryTriggerInteraction.Ignore)
                .OrderBy(hit => hit.distance)
                .ToArray();

            foreach (RaycastHit hit in hits)
            {
                if (hit.transform == null)
                    continue;

                if (hit.transform == transform || hit.transform.IsChildOf(transform))
                    continue;

                groundY = hit.point.y;
                return true;
            }

            return false;
        }

        private bool TryGetVisualBounds(out Bounds bounds)
        {
            Renderer[] renderers = modelRoot.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                bounds = default;
                return false;
            }

            bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);

            return true;
        }
    }
}
