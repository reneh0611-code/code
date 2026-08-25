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
        [SerializeField] private float groundEpsilon = 0.002f;
        [SerializeField] private float visualGroundOffset = 0.005f;
        [SerializeField] private float groundProbeHeight = 3f;
        [SerializeField] private float groundProbeDistance = 8f;

        private IEnumerator Start()
        {
            if (animator == null)
                animator = GetComponentInChildren<Animator>(true);
            if (characterController == null)
                characterController = GetComponent<CharacterController>();
            if (modelRoot == null && animator != null)
                modelRoot = animator.transform;

            // Skinned renderer bounds are not reliable on the first frame.
            yield return null;
            yield return new WaitForEndOfFrame();

            if (applyRelaxedPoseWithoutController && animator != null && animator.runtimeAnimatorController == null)
                ApplyRelaxedPose();

            // Wait one more frame because changing humanoid bones also changes mesh bounds.
            yield return new WaitForEndOfFrame();

            if (forceGrounding)
                SnapVisualFeetToRealGround();
        }

        private void ApplyRelaxedPose()
        {
            if (animator == null || animator.avatar == null || !animator.avatar.isHuman)
                return;

            // Mixamo imports in a T-pose. Rotate the upper arms much farther down
            // so the placeholder stance reads like a normal relaxed idle pose.
            RotateBone(HumanBodyBones.LeftUpperArm, new Vector3(0f, 0f, 82f));
            RotateBone(HumanBodyBones.RightUpperArm, new Vector3(0f, 0f, -82f));
            RotateBone(HumanBodyBones.LeftLowerArm, new Vector3(0f, -4f, 7f));
            RotateBone(HumanBodyBones.RightLowerArm, new Vector3(0f, 4f, -7f));

            RotateBone(HumanBodyBones.LeftUpperLeg, new Vector3(1f, 0f, -2f));
            RotateBone(HumanBodyBones.RightUpperLeg, new Vector3(-1f, 0f, 2f));
            RotateBone(HumanBodyBones.LeftLowerLeg, new Vector3(-1f, 0f, 0f));
            RotateBone(HumanBodyBones.RightLowerLeg, new Vector3(-1f, 0f, 0f));
        }

        private void RotateBone(HumanBodyBones bone, Vector3 localEulerDelta)
        {
            Transform boneTransform = animator.GetBoneTransform(bone);
            if (boneTransform == null)
                return;

            boneTransform.localRotation *= Quaternion.Euler(localEulerDelta);
        }

        public void SnapVisualFeetToRealGround()
        {
            if (modelRoot == null)
                return;

            if (!TryGetVisualBounds(out Bounds bounds))
                return;

            Vector3 probeOrigin = new Vector3(
                bounds.center.x,
                Mathf.Max(bounds.max.y + 0.25f, transform.position.y + groundProbeHeight),
                bounds.center.z);

            RaycastHit[] hits = Physics.RaycastAll(
                    probeOrigin,
                    Vector3.down,
                    groundProbeDistance,
                    ~0,
                    QueryTriggerInteraction.Ignore)
                .OrderBy(hit => hit.distance)
                .ToArray();

            bool foundGround = false;
            float groundY = 0f;

            foreach (RaycastHit hit in hits)
            {
                Transform hitTransform = hit.transform;
                if (hitTransform == null)
                    continue;

                // Skip our own CharacterController/player hierarchy.
                if (hitTransform == transform || hitTransform.IsChildOf(transform))
                    continue;

                groundY = hit.point.y;
                foundGround = true;
                break;
            }

            if (!foundGround)
            {
                Debug.LogWarning("[CYDOY] Could not find ground below Mixamo character.", this);
                return;
            }

            float targetFeetY = groundY + visualGroundOffset;
            float deltaY = targetFeetY - bounds.min.y;
            if (Mathf.Abs(deltaY) <= groundEpsilon)
                return;

            modelRoot.position += Vector3.up * deltaY;
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
