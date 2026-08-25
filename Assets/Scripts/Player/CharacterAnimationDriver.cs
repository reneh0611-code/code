using UnityEngine;

namespace CheatOnYourDayOnes.Player
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class CharacterAnimationDriver : MonoBehaviour
    {
        [SerializeField] private Animator animator;
        [SerializeField] private CharacterController characterController;
        [SerializeField] private float idleThreshold = 0.35f;
        [SerializeField] private float runThreshold = 5.1f;
        [SerializeField] private float crossFadeDuration = 0.10f;

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
                Debug.LogError("[CYDOY] CharacterAnimationDriver: AJ has no RuntimeAnimatorController.", animator);
                return;
            }

            ConfigureAnimator();
            animator.Rebind();
            animator.Update(0f);

            bool idleExists = animator.HasState(0, IdleHash);
            bool walkExists = animator.HasState(0, WalkHash);
            bool runExists = animator.HasState(0, RunHash);

            Debug.Log(
                $"[CYDOY] AJ Animator ready. Object='{animator.gameObject.name}', " +
                $"Controller='{animator.runtimeAnimatorController.name}', " +
                $"Avatar='{(animator.avatar != null ? animator.avatar.name : "NULL")}', " +
                $"States: Idle={idleExists}, Walk={walkExists}, Run={runExists}",
                animator);

            if (!idleExists || !walkExists || !runExists)
            {
                Debug.LogError(
                    "[CYDOY] AJ AnimatorController is missing one or more required states on Base Layer. " +
                    "Expected: Base Layer.Idle, Base Layer.Walk, Base Layer.Run.",
                    animator);
                return;
            }

            _ready = true;

            // Start absolutely clean in Idle. Do not blend from the FBX bind pose.
            animator.Play(IdleHash, 0, 0f);
            animator.Update(0f);
            _currentState = IdleHash;
            Debug.Log("[CYDOY] AJ state -> IDLE (forced at startup)", animator);
        }

        private void Update()
        {
            if (!_ready || animator == null || characterController == null)
                return;

            Vector3 planarVelocity = characterController.velocity;
            planarVelocity.y = 0f;
            float speed = planarVelocity.magnitude;

            int wantedState;

            // CharacterController.velocity can keep tiny residual horizontal values after stopping.
            // Treat everything below this threshold as a hard idle condition.
            if (speed <= idleThreshold)
                wantedState = IdleHash;
            else if (speed < runThreshold)
                wantedState = WalkHash;
            else
                wantedState = RunHash;

            if (wantedState != _currentState)
                PlayState(wantedState, speed);
        }

        private void PlayState(int stateHash, float speed)
        {
            if (animator == null || !animator.HasState(0, stateHash))
            {
                Debug.LogError($"[CYDOY] Animator state hash {stateHash} does not exist on AJ.", this);
                return;
            }

            animator.speed = 1f;

            if (stateHash == IdleHash)
            {
                // Idle is deliberately forced instead of cross-faded. This prevents a residual
                // locomotion pose from keeping AJ visually in Walk when movement has stopped.
                animator.Play(IdleHash, 0, 0f);
                animator.Update(0f);
                Debug.Log($"[CYDOY] AJ state -> IDLE | speed={speed:F3}", animator);
            }
            else
            {
                animator.CrossFadeInFixedTime(stateHash, crossFadeDuration, 0);

                string label = stateHash == RunHash ? "RUN" : "WALK";
                Debug.Log($"[CYDOY] AJ state -> {label} | speed={speed:F3}", animator);
            }

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
