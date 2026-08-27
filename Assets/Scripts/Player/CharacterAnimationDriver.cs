using UnityEngine;

namespace CheatOnYourDayOnes.Player
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class CharacterAnimationDriver : MonoBehaviour
    {
        [SerializeField] private Animator animator;
        [SerializeField] private CharacterController characterController;
        [SerializeField] private RuntimeAnimatorController fallbackController;

        [Header("Locomotion blending")]
        [SerializeField, Min(0f)] private float idleEnterSpeed = 0.16f;
        [SerializeField, Min(0f)] private float walkEnterSpeed = 0.28f;
        [SerializeField, Min(0f)] private float runEnterSpeed = 4.75f;
        [SerializeField, Min(0f)] private float runExitSpeed = 4.25f;
        [SerializeField, Min(0.01f)] private float speedSharpness = 12f;
        [SerializeField] private float idleWalkBlend = 0.16f;
        [SerializeField] private float walkRunBlend = 0.18f;
        [SerializeField] private float idleRunBlend = 0.20f;
        [SerializeField] private float jumpBlend = 0.10f;
        [SerializeField] private float landBlend = 0.16f;

        [Header("Footstep pace")]
        [SerializeField, Min(0.1f)] private float walkReferenceSpeed = 3.0f;
        [SerializeField, Min(0.1f)] private float runReferenceSpeed = 6.8f;
        [SerializeField] private Vector2 walkPlaybackRange = new(0.78f, 1.18f);
        [SerializeField] private Vector2 runPlaybackRange = new(0.82f, 1.12f);

        private static readonly int IdleHash = Animator.StringToHash("Base Layer.Idle");
        private static readonly int WalkHash = Animator.StringToHash("Base Layer.Walk");
        private static readonly int RunHash = Animator.StringToHash("Base Layer.Run");
        private static readonly int JumpHash = Animator.StringToHash("Base Layer.Jump");

        private NetworkPlayerController _movement;
        private int _currentState = -1;
        private bool _ready;
        private bool _jumpStateExists;
        private float _smoothedSpeed;

        private void Awake()
        {
            if (characterController == null)
                characterController = GetComponent<CharacterController>();
            _movement = GetComponent<NetworkPlayerController>();

            if (fallbackController == null)
                fallbackController = Resources.Load<RuntimeAnimatorController>("Tripo_Locomotion_ExactGeneric");

            RebindToCurrentVisual();
        }

        private void Start() => RebindToCurrentVisual();

        public void RebindToCurrentVisual(RuntimeAnimatorController preferredController = null)
        {
            if (characterController == null)
                characterController = GetComponent<CharacterController>();
            if (_movement == null)
                _movement = GetComponent<NetworkPlayerController>();

            Animator newest = FindAnimator();
            if (newest == null)
            {
                _ready = false;
                animator = null;
                return;
            }

            RuntimeAnimatorController desired = preferredController != null
                ? preferredController
                : newest.runtimeAnimatorController != null
                    ? newest.runtimeAnimatorController
                    : fallbackController;

            bool visualOrControllerChanged = animator != newest || newest.runtimeAnimatorController != desired;
            animator = newest;
            if (desired != null && animator.runtimeAnimatorController != desired)
                animator.runtimeAnimatorController = desired;

            if (animator.runtimeAnimatorController == null)
            {
                _ready = false;
                Debug.LogError("[CYDOY] CharacterAnimationDriver has no RuntimeAnimatorController.", animator);
                return;
            }

            ConfigureAnimator();
            if (visualOrControllerChanged)
            {
                animator.Rebind();
                animator.Update(0f);
            }

            bool idleExists = animator.HasState(0, IdleHash);
            bool walkExists = animator.HasState(0, WalkHash);
            bool runExists = animator.HasState(0, RunHash);
            _jumpStateExists = animator.HasState(0, JumpHash);

            if (!idleExists || !walkExists || !runExists)
            {
                _ready = false;
                Debug.LogError($"[CYDOY] Locomotion controller invalid. Idle={idleExists}, Walk={walkExists}, Run={runExists}", animator);
                return;
            }

            _ready = true;
            _smoothedSpeed = 0f;
            _currentState = IdleHash;
            animator.Play(IdleHash, 0, 0f);
            animator.Update(0f);
        }

        private void OnEnable()
        {
            if (animator != null)
                animator.speed = 1f;
        }

        private void OnDisable()
        {
            if (animator != null)
                animator.speed = 1f;
        }

        public void ResumeFromCombat(float blendDuration = .12f)
        {
            if (characterController == null)
                characterController = GetComponent<CharacterController>();
            if (_movement == null)
                _movement = GetComponent<NetworkPlayerController>();

            if (animator == null || !animator)
                RebindToCurrentVisual();
            if (!_ready || animator == null || !animator.isActiveAndEnabled)
                return;

            bool grounded = _movement != null ? _movement.IsGrounded : characterController != null && characterController.isGrounded;
            Vector3 velocity = _movement != null ? _movement.PlanarVelocity : characterController != null ? characterController.velocity : Vector3.zero;
            velocity.y = 0f;
            _smoothedSpeed = velocity.magnitude;

            int targetState;
            if (_jumpStateExists && !grounded)
                targetState = JumpHash;
            else if (_smoothedSpeed >= runEnterSpeed)
                targetState = RunHash;
            else if (_smoothedSpeed >= walkEnterSpeed)
                targetState = WalkHash;
            else
                targetState = IdleHash;

            animator.speed = 1f;
            animator.CrossFadeInFixedTime(targetState, Mathf.Max(0f, blendDuration), 0, 0f);
            _currentState = targetState;
        }

        private void Update()
        {
            if (animator == null || !animator)
            {
                RebindToCurrentVisual();
                if (!_ready) return;
            }

            if (!_ready || characterController == null)
                return;

            bool grounded = _movement != null ? _movement.IsGrounded : characterController.isGrounded;
            if (_jumpStateExists && !grounded)
            {
                animator.speed = 1f;
                if (_currentState != JumpHash)
                    BlendToState(JumpHash, jumpBlend);
                return;
            }

            Vector3 planarVelocity = _movement != null ? _movement.PlanarVelocity : characterController.velocity;
            planarVelocity.y = 0f;
            float rawSpeed = planarVelocity.magnitude;
            float smoothing = 1f - Mathf.Exp(-speedSharpness * Time.deltaTime);
            _smoothedSpeed = Mathf.Lerp(_smoothedSpeed, rawSpeed, smoothing);

            int wantedState = ChooseLocomotionState(_smoothedSpeed);
            if (wantedState != _currentState)
            {
                float blend = _currentState == JumpHash ? landBlend : GetBlendDuration(_currentState, wantedState);
                BlendToState(wantedState, blend);
            }

            UpdatePlaybackSpeed();
        }

        private int ChooseLocomotionState(float speed)
        {
            if (_currentState == RunHash)
                return speed < runExitSpeed ? (speed < idleEnterSpeed ? IdleHash : WalkHash) : RunHash;

            if (_currentState == IdleHash)
            {
                if (speed < walkEnterSpeed) return IdleHash;
                return speed >= runEnterSpeed ? RunHash : WalkHash;
            }

            if (speed < idleEnterSpeed) return IdleHash;
            if (speed >= runEnterSpeed) return RunHash;
            return WalkHash;
        }

        private void UpdatePlaybackSpeed()
        {
            if (animator == null) return;

            if (_currentState == WalkHash)
            {
                float ratio = _smoothedSpeed / Mathf.Max(0.1f, walkReferenceSpeed);
                animator.speed = Mathf.Clamp(ratio, walkPlaybackRange.x, walkPlaybackRange.y);
            }
            else if (_currentState == RunHash)
            {
                float ratio = _smoothedSpeed / Mathf.Max(0.1f, runReferenceSpeed);
                animator.speed = Mathf.Clamp(ratio, runPlaybackRange.x, runPlaybackRange.y);
            }
            else
            {
                animator.speed = 1f;
            }
        }

        private void BlendToState(int targetState, float blendDuration)
        {
            if (animator == null || !animator.HasState(0, targetState)) return;

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

            return landBlend;
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

            foreach (SkinnedMeshRenderer skin in animator.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                skin.updateWhenOffscreen = false;
                skin.forceMatrixRecalculationPerRender = false;
            }
        }
    }
}
