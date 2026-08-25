using UnityEngine;

namespace CheatOnYourDayOnes.Player
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class CharacterAnimationDriver : MonoBehaviour
    {
        [SerializeField] private Animator animator;
        [SerializeField] private CharacterController characterController;
        [SerializeField] private float walkThreshold = 0.15f;
        [SerializeField] private float runThreshold = 5.1f;
        [SerializeField] private float crossFadeDuration = 0.12f;

        private static readonly int IdleHash = Animator.StringToHash("Idle");
        private static readonly int WalkHash = Animator.StringToHash("Walk");
        private static readonly int RunHash = Animator.StringToHash("Run");

        private int _currentState;

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
            PlayState(IdleHash, true);
        }

        private void Update()
        {
            if (animator == null || animator.runtimeAnimatorController == null || characterController == null)
                return;

            Vector3 planarVelocity = characterController.velocity;
            planarVelocity.y = 0f;
            float speed = planarVelocity.magnitude;

            int wantedState = speed < walkThreshold
                ? IdleHash
                : speed < runThreshold
                    ? WalkHash
                    : RunHash;

            if (wantedState != _currentState)
                PlayState(wantedState, false);
        }

        private void PlayState(int stateHash, bool immediate)
        {
            _currentState = stateHash;
            animator.speed = 1f;

            if (immediate)
                animator.Play(stateHash, 0, 0f);
            else
                animator.CrossFade(stateHash, crossFadeDuration, 0, 0f);
        }
    }
}
