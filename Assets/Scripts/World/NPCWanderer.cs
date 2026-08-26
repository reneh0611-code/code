using CheatOnYourDayOnes.Vehicles;
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

        [Header("Vehicle reaction")]
        [SerializeField] private float carAwarenessRadius = 9f;
        [SerializeField] private float fleeSpeed = 3.8f;
        [SerializeField] private float impactSpeedThreshold = 2.2f;
        [SerializeField] private float lieDownSeconds = 1.15f;
        [SerializeField] private float getUpSeconds = 1.35f;

        private static readonly int IdleHash = Animator.StringToHash("Base Layer.Idle");
        private static readonly int WalkHash = Animator.StringToHash("Base Layer.Walk");
        private static readonly int FallHash = Animator.StringToHash("Base Layer.Fall");
        private static readonly int GettingUpHash = Animator.StringToHash("Base Layer.GettingUp");

        private CharacterController _controller;
        private Vector3 _home;
        private Vector3 _target;
        private float _pause;
        private float _verticalVelocity;
        private bool _walking;
        private float _fallUntil;
        private float _getUpUntil;
        private bool _gettingUp;
        private DriveableCar _dangerCar;

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
            if (animator == null) animator = GetComponentInChildren<Animator>(true);
        }

        private void Start()
        {
            _home = transform.position;
            Pause(Random.Range(0.4f, 2.5f));
        }

        private void Update()
        {
            if (Time.time < _fallUntil)
            {
                ApplyGravityOnly();
                return;
            }

            if (!_gettingUp && _fallUntil > 0f && Time.time >= _fallUntil)
            {
                StartGettingUp();
            }

            if (_gettingUp)
            {
                ApplyGravityOnly();
                if (Time.time >= _getUpUntil)
                {
                    _gettingUp = false;
                    _fallUntil = 0f;
                    if (animator != null && animator.runtimeAnimatorController != null && animator.isActiveAndEnabled && animator.HasState(0, IdleHash))
                        animator.CrossFadeInFixedTime(IdleHash, 0.08f, 0, 0f);
                    Pause(Random.Range(0.15f, 0.45f));
                }
                return;
            }

            FindDangerousCar();
            Vector3 horizontal = Vector3.zero;

            if (_dangerCar != null)
            {
                Vector3 away = transform.position - _dangerCar.transform.position;
                away.y = 0f;
                if (away.sqrMagnitude < 0.01f) away = transform.right;
                away.Normalize();
                Quaternion wanted = Quaternion.LookRotation(away, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, wanted, 1f - Mathf.Exp(-turnSpeed * 1.8f * Time.deltaTime));
                horizontal = away * fleeSpeed;
                SetWalking(true);
            }
            else if (_pause > 0f)
            {
                _pause -= Time.deltaTime;
                if (_pause <= 0f) PickTarget();
            }
            else
            {
                Vector3 toTarget = _target - transform.position;
                toTarget.y = 0f;
                if (toTarget.sqrMagnitude < 0.16f) Pause(Random.Range(minPause, maxPause));
                else
                {
                    Vector3 direction = toTarget.normalized;
                    Quaternion wantedRotation = Quaternion.LookRotation(direction, Vector3.up);
                    transform.rotation = Quaternion.Slerp(transform.rotation, wantedRotation, 1f - Mathf.Exp(-turnSpeed * Time.deltaTime));
                    horizontal = direction * walkSpeed;
                }
            }

            if (_controller.isGrounded && _verticalVelocity < 0f) _verticalVelocity = -2f;
            else _verticalVelocity += gravity * Time.deltaTime;
            _controller.Move((horizontal + Vector3.up * _verticalVelocity) * Time.deltaTime);
        }

        private void StartGettingUp()
        {
            _gettingUp = true;
            _getUpUntil = Time.time + getUpSeconds;
            if (animator != null && animator.runtimeAnimatorController != null && animator.isActiveAndEnabled && animator.HasState(0, GettingUpHash))
                animator.CrossFadeInFixedTime(GettingUpHash, 0.06f, 0, 0f);
        }

        private void ApplyGravityOnly()
        {
            if (!_controller.enabled) return;
            if (_controller.isGrounded && _verticalVelocity < 0f) _verticalVelocity = -2f;
            else _verticalVelocity += gravity * Time.deltaTime;
            _controller.Move(Vector3.up * _verticalVelocity * Time.deltaTime);
        }

        private void FindDangerousCar()
        {
            _dangerCar = null;
            float best = carAwarenessRadius;
            DriveableCar[] cars = Object.FindObjectsByType<DriveableCar>(FindObjectsSortMode.None);
            foreach (DriveableCar car in cars)
            {
                if (car == null || !car.IsOccupied) continue;
                float d = Vector3.Distance(transform.position, car.transform.position);
                if (d < best) { best = d; _dangerCar = car; }
            }
        }

        public void HitByVehicle(Vector3 carVelocity)
        {
            float speed = carVelocity.magnitude;
            if (speed < impactSpeedThreshold || Time.time < _fallUntil || _gettingUp) return;

            _fallUntil = Time.time + lieDownSeconds;
            _getUpUntil = 0f;
            _gettingUp = false;
            _dangerCar = null;
            SetWalking(false);

            if (animator != null && animator.runtimeAnimatorController != null && animator.isActiveAndEnabled && animator.HasState(0, FallHash))
                animator.CrossFadeInFixedTime(FallHash, 0.05f, 0, 0f);

            Debug.Log($"[CYDOY] NPC HIT BY CAR: {name} speed={speed:F1}m/s fall={(animator != null && animator.HasState(0, FallHash))} gettingUp={(animator != null && animator.HasState(0, GettingUpHash))}", this);
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
            if (_walking == walking) return;
            _walking = walking;
            if (animator == null || animator.runtimeAnimatorController == null || !animator.isActiveAndEnabled) return;
            int state = walking ? WalkHash : IdleHash;
            if (animator.HasState(0, state)) animator.CrossFadeInFixedTime(state, 0.10f, 0, 0f);
        }

        public void Configure(float speed, float radius)
        {
            walkSpeed = speed;
            wanderRadius = radius;
        }
    }
}
