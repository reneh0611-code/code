using UnityEngine;

namespace CheatOnYourDayOnes.World
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class NPCWanderer : MonoBehaviour
    {
        [SerializeField] private Animator animator;
        [SerializeField] private CharacterController controller;
        [SerializeField] private float walkSpeed = 1.55f;
        [SerializeField] private float turnSpeed = 5.5f;
        [SerializeField] private float wanderRadius = 12f;
        [SerializeField] private float minPause = 1.2f;
        [SerializeField] private float maxPause = 4.5f;
        [SerializeField] private float obstacleProbe = 1.1f;
        [SerializeField] private LayerMask obstacleMask = ~0;

        private static readonly int IdleHash = Animator.StringToHash("Base Layer.Idle");
        private static readonly int WalkHash = Animator.StringToHash("Base Layer.Walk");

        private Vector3 _home;
        private Vector3 _target;
        private float _pauseTimer;
        private float _verticalVelocity;
        private bool _walking;

        private void Awake()
        {
            if (controller == null)
                controller = GetComponent<CharacterController>();
            if (animator == null)
                animator = GetComponentInChildren<Animator>(true);
        }

        private void Start()
        {
            _home = transform.position;
            EnterPause(Random.Range(0.5f, 2.5f));
        }

        private void Update()
        {
            if (_pauseTimer > 0f)
            {
                _pauseTimer -= Time.deltaTime;
                if (_pauseTimer <= 0f)
                    PickNewTarget();
                return;
            }

            Vector3 toTarget = _target - transform.position;
            toTarget.y = 0f;

            if (toTarget.sqrMagnitude < 0.45f * 0.45f)
            {
                EnterPause(Random.Range(minPause, maxPause));
                return;
            }

            Vector3 desired = toTarget.normalized;

            Vector3 probeOrigin = transform.position + Vector3.up * 0.75f;
            if (Physics.Raycast(probeOrigin, desired, out RaycastHit obstacleHit, obstacleProbe, obstacleMask, QueryTriggerInteraction.Ignore))
            {
                if (obstacleHit.transform != transform && !obstacleHit.transform.IsChildOf(transform))
                {
                    desired = Quaternion.Euler(0f, Random.value < 0.5f ? -70f : 70f, 0f) * desired;
                    _target = transform.position + desired * Random.Range(4f, 8f);
                }
            }

            Quaternion targetRotation = Quaternion.LookRotation(desired, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, 1f - Mathf.Exp(-turnSpeed * Time.deltaTime));

            if (controller.isGrounded && _verticalVelocity < 0f)
                _verticalVelocity = -2f;
            _verticalVelocity += Physics.gravity.y * Time.deltaTime;

            Vector3 velocity = desired * walkSpeed;
            velocity.y = _verticalVelocity;
            controller.Move(velocity * Time.deltaTime);
        }

        private void PickNewTarget()
        {
            Vector2 circle = Random.insideUnitCircle * wanderRadius;
            _target = _home + new Vector3(circle.x, 0f, circle.y);
            SetWalking(true);
        }

        private void EnterPause(float duration)
        {
            _pauseTimer = duration;
            SetWalking(false);
        }

        private void SetWalking(bool walking)
        {
            if (_walking == walking)
                return;

            _walking = walking;
            if (animator == null)
                return;

            int target = walking ? WalkHash : IdleHash;
            if (animator.HasState(0, target))
                animator.CrossFadeInFixedTime(target, 0.10f, 0, 0f);
        }

        public void Configure(float speed, float radius)
        {
            walkSpeed = speed;
            wanderRadius = radius;
        }
    }
}
