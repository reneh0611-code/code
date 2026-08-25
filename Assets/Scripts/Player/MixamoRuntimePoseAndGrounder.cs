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
        [SerializeField] private float visualGroundOffset = 0.008f;
        [SerializeField] private float groundProbeHeight = 2.5f;
        [SerializeField] private float groundProbeDistance = 6f;

        private Transform _leftUpperArm;
        private Transform _leftLowerArm;
        private Transform _rightUpperArm;
        private Transform _rightLowerArm;
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

            bool hasRealAnimation = animator != null && animator.runtimeAnimatorController != null;
            if (applyRelaxedPoseWithoutController && !hasRealAnimation)
                ApplyNaturalStandingArms();

            // Ground the visual ONCE. Re-grounding every animation frame from renderer
            // bounds makes the complete character bob because the animated feet change bounds.min.y.
            if (forceGrounding)
                SnapFeetExactlyToGround();

            if (modelRoot != null)
            {
                _baseLocalX = modelRoot.localPosition.x;
                _baseLocalY = modelRoot.localPosition.y;
                _baseLocalZ = modelRoot.localPosition.z;
            }

            CacheArmBones();
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
                LockModelRootPosition();
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

        private void CacheArmBones()
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

        private void LockModelRootPosition()
        {
            if (modelRoot == null)
                return;

            // Player/world movement happens on the parent CharacterController. The visual child
            // therefore does not need animation-driven root translation on any axis.
            modelRoot.localPosition = new Vector3(_baseLocalX, _baseLocalY, _baseLocalZ);
        }

        public void SnapFeetExactlyToGround()
        {
            if (modelRoot == null)
                return;

            if (!TryGetVisualBounds(out Bounds bounds))
                return;

            Vector3 probeOrigin = new(
                transform.position.x,
                Mathf.Max(bounds.max.y + 0.20f, transform.position.y + groundProbeHeight),
                transform.position.z);

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

                float desiredSoleY = hit.point.y + visualGroundOffset;
                float deltaY = desiredSoleY - bounds.min.y;

                if (Mathf.Abs(deltaY) > 0.0005f)
                    modelRoot.position += Vector3.up * deltaY;

                return;
            }
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
