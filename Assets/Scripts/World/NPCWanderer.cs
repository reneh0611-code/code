using UnityEngine;

namespace CheatOnYourDayOnes.World
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class NPCWanderer : MonoBehaviour
    {
        [SerializeField] private Animator animator;
        [SerializeField] private float walkSpeed = 1.35f;
        [SerializeField] private float wanderRadius = 10f;
        [SerializeField] private float turnSpeed = 5f;
        [SerializeField] private float minPause = 1.25f;
        [SerializeField] private float maxPause = 4f;
        [SerializeField] private float gravity = -20f;

        private static readonly int IdleHash = Animator.StringToHash("Base Layer.Idle");
        private static readonly int WalkHash = Animator.StringToHash("Base Layer.Walk");

        private CharacterController _controller;
        private Vector3 _home;
        private Vector3 _target;
        private float _pause;
        private float _verticalVelocity;
        private bool _walking;

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
            if (animator == null)
                animator = GetComponentInChildren<Animator>(true);
        }

        private void Start()
        {
            _home = transform.position;
            Pause(Random.Range(0.4f, 2.5f));
        }

        private void Update()
        {
            Vector3 horizontal = Vector3.zero;

            if (_pause > 0f)
            {
                _pause -= Time.deltaTime;
                if (_pause <= 0f)
                    PickTarget();
            }
            else
            {
                Vector3 toTarget = _target - transform.position;
                toTarget.y = 0f;

                if (toTarget.sqrMagnitude < 0.4f * 0.4f)
                {
                    Pause(Random.Range(minPause, maxPause));
                }
                else
                {
                    Vector3 direction = toTarget.normalized;
                    Quaternion wantedRotation = Quaternion.LookRotation(direction, Vector3.up);
                    transform.rotation = Quaternion.Slerp(transform.rotation, wantedRotation, 1f - Mathf.Exp(-turnSpeed * Time.deltaTime));
                    horizontal = direction * walkSpeed;
                }
            }

            if (_controller.isGrounded && _verticalVelocity < 0f)
                _verticalVelocity = -2f;
            else
                _verticalVelocity += gravity * Time.deltaTime;

            Vector3 motion = horizontal + Vector3.up * _verticalVelocity;
            _controller.Move(motion * Time.deltaTime);
        }

        private void PickTarget()
        {
            Vector2 circle = Random.insideUnitCircle * wanderRadius;
            _target = _home + new Vector3(circle.x, 0f, circle.y);
            SetWalking(true);
        }

        private void Pause(float duration)
        {
            _pause = duration;
            SetWalking(false);
        }

        private void SetWalking(bool walking)
        {
            if (_walking == walking)
                return;

            _walking = walking;
            if (animator == null || animator.runtimeAnimatorController == null || !animator.isActiveAndEnabled)
                return;

            int state = walking ? WalkHash : IdleHash;
            if (animator.HasState(0, state))
                animator.CrossFadeInFixedTime(state, 0.10f, 0, 0f);
        }

        public void Configure(float speed, float radius)
        {
            walkSpeed = speed;
            wanderRadius = radius;
        }
    }
}
