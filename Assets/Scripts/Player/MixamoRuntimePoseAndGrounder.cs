using UnityEngine;

namespace CheatOnYourDayOnes.Player
{
    public sealed class MixamoRuntimePoseAndGrounder : MonoBehaviour
    {
        [SerializeField] private Animator animator;
        [SerializeField] private Transform modelRoot;
        [SerializeField] private CharacterController characterController;
        [SerializeField] private bool applyRelaxedPoseWithoutController = true;

        private Transform _leftUpperArm;
        private Transform _leftLowerArm;
        private Transform _rightUpperArm;
        private Transform _rightLowerArm;

        private void Start()
        {
            ResolveReferences();
            CacheHumanoidBones();

            bool hasRealAnimation = animator != null && animator.runtimeAnimatorController != null;
            if (applyRelaxedPoseWithoutController && !hasRealAnimation)
                ApplyNaturalStandingArms();
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
    }
}
