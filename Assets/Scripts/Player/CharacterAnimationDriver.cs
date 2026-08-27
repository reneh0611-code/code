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
        [SerializeField] private float idleWalkBlend = 0.10f;
        [SerializeField] private float walkRunBlend = 0.08f;
        [SerializeField] private float idleRunBlend = 0.10f;
        [SerializeField] private float jumpBlend = 0.06f;
        [SerializeField] private float landBlend = 0.10f;

        private static readonly int IdleHash = Animator.StringToHash("Base Layer.Idle");
        private static readonly int WalkHash = Animator.StringToHash("Base Layer.Walk");
        private static readonly int RunHash = Animator.StringToHash("Base Layer.Run");
        private static readonly int JumpHash = Animator.StringToHash("Base Layer.Jump");

        private int _currentState = -1;
        private bool _ready;
        private bool _jumpStateExists;

        private void Awake()
        {
            if (characterController == null)
                characterController = GetComponent<CharacterController>();

            if (fallbackController == null)
                fallbackController = Resources.Load<RuntimeAnimatorController>("Tripo_Locomotion_ExactGeneric");

            RebindToCurrentVisual();
        }

        private void Start()
        {
            RebindToCurrentVisual();
        }

        /// <summary>
        /// Call this whenever CharacterVisual is replaced at runtime. The old implementation cached
        /// the original Animator forever, so selectable characters could punch but never Walk/Run.
        /// </summary>
        public void RebindToCurrentVisual(RuntimeAnimatorController preferredController = null)
        {
            if (characterController == null)
                characterController = GetComponent<CharacterController>();

            Animator newest = FindAnimator();
            if (newest == null)
            {
                _ready = false;
                animator = null;
                Debug.LogWarning("[CYDOY] CharacterAnimationDriver: no Animator currently present; waiting for CharacterVisual.", this);
                return;
            }

            animator = newest;

            RuntimeAnimatorController desired = preferredController != null
                ? preferredController
                : animator.runtimeAnimatorController != null
                    ? animator.runtimeAnimatorController
                    : fallbackController;

            if (desired != null && animator.runtimeAnimatorController != desired)
                animator.runtimeAnimatorController = desired;

            if (animator.runtimeAnimatorController == null)
            {
                _ready = false;
                Debug.LogError("[CYDOY] CharacterAnimationDriver: no RuntimeAnimatorController after rebind.", animator);
                return;
            }

            ConfigureAnimator();
            animator.Rebind();
            animator.Update(0f);

            bool idleExists = animator.HasState(0, IdleHash);
            bool walkExists = animator.HasState(0, WalkHash);
            bool runExists = animator.HasState(0, RunHash);
            _jumpStateExists = animator.HasState(0, JumpHash);

            if (!idleExists || !walkExists || !runExists)
            {
                _ready = false;
                Debug.LogError($"[CYDOY] Locomotion controller invalid after rebind. Idle={idleExists}, Walk={walkExists}, Run={runExists}", animator);
                return;
            }

            _ready = true;
            _currentState = IdleHash;
            animator.Play(IdleHash, 0, 0f);
            animator.Update(0f);
            Debug.Log($"[CYDOY] CharacterAnimationDriver rebound to '{animator.gameObject.name}' using '{animator.runtimeAnimatorController.name}'.", animator);
        }

        private void Update()
        {
            // If a runtime visual swap happened without an explicit rebind, recover automatically.
            if (animator == null || !animator)
            {
                RebindToCurrentVisual();
                if (!_ready) return;
            }

            if (!_ready || characterController == null)
                return;

            if (_jumpStateExists && !characterController.isGrounded)
            {
                if (_currentState != JumpHash)
                    BlendToState(JumpHash, jumpBlend);
                return;
            }

            Vector3 planarVelocity = characterController.velocity;
            planarVelocity.y = 0f;
            float speed = planarVelocity.magnitude;

            int wantedState = speed < walkThreshold
                ? IdleHash
                : speed < runThreshold
                    ? WalkHash
                    : RunHash;

            if (wantedState != _currentState)
            {
                float blend = _currentState == JumpHash ? landBlend : GetBlendDuration(_currentState, wantedState);
                BlendToState(wantedState, blend);
            }
        }

        private void BlendToState(int targetState, float blendDuration)
        {
            if (animator == null || !animator.HasState(0, targetState))
                return;

            animator.speed = 1f;
            if (blendDuration <= 0.001f || _currentState == -1)
                animator.Play(targetState, 0, 0f);
            else
                animator.CrossFadeInFixedTime(targetState, blendDuration, 0, 0f);

            _currentState = targetState;
        }

        private float GetBlendDuration(int from, int to)
        {
            bool idleWalk = (from == IdleHash && to == WalkHash) || (from == WalkHash && to == IdleHash);
            if (idleWalk) return idleWalkBlend;

            bool walkRun = (from == WalkHash && to == RunHash) || (from == RunHash && to == WalkHash);
            if (walkRun) return walkRunBlend;

            bool idleRun = (from == IdleHash && to == RunHash) || (from == RunHash && to == IdleHash);
            if (idleRun) return idleRunBlend;

            return 0f;
        }

        private Animator FindAnimator()
        {
            Transform visual = transform.Find("CharacterVisual");
            if (visual != null)
            {
                Animator current = visual.GetComponentInChildren<Animator>(true);
                if (current != null) return current;
            }

            Animator[] animators = GetComponentsInChildren<Animator>(true);
            return animators.Length > 0 ? animators[0] : null;
        }

        private void ConfigureAnimator()
        {
            if (animator == null) return;
            animator.enabled = true;
            animator.speed = 1f;
            animator.applyRootMotion = false;
            animator.updateMode = AnimatorUpdateMode.Normal;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        }
    }
}
