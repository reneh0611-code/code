using System.Linq;
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
        [SerializeField] private float groundSearchUp = 1.5f;
        [SerializeField] private float groundSearchDown = 4f;
        [SerializeField] private float maxGroundCorrection = 0.45f;

        private static readonly int IdleHash = Animator.StringToHash("Base Layer.Idle");
        private static readonly int WalkHash = Animator.StringToHash("Base Layer.Walk");

        private CharacterController _controller;
        private Renderer[] _renderers;
        private Vector3 _home;
        private Vector3 _target;
        private float _pause;
        private bool _walking;

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
            if (animator == null)
                animator = GetComponentInChildren<Animator>(true);
            _renderers = GetComponentsInChildren<Renderer>(true).Where(r => r != null).ToArray();
        }

        private void Start()
        {
            _home = transform.position;
            Pause(Random.Range(0.4f, 2.5f));
        }

        private void Update()
        {
            if (_pause > 0f)
            {
                _pause -= Time.deltaTime;
                if (_pause <= 0f)
                    PickTarget();
                return;
            }

            Vector3 toTarget = _target - transform.position;
            toTarget.y = 0f;

            if (toTarget.sqrMagnitude < 0.4f * 0.4f)
            {
                Pause(Random.Range(minPause, maxPause));
                return;
            }

            Vector3 direction = toTarget.normalized;
            Quaternion wantedRotation = Quaternion.LookRotation(direction, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, wantedRotation, 1f - Mathf.Exp(-turnSpeed * Time.deltaTime));

            // Horizontal locomotion only. Vertical placement is handled from the visible feet in LateUpdate.
            _controller.Move(direction * walkSpeed * Time.deltaTime);
        }

        private void LateUpdate()
        {
            LockVisibleFeetToGround();
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
                animator.CrossFadeInFixedTime(state, 0.12f, 0, 0f);
        }

        private void LockVisibleFeetToGround()
        {
            if (_renderers == null || _renderers.Length == 0 || _controller == null)
                return;

            Bounds bounds = _renderers[0].bounds;
            for (int i = 1; i < _renderers.Length; i++)
            {
                if (_renderers[i] != null && _renderers[i].enabled)
                    bounds.Encapsulate(_renderers[i].bounds);
            }

            Vector3 origin = new(bounds.center.x, bounds.min.y + groundSearchUp, bounds.center.z);
            RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down, groundSearchUp + groundSearchDown, ~0, QueryTriggerInteraction.Ignore);
            if (hits == null || hits.Length == 0)
                return;

            float bestDistance = float.MaxValue;
            float groundY = 0f;
            bool found = false;

            foreach (RaycastHit hit in hits)
            {
                if (hit.collider == null)
                    continue;

                Transform hitTransform = hit.collider.transform;
                if (hitTransform == transform || hitTransform.IsChildOf(transform))
                    continue;

                if (hit.distance < bestDistance)
                {
                    bestDistance = hit.distance;
                    groundY = hit.point.y;
                    found = true;
                }
            }

            if (!found)
                return;

            float delta = groundY - bounds.min.y;
            delta = Mathf.Clamp(delta, -maxGroundCorrection, maxGroundCorrection);

            if (Mathf.Abs(delta) > 0.0005f)
                _controller.Move(Vector3.up * delta);
        }

        public void Configure(float speed, float radius)
        {
            walkSpeed = speed;
            wanderRadius = radius;
        }
    }
}
