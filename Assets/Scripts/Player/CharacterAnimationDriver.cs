using UnityEngine;

namespace CheatOnYourDayOnes.Player
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class CharacterAnimationDriver : MonoBehaviour
    {
        [SerializeField] private Animator animator;
        [SerializeField] private CharacterController characterController;
        [SerializeField] private RuntimeAnimatorController fallbackController;
        [SerializeField] private float walkThreshold = 0.35f;
        [SerializeField] private float runThreshold = 5.1f;
        [SerializeField] private float crossFadeDuration = 0f;

        private static readonly int IdleHash = Animator.StringToHash("Base Layer.Idle");
        private static readonly int WalkHash = Animator.StringToHash("Base Layer.Walk");
        private static readonly int RunHash = Animator.StringToHash("Base Layer.Run");

        private int _currentState = -1;
        private bool _ready;

        private void Awake()
        {
            if (characterController == null)
                characterController = GetComponent<CharacterController>();

            if (animator == null)
                animator = FindAjAnimator();

            if (fallbackController == null)
                fallbackController = Resources.Load<RuntimeAnimatorController>("AJ_Locomotion");

            if (animator != null && animator.runtimeAnimatorController == null && fallbackController != null)
                animator.runtimeAnimatorController = fallbackController;

            ConfigureAnimator();
        }

        private void Start()
        {
            if (animator == null)
            {
                Debug.LogError("[CYDOY] CharacterAnimationDriver: AJ Animator not found.", this);
                return;
            }

            if (animator.runtimeAnimatorController == null)
            {
                if (fallbackController == null)
                    fallbackController = Resources.Load<RuntimeAnimatorController>("AJ_Locomotion");

                if (fallbackController != null)
                    animator.runtimeAnimatorController = fallbackController;
            }

            if (animator.runtimeAnimatorController == null)
            {
                Debug.LogError("[CYDOY] CharacterAnimationDriver: AJ has no RuntimeAnimatorController.", animator);
                return;
            }

            ConfigureAnimator();
            animator.Rebind();
            animator.Update(0f);

            bool idleExists = animator.HasState(0, IdleHash);
            bool walkExists = animator.HasState(0, WalkHash);
            bool runExists = animator.HasState(0, RunHash);

            if (!idleExists || !walkExists || !runExists)
            {
                Debug.LogError("[CYDOY] AJ AnimatorController is missing Idle, Walk or Run.", animator);
                return;
            }

            _ready = true;
            PlayState(IdleHash);
        }

        private void Update()
        {
            if (!_ready || animator == null || characterController == null)
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
                PlayState(wantedState);
        }

        private void PlayState(int stateHash)
        {
            if (animator == null || !animator.HasState(0, stateHash))
                return;

            animator.speed = 1f;

            // Intentionally no CrossFade: blending would modify the source animation pose.
            animator.Play(stateHash, 0, 0f);
            _currentState = stateHash;
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

        private void ConfigureAnimator()
        {
            if (animator == null)
                return;

            animator.enabled = true;
            animator.speed = 1f;
            animator.applyRootMotion = false;
            animator.updateMode = AnimatorUpdateMode.Normal;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        }
    }
}
