using UnityEngine;

namespace CheatOnYourDayOnes.Player
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class StylizedCharacterAnimator : MonoBehaviour
    {
        [SerializeField] private Transform leftArm;
        [SerializeField] private Transform rightArm;
        [SerializeField] private Transform leftLeg;
        [SerializeField] private Transform rightLeg;
        [SerializeField] private Transform torso;
        [SerializeField] private float walkSwing = 28f;
        [SerializeField] private float runSwing = 38f;
        [SerializeField] private float animationSpeed = 8f;
        [SerializeField] private float bobAmount = 0.035f;

        private CharacterController _controller;
        private float _phase;
        private Vector3 _torsoBasePosition;

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
            if (torso != null)
                _torsoBasePosition = torso.localPosition;
        }

        private void Update()
        {
            Vector3 planarVelocity = _controller.velocity;
            planarVelocity.y = 0f;
            float speed = planarVelocity.magnitude;
            float normalizedSpeed = Mathf.InverseLerp(0f, 6.8f, speed);

            if (speed > 0.08f)
                _phase += Time.deltaTime * animationSpeed * Mathf.Lerp(0.75f, 1.4f, normalizedSpeed);

            float swingAmount = Mathf.Lerp(walkSwing, runSwing, normalizedSpeed);
            float swing = Mathf.Sin(_phase) * swingAmount * Mathf.Clamp01(speed / 2f);
            float targetReturn = Mathf.Lerp(12f, 20f, normalizedSpeed);

            SetLocalX(leftArm, swing);
            SetLocalX(rightArm, -swing);
            SetLocalX(leftLeg, -swing);
            SetLocalX(rightLeg, swing);

            if (speed <= 0.08f)
            {
                ReturnToIdle(leftArm, targetReturn);
                ReturnToIdle(rightArm, targetReturn);
                ReturnToIdle(leftLeg, targetReturn);
                ReturnToIdle(rightLeg, targetReturn);
            }

            if (torso != null)
            {
                float bob = speed > 0.08f ? Mathf.Abs(Mathf.Sin(_phase * 2f)) * bobAmount : 0f;
                Vector3 desired = _torsoBasePosition + Vector3.up * bob;
                torso.localPosition = Vector3.Lerp(torso.localPosition, desired, 14f * Time.deltaTime);
            }
        }

        private static void SetLocalX(Transform target, float angle)
        {
            if (target == null)
                return;
            Quaternion desired = Quaternion.Euler(angle, 0f, 0f);
            target.localRotation = Quaternion.Slerp(target.localRotation, desired, 18f * Time.deltaTime);
        }

        private static void ReturnToIdle(Transform target, float speed)
        {
            if (target == null)
                return;
            target.localRotation = Quaternion.Slerp(target.localRotation, Quaternion.identity, speed * Time.deltaTime);
        }
    }
}
