using UnityEngine;

namespace CheatOnYourDayOnes.Player
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class CharacterAnimationDriver : MonoBehaviour
    {
        [SerializeField] private Animator animator;
        [SerializeField] private CharacterController characterController;
        [SerializeField] private float walkReferenceSpeed = 4.2f;
        [SerializeField] private float runReferenceSpeed = 6.8f;
        [SerializeField] private float damping = 0.08f;
        [SerializeField] private float minimumMovingSpeed = 0.08f;

        private static readonly int SpeedHash = Animator.StringToHash("Speed");

        private void Awake()
        {
            if (animator == null)
                animator = GetComponentInChildren<Animator>(true);
            if (characterController == null)
                characterController = GetComponent<CharacterController>();

            if (animator != null)
            {
                animator.enabled = true;
                animator.speed = 1f;
                animator.applyRootMotion = false;
                animator.updateMode = AnimatorUpdateMode.Normal;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            }
        }

        private void Start()
        {
            if (animator == null || animator.runtimeAnimatorController == null)
                return;

            animator.Rebind();
            animator.Update(0f);
            animator.speed = 1f;
        }

        private void Update()
        {
            if (animator == null || animator.runtimeAnimatorController == null || characterController == null)
                return;

            Vector3 planarVelocity = characterController.velocity;
            planarVelocity.y = 0f;
            float speed = planarVelocity.magnitude;

            float blendValue;
            if (speed <= minimumMovingSpeed)
            {
                blendValue = 0f;
            }
            else if (speed <= walkReferenceSpeed)
            {
                blendValue = Mathf.InverseLerp(minimumMovingSpeed, walkReferenceSpeed, speed) * 0.5f;
            }
            else
            {
                blendValue = Mathf.Lerp(0.5f, 1f, Mathf.InverseLerp(walkReferenceSpeed, runReferenceSpeed, speed));
            }

            animator.speed = 1f;
            animator.SetFloat(SpeedHash, blendValue, damping, Time.deltaTime);
        }
    }
}
