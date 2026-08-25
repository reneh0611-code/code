using System.Collections;
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

        private IEnumerator Start()
        {
            if (animator == null)
                animator = GetComponentInChildren<Animator>(true);
            if (characterController == null)
                characterController = GetComponent<CharacterController>();
            if (modelRoot == null && animator != null)
                modelRoot = animator.transform;

            // Wait for skinned mesh bounds and humanoid bones to be fully initialized.
            yield return null;
            yield return new WaitForEndOfFrame();

            if (applyRelaxedPoseWithoutController && animator != null && animator.runtimeAnimatorController == null)
                ApplyRelaxedPose();

            if (forceGrounding)
                SnapVisualFeetToControllerBottom();
        }

        private void ApplyRelaxedPose()
        {
            if (animator.avatar == null || !animator.avatar.isHuman)
                return;

            RotateBone(HumanBodyBones.LeftUpperArm, new Vector3(0f, 0f, 52f));
            RotateBone(HumanBodyBones.RightUpperArm, new Vector3(0f, 0f, -52f));
            RotateBone(HumanBodyBones.LeftLowerArm, new Vector3(0f, 0f, 10f));
            RotateBone(HumanBodyBones.RightLowerArm, new Vector3(0f, 0f, -10f));

            RotateBone(HumanBodyBones.LeftUpperLeg, new Vector3(2f, 0f, -3f));
            RotateBone(HumanBodyBones.RightUpperLeg, new Vector3(-2f, 0f, 3f));
            RotateBone(HumanBodyBones.LeftLowerLeg, new Vector3(-2f, 0f, 0f));
            RotateBone(HumanBodyBones.RightLowerLeg, new Vector3(-2f, 0f, 0f));
        }

        private void RotateBone(HumanBodyBones bone, Vector3 localEulerDelta)
        {
            Transform t = animator.GetBoneTransform(bone);
            if (t == null)
                return;

            t.localRotation *= Quaternion.Euler(localEulerDelta);
        }

        public void SnapVisualFeetToControllerBottom()
        {
            if (modelRoot == null || characterController == null)
                return;

            if (!TryGetVisualBounds(out Bounds bounds))
                return;

            float desiredBottomWorldY = transform.TransformPoint(
                characterController.center + Vector3.down * (characterController.height * 0.5f)).y;

            float delta = desiredBottomWorldY - bounds.min.y;
            if (Mathf.Abs(delta) <= groundEpsilon)
                return;

            modelRoot.position += Vector3.up * delta;
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
