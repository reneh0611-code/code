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
        [SerializeField] private bool lockVisualRootXZ = true;
        [SerializeField] private float visualGroundOffset = 0.015f;
        [SerializeField] private float groundProbeHeight = 2.5f;
        [SerializeField] private float groundProbeDistance = 6f;
        [SerializeField] private float downwardCorrectionSpeed = 18f;
        [SerializeField] private float upwardCorrectionSpeed = 8f;
        [SerializeField] private float maxVerticalCorrection = 0.75f;

        private Transform _leftUpperArm;
        private Transform _leftLowerArm;
        private Transform _rightUpperArm;
        private Transform _rightLowerArm;
        private Transform _leftFoot;
        private Transform _rightFoot;

        private bool _ready;
        private float _baseLocalX;
        private float _baseLocalY;
        private float _baseLocalZ;

        private IEnumerator Start()
        {
            ResolveReferences();

            yield return null;
            yield return new WaitForEndOfFrame();
            yield return new WaitForEndOfFrame();

            CacheHumanoidBones();

            if (forceGrounding)
                SnapInitialFeetToGround();

            if (modelRoot != null)
            {
                _baseLocalX = modelRoot.localPosition.x;
                _baseLocalY = modelRoot.localPosition.y;
                _baseLocalZ = modelRoot.localPosition.z;
            }

            _ready = true;
        }

        private void LateUpdate()
        {
            if (!_ready)
                return;

            bool hasRealAnimation = animator != null && animator.runtimeAnimatorController != null;

            if (applyRelaxedPoseWithoutController && !hasRealAnimation)
                ApplyNaturalStandingArms();

            if (lockVisualRootXZ)
                LockModelRootXZ();

            if (forceGrounding && hasRealAnimation)
                KeepAnimatedFeetOnGround();
        }

        private void ResolveReferences()
        {
            if (animator == null)
                animator = GetComponentInChildren<Animator>(true);
            if (characterController == null)
                characterController = GetComponent<CharacterController>();
            if (modelRoot == null && animator != null)
                modelRoot = animator.transform;
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

        private void LockModelRootXZ()
        {
            if (modelRoot == null)
                return;

            Vector3 local = modelRoot.localPosition;
            local.x = _baseLocalX;
            local.z = _baseLocalZ;
            modelRoot.localPosition = local;
        }

        private void KeepAnimatedFeetOnGround()
        {
            if (modelRoot == null || _leftFoot == null || _rightFoot == null)
                return;

            if (!TryGetGroundY(out float groundY))
                return;

            // Use the lower of the two actual humanoid feet. Unlike renderer bounds,
            // this does not jump when hands/clothes/other mesh parts change the bounds.
            float lowerFootY = Mathf.Min(_leftFoot.position.y, _rightFoot.position.y);
            float desiredFootY = groundY + visualGroundOffset;
            float worldCorrection = desiredFootY - lowerFootY;

            // Prevent a broken clip/root transform from ever throwing the visual model far away.
            worldCorrection = Mathf.Clamp(worldCorrection, -maxVerticalCorrection, maxVerticalCorrection);

            Vector3 local = modelRoot.localPosition;
            float targetLocalY = local.y + worldCorrection / Mathf.Max(0.0001f, transform.lossyScale.y);

            float minAllowedY = _baseLocalY - maxVerticalCorrection;
            float maxAllowedY = _baseLocalY + maxVerticalCorrection;
            targetLocalY = Mathf.Clamp(targetLocalY, minAllowedY, maxAllowedY);

            // Falling back toward the floor must be quick so Walk/Run never hover.
            // Moving upward is intentionally slower to avoid visible pumping each stride.
            float speed = worldCorrection < 0f ? downwardCorrectionSpeed : upwardCorrectionSpeed;
            local.y = Mathf.Lerp(local.y, targetLocalY, 1f - Mathf.Exp(-speed * Time.deltaTime));
            modelRoot.localPosition = local;
        }

        private void SnapInitialFeetToGround()
        {
            if (modelRoot == null)
                return;

            if (_leftFoot != null && _rightFoot != null && TryGetGroundY(out float groundY))
            {
                float lowerFootY = Mathf.Min(_leftFoot.position.y, _rightFoot.position.y);
                modelRoot.position += Vector3.up * ((groundY + visualGroundOffset) - lowerFootY);
                return;
            }

            // Fallback only for a non-humanoid/unavailable-foot setup.
            if (!TryGetVisualBounds(out Bounds bounds) || !TryGetGroundY(out float fallbackGroundY))
                return;

            modelRoot.position += Vector3.up * ((fallbackGroundY + visualGroundOffset) - bounds.min.y);
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
